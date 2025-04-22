using HotChocolate.Execution;
using Microsoft.Extensions.Logging;
using HotChocolate.Execution.Instrumentation;
using HotChocolate.AspNetCore;

namespace ApiGateway.GraphQL
{
    public class LoggingDiagnosticEventListener : ExecutionDiagnosticEventListener
    {
        private readonly ILogger<LoggingDiagnosticEventListener> _logger;

        public LoggingDiagnosticEventListener(ILogger<LoggingDiagnosticEventListener> logger)
        {
            _logger = logger;
        }

        public override IDisposable ExecuteRequest(IRequestContext context)
        {
            try
            {
                var operationName = context.Operation?.Name ?? "Anonymous";
                var query = context.Request.Query;

                _logger.LogInformation(
                    "GraphQL запрос: Операция={Operation}, Запрос={Query}",
                    operationName,
                    query
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Ошибка логирования GraphQL запроса: {Error}",
                    ex.Message
                );
            }

            return base.ExecuteRequest(context);
        }

        public override void RequestError(IRequestContext context, Exception exception)
        {
            _logger.LogError(
                exception,
                "GraphQL ошибка запроса: {Error}",
                exception.Message
            );

            base.RequestError(context, exception);
        }
    }
}