namespace highlands.Models.DTO.OrderDTO
{
    public class OrderHistoryDTO
    {
        public int OrderId { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal TotalAmount { get; set; }

        public List<OrderItemDTO> Items { get; set; } = new();
    }
}
