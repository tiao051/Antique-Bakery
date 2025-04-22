using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace highlands.Controllers.User.Admin
{
    [Authorize(Policy = "Admin")]
    public class AdminController : Controller
    {
        private readonly PopularShoppingSequence _popularRepo;
        public AdminController(PopularShoppingSequence popularRepo)
        {
            _popularRepo = popularRepo;
        }
        public IActionResult Index()
        {
            return View("~/Views/User/Admin/Index.cshtml");
        }
        public IActionResult Product()
        {
            return View("~/Views/User/Admin/Product.cshtml");
        }
    }
}
