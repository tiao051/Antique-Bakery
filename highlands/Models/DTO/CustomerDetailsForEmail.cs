namespace highlands.Models.DTO
{
    public class CustomerDetailsForEmail
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = null!;
        public string? Email { get; set; }
        public string? Type { get; set; }
    }
}
