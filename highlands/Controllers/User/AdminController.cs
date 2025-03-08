using Microsoft.AspNetCore.Mvc;

namespace highlands.Controllers.User
{
    public class AdminController : Controller
    {
        public IActionResult Index()
        {
            return View("~/Views/User/Admin/Index.cshtml");
        }
    }
}
