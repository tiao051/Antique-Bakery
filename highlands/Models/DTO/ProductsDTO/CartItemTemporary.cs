namespace highlands.Models
{
    public class CartItemTemporary
    {
        public string ItemName { get; set; } = null!;
        public string Size { get; set; } = null!;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string ItemImg { get; set; }
    }
}
