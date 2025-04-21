using highlands.Models.DTO.ReportDTO;

namespace highlands.Interfaces
{
    public interface IReportRepository
    {
        Task<decimal> GetTotalRevenue(DateTime start, DateTime end);
        Task<List<ProductStat>> GetBestSellersBy5(DateTime start, DateTime end);
        Task<List<ProductStat>> GetWorstSellersBy5(DateTime start, DateTime end);
        Task<List<CategoryRevenue>> GetRevenueByCategory(DateTime start, DateTime end);
        Task<List<ProductRevenue>> GetRevenueByProduct(DateTime start, DateTime end);
        Task<List<TopCustomer>> GetTopCustomers(DateTime startDate, DateTime endDate);
        Task<TimeRevenueStat> GetPeakTime(DateTime startDate, DateTime endDate);
        Task<TimeRevenueStat> GetOffTime(DateTime startDate, DateTime endDate);
    }
}
