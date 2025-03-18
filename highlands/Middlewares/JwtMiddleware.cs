using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class JwtMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _config;

    public JwtMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        _config = config;
    }

    public async Task Invoke(HttpContext context)
    {
        var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
        if (token != null)
        {
            AttachUserToContext(context, token);
        }

        await _next(context); // Tiếp tục request
    }

    private void AttachUserToContext(HttpContext context, string token)
    {
        try
        {
            var key = Encoding.UTF8.GetBytes(_config["JwtSettings:SecretKey"]);
            var tokenHandler = new JwtSecurityTokenHandler();
            var claimsPrincipal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _config["JwtSettings:Issuer"],
                ValidAudience = _config["JwtSettings:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(key)
            }, out SecurityToken validatedToken);

            context.User = claimsPrincipal;
            Console.WriteLine($"Token nhận được: {token}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"JWT Middleware lỗi: {ex.Message}");
        }
    }
}
