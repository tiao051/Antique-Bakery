using System;
using System.Collections.Generic;

namespace highlands.Models;

public partial class OrderDetail
{
    public int OrderDetailId { get; set; }

    public int? OrderId { get; set; }

    public int? Quantity { get; set; }

    public decimal? Price { get; set; }

    public string? ItemName { get; set; }

    public string? Size { get; set; }

    public virtual MenuItem? ItemNameNavigation { get; set; }

    public virtual Order? Order { get; set; }
}
