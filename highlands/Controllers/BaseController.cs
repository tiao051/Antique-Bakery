using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace highlands.Controllers
{
    public abstract class BaseController : Controller
    {
        /// <summary>
        /// Lấy UserId từ JWT token claims
        /// </summary>
        /// <returns>UserId nếu hợp lệ, null nếu không</returns>
        protected int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userIdClaim))
            {
                Console.WriteLine("No UserId claim found in JWT token");
                return null;
            }

            if (int.TryParse(userIdClaim, out int userId))
            {
                Console.WriteLine($"Authenticated UserId from JWT: {userId}");
                return userId;
            }

            Console.WriteLine($"Invalid UserId claim format: {userIdClaim}");
            return null;
        }

        /// <summary>
        /// Lấy role của user từ JWT token claims
        /// </summary>
        /// <returns>RoleId nếu hợp lệ, null nếu không</returns>
        protected int? GetCurrentUserRole()
        {
            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
            
            if (string.IsNullOrEmpty(roleClaim))
            {
                Console.WriteLine("No Role claim found in JWT token");
                return null;
            }

            if (int.TryParse(roleClaim, out int roleId))
            {
                Console.WriteLine($"Authenticated User Role from JWT: {roleId}");
                return roleId;
            }

            Console.WriteLine($"Invalid Role claim format: {roleClaim}");
            return null;
        }

        /// <summary>
        /// Lấy email của user từ JWT token claims
        /// </summary>
        /// <returns>Email nếu có, null nếu không</returns>
        protected string? GetCurrentUserEmail()
        {
            var emailClaim = User.FindFirst(ClaimTypes.Email)?.Value;
            Console.WriteLine($"Authenticated User Email from JWT: {emailClaim}");
            return emailClaim;
        }

        /// <summary>
        /// Kiểm tra xem user có đăng nhập không
        /// </summary>
        /// <returns>True nếu đã đăng nhập, false nếu chưa</returns>
        protected bool IsUserAuthenticated()
        {
            return GetCurrentUserId().HasValue;
        }
    }
}
