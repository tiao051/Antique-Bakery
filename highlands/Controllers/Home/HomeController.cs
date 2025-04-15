using System.Diagnostics;
using highlands.Models;
using Microsoft.AspNetCore.Mvc;

namespace highlands.Controllers.Home
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }
        
        public IActionResult Empty()
        {
            return View("~/Views/Shared/EmptyView.cshtml");
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}
