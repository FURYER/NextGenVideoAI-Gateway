using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Types;
using Microsoft.Extensions.Logging;
using Grpc.Core;

namespace ApiGateway.GraphQL
{
    public class ErrorFilter : IErrorFilter
    {
        private readonly ILogger<ErrorFilter> _logger;

        public ErrorFilter(ILogger<ErrorFilter> logger)
        {
            _logger = logger;
        }

        public IError OnError(IError error)
        {
            _logger.LogError(error.Exception, "GraphQL Error: {Message}", error.Message);

            if (error.Exception is UnauthorizedAccessException)
            {
                return ErrorBuilder.New()
                    .SetMessage(error.Message)
                    .SetCode("UNAUTHORIZED")
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