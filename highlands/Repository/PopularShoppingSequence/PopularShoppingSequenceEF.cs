namespace highlands.Repository.PopularShoppingSequence
{
    using System.Text.Json;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Distributed;
    using highlands.Models.DTO.PopularSequenceDTO;
    using highlands.Data;

    public class PopularShoppingSequenceEF
    {
        private readonly AppDbContext _db; 
        private readonly IDistributedCache _cache;

        public PopularShoppingSequenceEF(AppDbContext db, IDistributedCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task<List<SequenceResult>> GetPopularSequencesAsync(int topN = 20)
        {
            string cacheKey = $"popular_sequences_top_{topN}";

            // Xóa cache cũ (nếu muốn invalidation mỗi lần gọi)
            await _cache.RemoveAsync(cacheKey);

            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                return JsonSerializer.Deserialize<List<SequenceResult>>(cached)!;
            }

            // EF Core LINQ query
            var query = _db.OrderDetails
                .GroupBy(od => od.OrderId)
                .Where(g => g.Select(x => x.ItemName).Distinct().Count() > 1) // chỉ lấy đơn có nhiều hơn 1 sản phẩm
                .Select(g => new
                {
                    Combo = string.Join(" | ", g.Select(x => x.ItemName).Distinct().OrderBy(x => x)), // build combo string
                    OrderId = g.Key
                })
                .GroupBy(x => x.Combo)
                .Select(g => new SequenceResult
                {
                    Combo = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(r => r.Count)
                .Take(topN);

            var result = await query.ToListAsync();

            // Save vào cache
            var json = JsonSerializer.Serialize(result);
            await _cache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            });

            return result;
        }
    }
}
