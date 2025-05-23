﻿using highlands.Repository.OrderRepository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;

namespace highlands.Controllers.User.Admin
{
    [Authorize(Policy = "Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class AdminApiController : ControllerBase
    {
        private readonly IDistributedCache _distributedCache;
        private readonly OrderRepository _orderRepository;
        private readonly PopularShoppingSequence _popularRepo;

        public AdminApiController(OrderRepository orderRepository, IDistributedCache distributedCache, PopularShoppingSequence popularRepo)
        {
            _orderRepository = orderRepository;
            _distributedCache = distributedCache;
            _popularRepo = popularRepo;
        }
        [HttpGet("getOrder")]
        public async Task<IActionResult> GetOrder()
        {
            var orders = await _orderRepository.GetOrderAsync();
            return Ok(orders);
        }
        [HttpGet("getOrderDetail/{timeFrame}")]
        public async Task<IActionResult> GetOrderDetail(string timeFrame)
        {
            if (!new[] { "day", "week", "month", "year" }.Contains(timeFrame.ToLower()))
            {
                return BadRequest("Invalid time frame. Use 'day', 'week', 'month', or 'year'.");
            }

            var data = await _orderRepository.GetRevenueBySubCategory(timeFrame.ToLower());
            return Ok(data);
        }
        [HttpGet("getMonthDetail")]
        public async Task<IActionResult> GetTotalByMonth()
        {
            var data = await _orderRepository.GetRevenueAndTotalOrdersByMonth();
            return Ok(data);
        }

        [HttpGet("productSequences")]
        public async Task<IActionResult> GeneratePopularSequences(int topN)
        {
            var result = await _popularRepo.GetPopularSequencesAsync(topN);
            return Ok(result);
        }
    }
}
