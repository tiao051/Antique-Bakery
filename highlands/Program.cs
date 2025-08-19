using highlands.Data;
using highlands.Interfaces;
using highlands.Repository.MenuItemRepository;
using highlands.Repository.OrderRepository;
using highlands.Repository.ReportRepository;
using highlands.Repository.AuthRepository;
using highlands.Services.RabbitMQServices.EmailServices;
using highlands.Services.RabbitMQServices.ExcelServices;
using highlands.Services.ReportServices;
using highlands.Services.AuthServices;
using highlands.Services.PasswordResetServices;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuestPDF.Infrastructure;
using System.Data;
using System.Text;
using highlands.Repository.PopularShoppingSequence;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;

// Cấu hình giấy phép QuestPDF
QuestPDF.Settings.License = LicenseType.Community;

// lay secretkey tu enviroment
var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET")
                ?? configuration["JwtSettings:SecretKey"];

// Validate secretKey
if (string.IsNullOrEmpty(secretKey))
{
    throw new InvalidOperationException("JWT Secret Key is not configured. Please set JWT_SECRET environment variable or JwtSettings:SecretKey in configuration.");
}

Console.WriteLine($"[JWT VALIDATION] SecretKey: {secretKey}");
Console.WriteLine($"[JWT VALIDATION] Key Length: {secretKey.Length}");

// đăng ký service
services.AddHostedService<MessageConsumerService>();
services.AddScoped<IEmailService, SendMessageToQueue>();
services.AddScoped<IPasswordResetService, PasswordResetService>();
services.AddScoped<IExcelExportService, ExcelExportService>();
services.AddScoped<IExcelQueuePublisherService, ExcelQueuePublisherService>();
services.AddHostedService<ExcelProcessingConsumerService>();
services.AddTransient<ExcelServiceManager>();

//đăng ký report
services.AddScoped<ReportService>();
services.AddScoped<ReportEFService>();
services.AddScoped<PdfService>();
services.AddScoped<IReportRepository, ReportRepository>();
//services.AddScoped<IReportRepository, ReportEFRepository>();

//đăng ký repo cho order
services.AddScoped<OrderRepository>();
services.AddScoped<OrderRepositoryEF>();
services.AddScoped<PopularShoppingSequenceEF>();
services.AddScoped<PopularShoppingSequence>();

// Đăng ký DbContext & Dapper
services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
    options.EnableSensitiveDataLogging(); // For debugging
});

services.AddScoped<IDbConnection>(_ => new SqlConnection(configuration.GetConnectionString("DefaultConnection")));

// Đăng ký Redis Cache
services.AddStackExchangeRedisCache(options =>
    options.Configuration = configuration.GetConnectionString("Redis"));

// Đăng ký Repository - Both Dapper and EF
services.AddScoped<MenuItemEFRepository>();
services.AddScoped<MenuItemDapperRepository>();
services.AddScoped<IEnumerable<IMenuItemRepository>>(sp => new List<IMenuItemRepository>
{
    sp.GetRequiredService<MenuItemEFRepository>(),
    sp.GetRequiredService<MenuItemDapperRepository>()
});

// Đăng ký Auth Repository và Services
services.AddScoped<IAuthRepository, AuthEFRepository>();
services.AddScoped<IAuthService, AuthService>();

services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "Cookies";
    options.DefaultSignInScheme = "Cookies";
    options.DefaultChallengeScheme = "Google";
})
.AddCookie("Cookies", options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(24);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
})
.AddJwtBearer("JWT", options =>
{
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var token = context.Request.Cookies["accessToken"];
            if (!string.IsNullOrEmpty(token))
            {
                context.Token = token;
            }
            return Task.CompletedTask;
        }
    };

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = configuration["JwtSettings:Issuer"],
        ValidAudience = configuration["JwtSettings:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        RoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
    };
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Check cookie first, then Authorization header
            var token = context.Request.Cookies["accessToken"];
            if (string.IsNullOrEmpty(token))
            {
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                if (authHeader?.StartsWith("Bearer ") == true)
                {
                    token = authHeader.Substring("Bearer ".Length).Trim();
                }
            }
            
            if (!string.IsNullOrEmpty(token))
            {
                context.Token = token;
            }
            return Task.CompletedTask;
        }
    };

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = configuration["JwtSettings:Issuer"],
        ValidAudience = configuration["JwtSettings:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        RoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
    };
})
.AddGoogle("Google", options =>
{
    options.ClientId = configuration["GoogleAuth:ClientId"] ?? "1057258473272-hnj6l7up7rv12crbh259h0o15pu8btep.apps.googleusercontent.com";
    options.ClientSecret = configuration["GoogleAuth:ClientSecret"] ?? "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";
    options.CallbackPath = "/signin-google";
    options.Events.OnRedirectToAuthorizationEndpoint = context =>
    {
        context.Response.Redirect(context.RedirectUri + "&prompt=select_account");
        return Task.CompletedTask;
    };
    options.Scope.Add("email");
    options.Scope.Add("profile");
    options.SaveTokens = true;
});

//Autho của jwt
services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("1"));
    options.AddPolicy("Manager", policy => policy.RequireRole("2"));
    options.AddPolicy("Customer", policy => policy.RequireRole("3"));
});

// Đăng ký Session
services.AddDistributedMemoryCache();
services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Đăng ký SignalR
services.AddSignalR();

// Đăng ký HttpClient
services.AddHttpClient();

// Đăng ký MVC
services.AddControllersWithViews();

configuration.AddJsonFile("appsettings.json");
var app = builder.Build();

try
{

    using var scope = app.Services.CreateScope();

    var dapperConn = scope.ServiceProvider.GetRequiredService<IDbConnection>();
    Console.WriteLine("Dapper Test: " + dapperConn.ConnectionString);
    dapperConn.Open();
    Console.WriteLine("✅ Dapper connected!");

    // EF
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    Console.WriteLine("EF Test: " + db.Database.GetConnectionString());
    db.Database.OpenConnection();
    Console.WriteLine("✅ EF Core connected!");
}
catch (Exception ex)
{
    Console.WriteLine("❌ Connection failed: " + ex.Message);
}



app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.UseEndpoints(endpoints =>
{
    endpoints.MapHub<OrderHub>("/orderHub");
    endpoints.MapHub<RecommendationHub>("/recommendationHub");
    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");
});

app.Run();