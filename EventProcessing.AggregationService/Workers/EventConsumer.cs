using System.Text;
using System.Text.Json;
using EventProcessing.Core.Interfaces;
using EventProcessing.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EventProcessing.AggregationService.Workers
{
    public class EventConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EventConsumer> _logger;
        private readonly IConfiguration _configuration;
        private IConnection _connection;
        private IModel _channel;
        private string _queueName;
        private string _dlqName;

        public EventConsumer(IServiceProvider serviceProvider, ILogger<EventConsumer> logger, IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            var hostName = _configuration["RabbitMQ:HostName"] ?? "localhost";
            _queueName = _configuration["RabbitMQ:QueueName"] ?? "transaction_events";
            _dlqName = $"{_queueName}_dlq";

            var factory = new ConnectionFactory { HostName = hostName, DispatchConsumersAsync = true };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.QueueDeclare(queue: _dlqName, durable: true, exclusive: false, autoDelete: false, arguments: null);

            var args = new Dictionary<string, object>
            {
                { "x-dead-letter-exchange", "" },
                { "x-dead-letter-routing-key", _dlqName }
            };

            _channel.QueueDeclare(queue: _queueName, durable: true, exclusive: false, autoDelete: false, arguments: args);

            _channel.BasicQos(prefetchSize: 0, prefetchCount: 50, global: false);

            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                TransactionEvent? transactionEvent = null;

                try
                {
                    transactionEvent = JsonSerializer.Deserialize<TransactionEvent>(message);
                    if (transactionEvent == null) throw new Exception("Mesaj deserialize edilemedi.");

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var repository = scope.ServiceProvider.GetRequiredService<ISummaryRepository>();

                        int maxRetries = 3;
                        for (int i = 1; i <= maxRetries; i++)
                        {
                            try
                            {
                                await repository.ProcessEventTransactionallyAsync(transactionEvent, stoppingToken);
                                break; 
                            }
                            catch (Exception ex)
                            {
                                if (i == maxRetries) throw; 
                                _logger.LogWarning(ex, "Veritabanı işlemi başarısız. Tekrar deneniyor (Deneme {Try}/{Max})", i, maxRetries);
                                await Task.Delay(1000 * i, stoppingToken); 
                            }
                        }
                    }

                    _channel.BasicAck(ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Mesaj işlenirken kalıcı hata oluştu. DLQ'ya yönlendiriliyor.");
                    _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                }
            };

            _channel.BasicConsume(queue: _queueName, autoAck: false, consumer: consumer);
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        public override void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
            base.Dispose();
        }
    }
}