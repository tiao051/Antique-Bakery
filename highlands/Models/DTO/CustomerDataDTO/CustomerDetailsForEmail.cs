﻿namespace highlands.Models.DTO.CustomerDataDTO
{
    public class CustomerDetailsForEmail
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = null!;
        public string? Email { get; set; }
        public string? Type { get; set; }
        public int? CustomerId { get; set; }
    }
}
