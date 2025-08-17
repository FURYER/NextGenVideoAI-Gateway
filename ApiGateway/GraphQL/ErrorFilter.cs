using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Types;
using Microsoft.Extensions.Logging;
using Grpc.Core;
using Microsoft.AspNetCore.Http;

namespace ApiGateway.GraphQL
{
    public class ErrorFilter : IErrorFilter
    {
        private readonly ILogger<ErrorFilter> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ErrorFilter(ILogger<ErrorFilter> logger, IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public IError OnError(IError error)
        {
            _logger.LogError(error.Exception, "GraphQL Error: {Message}", error.Message);

            // Проставляем корректные коды для клиента, чтобы инициировать refresh
            var httpContext = _httpContextAccessor.HttpContext;
            var authCode = httpContext?.Items?["AuthErrorCode"] as string;
            if (!string.IsNullOrWhiteSpace(authCode))
            {
                if (authCode == "UNAUTHENTICATED")
                {
                    if (httpContext != null)
                    {
                        httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    }
                    return ErrorBuilder.New()
                        .SetMessage("Unauthorized")
                        .SetCode("UNAUTHENTICATED")
                        .SetPath(error.Path)
                        .Build();
                }
                if (authCode == "AUTH_NOT_AUTHORIZED")
                {
                    if (httpContext != null)
                    {
                        httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    }
                    return ErrorBuilder.New()
                        .SetMessage("Not authorized")
                        .SetCode("AUTH_NOT_AUTHORIZED")
                        .SetPath(error.Path)
                        .Build();
                }
            }

            if (error.Exception is UnauthorizedAccessException)
            {
                return ErrorBuilder.New()
                    .SetMessage(error.Message)
                    .SetCode("UNAUTHENTICATED")
                    .SetPath(error.Path)
                    .Build();
            }

            if (error.Exception is RpcException rpcException)
            {
                return ErrorBuilder.New()
                    .SetMessage(rpcException.Status.Detail ?? "Ошибка при выполнении запроса")
                    .SetCode("RPC_ERROR")
                    .SetPath(error.Path)
                    .Build();
            }

            return error;
        }
    }
}