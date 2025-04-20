using Newtonsoft.Json;
using RabbitMQ.Client;
using System.Text;

namespace highlands.Services.RabbitMQServices.ExcelServices
{
    public interface IExcelQueuePublisherService
    {
        Task PublishExcelFilePathAsync(string filePath);
    }

    public class ExcelQueuePublisherService : IExcelQueuePublisherService
    {
        private readonly IConfiguration _configuration;
        private readonly string _excelQueueName;
        private readonly ILogger<ExcelQueuePublisherService> _logger;

        public ExcelQueuePublisherService(IConfiguration configuration, ILogger<ExcelQueuePublisherService> logger)
        {
            _configuration = configuration;
            _excelQueueName = _configuration["RabbitMQ:ExcelQueue"];
            _logger = logger;
        }

        public async Task PublishExcelFilePathAsync(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    _logger.LogError("File path is null or empty.");
                    return;
                }

                var factory = new ConnectionFactory
                {
                    HostName = _configuration["RabbitMQ:HostName"],
                    UserName = _configuration["RabbitMQ:UserName"],
                    Password = _configuration["RabbitMQ:Password"],
                    Port = int.Parse(_configuration["RabbitMQ:Port"]) 
                };

                // Tạo kết nối và kênh
                await using var connection = await factory.CreateConnectionAsync();
                await using var channel = await connection.CreateChannelAsync();

                // Khai báo Queue
                await channel.QueueDeclareAsync(
                    queue: _excelQueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                // Tạo đối tượng fileInfo và chuyển thành JSON
                var fileInfo = new
                {
                    FilePath = filePath,
                    Timestamp = DateTime.UtcNow
                };

                var message = JsonConvert.SerializeObject(fileInfo);
                var body = Encoding.UTF8.GetBytes(message);

                var properties = new BasicProperties { Persistent = true };

                await channel.BasicPublishAsync(
                    exchange: "",
                    routingKey: _excelQueueName,
                    mandatory: false,
                    basicProperties: properties,
                    body: body);

                _logger.LogInformation("Sent file info to RabbitMQ: {FilePath}, {Timestamp}", fileInfo.FilePath, fileInfo.Timestamp);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while sending file info to queue: {ex.Message}");
            }
        }
    }
}
