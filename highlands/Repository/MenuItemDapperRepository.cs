using Dapper;
using highlands.Models;
using highlands.Models.DTO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Data;
using highlands.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace highlands.Repository
{
    public class MenuItemDapperRepository : IMenuItemRepository
    {
        private readonly IDbConnection _connection;
        private readonly IDistributedCache _distributedCache;
        private IDbTransaction _transaction;
        private readonly HttpClient _httpClient;
        public MenuItemDapperRepository(IDbConnection connection, IDistributedCache distributedCache, HttpClient httpClient)
        {
            _connection = connection;
            _distributedCache = distributedCache;
            _httpClient = httpClient;
        }
        public void BeginTransaction()
        {
            if (_connection.State != ConnectionState.Open)
            {
                _connection.Open();
            }
            _transaction = _connection.BeginTransaction();
        }
        public void CommitTransaction()
        {
            _transaction?.Commit();
            _transaction = null;
        }
        public void RollbackTransaction()
        {
            _transaction?.Rollback();
            _transaction = null;
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
            var normalizedSize = itemName == "Espresso" && size == "M" ? "Single Shot" : size;

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
                var normalizedSize = itemName == "Espresso" && size == "M" ? "Single Shot" : size;

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
                //Console.WriteLine("Recipe List:");
                //foreach (var recipe in result)
                //{
                //    Console.WriteLine(JsonConvert.SerializeObject(recipe, Formatting.Indented));
                //}

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

            // Xóa cache trước khi cập nhật
            await _distributedCache.RemoveAsync(cacheKey);
            Console.WriteLine($"Cache REMOVED - Key: {cacheKey}");

            // Cập nhật trước, nếu không có thì thêm mới
            string updateSql = @"
                UPDATE Customer
                SET FullName = @FullName, Phone = @Phone, Address = @Address, Message = @Message
                WHERE UserId = @UserId";

            int rowsAffected = await _connection.ExecuteAsync(updateSql, customer);

            if (rowsAffected == 0)
            {
                string insertSql = @"
                    INSERT INTO Customer (FullName, Phone, Address, Message, UserId)
                    VALUES (@FullName, @Phone, @Address, @Message, @UserId)";

                rowsAffected = await _connection.ExecuteAsync(insertSql, customer);
            }

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
        [HttpGet]
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
                itemToRemove.Quantity = itemToRemove.Quantity - 1;

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
                Console.WriteLine($"Luu user {userId} vao cache.");
            }
            else
            {
                Console.WriteLine($"Không tìm thấy userId={userId} trong database.");
            }

            return customer;
        }
        public async Task<int> InsertOrderAsync(Order order)
        {
            const string query = @"
                INSERT INTO [Order] (OrderDate, TotalAmount, Status, CustomerId)
                VALUES (@OrderDate, @TotalAmount, @Status, @CustomerId);
                SELECT CAST(SCOPE_IDENTITY() as int);";

            return await _connection.ExecuteScalarAsync<int>(query, order, _transaction);
        }
        public async Task InsertOrderDetailAsync(OrderDetail detail)
        {
            const string query = @"
                INSERT INTO OrderDetail (OrderId, ItemName, Quantity, Price, Size)
                VALUES (@OrderId, @ItemName, @Quantity, @Price, @Size);";

            await _connection.ExecuteAsync(query, detail, _transaction);
        }
        public async Task<CustomerCheckoutInfoDTO> GetCustomerPhoneAddrPoints(string userId)
        {
            const string query = @"
                SELECT Phone, Address, LoyaltyPoints 
                FROM Customer
                WHERE UserId = @UserId";

            // Thực hiện truy vấn cơ sở dữ liệu
            var customerInfo = await _connection.QuerySingleOrDefaultAsync<CustomerCheckoutInfoDTO>(query, new { UserId = userId });

            if (customerInfo == null)
            {
                customerInfo = new CustomerCheckoutInfoDTO(); 
            }

            return customerInfo;
        }
        public async Task<string> GetCustomerIdFromUserId(string userId)
        {
            var query = "SELECT CustomerId FROM Customer WHERE UserId = @UserId";
            var result = await _connection.QuerySingleOrDefaultAsync<string>(query, new { UserId = userId });
            return result;
        }
        public async Task<List<string>> GetSuggestedProductsDapper(List<string> productNames)
        {
            try
            {
                Console.WriteLine("test api");

                if (productNames == null || !productNames.Any())
                {
                    Console.WriteLine("productNames null hoặc rỗng.");
                    return new List<string>();
                }

                Console.WriteLine("Tên sản phẩm gửi tới Python API: " + string.Join(", ", productNames));

                var requestUri = "http://127.0.0.1:5000/get_mining_results";

                // Gửi danh sách tên sản phẩm
                var response = await _httpClient.PostAsJsonAsync(requestUri, productNames);

                if (response.IsSuccessStatusCode)
                {
                    var rawJson = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("Raw JSON từ Python: " + rawJson);

                    try
                    {
                        using (var jsonDoc = JsonDocument.Parse(rawJson))
                        {
                            var suggestedProducts = jsonDoc.RootElement
                                .GetProperty("suggested_products")
                                .EnumerateArray()
                                .Select(x => x.GetString())
                                .ToList();

                            Console.WriteLine("Suggested products: " + string.Join(", ", suggestedProducts));

                            return suggestedProducts;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Lỗi khi parse JSON: {ex.Message}");
                        return new List<string>();
                    }
                }
                else
                {
                    Console.WriteLine($"Request lỗi - Status Code: {response.StatusCode}");
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Nội dung lỗi: {errorContent}");
                    return new List<string>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception khi gọi API: {ex.Message}");
                return new List<string>();
            }
        }
        public async Task<List<(string Name, string Img, string Subcategory)>> GetSuggestedProductWithImg(List<string> productNames)
        {
            if (productNames == null || !productNames.Any())
            {
                return new List<(string, string, string)>();
            }

            var query = @"
            SELECT 
                ItemName AS Name, 
                ItemImg AS Img, 
                Subcategory 
            FROM MenuItem 
            WHERE ItemName IN @ProductNames";

            var result = await _connection.QueryAsync<(string Name, string Img, string Subcategory)>(
                query,
                new { ProductNames = productNames }
            );

            return result.ToList();
        }
        public (List<MenuItem> items, int totalPages) Search(string keyword, int page = 1, int pageSize = 6)
        {
            var offset = (page - 1) * pageSize;
            var query = @"
            SELECT * 
            FROM MenuItem 
            WHERE ItemName LIKE @Keyword
            ORDER BY ItemName
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var items = _connection.Query<MenuItem>(query, new { Keyword = $"%{keyword}%", Offset = offset, PageSize = pageSize }).ToList();

            var countQuery = @"SELECT COUNT(*) FROM MenuItem WHERE ItemName LIKE @Keyword";
            var totalItems = _connection.QuerySingle<int>(countQuery, new { Keyword = $"%{keyword}%" });

            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            return (items, totalPages);
        }
        public async Task<List<(string Name, string Img)>> GetSugestedProductByUser(string customerId)
        {
            string cacheKey = $"suggested:{customerId}";
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            string cachedData = await _distributedCache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedData))
            {
                stopwatch.Stop();
                Console.WriteLine($"[Redis] Cache hit! Time: {stopwatch.ElapsedMilliseconds} ms");
                return JsonConvert.DeserializeObject<List<(string Name, string Img)>>(cachedData);
            }

            var query = @"
            SELECT TOP 3
                mi.ItemName,
                mi.ItemImg,
                COUNT(*) AS TimesPurchased
            FROM [Order] o
            JOIN OrderDetail od ON o.OrderId = od.OrderId
            JOIN MenuItem mi ON od.ItemName = mi.ItemName
            WHERE o.CustomerId = @CustomerId
            GROUP BY mi.ItemName, mi.ItemImg
            ORDER BY TimesPurchased DESC";

            var result = await _connection.QueryAsync<(string Name, string Img)>(
                query,
                new { CustomerId = customerId }
            );

            // Lưu vào cache
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            };

            await _distributedCache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(result), cacheOptions);

            stopwatch.Stop();
            Console.WriteLine($"[Redis] Cache miss! Query time from SQL Server: {stopwatch.ElapsedMilliseconds} ms");

            return result.ToList();
        }
    }
}