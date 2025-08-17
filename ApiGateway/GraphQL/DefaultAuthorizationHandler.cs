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
            _logger.LogWarning("AuthorizeAsync: directive.Policy = {Policy}", directive.Policy);
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

            // Валидируем ТОЛЬКО через UserService (игнорируем состояние ASP.NET аутентификации)
            string token = string.Empty;
            if (httpContext.Items.TryGetValue("WebSocketAuthToken", out var wsTokenObj) && wsTokenObj is string wsToken && !string.IsNullOrWhiteSpace(wsToken))
            {
                token = wsToken;
            }
            else
            {
                token = httpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("No token provided for authorization");
                httpContext.Items["AuthErrorCode"] = "UNAUTHENTICATED";
                return HotChocolate.Authorization.AuthorizeResult.NotAllowed;
            }

            try
            {
                var validateResponse = await _userServiceClient.ValidateTokenAsync(new ValidateTokenRequest { Token = token });
                if (!validateResponse.IsValid)
                {
                    _logger.LogWarning("[JWT] Token is NOT valid in UserService: {Token}", token);
                    httpContext.Items["AuthErrorCode"] = "UNAUTHENTICATED";
                    return HotChocolate.Authorization.AuthorizeResult.NotAllowed;
                }

                _logger.LogInformation("Token validated successfully for user {UserId}", validateResponse.UserId);
                // Сохраняем идентификатор пользователя для последующего использования
                context.SetLocalState("UserId", validateResponse.UserId);
                context.SetLocalState("TokenInfo", validateResponse.TokenInfo);
                httpContext.Items["UserId"] = validateResponse.UserId;
                httpContext.Items["TokenInfo"] = validateResponse.TokenInfo;

                // Проверка наличия email
                var allowedWithoutEmail = new[] { "login", "registerUser", "requestEmailCode", "confirmEmailCode", "refreshToken" };
                var fieldName = context.Selection?.Field?.Name;
                if (string.IsNullOrEmpty(validateResponse.TokenInfo.Email) && (fieldName == null || !allowedWithoutEmail.Contains(fieldName, StringComparer.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning("User {UserId} has no email and tried to access {FieldName}", validateResponse.UserId, fieldName);
                    httpContext.Items["AuthErrorCode"] = "AUTH_NOT_AUTHORIZED";
                    return HotChocolate.Authorization.AuthorizeResult.NotAllowed;
                }

                return HotChocolate.Authorization.AuthorizeResult.Allowed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token via UserService: {Error}", ex.Message);
                httpContext.Items["AuthErrorCode"] = "UNAUTHENTICATED";
                return HotChocolate.Authorization.AuthorizeResult.NotAllowed;
            }
        }

        public async ValueTask<HotChocolate.Authorization.AuthorizeResult> AuthorizeAsync(
            HotChocolate.Authorization.AuthorizationContext context,
            IReadOnlyList<HotChocolate.Authorization.AuthorizeDirective> directives,
            CancellationToken cancellationToken = default)
        {
            var policies = (directives ?? Array.Empty<HotChocolate.Authorization.AuthorizeDirective>())
                .Select(d => d.Policy)
                .OfType<string>()
                .ToList();
            _logger.LogWarning("AuthorizeAsync: директивы = {Policies}", string.Join(",", policies));
            // Логируем входящий контекст для диагностики
            _logger.LogInformation("AuthorizationContext типа: {ContextType}", context.GetType().Name);

            // Проверяем наличие политики AllowAnonymous
            if (directives != null && await Task.Run(() => directives.Any(d => d.Policy == "AllowAnonymous"), cancellationToken))
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

            // Особые проверки для WebSocket соединений: больше не разрешаем без токена
            if (httpContext.WebSockets.IsWebSocketRequest || httpContext.Request.Path.StartsWithSegments("/graphql/ws"))
            {
                _logger.LogInformation("WebSocket connection detected");
            }

            // Валидируем ТОЛЬКО через UserService (игнорируем состояние ASP.NET аутентификации)
            string token = string.Empty;
            if (httpContext.Items.TryGetValue("WebSocketAuthToken", out var wsTokenObj) && wsTokenObj is string wsToken && !string.IsNullOrWhiteSpace(wsToken))
            {
                token = wsToken;
            }
            else
            {
                token = httpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("No token provided for authorization");
                httpContext.Items["AuthErrorCode"] = "UNAUTHENTICATED";
                return HotChocolate.Authorization.AuthorizeResult.NotAllowed;
            }

            try
            {
                var validateResponse = await _userServiceClient.ValidateTokenAsync(new ValidateTokenRequest { Token = token });
                if (!validateResponse.IsValid)
                {
                    _logger.LogWarning("[JWT] Token is NOT valid in UserService: {Token}", token);
                    httpContext.Items["AuthErrorCode"] = "UNAUTHENTICATED";
                    return HotChocolate.Authorization.AuthorizeResult.NotAllowed;
                }

                _logger.LogInformation("Token validated successfully for user {UserId}", validateResponse.UserId);
                httpContext.Items["UserId"] = validateResponse.UserId;
                httpContext.Items["TokenInfo"] = validateResponse.TokenInfo;
                return HotChocolate.Authorization.AuthorizeResult.Allowed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token via UserService: {Error}", ex.Message);
                httpContext.Items["AuthErrorCode"] = "UNAUTHENTICATED";
                return HotChocolate.Authorization.AuthorizeResult.NotAllowed;
            }
        }

        private bool IsSchemaQuery(IMiddlewareContext context)
        {
            return context.Selection != null &&
                   context.Selection.Field.Name.Equals("__schema", StringComparison.OrdinalIgnoreCase);
        }
    }
}