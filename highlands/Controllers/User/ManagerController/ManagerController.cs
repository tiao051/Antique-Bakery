using highlands.Interfaces;
using highlands.Models;
using highlands.Repository.MenuItemRepository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Newtonsoft.Json;

namespace highlands.Controllers.User.Manager
{
    [Authorize(Policy = "Manager")]
    public class ManagerController : BaseController
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
            var userId = GetCurrentUserId();
            
            if (!userId.HasValue)
            {
                Console.WriteLine("User not authenticated or invalid UserId claim");
                return RedirectToAction("Login", "Account");
            }

            Console.WriteLine($"Authenticated UserId from JWT: {userId.Value}");
            Console.WriteLine($"Creating product: Name={itemName}, Category={category}, Subcategory={subcategory}, Type={type}, Image={itemimg}");

            var item = new MenuItem
            {
                ItemName = itemName,
                Category = category,
                SubCategory = subcategory,
                ItemImg = itemimg,
                SubcategoryImg = null,
                Type = type,
            };

            Console.WriteLine($"MenuItem object created: {JsonConvert.SerializeObject(item)}");

            var result = await _efRepo.CreateItemAsync(item);
            Console.WriteLine($"CreateItemAsync result: {result}");

            if (result)
            {
                TempData["SuccessMessage"] = "Product created successfully!";
                Console.WriteLine("Redirecting to ProductList");
                return RedirectToAction("ProductList");
            }
            else
            {
                TempData["ErrorMessage"] = "Product creation failed!";
                Console.WriteLine("Product creation failed, redirecting to CreateProduct");
                return RedirectToAction("CreateProduct");
            }
        }
        [HttpPost]
        public async Task<IActionResult> Delete(string itemName)
        {
            var userId = GetCurrentUserId();
            
            if (!userId.HasValue)
            {
                Console.WriteLine("User not authenticated or invalid UserId claim");
                return Json(new { success = false, message = "User not authenticated" });
            }

            Console.WriteLine($"Authenticated UserId from JWT: {userId.Value}");

            var result = await _efRepo.DeleteItemAsync(itemName);

            if (result)
            {
                return Json(new { success = true, message = "Product deleted successfully!" });
            }
            else
            {
                return Json(new { success = false, message = "Product deletion failed!" });
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
