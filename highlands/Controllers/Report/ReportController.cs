using Microsoft.AspNetCore.Mvc;

namespace highlands.Controllers.Report
{   
    public class ReportController : Controller
    {   
        public IActionResult Export()
        {
            return View();
        }
    }
}
