using System;
using System.Collections.Generic;

namespace highlands.Models;

public partial class Ingredient
{
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = null!;
    public string Unit { get; set; } = null!;
    public string? IngredientCategory { get; set; }
    public string? IngredientType { get; set; }
    public string? InRecipe { get; set; }

    public virtual ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();
}
