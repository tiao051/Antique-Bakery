using System;
using System.Collections.Generic;

namespace highlands.Models;

public partial class Supplier
{
    public int SupplierId { get; set; }

    public string? SupplierName { get; set; }

    public string? Contact { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public virtual ICollection<StockIn> StockIns { get; set; } = new List<StockIn>();
}
