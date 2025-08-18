using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using highlands.Models.DTO.LoginDTO;
using highlands.Models.DTO.PasswordResetDTO;
using highlands.Services.PasswordResetServices;
using System.Security.Cryptography;

namespace highlands.Controllers.Account
{
    public class AccountController : Controller
    {
        private readonly string _connectionString;
        private readonly IDistributedCache _distributedCache;
        private readonly IConfiguration _config;
        private readonly IPasswordResetService _passwordResetService;

        public AccountController(IConfiguration config, IDistributedCache distributedCache, IPasswordResetService passwordResetService)
        {
            _connectionString = config.GetConnectionString("DefaultConnection");
            _config = config;
            _distributedCache = distributedCache;
            _passwordResetService = passwordResetService;
        }

        public IActionResult Index(string view = "login")
        {
            ViewBag.ViewToShow = view;
            return View();
        }
        private string GenerateJwtToken(int userId, string email, int roleId)
        {
            var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET")
                            ?? _config["JwtSettings:SecretKey"];
            Console.WriteLine($"Secret Key Length: {secretKey.Length}");
            Console.WriteLine($"Secret Key: {secretKey}");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, roleId.ToString())
            };

            var token = new JwtSecurityToken(
               issuer: _config["JwtSettings:Issuer"],
               audience: _config["JwtSettings:Audience"],
               claims: claims,
               expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(_config["JwtSettings:ExpireMinutes"])),
               signingCredentials: creds
           );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        private string GenerateRefreshToken()
        {
            return Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        }
        private async Task StoreRefreshToken(string email, string refreshToken)
        {
            string refreshKey = $"user:refresh:{email}";
            await _distributedCache.SetStringAsync(refreshKey, refreshToken, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
            });
        }
        public async Task<IActionResult> Login([FromBody] LoginRequestDTO request)
        {
            Console.WriteLine("Login API called");
            Console.WriteLine($"Email: {request.Email}");

            string redisKey = $"user:role:{request.Email}";
            string roleData = await _distributedCache.GetStringAsync(redisKey);

            int roleId = 0;
            int userId = 0;

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                Console.WriteLine("Database connection opened");

                if (roleData != null)
                {
                    Console.WriteLine("Found cached role data in Redis");
                    Console.WriteLine($"Cached Data: {roleData}");

                    try
                    {
                        var roleObj = JsonConvert.DeserializeObject<dynamic>(roleData);

                        if (int.TryParse(roleObj.RoleId.ToString(), out roleId) &&
                            roleObj.UserId != null && int.TryParse(roleObj.UserId.ToString(), out userId))
                        {
                            Console.WriteLine($"Retrieved from Redis - UserId: {userId}, RoleId: {roleId}");

                            // Ki?m tra password t? DB ngay c? khi c� cache
                            var query = "SELECT Password FROM Users WHERE UserId = @UserId AND Email = @Email";
                            var user = connection.QuerySingleOrDefault(query, new { UserId = userId, Email = request.Email });

                            if (user == null || !VerifyPassword(request.Password, user.Password))
                            {
                                Console.WriteLine("Invalid email or password (cached UserId)");
                                return Unauthorized(new { message = "Invalid email or password" });
                            }
                        }
                        else
                        {
                            Console.WriteLine("Invalid cache format, fetching from DB");
                            await FetchUserFromDB(request.Email, request.Password, connection, redisKey);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error parsing Redis data: {ex.Message}");
                        await FetchUserFromDB(request.Email, request.Password, connection, redisKey);
                    }
                }
                else
                {
                    Console.WriteLine("No cache found, fetching from DB");
                    (userId, roleId) = await FetchUserFromDB(request.Email, request.Password, connection, redisKey);
                }
            }

            Console.WriteLine($"Generating JWT for UserId: {userId}, RoleId: {roleId}");
            var token = GenerateJwtToken(userId, request.Email, roleId);
            var refreshToken = GenerateRefreshToken();

            await StoreRefreshToken(request.Email, refreshToken);

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = false,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddHours(1)
            };

            Response.Cookies.Append("accessToken", token, cookieOptions);

            return Ok(new { accessToken = token, refreshToken, roleId });
        }
        private async Task<(int userId, int roleId)> FetchUserFromDB(string email, string password, SqlConnection connection, string redisKey)
        {
            try
            {
                Console.WriteLine("Querying user from database...");
                var query = "SELECT UserId, Email, Password, RoleId FROM Users WHERE Email = @Email";
                var user = connection.QuerySingleOrDefault(query, new { Email = email });

                if (user == null || !VerifyPassword(password, user.Password))
                {
                    Console.WriteLine("Invalid email or password (DB check)");
                    throw new UnauthorizedAccessException("Invalid email or password");
                }

                int userId = user.UserId;
                int roleId = user.RoleId;
                Console.WriteLine($"Fetched from DB - UserId: {userId}, RoleId: {roleId}");

                // Cache userId v� roleId v�o Redis
                var roleCacheData = JsonConvert.SerializeObject(new { UserId = userId, RoleId = roleId });
                await _distributedCache.SetStringAsync(redisKey, roleCacheData, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                });

                var dataCached = await _distributedCache.GetStringAsync(redisKey);
                if (!string.IsNullOrEmpty(dataCached))
                {
                    var data = JsonConvert.DeserializeObject<dynamic>(dataCached);
                    Console.WriteLine($"UserId: {data.UserId}");
                    Console.WriteLine($"RoleId: {data.RoleId}");
                }

                return (userId, roleId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching user from DB: {ex.Message}");
                throw;
            }
        }
        [HttpPost]
        public IActionResult Register(string name, string email, string password)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                var checkEmailQuery = "SELECT COUNT(*) FROM Users WHERE Email = @Email";
                int emailExists = connection.ExecuteScalar<int>(checkEmailQuery, new { Email = email });

                if (emailExists > 0)
                {
                    TempData["ErrorMessage"] = "Email already exists. Please use another one.";
                    return RedirectToAction("Index", new { view = "register" });
                }

                var insertUserQuery = @"
                    INSERT INTO Users (Username, Email, Password, Role)
                    VALUES (@Username, @Email, @Password, @Role)";

                // Hash the password before storing
                var hashedPassword = HashPassword(password);

                var result = connection.Execute(insertUserQuery, new
                {
                    Username = name,
                    Email = email,
                    Password = hashedPassword,
                    Role = "Customer"
                });
                if (result > 0)
                {
                    TempData["SuccessMessage"] = "Sign up successful! Please log in.";
                    return RedirectToAction("Index", new { view = "login" });
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred. Please try again.";
                    return RedirectToAction("Index", new { view = "register" });
                }
            }
        }
        public async Task<IActionResult> Logout()
        {
            // ??ng xu?t kh?i ?ng d?ng
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Chuy?n h??ng ng??i d�ng v? trang login ho?c trang ch?
            return RedirectToAction("Index", "Account");
        }

        [HttpGet]
        public async Task<IActionResult> GoogleLogin(string returnUrl = null)
        {
            var redirectUrl = Url.Action("GoogleResponse", "Account", new { returnUrl });
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet]
        public async Task<IActionResult> GoogleResponse(string returnUrl = null)
        {
            var authenticateResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var claimsPrincipal = authenticateResult.Principal;

            var email = claimsPrincipal?.FindFirst(ClaimTypes.Email)?.Value;

            if (email != null)
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    var query = "SELECT * FROM Users WHERE Email = @Email";
                    var user = connection.QuerySingleOrDefault(query, new { Email = email });

                    int userId;
                    if (user == null)
                    {
                        var insertUserQuery = @"
                            INSERT INTO Users (Username, Email, Password, Role, Type)
                            VALUES (@Username, @Email, NULL, @Role, 'Google')";

                        userId = connection.Execute(insertUserQuery, new
                        {
                            Username = email.Split('@')[0],
                            Email = email,
                            Role = "Customer"
                        });
                    }
                    else
                    {
                        // N?u ng??i d�ng ?� t?n t?i, c?p nh?t th�ng tin ph??ng th?c ??ng nh?p
                        var updateUserQuery = "UPDATE Users SET Type = 'Google' WHERE Email = @Email";
                        connection.Execute(updateUserQuery, new { Email = email });
                        userId = user.UserId;
                    }
                    HttpContext.Session.SetInt32("UserId", userId);
                }
            }
            return RedirectToAction("Index", "Customer");
        }

        // GET: Trang quên mật khẩu
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // POST: Send OTP to email
        [HttpPost]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDTO request)
        {
            if (!ModelState.IsValid)
            {
                return Json(new ForgotPasswordResponseDTO
                {
                    Success = false,
                    Message = "Invalid data."
                });
            }

            var result = await _passwordResetService.SendResetOtpAsync(request);
            return Json(result);
        }

        // GET: OTP verification page
        [HttpGet]
        public IActionResult VerifyOtp(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("ForgotPassword");
            }
            ViewBag.Email = email;
            return View();
        }

        // POST: Verify OTP
        [HttpPost]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequestDTO request)
        {
            if (!ModelState.IsValid)
            {
                return Json(new ForgotPasswordResponseDTO
                {
                    Success = false,
                    Message = "Invalid data."
                });
            }

            var result = await _passwordResetService.VerifyOtpAsync(request);
            return Json(result);
        }

        // GET: Reset password page
        [HttpGet]
        public IActionResult ResetPassword(string email, string otp)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(otp))
            {
                return RedirectToAction("ForgotPassword");
            }
            ViewBag.Email = email;
            ViewBag.OtpCode = otp;
            return View();
        }

        // POST: Reset password
        [HttpPost]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDTO request)
        {
            if (!ModelState.IsValid)
            {
                return Json(new ForgotPasswordResponseDTO
                {
                    Success = false,
                    Message = "Invalid data."
                });
            }

            var result = await _passwordResetService.ResetPasswordAsync(request);
            return Json(result);
        }

        // Password verification methods
        private bool VerifyPassword(string plainTextPassword, string storedPassword)
        {
            // First, try direct comparison for legacy unhashed passwords
            if (plainTextPassword == storedPassword)
            {
                return true;
            }

            // Then, try hashed comparison for new passwords
            try
            {
                string hashedInput = HashPassword(plainTextPassword);
                return hashedInput == storedPassword;
            }
            catch
            {
                return false;
            }
        }

        private string HashPassword(string password)
        {
            // Use the same hashing method as in PasswordResetService
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        // Migration method to hash existing plaintext passwords
        [HttpPost]
        public async Task<IActionResult> MigratePasswordsToHashed()
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    // Get all users with potentially unhashed passwords
                    var query = "SELECT UserId, Email, Password FROM Users";
                    var users = connection.Query<dynamic>(query).ToList();
                    
                    int migratedCount = 0;
                    
                    foreach (var user in users)
                    {
                        string currentPassword = user.Password.ToString();
                        
                        // Check if password is already hashed (Base64 strings typically end with = or have specific length patterns)
                        // Also check if it matches common hash lengths
                        bool isAlreadyHashed = IsLikelyHashedPassword(currentPassword);
                        
                        if (!isAlreadyHashed)
                        {
                            // Hash the plaintext password
                            string hashedPassword = HashPassword(currentPassword);
                            
                            // Update the password in database
                            var updateQuery = "UPDATE Users SET Password = @Password WHERE UserId = @UserId";
                            var result = connection.Execute(updateQuery, new 
                            { 
                                Password = hashedPassword, 
                                UserId = user.UserId 
                            });
                            
                            if (result > 0)
                            {
                                migratedCount++;
                                Console.WriteLine($"Migrated password for user: {user.Email}");
                            }
                        }
                    }
                    
                    return Ok(new { 
                        success = true, 
                        message = $"Successfully migrated {migratedCount} passwords to hashed format.",
                        migratedCount = migratedCount,
                        totalUsers = users.Count
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error migrating passwords: {ex.Message}");
                return BadRequest(new { 
                    success = false, 
                    message = "Error occurred during password migration.",
                    error = ex.Message
                });
            }
        }

        private bool IsLikelyHashedPassword(string password)
        {
            // SHA256 hash converted to Base64 should be 44 characters long
            // This is a simple heuristic to detect if password is likely already hashed
            if (string.IsNullOrEmpty(password))
                return false;
                
            // Base64 encoded SHA256 hash is typically 44 characters and ends with =
            if (password.Length == 44 && password.EndsWith("="))
                return true;
                
            // Additional checks: contains only Base64 characters
            if (password.Length >= 40 && System.Text.RegularExpressions.Regex.IsMatch(password, @"^[A-Za-z0-9+/]*={0,2}$"))
                return true;
                
            return false;
        }
    }
}