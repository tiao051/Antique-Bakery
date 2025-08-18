using highlands.Interfaces;
using highlands.Models.DTO.ReportDTO;

namespace highlands.Services.ReportServices
{
    public class ReportEFService
    {
        private readonly IReportRepository _reportEFRepo;
        public ReportEFService(IReportRepository reportEFRepo)
        {
            _reportEFRepo = reportEFRepo;
        }
        public async Task<ReportData> GenerateReportAsync(string type)
        {
            // Xác định phạm vi thời gian dựa trên loại báo cáo
            var timeRange = GetTimeRange(type);

            // Thực hiện các truy vấn song song
            var totalRevenue = await _reportEFRepo.GetTotalRevenue(timeRange.Item2, timeRange.Item3);
            var bestSellers = await _reportEFRepo.GetBestSellersBy5(timeRange.Item2, timeRange.Item3);
            var worstSellers = await _reportEFRepo.GetWorstSellersBy5(timeRange.Item2, timeRange.Item3);
            var revenueByCategory = await _reportEFRepo.GetRevenueByCategory(timeRange.Item2, timeRange.Item3);
            var revenueByProduct = await _reportEFRepo.GetRevenueByProduct(timeRange.Item2, timeRange.Item3);
            var topCustomers = await _reportEFRepo.GetTopCustomers(timeRange.Item2, timeRange.Item3);
            var peakTime = await _reportEFRepo.GetPeakTime(timeRange.Item2, timeRange.Item3);
            var offTime = await _reportEFRepo.GetOffTime(timeRange.Item2, timeRange.Item3);

            // Lấy kết quả từ các tác vụ
            return new ReportData
            {
                ReportType = type,
                TimeRangeText = timeRange.Item1,
                TotalRevenue = (int)totalRevenue,
                BestSellers = bestSellers,
                WorstSellers = worstSellers,
                RevenueByCategory = revenueByCategory,
                RevenueByProduct = revenueByProduct,
                TopCustomers = topCustomers,
                PeakTime = peakTime,
                OffTime = offTime
            };
        }

        private Tuple<string, DateTime, DateTime> GetTimeRange(string type)
        {
            DateTime startDate, endDate;
            string timeText;

            endDate = DateTime.Now;

            switch (type.ToLower())
            {
                case "daily":
                    startDate = DateTime.Today;
                    timeText = $"{startDate:dd/MM/yyyy}";
                    break;

                case "weekly":
                    int daysFromMonday = ((int)DateTime.Now.DayOfWeek + 6) % 7;
                    startDate = DateTime.Today.AddDays(-daysFromMonday);
                    timeText = $"{startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}";
                    break;

                case "monthly":
                    startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                    timeText = $"{startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}";
                    break;

                case "quarterly":
                    int quarter = (DateTime.Now.Month - 1) / 3 + 1;
                    startDate = new DateTime(DateTime.Now.Year, (quarter - 1) * 3 + 1, 1);
                    timeText = $"Q{quarter} {DateTime.Now.Year} ({startDate:dd/MM} - {endDate:dd/MM/yyyy})";
                    break;

                default:
                    throw new ArgumentException("Invalid report type");
            }

            return new Tuple<string, DateTime, DateTime>(timeText, startDate, endDate);
        }
    }
}
