using System;
using System.Collections.Generic;

namespace highlands.Models;

public partial class StockIn
{
    public int StockInId { get; set; }

    public int? SupplierId { get; set; }

    public int? InventoryId { get; set; }

    public int? Quantity { get; set; }

    public decimal? Price { get; set; }

    public DateTime? StockInDate { get; set; }

    public int? StoreId { get; set; }

    public virtual Inventory? Inventory { get; set; }

    public virtual Store? Store { get; set; }

    public virtual Supplier? Supplier { get; set; }
}
