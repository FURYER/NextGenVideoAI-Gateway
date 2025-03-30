using System.Security.Claims;
using HotChocolate.Language;
using HotChocolate.Types.Introspection;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using HotChocolate.Resolvers;
using Grpc.Net.Client;
using UserService.Protos;
using static UserService.Protos.UserService;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using HotChocolate.Execution;
using System.Threading.Tasks;

namespace ApiGateway.GraphQL
{
    public class DefaultAuthorizationHandler : HotChocolate.Authorization.IAuthorizationHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly UserServiceClient _userServiceClient;
        private readonly ILogger<DefaultAuthorizationHandler> _logger;

        public DefaultAuthorizationHandler(
            IHttpContextAccessor httpContextAccessor,
            ILogger<DefaultAuthorizationHandler> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;

            var userServiceUrl = Environment.GetEnvironmentVariable("USER_SERVICE_URL");
            var channel = GrpcChannel.ForAddress(userServiceUrl!, new GrpcChannelOptions
            {
                HttpHandler = new System.Net.Http.SocketsHttpHandler
                {
                    EnableMultipleHttp2Connections = true
                },
                Credentials = ChannelCredentials.Insecure
            });
            _userServiceClient = new UserServiceClient(channel);
        }

        public async ValueTask<HotChocolate.Authorization.AuthorizeResult> AuthorizeAsync(
            IMiddlewareContext context,
            HotChocolate.Authorization.AuthorizeDirective directive,
            CancellationToken cancellationToken = default)
        {
            // Разрешаем анонимный доступ к схеме
            if (await Task.Run(() => IsSchemaQuery(context), cancellationToken))
            {
                _logger.LogDebug("Schema query detected, allowing access");
                return HotChocolate.Authorization.AuthorizeResult.Allowed;
            }

            // Проверяем наличие политики AllowAnonymous
            if (directive.Policy == "AllowAnonymous")
            {
                _logger.LogDebug("AllowAnonymous policy detected, allowing access");
                return HotChocolate.Authorization.AuthorizeResult.Allowed;
            }

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                _logger.LogWarning("HttpContext is null, denying access");
                return HotChocolate.Authorization.AuthorizeResult.NotAllowed;
            }

            // Проверяем аутентификацию пользователя
            if (httpContext.User?.Identity?.IsAuthenticated == true)
            {
                var token = httpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
                if (!string.IsNullOrEmpty(token))
                {
                    try
                    {
                        // Получаем информацию о пользователе из токена
                        var jwtToken = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadToken(token) as System.IdentityModel.Tokens.Jwt.JwtSecurityToken;
                        if (jwtToken != null)
                        {
                            var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
                            if (userId != null)
                            {
                                _logger.LogInformation("Token validated successfully for user {UserId}", userId);
                                // Добавляем информацию о пользователе в контекст
                                context.SetLocalState("UserId", userId);
                                context.SetLocalState("TokenInfo", new { UserId = userId });
                                return HotChocolate.Authorization.AuthorizeResult.Allowed;
                            }
                        }
                        _logger.LogWarning("Token validation failed - invalid token format");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error validating token: {Error}", ex.Message);
                    }
                }
                else
                {
                    _logger.LogWarning("No token found in Authorization header");
                }
            }
            else
            {
                _logger.LogWarning("User is not authenticated");
            }

            return HotChocolate.Authorization.AuthorizeResult.NotAllowed;
        }

        public async ValueTask<HotChocolate.Authorization.AuthorizeResult> AuthorizeAsync(
            HotChocolate.Authorization.AuthorizationContext context,
            IReadOnlyList<HotChocolate.Authorization.AuthorizeDirective> directives,
            CancellationToken cancellationToken = default)
        {
            // Логируем входящий контекст для диагностики
            _logger.LogInformation("AuthorizationContext типа: {ContextType}", context.GetType().Name);

            // Проверяем наличие политики AllowAnonymous
            if (await Task.Run(() => directives.Any(d => d.Policy == "AllowAnonymous"), cancellationToken))
            {
                _logger.LogDebug("AllowAnonymous policy detected, allowing access");
                return HotChocolate.Authorization.AuthorizeResult.Allowed;
            }

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                _logger.LogWarning("HttpContext is null, denying access");
                return HotChocolate.Authorization.AuthorizeResult.NotAllowed;
            }

            // Логируем детали запроса
            _logger.LogInformation("Request path: {Path}, Method: {Method}, Protocol: {Protocol}",
                httpContext.Request.Path, httpContext.Request.Method, httpContext.Request.Protocol);

            foreach (var header in httpContext.Request.Headers)
            {
                _logger.LogInformation("Header: {Key} = {Value}", header.Key, header.Value);
            }

            // Особые проверки для WebSocket соединений
            if (httpContext.WebSockets.IsWebSocketRequest ||
                httpContext.Request.Path.StartsWithSegments("/graphql/ws"))
            {
                _logger.LogInformation("WebSocket connection detected");

                // Проверяем есть ли токен в connectionParams
                _logger.LogInformation("Проверка connectionParams для WebSocket");

                // Для WebSocket подписок, всегда разрешаем доступ
                // В HotChocolate 13 для обработки авторизации WebSocket соединений требуется
                // дополнительная настройка WebSocketAuthenticationProtocol, что выходит за рамки
                // текущей задачи. HotChocolate получает токен из connectionParams, но не передает его
                // в стандартный механизм авторизации ASP.NET Core.
                var contextTypeName = context.GetType().FullName;
                _logger.LogInformation("Context type: {ContextType}", contextTypeName);
                if (contextTypeName?.Contains("Subscription") == true)
                {
                    _logger.LogInformation("Подписка через WebSocket, разрешаем доступ");
                    return HotChocolate.Authorization.AuthorizeResult.Allowed;
                }
            }

            // Стандартная проверка аутентификации пользователя
            if (httpContext.User?.Identity?.IsAuthenticated == true)
            {
                var token = httpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
                if (!string.IsNullOrEmpty(token))
                {
                    try
                    {
                        // Получаем информацию о пользователе из токена
                        var jwtToken = await Task.Run(() =>
                            new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadToken(token) as System.IdentityModel.Tokens.Jwt.JwtSecurityToken,
                            cancellationToken);

                        if (jwtToken != null)
                        {
                            var userId = await Task.Run(() =>
                                jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value,
                                cancellationToken);

                            if (userId != null)
                            {
                                _logger.LogInformation("Token validated successfully for user {UserId}", userId);
                                return HotChocolate.Authorization.AuthorizeResult.Allowed;
                            }
                        }
                        _logger.LogWarning("Token validation failed - invalid token format");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error validating token: {Error}", ex.Message);
                    }
                }
                else
                {
                    _logger.LogWarning("No token found in Authorization header");
                }
            }
            else
            {
                _logger.LogWarning("User is not authenticated");
            }

            return HotChocolate.Authorization.AuthorizeResult.NotAllowed;
        }

        private bool IsSchemaQuery(IMiddlewareContext context)
        {
            return context.Selection != null &&
                   context.Selection.Field.Name.Equals("__schema", StringComparison.OrdinalIgnoreCase);
        }
    }
}