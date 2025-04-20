using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace highlands.Services.RabbitMQServices.EmailServices
{
    public class MessageConsumerService : BackgroundService
    {
        private readonly ConnectionFactory _factory;
        private readonly string _queueName;
        private readonly IConfiguration _configuration;

        public MessageConsumerService(IConfiguration configuration)
        {
            _configuration = configuration;

            var hostName = _configuration["RabbitMQ:HostName"];
            var userName = _configuration["RabbitMQ:UserName"];
            var password = _configuration["RabbitMQ:Password"];
            var port = _configuration["RabbitMQ:Port"];
            _queueName = _configuration["RabbitMQ:EmailQueue"];

            // Kiểm tra xem các tham số có hợp lệ không
            if (string.IsNullOrWhiteSpace(hostName) || string.IsNullOrWhiteSpace(userName) ||
                string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(port) || string.IsNullOrWhiteSpace(_queueName))
            {
                throw new ArgumentNullException("RabbitMQ connection parameters or Queue name are not configured properly.");
            }

            _factory = new ConnectionFactory()
            {
                HostName = hostName,
                UserName = userName,
                Password = password,
                Port = int.Parse(port)
            };
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

                    // Kiểm tra email và userName trước khi gửi email
                    if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(userName))
                    {
                        Console.WriteLine($"Received email: {email}");
                        await SendEmailAsync(email, userName);
                    }
                    else
                    {
                        Console.WriteLine("Lỗi: Email hoặc tên người dùng không hợp lệ.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing message: {ex.Message}");
                }
                finally
                {
                    // Đảm bảo ACK message ngay cả khi có lỗi
                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                    Console.WriteLine($"[DEBUG] ACK sent for message: {ea.DeliveryTag}");
                }
            };

            Console.WriteLine("[DEBUG] Starting consumer...");
            await channel.BasicConsumeAsync(queue: _queueName, autoAck: false, consumer: consumer);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        private async Task SendEmailAsync(string toEmail, string userName)
        {
            Console.WriteLine($"[DEBUG] Email được gửi tới: '{toEmail}'");

            // Kiểm tra tính hợp lệ của email người nhận
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                Console.WriteLine("Lỗi: Email không hợp lệ.");
                return;
            }

            toEmail = toEmail.Trim();

            // Lấy cấu hình từ appsettings.json
            var smtpServer = _configuration["EmailSettings:SmtpServer"];
            var smtpPort = _configuration["EmailSettings:Port"];
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var senderPassword = _configuration["EmailSettings:SenderPassword"];

            // Kiểm tra tính hợp lệ của các giá trị cấu hình
            if (string.IsNullOrWhiteSpace(smtpServer) || string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(senderPassword))
            {
                Console.WriteLine("Lỗi: Thông tin cấu hình email không đầy đủ.");
                return;
            }

            var email = new MimeMessage();
            email.From.Add(new MailboxAddress("Antique", senderEmail));
            email.To.Add(new MailboxAddress(userName, toEmail));

            email.Subject = "Antique CàFe";
            email.Body = new TextPart("plain")
            {
                Text = $"Hello {userName},\n\nTesttttttttt!\n\nHihi,\nMinh Tho"
            };

            using var smtp = new SmtpClient();
            try
            {
                // Kết nối tới server SMTP và gửi email
                await smtp.ConnectAsync(smtpServer, int.Parse(smtpPort), SecureSocketOptions.StartTls);  // Sử dụng StartTLS
                await smtp.AuthenticateAsync(senderEmail, senderPassword);  // Xác thực với mật khẩu ứng dụng
                await smtp.SendAsync(email);  // Gửi email
                await smtp.DisconnectAsync(true);  // Ngắt kết nối

                Console.WriteLine($"Email đã gửi thành công tới {toEmail}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi gửi email: {ex.Message}");
            }
        }
    }
}
