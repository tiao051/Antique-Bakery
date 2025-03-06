using System;
using System.Collections.Generic;

namespace highlands.Models;

public partial class Promotion
{
    public int PromotionId { get; set; }

    public string? PromoCode { get; set; }

    public decimal? Discount { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public virtual ICollection<Sale> Sales { get; set; } = new List<Sale>();
}
