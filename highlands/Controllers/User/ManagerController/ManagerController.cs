using highlands.Interfaces;
using highlands.Models;
using highlands.Repository.MenuItemRepository;
using Microsoft.AspNetCore.Mvc;

namespace highlands.Controllers.User.Manager
{
    public class ManagerController : Controller
    {
        private readonly IMenuItemRepository _efRepo;
        public ManagerController(IEnumerable<IMenuItemRepository> repositories)
        {
            _efRepo = repositories.OfType<MenuItemDapperRepository>().FirstOrDefault();
        }

        public IActionResult Index()
        {
            return View("~/Views/User/Manager/Index.cshtml");
        }
        [HttpPost]
        public async Task<IActionResult> Create(string itemName, string category, string subcategory, string itemimg, string type)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

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
                TempData["SuccessMessage"] = "Data inserted successfully!";
            }
            else
            {
                TempData["ErrorMessage"] = "Data insertion failed!";
            }

            return RedirectToAction("Index");
        }
        [HttpDelete]
        public async Task<IActionResult> Delete(string itemName)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var result = await _efRepo.DeleteItemAsync(itemName);

            if (result)
            {
                TempData["SuccessMessage"] = "Data delete successfully!";
            }
            else
            {
                TempData["ErrorMessage"] = "Data delete failed!";
            }

            return RedirectToAction("Index");
        }
    }
}
