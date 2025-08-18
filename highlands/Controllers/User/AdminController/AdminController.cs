using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace highlands.Controllers.User.Admin
{
    [Authorize(Policy = "Admin")]
    public class AdminController : BaseController
    {
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
