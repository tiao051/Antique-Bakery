using Newtonsoft.Json;
using RabbitMQ.Client;
using System.Text;

namespace highlands.Services.RabbitMQServices.EmailServices
{
    public interface IEmailService
    {
        Task SendPaymentConfirmationEmailAsync(string customerEmail, string userName);
        Task SendPasswordResetOtpEmailAsync(string email, string userName, string otpCode);
    }
    public class SendMessageToQueue : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly string _emailQueueName;

        public SendMessageToQueue(IConfiguration configuration)
        {
            _configuration = configuration;
            _emailQueueName = _configuration["RabbitMQ:EmailQueue"];
            if (string.IsNullOrEmpty(_emailQueueName))
            {
                throw new ArgumentNullException("RabbitMQ:EmailQueue", "Queue name is not configured properly.");
            }
        }

        public async Task SendPaymentConfirmationEmailAsync(string customerEmail, string userName)
        {
            var factory = new ConnectionFactory()
            {
                HostName = _configuration["RabbitMQ:HostName"],
                UserName = _configuration["RabbitMQ:UserName"],
                Password = _configuration["RabbitMQ:Password"],
                Port = int.Parse(_configuration["RabbitMQ:Port"])
            };

            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: _emailQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var paymentInfo = new
            {
                CustomerEmail = customerEmail,
                UserName = userName,
                Time = DateTime.UtcNow
            };

            var message = JsonConvert.SerializeObject(paymentInfo);
            var body = Encoding.UTF8.GetBytes(message);

            var properties = new BasicProperties { Persistent = true };

            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: _emailQueueName,
                mandatory: false,
                basicProperties: properties,
                body: body);

            Console.WriteLine($"Sent payment message: {message}");
        }

        public async Task SendPasswordResetOtpEmailAsync(string email, string userName, string otpCode)
        {
            var factory = new ConnectionFactory()
            {
                HostName = _configuration["RabbitMQ:HostName"],
                UserName = _configuration["RabbitMQ:UserName"],
                Password = _configuration["RabbitMQ:Password"],
                Port = int.Parse(_configuration["RabbitMQ:Port"])
            };

            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: _emailQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var passwordResetInfo = new
            {
                CustomerEmail = email,
                UserName = userName,
                OtpCode = otpCode,
                EmailType = "PasswordReset",
                Time = DateTime.UtcNow
            };

            var message = JsonConvert.SerializeObject(passwordResetInfo);
            var body = Encoding.UTF8.GetBytes(message);

            var properties = new BasicProperties { Persistent = true };

            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: _emailQueueName,
                mandatory: false,
                basicProperties: properties,
                body: body);

            Console.WriteLine($"Sent password reset OTP message: {message}");
        }
    }
}
