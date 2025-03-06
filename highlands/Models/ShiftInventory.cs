using System;
using System.Collections.Generic;

namespace highlands.Models;

public partial class ShiftInventory
{
    public int ShiftInventoryId { get; set; }

    public int? ShiftId { get; set; }

    public int? InventoryId { get; set; }

    public int? QuantityAvailable { get; set; }

    public virtual Inventory? Inventory { get; set; }

    public virtual ICollection<OrderInventory> OrderInventories { get; set; } = new List<OrderInventory>();

    public virtual Shift? Shift { get; set; }
}
