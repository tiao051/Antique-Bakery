using Microsoft.AspNetCore.Mvc;
using highlands.Models;
using highlands.Models.DTO;
using highlands.Models.DTO.ProductsDTO;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using highlands.Interfaces;
using Microsoft.AspNetCore.SignalR;
using highlands.Services.RabbitMQServices.ExcelServices;
using highlands.Services.RabbitMQServices.EmailServices;
using highlands.Models.DTO.CustomerDataDTO;
using highlands.Models.DTO.PaymentDTO;
using highlands.Repository.MenuItemRepository;
using highlands.Repository.OrderRepository;

namespace highlands.Controllers.User.CustomerController
{
    [Authorize(AuthenticationSchemes = "Cookies,JWT", Policy = "Customer")]
    public class CustomerController : BaseController
    {
        private readonly IMenuItemRepository _dapperRepository;
        private readonly IDistributedCache _distributedCache;
        private readonly IHubContext<OrderHub> _hubContext;
        private readonly IHubContext<RecommendationHub> _recommendationHub;
        private readonly IHttpClientFactory _clientFactory;
        private readonly IEmailService _emailService;
        private readonly ExcelServiceManager _excelServiceManager;
        private readonly OrderRepository _orderRepository;
        private readonly IMenuItemRepository _efRepo;

