using highlands.Data;
using highlands.Models;
using highlands.Models.DTO.PasswordResetDTO;
using highlands.Services.RabbitMQServices.EmailServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;

namespace highlands.Services.PasswordResetServices
{
    public interface IPasswordResetService
    {
        Task<ForgotPasswordResponseDTO> SendResetOtpAsync(ForgotPasswordRequestDTO request);
        Task<ForgotPasswordResponseDTO> VerifyOtpAsync(VerifyOtpRequestDTO request);
        Task<ForgotPasswordResponseDTO> ResetPasswordAsync(ResetPasswordRequestDTO request);
    }

    public class PasswordResetService : IPasswordResetService
    {
        private readonly IDistributedCache _distributedCache;
        private readonly IEmailService _emailService;
        private readonly ILogger<PasswordResetService> _logger;
        private readonly string _connectionString;

        public PasswordResetService(IDistributedCache distributedCache, IEmailService emailService, 
            ILogger<PasswordResetService> logger, IConfiguration configuration)
        {
            _distributedCache = distributedCache;
            _emailService = emailService;
            _logger = logger;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task<ForgotPasswordResponseDTO> SendResetOtpAsync(ForgotPasswordRequestDTO request)
        {
            try
            {
                _logger.LogInformation($"Attempting to send reset OTP for email: {request.Email}");

                // Kiểm tra định dạng email
                if (!IsValidEmailFormat(request.Email))
                {
                    return new ForgotPasswordResponseDTO
                    {
                        Success = false,
                        Message = "Invalid email format."
                    };
                }

                // Tìm user theo email trong database
                using (var connection = new SqlConnection(_connectionString))
                {
                    var user = await connection.QueryFirstOrDefaultAsync<dynamic>(
                        "SELECT UserId, UserName, Email FROM Users WHERE Email = @Email",
                        new { Email = request.Email });

                    if (user == null)
                    {
                        return new ForgotPasswordResponseDTO
                        {
                            Success = false,
                            Message = "Email does not exist in the system."
                        };
                    }

                    // Tạo OTP mới
                    var otpCode = GenerateOtpCode();
                    
                    // Lưu OTP vào Redis với thời gian hết hạn 15 phút
                    var otpData = new
                    {
                        Email = request.Email,
                        OtpCode = otpCode,
                        UserId = (int)user.UserId,
                        UserName = (string)user.UserName,
                        CreatedAt = DateTime.Now,
                        ExpiresAt = DateTime.Now.AddMinutes(15)
                    };

                    var otpKey = $"password_reset_otp:{request.Email}";
                    var otpJson = JsonConvert.SerializeObject(otpData);
                    
                    await _distributedCache.SetStringAsync(otpKey, otpJson, new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15) // OTP có hiệu lực 15 phút
                    });

                    // Gửi email chứa OTP
                    await _emailService.SendPasswordResetOtpEmailAsync(request.Email, user.UserName, otpCode);

                    _logger.LogInformation($"Reset OTP sent successfully for email: {request.Email}");

                    return new ForgotPasswordResponseDTO
                    {
                        Success = true,
                        Message = "OTP code has been sent to your email. Please check your inbox.",
                        Email = request.Email
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending reset OTP for email: {request.Email}");
                return new ForgotPasswordResponseDTO
                {
                    Success = false,
                    Message = "An error occurred while sending OTP code. Please try again."
                };
            }
        }

        public async Task<ForgotPasswordResponseDTO> VerifyOtpAsync(VerifyOtpRequestDTO request)
        {
            try
            {
                _logger.LogInformation($"Attempting to verify OTP for email: {request.Email}");

                // Lấy OTP từ Redis
                var otpKey = $"password_reset_otp:{request.Email}";
                var otpJson = await _distributedCache.GetStringAsync(otpKey);

                if (string.IsNullOrEmpty(otpJson))
                {
                    return new ForgotPasswordResponseDTO
                    {
                        Success = false,
                        Message = "OTP code does not exist or has expired."
                    };
                }

                var otpData = JsonConvert.DeserializeObject<dynamic>(otpJson);
                
                // Kiểm tra OTP code
                if (otpData.OtpCode != request.OtpCode)
                {
                    return new ForgotPasswordResponseDTO
                    {
                        Success = false,
                        Message = "Invalid OTP code."
                    };
                }

                // Kiểm tra thời gian hết hạn
                var expiresAt = DateTime.Parse(otpData.ExpiresAt.ToString());
                if (DateTime.Now > expiresAt)
                {
                    // Xóa OTP đã hết hạn
                    await _distributedCache.RemoveAsync(otpKey);
                    
                    return new ForgotPasswordResponseDTO
                    {
                        Success = false,
                        Message = "OTP code has expired."
                    };
                }

                _logger.LogInformation($"OTP verified successfully for email: {request.Email}");

                return new ForgotPasswordResponseDTO
                {
                    Success = true,
                    Message = "Valid OTP code. You can reset your password.",
                    Email = request.Email
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying OTP for email: {request.Email}");
                return new ForgotPasswordResponseDTO
                {
                    Success = false,
                    Message = "An error occurred while verifying OTP code. Please try again."
                };
            }
        }

        public async Task<ForgotPasswordResponseDTO> ResetPasswordAsync(ResetPasswordRequestDTO request)
        {
            try
            {
                _logger.LogInformation($"Attempting to reset password for email: {request.Email}");

                // Lấy OTP từ Redis để xác minh
                var otpKey = $"password_reset_otp:{request.Email}";
                var otpJson = await _distributedCache.GetStringAsync(otpKey);

                if (string.IsNullOrEmpty(otpJson))
                {
                    return new ForgotPasswordResponseDTO
                    {
                        Success = false,
                        Message = "Password reset session has expired. Please start over."
                    };
                }

                var otpData = JsonConvert.DeserializeObject<dynamic>(otpJson);
                
                // Kiểm tra OTP code
                if (otpData.OtpCode != request.OtpCode)
                {
                    return new ForgotPasswordResponseDTO
                    {
                        Success = false,
                        Message = "Invalid OTP code."
                    };
                }

                // Kiểm tra thời gian hết hạn
                var expiresAt = DateTime.Parse(otpData.ExpiresAt.ToString());
                if (DateTime.Now > expiresAt)
                {
                    // Xóa OTP đã hết hạn
                    await _distributedCache.RemoveAsync(otpKey);
                    
                    return new ForgotPasswordResponseDTO
                    {
                        Success = false,
                        Message = "OTP code has expired."
                    };
                }

                // Cập nhật mật khẩu mới trong database
                using (var connection = new SqlConnection(_connectionString))
                {
                    var userId = (int)otpData.UserId;
                    var hashedPassword = HashPassword(request.NewPassword);
                    
                    var updateResult = await connection.ExecuteAsync(
                        "UPDATE Users SET Password = @Password WHERE UserId = @UserId",
                        new { Password = hashedPassword, UserId = userId });

                    if (updateResult > 0)
                    {
                        // Xóa OTP đã sử dụng khỏi Redis
                        await _distributedCache.RemoveAsync(otpKey);
                        
                        _logger.LogInformation($"Password reset successfully for email: {request.Email}");

                        return new ForgotPasswordResponseDTO
                        {
                            Success = true,
                            Message = "Password has been reset successfully. You can login with your new password."
                        };
                    }
                    else
                    {
                        return new ForgotPasswordResponseDTO
                        {
                            Success = false,
                            Message = "Unable to update password. Please try again."
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error resetting password for email: {request.Email}");
                return new ForgotPasswordResponseDTO
                {
                    Success = false,
                    Message = "An error occurred while resetting password. Please try again."
                };
            }
        }

        private string GenerateOtpCode()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var bytes = new byte[4];
                rng.GetBytes(bytes);
                var number = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 1000000;
                return number.ToString("D6"); // 6 digit OTP
            }
        }

        private bool IsValidEmailFormat(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
                return emailRegex.IsMatch(email);
            }
            catch
            {
                return false;
            }
        }

        private string HashPassword(string password)
        {
            // Sử dụng cách hash tương tự như trong hệ thống hiện tại
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }
    }
}
