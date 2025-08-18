namespace highlands.Models;

public partial class MenuItem
{
    public string ItemName { get; set; } = null!;

    public string? Category { get; set; }

    public string? SubCategory { get; set; }

    public string? ItemImg { get; set; }

    public string? SubcategoryImg { get; set; }

    public string? Type { get; set; }

    public virtual ICollection<MenuItemPrice> MenuItemPrices { get; set; } = new List<MenuItemPrice>();

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();
}
