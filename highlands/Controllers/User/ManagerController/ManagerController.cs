using highlands.Interfaces;
using highlands.Models;
using highlands.Repository.MenuItemRepository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace highlands.Controllers.User.Manager
{
    [Authorize(Policy = "Manager")]
    public class ManagerController : Controller
    {
        private readonly IMenuItemRepository _efRepo;
        public ManagerController(IEnumerable<IMenuItemRepository> repositories)
        {
            _efRepo = repositories.OfType<MenuItemEFRepository>().FirstOrDefault();
        }

        public IActionResult Index()
        {
            return View("~/Views/User/Manager/Index.cshtml");
        }        
        public IActionResult CreateProduct()
        {
            return View("~/Views/User/Manager/CreateProduct.cshtml");
        }

        public async Task<IActionResult> ProductList()
        {
            try
            {
                var menuItems = await _efRepo.GetAllMenuItemsAsync();
                return View("~/Views/User/Manager/ProductList.cshtml", menuItems);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Unable to load products. Please try again.";
                return RedirectToAction("Index");
            }
        }
        [HttpPost]
        public async Task<IActionResult> Create(string itemName, string category, string subcategory, string itemimg, string type)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                Console.WriteLine("User not authenticated or invalid UserId claim");
                return RedirectToAction("Login", "Account");
            }

            Console.WriteLine($"Authenticated UserId from JWT: {userId}");

            var item = new MenuItem
            {
                ItemName = itemName,
                Category = category,
                SubCategory = subcategory,
                ItemImg = itemimg,
                SubcategoryImg = null,
                Type = type,
            };

            var result = await _efRepo.CreateItemAsync(item);

            if (result)
            {
                TempData["SuccessMessage"] = "Product created successfully!";
                // Redirect to product list view showing the new product
                return RedirectToAction("ProductList");
            }
            else
            {
                TempData["ErrorMessage"] = "Product creation failed!";
                return RedirectToAction("CreateProduct");
            }
        }
        [HttpPost]
        public async Task<IActionResult> DeleteProduct(string itemName)
        {
            try
            {
                // Lấy UserId từ JWT claims thay vì session
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    Console.WriteLine("User not authenticated or invalid UserId claim");
                    return Json(new { success = false, message = "User not authenticated" });
                }

                Console.WriteLine($"Authenticated UserId from JWT: {userId}");

                if (string.IsNullOrEmpty(itemName))
                {
                    return Json(new { success = false, message = "Product name is required" });
                }

                var result = await _efRepo.DeleteItemAsync(itemName);

                if (result)
                {
                    return Json(new { success = true, message = "Product deleted successfully!" });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to delete product" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting product: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while deleting the product" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var categories = await _efRepo.GetCategoriesAsync();
                return Json(categories);
            }
            catch (Exception ex)
            {
                return Json(new List<string>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSubcategories(string categoryName)
        {
            try
            {
                var subcategories = await _efRepo.GetSubcategoriesByCategoryAsync(categoryName);
                return Json(subcategories);
            }
            catch (Exception ex)
            {
                return Json(new List<string>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetTypes()
        {
            try
            {
                var types = await _efRepo.GetTypesAsync();
                return Json(types);
            }
            catch (Exception ex)
            {
                return Json(new List<string>());
            }
        }
    }
}
