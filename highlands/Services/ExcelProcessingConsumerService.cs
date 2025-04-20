using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace highlands.Services
{
    public class ExcelProcessingConsumerService : BackgroundService
    {
        private readonly ConnectionFactory _factory;
        private readonly string _queueName;
        public ExcelProcessingConsumerService()
        {
            _factory = new ConnectionFactory()
            {
                HostName = "localhost",
                UserName = "admin",
                Password = "admin",
                Port = 5672
            };
            _queueName = "excel_queue";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await using var connection = await _factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(queue: _queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (sender, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    Console.WriteLine($"Received message from 'excel_queue': {message}");

                    var fileMessage = JsonConvert.DeserializeObject<dynamic>(message);
                    string filePath = fileMessage?.FilePath;

                    Console.WriteLine($"Received file path: {filePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing file: {ex.Message}");
                }
                finally
                {
                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                }
            };

            Console.WriteLine("Waiting for file messages from RabbitMQ...");
            await channel.BasicConsumeAsync(queue: _queueName, autoAck: false, consumer: consumer);
        }
    }
}
