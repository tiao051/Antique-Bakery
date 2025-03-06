namespace highlands.Models.DTO
{
    public class ItemSelectedViewModel
    {
        public MenuItem MenuItem { get; set; }
        public List<MenuItemPrice> AvailableSizes { get; set; }
        public List<RecipeWithIngredientDetail> RecipeList { get; set; }
    }

}
