using System.Security.Claims;
using HotChocolate.Language;
using HotChocolate.Types.Introspection;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using HotChocolate.Resolvers;

namespace ApiGateway.GraphQL
{
    public class DefaultAuthorizationHandler : HotChocolate.Authorization.IAuthorizationHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DefaultAuthorizationHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public ValueTask<HotChocolate.Authorization.AuthorizeResult> AuthorizeAsync(
            IMiddlewareContext context,
            HotChocolate.Authorization.AuthorizeDirective directive,
            CancellationToken cancellationToken = default)
        {
            // Разрешаем анонимный доступ к схеме
            if (IsSchemaQuery(context))
            {
                return new ValueTask<HotChocolate.Authorization.AuthorizeResult>(
                    HotChocolate.Authorization.AuthorizeResult.Allowed);
            }

            // Проверяем наличие политики AllowAnonymous
            if (directive.Policy == "AllowAnonymous")
            {
                return new ValueTask<HotChocolate.Authorization.AuthorizeResult>(
                    HotChocolate.Authorization.AuthorizeResult.Allowed);
            }

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User?.Identity?.IsAuthenticated == true)
            {
                return new ValueTask<HotChocolate.Authorization.AuthorizeResult>(
                    HotChocolate.Authorization.AuthorizeResult.Allowed);
            }

            return new ValueTask<HotChocolate.Authorization.AuthorizeResult>(
                HotChocolate.Authorization.AuthorizeResult.NotAllowed);
        }

        public ValueTask<HotChocolate.Authorization.AuthorizeResult> AuthorizeAsync(
            HotChocolate.Authorization.AuthorizationContext context,
            IReadOnlyList<HotChocolate.Authorization.AuthorizeDirective> directives,
            CancellationToken cancellationToken = default)
        {
            // Всегда разрешаем доступ к схеме
            return new ValueTask<HotChocolate.Authorization.AuthorizeResult>(
                HotChocolate.Authorization.AuthorizeResult.Allowed);
        }

        private bool IsSchemaQuery(IMiddlewareContext context)
        {
            return context.Selection != null &&
                   context.Selection.Field.Name.Equals("__schema", StringComparison.OrdinalIgnoreCase);
        }
    }
}