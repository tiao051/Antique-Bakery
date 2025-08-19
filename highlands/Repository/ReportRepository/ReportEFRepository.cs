using highlands.Data;
using highlands.Interfaces;
using highlands.Models.DTO.ReportDTO;
using Microsoft.EntityFrameworkCore;

namespace highlands.Repository.ReportRepository
{
    public class ReportEFRepository : IReportRepository
    {
        private readonly AppDbContext _context;
        public ReportEFRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task<ReportData> GenerateReportAsync(DateTime startDate, DateTime endDate)
        {
            var reportData = new ReportData();

            reportData.TotalRevenue = (int)await GetTotalRevenue(startDate, endDate);

            reportData.BestSellers = await GetBestSellerBy5(startDate, endDate);
            reportData.WorstSellers = await GetWorstSellerBy5(startDate, endDate);

            reportData.RevenueByCategory = await GetRevenueByCategory(startDate, endDate);
            reportData.RevenueByProduct = await GetRevenueByProduct(startDate, endDate);

            reportData.TopCustomers = await GetTopCustomers(startDate, endDate);

            reportData.PeakTime = await GetPeakTime(startDate, endDate);
            reportData.OffTime = await GetOffTime(startDate, endDate);

            return reportData;
        }
        public async Task<decimal> GetTotalRevenue(DateTime startDate, DateTime endDate)
        {
            return await _context.Orders
                   .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate)
                   .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
        }
        public async Task<List<ProductStat>> GetBestSellerBy5(DateTime startDate, DateTime endDate)
        {
            return await _context.OrderDetails
                .Where(od => od.Order.OrderDate >= startDate && od.Order.OrderDate <= endDate)
                .GroupBy(od => od.ItemName)
                .Select(g => new ProductStat
                {
                    Name = g.Key ?? string.Empty,
                    Quantity = g.Sum(x => (int?)x.Quantity) ?? 0
                })
                .OrderByDescending(x => x.Quantity)
                .Take(5).ToListAsync();
        }
        public async Task<List<ProductStat>> GetWorstSellerBy5(DateTime startDate, DateTime endDate)
        {
            return await _context.OrderDetails
                .Where(od => od.Order.OrderDate >= startDate && od.Order.OrderDate <= endDate)
                .GroupBy(od => od.ItemName)
                .Select(g => new ProductStat
                {
                    Name = g.Key ?? string.Empty,
                    Quantity = g.Sum(x => (int?)x.Quantity) ?? 0
                })
                .OrderByDescending(x => x.Quantity)
                .Take(5).ToListAsync();
        }
        public async Task<List<CategoryRevenue>> GetRevenueByCategory(DateTime startDate, DateTime endDate)
        {
            return await _context.OrderDetails
               .Where(od => od.Order.OrderDate >= startDate && od.Order.OrderDate <= endDate)
               .GroupBy(od => od.ItemNameNavigation.Category)
               .Select(g => new CategoryRevenue
               {
                   Category = g.Key ?? string.Empty,
                   Revenue = g.Sum(x => (int?)x.Quantity * x.Price) ?? 0
               })
               .OrderByDescending(x => x.Revenue)
               .ToListAsync();
        }
        public async Task<List<ProductRevenue>> GetRevenueByProduct(DateTime startDate, DateTime endDate)
        {
            return await _context.OrderDetails
               .Where(od => od.Order.OrderDate >= startDate && od.Order.OrderDate <= endDate)
               .GroupBy(od => od.ItemName)
               .Select(g => new ProductRevenue
               {
                   Name = g.Key ?? string.Empty,
                   Revenue = g.Sum(x => (int?)x.Quantity * x.Price) ?? 0
               })
               .OrderByDescending(x => x.Revenue)
               .ToListAsync();
        }
        public async Task<List<TopCustomer>> GetTopCustomers(DateTime startDate, DateTime endDate)
        {
            return await _context.Orders
                .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate)
                .Join(_context.OrderDetails,
                      o => o.OrderId,
                      od => od.OrderId,
                      (o, od) => new { o, od })
                .Join(_context.Customers,
                      x => x.o.CustomerId,
                      c => c.CustomerId,
                      (x, c) => new { x.o, x.od, c })
                .GroupBy(g => g.c.FullName)
                .Select(g => new TopCustomer
                {
                    CustomerName = g.Key ?? "Guest",
                    OrderCount = g.Select(x => x.o.OrderId).Distinct().Count(),
                    TotalSpent = (decimal)g.Sum(x => x.od.Quantity * x.od.Price)
                })
                .OrderByDescending(x => x.TotalSpent)
                .Take(10)
                .ToListAsync();
        }
        public async Task<TimeRevenueStat> GetPeakTime(DateTime startDate, DateTime endDate)
        {
            var row = await _context.OrderDetails
                .Where(od => od.Order.OrderDate.HasValue
                             && od.Order.OrderDate.Value >= startDate
                             && od.Order.OrderDate.Value <= endDate)
                .GroupBy(od => new
                {
                    Hour = od.Order.OrderDate!.Value.Hour,
                    Minute = od.Order.OrderDate!.Value.Minute
                })
                .Select(g => new
                {
                    g.Key.Hour,
                    g.Key.Minute,
                    Revenue = g.Sum(x => x.Quantity * x.Price)
                })
                .OrderByDescending(x => x.Revenue)
                .FirstOrDefaultAsync();

            return row == null
                ? new TimeRevenueStat { TimeRange = "00:00 - 00:00", Revenue = 0 }
                : new TimeRevenueStat { TimeRange = $"{row.Hour:D2}:{row.Minute:D2}", Revenue = (decimal)row.Revenue };
        }

        public async Task<TimeRevenueStat> GetOffTime(DateTime startDate, DateTime endDate)
        {
            var row = await _context.OrderDetails
                .Where(od => od.Order.OrderDate.HasValue
                             && od.Order.OrderDate.Value >= startDate
                             && od.Order.OrderDate.Value <= endDate)
                .GroupBy(od => new
                {
                    Hour = od.Order.OrderDate!.Value.Hour,
                    Minute = od.Order.OrderDate!.Value.Minute
                })
                .Select(g => new
                {
                    g.Key.Hour,
                    g.Key.Minute,
                    Revenue = g.Sum(x => x.Quantity * x.Price)
                })
                .OrderBy(x => x.Revenue)
                .FirstOrDefaultAsync();

            return row == null
                ? new TimeRevenueStat { TimeRange = "00:00 - 00:00", Revenue = 0 }
                : new TimeRevenueStat { TimeRange = $"{row.Hour:D2}:{row.Minute:D2}", Revenue = (decimal)row.Revenue };
        }

        public Task<List<ProductStat>> GetBestSellersBy5(DateTime start, DateTime end)
        {
            throw new Exception("dapper should hanlde this method");
        }

        public Task<List<ProductStat>> GetWorstSellersBy5(DateTime start, DateTime end)
        {
            throw new Exception("dapper should hanlde this method");
        }
    }
}
