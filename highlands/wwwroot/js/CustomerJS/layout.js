
// Toggling user menu
function handleUserMenuToggle() {
    const menu = document.querySelector(".dropdown-menu");
    menu.classList.toggle("show");
}

// Auto-hide menu when clicking outside
function registerOutsideClickHandler() {
    document.addEventListener("click", function (e) {
        const menu = document.querySelector(".dropdown-menu");
        const button = document.querySelector(".user-btn");

        if (!menu.contains(e.target) && !button.contains(e.target)) {
            menu.classList.remove("show");
        }
    });
}

// Cập nhật số lượng giỏ hàng
function updateCartQuantity() {
    $.ajax({
        url: "/Customer/GetCartQuantity",
        type: "GET",
        success: function (response) {
            console.log("[DEBUG] Cart Quantity Response:", response);
            if (response.success) {
                $(".cart-count").text(response.quantity);
            }
        },
        error: function () {
            console.error("[ERROR] Failed to update cart quantity");
        }
    });
}

// Tìm kiếm menu
function searchMenu() {
    const keyword = $("#searchInput").val().trim();

    if (keyword === "") {
        alert("Please enter a keyword.");
        return;
    }

    $.ajax({
        url: '/Customer/SearchMenuItems',
        type: 'GET',
        data: { keyword: keyword },
        success: function (result) {
            $("#menu-items").html(result);
        },
        error: function (xhr, status, error) {
            console.error("Search AJAX Error:", status, error);
            console.log("Response Text:", xhr.responseText);
            alert("Không thể tải dữ liệu, vui lòng thử lại.");
        }
    });
}

// Lắng nghe enter trong input
function registerSearchInputListener() {
    $(document).on('keypress', '#searchInput', function (e) {
        if (e.which === 13) {
            searchMenu();
        }
    });
}

function registerCartUpdatedEvent() {
    $(document).on("cartUpdated", function () {
        updateCartQuantity();
    });
}

$(document).ready(function () {
    registerOutsideClickHandler();
    registerSearchInputListener();
    registerCartUpdatedEvent();
    updateCartQuantity(); 

    $(".user-btn").on("click", handleUserMenuToggle);
});
