﻿using Dapper;
using highlands.Models;
using highlands.Models.DTO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Data;
using Microsoft.CodeAnalysis.Elfie.Model.Map;
using System.Drawing;
using Microsoft.AspNetCore.Mvc;

namespace highlands.Services
{
    public class MenuItemDapperRepository : IMenuItemRepository
    {
        private readonly IDbConnection _connection;
        private readonly IDistributedCache _distributedCache;

        public MenuItemDapperRepository(IDbConnection connection, IDistributedCache distributedCache)
        {
            _connection = connection;
            _distributedCache = distributedCache;
        }
        private string GetCacheKey(int userId) => $"cart:{userId}";
        public Task<List<MenuItem>> GetAllMenuItemsAsync()
        {
            throw new NotImplementedException("EF Core repository should handle this method.");
        }
        // load menu khi click vao cai subcategory
        public async Task<List<MenuItem>> GetMenuItemsBySubcategoryAsync(string subcategory)
        {
            string cacheKey = $"menu:{subcategory}";
            Stopwatch stopwatch = new Stopwatch(); // Tạo đồng hồ đo thời gian

            stopwatch.Start();
            string cachedData = await _distributedCache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedData))
            {
                stopwatch.Stop();
                Console.WriteLine($"Cache hit! Query time from Redis: {stopwatch.ElapsedMilliseconds} ms");
                return JsonConvert.DeserializeObject<List<MenuItem>>(cachedData);
            }

            const string menuItemOrder = "SELECT ItemName, ItemImg, Type, SubCategory " +
                "FROM MenuItem WHERE SubCategory = @Subcategory";
            var result = await _connection.QueryAsync<MenuItem>(menuItemOrder, new { Subcategory = subcategory });

