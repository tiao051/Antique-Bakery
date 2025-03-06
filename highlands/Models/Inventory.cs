using System;
using System.Collections.Generic;

namespace highlands.Models;

public partial class Inventory
{
    public int InventoryId { get; set; }

    public string? ItemName { get; set; }

    public int? Quantity { get; set; }

    public decimal? Price { get; set; }

    public virtual ICollection<ShiftInventory> ShiftInventories { get; set; } = new List<ShiftInventory>();

    public virtual ICollection<StockIn> StockIns { get; set; } = new List<StockIn>();

    public virtual ICollection<StockOut> StockOuts { get; set; } = new List<StockOut>();
}
