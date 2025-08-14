using ApiGateway.Models;
using HotChocolate;
using HotChocolate.Types;
using HotChocolate.Authorization;
using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ApiGateway.GraphQL.Subscriptions
{
    [ExtendObjectType("Subscription")]
    public class NotificationSubscription
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<NotificationSubscription> _logger;

        public NotificationSubscription(
            IHttpContextAccessor httpContextAccessor,
            ILogger<NotificationSubscription> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        [Subscribe]
        [Topic("OnNotification")]
        [Authorize(Policy = "AllowAnonymous")]
        public NotificationMessage? OnNotification([EventMessage] NotificationMessage message)
        {
            try
            {
                var userId = GetUserId();

                // Проверяем, есть ли userId в сообщении
                if (message.Data != null && message.Data.TryGetValue("userId", out var messageUserId))
                {
                    // Если в сообщении указан userId, проверяем соответствие
                    if (messageUserId != null && userId != null && !messageUserId.ToString()!.Equals(userId))
                    {
                        _logger.LogInformation("Сообщение отфильтровано. Предназначено для {MessageUserId}, текущий пользователь {UserId}",
                            messageUserId, userId);
                        return null;
                    }
                }
                else
                {
                    // Если в сообщении нет userId, не отправляем его никому
                    _logger.LogInformation("Сообщение отфильтровано. Отсутствует userId в данных сообщения");
                    return null;
                }

                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при фильтрации уведомления");
                return null;
            }
        }

        private string? GetUserId()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null || !httpContext.User.Identity?.IsAuthenticated == true)
            {
                return null;
            }

            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? httpContext.User.FindFirst("sub")?.Value;

            if (userId == null)
            {
                var authHeader = httpContext.Request.Headers["Authorization"].ToString();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                {
                    var token = authHeader.Substring(7);
                    var handler = new JwtSecurityTokenHandler();

                    if (handler.CanReadToken(token))
                    {
                        var jwtToken = handler.ReadJwtToken(token);
                        userId = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
                    }
                }
            }

            return userId;
        }
    }
}