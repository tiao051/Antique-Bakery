using highlands.Repository.OrderRepository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace highlands.Controllers.User.Admin
{
    [Authorize(Policy = "Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class AdminApiController : ControllerBase
    {
        private readonly OrderRepository _orderRepository;
        public AdminApiController(OrderRepository orderRepository)
        {
            _orderRepository = orderRepository;
        }
        [HttpGet("getOrder")]
        public async Task<IActionResult> GetOrder()
        {
            var orders = await _orderRepository.GetOrderAsync();
            return Ok(orders);
        }
    }
}
