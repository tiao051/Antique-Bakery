using Dapper;
using highlands.Models.DTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using System.Data;

namespace highlands.Repository.OrderRepository
{
    public class OrderRepository
    {
        private readonly IDbConnection _connection;
        private readonly IDistributedCache _distributedCache;

        public OrderRepository(IDbConnection connection, IDistributedCache distributedCache)
        {
            _connection = connection;
            Console.WriteLine($"OrderRepository initialized with connection type: {_connection.GetType().FullName}");
            _distributedCache = distributedCache;
        }
        public async Task<IEnumerable<OrderLoadingDTO>> GetOrderAsync()
        {
            string cacheKey = "orders_cache";

            await _distributedCache.RemoveAsync(cacheKey);

            // Retrieve cached data
            var cachedData = await _distributedCache.GetStringAsync(cacheKey);
            Console.WriteLine($"Cached Data: {cachedData}");
            if (!string.IsNullOrEmpty(cachedData))
            {
                try
                {
                    // Kiểm tra nếu JSON hợp lệ trước khi deserialize
                    if (cachedData.StartsWith("[") && cachedData.EndsWith("]"))
                    {
                        var cachedOrders = JsonConvert.DeserializeObject<List<OrderLoadingDTO>>(cachedData);
                        if (cachedOrders != null) return cachedOrders;
                    }
                    else
                    {
                        Console.WriteLine("Invalid cache format, removing corrupted cache.");
                        await _distributedCache.RemoveAsync(cacheKey);
                    }
                }
                catch (JsonSerializationException ex)
                {
                    Console.WriteLine($"Cache deserialization error: {ex.Message}");
                    await _distributedCache.RemoveAsync(cacheKey);
                }
            }

            // Fetch from DB if cache is empty or corrupted
            string sql = @"
                SELECT TOP 5 OrderId, OrderDate, TotalAmount, Status, CustomerId 
                FROM [Order] 
                ORDER BY OrderDate DESC";
            var orders = (await _connection.QueryAsync<OrderLoadingDTO>(sql)).ToList();

            Console.WriteLine("Fetched Orders from DB:");
            foreach (var order in orders)
            {
                Console.WriteLine(JsonConvert.SerializeObject(order, Formatting.Indented));
            }

            // Nếu không có dữ liệu thì không cache
            if (orders == null || !orders.Any()) return orders;

            var cacheOptions = new DistributedCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromHours(2));
            await _distributedCache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(orders), cacheOptions);

            return orders;
        }
        public async Task<IEnumerable<RevenueBySubCategoryDTO>> GetRevenueBySubCategory()
        {
            const string cacheKey = "revenue_by_subcategory";
            const string timestampKey = "revenue_last_updated";

            // Lấy dữ liệu từ cache
            var cachedData = await _distributedCache.GetStringAsync(cacheKey);
            var lastUpdatedStr = await _distributedCache.GetStringAsync(timestampKey);

            DateTime lastUpdated = string.IsNullOrEmpty(lastUpdatedStr)
                ? new DateTime(1753, 1, 1) // Giá trị mặc định rất xa nếu cache rỗng
                : DateTime.Parse(lastUpdatedStr);

            Console.WriteLine($"LastUpdatedStr từ cache: {lastUpdatedStr}");
            Console.WriteLine($"LastUpdated sau khi parse: {lastUpdated}");

            // Query lấy dữ liệu mới
            const string query = @"
            SELECT mi.SubCategory, SUM(od.Price * od.Quantity) AS TotalRevenue
            FROM OrderDetail od
            JOIN MenuItem mi ON od.ItemName = mi.ItemName
            JOIN [Order] o ON od.OrderId = o.OrderId
            WHERE o.OrderDate > @LastUpdated
            GROUP BY mi.SubCategory
            ORDER BY TotalRevenue DESC;";

            var newData = await _connection.QueryAsync<RevenueBySubCategoryDTO>(query, new { LastUpdated = lastUpdated });

            Console.WriteLine("Dữ liệu mới từ DB:");
            foreach (var item in newData)
            {
                Console.WriteLine($"- {item.SubCategory}: {item.TotalRevenue}");
            }

            //Lấy OrderDate mới nhất để cập nhật cache
            const string maxDateQuery = @"SELECT MAX(OrderDate) FROM [Order];";
            var latestOrderDate = await _connection.ExecuteScalarAsync<DateTime?>(maxDateQuery) ?? lastUpdated;

            Console.WriteLine($"MAX(OrderDate) từ DB: {latestOrderDate}");

            // Nếu có dữ liệu mới, cập nhật lại cache
            if (newData.Any())
            {
                await _distributedCache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(newData));
                await _distributedCache.SetStringAsync(timestampKey, latestOrderDate.ToString("o")); // Format ISO 8601

                Console.WriteLine("Cache đã được cập nhật!");
            }
            else
            {
                Console.WriteLine("Không có dữ liệu mới, cache không thay đổi.");
            }

            return newData;
        }

    }
}
