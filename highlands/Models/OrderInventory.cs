using System;
using System.Collections.Generic;

namespace highlands.Models;

public partial class OrderInventory
{
    public int OrderInventoryId { get; set; }

    public int? OrderId { get; set; }

    public int? ShiftInventoryId { get; set; }

    public int? QuantityUsed { get; set; }

    public virtual Order? Order { get; set; }

    public virtual ShiftInventory? ShiftInventory { get; set; }
}
