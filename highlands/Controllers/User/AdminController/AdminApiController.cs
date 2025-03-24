using highlands.Repository.OrderRepository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Claims;

namespace highlands.Controllers.User.Admin
{
    [Authorize(Policy = "Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class AdminApiController : ControllerBase
    {
        private readonly IDistributedCache _distributedCache;
        private readonly OrderRepository _orderRepository;
        public AdminApiController(OrderRepository orderRepository, IDistributedCache distributedCache)
        {
            _orderRepository = orderRepository;
            _distributedCache = distributedCache;
        }
        [HttpGet("getOrder")]
        public async Task<IActionResult> GetOrder()
        {
            var orders = await _orderRepository.GetOrderAsync();
            return Ok(orders);
        }
        [HttpGet("getOrderDetail")]
        public async Task<IActionResult> GetOrderDetail()
        {
            //string orderIdStr = await _distributedCache.GetStringAsync("latest_order");
            //if (string.IsNullOrEmpty(orderIdStr)) return NotFound("Không tìm thấy đơn hàng gần đây.");

            //int orderId = int.Parse(orderIdStr);
            //Console.WriteLine($"OrderId trong GetOrderDetail: {orderId}");
            var orderDetail = await _orderRepository.GetRevenueBySubCategory();
            return Ok(orderDetail);
        }
    }
}
