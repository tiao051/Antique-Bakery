using Dapper;
using highlands.Models;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using System.Data;
using System.Data.SqlClient;

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
        public async Task<IEnumerable<Order>> GetOrderAsync()
        {
            string cacheKey = "orders_cache";

            // Retrieve cached data
            var cachedData = await _distributedCache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedData))
            {
                try
                {
                    var cachedOrders = JsonConvert.DeserializeObject<List<Order>>(cachedData);
                    if (cachedOrders != null)
                    {
                        return cachedOrders; // Return cached data if available
                    }
                }
                catch (JsonSerializationException ex)
                {
                    Console.WriteLine($"Cache deserialization error: {ex.Message}");
                    // Handle corrupted cache by removing it
                    await _distributedCache.RemoveAsync(cacheKey);
                }
            }

            // Fetch from DB if cache is empty or corrupted
            string sql = "SELECT * FROM [Order]";
            var orders = (await _connection.QueryAsync<Order>(sql)).ToList();

            // Store data in cache
            var cacheOptions = new DistributedCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromHours(2));

            await _distributedCache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(orders), cacheOptions);

            return orders;
        }
    }
}
