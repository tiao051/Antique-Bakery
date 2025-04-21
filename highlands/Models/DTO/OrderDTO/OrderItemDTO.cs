namespace highlands.Models.DTO.OrderDTO
{
    public class OrderItemDTO
    {
        public string ItemName { get; set; }
        public string ItemImg { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public string Size { get; set; }
    }
}
