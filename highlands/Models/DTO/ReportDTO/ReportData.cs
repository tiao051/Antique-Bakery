namespace highlands.Models.DTO.ReportDTO
{
    public class ReportData
    {
        public string ReportType { get; set; }
        public string TimeRangeText { get; set; }
        public int TotalRevenue { get; set; }
        public List<ProductStat> BestSellers { get; set; }
        public List<ProductStat> WorstSellers { get; set; }
        public List<CategoryRevenue> RevenueByCategory { get; set; }
        public List<ProductRevenue> RevenueByProduct { get; set; }
        public List<TopCustomer> TopCustomers { get; set; }
        public TimeRevenueStat PeakTime { get; set; }
        public TimeRevenueStat OffTime { get; set; }
    }

    public class ProductStat
    {
        public string Name { get; set; }
        public int Quantity { get; set; }
    }

    public class CategoryRevenue
    {
        public string Category { get; set; }
        public decimal Revenue { get; set; }
    }

    public class ProductRevenue
    {
        public string Name { get; set; }
        public decimal Revenue { get; set; }
    }

    public class TopCustomer
    {
        public string CustomerName { get; set; }
        public int OrderCount { get; set; }
        public decimal TotalSpent { get; set; }
    }

    public class TimeRevenueStat
    {
        public string TimeRange { get; set; }
        public decimal Revenue { get; set; }
    }
}
