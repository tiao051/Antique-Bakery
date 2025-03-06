using System;
using System.Collections.Generic;

namespace highlands.Models;

public partial class StockOut
{
    public int StockOutId { get; set; }

    public string? ItemName { get; set; }

    public int? Quantity { get; set; }

    public decimal? Price { get; set; }

    public DateTime? StockOutDate { get; set; }

    public int? InventoryId { get; set; }

    public int? StoreId { get; set; }

    public virtual Inventory? Inventory { get; set; }

    public virtual Store? Store { get; set; }
}
