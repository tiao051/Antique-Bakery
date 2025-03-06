using MailKit.Net.Smtp;
using MimeKit;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace highlands.Services
{
    public class MessageConsumerService : BackgroundService
    {
        private readonly ConnectionFactory _factory;
        private readonly string _queueName;
        public MessageConsumerService()
        {
            _factory = new ConnectionFactory()
            {
                HostName = "localhost",
                UserName = "guest",
                Password = "guest",
                Port = 5672
            };
            _queueName = "payment_queue";
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await using var connection = await _factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: _queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (sender, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    Console.WriteLine($"[DEBUG] Raw message từ RabbitMQ: {message}");

                    var paymentInfo = JsonConvert.DeserializeObject<dynamic>(message);
                    string email = paymentInfo?.CustomerEmail;
                    string userName = paymentInfo?.UserName;

                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        Console.WriteLine($" Received email: {email}");
                        await SendEmailAsync(email, userName);
                    }
                    else
                    {
                        Console.WriteLine(" Lỗi: Email không hợp lệ.");
                    }

                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                    Console.WriteLine($"[CONSUMER] Received message: {message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($" Error processing message: {ex.Message}");
                }
            };

            await channel.BasicConsumeAsync(queue: _queueName, autoAck: false, consumer: consumer); 

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        private async Task SendEmailAsync(string toEmail, string userName)
        {
            Console.WriteLine($"[DEBUG] Email được gửi tới: '{toEmail}'");
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                Console.WriteLine("[❌] Lỗi: Email không hợp lệ.");
                return;
            }
            toEmail = toEmail.Trim();
            Console.WriteLine($"[DEBUG] Email được gửi tới: '{toEmail}'");

            var email = new MimeMessage();
            email.From.Add(new MailboxAddress("Antique", "thubeztdaxuo@gmail.com"));
            email.To.Add(new MailboxAddress(userName, toEmail));  

            email.Subject = "Antique Càfe";
            email.Body = new TextPart("plain")
            {
                Text = $"Hello {userName},\n\ntest!\n\nHihi,\nMinh Tho"
            };

            using var smtp = new SmtpClient();
            try
            {
                await smtp.ConnectAsync("smtp.gmail.com", 587, false);
                await smtp.AuthenticateAsync("thubeztdaxuo@gmail.com", "dxynznmammruchcn");
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);

                Console.WriteLine($"[✔] Email đã gửi thành công tới {toEmail}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[❌] Lỗi gửi email: {ex.Message}");
            }
        }
    }
}
