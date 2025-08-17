using System.Security.Claims;

namespace ApiGateway.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string GetCurrentUserId()
        {
            var items = _httpContextAccessor.HttpContext?.Items;
            string? userId = null;
            if (items != null && items.TryGetValue("UserId", out var idObj) && idObj is string idStr && !string.IsNullOrWhiteSpace(idStr))
            {
                userId = idStr;
            }
            else
            {
                userId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;
            }
            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException("Пользователь не авторизован");
            }
            return userId;
        }
    }
}