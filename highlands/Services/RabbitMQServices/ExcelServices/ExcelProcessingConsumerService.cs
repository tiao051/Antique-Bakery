using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace highlands.Services.RabbitMQServices.ExcelServices
{
    public interface IExcelProcessingConsumerService
    {
        Task StartProcessing(CancellationToken stoppingToken);
    }

    public class ExcelProcessingConsumerService : BackgroundService, IExcelProcessingConsumerService
    {
        private readonly IConfiguration _configuration;
        private readonly string _queueName;
        private readonly ILogger<ExcelProcessingConsumerService> _logger;

        public ExcelProcessingConsumerService(IConfiguration configuration, ILogger<ExcelProcessingConsumerService> logger)
        {
            _configuration = configuration;
            _queueName = _configuration["RabbitMQ:EmailQueue"];
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await StartProcessing(stoppingToken);
        }

        public async Task StartProcessing(CancellationToken stoppingToken)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _configuration["RabbitMQ:HostName"],
                    UserName = _configuration["RabbitMQ:UserName"],
                    Password = _configuration["RabbitMQ:Password"],
                    Port = int.Parse(_configuration["RabbitMQ:Port"])
                };

                await using var connection = await factory.CreateConnectionAsync();
                await using var channel = await connection.CreateChannelAsync();

                await channel.QueueDeclareAsync(queue: _queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (sender, ea) =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);

                        _logger.LogInformation($"Received message from 'excel_queue': {message}");

                        var fileMessage = JsonConvert.DeserializeObject<dynamic>(message);
                        string filePath = fileMessage?.FilePath;

                        _logger.LogInformation($"Received file path: {filePath}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error processing file: {ex.Message}");
                    }
                    finally
                    {
                        await channel.BasicAckAsync(ea.DeliveryTag, false);
                    }
                };

                _logger.LogInformation("Waiting for file messages from RabbitMQ...");
                await channel.BasicConsumeAsync(queue: _queueName, autoAck: false, consumer: consumer);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in StartProcessing: {ex.Message}");
            }
        }
    }
}