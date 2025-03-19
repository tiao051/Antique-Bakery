using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace highlands.Controllers.User
{
    [Authorize(Policy = "Admin")]
    public class AdminController : Controller
    {   
        public IActionResult Index()
        {
            return View("~/Views/User/Admin/Index.cshtml");
        }
    }
}
