﻿using Dapper;
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
using System.Diagnostics;

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
            ViewBag.ViewToShow = view; // Lưu tham số để xác định giao diện
            return View();
        }
        private string GenerateJwtToken(int userId, int roleId)
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
                new Claim(ClaimTypes.Role, roleId.ToString()),
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
        // Xử lý đăng nhập
        public async Task<IActionResult> Login(string email, string password)
        {
            Stopwatch stopwatch = new Stopwatch();

            // Kiểm tra Redis trước
            string redisKey = $"user:role:{email}";
            stopwatch.Start();
            string roleData = await _distributedCache.GetStringAsync(redisKey);
            stopwatch.Stop();

            Console.WriteLine($" Kiem tra Redis - Thoi gian: {stopwatch.ElapsedMilliseconds} ms");

            if (roleData != null)
            {
                Console.WriteLine("Du lieu lay tu Redis:");
                Console.WriteLine(roleData);

                dynamic roleObj = JsonConvert.DeserializeObject<dynamic>(roleData);
                Console.WriteLine($"RoleId: {roleObj.RoleId}, Type: {roleObj.RoleId.GetType()}");

                int roleId;
                if (int.TryParse(roleObj.RoleId.ToString(), out roleId))
                {
                    return RedirectByRole(roleId);
                }
                else
                {
                    return BadRequest("Invalid RoleId");
                }
            }

            stopwatch.Restart();
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                var query = "SELECT * FROM Users WHERE Email = @Email";
                var user = connection.QuerySingleOrDefault(query, new { Email = email });

                if (user == null)
                {
                    TempData["ErrorMessage"] = "Email does not exist.";
                    return RedirectToAction("Index");
                }
                else if (user.Password != password)
                {
                    TempData["ErrorMessage"] = "Password is incorrect.";
                    return RedirectToAction("Index");
                }
                stopwatch.Stop();
                Console.WriteLine($"Query DB - Thoi gian: {stopwatch.ElapsedMilliseconds} ms");

                // Tạo JWT & Refresh Token
                var token = GenerateJwtToken(user.UserId, user.RoleId);
                var refreshToken = GenerateRefreshToken();

                // Lưu Role vào Redis
                var roleCacheData = JsonConvert.SerializeObject(new { RoleId = user.RoleId, Permissions = "View,Edit" });
                await _distributedCache.SetStringAsync(redisKey, roleCacheData, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                });

                // Lưu Refresh Token vào Redis
                string refreshKey = $"user:refresh:{user.UserId}";
                await _distributedCache.SetStringAsync(refreshKey, refreshToken, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
                });

                // Lưu UserId vào Session
                HttpContext.Session.SetInt32("UserId", (int)user.UserId);

                // Lưu token vào Cookie
                Response.Cookies.Append("jwt", token, new CookieOptions { HttpOnly = true, Secure = true });
                Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions { HttpOnly = true, Secure = true });
                Console.WriteLine($"roleData: {roleData}");
                return RedirectByRole(user.RoleId);
            }
        }
        private IActionResult RedirectByRole(int roleId)
        {
            return roleId switch
            {
                2 => RedirectToAction("Index", "Manager"),
                1 => RedirectToAction("Index", "Admin"),
                3 => RedirectToAction("Index", "Customer"),
                _ => RedirectToAction("Index", "Home"),
            };
        }
        [HttpPost]
        public IActionResult Register(string name, string email, string password)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                // Kiểm tra email đã tồn tại
                var checkEmailQuery = "SELECT COUNT(*) FROM Users WHERE Email = @Email";
                int emailExists = connection.ExecuteScalar<int>(checkEmailQuery, new { Email = email });

                if (emailExists > 0)
                {
                    TempData["ErrorMessage"] = "Email already exists. Please use another one.";
                    return RedirectToAction("Index", new { view = "register" });
                }

                // Lưu người dùng mới
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

            // Lấy email từ thông tin người dùng
            var email = claimsPrincipal?.FindFirst(ClaimTypes.Email)?.Value;

            if (email != null)
            {
                // Kiểm tra xem người dùng đã tồn tại trong hệ thống chưa
                using (var connection = new SqlConnection(_connectionString))
                {
                    var query = "SELECT * FROM Users WHERE Email = @Email";
                    var user = connection.QuerySingleOrDefault(query, new { Email = email });

                    int userId;
                    if (user == null)
                    {
                        // Nếu người dùng chưa tồn tại, thêm người dùng mới với phương thức đăng nhập là 'Google'
                        var insertUserQuery = @"
                INSERT INTO Users (Username, Email, Password, Role, Type)
                VALUES (@Username, @Email, NULL, @Role, 'Google')";

                        userId = connection.Execute(insertUserQuery, new
                        {
                            Username = email.Split('@')[0], // Lấy username từ email
                            Email = email,
                            Role = "Customer" // Mặc định là Customer, có thể thay đổi tùy theo yêu cầu
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
            // Chuyển hướng người dùng đến trang yêu cầu
            return RedirectToAction("Index", "Customer");
        }
    }
}
