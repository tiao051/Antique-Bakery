using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace highlands.Controllers.User.Admin
{
    [Authorize(Policy = "Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class AdminApiController : ControllerBase
    {
        [HttpGet("getOrder")]
        public IActionResult GetOrder()
        {
            var orders = new List<object>
            {
                new { id = 1, email = "john@example.com", amount = 100, status = "Pending" },
                new { id = 2, email = "alice@example.com", amount = 200, status = "Completed" },
                new { id = 3, email = "bob@example.com", amount = 150, status = "Cancelled" }
            };

            return Ok(orders);
        }

    }
}
