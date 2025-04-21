using Dapper;
using highlands.Models.DTO;
using highlands.Models.DTO.OrderDTO;
using highlands.Models.DTO.RevenueDTO;
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
            _distributedCache = distributedCache;
        }
        public async Task<IEnumerable<OrderLoadingDTO>> GetOrderAsync()
        {
            Console.WriteLine("[DEBUG] GetOrderAsync");
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
        public async Task<IEnumerable<RevenueBySubCategoryDTO>> GetRevenueBySubCategory(string timeFrame)
        {   
            Console.WriteLine("[DEBUG] GetRevenueBySubCategory");

            const string cacheKeyPrefix = "revenue_by_subcategory_";
            const string timestampKey = "revenue_last_updated";

            string cacheKey = cacheKeyPrefix + timeFrame;

            try
            {
                // Lấy dữ liệu cache
                var cachedData = await _distributedCache.GetStringAsync(cacheKey);
                var lastUpdatedStr = await _distributedCache.GetStringAsync(timestampKey);

                Console.WriteLine($"Cache Key: {cacheKey}");
                Console.WriteLine($"Cache Data: {cachedData}");
                Console.WriteLine($"Last Updated String: {lastUpdatedStr}");

                DateTime lastUpdated = DateTime.MinValue;
                if (!string.IsNullOrEmpty(lastUpdatedStr))
                {
                    if (!DateTime.TryParse(lastUpdatedStr, out lastUpdated))
                    {
                        Console.WriteLine($"Lỗi parse LastUpdatedStr: {lastUpdatedStr}");
                        lastUpdated = DateTime.MinValue;
                    }
                }

                // Xác định khoảng thời gian lọc
                string dateCondition = timeFrame switch
                {
                    "day" => "OrderDate >= CAST(GETDATE() AS DATE)",
                    "week" => "OrderDate >= DATEADD(DAY, -7, CAST(GETDATE() AS DATE))",
                    "month" => "OrderDate >= DATEADD(MONTH, -1, CAST(GETDATE() AS DATE))",
                    "year" => "OrderDate >= DATEADD(YEAR, -1, CAST(GETDATE() AS DATE))",
                    _ => "1=1"
                };

                // Query lấy dữ liệu
                string query = $@"
                    SELECT TOP 5 mi.SubCategory, SUM(od.Price * od.Quantity) AS TotalRevenue
                    FROM OrderDetail od
                    JOIN MenuItem mi ON od.ItemName = mi.ItemName
                    JOIN [Order] o ON od.OrderId = o.OrderId
                    WHERE {dateCondition}
                    GROUP BY mi.SubCategory
                    ORDER BY TotalRevenue DESC;";

                var newData = (await _connection.QueryAsync<RevenueBySubCategoryDTO>(query)).ToList();

                Console.WriteLine("Dữ liệu từ DB:");
                foreach (var item in newData)
                {
                    Console.WriteLine($"- {item.SubCategory}: {item.TotalRevenue}");
                }

                // Kiểm tra MAX(OrderDate)
                const string maxDateQuery = "SELECT MAX(OrderDate) FROM [Order];";
                var latestOrderDate = await _connection.ExecuteScalarAsync<DateTime?>(maxDateQuery) ?? lastUpdated;

                Console.WriteLine($"MAX(OrderDate) từ DB: {latestOrderDate}");

                // Nếu có dữ liệu mới, cập nhật lại cache
                if (newData.Any())
                {
                    await _distributedCache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(newData));
                    await _distributedCache.SetStringAsync(timestampKey, latestOrderDate.ToString("o"));
                    Console.WriteLine("Cache đã được cập nhật!");
                }
                else
                {
                    Console.WriteLine("Không có dữ liệu mới, giữ nguyên cache.");
                }

                return newData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi trong GetRevenueBySubCategory: {ex.Message}");
                return new List<RevenueBySubCategoryDTO>();
            }
        }
        public async Task<RevenueByMonth> GetRevenueAndTotalOrdersByMonth()
        {
            try
            {
                await _distributedCache.RemoveAsync($"revenue_by_month_{DateTime.UtcNow:yyyyMM}");
                await _distributedCache.RemoveAsync("revenue_last_updated");

                string cacheKey = $"revenue_by_month_{DateTime.UtcNow:yyyyMM}";
                string timestampKey = "revenue_last_updated";

                Console.WriteLine("[DEBUG] Kiểm tra cache trước khi query DB...");

                //Lấy cache từ Redis
                var cachedData = await _distributedCache.GetStringAsync(cacheKey);
                var lastUpdatedStr = await _distributedCache.GetStringAsync(timestampKey);

                DateTime lastUpdated = DateTime.MinValue;
                if (!string.IsNullOrEmpty(lastUpdatedStr))
                {
                    DateTime.TryParse(lastUpdatedStr, out lastUpdated);
                }

                //Kiểm tra MAX(OrderDate) trong DB
                const string maxDateQuery = "SELECT MAX(OrderDate) FROM [Order];";
                var latestOrderDate = await _connection.ExecuteScalarAsync<DateTime?>(maxDateQuery) ?? DateTime.MinValue;

                //Nếu cache có dữ liệu và không có order mới, dùng cache luôn
                if (!string.IsNullOrEmpty(cachedData) && latestOrderDate <= lastUpdated)
                {
                    Console.WriteLine("Lấy dữ liệu từ cache (không có order mới).");
                    return JsonConvert.DeserializeObject<RevenueByMonth>(cachedData);
                }

                Console.WriteLine("Có order mới, query DB...");

                //Query DB nếu có order mới
                var sql = @"SELECT 
                    (SELECT SUM(TotalAmount) 
                    FROM [Order] 
                    WHERE YEAR(OrderDate) = YEAR(GETDATE()) 
                    AND MONTH(OrderDate) = MONTH(GETDATE())) AS TotalRevenue,

                    COUNT(o.OrderId) AS TotalOrders,
                    COUNT(DISTINCT o.CustomerId) AS TotalCustomers,
                    SUM(od.Quantity) AS TotalQuantity
                    FROM [Order] o
                    JOIN [OrderDetail] od ON o.OrderId = od.OrderId
                    WHERE YEAR(o.OrderDate) = YEAR(GETDATE()) 
                      AND MONTH(o.OrderDate) = MONTH(GETDATE());";

                var result = await _connection.QueryFirstOrDefaultAsync<RevenueByMonth>(sql) ?? new RevenueByMonth();

                //Lưu vào cache
                await _distributedCache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(result));
                await _distributedCache.SetStringAsync(timestampKey, latestOrderDate.ToString("o"));

                Console.WriteLine("Cập nhật cache thành công!");

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi lấy revenue: {ex.Message}");
                return new RevenueByMonth();
            }
        }
        public async Task<List<OrderHistoryDTO>> GetOrderHistoryByUser(string customerId)
        {
            string sql = @"
            SELECT 
                o.OrderId,
                o.OrderDate,
                o.TotalAmount,
                od.ItemName,
                mi.ItemImg,
                od.Quantity,
                od.Price,
                od.Size 
            FROM [Order] o
            JOIN OrderDetail od ON o.OrderId = od.OrderId
            JOIN MenuItem mi ON od.ItemName = mi.ItemName
            WHERE o.CustomerId = @CustomerId
            ORDER BY o.OrderDate DESC";

            var orderDict = new Dictionary<int, OrderHistoryDTO>();

            var result = await _connection.QueryAsync<OrderHistoryDTO, OrderItemDTO, OrderHistoryDTO>(
                sql,
                (order, item) =>
                {
                    if (!orderDict.TryGetValue(order.OrderId, out var orderEntry))
                    {
                        orderEntry = order;
                        orderEntry.Items = new List<OrderItemDTO>();
                        orderDict.Add(order.OrderId, orderEntry);
                    }

                    item.Size = item.Size ?? ""; 

                    orderEntry.Items.Add(item);
                    return orderEntry;
                },
                new { CustomerId = customerId },
                splitOn: "ItemName"
            );

            return orderDict.Values.ToList();
        }
    }
}
