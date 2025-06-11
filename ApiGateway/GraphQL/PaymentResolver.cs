using Grpc.Net.Client;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using NextGenVideoAI.PaymentService.Api;
using HotChocolate;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ApiGateway.GraphQL
{
    public class PaymentResolver
    {
        private readonly PaymentService.PaymentServiceClient _client;
        private readonly ILogger<PaymentResolver> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public PaymentResolver(ILogger<PaymentResolver> logger, IHttpContextAccessor httpContextAccessor)
        {
            var paymentServiceUrl = Environment.GetEnvironmentVariable("PAYMENT_SERVICE_URL");
            var channel = GrpcChannel.ForAddress(paymentServiceUrl!, new GrpcChannelOptions
            {
                HttpHandler = new System.Net.Http.SocketsHttpHandler
                {
                    EnableMultipleHttp2Connections = true
                },
                Credentials = ChannelCredentials.Insecure
            });
            _client = new PaymentService.PaymentServiceClient(channel);
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<CreatePaymentPayload> CreatePayment([Argument] CreatePaymentInput input)
        {
            try
            {
                var response = await _client.CreatePaymentAsync(new CreatePaymentRequest
                {
                    UserId = input.UserId,
                    SubscriptionTypeId = input.SubscriptionTypeId,
                    PaymentMethod = input.PaymentMethod
                });
                return new CreatePaymentPayload
                {
                    PaymentId = response.PaymentId,
                    PaymentUrl = response.PaymentUrl,
                    Status = response.Status
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Ошибка при создании платежа: {Detail}", ex.Status.Detail);
                throw;
            }
        }

        public async Task<PaymentWebhookPayload> PaymentWebhook([Argument] PaymentWebhookInput input)
        {
            try
            {
                var response = await _client.PaymentWebhookAsync(new PaymentWebhookRequest
                {
                    PaymentId = input.PaymentId,
                    Status = input.Status,
                    ProviderPayload = input.ProviderPayload
                });
                return new PaymentWebhookPayload { Success = response.Success };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Ошибка при обработке webhook платежа: {Detail}", ex.Status.Detail);
                throw;
            }
        }

        public async Task<GetUserSubscriptionPayload> GetUserSubscription([Argument] string userId)
        {
            try
            {
                var response = await _client.GetUserSubscriptionAsync(new GetUserSubscriptionRequest
                {
                    UserId = userId
                });
                return new GetUserSubscriptionPayload
                {
                    SubscriptionType = response.SubscriptionType,
                    Status = response.Status,
                    ExpiresAt = response.ExpiresAt
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Ошибка при получении подписки пользователя: {Detail}", ex.Status.Detail);
                throw;
            }
        }

        public async Task<GetSubscriptionTypesPayload> GetSubscriptionTypes()
        {
            try
            {
                var response = await _client.GetSubscriptionTypesAsync(new GetSubscriptionTypesRequest());
                var types = new List<SubscriptionTypeInfoDto>();
                foreach (var t in response.Types_)
                {
                    types.Add(new SubscriptionTypeInfoDto
                    {
                        Id = t.Id,
                        Name = t.Name,
                        Description = t.Description,
                        Price = t.Price,
                        DurationDays = t.DurationDays
                    });
                }
                return new GetSubscriptionTypesPayload { Types = types };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Ошибка при получении типов подписок: {Detail}", ex.Status.Detail);
                throw;
            }
        }

        public async Task<UseDemoQuotaPayload> UseDemoQuota([Argument] string userId)
        {
            try
            {
                var response = await _client.UseDemoQuotaAsync(new UseDemoQuotaRequest
                {
                    UserId = userId
                });
                return new UseDemoQuotaPayload
                {
                    DemoQuotaLeft = response.DemoQuotaLeft,
                    Allowed = response.Allowed
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Ошибка при использовании demo-квоты: {Detail}", ex.Status.Detail);
                throw;
            }
        }

        public async Task<GetUserQuotaPayload> GetUserQuota([Argument] string userId)
        {
            try
            {
                var response = await _client.GetUserQuotaAsync(new GetUserQuotaRequest
                {
                    UserId = userId
                });
                return new GetUserQuotaPayload
                {
                    QuotaLeft = response.QuotaLeft
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Ошибка при получении квоты пользователя: {Detail}", ex.Status.Detail);
                throw;
            }
        }
    }
}