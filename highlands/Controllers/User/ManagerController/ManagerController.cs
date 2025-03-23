using Microsoft.AspNetCore.Mvc;

namespace highlands.Controllers.User.Manager
{
    public class ManagerController : Controller
    {
        public IActionResult Index()
        {
            return View("~/Views/User/Manager/Index.cshtml");
        }
    }
}
