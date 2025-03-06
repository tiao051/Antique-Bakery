using System;
using System.Collections.Generic;

namespace highlands.Models;

public partial class TransactionHistory
{
    public int TransactionId { get; set; }

    public int? OrderId { get; set; }

    public DateTime? TransactionDate { get; set; }

    public decimal? Amount { get; set; }

    public string? PaymentMethod { get; set; }

    public virtual Order? Order { get; set; }
}
