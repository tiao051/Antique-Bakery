using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using highlands.Models;

namespace highlands.Controllers.Account
{
    public class AccountController : Controller
    {
        private readonly string _connectionString = "Server=DESKTOP-IN72EQB;Database=coffee_shop;Trusted_Connection=True;Encrypt=False;";

        public IActionResult Index(string view = "login")
        {
            ViewBag.ViewToShow = view; // Lưu tham số để xác định giao diện
            return View();
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


        //public IActionResult Logged()
        //{
        //    var userEmail = HttpContext.Session.GetString("UserEmail");
        //    if (string.IsNullOrEmpty(userEmail))
        //    {
        //        return RedirectToAction("Index", "Account"); 
        //    }
        //    return View("~/Views/User/Customer/Index.cshtml");
        //}

        // Xử lý đăng nhập
        public IActionResult Login(string email, string password)
        {
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
                else
                {
                    HttpContext.Session.SetString("UserName", (string)user.UserName);
                    HttpContext.Session.SetInt32("UserId", (int)user.UserId);

                    //HttpContext.Session.SetString("UserRole", user.Role);

                    switch (user.Role)
                    {
                        case "Customer":
                            return RedirectToAction("Index", "Customer");
                        case "Admin":
                            return RedirectToAction("Index", "Admin");
                        case "Manager":
                            return RedirectToAction("Index", "Manager");
                        default:
                            return RedirectToAction("Index", "Home");
                    }
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
