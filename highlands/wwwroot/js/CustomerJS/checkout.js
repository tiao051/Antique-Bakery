const selectedOptions = new Set();

document.addEventListener("DOMContentLoaded", function () {
    getCustomerInfo();
});

/** ===========================
 *  XỬ LÝ THANH TOÁN
 *  ===========================
 */
async function payNow() {
    try {
        const totalPaymentElement = document.querySelector('.total-row span:last-child');
        let totalAmount = parseFloat(totalPaymentElement.textContent.replace('$', '')) || 0;

        console.log("Tổng tiền gửi lên server:", totalAmount);

        let userId = await getUserId();
        if (!userId) return;

        const requestData = { userId, totalAmount: parseFloat(totalAmount.toFixed(2)) };
        console.log("Request gửi lên server:", JSON.stringify(requestData));

        const response = await fetch("/customer/pay", {
            method: "POST",
            headers: { "Accept": "application/json", "Content-Type": "application/json" },
            body: JSON.stringify(requestData)
        });

        const result = await response.text();
        alert(response.ok ? "✅ " + result : "❌ " + result);
    } catch (error) {
        console.error("Lỗi khi gửi yêu cầu:", error);
        alert("❌ Có lỗi xảy ra! Vui lòng thử lại.");
    }
}

/** ===========================
 *  XỬ LÝ PHÍ GIAO HÀNG & TỔNG TIỀN
 *  ===========================
 */
function updateDeliveryFee() {
    let deliveryFee = 0;

    document.querySelectorAll('.shipping-option.selected').forEach(option => {
        if (option.dataset.type === 'doorstep') {
            deliveryFee += parseFloat(option.querySelector('.shipping-price').textContent.replace('$', '')) || 0;
        }
    });

    const selectedTip = document.querySelector('.tip-options .selected');
    if (selectedTip) {
        deliveryFee += parseFloat(selectedTip.getAttribute('data-amount') || selectedTip.textContent.replace('$', '')) || 0;
    }

    document.querySelector('.summary-row:nth-of-type(6) span:last-child').textContent = `$${deliveryFee.toFixed(2)}`;
    updateTotalPayment(deliveryFee);
}

function updateTotalPayment(deliveryFee) {
    const subtotal = parseFloat(document.querySelector('.summary-row:nth-of-type(4) span:last-child').textContent.replace('$', '')) || 0;
    const tax = parseFloat(document.querySelector('.summary-row:nth-of-type(5) span:last-child').textContent.replace('$', '')) || 0;
    document.querySelector('.total-row span:last-child').textContent = `$${(subtotal + tax + deliveryFee).toFixed(2)}`;
}

/** ===========================
 *  XỬ LÝ SHIPPING & TIP
 *  ===========================
 */
function selectShipping(element, type) {
    element.classList.toggle('selected');
    element.querySelector('.shipping-radio').checked = !element.querySelector('.shipping-radio').checked;
    selectedOptions.has(type) ? selectedOptions.delete(type) : selectedOptions.add(type);
    updateDeliveryFee();
}

function selectTip(element, amount) {
    const shippingOption = element.closest('.shipping-option');
    if (!shippingOption) return;

    shippingOption.querySelectorAll('.tip-option').forEach(option => option.classList.remove('selected'));
    element.classList.toggle('selected');
    shippingOption.classList.toggle('selected', element.classList.contains('selected'));

    if (amount === 'custom' && element.classList.contains('selected')) {
        const customAmount = prompt('Nhập số tiền tip (USD):', '2');
        if (customAmount && !isNaN(customAmount)) {
            element.textContent = `$${parseFloat(customAmount)}`;
            element.setAttribute('data-amount', parseFloat(customAmount));
        } else {
            element.textContent = 'Others';
            element.removeAttribute('data-amount');
        }
    }

    updateDeliveryFee();
}

/** ===========================
 *  API LẤY THÔNG TIN NGƯỜI DÙNG
 *  ===========================
 */
async function getUserId() {
    try {
        let response = await fetch("/Customer/GetUserId");
        let data = await response.json();

        if (!data.success || !data.userId || isNaN(data.userId) || data.userId <= 0) {
            alert("Không thể lấy userId! Vui lòng đăng nhập lại.");
            return null;
        }
        return data.userId;
    } catch (error) {
        console.error("Lỗi khi lấy userId:", error);
        alert("❌ Lỗi khi lấy userId!");
        return null;
    }
}

async function getCustomerInfo() {
    try {
        const response = await fetch("/Customer/GetCustomerData", {
            method: "GET",
            headers: {
                "Authorization": "Bearer " + localStorage.getItem("token")
            }
        });

        if (!response.ok) {
            console.error("Failed to fetch customer data:", response.status);
            return;
        }

        const data = await response.json();
        console.log("Data from API:", data);

        document.getElementById("email").value = data.email;
        const phoneInput = document.getElementById("phone");
        const addressInput = document.getElementById("address");

        console.log("Phone Input:", phoneInput);
        console.log("Address Input:", addressInput);

        // Kiểm tra null trước khi gán
        if (phoneInput) phoneInput.value = data.phone || "";
        if (addressInput) addressInput.value = data.address || "";

    } catch (error) {
        console.error("Error fetching customer info: ", error);
    }
}




