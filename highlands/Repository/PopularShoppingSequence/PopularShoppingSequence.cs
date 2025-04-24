using System.Data;
using System.Text.Json;
using Dapper;
using highlands.Models.DTO.PopularSequenceDTO;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Distributed;

public class PopularShoppingSequence
{
    private readonly string _connectionString;
    private readonly IDistributedCache _cache;

    public PopularShoppingSequence(IConfiguration configuration, IDistributedCache cache)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection"); 
        _cache = cache;
    }
    public async Task<List<SequenceResult>> GetPopularSequencesAsync(int topN = 20)
    {
        string cacheKey = $"popular_sequences_top_{topN}";
        _cache.RemoveAsync(cacheKey);
        var cached = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cached))
        {
            return JsonSerializer.Deserialize<List<SequenceResult>>(cached)!;
        }

        string sql = @"
        WITH ProductCombinations AS (
            SELECT DISTINCT
                od.OrderId,
                od.ItemName
            FROM OrderDetail od
        )
        , ComboList AS (
            SELECT
                STRING_AGG(pc.ItemName, ' | ') AS Combo,
                pc.OrderId
            FROM ProductCombinations pc
            JOIN OrderDetail od ON pc.OrderId = od.OrderId AND pc.ItemName = od.ItemName
            GROUP BY pc.OrderId
            HAVING COUNT(DISTINCT pc.ItemName) > 1  
        )
        SELECT
            Combo,
            COUNT(*) AS Count
        FROM ComboList
        GROUP BY Combo
        ORDER BY Count DESC
        OFFSET 0 ROWS FETCH NEXT @TopN ROWS ONLY;";

        using var conn = new SqlConnection(_connectionString);
        var result = (await conn.QueryAsync<SequenceResult>(sql, new { TopN = topN })).ToList();

        var json = JsonSerializer.Serialize(result);
        await _cache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
        });

        return result;
    }
}