        public CustomerController(
          IEnumerable<IMenuItemRepository> repositories,
          IDistributedCache distributedCache,
          IHubContext<OrderHub> hubContext,
          IHubContext<RecommendationHub> recommendationHub,
          ExcelServiceManager excelServiceManager,
          IEmailService emailService,
          OrderRepository orderRepository)
        {
            _dapperRepository = repositories.OfType<MenuItemDapperRepository>().FirstOrDefault();
            _distributedCache = distributedCache;
            _hubContext = hubContext;
            _recommendationHub = recommendationHub;
            _excelServiceManager = excelServiceManager;
            _emailService = emailService;
            _orderRepository = orderRepository;
            _efRepo = repositories.OfType<MenuItemEFRepository>().FirstOrDefault();
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewBag.ShowSearchBox = true;
            foreach (var claim in HttpContext.User.Claims)
            {
                Console.WriteLine($"Claim Type: {claim.Type}, Claim Value: {claim.Value}");
            }

            var userId = GetCurrentUserId();
            var email = GetCurrentUserEmail();
            var role = GetCurrentUserRole();
            
            if (!userId.HasValue)
            {
                return Unauthorized();
            }
            
            Console.WriteLine($"UserIdClaim Value: {userId.Value}");
            Console.WriteLine($"User Email: {email ?? "null"}");
            Console.WriteLine($"User Role: {role?.ToString() ?? "null"}");

            // Kiểm tra Role trong Token
            var userRole = GetCurrentUserRole();
            if (userRole == null || userRole != 3)
            {
                return Forbid();
            }

            // Đọc giỏ hàng từ Redis
            string cacheKey = $"cart:{userId}";
            string cachedCart = await _distributedCache.GetStringAsync(cacheKey);
            List<CartItemTemporary> cartItems = JsonConvert.DeserializeObject<List<CartItemTemporary>>(cachedCart ?? "[]");

            ViewBag.TotalQuantity = cartItems?.Sum(i => i.Quantity) ?? 0;

            //var subcategories = await _dapperRepository.GetSubcategoriesAsync();
            var subcategories = await _efRepo.GetSubcategoriesAsync();
            return View("~/Views/User/Customer/Index.cshtml", subcategories);
        }
        [HttpGet]
        public async Task<IActionResult> MenuItems(string subcategory)
        {
            if (string.IsNullOrEmpty(subcategory))
            {
                return BadRequest("Subcategory is required");
            }

            //var menuItems = await _dapperRepository.GetMenuItemsBySubcategoryAsync(subcategory);
            var menuItems = await _efRepo.GetMenuItemsBySubcategoryAsync(subcategory);

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
                //var (menuItem, prices, recipeList) = await _efRepo.GetItemDetailsAsync(subcategory, itemName, size);

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
                Console.WriteLine($"[ERROR] Error in ItemSelected: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                
                // Return a proper error view instead of exposing internal error message
                ViewBag.ErrorMessage = "Unable to load item details. Please try again.";
                return View("~/Views/User/Customer/Error.cshtml");
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
                //var recipeList = await _efRepo.GetIngredientsBySizeAsync(itemName, size);

                return PartialView("~/Views/User/Customer/_RecipePartial.cshtml", recipeList);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error in LoadRecipePartial: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                
                // Return empty list to trigger the "no ingredients" message
                var emptyList = new List<RecipeWithIngredientDetail>();
                return PartialView("~/Views/User/Customer/_RecipePartial.cshtml", emptyList);
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
            Console.WriteLine($"userid khi tao user: {userId}");
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

            //var result = await _dapperRepository.CreateCustomerAsync(customer);
            var result = await _efRepo.CreateCustomerAsync(customer);

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
                var currentUserId = GetCurrentUserId();
                if (currentUserId.HasValue)
                {
                    userId = currentUserId.Value;
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

            //var price = await _dapperRepository.GetPriceAsync(itemName, size);
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

            //int totalQuantity = await _efRepo.GetTotalQuantityAsync(userId);
            //var sizeQuantities = await _efRepo.GetSizeQuantitiesAsync(userId);

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
                var currentUserId = GetCurrentUserId();

                if (currentUserId.HasValue)
                {
                    userId = currentUserId.Value;
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
                Console.WriteLine($"[DEBUG] Pay method started");
                
                // Xác định xem user có đăng ký nhận email không
                bool subscribeEmails = HttpContext.Session.GetString("SubscribeEmails") == "True";
                Console.WriteLine($"[DEBUG] Subscribe emails: {subscribeEmails}");

                // Lấy thông tin đơn hàng và các sản phẩm trong giỏ hàng
                int userId = request.UserID;
                string cartKey = $"cart:{userId}";
                decimal totalAmount = request.TotalAmount;
                Console.WriteLine($"[DEBUG] Getting cart items for userId: {userId}");
                
                List<CartItemTemporary> cartItems = await _dapperRepository.GetCartItemsAsync(userId);
                Console.WriteLine($"[DEBUG] Cart Items trong review order: {JsonConvert.SerializeObject(cartItems)}");
                Console.WriteLine($"[DEBUG] Received payment request: userId={userId}, totalAmount={totalAmount}");

                if (cartItems == null || !cartItems.Any())
                {
                    Console.WriteLine($"[ERROR] Cart is empty for userId: {userId}");
                    return BadRequest("Cart is empty");
                }

                // Tạo đơn hàng từ giỏ hàng
                List<OrderDetail> orderDetails = cartItems.Select(cartItem => new OrderDetail
                {
                    ItemName = cartItem.ItemName,
                    Size = cartItem.Size,
                    Price = cartItem.Price,
                    Quantity = cartItem.Quantity
                }).ToList();

                Console.WriteLine($"[DEBUG] Getting customer details for userId: {userId}");
                
                // Kiểm tra user và tạo đơn hàng
                //var userDetails = await _dapperRepository.GetCustomerDetailsAsync(userId);
                var userDetails = await _efRepo.GetCustomerDetailsAsync(userId);
                if (userDetails == null)
                {
                    Console.WriteLine($"[ERROR] User not found for userId: {userId}");
                    return BadRequest("User not found.");
                }

                Console.WriteLine($"[DEBUG] Customer details found: CustomerId={userDetails.CustomerId}");

                int? customerId = userDetails.CustomerId;
                
                Console.WriteLine($"[DEBUG] Creating order for customerId: {customerId}");
                int orderId = await CreateOrder((int)customerId, totalAmount, orderDetails);
                if (orderId == -1)
                {
                    Console.WriteLine($"[ERROR] Failed to create order");
                    return StatusCode(500, "Failed to create order");
                }

                Console.WriteLine($"[DEBUG] Order created successfully: orderId={orderId}");

                // Nếu user không đăng ký nhận email => Trả về luôn
                if (!subscribeEmails)
                {
                    Console.WriteLine($"[DEBUG] User not subscribed to emails, returning immediately");
                    _ = ExportProductPairsToExcelAsync(orderId);
                    await ClearUserSessionAndCart(cartKey);
                    return Ok(new
                    {
                        OrderId = orderId,
                        OrderDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                }

                Console.WriteLine($"[DEBUG] User subscribed to emails, sending email");
                
                // Gửi email qua RabbitMQ
                try 
                {
                    await _emailService.SendPaymentConfirmationEmailAsync(userDetails.Email, userDetails.UserName);
                    Console.WriteLine($"[DEBUG] Email sent successfully");
                }
                catch (Exception emailEx)
                {
                    Console.WriteLine($"[WARNING] Email failed but continuing: {emailEx.Message}");
                }

                _ = ExportProductPairsToExcelAsync(orderId);

                // Xóa giỏ hàng và session sau khi thanh toán thành công
                await ClearUserSessionAndCart(cartKey);

                Console.WriteLine($"[DEBUG] Payment process completed successfully");

                return Ok(new
                {
                    OrderId = orderId,
                    OrderDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Payment failed: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                return StatusCode(500, $"Lỗi khi xử lý thanh toán: {ex.Message}");
            }
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
                Console.WriteLine($"[DEBUG] Starting CreateOrder for customer: {customerId}");
                
                //_dapperRepository.BeginTransaction();
                _efRepo.BeginTransaction();
                var order = new Order
                {
                    OrderDate = DateTime.Now,
                    TotalAmount = totalAmount,
                    Status = "Confirmed",
                    CustomerId = customerId
                };

                //int orderId = await _dapperRepository.InsertOrderAsync(order);
                int orderId = await _efRepo.InsertOrderAsync(order);
                Console.WriteLine($"[DEBUG] Created order successfully for customer = {customerId}, OrderId = {orderId}");

                Console.WriteLine($"[DEBUG] Inserting {orderDetails.Count} order details");
                foreach (var detail in orderDetails)
                {
                    detail.OrderId = orderId;
                    // FIX: Use EF instead of Dapper to match transaction context
                    await _efRepo.InsertOrderDetailAsync(detail);
                    Console.WriteLine($"[DEBUG] Inserted order detail: {detail.ItemName}");
                }

                Console.WriteLine($"[DEBUG] Committing transaction");
                _efRepo.CommitTransaction();
                //_dapperRepository.CommitTransaction();

                Console.WriteLine($"[DEBUG] Sending SignalR notification");
                var hubContext = _hubContext.Clients.All;
                await hubContext.SendAsync("ReceiveNewOrder");
                Console.WriteLine("SignalR: Đã gửi tín hiệu 'ReceiveNewOrder' đến tất cả client.");

                Console.WriteLine($"[DEBUG] CreateOrder completed successfully, returning orderId: {orderId}");
                return orderId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] CreateOrder failed: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                //_dapperRepository.RollbackTransaction();
                _efRepo.RollbackTransaction();
                Console.WriteLine($"ERROR: {ex.Message}");
                return -1;
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetCustomerData()
        {
            var email = GetCurrentUserEmail();
            var userId = GetCurrentUserId();

            if (email == null)
            {
                return Unauthorized("Email not found in token");
            }

            if (userId == null)
            {
                return Unauthorized("UserId not found in token");
            }

            //var userDetail = await _dapperRepository.GetCustomerPhoneAddrPoints(id);
            var userDetail = await _efRepo.GetCustomerPhoneAddrPoints(userId.Value.ToString());


            return Ok(new
            {
                Email = email,
                UserId = userId.Value,
                Phone = userDetail.Phone ?? "",  
                Address = userDetail.Address ?? "", 
                LoyaltyPoints = userDetail.LoyaltyPoints ?? 0 
            });
        }
        [HttpGet]
        public IActionResult SearchMenuItems(string keyword, int page = 1, int pageSize = 6)
        {
            //var (results, totalPages) = _dapperRepository.Search(keyword, page, pageSize);
            var (results, totalPages) = _efRepo.Search(keyword, page, pageSize);

            ViewBag.TotalPages = totalPages;
            ViewBag.CurrentPage = page;

            return PartialView("~/Views/User/Customer/_SearchResultsPartial.cshtml", results);
        }
        [HttpGet]
        public async Task<IActionResult> RecommentByUser() 
        {
            try
            {
                var userId = GetCurrentUserId();

                if (!userId.HasValue)
                {
                    return Unauthorized("UserId not found in token");
                }

                //var customerId = _dapperRepository.GetCustomerIdFromUserId(userId);

                var customerId = _efRepo.GetCustomerIdFromUserId(userId.Value.ToString());
                var sugestedProduct = await _efRepo.GetSugestedProductByUser(await customerId);
                //var sugestedProduct = await _dapperRepository.GetSugestedProductByUser(await customerId);

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
        private async Task ExportProductPairsToExcelAsync(int orderId)
        {
            try
            {
                var productPairs = await _dapperRepository.GetCommonProductPairsAsync(orderId);

                if (productPairs == null || !productPairs.Any())
                {
                    Console.WriteLine("Không có cặp sản phẩm nào.");
                    return;
                }

                var filePath = await _excelServiceManager.CreateExcelFileAsync(productPairs);

                if (!string.IsNullOrEmpty(filePath))
                {
                    await _excelServiceManager.PublishFilePathAsync(filePath);
                }

                Console.WriteLine("File Excel đã được tạo và gửi đi thành công.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi xuất file Excel: {ex.Message}");
            }
        }
        public async Task<IActionResult> HistoryPurchase()
        {
            ViewData["ShowFooter"] = false;
            ViewData["ShowNavbarMenu"] = false;

            return View("~/Views/User/Customer/HistoryPurchase.cshtml");
        }
        [HttpGet]
        public async Task<IActionResult> HistoryPurchaseData()
        {
            var userId = GetCurrentUserId();

            if (!userId.HasValue)
            {
                return Unauthorized("UserId not found in token");
            }

            var customerId = await _efRepo.GetCustomerIdFromUserId(userId.Value.ToString());

            if (string.IsNullOrEmpty(customerId))
            {
                return NotFound("Customer ID not found.");
            }

            var orderHistory = await _orderRepository.GetOrderHistoryByUser(customerId);

            if (orderHistory == null || !orderHistory.Any())
            {
                return NotFound("No order history found.");
            }

            return Ok(orderHistory);
        }
    }
}
