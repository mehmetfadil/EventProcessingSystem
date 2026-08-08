using System.Text;
using System.Text.Json;
using EventProcessing.Core.Interfaces;
using EventProcessing.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace EventProcessing.Infrastructure.Messaging
{
    public class EventPublisher : IEventPublisher
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EventPublisher> _logger;
        private readonly string _hostName;
        private readonly string _queueName;
        private readonly string _userName;
        private readonly string _password;

        public EventPublisher(IConfiguration configuration, ILogger<EventPublisher> logger)
        {
            _configuration = configuration;
            _logger = logger;

            // Konfigürasyondan RabbitMQ ayarlarını alıyoruz, yoksa varsayılan atıyoruz
            _hostName = _configuration["RabbitMQ:HostName"] ?? "localhost";
            _queueName = _configuration["RabbitMQ:QueueName"] ?? "transaction_events";
            _userName = _configuration["RabbitMQ:UserName"] ?? "guest";
            _password = _configuration["RabbitMQ:Password"] ?? "guest";
        }

        public Task PublishBatchAsync(IEnumerable<TransactionEvent> events, CancellationToken cancellationToken = default)
        {
            var factory = new ConnectionFactory
            {
                HostName = _hostName,
                UserName = _userName,
                Password = _password
            };

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();
            //yoksa yeniden oluştur
            channel.QueueDeclare(
                queue: _queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var batch = channel.CreateBasicPublishBatch();

            foreach (var transactionEvent in events)
            {
                var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(transactionEvent));

                var properties = channel.CreateBasicProperties();
                properties.Persistent = true;

                batch.Add(exchange: string.Empty, routingKey: _queueName, mandatory: false, properties: properties, body: new ReadOnlyMemory<byte>(body));
            }

            batch.Publish();

            _logger.LogInformation("{Count} adet event RabbitMQ '{QueueName}' kuyruğuna gönderildi.", events.Count(), _queueName);

            return Task.CompletedTask;
        }
    }
}