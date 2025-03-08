using highlands.Models;
using highlands.Models.DTO;

namespace highlands.Services
{
    public interface IMenuItemRepository
    {
        Task<List<MenuItem>> GetAllMenuItemsAsync();
        Task<List<SubcategoryDTO>> GetSubcategoriesAsync();
        Task<List<MenuItem>> GetMenuItemsBySubcategoryAsync(string subcategory);
        Task<bool> CreateCustomerAsync(Customer customer);
        //Task<List<RecipeWithIngredientDetail>> GetIngredientsBySizeAsync(string itemName, string size);
        Task<(MenuItem?, List<MenuItemPrice>, List<RecipeWithIngredientDetail>)>
            GetItemDetailsAsync(string subcategory, string itemName, string size);
        Task<decimal?> GetPriceAsync(string itemName, string size);
        Task<List<CartItemTemporary>> GetCartItemsAsync(int userId);
        Task<int> GetTotalQuantityAsync(int userId);
        Task<Dictionary<string, int>> GetSizeQuantitiesAsync(int userId);
        Task<bool> RemoveCartItemAsync(int userId, string itemName, string itemSize);
        Task<bool> AddToCartAsync(int userId, string itemName, string size, decimal price, int quantity, string itemImg);
        Task<bool> IncreaseCartItem(int userId, string itemName, string itemSize);
        Task<CustomerDetailsForEmail?> GetCustomerDetailsAsync(int userId);
        //Task<CustomerInfoDTO?> GetCustomerInfoAsync(int userId);
        Task<List<RecipeWithIngredientDetail>> GetIngredientsBySizeAsync(string itemName, string size);
    }
}
