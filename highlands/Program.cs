﻿using highlands.Data;
using highlands.Models;
using highlands.Repository;
using highlands.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;

// lay secretkey tu enviroment
var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET")
                ?? builder.Configuration["JwtSettings:SecretKey"];
Console.WriteLine($"[JWT VALIDATION] SecretKey: {secretKey}");
Console.WriteLine($"[JWT VALIDATION] Key Length: {secretKey.Length}");

// đăng ký rabbitmq
builder.Services.AddHostedService<MessageConsumerService>();


// Đăng ký DbContext & Dapper
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
services.AddScoped<IDbConnection>(_ => new SqlConnection(configuration.GetConnectionString("DefaultConnection")));

// Đăng ký Redis Cache
services.AddStackExchangeRedisCache(options =>
    options.Configuration = configuration.GetConnectionString("Redis"));

// Đăng ký Repository
services.AddScoped<MenuItemEFRepository>();
services.AddScoped<MenuItemDapperRepository>();
services.AddScoped<IEnumerable<IMenuItemRepository>>(sp => new List<IMenuItemRepository>
{
    sp.GetRequiredService<MenuItemDapperRepository>()
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    //    options.DefaultAuthenticateScheme = "Cookies";
    //    options.DefaultSignInScheme = "Cookies";
    //    options.DefaultChallengeScheme = "Google";
})
.AddJwtBearer(options =>
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
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
        ValidAudience = builder.Configuration["JwtSettings:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
    };
})
.AddCookie()
.AddGoogle(options =>
{
    options.ClientId = "1057258473272-hnj6l7up7rv12crbh259h0o15pu8btep.apps.googleusercontent.com";
    options.ClientSecret = "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";
    options.Events.OnRedirectToAuthorizationEndpoint = context =>
    {
        context.Response.Redirect(context.RedirectUri + "&prompt=select_account");
        return Task.CompletedTask;
    };
});

//Autho của jwt
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("1"));
    options.AddPolicy("Manager", policy => policy.RequireRole("2"));
    options.AddPolicy("Customer", policy => policy.RequireRole("3"));
});

//Đăng ký signalR
// Đăng ký Session
services.AddDistributedMemoryCache();
services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Đăng ký MVC
services.AddControllersWithViews();

var app = builder.Build();

// Middleware
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

// Cấu hình route mặc định
app.MapControllerRoute(name: "default", pattern: "{controller=Account}/{action=Index}/{id?}");

app.Run();