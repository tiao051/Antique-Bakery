document.addEventListener("click", async function (event) {
    if (event.target.classList.contains("fa-trash-alt")) {
        await removeCartItem(event);
    }
    if (event.target.classList.contains("fa-plus-circle")) {
        await increaseCartItem(event);
    }
});

document.addEventListener("DOMContentLoaded", function () {
    initializeOptionButtons();
    loadDeliveryMethod();
    loadItemRcm();
    updateTotal();
    getRealtimeOrders();
});

document.getElementById("subscribeEmails").addEventListener("change", function () {
    console.log("[DEBUG] Checkbox checked:", this.checked);
});

async function getUserId() {
    try {
        let response = await fetch("/Customer/GetUserId");
        let data = await response.json();
        if (!data.success) throw new Error("Không thể lấy userId! Vui lòng đăng nhập lại.");
        return data.userId;
    } catch (error) {
        alert(error.message);
        return null;
    }
}

async function removeCartItem(event) {
    let quantityElement = event.target.closest(".item-detail").querySelector(".item-quantity");
    let itemName = event.target.getAttribute("data-itemname");
    let itemSize = event.target.getAttribute("data-size");
    let userId = await getUserId();
    if (!userId) return;

    let url = `/Customer/RemoveCartItem?userId=${userId}&itemName=${encodeURIComponent(itemName)}&itemSize=${encodeURIComponent(itemSize)}`;
    console.log("Fetch URL:", url);

    try {
        let response = await fetch(url, { method: "DELETE" });
        let data = await response.json();
        console.log("[DEBUG]: 123");
        if (data.success) {
            console.log("[DEBUG]: 456");
            updateCartItemUI(quantityElement, data.updatedQuantity, event);
        } else {
            alert("Lỗi: " + data.message);
        }
    } catch (error) {
        console.error("Lỗi:", error);
    }
}

async function increaseCartItem(event) {
    let itemName = event.target.getAttribute("data-itemname");
    let itemSize = event.target.getAttribute("data-size");
    let quantityElement = event.target.closest(".item-detail").querySelector(".item-quantity");
    let userId = await getUserId();
    if (!userId) return;

    let url = `/Customer/IncreaseCartItem?userId=${userId}&itemName=${encodeURIComponent(itemName)}&itemSize=${encodeURIComponent(itemSize)}`;
    console.log("Fetch URL:", url);

    try {
        let response = await fetch(url, { method: "PUT" });
        let data = await response.json();
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

function updateCartItemUI(quantityElement, updatedQuantity, event) {
    console.log("[DEBUG]: Tới đây rồi");
    if (updatedQuantity > 0) {
        quantityElement.textContent = updatedQuantity;
    } else {
        let itemToRemove = event.target.closest("li");
        if (itemToRemove) {
            itemToRemove.remove();
        }
    }
    console.log("[DEBUG]: Gọi updatetotal");
    updateTotal();
}
function updateTotal() {
    let subtotal = 0;
    let totalQuantity = 0;
    let cartItems = document.querySelectorAll(".cart-items li");

    console.log("[DEBUG] Cart items count:", cartItems.length);

    if (cartItems.length === 0) {
        showEmptyCartView();
        return;
    }

    cartItems.forEach(item => {
        let price = parseFloat(item.querySelector(".item-price-number")?.textContent.replace(/[^0-9.]/g, "") || "0");
        let quantity = parseInt(item.querySelector(".item-quantity")?.textContent.trim() || "0");
        if (!isNaN(price) && !isNaN(quantity)) {
            subtotal += price * quantity;
            totalQuantity += quantity;
        }
    });

    let tax = subtotal * 0.05;
    let total = subtotal + tax;

    document.querySelector(".subtotal-number").textContent = `$${subtotal.toFixed(2)}`;
    document.querySelector(".tax-number").textContent = `$${tax.toFixed(2)}`;
    document.querySelector(".total-number").textContent = `$${total.toFixed(2)}`;
    document.querySelector("h1").textContent = `Guest checkout (${totalQuantity})`;

    sessionStorage.setItem("subtotal", subtotal.toFixed(2));
    sessionStorage.setItem("tax", tax.toFixed(2));
    sessionStorage.setItem("total", total.toFixed(2));
    sessionStorage.setItem("totalQuantity", totalQuantity);
}

function showEmptyCartView() {
    console.log("[DEBUG] showEmptyCartView() was called"); 

    const emptyCartHTML =
        `<div class="content-center">
            <span class="coffee-icon">☕</span>
            <h2>Start your next order</h2> 
            <p>As you add menu items, they'll appear here. You'll have a chance to review before placing your order.</p>
                <button type="button" class="add-items-button" id="addItemsBtn">
                    Add items
                </button>
        </div>`;

    const rightPanel = document.querySelector(".right-panel");

    if (rightPanel) {
        rightPanel.innerHTML = emptyCartHTML;
        document.getElementById("addItemsBtn").addEventListener("click", function () {
            window.location.href = "/Customer/Index"; 
        });
    } else {
        console.log("[ERROR] .right-panel not found");
    }
}

function sendCartDataToServerAndRedirect() {
    let subtotal = sessionStorage.getItem("subtotal") || "0.00";
    let tax = sessionStorage.getItem("tax") || "0.00";
    let total = sessionStorage.getItem("total") || "0.00";
    let totalQuantity = sessionStorage.getItem("totalQuantity") || "0";
    let subscribeEmails = document.getElementById("subscribeEmails")?.checked || false;
    let deliveryMethod = sessionStorage.getItem("deliveryMethod") || null;

    if (!deliveryMethod) {
        alert("Please select a delivery method before proceeding.");
        return;
    }
    
    fetch("/Customer/SaveCartData", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ subtotal, tax, total, totalQuantity, subscribeEmails, deliveryMethod})
    })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                window.location.href = "/Customer/Checkout";
            }
        })
        .catch(error => console.error("[ERROR]", error));
}

