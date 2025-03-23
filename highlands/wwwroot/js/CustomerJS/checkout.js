  
    const selectedOptions = new Set();
     async function payNow() { 
        try {

            const totalPaymentElement = document.querySelector('.total-row span:last-child');
            let totalAmount = parseFloat(totalPaymentElement.textContent.replace('$', '')) || 0;

            console.log("🔹 Tổng tiền gửi lên server:", totalAmount);
            // Gửi request để lấy userId từ server
            let userResponse = await fetch("/Customer/GetUserId");
            let userData = await userResponse.json();

            if (!userData.success || !userData.userId || isNaN(userData.userId) || userData.userId <= 0) {
                alert("❌ Không thể lấy userId! Vui lòng đăng nhập lại.");
                return;
            }

            let userId = userData.userId;
            const requestData = {
                userId: userId,
                totalAmount: parseFloat(totalAmount.toFixed(2))
            };

            console.log("Request gửi lên server:", JSON.stringify(requestData));

            // Gửi request thanh toán
            const response = await fetch("/customer/pay", {
                method: "POST",
                headers: {
                    "Accept": "application/json",
                    "Content-Type": "application/json"
                },
                body: JSON.stringify(requestData)
            });

            const result = await response.text();
            if (response.ok) {
                alert("✅ " + result);
            } else {
                alert("❌ " + result);
            }
        } catch (error) {
            console.error("Lỗi khi gửi yêu cầu:", error);
            alert("❌ Có lỗi xảy ra! Vui lòng thử lại.");
        }
    }
    function updateDeliveryFee() {
        const deliveryFeeElement = document.querySelector('.summary-row:nth-of-type(6) span:last-child'); // Delivery Preferences Fee
        let deliveryFee = 0;

        // Kiểm tra xem Doorstep Delivery có được chọn không
        document.querySelectorAll('.shipping-option.selected').forEach(option => {
        if (option.dataset.type === 'doorstep') {
            let shippingPrice = option.querySelector('.shipping-price').textContent.replace('$', '');
            deliveryFee += parseFloat(shippingPrice) || 0;
            }
        });


        // Kiểm tra xem có tip được chọn không
        const selectedTip = document.querySelector('.tip-options .selected');
        if (selectedTip) {
            let tipAmount = selectedTip.getAttribute('data-amount') || selectedTip.textContent.replace('$', '');
            deliveryFee += parseFloat(tipAmount) || 0;
        }

        // Cập nhật giá trị hiển thị chính xác
        deliveryFeeElement.textContent = `$${deliveryFee.toFixed(2)}`;

        // Cập nhật tổng thanh toán
        updateTotalPayment(deliveryFee);
    }

    function updateTotalPayment(deliveryFee) {
        const subtotalElement = document.querySelector('.summary-row:nth-of-type(4) span:last-child'); // Subtotal
        const taxElement = document.querySelector('.summary-row:nth-of-type(5) span:last-child'); // Tax
        const totalPaymentElement = document.querySelector('.total-row span:last-child'); // Total Payment

        let subtotal = parseFloat(subtotalElement.textContent.replace('$', '')) || 0;
        let tax = parseFloat(taxElement.textContent.replace('$', '')) || 0;

        let total = subtotal + tax + deliveryFee;

        // Cập nhật tổng thanh toán
        totalPaymentElement.textContent = `$${total.toFixed(2)}`;
    }
    function selectShipping(element, type) {
        element.classList.toggle('selected'); 

        const radio = element.querySelector('.shipping-radio');
        if (radio) {
            radio.checked = !radio.checked;
        }

        // Thêm hoặc xóa khỏi danh sách chọn
        if (selectedOptions.has(type)) {
            selectedOptions.delete(type);
        } else {
            selectedOptions.add(type);
        }
        updateDeliveryFee();
    }
    function getSelectedShipping() {
        return Array.from(selectedOptions);
    }
    function selectTip(element, amount) {
        const shippingOption = element.closest('.shipping-option'); 
        if (!shippingOption) return;

        const tipContainer = shippingOption.querySelector('.tip-options');
        const allTipOptions = tipContainer.querySelectorAll('.tip-option');

        // Kiểm tra xem tip này đã được chọn chưa
        const isSelected = element.classList.contains('selected');

        // Xóa tất cả 'selected' trong cùng nhóm tip
        allTipOptions.forEach(option => option.classList.remove('selected'));

        if (!isSelected) {
            element.classList.add('selected');
            shippingOption.classList.add('selected'); // Thêm class selected vào shipping-option cha
        } else {
            shippingOption.classList.remove('selected'); // Nếu bỏ chọn, bỏ luôn selected của shipping-option cha
        }

        // Đồng bộ radio input
        const radio = shippingOption.querySelector('.shipping-radio');
        if (radio) {
            radio.checked = !isSelected;
        }

        // Xử lý hiển thị cho "Others"
        if (amount === 'custom') {
            if (!isSelected) {
                const customAmount = prompt('Nhập số tiền tip (USD):', '2');
                if (customAmount && !isNaN(customAmount)) {
                    element.textContent = `$${parseFloat(customAmount)}`;
                    element.setAttribute('data-amount', parseFloat(customAmount));
                } else {
                    element.textContent = 'Others'; // Nếu nhập sai, reset về Others
                }
            } else {
                element.textContent = 'Others';
                element.removeAttribute('data-amount');
            }
        }
        updateDeliveryFee();
    }
    // async function getCustomerInfo() {
    //     try {
    //         // Lấy userId từ API trước
    //         let userResponse = await fetch("/Customer/GetUserId");
    //         if (!userResponse.ok) {
    //             throw new Error("Không thể lấy userId! Vui lòng đăng nhập lại.");
    //         }

    //         let userData = await userResponse.json();

    //         // Kiểm tra dữ liệu hợp lệ
    //         if (!userData.success || !userData.userId || isNaN(userData.userId) || userData.userId <= 0) {
    //             alert("❌ Không thể lấy userId! Vui lòng đăng nhập lại.");
    //             return;
    //         }

    //         let userId = userData.userId;

    //         // Gọi API lấy thông tin khách hàng
    //         let response = await fetch(`/api/customer/get-customer-info?userId=${userId}`);
    //         if (!response.ok) {
    //             throw new Error("Failed to fetch customer info");
    //         }

    //         let data = await response.json();
    //         console.log("Customer Info:", data);

    //         // Hiển thị lên UI
    //         updateField("email-container", "email", data.email);
    //         updateField("address-container", "address", data.deliveryAddress);
    //         updateField("phone-container", "phone", data.phone);

    //         return data;
    //     } catch (error) {
    //         console.error("Error fetching customer info:", error);
    //         alert("❌ Lỗi khi lấy thông tin khách hàng!");
    //     }
    // }

    // // Gọi API khi load trang
    // document.addEventListener("DOMContentLoaded", function () {
    //     getCustomerInfo();
    // });
