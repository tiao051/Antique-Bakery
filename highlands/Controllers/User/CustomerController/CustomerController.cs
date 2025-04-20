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
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;

namespace highlands.Controllers.User.CustomerController
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "Customer")]
    public class CustomerController : Controller
    {
        private readonly string _hostname;
        private readonly string _username;
        private readonly string _password;
        private readonly string _emailQueueName;
        private readonly string _excelQueueName;
        private readonly string _port;
        private readonly IMenuItemRepository _dapperRepository;
        private readonly IDistributedCache _distributedCache;
        private readonly string _connectionString;
        private readonly IHubContext<OrderHub> _hubContext;
        private readonly IHubContext<RecommendationHub> _recommendationHub;
        private readonly IHttpClientFactory _clientFactory;

        public CustomerController(
          IConfiguration configuration,
          IEnumerable<IMenuItemRepository> repositories,
          IDistributedCache distributedCache,
          IHubContext<OrderHub> hubContext,
          IHubContext<RecommendationHub> recommendationHub)
        {
            _hostname = configuration["RabbitMQ:HostName"];
            _username = configuration["RabbitMQ:UserName"];
            _password = configuration["RabbitMQ:Password"];
            _port = configuration["RabbitMQ:Port"];
            _emailQueueName = configuration["RabbitMQ:EmailQueue"];
            _excelQueueName = configuration["RabbitMQ:ExcelQueue"]; 
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _dapperRepository = repositories.OfType<MenuItemDapperRepository>().FirstOrDefault();
            _distributedCache = distributedCache;
            _hubContext = hubContext;
            _recommendationHub = recommendationHub;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewBag.ShowSearchBox = true;
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
            string userId = userIdClaim.Value; 
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
            ViewBag.ShowSearchBox = true;
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
                    Console.WriteLine("Luu lai vao session");
                    // Lưu lại vào Session để lần sau không cần lấy lại từ token
                    HttpContext.Session.SetInt32("UserId", userId.Value);
                    Console.WriteLine("Luu lai vao session thanh cong");
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
                var userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int parsedUserId))
                {
                    userId = parsedUserId;
                    HttpContext.Session.SetInt32("UserId", userId);
                    Console.WriteLine("Lưu lại vào session thành công");
                }
                else
                {
                    return Json(new { success = false, message = "User session expired!", userId = 0 });
                }
            }
            int totalQuantity = await _dapperRepository.GetTotalQuantityAsync(userId);
            Console.WriteLine($"Số lượng giỏ hàng hiện tại là: {totalQuantity}");

            return Json(new { success = true, quantity = totalQuantity });
        }
        public async Task<IActionResult> ReviewOrder()
        {
            try
            {
                int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
                List<CartItemTemporary> cartItems = await _dapperRepository.GetCartItemsAsync(userId);

                Console.WriteLine($"[DEBUG] Cart Items trong review order: {JsonConvert.SerializeObject(cartItems)}");

                return View("~/Views/User/Customer/ReviewOrder.cshtml", cartItems);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ReviewOrder exception: {ex.Message}");
                return StatusCode(500, "Lỗi khi xử lý.");
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetSuggestedProducts()
        {
            Console.WriteLine("goi api");

            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            List<CartItemTemporary> cartItems = await _dapperRepository.GetCartItemsAsync(userId);

            var productNames = cartItems.Select(item => item.ItemName).ToList();
            Console.WriteLine($"[DEBUG] Tên sản phẩm gửi sang Python: {JsonConvert.SerializeObject(productNames)}");

            var suggestedNames = await _dapperRepository.GetSuggestedProductsDapper(productNames);
            Console.WriteLine($"[DEBUG] Gợi ý từ Python: {JsonConvert.SerializeObject(suggestedNames)}");

            var suggestionsWithDetails = await _dapperRepository.GetSuggestedProductWithImg(suggestedNames);

            var suggestedProducts = suggestedNames
                .Select(name =>
                {
                    var matched = suggestionsWithDetails.FirstOrDefault(x => x.Name == name);
                    return new
                    {
                        name = name,
                        img = matched.Img ?? "/img/placeholder.jpg",
                        subcategory = matched.Subcategory ?? "Other"
                    };
                }).ToList();

            Console.WriteLine($"[DEBUG] Kết quả gửi FE: {JsonConvert.SerializeObject(suggestedProducts)}");

            return Ok(new { suggested_products = suggestedProducts });
        }

        [HttpDelete]
        public async Task<IActionResult> RemoveCartItem([FromQuery] int userId, [FromQuery] string itemName, [FromQuery] string itemSize)
        {
            Console.WriteLine($"[DEBUG] Received request: userId={userId}, itemName={itemName}, itemSize={itemSize}");
            bool success = await _dapperRepository.RemoveCartItemAsync(userId, itemName, itemSize);

            // Xóa cache cũ
            string cacheKey = $"cart_{userId}";
            await _distributedCache.RemoveAsync(cacheKey);

            // Lấy giỏ hàng mới từ DB
            var cart = await _dapperRepository.GetCartItemsAsync(userId);

            // Cập nhật lại cache với dữ liệu mới
            string cartJson = JsonConvert.SerializeObject(cart);
            await _distributedCache.SetStringAsync(cacheKey, cartJson, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            });

            Console.WriteLine($"[DEBUG] Cart after delete/remove: {cartJson}");

            // Chuẩn hóa size
            Dictionary<string, string> sizeMapping = new Dictionary<string, string>
            {
                { "Small", "S" }, { "Medium", "M" }, { "Large", "L" }
            };
            string normalizedSize = sizeMapping.ContainsKey(itemSize) ? sizeMapping[itemSize] : itemSize;

            // Tìm kiếm item trong giỏ hàng
            var updatedItem = cart.FirstOrDefault(i =>
                i.ItemName.Trim().ToLower() == itemName.Trim().ToLower() &&
                i.Size == normalizedSize);

            int updatedQuantity = updatedItem?.Quantity ?? 0;

            if (success && updatedQuantity == 0)
            {
                Console.WriteLine("[SignalR] Sản phẩm bị xóa hoàn toàn khỏi giỏ hàng → Gửi signal update gợi ý");

                await _recommendationHub.Clients.All.SendAsync("ReceiveNewRecommention", "update");
            }

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
                // Xóa cache cũ
                string cacheKey = $"cart_{userId}";
                await _distributedCache.RemoveAsync(cacheKey);

                // Lấy giỏ hàng mới từ DB
                var cart = await _dapperRepository.GetCartItemsAsync(userId);

                // Cập nhật lại cache
                string cartJson = JsonConvert.SerializeObject(cart);
                await _distributedCache.SetStringAsync(cacheKey, cartJson, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                });

                Console.WriteLine($"[DEBUG] Cart after update: {cartJson}");

                // Chuẩn hóa size trước khi tìm kiếm
                Dictionary<string, string> sizeMapping = new Dictionary<string, string>
                {
                    { "Small", "S" }, { "Medium", "M" }, { "Large", "L" }
                };
                string normalizedSize = sizeMapping.ContainsKey(itemSize) ? sizeMapping[itemSize] : itemSize;

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
            if (cartData == null)
            {
                Console.WriteLine("[ERROR] cartData is null");
                return BadRequest(new { success = false, message = "Invalid cart data" });
            }

            Console.WriteLine($"Subtotal: {cartData.Subtotal}, Tax: {cartData.Tax}, Total: {cartData.Total}, TotalQuantity: {cartData.TotalQuantity}, SubscribeEmails: {cartData.SubscribeEmails}, deliveryMethod: {cartData.deliveryMethod}");

            HttpContext.Session.SetString("Subtotal", cartData.Subtotal);
            HttpContext.Session.SetString("Tax", cartData.Tax);
            HttpContext.Session.SetString("Total", cartData.Total);
            HttpContext.Session.SetString("TotalQuantity", cartData.TotalQuantity);
            HttpContext.Session.SetString("SubscribeEmails", cartData.SubscribeEmails.ToString());
            HttpContext.Session.SetString("DeliveryMethod", cartData.deliveryMethod.ToString());

            return Json(new { success = true });
        }
        [HttpPost]
        public async Task<IActionResult> Pay([FromBody] PaymentRequestDTO request)
        {
            try
            {
                // Xác định xem user có đăng ký nhận email không
                bool subscribeEmails = HttpContext.Session.GetString("SubscribeEmails") == "True";

                // Lấy thông tin đơn hàng và các sản phẩm trong giỏ hàng
                int userId = request.UserID;
                string cartKey = $"cart:{userId}";
                decimal totalAmount = request.TotalAmount;
                List<CartItemTemporary> cartItems = await _dapperRepository.GetCartItemsAsync(userId);
                Console.WriteLine($"[DEBUG] Cart Items trong review order: {JsonConvert.SerializeObject(cartItems)}");
                Console.WriteLine($"[DEBUG] Received payment request: userId={userId}, totalAmount={totalAmount}");

                // Tạo đơn hàng từ giỏ hàng
                List<OrderDetail> orderDetails = cartItems.Select(cartItem => new OrderDetail
                {
                    ItemName = cartItem.ItemName,
                    Size = cartItem.Size,
                    Price = cartItem.Price,
                    Quantity = cartItem.Quantity
                }).ToList();

                // Kiểm tra user và tạo đơn hàng
                var userDetails = await _dapperRepository.GetCustomerDetailsAsync(userId);
                if (userDetails == null)
                {
                    return BadRequest("User not found.");
                }

                int? customerId = userDetails.CustomerId;
                int orderId = await CreateOrder((int)customerId, totalAmount, orderDetails);
                if (orderId == -1)
                {
                    return StatusCode(500, "Failed to create order");
                }

                Console.WriteLine($"[DEBUG] Order created successfully: orderId={orderId}");

                // Nếu user không đăng ký nhận email => Trả về luôn
                if (!subscribeEmails)
                {
                    await ClearUserSessionAndCart(cartKey);
                    return Ok(new
                    {
                        OrderId = orderId,
                        OrderDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                }

                // Gửi email qua RabbitMQ
                await SendPaymentConfirmationEmail(userDetails.Email, userDetails.UserName);

                // Lưu vào Redis để tránh gửi lại
                //await _distributedCache.SetStringAsync(cacheKey, "sent", new DistributedCacheEntryOptions
                //{
                //    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                //});

                // Xóa giỏ hàng và session sau khi thanh toán thành công
                await ClearUserSessionAndCart(cartKey);

                return Ok(new
                {
                    OrderId = orderId,
                    OrderDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return StatusCode(500, "Lỗi khi xử lý thanh toán.");
            }
        }
        private async Task SendPaymentConfirmationEmail(string customerEmail, string userName)
        {
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
                queue: _emailQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var paymentInfo = new
            {
                CustomerEmail = customerEmail,
                UserName = userName,
                Time = DateTime.UtcNow
            };

            var message = JsonConvert.SerializeObject(paymentInfo);
            var body = Encoding.UTF8.GetBytes(message);

            var properties = new BasicProperties { Persistent = true };

            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: _emailQueueName,
                mandatory: false,
                basicProperties: properties,
                body: body);

            Console.WriteLine($"Sent payment message: {message}");
        }
        private async Task ClearUserSessionAndCart(string cartKey)
        {
            // Xóa giỏ hàng và session của user
            await _distributedCache.RemoveAsync(cartKey);
            HttpContext.Response.Cookies.Delete(".AspNetCore.Session");
            Console.WriteLine($"[DEBUG] Cart and Session cleared for userId={cartKey}");
        }
        [HttpPost]
        public async Task<int> CreateOrder(int customerId, decimal totalAmount, List<OrderDetail> orderDetails)
        {
            try
            {
                _dapperRepository.BeginTransaction();

                var order = new Order
                {
                    OrderDate = DateTime.Now,
                    TotalAmount = totalAmount,
                    Status = "Confirmed",
                    CustomerId = customerId
                };

                int orderId = await _dapperRepository.InsertOrderAsync(order);
                Console.WriteLine($"Created order successfully for customer = {customerId}, OrderId = {orderId}");

                foreach (var detail in orderDetails)
                {
                    detail.OrderId = orderId;
                    await _dapperRepository.InsertOrderDetailAsync(detail);
                }

                _dapperRepository.CommitTransaction();

                var hubContext = _hubContext.Clients.All;
                await hubContext.SendAsync("ReceiveNewOrder");
                Console.WriteLine("SignalR: Đã gửi tín hiệu 'ReceiveNewOrder' đến tất cả client.");

                return orderId;
            }
            catch (Exception ex)
            {
                _dapperRepository.RollbackTransaction();
                Console.WriteLine($"ERROR: {ex.Message}");
                return -1;
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetCustomerData()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (email == null)
            {
                return Unauthorized("Email not found in token");
            }

            if (id == null)
            {
                return Unauthorized("UserId not found in token");
            }

            var userDetail = await _dapperRepository.GetCustomerPhoneAddrPoints(id);

            return Ok(new
            {
                Email = email,
                UserId = id,
                Phone = userDetail.Phone ?? "",  
                Address = userDetail.Address ?? "", 
                LoyaltyPoints = userDetail.LoyaltyPoints ?? 0 
            });
        }
        [HttpGet]
        public IActionResult SearchMenuItems(string keyword, int page = 1, int pageSize = 6)
        {
            var (results, totalPages) = _dapperRepository.Search(keyword, page, pageSize);

            ViewBag.TotalPages = totalPages;
            ViewBag.CurrentPage = page;

            return PartialView("~/Views/User/Customer/_SearchResultsPartial.cshtml", results);
        }
        [HttpGet]
        public async Task<IActionResult> RecommentByUser() 
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("UserId not found in token");
                }

                var customerId = _dapperRepository.GetCustomerIdFromUserId(userId);
                var sugestedProduct = await _dapperRepository.GetSugestedProductByUser(await customerId);

                if (sugestedProduct == null || !sugestedProduct.Any())
                {
                    Console.WriteLine("Khong co san pham tra ve");
                    return NotFound("No recommendations found.");
                }
                var result = sugestedProduct.Select(p => new {
                    p.Name,
                    p.Img,
                    p.Subcategory
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
        [HttpGet]
        public async Task<IActionResult> RecommentByTime(int hour)
        {
            var timeSlot = GetTimeSlotByHour(hour);

            var sugestedProduct = await _dapperRepository.GetSuggestedProductByTime(timeSlot);
            Console.WriteLine("Thanh cong");

            var result = sugestedProduct.Select(p => new {
                p.Name,
                p.Img,
                p.Subcategory
            }).ToList();

            return Ok(result);
        }
        public string GetTimeSlotByHour(int hour)
        {
            return hour switch
            {
                >= 5 and <= 10 => "Morning",
                >= 11 and <= 17 => "Afternoon",
                >= 18 and <= 21 => "Evening",
                _ => "Night"
            };
        }
        [HttpGet]
        public async Task<IActionResult> ExportProductPairsToExcel(int orderId)
        {
            var productPairs = await _dapperRepository.GetCommonProductPairsAsync(orderId);

            if (productPairs == null || !productPairs.Any())
            {
                Console.WriteLine("deo co cap nao het");
                return Ok(new { message = "No product pairs found, but request processed successfully." });
            }

            var filePath = await CreateExcelFile(productPairs);

            if (!string.IsNullOrEmpty(filePath))
            {
                await SendExcelFileInfoToQueue(filePath);
            }

            Console.WriteLine("Gui File thanh cong");
            return Ok(new { message = "File has been saved and sent to Python API." });
        }
        // tạo được excel 
        public async Task<string> CreateExcelFile(List<OrderDetailDTO> productPairs)
        {
            var filePath = Path.Combine(@"D:\coffe_shop\python\dataSet", "ProductPairsCSharp.xlsx");

            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("ProductPairs");

                    worksheet.Cell(1, 1).Value = "transaction_id";
                    worksheet.Cell(1, 2).Value = "product_detail";

                    int row = 2;
                    foreach (var productPair in productPairs)
                    {
                        var itemNames = productPair.ItemNames.Split(',');

                        foreach (var itemName in itemNames)
                        {
                            worksheet.Cell(row, 1).Value = productPair.OrderId;
                            worksheet.Cell(row, 2).Value = itemName.Trim();

                            row++;
                        }
                    }

                    workbook.SaveAs(filePath);
                }

                return filePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating Excel file: {ex.Message}");
                return null;
            }
        }
        private async Task SendExcelFileInfoToQueue(string filePath)
        {
            try
            {
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
                    queue: _excelQueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                var fileInfo = new
                {
                    FilePath = filePath,
                    Timestamp = DateTime.UtcNow
                };

                var message = JsonConvert.SerializeObject(fileInfo);
                var body = Encoding.UTF8.GetBytes(message);

                var properties = new BasicProperties { Persistent = true };

                await channel.BasicPublishAsync(
                    exchange: "",
                    routingKey: _excelQueueName,
                    mandatory: false,
                    basicProperties: properties,
                    body: body);

                Console.WriteLine("Sent file info to RabbitMQ:");
                Console.WriteLine($"File Path: {fileInfo.FilePath}");
                Console.WriteLine($"Timestamp: {fileInfo.Timestamp}");
                Console.WriteLine($"Message: {message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while sending file info to queue: {ex.Message}");
            }
        }
    }
}
