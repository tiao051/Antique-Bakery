using highlands.Models;
using highlands.Models.DTO;
using highlands.Models.DTO.CustomerDataDTO;
using highlands.Models.DTO.OrderDTO;
using highlands.Models.DTO.ProductsDTO;

namespace highlands.Interfaces
{
    public interface IMenuItemRepository
    {
        Task<List<MenuItem>> GetAllMenuItemsAsync();
        Task<List<SubcategoryDTO>> GetSubcategoriesAsync();
        Task<List<MenuItem>> GetMenuItemsBySubcategoryAsync(string subcategory);
        Task<bool> CreateCustomerAsync(Customer customer);
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
        Task<List<RecipeWithIngredientDetail>> GetIngredientsBySizeAsync(string itemName, string size);      
        void BeginTransaction();
        void CommitTransaction();
        void RollbackTransaction();
        Task<int> InsertOrderAsync(Order order);
        Task InsertOrderDetailAsync(OrderDetail detail);
        Task<CustomerCheckoutInfoDTO> GetCustomerPhoneAddrPoints(string customerId);
        Task<List<string>> GetSuggestedProductsDapper(List<string> productNames);
        Task<List<(string Name, string Img, string Subcategory)>> GetSuggestedProductWithImg(List<string> productNames);
        (List<MenuItem> items, int totalPages) Search(string keyword, int page = 1, int pageSize = 6);
        Task<string> GetCustomerIdFromUserId(string userId);
        Task<List<ProductSuggestionDTO>> GetSugestedProductByUser(string customerId);
        Task<List<ProductSuggestionDTO>> GetSuggestedProductByTime(string timeSlot);
        Task<List<OrderDetailDTO>> GetCommonProductPairsAsync(int orderId);
        Task<bool> CreateItemAsync(MenuItem menuItem);
        Task<bool> DeleteItemAsync(string itemName);
    }
}
