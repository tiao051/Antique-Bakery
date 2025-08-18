namespace highlands.Models.DTO.ProductsDTO
{
    public class RecipeWithIngredientDetail
    {
        public string IngredientName { get; set; }
        public int IngredientID { get; set; }
        public string IngredientCategory { get; set; }
        public string IngredientType { get; set; }
        public int Quantity { get; set; }
        public string Size { get; set; }
        public int InRecipe { get; set; }
        public string Unit { get; set; }
    }
}
