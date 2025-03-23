document.addEventListener("click", async function (event) {
    if (event.target.classList.contains("fa-trash-alt")) {
        let quantityElement = event.target.closest(".item-detail").querySelector(".item-quantity");
        let itemName = event.target.getAttribute("data-itemname");
        let itemSize = event.target.getAttribute("data-size");
        try {
            let userResponse = await fetch("/Customer/GetUserId");
            let userData = await userResponse.json();

            if (!userData.success) {
                alert("Không thể lấy userId! Vui lòng đăng nhập lại.");
                return;
            }

            let userId = userData.userId;

            // Fix the URL template syntax and add itemSize
            let url = `/Customer/RemoveCartItem?userId=${userId}&itemName=${encodeURIComponent(itemName)}&itemSize=${encodeURIComponent(itemSize)}`;
            console.log("Fetch URL:", url);

            let response = await fetch(url, {
                method: "DELETE"
            });

            let data = await response.json();
            if (data.success) {
                // Check if we have an updated quantity
                if (data.updatedQuantity !== undefined) {
                    if (data.updatedQuantity > 0) {
                        // Update the quantity display
                        quantityElement.textContent = data.updatedQuantity;
                    }
                    else {
                        // Remove the item from the DOM if quantity is 0
                        let itemToRemove = event.target.closest("li");
                        if (itemToRemove) {
                            itemToRemove.remove();
                        }
                    }
                    updateTotal();
                }
                else {
                    // If backend doesn't return a quantity, refresh the page
                    window.location.reload();
                }
            }
            else {
                alert("Lỗi: " + data.message);
            }
        }
        catch (error) {
            console.error("Lỗi:", error);
        }
    }
    if (event.target.classList.contains("fa-plus-circle")) {
        let itemName = event.target.getAttribute("data-itemname");
        let itemSize = event.target.getAttribute("data-size");
        let quantityElement = event.target.closest(".item-detail").querySelector(".item-quantity");
        if (!itemName || !quantityElement) {
            console.error("Không thể lấy itemName hoặc item-quantity!");
            return;
        }
        try {
            // Lấy userId từ backend (Redis)
            let userResponse = await fetch("/Customer/GetUserId");
            let userData = await userResponse.json();

            if (!userData.success) {
                alert("Không thể lấy userId! Vui lòng đăng nhập lại.");
                return;
            }

            let userId = userData.userId; // Lấy userId từ Redis

            // Gửi request tăng số lượng sản phẩm
            let url = `/Customer/IncreaseCartItem?userId=${userId}&itemName=${encodeURIComponent(itemName)}&itemSize=${encodeURIComponent(itemSize)}`;
            console.log("Fetch URL:", url);

            let response = await fetch(url, {
                method: "PUT"
            });

            let data = await response.json();
            console.log("Full Response:", data);
            if (data.success) {
                quantityElement.textContent = data.updatedQuantity;
                updateTotal();

            } else {
                alert("Lỗi: " + data.message);
            }
        } catch (error) {
            console.error("Lỗi:", error);
        }
    }
});
function updateTotal() {
    let subtotal = 0;
    let totalQuantity = 0;
    let cartItems = document.querySelectorAll(".cart-items li");
    // Check if there are any items left
    if (cartItems.length === 0) {
        let headerElement = document.querySelector("h1");
        if (headerElement) {
            headerElement.textContent = "Guest checkout (0)";
        }
        // No items left - show empty cart view
        showEmptyCartView();
        return;
    }

    cartItems.forEach(item => {
        let priceText = item.querySelector(".item-price-number")?.textContent || "$0";
        let quantityText = item.querySelector(".item-quantity")?.textContent || "0";

        let price = parseFloat(priceText.replace(/[^0-9.]/g, ""));
        let quantity = parseInt(quantityText.trim());

        if (!isNaN(price) && !isNaN(quantity)) {
            subtotal += price * quantity;
            totalQuantity += quantity;
        }
    });
    let tax = subtotal * 0.05;
    let total = subtotal + tax;
    // Update UI
    document.querySelector(".subtotal-number").textContent = `$${subtotal.toFixed(2)}`;
    document.querySelector(".tax-number").textContent = `$${tax.toFixed(2)}`;
    document.querySelector(".total-number").textContent = `$${total.toFixed(2)}`;

    // Update checkout title
    let headerElement = document.querySelector("h1");
    if (headerElement) {
        headerElement.textContent = `Guest checkout (${totalQuantity})`;
    }
    // luu vao localstorage
    localStorage.setItem("subtotal", subtotal.toFixed(2));
    localStorage.setItem("tax", tax.toFixed(2));
    localStorage.setItem("total", total.toFixed(2));
    localStorage.setItem("totalQuantity", totalQuantity);
}
// Function to show empty cart view
function showEmptyCartView() {
    // Create the empty cart HTML
    const emptyCartHTML = `
    <div class="content-center">
        <svg class="coffee-icon" viewBox="0 0 24 24" fill="none" stroke="#00754a" stroke-width="2">
            <path d="M12 8v13m0-13V6a2 2 0 112 2h-2zm0 0V5.5A2.5 2.5 0 109.5 8H12zm-7 4h14M5 12a2 2 0 110-4h14a2 2 0 110 4M5 12v7a2 2 0 002 2h10a2 2 0 002-2v-7" />
        </svg>
        <h2>Start your next order</h2>
        <p>As you add menu items, they'll appear here. You'll have a chance to review before placing your order.</p>
        <button class="add-items-button">Add items</button>
    </div>
    `;
    // Get the right panel
    const rightPanel = document.querySelector(".right-panel");
    // Replace its contents
    rightPanel.innerHTML = emptyCartHTML;
}
function sendCartDataToServerAndRedirect() {
    let subtotal = localStorage.getItem("subtotal") || "0.00";
    let tax = localStorage.getItem("tax") || "0.00";
    let total = localStorage.getItem("total") || "0.00";
    let totalQuantity = localStorage.getItem("totalQuantity") || "0";
    let checkbox = document.getElementById("subscribeEmails");
    if (!checkbox) {
        console.error("[ERROR] Checkbox không tồn tại!");
        return;
    }

    let subscribeEmails = checkbox.checked;
    console.log("[DEBUG] Checkbox checked:", subscribeEmails);

    fetch("/Customer/SaveCartData", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ subtotal, tax, total, totalQuantity, subscribeEmails })
    })
        .then(response => response.json())
        .then(data => {
            console.log("[DEBUG] Server response:", data); // Check phản hồi server
            if (data.success) {
                window.location.href = "/Customer/Checkout";
            }
        })
        .catch(error => console.error("[ERROR]", error));
}
document.getElementById("subscribeEmails").addEventListener("change", function () {
    console.log("[DEBUG] Checkbox checked:", this.checked);
});

document.addEventListener('DOMContentLoaded', function () {
    const divs = document.querySelectorAll(".option-button");
    divs.forEach(div => {
        div.addEventListener("click", function () {
            divs.forEach(d => d.classList.remove("active"));
            this.classList.add("active");
        });
    });
    // const icon = document.querySelector('.coffee-icon');
    // if (icon && icon.tagName !== "SPAN") {
    //     let newIcon = document.createElement("span");
    //     newIcon.className = "coffee-icon";
    //     newIcon.textContent = "☕";
    //     icon.replaceWith(newIcon);
    // }
    // Reset the animation every 9 seconds to maintain the pattern
    // setInterval(() => {
    //     icon.style.animation = 'none';
    //     void icon.offsetWidth; // Trigger reflow
    //     icon.style.animation = `
    //         shake 2s ease-in-out 0s 1,
    //         fly-up 1.5s ease-in-out 2s forwards,
    //         reset-position 0s ease-in-out 3.5s forwards,
    //         shake 2s ease-in-out 3.5s 1,
    //         fly-up 1.5s ease-in-out 5.5s forwards,
    //         reset-position 0s ease-in-out 7s forwards,
    //         shake 2s ease-in-out 7s 1
    //     `;
    // }, 9000);
    updateTotal();
});