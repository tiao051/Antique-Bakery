using System;
using System.Collections.Generic;

namespace highlands.Models;

public partial class Recipe
{
    public int RecipeId { get; set; }

    public string ItemName { get; set; } = null!;

    public int IngredientId { get; set; }

    public decimal Quantity { get; set; }

    public string Size { get; set; } = null!;

    public virtual Ingredient Ingredient { get; set; } = null!;

    public virtual MenuItem ItemNameNavigation { get; set; } = null!;
}
