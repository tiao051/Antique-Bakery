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

                    var emailInfo = JsonConvert.DeserializeObject<dynamic>(message);
                    string email = emailInfo?.CustomerEmail;
                    string userName = emailInfo?.UserName;
                    string emailType = emailInfo?.EmailType;
                    string otpCode = emailInfo?.OtpCode;

                    // Kiểm tra email và userName trước khi gửi email
                    if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(userName))
                    {
                        if (emailType == "PasswordReset" && !string.IsNullOrWhiteSpace(otpCode))
                        {
                            await SendPasswordResetEmailAsync(email, userName, otpCode);
                        }
                        else
                        {
                            await SendEmailAsync(email, userName);
                        }
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

            if (string.IsNullOrWhiteSpace(toEmail))
            {
                Console.WriteLine("Lỗi: Email không hợp lệ.");
                return;
            }
            toEmail = toEmail.Trim();

            var smtpServer = _configuration["EmailSettings:SmtpServer"];
            var smtpPortStr = _configuration["EmailSettings:Port"];
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var senderPassword = _configuration["EmailSettings:SenderPassword"];
            Console.WriteLine($"sender email: {senderEmail}");
            Console.WriteLine($"sender password: {senderPassword}");
            if (string.IsNullOrWhiteSpace(smtpServer) ||
                string.IsNullOrWhiteSpace(smtpPortStr) ||
                string.IsNullOrWhiteSpace(senderEmail) ||
                string.IsNullOrWhiteSpace(senderPassword))
            {
                Console.WriteLine("Lỗi: Thông tin cấu hình email không đầy đủ.");
                return;
            }

            if (!int.TryParse(smtpPortStr, out int smtpPort))
            {
                Console.WriteLine("Lỗi: Port SMTP không hợp lệ.");
                return;
            }

            MimeMessage email;
            try
            {
                Console.WriteLine("[DEBUG] Tạo đối tượng email...");
                email = new MimeMessage();
                email.From.Add(new MailboxAddress("Antique", senderEmail));
                email.To.Add(new MailboxAddress(userName, toEmail));
                email.Subject = "Antique CàFe";
                email.Body = new TextPart("plain")
                {
                    Text = $"Hello {userName},\n\nTesttttttttt!\n\nHihi,\nMinh Tho"
                };
                Console.WriteLine("[DEBUG] Tạo email thành công.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi tạo email: {ex.Message}");
                return;
            }

            try
            {
                Console.WriteLine($"[DEBUG] Kết nối tới SMTP server {smtpServer}:{smtpPort}...");
                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(smtpServer, smtpPort, SecureSocketOptions.StartTls);
                Console.WriteLine("[DEBUG] Đã kết nối, đang xác thực...");
                await smtp.AuthenticateAsync(senderEmail, senderPassword);
                Console.WriteLine("[DEBUG] Đã xác thực, đang gửi email...");
                await smtp.SendAsync(email);
                Console.WriteLine("[DEBUG] Email đã gửi thành công.");
                await smtp.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi gửi email: {ex.Message}");
            }
        }

        private async Task SendPasswordResetEmailAsync(string toEmail, string userName, string otpCode)
        {
            Console.WriteLine($"[DEBUG] Gửi email reset password tới: '{toEmail}' với OTP: {otpCode}");

            if (string.IsNullOrWhiteSpace(toEmail))
            {
                Console.WriteLine("Lỗi: Email không hợp lệ.");
                return;
            }
            toEmail = toEmail.Trim();

            var smtpServer = _configuration["EmailSettings:SmtpServer"];
            var smtpPortStr = _configuration["EmailSettings:Port"];
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var senderPassword = _configuration["EmailSettings:SenderPassword"];

            if (string.IsNullOrWhiteSpace(smtpServer) ||
                string.IsNullOrWhiteSpace(smtpPortStr) ||
                string.IsNullOrWhiteSpace(senderEmail) ||
                string.IsNullOrWhiteSpace(senderPassword))
            {
                Console.WriteLine("Lỗi: Thông tin cấu hình email không đầy đủ.");
                return;
            }

            if (!int.TryParse(smtpPortStr, out int smtpPort))
            {
                Console.WriteLine("Lỗi: Port SMTP không hợp lệ.");
                return;
            }

            MimeMessage email;
            try
            {
                Console.WriteLine("[DEBUG] Tạo đối tượng email reset password...");
                email = new MimeMessage();
                email.From.Add(new MailboxAddress("Antique Cafe", senderEmail));
                email.To.Add(new MailboxAddress(userName, toEmail));
                email.Subject = "Antique Cafe - Đặt lại mật khẩu";
                email.Body = new TextPart("html")
                {
                    Text = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 10px;'>
                        <div style='text-align: center; margin-bottom: 30px;'>
                            <h1 style='color: #8B4513; margin: 0;'>☕ Antique Cafe</h1>
                            <h2 style='color: #333; margin: 10px 0;'>Đặt lại mật khẩu</h2>
                        </div>
                        
                        <div style='background-color: #f9f9f9; padding: 20px; border-radius: 8px; margin-bottom: 20px;'>
                            <p style='margin: 0 0 15px 0; color: #333;'>Xin chào <strong>{userName}</strong>,</p>
                            <p style='margin: 0 0 15px 0; color: #333;'>Bạn đã yêu cầu đặt lại mật khẩu cho tài khoản tại Antique Cafe.</p>
                            <p style='margin: 0 0 15px 0; color: #333;'>Mã OTP của bạn là:</p>
                            
                            <div style='text-align: center; margin: 20px 0;'>
                                <div style='display: inline-block; background-color: #8B4513; color: white; padding: 15px 30px; border-radius: 5px; font-size: 24px; font-weight: bold; letter-spacing: 3px;'>
                                    {otpCode}
                                </div>
                            </div>
                            
                            <p style='margin: 15px 0 0 0; color: #666; font-size: 14px;'>
                                ⚠️ Mã OTP này có hiệu lực trong 15 phút. Vui lòng không chia sẻ mã này với bất kỳ ai.
                            </p>
                        </div>
                        
                        <div style='text-align: center; margin-top: 30px; padding-top: 20px; border-top: 1px solid #eee;'>
                            <p style='margin: 0; color: #999; font-size: 12px;'>
                                Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.
                            </p>
                            <p style='margin: 5px 0 0 0; color: #8B4513; font-weight: bold;'>
                                🏪 Antique Cafe - Nơi hương vị cà phê đích thực
                            </p>
                        </div>
                    </div>"
                };
                Console.WriteLine("[DEBUG] Tạo email reset password thành công.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi tạo email reset password: {ex.Message}");
                return;
            }

            try
            {
                Console.WriteLine($"[DEBUG] Kết nối tới SMTP server {smtpServer}:{smtpPort}...");
                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(smtpServer, smtpPort, SecureSocketOptions.StartTls);
                Console.WriteLine("[DEBUG] Đã kết nối, đang xác thực...");
                await smtp.AuthenticateAsync(senderEmail, senderPassword);
                Console.WriteLine("[DEBUG] Đã xác thực, đang gửi email reset password...");
                await smtp.SendAsync(email);
                Console.WriteLine("[DEBUG] Email reset password đã gửi thành công.");
                await smtp.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi gửi email reset password: {ex.Message}");
            }
        }
    }
}
