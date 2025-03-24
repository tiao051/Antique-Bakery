using Dapper;
using highlands.Models.DTO;
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
    }
}
