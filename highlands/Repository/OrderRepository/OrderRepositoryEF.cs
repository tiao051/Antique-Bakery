using highlands.Data;
using highlands.Models.DTO;
using highlands.Models.DTO.RevenueDTO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;

namespace highlands.Repository.OrderRepository
{
    public class OrderRepositoryEF
    {
        private readonly AppDbContext _db;
        private readonly IDistributedCache _distributedCache;

        public OrderRepositoryEF(AppDbContext db, IDistributedCache distributedCache)
        {
            _db = db;
            _distributedCache = distributedCache;
        }

        // Lấy 5 order mới nhất
        public async Task<IEnumerable<OrderLoadingDTO>> GetOrderAsync()
        {
            Console.WriteLine("[DEBUG] GetOrderAsync");
            string cacheKey = "orders_cache";

            await _distributedCache.RemoveAsync(cacheKey); // nếu muốn invalidation mỗi lần gọi

            var cachedData = await _distributedCache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedData))
            {
                try
                {
                    var cachedOrders = JsonConvert.DeserializeObject<List<OrderLoadingDTO>>(cachedData);
                    if (cachedOrders != null) return cachedOrders;
                }
                catch
                {
                    await _distributedCache.RemoveAsync(cacheKey);
                }
            }

            var orders = await _db.Orders
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .Select(o => new OrderLoadingDTO
                {
                    OrderId = o.OrderId,
                    OrderDate = (DateTime)o.OrderDate,
                    TotalAmount = (decimal)o.TotalAmount,
                    Status = o.Status,
                    CustomerId = (int)o.CustomerId
                })
                .ToListAsync();

            if (orders.Any())
            {
                var cacheOptions = new DistributedCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromHours(2));
                await _distributedCache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(orders), cacheOptions);
            }
            Console.WriteLine("tra ve ket qua cho ef");
            return orders;
        }

        // Lấy revenue theo SubCategory
        public async Task<IEnumerable<RevenueBySubCategoryDTO>> GetRevenueBySubCategory(string timeFrame)
        {
            Console.WriteLine("[DEBUG] GetRevenueBySubCategory");

            const string cacheKeyPrefix = "revenue_by_subcategory_";
            const string timestampKey = "revenue_last_updated";

            string cacheKey = cacheKeyPrefix + timeFrame;

            var cachedData = await _distributedCache.GetStringAsync(cacheKey);
            var lastUpdatedStr = await _distributedCache.GetStringAsync(timestampKey);

            DateTime lastUpdated = DateTime.MinValue;
            if (!string.IsNullOrEmpty(lastUpdatedStr) && DateTime.TryParse(lastUpdatedStr, out var parsed))
            {
                lastUpdated = parsed;
            }

            // Xác định khoảng thời gian
            DateTime fromDate = timeFrame switch
            {
                "day" => DateTime.Today,
                "week" => DateTime.Today.AddDays(-7),
                "month" => DateTime.Today.AddMonths(-1),
                "year" => DateTime.Today.AddYears(-1),
                _ => DateTime.MinValue
            };

            // EF Core query
            var newData = await _db.OrderDetails
                .Where(od => fromDate == DateTime.MinValue || od.Order.OrderDate >= fromDate)
                .GroupBy(od => od.ItemNameNavigation.SubCategory)
                .Select(g => new RevenueBySubCategoryDTO
                {
                    SubCategory = g.Key!,
                    TotalRevenue = (decimal)g.Sum(x => x.Price * x.Quantity)
                })
                .OrderByDescending(r => r.TotalRevenue)
                .Take(5)
                .ToListAsync();

            Console.WriteLine("Dữ liệu từ DB:");
            foreach (var item in newData)
            {
                Console.WriteLine($"- {item.SubCategory}: {item.TotalRevenue}");
            }

            // Check MAX(OrderDate)
            var latestOrderDate = await _db.Orders.MaxAsync(o => (DateTime?)o.OrderDate) ?? lastUpdated;

            if (latestOrderDate > lastUpdated)
            {
                await _distributedCache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(newData));
                await _distributedCache.SetStringAsync(timestampKey, latestOrderDate.ToString("O"));
            }

            return newData;
        }
    }
}