            // Lưu vào cache với thời gian hết hạn 30 phút
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            };
            await _distributedCache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(result), cacheOptions);
            stopwatch.Stop(); // Dừng đo thời gian
            Console.WriteLine($"Cache miss! Query time from SQL Server: {stopwatch.ElapsedMilliseconds} ms");
            return result.ToList();
        }

        public async Task<(MenuItem?, List<MenuItemPrice>, List<RecipeWithIngredientDetail>)> GetItemDetailsAsync(string subcategory, string itemName, string size)
        {
            string cacheKey = $"itemDetails: {subcategory}:{itemName}:{size}";

            Stopwatch stopwatch = new Stopwatch(); // Tạo đồng hồ đo thời gian

            stopwatch.Start();

            string cachedData = await _distributedCache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedData))
            {
                stopwatch.Stop();
                Console.WriteLine($"Cache hit! Query time from Redis: {stopwatch.ElapsedMilliseconds} ms");
                return JsonConvert.DeserializeObject<(MenuItem?, List<MenuItemPrice>, List<RecipeWithIngredientDetail>)>(cachedData);
            }

            // khong co trong cache thi truy cap bang query sql
            const string menuItemQuery = "SELECT * FROM MenuItem WHERE Subcategory = @Subcategory AND ItemName = @ItemName;";
            const string priceQuery = "SELECT DISTINCT Size, Price FROM MenuItemPrice WHERE ItemName = @ItemName;";

            // Lấy thông tin item
            var menuItem = await _connection.QuerySingleOrDefaultAsync<MenuItem>(
                menuItemQuery,
                new { Subcategory = subcategory, ItemName = itemName }
            );

            if (menuItem == null)
                return (null, new List<MenuItemPrice>(), new List<RecipeWithIngredientDetail>());

            // Lấy danh sách giá
            var prices = (await _connection.QueryAsync<MenuItemPrice>(
                priceQuery,
                new { ItemName = itemName }
            ))?.ToList() ?? new List<MenuItemPrice>();

            // m = single shot
            var normalizedSize = (itemName == "Espresso" && size == "M") ? "Single Shot" : size;

            // Lấy danh sách nguyên liệu từ `GetRecipeByItemName`
            var parameters = new DynamicParameters();
            parameters.Add("@ItemName", itemName);
            parameters.Add("@Size", normalizedSize);

            var recipeList = (await _connection.QueryAsync<RecipeWithIngredientDetail>(
                "GetRecipeByItemName",
                parameters,
                commandType: CommandType.StoredProcedure
            ))?.ToList() ?? new List<RecipeWithIngredientDetail>();

            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            };
            // luu vao cache
            await _distributedCache.SetStringAsync(cacheKey, JsonConvert.SerializeObject((menuItem, prices, recipeList)), cacheOptions);
            stopwatch.Stop(); // Dừng đo thời gian
            Console.WriteLine($"Cache miss! Query time from SQL Server: {stopwatch.ElapsedMilliseconds} ms");
            return (menuItem, prices, recipeList);
        }
        public async Task<List<RecipeWithIngredientDetail>> GetIngredientsBySizeAsync(string itemName, string size)
        {
            string cacheKey = $"ingredients:{itemName}:{size}";
            string cachedData = await _distributedCache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedData))
            {
                Console.WriteLine($"Cache HIT - Key: {cacheKey} (Size: {size})");
                return JsonConvert.DeserializeObject<List<RecipeWithIngredientDetail>>(cachedData);
            }

            try
            {
                // Chuyển đổi size nếu là Espresso
                var normalizedSize = (itemName == "Espresso" && size == "M") ? "Single Shot" : size;

                // Thiết lập tham số cho Stored Procedure
                var parameters = new DynamicParameters();
                parameters.Add("@ItemName", itemName);
                parameters.Add("@Size", normalizedSize);

                // Gọi stored procedure để lấy danh sách nguyên liệu
                var recipeList = await _connection.QueryAsync<RecipeWithIngredientDetail>(
                    "GetRecipeByItemName",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

                var result = recipeList.AsList();
                // Cache kết quả với TTL 30 phút
                var cacheOptions = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) };
                await _distributedCache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(result), cacheOptions);

                Console.WriteLine($"Cache MISS - Key: {cacheKey} (Size: {size}), Cached for: 30 minutes");
                Console.WriteLine("Recipe List:");
                foreach (var recipe in result)
                {
                    Console.WriteLine(JsonConvert.SerializeObject(recipe, Formatting.Indented));
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetIngredientsBySizeAsync: {ex.Message}");
                return new List<RecipeWithIngredientDetail>(); // Trả về danh sách rỗng nếu có lỗi
            }
        }

        public async Task<List<SubcategoryDTO>> GetSubcategoriesAsync()
        {
            string cacheKey = $"subcategory";
            string cachedData = await _distributedCache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedData))
            {
                return JsonConvert.DeserializeObject<List<SubcategoryDTO>>(cachedData);
            }
            string query = @"
        SELECT DISTINCT Category, Subcategory, SubcategoryImg
        FROM MenuItem
        ORDER BY Category, Subcategory";

            var subcategories = await _connection.QueryAsync<SubcategoryDTO>(query);
            var result = subcategories.ToList();
            // Cache kết quả
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            };
            await _distributedCache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(result), cacheOptions);

            return result;
        }
        // insert data duoc gui vao db
        public async Task<bool> CreateCustomerAsync(Customer customer)
        {
            string cacheKey = $"customer:{customer.UserId}";

            // Xóa customer cũ trong cache
            await _distributedCache.RemoveAsync(cacheKey);
            Console.WriteLine($"Cache REMOVED - Key: {cacheKey}");

            // Xóa dữ liệu cũ trong DB
            string deleteSql = "DELETE FROM Customer WHERE UserId = @UserId";
            await _connection.ExecuteAsync(deleteSql, new { customer.UserId });

            // Thêm customer mới vào DB
            string insertSql = @"
        INSERT INTO Customer (FullName, Phone, Address, Message, UserId)
        VALUES (@FullName, @Phone, @Address, @Message, @UserId);
    ";
            int rowsAffected = await _connection.ExecuteAsync(insertSql, customer);

            if (rowsAffected > 0)
            {
                await _distributedCache.SetStringAsync(
                    cacheKey,
                    JsonConvert.SerializeObject(customer),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) }
                );
                Console.WriteLine($"Cache UPDATED - Key: {cacheKey}, Cached for: 30 minutes");
            }

            return rowsAffected > 0;
        }
        public async Task<decimal?> GetPriceAsync(string itemName, string size)
        {
            if (string.IsNullOrEmpty(itemName) || string.IsNullOrEmpty(size))
            {
                Console.WriteLine("Lỗi: ItemName hoặc Size bị trống.");
                return null;
            }

            string cacheKey = $"price:{itemName}:{size}";
            string cachedPrice = await _distributedCache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedPrice))
            {
                if (decimal.TryParse(cachedPrice, out decimal cachedPriceValue))
                {
                    Console.WriteLine($"Lấy giá từ cache: {cachedPriceValue}");
                    return cachedPriceValue; // Trả về giá từ cache nếu có
                }
                Console.WriteLine($"Lỗi: Giá trong cache không hợp lệ ({cachedPrice})");
            }

            Console.WriteLine($"Truy vấn DB cho Item: {itemName}, Size: {size}");

            string query = "SELECT Price FROM MenuItemPrice WHERE ItemName = @ItemName AND Size = @Size";
            var price = await _connection.QueryFirstOrDefaultAsync<decimal?>(query, new { ItemName = itemName, Size = size });

            if (!price.HasValue)
            {
                Console.WriteLine($"Không tìm thấy giá cho Item: {itemName}, Size: {size}");
                return null;
            }

            Console.WriteLine($"Giá tìm thấy: {price.Value}");

            // Kiểm tra lại cache trước khi lưu
            string existingCache = await _distributedCache.GetStringAsync(cacheKey);
            if (string.IsNullOrEmpty(existingCache))
            {
                await _distributedCache.SetStringAsync(cacheKey, price.Value.ToString(), new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                });
                Console.WriteLine($"Giá {price.Value} đã lưu vào cache.");
            }

            return price;
        }
        public async Task<List<CartItemTemporary>> GetCartItemsAsync(int userId)
        {
            string cacheKey = GetCacheKey(userId);
            string cachedCart = await _distributedCache.GetStringAsync(cacheKey);
            return JsonConvert.DeserializeObject<List<CartItemTemporary>>(cachedCart ?? "[]");
        }
        public async Task<int> GetTotalQuantityAsync(int userId)
        {
            // lấy lại giỏ hàng để lấy tổng số lượng
            var cartItems = await GetCartItemsAsync(userId);
            return cartItems.Sum(i => i.Quantity);
        }

        public async Task<Dictionary<string, int>> GetSizeQuantitiesAsync(int userId)
        {
            // lấy lại giỏ hàng để group size
            var cartItems = await GetCartItemsAsync(userId);
            return cartItems.GroupBy(i => i.Size)
                            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        }

        public async Task<bool> AddToCartAsync(int userId, string itemName, string size, decimal price, int quantity, string itemImg)
        {
            try
            {
                if (userId <= 0) return false;

                // lấy lại giỏ hàng cũ để xử lý tăng số lượng
                List<CartItemTemporary> cartItems = await GetCartItemsAsync(userId);

                // dùng linq check xem đã tồn tại sản phẩm trong giỏ chưa
                var existingItem = cartItems.FirstOrDefault(p => p.ItemName == itemName && p.Size == size);
                if (existingItem != null)
                {
                    existingItem.Quantity += quantity;
                }
                else
                {
                    cartItems.Add(new CartItemTemporary { ItemName = itemName, Size = size, Price = price, Quantity = quantity, ItemImg = itemImg });
                }

                // lưu vào cache
                string cacheKey = GetCacheKey(userId);
                var cacheOptions = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) };
                await _distributedCache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(cartItems), cacheOptions);
                return true;
            }
            catch
            {
                return false;
            }
        }
        public async Task<bool> RemoveCartItemAsync(int userId, string itemName, string itemSize)
        {
            var cart = await GetCartItemsAsync(userId);

            Console.WriteLine($"[DEBUG] User ID: {userId}, Item Name: {itemName}, Item Size: {itemSize}");
            Console.WriteLine($"[DEBUG] Full Cart Items: {JsonConvert.SerializeObject(cart)}");

            // Include size in the search criteria
            Dictionary<string, string> sizeMapping = new Dictionary<string, string>
            {
                { "Small", "S" }, { "Medium", "M" }, { "Large", "L" }
            };
            if (sizeMapping.ContainsKey(itemSize))
            {
                itemSize = sizeMapping[itemSize];
            }

            var itemToRemove = cart.FirstOrDefault(i => i.ItemName == itemName && i.Size == itemSize);
            Console.WriteLine($"item to remove: ({itemToRemove})");
            if (itemToRemove != null)
            {
                itemToRemove.Quantity = itemToRemove.Quantity - 1 ;

                if (itemToRemove.Quantity <= 0)
                {
                    cart.Remove(itemToRemove);
                }

                string cacheKey = GetCacheKey(userId);
                await _distributedCache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(cart));

                Console.WriteLine($"[DEBUG] Update Cart Items: {JsonConvert.SerializeObject(cart)}");
                return true;
            }
            return false;
        }
        public async Task<bool> IncreaseCartItem(int userId, string itemName, string itemSize)
        {
            var cart = await GetCartItemsAsync(userId);
            Dictionary<string, string> sizeMapping = new Dictionary<string, string>
            {
                { "Small", "S" }, { "Medium", "M" }, { "Large", "L" }
            };

            if (sizeMapping.ContainsKey(itemSize))
            {
                itemSize = sizeMapping[itemSize];
            }

            Console.WriteLine($"[DEBUG] Full Cart Items: {JsonConvert.SerializeObject(cart)}");

            var itemToIncrease = cart.FirstOrDefault(i => i.ItemName == itemName && i.Size == itemSize);
            if (itemToIncrease != null)
            {
                itemToIncrease.Quantity += 1;
                string cacheKey = GetCacheKey(userId);
                await _distributedCache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(cart));
                Console.WriteLine($"[DEBUG] Updated Cart Items: {JsonConvert.SerializeObject(cart)}");
                return true;
            }

            return false;
        }
        public async Task<CustomerDetailsForEmail?> GetCustomerDetailsAsync(int userId)
        { 
            string cacheKey = $"customer:{userId}"; // Đổi tên key để tránh trùng với cart
            Console.WriteLine($"[DEBUG] Cache key: {cacheKey}");

            // Kiểm tra cache
            string cachedData = await _distributedCache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedData))
            {
                Console.WriteLine($"[DEBUG] Dữ liệu lấy từ cache: {cachedData}");
                return JsonConvert.DeserializeObject<CustomerDetailsForEmail>(cachedData);
            }
            Console.WriteLine($"[DEBUG] Không tìm thấy userId={userId} trong cache. Đang truy vấn database...");

            // Truy vấn database
            string query = @"
                        SELECT u.UserId, u.UserName, u.Email, c.CustomerId
                        FROM Users u
                        LEFT JOIN Customer c ON u.UserId = c.UserId
                        WHERE u.UserId = @userId";
            var customer = await _connection.QueryFirstOrDefaultAsync<CustomerDetailsForEmail>(query, new { userId });

            if (customer != null)
            {
                Console.WriteLine($"[DEBUG] Dữ liệu từ database: {JsonConvert.SerializeObject(customer)}");

                // Lưu vào cache
                await _distributedCache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(customer), new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                });
                Console.WriteLine($"[✔] Luu user {userId} vao cache.");
            }
            else
            {
                Console.WriteLine($"[❌] Không tìm thấy userId={userId} trong database.");
            }

            return customer;
        }
        //public async Task<CustomerInfoDTO?> GetCustomerInfoAsync(int userId)
        //{
        //    string cacheKey = $"customer:{userId}";
        //    Console.WriteLine($"[DEBUG] Cache key: {cacheKey}");

        //    // Kiểm tra cache trước
        //    string cachedData = await _distributedCache.GetStringAsync(cacheKey);
        //    if (!string.IsNullOrEmpty(cachedData))
        //    {
        //        Console.WriteLine($"[DEBUG] Dữ liệu lấy từ cache: {cachedData}");
        //        return JsonConvert.DeserializeObject<CustomerInfoDTO>(cachedData);
        //    }

        //    Console.WriteLine($"[DEBUG] Không tìm thấy userId={userId} trong cache. Đang truy vấn database...");

        //    // Truy vấn database
        //    string query = @"
        //    SELECT u.UserId, u.Email, c.Address, c.Phone
        //    FROM users u
        //    LEFT JOIN Customers c ON u.UserId = c.UserId
        //    WHERE u.UserId = @userId";

        //    var customerinfo = await _connection.QueryFirstOrDefaultAsync<CustomerInfoDTO>(query, new { userId });

        //    if (customerinfo != null)
        //    {
        //        Console.WriteLine($"[DEBUG] Dữ liệu từ database: {JsonConvert.SerializeObject(customerinfo)}");

        //        // Lưu vào cache để dùng lại
        //        await _distributedCache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(customerinfo), new DistributedCacheEntryOptions
        //        {
        //            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
        //        });

        //        Console.WriteLine($"[✔] Lưu user {userId} vào cache.");
        //    }
        //    else
        //    {
        //        Console.WriteLine($"[❌] Không tìm thấy userId={userId} trong database.");
        //    }

        //    return customerinfo;
        //}
        public async Task<int> InsertOrderAsync (Order order)
        {
            const string query = @"
                   INSERT INTO [Order] (OrderDate, TotalAmount, Status, CustomerId)
                   VALUES (@OrderDate, @TotalAmount, @Status, @CustomerId);
                   SELECT CAST(SCOPE_IDENTITY() as int);";
            return await _connection.ExecuteScalarAsync<int>(query, order);
        }
    }
}