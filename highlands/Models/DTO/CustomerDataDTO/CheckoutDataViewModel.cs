﻿namespace highlands.Models.DTO.CustomerDataDTO
{
    public class CheckoutDataViewModel
    {
        public string Subtotal { get; set; }
        public string Tax { get; set; }
        public string Total { get; set; }
        public string TotalQuantity { get; set; }
        public bool SubscribeEmails { get; set; }
        public string deliveryMethod { get; set; }
    }
}
