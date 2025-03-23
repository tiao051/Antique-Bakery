using Microsoft.AspNetCore.Mvc;
using highlands.Models;
using highlands.Models.DTO;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System.Text;
using highlands.Repository;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using highlands.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace highlands.Controllers.User.CustomerController
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "Customer")]
    public class CustomerController : Controller
    {
        private readonly string _hostname;
        private readonly string _username;
        private readonly string _password;
        private readonly string _queueName;
        private readonly string _port;
        private readonly IMenuItemRepository _dapperRepository;
        private readonly IDistributedCache _distributedCache;
        private readonly string _connectionString;
        private readonly IHubContext<OrderHub> _hubContext;

        public CustomerController(
    IConfiguration configuration,
    IEnumerable<IMenuItemRepository> repositories,
    IDistributedCache distributedCache,
    IHubContext<OrderHub> hubContext)
        {
            _hostname = configuration["RabbitMQ:HostName"];
            _username = configuration["RabbitMQ:UserName"];
            _password = configuration["RabbitMQ:Password"];
            _queueName = configuration["RabbitMQ:QueueName"];
            _port = configuration["RabbitMQ:Port"];
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _dapperRepository = repositories.OfType<MenuItemDapperRepository>().FirstOrDefault();
            _distributedCache = distributedCache;
            _hubContext = hubContext;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            foreach (var claim in HttpContext.User.Claims)
            {
                Console.WriteLine($"Claim Type: {claim.Type}, Claim Value: {claim.Value}");
            }

            var userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized();
            }
            var emailClaim = HttpContext.User.FindFirst(ClaimTypes.Email);
            string userId = userIdClaim.Value; // Giữ UserId ở dạng string
            Console.WriteLine($"UserIdClaim Value: {userId}");

            // Kiểm tra Role từ JWT Token
            var roleClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role");
            Console.WriteLine($"User Role: {roleClaim?.Value ?? "null"}");

            // Kiểm tra Role trong Token
            if (roleClaim == null || roleClaim.Value != "3")
            {
                return Forbid();
            }

            // Đọc giỏ hàng từ Redis
            string cacheKey = $"cart:{userId}";
            string cachedCart = await _distributedCache.GetStringAsync(cacheKey);
            List<CartItemTemporary> cartItems = JsonConvert.DeserializeObject<List<CartItemTemporary>>(cachedCart ?? "[]");

            ViewBag.TotalQuantity = cartItems?.Sum(i => i.Quantity) ?? 0;

            var subcategories = await _dapperRepository.GetSubcategoriesAsync();
            return View("~/Views/User/Customer/Index.cshtml", subcategories);
        }

        [HttpGet]
        public async Task<IActionResult> MenuItems(string subcategory)
        {
            if (string.IsNullOrEmpty(subcategory))
            {
                return BadRequest("Subcategory is required");
            }

            var menuItems = await _dapperRepository.GetMenuItemsBySubcategoryAsync(subcategory);

            if (menuItems == null || !menuItems.Any())
            {
                return NotFound("No menu items found.");
            }

            return PartialView("~/Views/User/Customer/_MenuItemsPartial.cshtml", menuItems);
        }
        public async Task<ActionResult> ItemSelected(string subcategory, string itemName, string size, int userId)
        {
            ViewData["ShowNavbarMenu"] = false;
            Console.WriteLine($"[DEBUG] ItemSelected received: subcategory={subcategory}, itemName={itemName}, size={size}");

            string cacheKey = $"user_selection:{userId}"; // Key Redis theo userId

            // Nếu tham số bị null, thử lấy từ Redis
            if (string.IsNullOrEmpty(subcategory) || string.IsNullOrEmpty(itemName) || string.IsNullOrEmpty(size))
            {
                var cachedData = await _distributedCache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    var userSelection = JsonConvert.DeserializeObject<UserSelectionDTO>(cachedData);
                    subcategory = userSelection.Subcategory;
                    itemName = userSelection.ItemName;
                    size = userSelection.Size;
                }
            }

            // Nếu vẫn thiếu dữ liệu, báo lỗi
            if (string.IsNullOrEmpty(subcategory) || string.IsNullOrEmpty(itemName) || string.IsNullOrEmpty(size))
            {
                return BadRequest("Subcategory, ItemName, or Size cannot be null or empty.");
            }

            // Lưu vào Redis để sử dụng sau này
            var userSelectionData = new UserSelectionDTO
            {
                Subcategory = subcategory,
                ItemName = itemName,
                Size = size
            };

            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) // Giữ dữ liệu trong Redis 1 giờ
            };

            await _distributedCache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(userSelectionData), cacheOptions);

            try
            {
                // Lấy chi tiết món từ database
                var (menuItem, prices, recipeList) = await _dapperRepository.GetItemDetailsAsync(subcategory, itemName, size);

                var viewModel = new ItemSelectedViewModel
                {
                    MenuItem = menuItem,
                    AvailableSizes = prices ?? new List<MenuItemPrice>(),
                    RecipeList = recipeList
                };

                return View("~/Views/User/Customer/ItemSelected.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
                return Content($"An error occurred: {ex.Message}");
            }
        }

        public async Task<ActionResult> LoadRecipePartial(string itemName, string size)
        {
            if (string.IsNullOrEmpty(size))
            {
                return BadRequest("Size cannot be null or empty.");
            }

            try
            {
                var recipeList = await _dapperRepository.GetIngredientsBySizeAsync(itemName, size);

                return PartialView("~/Views/User/Customer/_RecipePartial.cshtml", recipeList);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred: {ex.Message}");
                return Content($"An error occurred: {ex.Message}");
            }
        }

        public async Task<IActionResult> Checkout()
        {
            ViewData["ShowNavbarMenu"] = false;
            ViewData["ShowFooter"] = false;

            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            return View("~/Views/User/Customer/Checkout.cshtml");
        }
        public IActionResult CustomerDataInsertForm()
        {
            ViewData["ShowNavbarMenu"] = true;
            ViewData["ShowFooter"] = false;
            return View("~/Views/User/Customer/CustomerDataInsertForm.cshtml");
        }
        // insert du lieu cua customer vao db
        [HttpPost]
        public async Task<IActionResult> Create(string fullname, string phone, string address, string message)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var customer = new Customer
            {
                FullName = fullname,
                Phone = phone,
                Address = address,
                Message = message,
                UserId = userId.Value
            };

            var result = await _dapperRepository.CreateCustomerAsync(customer);

            if (result)
            {
                TempData["SuccessMessage"] = "Data inserted successfully!";
            }
            else
            {
                TempData["ErrorMessage"] = "Data insertion failed!";
            }

            return RedirectToAction("CustomerDataInsertForm");

        }
        [HttpGet]
        public async Task<IActionResult> GetUserId()
        {
            // Thử lấy userId từ Session
            int? userId = HttpContext.Session.GetInt32("UserId");

            // Nếu không có, hãy thử lấy từ JWT token (HttpContext.User)
            if (userId == null || userId == 0)
            {
                var userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int parsedUserId))
                {
                    userId = parsedUserId;
                    // Lưu lại vào Session để lần sau không cần lấy lại từ token
                    HttpContext.Session.SetInt32("UserId", userId.Value);
                }
                else
                {
                    // Nếu không lấy được từ token, kiểm tra Redis (nếu cần)
                    // Chú ý: Khi userId vẫn null, không thể sử dụng nó trong refreshKey
                    return Json(new { success = false, message = "User session expired!", userId = 0 });
                }
            }

            // Nếu cần kiểm tra thêm refresh token từ Redis (nếu userId được lấy được nhưng vẫn cần refresh token)
            // var refreshKey = $"user:refresh:{userId}";
            // var refreshToken = await _distributedCache.GetStringAsync(refreshKey);
            // if (string.IsNullOrEmpty(refreshToken))
            // {
            //     return Json(new { success = false, message = "User session expired!", userId = 0 });
            // }

            return Json(new { success = true, userId });
        }


        // lấy giá về để phản hồi cho js
        [HttpGet]
        public async Task<IActionResult> GetPrice(string itemName, string size)
        {
            Console.WriteLine($"Trong controller Querying price for Item: {itemName}, Size: {size}");
            if (string.IsNullOrEmpty(itemName) || string.IsNullOrEmpty(size))
            {
                return Json(new { success = false, message = "Invalid parameters." });
            }

            var price = await _dapperRepository.GetPriceAsync(itemName, size);

            if (price.HasValue)
            {
                return Json(new { success = true, price = price.Value });
            }
            else
            {
                return Json(new { success = false, message = "Price not found in controller." });
            }
        }
        [HttpPost]
        public async Task<IActionResult> AddToOrder(int userId, string itemName, string size, decimal price, int quantity = 1, string itemImg = "")
        {
            Console.WriteLine($"[DEBUG] Received AddToOrder: userId={userId}, itemName={itemName}, size={size}, quantity={quantity}, itemImg={itemImg}");

            bool added = await _dapperRepository.AddToCartAsync(userId, itemName, size, price, quantity, itemImg);
            if (!added)
            {
                return Json(new { success = false, message = "Failed to add item!" });
            }

            int totalQuantity = await _dapperRepository.GetTotalQuantityAsync(userId);
            var sizeQuantities = await _dapperRepository.GetSizeQuantitiesAsync(userId);

            Console.WriteLine($"[DEBUG] Redis Updated - Total Quantity: {totalQuantity}");
            Console.WriteLine($"[DEBUG] Redis Updated - Size Quantity: {JsonConvert.SerializeObject(sizeQuantities)}");

            return Json(new
            {
                success = true,
                message = "Item added to cart!",
                cartCount = totalQuantity,
                sizeQuantities
            });
        }
        // lấy tổng số lượng sản phẩm được add vào giỏ hàng
        [HttpGet]
        public async Task<IActionResult> GetCartQuantity()
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (userId == 0)
            {
                return Json(new { success = false, quantity = 0 });
            }

            int totalQuantity = await _dapperRepository.GetTotalQuantityAsync(userId);
            return Json(new { success = true, quantity = totalQuantity });
        }
        public async Task<IActionResult> ReviewOrder()
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            List<CartItemTemporary> cartItems = await _dapperRepository.GetCartItemsAsync(userId);

            Console.WriteLine($"[DEBUG] Cart Items trong review order: {JsonConvert.SerializeObject(cartItems)}");

            return View("~/Views/User/Customer/ReviewOrder.cshtml", cartItems);
        }
        [HttpDelete]
        public async Task<IActionResult> RemoveCartItem([FromQuery] int userId, [FromQuery] string itemName, [FromQuery] string itemSize)
        {
            Console.WriteLine($"[DEBUG] Received request: userId={userId}, itemName={itemName}, itemSize={itemSize}");
            bool success = await _dapperRepository.RemoveCartItemAsync(userId, itemName, itemSize);
            var cart = await _dapperRepository.GetCartItemsAsync(userId);
            Console.WriteLine($"[DEBUG] Cart after delete/remove: {JsonConvert.SerializeObject(cart)}");

            // Chuẩn hóa size
            Dictionary<string, string> sizeMapping = new Dictionary<string, string>
    {
        { "Small", "S" }, { "Medium", "M" }, { "Large", "L" }
    };
            string normalizedSize = itemSize;
            if (sizeMapping.ContainsKey(itemSize))
            {
                normalizedSize = sizeMapping[itemSize];
            }

            // Tìm kiếm item dựa trên cả tên và size
            var updatedItem = cart.FirstOrDefault(i =>
                i.ItemName.Trim().ToLower() == itemName.Trim().ToLower() &&
                i.Size == normalizedSize);

            int updatedQuantity = updatedItem?.Quantity ?? 0;
            return Json(new
            {
                success,
                message = success ? "Xóa thành công!" : "Không tìm thấy sản phẩm.",
                updatedQuantity
            });
        }
        [HttpPut]
        public async Task<IActionResult> IncreaseCartItem([FromQuery] int userId, [FromQuery] string itemName, string itemSize)
        {
            Console.WriteLine($"[DEBUG] Received request: userId={userId}, itemName={itemName}, itemSize={itemSize}");
            bool success = await _dapperRepository.IncreaseCartItem(userId, itemName, itemSize);
            if (success)
            {
                var cart = await _dapperRepository.GetCartItemsAsync(userId);
                Console.WriteLine($"[DEBUG] Cart after update: {JsonConvert.SerializeObject(cart)}");

                // Chuẩn hóa size trước khi tìm kiếm
                Dictionary<string, string> sizeMapping = new Dictionary<string, string>
        {
            { "Small", "S" }, { "Medium", "M" }, { "Large", "L" }
        };
                string normalizedSize = itemSize;
                if (sizeMapping.ContainsKey(itemSize))
                {
                    normalizedSize = sizeMapping[itemSize];
                }

                // Tìm sản phẩm dựa trên cả tên và size
                var updatedItem = cart.FirstOrDefault(i =>
                    i.ItemName.Trim().ToLower() == itemName.Trim().ToLower() &&
                    i.Size == normalizedSize);

                if (updatedItem == null)
                {
                    Console.WriteLine("[ERROR] Không tìm thấy sản phẩm sau khi cập nhật!");
                    return Json(new { success = false, message = "Không tìm thấy sản phẩm sau khi cập nhật." });
                }
                int updatedQuantity = updatedItem.Quantity;
                Console.WriteLine($"số lượng sau khi tăng: {updatedQuantity}");
                return Json(new { success, message = "Tăng số lượng thành công!", updatedQuantity });
            }
            return Json(new { success, message = "Không tìm thấy sản phẩm." });
        }
        [HttpPost]
        public IActionResult SaveCartData([FromBody] CheckoutDataViewModel cartData)
        {
            Console.WriteLine($"Subtotal: {cartData.Subtotal}, Tax: {cartData.Tax}, Total: {cartData.Total}, TotalQuantity: {cartData.TotalQuantity}, SubscribeEmails: {cartData.SubscribeEmails}");

            HttpContext.Session.SetString("Subtotal", cartData.Subtotal);
            HttpContext.Session.SetString("Tax", cartData.Tax);
            HttpContext.Session.SetString("Total", cartData.Total);
            HttpContext.Session.SetString("TotalQuantity", cartData.TotalQuantity);
            HttpContext.Session.SetString("SubscribeEmails", cartData.SubscribeEmails.ToString());

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> Pay([FromBody] PaymentRequestDTO request)
        {
            bool subscribeEmails = HttpContext.Session.GetString("SubscribeEmails") == "True";

            int userId = request.UserID;
            decimal totalAmount = request.TotalAmount;
            Console.WriteLine($"[DEBUG] Received payment request: userId={userId}, totalAmount={totalAmount}");

            try
            {
                // Kiểm tra user có tồn tại không
                var userDetails = await _dapperRepository.GetCustomerDetailsAsync(userId);
                if (userDetails == null)
                {
                    return BadRequest("User not found.");
                }
                int? customerId = userDetails.CustomerId;
                // Gọi hàm riêng để tạo đơn hàng
                int orderId = await CreateOrder((int)customerId, totalAmount);
                if (orderId == -1)
                {
                    return StatusCode(500, "Failed to create order");
                }
                Console.WriteLine($"[✔] Order created successfully: orderId={orderId}");

                // Nếu user không đăng ký nhận email => Trả về luôn
                if (!subscribeEmails)
                {
                    return Ok("Thanh toán thành công.");
                }

                // 4️⃣ Kiểm tra Redis tránh gửi email trùng
                //string cacheKey = $"email_sent:{userId}";
                //var cacheValue = await _distributedCache.GetStringAsync(cacheKey);
                //if (!string.IsNullOrEmpty(cacheValue))
                //{
                //    Console.WriteLine($"Email đã gửi trước đó, bỏ qua...");
                //    return Ok("Bạn đã thực hiện thanh toán rồi.");
                //}

                // Gửi email qua RabbitMQ
                var factory = new ConnectionFactory()
                {
                    HostName = _hostname,
                    UserName = _username,
                    Password = _password,
                    Port = int.Parse(_port)
                };

                await using var connection = await factory.CreateConnectionAsync();
                await using var channel = await connection.CreateChannelAsync();

                await channel.QueueDeclareAsync(
                    queue: _queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                var paymentInfo = new
                {
                    CustomerEmail = userDetails.Email,
                    userDetails.UserName,
                    Time = DateTime.UtcNow
                };

                var message = JsonConvert.SerializeObject(paymentInfo);
                var body = Encoding.UTF8.GetBytes(message);

                var properties = new BasicProperties { Persistent = true };

                await channel.BasicPublishAsync(
                    exchange: "",
                    routingKey: _queueName,
                    mandatory: false,
                    basicProperties: properties,
                    body: body);

                Console.WriteLine($"[✔] Sent payment message: {message}");

                // Lưu vào Redis để tránh gửi lại
                //await _distributedCache.SetStringAsync(cacheKey, "sent", new DistributedCacheEntryOptions
                //{
                //    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                //});

                return Ok("Thanh toán thành công và email xác nhận đã được gửi.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[❌] Error: {ex.Message}");
                return StatusCode(500, "Lỗi khi xử lý thanh toán.");
            }
        }
        [HttpPost]
        public async Task<int> CreateOrder(int customerId, decimal totalAmount)
        {
            try
            {
                var order = new Order
                {
                    OrderDate = DateTime.Now,
                    TotalAmount = totalAmount,
                    Status = "Confirmed",
                    CustomerId = customerId
                };

                int orderId = await _dapperRepository.InsertOrderAsync(order);
                Console.WriteLine($"Created order successfully for customer = {customerId}, OrderId = {orderId}");

                var hubContext = _hubContext.Clients.All;
                await hubContext.SendAsync("ReceiveNewOrder");
                Console.WriteLine("SignalR: Đã gửi tín hiệu 'ReceiveNewOrder' đến tất cả client.");

                return orderId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                return -1;
            }
        }
    }
}
