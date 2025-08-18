using highlands.Data;
using highlands.Models;
using highlands.Models.DTO;
using highlands.Models.DTO.CustomerDataDTO;
using highlands.Models.DTO.ProductsDTO;
using highlands.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using System.Diagnostics;

namespace highlands.Repository.MenuItemRepository
{
    public class MenuItemEFRepository : IMenuItemRepository
    {
        private readonly AppDbContext _context;
        private readonly IDistributedCache _distributedCache;
        private readonly HttpClient _httpClient;

        public MenuItemEFRepository(AppDbContext context, IDistributedCache distributedCache, HttpClient httpClient)
        {
            _context = context;
            _distributedCache = distributedCache;
            _httpClient = httpClient;
        }

        public async Task<List<MenuItem>> GetAllMenuItemsAsync()
        {
            return await _context.MenuItems.AsNoTracking().ToListAsync();
        }

        public async Task<List<SubcategoryDTO>> GetSubcategoriesAsync()
        {
            string cacheKey = "subcategory";
            string cachedData = await _distributedCache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedData))
            {
                return JsonConvert.DeserializeObject<List<SubcategoryDTO>>(cachedData);
            }

            var subcategories = await _context.MenuItems
                .AsNoTracking()
                .Select(m => new SubcategoryDTO
                {
                    Category = m.Category,
                    SubCategory = m.SubCategory,
                    SubcategoryImg = m.SubcategoryImg
                })
                .Distinct()
                .OrderBy(s => s.Category)
                .ThenBy(s => s.SubCategory)
                .ToListAsync();

