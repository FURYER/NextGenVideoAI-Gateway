// ApiGateway/GraphQL/UserResolver.cs
using Grpc.Net.Client;
using HotChocolate;
using UserService.Protos;
using static UserService.Protos.UserService;
using Grpc.Core;

namespace ApiGateway.GraphQL
{
    public class UserResolver
    {
        private readonly UserServiceClient _client;

        public UserResolver()
        {
            var userServiceUrl = Environment.GetEnvironmentVariable("USER_SERVICE_URL");
            if (string.IsNullOrEmpty(userServiceUrl))
            {
                throw new ArgumentNullException("USER_SERVICE_URL environment variable is not set");
            }

            var channel = GrpcChannel.ForAddress(userServiceUrl, new GrpcChannelOptions
            {
                HttpHandler = new System.Net.Http.SocketsHttpHandler
                {
                    EnableMultipleHttp2Connections = true
                },
                Credentials = ChannelCredentials.Insecure // Разрешаем незащищенное соединение
            });
            _client = new UserServiceClient(channel);
        }

        public async Task<AuthResponse> RegisterUser([Argument] RegisterUserInput input)
        {
            try
            {
                var response = await _client.RegisterUserAsync(new RegisterUserRequest
                {
                    Username = input.Username,
                    Email = input.Email,
                    Password = input.Password
                });

                return new AuthResponse
                {
                    Success = true,
                    Message = "Пользователь успешно зарегистрирован",
                    UserId = response.UserId,
                    Token = response.Token
                };
            }
            catch (RpcException ex)
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = ex.Status.Detail ?? "Ошибка при регистрации пользователя"
                };
            }
        }

        public async Task<AuthResponse> Login([Argument] LoginInput input)
        {
            try
            {
                var response = await _client.LoginAsync(new LoginRequest
                {
                    Email = input.Email,
                    Password = input.Password
                });

                return new AuthResponse
                {
                    Success = true,
                    Message = "Успешный вход в систему",
                    UserId = response.UserId,
                    Token = response.Token
                };
            }
            catch (RpcException ex)
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = ex.Status.Detail ?? "Ошибка при входе в систему"
                };
            }
        }

        public async Task<User> GetUserById([Argument] string userId)
        {
            // Валидация userId
            if (!Guid.TryParse(userId, out _))
            {
                throw new ArgumentException("Invalid user_id format. Must be a valid UUID.");
            }

            var response = await _client.GetUserByIdAsync(new GetUserByIdRequest { UserId = userId });
            return new User
            {
                Id = response.UserId,
                Username = response.Username,
                Email = response.Email,
                Credits = response.Credits
            };
        }

        public async Task<double> UpdateCredits([Argument] UpdateCreditsInput input)
        {
            // Валидация userId
            if (!Guid.TryParse(input.UserId, out _))
            {
                throw new ArgumentException("Invalid user_id format. Must be a valid UUID.");
            }

            var response = await _client.UpdateCreditsAsync(new UpdateCreditsRequest
            {
                UserId = input.UserId,
                Amount = (float)input.Amount
            });
            return response.NewBalance;
        }
    }
}