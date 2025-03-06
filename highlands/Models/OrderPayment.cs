using System;
using System.Collections.Generic;

namespace highlands.Models;

public partial class OrderPayment
{
    public int PaymentId { get; set; }

    public int? OrderId { get; set; }

    public string? PaymentMethod { get; set; }

    public decimal? PaymentAmount { get; set; }

    public DateTime? PaymentDate { get; set; }

    public virtual Order? Order { get; set; }
}
