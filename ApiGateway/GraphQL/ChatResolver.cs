using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NextGenVideoAI.ChatService.Api;
using System.IdentityModel.Tokens.Jwt;

namespace ApiGateway.GraphQL
{
    public class ChatResolver
    {
        private readonly ChatService.ChatServiceClient _client;
        private readonly ILogger<ChatResolver> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private static readonly Dictionary<string, bool> _activeSubscriptions = new();

        public ChatResolver(
            ILogger<ChatResolver> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            var chatServiceUrl = Environment.GetEnvironmentVariable("CHAT_SERVICE_URL");

            var channel = GrpcChannel.ForAddress(chatServiceUrl!, new GrpcChannelOptions
            {
                HttpHandler = new System.Net.Http.SocketsHttpHandler
                {
                    EnableMultipleHttp2Connections = true
                },
                Credentials = ChannelCredentials.Insecure
            });
            _client = new ChatService.ChatServiceClient(channel);
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<Chat> CreateChat(string userId)
        {
            try
            {
                if (!Guid.TryParse(userId, out Guid userGuid))
                {
                    throw new ArgumentException("Invalid user ID format", nameof(userId));
                }

                _logger.LogInformation("Создание нового чата для пользователя {UserId}", userId);
                var request = new CreateChatRequest
                {
                    UserId = userGuid.ToString(),
                    ChatContext = ""
                };
                var response = await _client.CreateChatAsync(request);

                _logger.LogInformation("Создан новый чат {ChatId} для пользователя {UserId}", response.ChatId, userId);
                return new Chat
                {
                    Id = response.ChatId,
                    Title = response.Name,
                    LastUpdate = DateTime.Parse(response.LastUpdate),
                    Participants = new List<Participant>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании чата для пользователя {UserId}: {Error}", userId, ex.Message);
                throw;
            }
        }

        public async Task<Chat?> GetChatById(string chatId)
        {
            try
            {
                _logger.LogInformation("Получение чата по ID {ChatId}", chatId);
                var request = new GetChatByIdRequest
                {
                    ChatId = chatId
                };
                var response = await _client.GetChatByIdAsync(request);

                if (response == null || string.IsNullOrEmpty(response.Id))
                {
                    _logger.LogWarning("Чат с ID {ChatId} не найден", chatId);
                    return null;
                }

                _logger.LogInformation("Получен чат {ChatId} с названием {Title}", response.Id, response.Name);
                return new Chat
                {
                    Id = response.Id,
                    Title = response.Name,
                    LastUpdate = DateTime.Parse(response.LastUpdate),
                    Participants = new List<Participant>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении чата с ID {ChatId}", chatId);
                throw;
            }
        }

        public async Task<IEnumerable<Chat>> GetChats(string userId, int? skip = null, int? limit = null)
        {
            try
            {
                _logger.LogInformation("Получение чатов для пользователя {0} с параметрами skip={1}, limit={2}", userId, skip, limit);
                var request = new GetUserChatsRequest
                {
                    UserId = userId,
                    Skip = skip ?? 0,
                    Limit = limit ?? 100
                };
                var response = await _client.GetUserChatsAsync(request);

                _logger.LogInformation("Получено {Count} чатов для пользователя {UserId}", response.Chats.Count, userId);
                return response.Chats.Select(chat => new Chat
                {
                    Id = chat.Id,
                    Title = chat.Name,
                    LastUpdate = DateTime.Parse(chat.LastUpdate),
                    Participants = new List<Participant>()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении чатов для пользователя {0}", userId);
                throw;
            }
        }

        public async Task<IEnumerable<ChatMessage>> GetChatMessages(string chatId, int? skip = null, int? limit = null)
        {
            try
            {
                _logger.LogInformation("Получение сообщений для чата {ChatId} с параметрами skip={1}, limit={2}", chatId, skip, limit);
                var request = new GetChatMessagesRequest
                {
                    ChatId = chatId,
                    Skip = skip ?? 0,
                    Limit = limit ?? 100
                };
                var response = await _client.GetChatMessagesAsync(request);

                _logger.LogInformation("Получено {Count} сообщений для чата {ChatId}", response.Messages.Count, chatId);
                return response.Messages.Select(msg => new ChatMessage
                {
                    Id = msg.Id,
                    Content = msg.Content,
                    CreatedAt = DateTime.Parse(msg.CreatedAt),
                    SenderType = (SenderType)msg.SenderType,
                    SenderId = msg.SenderId,
                    ChatId = chatId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении сообщений для чата {ChatId}", chatId);
                throw;
            }
        }

        public async Task<ChatMessage> SendMessage(string chatId, string content, string userId)
        {
            try
            {
                _logger.LogInformation("Отправка сообщения в чат {ChatId} от пользователя {UserId}", chatId, userId);
                var request = new SendMessageRequest
                {
                    ChatId = chatId,
                    Content = content,
                    SenderId = userId,
                    SenderType = (NextGenVideoAI.ChatService.Api.SenderType)SenderType.User
                };

                var response = await _client.SendMessageAsync(request);

                _logger.LogInformation("Сообщение {MessageId} успешно отправлено в чат {ChatId}", response.MessageId, chatId);
                return new ChatMessage
                {
                    Id = response.MessageId,
                    Content = response.Content,
                    CreatedAt = DateTime.Parse(response.CreatedAt),
                    SenderType = SenderType.User,
                    SenderId = userId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке сообщения в чат {ChatId}: {Error}", chatId, ex.Message);
                throw;
            }
        }

        public async Task<ChatMessage> GetMessage(string chatId, string content)
        {
            try
            {
                _logger.LogInformation("Получение сообщения для чата {ChatId} с содержимым {Content}", chatId, content);
                var request = new GetMessageRequest
                {
                    ChatId = chatId,
                    Content = content
                };
                var response = await _client.GetMessageAsync(request);

                _logger.LogInformation("Получено сообщение {MessageId} для чата {ChatId}", response.MessageId, chatId);
                return new ChatMessage
                {
                    Id = response.MessageId,
                    Content = response.Content,
                    CreatedAt = DateTime.Parse(response.CreatedAt),
                    SenderType = (SenderType)response.SenderType,
                    SenderId = response.SenderId,
                    ChatId = chatId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении сообщения для чата {ChatId}: {Error}", chatId, ex.Message);
                throw;
            }
        }

        private ChatStreamResponse CreateErrorResponse(string chatId, Exception ex)
        {
            _logger.LogError(ex, "Ошибка при стриминге для чата {ChatId}: {Error}", chatId, ex.Message);
            return new ChatStreamResponse
            {
                Content = string.Empty,
                IsFinal = true,
                Error = "Ошибка при получении ответа от ИИ"
            };
        }

        public IAsyncEnumerable<ChatStreamResponse> SubscribeToChat(StreamChatInput input)
        {
            var subscriptionKey = $"{input.ChatId}_{input.UserId}";
            if (_activeSubscriptions.ContainsKey(subscriptionKey))
            {
                _logger.LogInformation("Подписка уже существует для чата {ChatId} от пользователя {UserId}", input.ChatId, input.UserId);
                return StreamChat(input);
            }

            _activeSubscriptions[subscriptionKey] = true;
            _logger.LogInformation("Подписка на стриминг для чата {ChatId} от пользователя {UserId}", input.ChatId, input.UserId);
            return StreamChat(input);
        }

        public async IAsyncEnumerable<ChatStreamResponse> StreamChat(StreamChatInput input)
        {
            _logger.LogInformation("Начало стриминга для чата {ChatId} от пользователя {UserId}", input.ChatId, input.UserId);

            var request = new StreamChatRequest
            {
                ChatId = input.ChatId,
                Content = input.Content,
                UserId = input.UserId
            };

            using var call = _client.StreamChat(request);
            var responseStream = call.ResponseStream;

            while (await responseStream.MoveNext())
            {
                var response = responseStream.Current;
                _logger.LogInformation("Получено сообщение от сервиса чатов: Content='{Content}', IsFinal={IsFinal}, Error='{Error}'",
                    response.Content, response.IsFinal, response.Error);

                yield return new ChatStreamResponse
                {
                    Content = response.Content,
                    IsFinal = response.IsFinal,
                    Error = response.Error
                };

                if (response.IsFinal)
                {
                    var subscriptionKey = $"{input.ChatId}_{input.UserId}";
                    _activeSubscriptions.Remove(subscriptionKey);
                    _logger.LogInformation("Стриминг завершен для чата {ChatId}", input.ChatId);
                    break;
                }
            }
        }
    }
}