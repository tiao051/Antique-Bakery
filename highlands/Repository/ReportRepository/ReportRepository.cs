using Dapper;
using highlands.Interfaces;
using highlands.Models.DTO.ReportDTO;
using System.Data;

namespace highlands.Repository.ReportRepository
{
    public class ReportRepository : IReportRepository
    {
        private readonly IDbConnection _db;

        public ReportRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<ReportData> GenerateReportAsync(DateTime startDate, DateTime endDate)
        {
            var reportData = new ReportData();

            reportData.TotalRevenue = (int)await GetTotalRevenue(startDate, endDate);

            reportData.BestSellers = await GetBestSellersBy5(startDate, endDate);
            reportData.WorstSellers = await GetWorstSellersBy5(startDate, endDate);

            reportData.RevenueByCategory = await GetRevenueByCategory(startDate, endDate);
            reportData.RevenueByProduct = await GetRevenueByProduct(startDate, endDate);

            reportData.TopCustomers = await GetTopCustomers(startDate, endDate);

            reportData.PeakTime = await GetPeakTime(startDate, endDate);
            reportData.OffTime = await GetOffTime(startDate, endDate);

            return reportData;
        }

        public async Task<decimal> GetTotalRevenue(DateTime startDate, DateTime endDate)
        {
            string sql = @"
            SELECT SUM(TotalAmount) AS Revenue
            FROM [Order] 
            WHERE OrderDate BETWEEN @StartDate AND @EndDate";
            return await _db.ExecuteScalarAsync<decimal?>(sql, new { StartDate = startDate, EndDate = endDate }) ?? 0;
        }

        public async Task<List<ProductStat>> GetBestSellersBy5(DateTime startDate, DateTime endDate)
        {
            string sql = @"
            SELECT od.ItemName AS Name, SUM(od.Quantity) AS Quantity
            FROM OrderDetail od
            JOIN [Order] o ON od.OrderId = o.OrderId
            WHERE o.OrderDate BETWEEN @StartDate AND @EndDate
            GROUP BY od.ItemName
            ORDER BY Quantity DESC
            OFFSET 0 ROWS FETCH NEXT 5 ROWS ONLY";

            return (await _db.QueryAsync<ProductStat>(sql, new { StartDate = startDate, EndDate = endDate })).ToList();
        }

        public async Task<List<ProductStat>> GetWorstSellersBy5(DateTime startDate, DateTime endDate)
        {
            string sql = @"
            SELECT od.ItemName AS Name, SUM(od.Quantity) AS Quantity
            FROM OrderDetail od
            JOIN [Order] o ON od.OrderId = o.OrderId
            WHERE o.OrderDate BETWEEN @StartDate AND @EndDate
            GROUP BY od.ItemName
            ORDER BY Quantity ASC
            OFFSET 0 ROWS FETCH NEXT 5 ROWS ONLY";
            return (await _db.QueryAsync<ProductStat>(sql, new { StartDate = startDate, EndDate = endDate })).ToList();
        }

        public async Task<List<CategoryRevenue>> GetRevenueByCategory(DateTime startDate, DateTime endDate)
        {
            string sql = @"
            SELECT m.Category, SUM(od.Quantity * od.Price) AS Revenue
            FROM [Order] o
            JOIN OrderDetail od ON o.OrderId = od.OrderId
            JOIN MenuItem m ON od.ItemName = m.ItemName
            WHERE o.OrderDate BETWEEN @StartDate AND @EndDate
            GROUP BY m.Category";

            return (await _db.QueryAsync<CategoryRevenue>(sql, new { StartDate = startDate, EndDate = endDate })).ToList();
        }

        public async Task<List<ProductRevenue>> GetRevenueByProduct(DateTime startDate, DateTime endDate)
        {
            string sql = @"
            SELECT od.ItemName AS Name, SUM(od.Quantity * od.Price) AS Revenue
            FROM OrderDetail od
            JOIN [Order] o ON od.OrderId = o.OrderId
            WHERE o.OrderDate BETWEEN @StartDate AND @EndDate
            GROUP BY od.ItemName
            ORDER BY Revenue DESC";
            return (await _db.QueryAsync<ProductRevenue>(sql, new { StartDate = startDate, EndDate = endDate })).ToList();
        }

        public async Task<List<TopCustomer>> GetTopCustomers(DateTime startDate, DateTime endDate)
        {
            string sql = @"
            SELECT TOP 10 
                COALESCE(c.FullName, 'Guest') AS CustomerName, 
                COUNT(o.OrderId) AS OrderCount, 
                SUM(od.Quantity * od.Price) AS TotalSpent
            FROM [Order] o
            JOIN OrderDetail od ON o.OrderId = od.OrderId
            JOIN Customer c ON o.CustomerId = c.CustomerId
            WHERE o.OrderDate BETWEEN @StartDate AND @EndDate
            GROUP BY c.FullName
            ORDER BY TotalSpent DESC";
            return (await _db.QueryAsync<TopCustomer>(sql, new { StartDate = startDate, EndDate = endDate })).ToList();
        }

        public async Task<TimeRevenueStat> GetPeakTime(DateTime startDate, DateTime endDate)
        {
            string sql = @"
            SELECT TOP 1
                FORMAT(o.OrderDate, 'HH:mm') AS TimeRange, 
                SUM(od.Quantity * od.Price) AS TotalRevenue
            FROM [Order] o
            JOIN OrderDetail od ON o.OrderId = od.OrderId
            WHERE o.OrderDate BETWEEN @StartDate AND @EndDate
            GROUP BY FORMAT(o.OrderDate, 'HH:mm')
            ORDER BY TotalRevenue DESC";
            var result = await _db.QueryFirstOrDefaultAsync<TimeRevenueStat>(sql, new { StartDate = startDate, EndDate = endDate });
            return result ?? new TimeRevenueStat { TimeRange = "00:00 - 00:00", Revenue = 0 };
        }

        public async Task<TimeRevenueStat> GetOffTime(DateTime startDate, DateTime endDate)
        {
            string sql = @"
            SELECT TOP 1
                FORMAT(o.OrderDate, 'HH:mm') AS TimeRange, 
                SUM(od.Quantity * od.Price) AS TotalRevenue
            FROM [Order] o
            JOIN OrderDetail od ON o.OrderId = od.OrderId
            WHERE o.OrderDate BETWEEN @StartDate AND @EndDate
            GROUP BY FORMAT(o.OrderDate, 'HH:mm')
            ORDER BY TotalRevenue ASC";
            var result = await _db.QueryFirstOrDefaultAsync<TimeRevenueStat>(sql, new { StartDate = startDate, EndDate = endDate });
            return result ?? new TimeRevenueStat { TimeRange = "00:00 - 00:00", Revenue = 0 };
        }
    }
}
