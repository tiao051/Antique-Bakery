using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace highlands.Controllers.User
{
    public class UserController : Controller
    {
        private readonly string _connectionString = "Server=DESKTOP-IN72EQB;Database=coffee_shop;Trusted_Connection=True;Encrypt=False;";

        public IActionResult Index()
        {
            return View();
        }
    }
}
