using System.Text.Json;
using ApiGateway.Models;
using HotChocolate.Subscriptions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ApiGateway.Services
{
    public interface IRabbitMQService
    {
        Task PublishNotificationAsync(NotificationMessage message);
        Task StartConsumingAsync();
        void StopConsuming();
        Task PublishMontageStopCommandAsync(string userId, string chatId, string? messageId = null);
    }

    public class RabbitMQService : IRabbitMQService, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly ILogger<RabbitMQService> _logger;
        private readonly ITopicEventSender _topicEventSender;
        private readonly string _queueName = "site_notifications";
        private readonly string _montageStopQueueName = "montage_stop_commands";
        private bool _isConsuming = false;

        public RabbitMQService(
            ILogger<RabbitMQService> logger,
            ITopicEventSender topicEventSender)
        {
            _logger = logger;
            _topicEventSender = topicEventSender;

            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST"),
                    Port = int.Parse(Environment.GetEnvironmentVariable("RABBITMQ_PORT")!),
                    UserName = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME"),
                    Password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD"),
                    VirtualHost = "/"
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                // Объявляем очередь
                _channel.QueueDeclare(
                    queue: _queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                _logger.LogInformation("Успешно подключились к RabbitMQ");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при подключении к RabbitMQ");
                throw;
            }
        }

        public Task PublishNotificationAsync(NotificationMessage message)
        {
            try
            {
                var body = JsonSerializer.SerializeToUtf8Bytes(message);

                _channel.BasicPublish(
                    exchange: "",
                    routingKey: _queueName,
                    basicProperties: null,
                    body: body);

                _logger.LogInformation("Опубликовано уведомление типа {Type}: {Message}",
                    message.Type, message.Message);

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при публикации уведомления");
                return Task.FromException(ex);
            }
        }

        public Task StartConsumingAsync()
        {
            if (_isConsuming)
                return Task.CompletedTask;

            try
            {
                var consumer = new EventingBasicConsumer(_channel);

                consumer.Received += async (model, ea) =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var message = JsonSerializer.Deserialize<NotificationMessage>(body);

                        if (message != null)
                        {
                            // Публикуем в GraphQL подписки
                            await _topicEventSender.SendAsync("OnNotification", message);

                            _logger.LogInformation("Получено и передано уведомление типа {Type}: {Message}",
                                message.Type, message.Message);
                        }

                        _channel.BasicAck(ea.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка обработки сообщения из RabbitMQ");
                        _channel.BasicNack(ea.DeliveryTag, false, true);
                    }
                };

                _channel.BasicConsume(
                    queue: _queueName,
                    autoAck: false,
                    consumer: consumer);

                _isConsuming = true;
                _logger.LogInformation("Начали прослушивание очереди {QueueName}", _queueName);

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при старте прослушивания очереди");
                return Task.FromException(ex);
            }
        }

        public void StopConsuming()
        {
            if (_isConsuming)
            {
                _channel?.Close();
                _connection?.Close();
                _isConsuming = false;
                _logger.LogInformation("Остановили прослушивание очереди");
            }
        }

        public Task PublishMontageStopCommandAsync(string userId, string chatId, string? messageId = null)
        {
            // Объявляем очередь для команд остановки монтажа, если не объявлена
            _channel.QueueDeclare(
                queue: _montageStopQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var stopMessage = new MontageStopMessage
            {
                UserId = userId,
                ChatId = chatId,
                MessageId = messageId
            };
            var body = JsonSerializer.SerializeToUtf8Bytes(stopMessage);
            _channel.BasicPublish(
                exchange: "",
                routingKey: _montageStopQueueName,
                basicProperties: null,
                body: body);
            _logger.LogInformation("Опубликована команда остановки монтажа: {@StopMessage}", stopMessage);
            return Task.CompletedTask;
        }

        private class MontageStopMessage
        {
            public string UserId { get; set; } = string.Empty;
            public string ChatId { get; set; } = string.Empty;
            public string? MessageId { get; set; }
        }

        public void Dispose()
        {
            StopConsuming();
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
}