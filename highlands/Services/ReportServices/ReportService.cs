using highlands.Models.DTO.ReportDTO;

namespace highlands.Services.ReportServices
{
    public class ReportService
    {
        public ReportData GenerateReport(string type)
        {
            // Xác định phạm vi thời gian dựa trên loại báo cáo
            var timeRange = GetTimeRange(type);

            return new ReportData
            {
                ReportType = type,
                TimeRangeText = timeRange.Item1,
                TotalRevenue = (int)CalculateTotalRevenue(timeRange.Item2, timeRange.Item3),  // Hàm tính tổng doanh thu
                BestSellers = GetBestSellers(timeRange.Item2, timeRange.Item3),
                WorstSellers = GetWorstSellers(timeRange.Item2, timeRange.Item3),
                RevenueByCategory = GetRevenueByCategory(timeRange.Item2, timeRange.Item3),
                RevenueByProduct = GetRevenueByProduct(timeRange.Item2, timeRange.Item3),
                TopCustomers = GetTopCustomers(timeRange.Item2, timeRange.Item3),
                PeakTime = GetPeakTime(timeRange.Item2, timeRange.Item3),
                OffTime = GetOffTime(timeRange.Item2, timeRange.Item3)
            };
        }

        // Hàm lấy thời gian tương ứng với loại báo cáo
        private Tuple<string, DateTime, DateTime> GetTimeRange(string type)
        {
            DateTime startDate, endDate;
            string timeText;

            switch (type.ToLower())
            {
                case "daily":
                    startDate = endDate = DateTime.Now.Date;
                    timeText = $"{startDate:dd/MM/yyyy}";
                    break;
                case "weekly":
                    startDate = DateTime.Now.AddDays(-(int)DateTime.Now.DayOfWeek);
                    endDate = startDate.AddDays(7).AddSeconds(-1);
                    timeText = $"{startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}";
                    break;
                case "monthly":
                    startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                    endDate = startDate.AddMonths(1).AddDays(-1);
                    timeText = $"{startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}";
                    break;
                case "quarterly":
                    int quarter = (DateTime.Now.Month - 1) / 3 + 1;
                    startDate = new DateTime(DateTime.Now.Year, (quarter - 1) * 3 + 1, 1);
                    endDate = startDate.AddMonths(3).AddDays(-1);
                    timeText = $"Q{quarter} {DateTime.Now.Year}";
                    break;
                default:
                    throw new ArgumentException("Invalid report type");
            }

            return new Tuple<string, DateTime, DateTime>(timeText, startDate, endDate);
        }

        // Các hàm tính doanh thu, sản phẩm bán chạy, v.v. (Dữ liệu mẫu)
        private decimal CalculateTotalRevenue(DateTime startDate, DateTime endDate)
        {
            // Tính tổng doanh thu từ cơ sở dữ liệu hoặc dữ liệu mẫu
            return 12345678;
        }

        private List<ProductStat> GetBestSellers(DateTime startDate, DateTime endDate)
        {
            return new List<ProductStat>
            {
                new ProductStat { Name = "Trà sữa truyền thống", Quantity = 300 },
                new ProductStat { Name = "Cà phê sữa đá", Quantity = 250 },
            };
        }

        private List<ProductStat> GetWorstSellers(DateTime startDate, DateTime endDate)
        {
            return new List<ProductStat>
            {
                new ProductStat { Name = "Matcha đá xay", Quantity = 5 },
                new ProductStat { Name = "Cacao nóng", Quantity = 8 },
            };
        }

        private List<CategoryRevenue> GetRevenueByCategory(DateTime startDate, DateTime endDate)
        {
            return new List<CategoryRevenue>
            {
                new CategoryRevenue { Category = "Cafe", Revenue = 4500000 },
                new CategoryRevenue { Category = "Trà sữa", Revenue = 6000000 },
            };
        }

        private List<ProductRevenue> GetRevenueByProduct(DateTime startDate, DateTime endDate)
        {
            return new List<ProductRevenue>
            {
                new ProductRevenue { Name = "Bạc xỉu", Revenue = 2000000 },
                new ProductRevenue { Name = "Hồng trà sữa", Revenue = 1800000 },
            };
        }

        private List<TopCustomer> GetTopCustomers(DateTime startDate, DateTime endDate)
        {
            return new List<TopCustomer>
            {
                new TopCustomer { CustomerName = "Nguyễn Văn A", OrderCount = 10, TotalSpent = 1500000 },
                new TopCustomer { CustomerName = "Trần Thị B", OrderCount = 7, TotalSpent = 1200000 },
            };
        }

        private TimeRevenueStat GetPeakTime(DateTime startDate, DateTime endDate)
        {
            return new TimeRevenueStat { TimeRange = "08:00 - 10:00", Revenue = 5000000 };
        }

        private TimeRevenueStat GetOffTime(DateTime startDate, DateTime endDate)
        {
            return new TimeRevenueStat { TimeRange = "14:00 - 16:00", Revenue = 500000 };
        }
    }
}
