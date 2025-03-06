using System;
using System.Collections.Generic;

namespace highlands.Models;

public partial class MenuItemPrice
{
    public string ItemName { get; set; } = null!;

    public string Size { get; set; } = null!;

    public decimal Price { get; set; }

    public virtual MenuItem ItemNameNavigation { get; set; } = null!;
}