function setDeliveryMethod(method, button) {
    sessionStorage.setItem("deliveryMethod", method);
    document.querySelectorAll(".option-button").forEach(btn => btn.classList.remove("active"));
    button.classList.add("active");

    console.log(`[DEBUG] Selected delivery method: ${method}`);
}

function loadDeliveryMethod() {
    let savedMethod = sessionStorage.getItem("deliveryMethod");
    if (savedMethod) {
        let buttons = document.querySelectorAll(".option-button");
        buttons.forEach(btn => {
            if (btn.textContent.trim() === savedMethod) {
                btn.classList.add("active");
            }
        });
    }
}
function initializeOptionButtons() {
    document.querySelectorAll(".option-button").forEach(div => {
        div.addEventListener("click", function () {
            document.querySelectorAll(".option-button").forEach(d => d.classList.remove("active"));
            this.classList.add("active");
        });
    });
}

async function loadItemRcm() {
    try {
        const response = await fetch("https://localhost:44351/Customer/GetSuggestedProducts", {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json',
            },
        });

        if (response.ok) {
            const data = await response.json();
            console.log('Raw Data:', data);
            const suggestedProducts = data.suggested_products || [];

            const recProductsContainer = document.querySelector('.rec-products');
            recProductsContainer.innerHTML = '';

            suggestedProducts.forEach((product, index) => {
                const productDiv = document.createElement('div');
                productDiv.classList.add('rec-product');

                const productImgDiv = document.createElement('div');
                productImgDiv.classList.add('rec-product-img');

                const productImg = document.createElement('img');
                productImg.src = product.img || "/img/placeholder.jpg";  // ✅ Dùng ảnh thật
                productImg.alt = product.name || "Product";
                productImgDiv.appendChild(productImg);

                const productNameDiv = document.createElement('div');
                productNameDiv.classList.add('rec-product-name');
                productNameDiv.textContent = product.name || "";

                const addProductIcon = document.createElement('i');
                addProductIcon.classList.add('fas', 'fa-plus-circle');
                addProductIcon.setAttribute('data-itemname', product.name);

                if (index < 2) {
                    const promoBadgeDiv = document.createElement('div');
                    promoBadgeDiv.classList.add('promo-badge');
                    promoBadgeDiv.textContent = '10% off';
                    productDiv.appendChild(promoBadgeDiv);
                }

                productDiv.appendChild(productImgDiv);
                productDiv.appendChild(productNameDiv);
                productDiv.appendChild(addProductIcon);

                recProductsContainer.appendChild(productDiv);
            });
        } else {
            console.log('Error fetching suggested products:', response.status, await response.text());
        }
    } catch (error) {
        console.error('Request failed', error);
    }
}
function getRealtimeOrders() {

    console.log("signalR defined:", typeof signalR !== "undefined");
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/recommendationHub")
        .build();

    connection.on("ReceiveNewRecommention", function () {
        console.log("New item rec received!");
        loadItemRcm();
    });

    connection.start().then(function () {
        console.log("Connection to Hub established");
    }).catch(function (err) {
        console.error("SignalR connection failed: " + err.toString());
    });
}





