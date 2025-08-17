using highlands.Data;
using highlands.Interfaces;
using highlands.Models;
using highlands.Models.DTO.LoginDTO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using System.Diagnostics;

namespace highlands.Repository.AuthRepository
{
    public class AuthEFRepository : IAuthRepository
    {
        private readonly AppDbContext _context;
        private readonly IDistributedCache _distributedCache;

        public AuthEFRepository(AppDbContext context, IDistributedCache distributedCache)
        {
            _context = context;
            _distributedCache = distributedCache;
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            string cacheKey = $"user:email:{email}";
            string cachedData = await _distributedCache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedData))
            {
                Console.WriteLine($"Cache HIT for user email: {email}");
                return JsonConvert.DeserializeObject<User>(cachedData);
            }

            var user = await _context.Users
                .AsNoTracking()
                .Include(u => u.RoleNavigation)
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user != null)
            {
                // Cache user data for 1 hour
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                };
                await _distributedCache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(user), cacheOptions);
                Console.WriteLine($"Cache MISS - User cached for email: {email}");
            }

            return user;
        }

        public async Task<User?> ValidateUserCredentialsAsync(string email, string password)
        {
            Console.WriteLine($"[AUTH] Validating credentials for email: {email}");

            string redisKey = $"user:role:{email}";
            string? roleData = await _distributedCache.GetStringAsync(redisKey);

            if (!string.IsNullOrEmpty(roleData))
            {
                Console.WriteLine("[CACHE] Found cached role data in Redis");

                try
                {
                    dynamic? roleObj = JsonConvert.DeserializeObject<dynamic>(roleData);
                    if (roleObj != null)
                    {
                        string userIdStr = roleObj.UserId?.ToString();
                        if (int.TryParse(userIdStr, out int cachedUserId))
                        {
                            var user = await _context.Users
                                .AsNoTracking()
                                .FirstOrDefaultAsync(u => u.UserId == cachedUserId && u.Email == email);

                            if (user != null && user.Password == password)
                            {
                                Console.WriteLine("[AUTH] Cache hit - Valid credentials");
                                return user;
                            }
                            else
                            {
                                Console.WriteLine("[AUTH] Cache hit - Invalid password, removing cache");
                                await _distributedCache.RemoveAsync(redisKey);
                                return null;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CACHE] Error parsing Redis data: {ex.Message}");
                    await _distributedCache.RemoveAsync(redisKey);
                }
            }

            Console.WriteLine("[CACHE] Cache miss or invalid - Querying database");

            var dbUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == email && u.Password == password);

            if (dbUser != null)
            {
                Console.WriteLine($"[AUTH] DB hit - Valid credentials for UserId: {dbUser.UserId}");

                var roleCacheData = JsonConvert.SerializeObject(new
                {
                    UserId = dbUser.UserId,
                    RoleId = dbUser.RoleId ?? 0
                });

                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                };

                await _distributedCache.SetStringAsync(redisKey, roleCacheData, cacheOptions);
                Console.WriteLine("[CACHE] User role cached successfully");
            }

            return dbUser;
        }

        public async Task<bool> IsEmailExistsAsync(string email)
        {
            return await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.Email == email);
        }

        public async Task<User> CreateUserAsync(string username, string email, string password, string role = "Customer", string type = "Normal")
        {
            var user = new User
            {
                UserName = username,
                Email = email,
                Password = password,
                Role = role,
                Type = type,
                CreatedAt = DateTime.UtcNow,
                RoleId = await GetRoleIdByNameAsync(role)
            };

            try
            {
                await _context.Users.AddAsync(user);
                await _context.SaveChangesAsync();

                // Clear any existing cache for this email
                string cacheKey = $"user:email:{email}";
                await _distributedCache.RemoveAsync(cacheKey);

                Console.WriteLine($"User created successfully - UserId: {user.UserId}, Email: {email}");
                return user;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating user: {ex.Message}");
                throw;
            }
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            string cacheKey = $"user:id:{userId}";
            string cachedData = await _distributedCache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedData))
            {
                return JsonConvert.DeserializeObject<User>(cachedData);
            }

            var user = await _context.Users
                .AsNoTracking()
                .Include(u => u.RoleNavigation)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user != null)
            {
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                };
                await _distributedCache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(user), cacheOptions);
            }

            return user;
        }

        public async Task<bool> UpdateUserLoginTypeAsync(string email, string type)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == email);

                if (user == null)
                    return false;

                user.Type = type;
                await _context.SaveChangesAsync();

                // Clear cache
                string cacheKey = $"user:email:{email}";
                await _distributedCache.RemoveAsync(cacheKey);

                Console.WriteLine($"User login type updated - Email: {email}, Type: {type}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating user login type: {ex.Message}");
                return false;
            }
        }

        public async Task<(int userId, int roleId)> GetUserRoleInfoAsync(string email)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
                throw new InvalidOperationException($"User not found with email: {email}");

            return (user.UserId, user.RoleId ?? 0);
        }

        private async Task<int> GetRoleIdByNameAsync(string roleName)
        {
            var role = await _context.Roles
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.RoleName == roleName);

            return role?.RoleId ?? 1; // Default to 1 if role not found (assuming 1 is Customer)
        }
    }
}
