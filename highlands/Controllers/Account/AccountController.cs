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
using highlands.Models.DTO;

namespace highlands.Controllers.Account
{
    public class AccountController : Controller
    {
        private readonly string _connectionString;
        private readonly IDistributedCache _distributedCache;
        private readonly IConfiguration _config;

        public AccountController(IConfiguration config, IDistributedCache distributedCache)
        {
            _connectionString = config.GetConnectionString("DefaultConnection");
            _config = config;
            _distributedCache = distributedCache;
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

                        // Kiểm tra cả UserId và RoleId từ Redis
                        if (int.TryParse(roleObj.RoleId.ToString(), out roleId) &&
                            roleObj.UserId != null && int.TryParse(roleObj.UserId.ToString(), out userId))
                        {
                            Console.WriteLine($"Retrieved from Redis - UserId: {userId}, RoleId: {roleId}");

                            // Kiểm tra password từ DB ngay cả khi có cache
                            var query = "SELECT Password FROM Users WHERE UserId = @UserId AND Email = @Email";
                            var user = connection.QuerySingleOrDefault(query, new { UserId = userId, Email = request.Email });

                            if (user == null || user.Password != request.Password)
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

            // Đảm bảo đã có userId và roleId
            Console.WriteLine($"Generating JWT for UserId: {userId}, RoleId: {roleId}");
            var token = GenerateJwtToken(userId, request.Email, roleId);
            var refreshToken = GenerateRefreshToken();

            // Lưu Refresh Token vào Redis
            await StoreRefreshToken(request.Email, refreshToken);

            // Lưu token vào HttpOnly cookie (Secure = true nếu dùng HTTPS)
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = false, // Đặt true nếu đang dùng HTTPS
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

                if (user == null || user.Password != password)
                {
                    Console.WriteLine("Invalid email or password (DB check)");
                    throw new UnauthorizedAccessException("Invalid email or password");
                }

                int userId = user.UserId;
                int roleId = user.RoleId;
                Console.WriteLine($"Fetched from DB - UserId: {userId}, RoleId: {roleId}");

                // Cache userId và roleId vào Redis
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

                var result = connection.Execute(insertUserQuery, new
                {
                    Username = name,
                    Email = email,
                    Password = password,
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
            // Đăng xuất khỏi ứng dụng
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Chuyển hướng người dùng về trang login hoặc trang chủ
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
                        // Nếu người dùng đã tồn tại, cập nhật thông tin phương thức đăng nhập
                        var updateUserQuery = "UPDATE Users SET Type = 'Google' WHERE Email = @Email";
                        connection.Execute(updateUserQuery, new { Email = email });
                        userId = user.UserId;
                    }
                    HttpContext.Session.SetInt32("UserId", userId);
                }
            }
            return RedirectToAction("Index", "Customer");
        }
    }
}
