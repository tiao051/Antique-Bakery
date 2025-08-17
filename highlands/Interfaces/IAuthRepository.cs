using highlands.Models;
using highlands.Models.DTO.LoginDTO;

namespace highlands.Interfaces
{
    public interface IAuthRepository
    {
        Task<User?> GetUserByEmailAsync(string email);
        Task<User?> ValidateUserCredentialsAsync(string email, string password);
        Task<bool> IsEmailExistsAsync(string email);
        Task<User> CreateUserAsync(string username, string email, string password, string role = "Customer", string type = "Normal");
        Task<User?> GetUserByIdAsync(int userId);
        Task<bool> UpdateUserLoginTypeAsync(string email, string type);
        Task<(int userId, int roleId)> GetUserRoleInfoAsync(string email);
    }
}