            // Cache kết quả
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            };
            await _distributedCache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(subcategories), cacheOptions);

            return subcategories;
        }

        public async Task<List<MenuItem>> GetMenuItemsBySubcategoryAsync(string subcategory)
        {
            string cacheKey = $"menu:{subcategory}";
            Stopwatch stopwatch = new Stopwatch();

            stopwatch.Start();
            string cachedData = await _distributedCache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedData))
            {
                stopwatch.Stop();
                Console.WriteLine($"Cache hit! Query time from Redis: {stopwatch.ElapsedMilliseconds} ms");
                return JsonConvert.DeserializeObject<List<MenuItem>>(cachedData);
            }

            var result = await _context.MenuItems
                .AsNoTracking()
                .Where(m => m.SubCategory == subcategory)
                .Select(m => new MenuItem
                {
                    ItemName = m.ItemName,
                    ItemImg = m.ItemImg,
                    Type = m.Type,
                    SubCategory = m.SubCategory
                })
                .ToListAsync();

            // Lưu vào cache với thời gian hết hạn 30 phút
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            };
            await _distributedCache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(result), cacheOptions);
            
            stopwatch.Stop();
            Console.WriteLine($"Cache miss! Query time from EF Core: {stopwatch.ElapsedMilliseconds} ms");
            return result;
        }

        public async Task<bool> CreateCustomerAsync(Customer customer)
        {
            string cacheKey = $"customer:{customer.UserId}";

            // Xóa cache trước khi cập nhật
            await _distributedCache.RemoveAsync(cacheKey);
            Console.WriteLine($"Cache REMOVED - Key: {cacheKey}");

            try
            {
                // Kiểm tra xem customer đã tồn tại chưa
                var existingCustomer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.UserId == customer.UserId);

                if (existingCustomer != null)
                {
                    // Cập nhật thông tin
                    existingCustomer.FullName = customer.FullName;
                    existingCustomer.Phone = customer.Phone;
                    existingCustomer.Address = customer.Address;
                    existingCustomer.LoyaltyPoints = customer.LoyaltyPoints;
                    existingCustomer.Message = customer.Message;

                    _context.Customers.Update(existingCustomer);
                }
                else
                {
                    // Thêm mới
                    await _context.Customers.AddAsync(customer);
                }

                var rowsAffected = await _context.SaveChangesAsync();
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating/updating customer: {ex.Message}");
                return false;
            }
        }
        public async Task<(MenuItem?, List<MenuItemPrice>, List<RecipeWithIngredientDetail>)> GetItemDetailsAsync(string subcategory, string itemName, string size)
        {
            string cacheKey = $"itemDetails:{subcategory}:{itemName}:{size}";
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            string cachedData = await _distributedCache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedData))
            {
                stopwatch.Stop();
                Console.WriteLine($"Cache hit! Query time from Redis: {stopwatch.ElapsedMilliseconds} ms");
                return JsonConvert.DeserializeObject<(MenuItem?, List<MenuItemPrice>, List<RecipeWithIngredientDetail>)>(cachedData);
            }

            // Lấy thông tin item
            var menuItem = await _context.MenuItems
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.SubCategory == subcategory && m.ItemName == itemName);

            if (menuItem == null)
                return (null, new List<MenuItemPrice>(), new List<RecipeWithIngredientDetail>());

            // Lấy danh sách giá
            var prices = await _context.MenuItemPrices
                .AsNoTracking()
                .Where(p => p.ItemName == itemName)
                .Select(p => new MenuItemPrice { Size = p.Size, Price = p.Price })
                .Distinct()
                .ToListAsync();

            // Xử lý size đặc biệt cho Espresso
            var normalizedSize = itemName == "Espresso" && size == "M" ? "Single Shot" : size;

            // Lấy recipe thông qua join với các bảng liên quan
            var recipeList = await _context.Recipes
                .AsNoTracking()
                .Where(r => r.ItemName == itemName && r.Size == normalizedSize)
                .Join(_context.Ingredients,
                    recipe => recipe.IngredientId,
                    ingredient => ingredient.IngredientId,
                    (recipe, ingredient) => new RecipeWithIngredientDetail
                    {
                        IngredientName = ingredient.IngredientName,
                        Quantity = (int)recipe.Quantity,
                        Unit = ingredient.Unit,
                        IngredientType = ingredient.IngredientType,
                        IngredientCategory = ingredient.IngredientCategory
                    })
                .ToListAsync();

            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            };
            await _distributedCache.SetStringAsync(cacheKey, JsonConvert.SerializeObject((menuItem, prices, recipeList)), cacheOptions);
            
            stopwatch.Stop();
            Console.WriteLine($"Cache miss! Query time from EF Core: {stopwatch.ElapsedMilliseconds} ms");
            return (menuItem, prices, recipeList);
        }

        public async Task<decimal?> GetPriceAsync(string itemName, string size)
        {
            if (string.IsNullOrEmpty(itemName) || string.IsNullOrEmpty(size))
                return null;

            string cacheKey = $"price:{itemName}:{size}";
            string cachedPrice = await _distributedCache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedPrice))
            {
                if (decimal.TryParse(cachedPrice, out decimal cachedPriceValue))
                {
                    return cachedPriceValue;
                }
            }

            var price = await _context.MenuItemPrices
                .AsNoTracking()
                .Where(p => p.ItemName == itemName && p.Size == size)
                .Select(p => p.Price)
                .FirstOrDefaultAsync();

            if (price == null || price == 0)
                return null;

            // Cache giá trong 1 giờ
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            };
            await _distributedCache.SetStringAsync(cacheKey, price.ToString(), cacheOptions);

            return price;
        }

        public async Task<List<CartItemTemporary>> GetCartItemsAsync(int userId)
        {
            // EF Core không thể truy cập trực tiếp Redis cart data
            // Cần implement cart storage trong database hoặc sử dụng cách khác
            throw new NotImplementedException("Cart functionality requires database table or Redis implementation");
        }

        public async Task<int> GetTotalQuantityAsync(int userId)
        {
            // Tương tự như GetCartItemsAsync
            throw new NotImplementedException("Cart functionality requires database table or Redis implementation");
        }

        public async Task<Dictionary<string, int>> GetSizeQuantitiesAsync(int userId)
        {
            // Tương tự như GetCartItemsAsync
            throw new NotImplementedException("Cart functionality requires database table or Redis implementation");
        }

        public async Task<bool> RemoveCartItemAsync(int userId, string itemName, string itemSize)
        {
            // Tương tự như GetCartItemsAsync
            throw new NotImplementedException("Cart functionality requires database table or Redis implementation");
        }

        public async Task<bool> AddToCartAsync(int userId, string itemName, string size, decimal price, int quantity, string itemImg)
        {
            // Tương tự như GetCartItemsAsync
            throw new NotImplementedException("Cart functionality requires database table or Redis implementation");
        }

        public async Task<bool> IncreaseCartItem(int userId, string itemName, string itemSize)
        {
            // Tương tự như GetCartItemsAsync
            throw new NotImplementedException("Cart functionality requires database table or Redis implementation");
        }

        public async Task<CustomerDetailsForEmail?> GetCustomerDetailsAsync(int userId)
        {
            string cacheKey = $"customer:{userId}"; 
            Console.WriteLine($"[DEBUG] Cache key: {cacheKey}");

            // 1. Check cache
            string? cachedData = await _distributedCache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedData))
            {
                Console.WriteLine($"[DEBUG] Dữ liệu lấy từ cache: {cachedData}");
                return JsonConvert.DeserializeObject<CustomerDetailsForEmail>(cachedData);
            }

            Console.WriteLine($"[DEBUG] Không tìm thấy userId={userId} trong cache. Đang truy vấn database...");

            // 2. Truy vấn database với EF Core
            var customer = await (
                from u in _context.Users
                join c in _context.Customers on u.UserId equals c.UserId into customerJoin
                from c in customerJoin.DefaultIfEmpty()
                where u.UserId == userId
                select new CustomerDetailsForEmail
                {
                    UserId = u.UserId,
                    UserName = u.UserName,
                    Email = u.Email,
                    CustomerId = c != null ? c.CustomerId : null
                }
            ).AsNoTracking().FirstOrDefaultAsync();

            // 3. Lưu lại cache nếu có
            if (customer != null)
            {
                Console.WriteLine($"[DEBUG] Dữ liệu từ database: {JsonConvert.SerializeObject(customer)}");

                await _distributedCache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(customer), new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                });

                Console.WriteLine($"Lưu user {userId} vào cache.");
            }
            else
            {
                Console.WriteLine($"Không tìm thấy userId={userId} trong database.");
            }

            return customer;
        }


        public async Task<List<RecipeWithIngredientDetail>> GetIngredientsBySizeAsync(string itemName, string size)
        {
            throw new Exception("Dapper will handle this method");
        }

        public void BeginTransaction()
        {
            _context.Database.BeginTransaction();
        }

        public void CommitTransaction()
        {
            _context.Database.CommitTransaction();
        }

        public void RollbackTransaction()
        {
            _context.Database.RollbackTransaction();
        }

        public async Task<int> InsertOrderAsync(Order order)
        {
            await _context.Orders.AddAsync(order);
            await _context.SaveChangesAsync();
            return order.OrderId;
        }

        public async Task InsertOrderDetailAsync(OrderDetail detail)
        {
            await _context.OrderDetails.AddAsync(detail);
            await _context.SaveChangesAsync();
        }

        public async Task<CustomerCheckoutInfoDTO> GetCustomerPhoneAddrPoints(string userId)
        {
            if (!int.TryParse(userId, out int parsedUserId))
            {
                throw new ArgumentException("Invalid userId format. Expected an integer value.", nameof(userId));
            }

            var customerInfo = await _context.Customers
                .AsNoTracking()
                .Where(c => c.UserId == parsedUserId)
                .Select(c => new CustomerCheckoutInfoDTO
                {
                    Phone = c.Phone,
                    Address = c.Address,
                    LoyaltyPoints = c.LoyaltyPoints
                })
                .FirstOrDefaultAsync();

            return customerInfo ?? new CustomerCheckoutInfoDTO();
        }


        public async Task<string> GetCustomerIdFromUserId(string userId)
        {
            if (!int.TryParse(userId, out int parsedUserId))
            {
                throw new ArgumentException("Invalid userId format. Expected an integer value.", nameof(userId));
            }

            var customerId = await _context.Customers
                .AsNoTracking()
                .Where(c => c.UserId == parsedUserId) // Fixed type mismatch by parsing userId to int  
                .Select(c => c.CustomerId.ToString())
                .FirstOrDefaultAsync();

            return customerId;
        }

        public async Task<List<string>> GetSuggestedProductsDapper(List<string> productNames)
        {
            //try
            //{
            //    if (productNames == null || !productNames.Any())
            //        return new List<string>();

            //    // Gọi API recommendation service
            //    var json = JsonConvert.SerializeObject(productNames);
            //    var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            //    var response = await _httpClient.PostAsync("http://localhost:8000/recommend", content);

            //    if (response.IsSuccessStatusCode)
            //    {
            //        var rawJson = await response.Content.ReadAsStringAsync();
            //        Console.WriteLine($"Raw JSON response: {rawJson}");

            //        try
            //        {
            //            using (var jsonDoc = JsonDocument.Parse(rawJson))
            //            {
            //                var recommendations = jsonDoc.RootElement.GetProperty("recommendations");
            //                var result = new List<string>();

            //                foreach (var item in recommendations.EnumerateArray())
            //                {
            //                    result.Add(item.GetString());
            //                }

            //                return result;
            //            }
            //        }
            //        catch (Exception ex)
            //        {
            //            Console.WriteLine($"JSON parsing error: {ex.Message}");
            //            return new List<string>();
            //        }
            //    }
            //    else
            //    {
            //        Console.WriteLine($"Request failed - Status Code: {response.StatusCode}");
            //        return new List<string>();
            //    }
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine($"Exception when calling API: {ex.Message}");
            //    return new List<string>();
            //}
            throw new NotImplementedException("not imple yet");
        }

        public async Task<List<(string Name, string Img, string Subcategory)>> GetSuggestedProductWithImg(List<string> productNames)
        {
            if (productNames == null || !productNames.Any())
            {
                return new List<(string, string, string)>();
            }

            var result = await _context.MenuItems
                .AsNoTracking()
                .Where(m => productNames.Contains(m.ItemName))
                .Select(m => new { Name = m.ItemName, Img = m.ItemImg, Subcategory = m.SubCategory })
                .ToListAsync();

            return result.Select(r => (r.Name, r.Img, r.Subcategory)).ToList();
        }

        public (List<MenuItem> items, int totalPages) Search(string keyword, int page = 1, int pageSize = 6)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return (new List<MenuItem>(), 0);
            }

            // Normalize keyword for better search
            keyword = keyword.Trim();
            
            var query = _context.MenuItems.AsNoTracking()
                .Where(m => 
                    // Exact matches first (highest priority)
                    m.ItemName.Contains(keyword) ||
                    // Case insensitive search
                    EF.Functions.Like(m.ItemName.ToLower(), $"%{keyword.ToLower()}%" ) ||
                    // Search in description if available
                    //(m.Description != null && EF.Functions.Like(m.Description.ToLower(), $"%{keyword.ToLower()}%")) ||
                    // Search in subcategory
                    EF.Functions.Like(m.SubCategory.ToLower(), $"%{keyword.ToLower()}%"))
                .OrderBy(m => 
                    // Order by relevance: exact matches first, then partial matches
                    m.ItemName.ToLower() == keyword.ToLower() ? 0 :
                    m.ItemName.ToLower().StartsWith(keyword.ToLower()) ? 1 :
                    m.ItemName.ToLower().Contains(keyword.ToLower()) ? 2 : 3)
                .ThenBy(m => m.ItemName);

            var totalItems = query.Count();
            var items = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            return (items, totalPages);
        }

        public async Task<List<ProductSuggestionDTO>> GetSugestedProductByUser(string customerId)
        {
            Console.WriteLine("GetSugestedProductByUser");
            string cacheKey = $"suggested:{customerId}";

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            string cachedData = await _distributedCache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedData))
            {
                stopwatch.Stop();
                Console.WriteLine($"[Redis] Cache hit! Time: {stopwatch.ElapsedMilliseconds} ms");
                return JsonConvert.DeserializeObject<List<ProductSuggestionDTO>>(cachedData);
            }

            var result = await _context.Orders
                .AsNoTracking()
                .Where(o => o.CustomerId.ToString() == customerId)
                .SelectMany(o => o.OrderDetails)
                .Join(_context.MenuItems,
                    od => od.ItemName,
                    mi => mi.ItemName,
                    (od, mi) => new { od.ItemName, mi.ItemImg, mi.SubCategory })
                .GroupBy(x => new { x.ItemName, x.ItemImg, x.SubCategory })
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => new ProductSuggestionDTO
                {
                    Name = g.Key.ItemName,
                    Img = g.Key.ItemImg,
                    Subcategory = g.Key.SubCategory
                })
                .ToListAsync();

            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            };

            await _distributedCache.SetStringAsync(
                cacheKey,
                JsonConvert.SerializeObject(result),
                cacheOptions
            );

            stopwatch.Stop();
            Console.WriteLine($"[Redis] Cache miss! Query time from EF Core: {stopwatch.ElapsedMilliseconds} ms");

            return result;
        }

        public async Task<List<ProductSuggestionDTO>> GetSuggestedProductByTime(string timeSlot)
        {
            throw new NotImplementedException("This method needs specific business logic implementation");
        }

        public async Task<List<OrderDetailDTO>> GetCommonProductPairsAsync(int orderId)
        {
            Console.WriteLine("GetCommonProductPairsAsync");
            // Implementation tùy thuộc vào logic nghiệp vụ cụ thể
            throw new NotImplementedException("This method needs specific business logic implementation");
        }
        public async Task<bool> CreateItemAsync(MenuItem menuItem)
        {
            try
            {
                Console.WriteLine($"Creating item: {menuItem.ItemName}");
                
                // Kiểm tra xem item đã tồn tại chưa
                var existingItem = await _context.MenuItems
                    .FirstOrDefaultAsync(c => c.ItemName == menuItem.ItemName);

                if (existingItem != null)
                {
                    Console.WriteLine($"Item {menuItem.ItemName} already exists, updating...");
                    // Cập nhật thông tin
                    existingItem.Category = menuItem.Category;
                    existingItem.SubCategory = menuItem.SubCategory;
                    existingItem.ItemImg = menuItem.ItemImg;
                    existingItem.Type = menuItem.Type;

                    _context.MenuItems.Update(existingItem);
                }
                else
                {
                    Console.WriteLine($"Item {menuItem.ItemName} is new, adding...");
                    // Thêm mới
                    await _context.MenuItems.AddAsync(menuItem);
                }

                var rowsAffected = await _context.SaveChangesAsync();
                Console.WriteLine($"Rows affected: {rowsAffected}");
                
                // Verify the item was actually saved
                var verifyItem = await _context.MenuItems
                    .FirstOrDefaultAsync(c => c.ItemName == menuItem.ItemName);
                Console.WriteLine($"Item verified in database: {verifyItem != null}");
                
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating/updating item: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }
        public async Task<bool> DeleteItemAsync(string itemName)
        {
            try
            {
                Console.WriteLine($"Deleting item: {itemName}");
                
                // Tìm item trong database
                var existingItem = await _context.MenuItems
                    .FirstOrDefaultAsync(c => c.ItemName == itemName);

                if (existingItem == null)
                {
                    Console.WriteLine($"Item '{itemName}' not found in database");
                    return false; // Item không tồn tại
                }

                Console.WriteLine($"Found item: {existingItem.ItemName}, proceeding to delete...");
                
                // Xóa entity khỏi database
                _context.MenuItems.Remove(existingItem);

                var rowsAffected = await _context.SaveChangesAsync();
                Console.WriteLine($"Delete operation completed. Rows affected: {rowsAffected}");
                
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting item: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        public async Task<List<string>> GetCategoriesAsync()
        {
            return await _context.MenuItems
                .AsNoTracking()
                .Where(m => !string.IsNullOrEmpty(m.Category))
                .Select(m => m.Category!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
        }

        public async Task<List<string>> GetSubcategoriesByCategoryAsync(string category)
        {
            return await _context.MenuItems
                .AsNoTracking()
                .Where(m => m.Category == category && !string.IsNullOrEmpty(m.SubCategory))
                .Select(m => m.SubCategory!)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();
        }

        public async Task<List<string>> GetTypesAsync()
        {
            return await _context.MenuItems
                .AsNoTracking()
                .Where(m => !string.IsNullOrEmpty(m.Type))
                .Select(m => m.Type!)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();
        }
    }
}
