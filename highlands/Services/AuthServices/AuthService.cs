using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace highlands.Services.AuthServices
{
    public interface IAuthService
    {
        string GenerateJwtToken(int userId, string email, int roleId);
        string GenerateRefreshToken();
        Task StoreRefreshTokenAsync(string email, string refreshToken);
        Task<bool> ValidateRefreshTokenAsync(string email, string refreshToken);
        Task RemoveRefreshTokenAsync(string email);
    }

    public class AuthService : IAuthService
    {
        private readonly IConfiguration _config;
        private readonly IDistributedCache _distributedCache;

        public AuthService(IConfiguration config, IDistributedCache distributedCache)
        {
            _config = config;
            _distributedCache = distributedCache;
        }

        public string GenerateJwtToken(int userId, string email, int roleId)
        {
            var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET")
                            ?? _config["JwtSettings:SecretKey"];
            
            Console.WriteLine($"Secret Key Length: {secretKey?.Length}");
            
            if (string.IsNullOrEmpty(secretKey))
                throw new InvalidOperationException("JWT Secret Key is not configured");

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

        public string GenerateRefreshToken()
        {
            return Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        }

        public async Task StoreRefreshTokenAsync(string email, string refreshToken)
        {
            string refreshKey = $"user:refresh:{email}";
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
            };
            
            await _distributedCache.SetStringAsync(refreshKey, refreshToken, cacheOptions);
            Console.WriteLine($"Refresh token stored for email: {email}");
        }

        public async Task<bool> ValidateRefreshTokenAsync(string email, string refreshToken)
        {
            string refreshKey = $"user:refresh:{email}";
            string storedToken = await _distributedCache.GetStringAsync(refreshKey);
            
            return !string.IsNullOrEmpty(storedToken) && storedToken == refreshToken;
        }

        public async Task RemoveRefreshTokenAsync(string email)
        {
            string refreshKey = $"user:refresh:{email}";
            await _distributedCache.RemoveAsync(refreshKey);
            Console.WriteLine($"Refresh token removed for email: {email}");
        }
    }
}
