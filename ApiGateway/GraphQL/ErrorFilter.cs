using HotChocolate;
using Grpc.Core;

namespace ApiGateway.GraphQL
{
    public class ErrorFilter : IErrorFilter
    {
        public IError OnError(IError error)
        {
            // Обработка gRPC-ошибок
            if (error.Exception is RpcException rpcException)
            {
                return error.WithMessage(rpcException.Status.Detail)
                            .WithCode(rpcException.StatusCode.ToString());
            }

            // Отображаем детали других ошибок
            return error.WithMessage(error.Exception?.Message ?? error.Message);
        }
    }
}