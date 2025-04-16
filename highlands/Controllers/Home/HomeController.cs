using System.Diagnostics;
using highlands.Models;
using highlands.Models.DTO;
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

        public IActionResult Empty(int orderId, decimal totalAmount, DateTime orderDate)
        {
            var model = new OrderLoadingDTO
            {
                OrderId = orderId,
                TotalAmount = totalAmount,
                OrderDate = orderDate
            };

            return View("~/Views/Shared/EmptyView.cshtml", model);
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}
