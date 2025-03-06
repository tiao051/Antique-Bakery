using Microsoft.AspNetCore.Mvc;

namespace highlands.Controllers.User
{
    public class ManagerController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
