// ApiGateway/GraphQL/UserResolver.cs
using Grpc.Net.Client;
using HotChocolate;
using UserService.Protos;
using static UserService.Protos.UserService;
using Grpc.Core;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ApiGateway.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ApiGateway.GraphQL
{
    public class UserResolver
    {
        private readonly UserServiceClient _client;
        private readonly ITwoFactorAuthService _twoFactorAuthService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<UserResolver> _logger;

        public UserResolver(
            ITwoFactorAuthService twoFactorAuthService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<UserResolver> logger)
        {
            var userServiceUrl = Environment.GetEnvironmentVariable("USER_SERVICE_URL");

            var channel = GrpcChannel.ForAddress(userServiceUrl!, new GrpcChannelOptions
            {
                HttpHandler = new System.Net.Http.SocketsHttpHandler
                {
                    EnableMultipleHttp2Connections = true
                },
                Credentials = ChannelCredentials.Insecure
            });
            _client = new UserServiceClient(channel);
            _twoFactorAuthService = twoFactorAuthService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        private string GetDeviceInfo()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return "Unknown";

            var userAgent = context.Request.Headers["User-Agent"].ToString();
            var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            return $"{userAgent} ({ipAddress})";
        }

        private string GetIpAddress()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return "Unknown";

            return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }

        public async Task<AuthResponse> RegisterUser([Argument] RegisterUserInput input)
        {
            try
            {
                _logger.LogInformation("Attempting to register user with username: {Username}", input.Username);

                var response = await _client.RegisterUserAsync(new RegisterUserRequest
                {
                    Username = input.Username,
                    Password = input.Password,
                    DeviceInfo = GetDeviceInfo(),
                    IpAddress = GetIpAddress()
                });

                // Генерируем секрет для 2FA
                var twoFactorSecret = _twoFactorAuthService.GenerateSecret();

                _logger.LogInformation("User successfully registered with ID: {UserId}", response.UserId);

                return new AuthResponse
                {
                    Success = true,
                    Message = "Пользователь успешно зарегистрирован",
                    UserId = response.UserId,
                    AccessToken = response.AccessToken,
                    RefreshToken = response.RefreshToken,
                    TwoFactorSecret = twoFactorSecret,
                    Username = response.TokenInfo.Username
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error registering user: {Detail}", ex.Status.Detail);
                return new AuthResponse
                {
                    Success = false,
                    Message = ex.Status.Detail ?? "Ошибка при регистрации пользователя"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during user registration");
                return new AuthResponse
                {
                    Success = false,
                    Message = "Внутренняя ошибка сервера при регистрации пользователя"
                };
            }
        }

        public async Task<AuthResponse> Login([Argument] LoginInput input)
        {
            try
            {
                _logger.LogInformation("Attempting to login user with username: {Username}", input.Username);

                if (input == null)
                {
                    _logger.LogWarning("Login attempt with null input");
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Неверные входные данные"
                    };
                }

                if (string.IsNullOrEmpty(input.Username) || string.IsNullOrEmpty(input.Password))
                {
                    _logger.LogWarning("Login attempt with empty username or password");
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Имя пользователя и пароль не могут быть пустыми"
                    };
                }

                var response = await _client.LoginAsync(new LoginRequest
                {
                    Username = input.Username,
                    Password = input.Password,
                    DeviceInfo = GetDeviceInfo(),
                    IpAddress = GetIpAddress(),
                });

                // Проверяем 2FA код, если он предоставлен
                if (!string.IsNullOrEmpty(input.TwoFactorCode))
                {
                    if (string.IsNullOrEmpty(input.TwoFactorSecret))
                    {
                        _logger.LogWarning("2FA code provided but no secret");
                        return new AuthResponse
                        {
                            Success = false,
                            Message = "Необходим секретный ключ для 2FA"
                        };
                    }

                    if (!_twoFactorAuthService.ValidateCode(input.TwoFactorSecret, input.TwoFactorCode))
                    {
                        _logger.LogWarning("Invalid 2FA code for user: {Username}", input.Username);
                        return new AuthResponse
                        {
                            Success = false,
                            Message = "Неверный код двухфакторной аутентификации"
                        };
                    }
                }

                _logger.LogInformation("User successfully logged in: {Username}", input.Username);
                _logger.LogInformation("Generated access token: {Token}", response.AccessToken);
                _logger.LogInformation("Generated refresh token: {Token}", response.RefreshToken);
                _logger.LogInformation("Generated email: {Email}", response.TokenInfo.Email);

                return new AuthResponse
                {
                    Success = true,
                    Message = "Успешный вход в систему",
                    UserId = response.UserId,
                    AccessToken = response.AccessToken,
                    RefreshToken = response.RefreshToken,
                    Username = response.TokenInfo.Username,
                    Email = response.TokenInfo.Email
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "RPC error during login: {Detail}", ex.Status.Detail);
                return new AuthResponse
                {
                    Success = false,
                    Message = ex.Status.Detail ?? "Ошибка при входе в систему"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during login");
                return new AuthResponse
                {
                    Success = false,
                    Message = "Внутренняя ошибка сервера при входе в систему"
                };
            }
        }

        public async Task<AuthResponse> RefreshToken([Argument] string refreshToken)
        {
            try
            {
                // Проверяем refresh токен через UserService
                var response = await _client.RefreshTokenAsync(new RefreshTokenRequest
                {
                    RefreshToken = refreshToken
                });
                return new AuthResponse
                {
                    Success = true,
                    Message = "Токен успешно обновлен",
                    AccessToken = response.AccessToken,
                    RefreshToken = response.RefreshToken,
                    Username = response.TokenInfo.Username,
                    UserId = response.TokenInfo.UserId
                };
            }
            catch (RpcException ex)
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = ex.Status.Detail ?? "Ошибка при обновлении токена"
                };
            }
        }

        public async Task<bool> Logout([GlobalState] string? accessToken)
        {
            if (!string.IsNullOrEmpty(accessToken))
            {
                var jwtToken = new JwtSecurityTokenHandler().ReadToken(accessToken) as JwtSecurityToken;
                if (jwtToken != null)
                {
                    var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == "userId")?.Value;
                    if (userId != null)
                    {
                        try
                        {
                            var response = await _client.RevokeTokenAsync(new RevokeTokenRequest
                            {
                                Token = accessToken,
                                UserId = userId
                            });
                            return response.Success;
                        }
                        catch
                        {
                            return false;
                        }
                    }
                }
            }
            return false;
        }

        public async Task<UserProfileResponse> GetUserProfile([Argument] string userId)
        {
            try
            {
                _logger.LogInformation("Получение профиля пользователя с ID: {UserId}", userId);

                var response = await _client.GetUserProfileAsync(new GetUserProfileRequest
                {
                    UserId = userId
                });

                return new UserProfileResponse
                {
                    Success = true,
                    Message = "Профиль пользователя успешно получен",
                    Profile = new UserProfileDto
                    {
                        UserId = response.Profile.UserId,
                        Username = response.Profile.Username,
                        Email = response.Profile.Email,
                        CreatedAt = response.Profile.CreatedAt
                    }
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Ошибка при получении профиля пользователя: {Detail}", ex.Status.Detail);
                return new UserProfileResponse
                {
                    Success = false,
                    Message = ex.Status.Detail ?? "Ошибка при получении профиля пользователя"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Непредвиденная ошибка при получении профиля пользователя");
                return new UserProfileResponse
                {
                    Success = false,
                    Message = "Внутренняя ошибка сервера при получении профиля пользователя"
                };
            }
        }

        // Методы для работы с email-кодом
        public async Task<EmailCodePayload> RequestEmailCode([Argument] EmailCodeInput input)
        {
            try
            {
                var response = await _client.RequestEmailCodeAsync(new RequestEmailCodeRequest
                {
                    UserId = input.UserId ?? string.Empty,
                    Email = input.Email ?? string.Empty
                });
                return new EmailCodePayload { Success = response.Success };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Ошибка при запросе email-кода: {Detail}", ex.Status.Detail);
                return new EmailCodePayload { Success = false };
            }
        }

        public async Task<EmailCodePayload> ConfirmEmailCode([Argument] ConfirmEmailCodeInput input)
        {
            try
            {
                var response = await _client.ConfirmEmailCodeAsync(new ConfirmEmailCodeRequest
                {
                    UserId = input.UserId ?? string.Empty,
                    Email = input.Email ?? string.Empty,
                    Code = input.Code ?? string.Empty
                });
                return new EmailCodePayload { Success = response.Success };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Ошибка при подтверждении email-кода: {Detail}", ex.Status.Detail);
                return new EmailCodePayload { Success = false };
            }
        }
    }
}