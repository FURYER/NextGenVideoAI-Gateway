using HotChocolate.Execution;
using Microsoft.Extensions.Logging;
using HotChocolate.Language;
using HotChocolate.Types;
using HotChocolate.Execution.Instrumentation;

namespace ApiGateway.GraphQL
{
    public class GraphQLDiagnosticEventListener : ExecutionDiagnosticEventListener
    {
        private readonly ILogger<GraphQLDiagnosticEventListener> _logger;

        public GraphQLDiagnosticEventListener(ILogger<GraphQLDiagnosticEventListener> logger)
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
                    "GraphQL Request: Operation={Operation}, Query={Query}",
                    operationName,
                    query
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error logging GraphQL request: {Error}",
                    ex.Message
                );
            }

            return base.ExecuteRequest(context);
        }
    }
}