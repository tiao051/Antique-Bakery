namespace highlands.Models.DTO.LoginDTO
{
    public class RegisterRequestDTO
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RefreshTokenRequestDTO
    {
        public string Email { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class LogoutRequestDTO
    {
        public string Email { get; set; } = string.Empty;
    }

    public class LoginResponseDTO
    {
        public string Message { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public int UserId { get; set; }
        public string Email { get; set; } = string.Empty;
    }

    public class UserInfoResponseDTO
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int? RoleId { get; set; }
        public string Type { get; set; } = string.Empty;
        public DateTime? CreatedAt { get; set; }
    }
}
