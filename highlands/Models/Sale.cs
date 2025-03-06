using System;
using System.Collections.Generic;

namespace highlands.Models;

public partial class Sale
{
    public int SalesId { get; set; }

    public int? StoreId { get; set; }

    public decimal? TotalAmount { get; set; }

    public DateTime? SalesDate { get; set; }

    public int? PromotionId { get; set; }

    public virtual Promotion? Promotion { get; set; }

    public virtual Store? Store { get; set; }
}
