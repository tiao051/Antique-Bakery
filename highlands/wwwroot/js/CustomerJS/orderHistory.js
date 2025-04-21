let orderHistory = [];

document.addEventListener('DOMContentLoaded', () => {
    fetchOrderHistory();
    setupModalEvents();
});

// Fetch order history from API
async function fetchOrderHistory() {
    const container = document.getElementById('orderHistoryContainer');

    try {
        const response = await fetch("/Customer/HistoryPurchaseData");
        if (!response.ok) {
            throw new Error('No order history found or user not authorized.');
        }

        orderHistory = await response.json();
        displayOrderHistory();
    } catch (error) {
        console.error(error);
        container.innerHTML = renderEmptyHistory('Why not treat yourself to something nice? 😊');
    }
}

function displayOrderHistory() {
    const container = document.getElementById('orderHistoryContainer');

    // Kiểm tra nếu không có đơn hàng
    if (!orderHistory.length) {
        container.innerHTML = renderEmptyHistory();
        return;
    }

    const orderListHTML = `
    <div class="order-list">
        ${orderHistory.map(order => {
        const previewItem = order.items[0];
        const additionalItems = order.items.length > 1
            ? `<div class="order-item-count">+${order.items.length - 1} more item(s)</div>`
            : '';

        return `
            <div class="order-card" data-order-id="${order.orderId}">
                <div class="order-header">
                    <div class="order-id">#Order ${order.orderId}</div>
                    <div class="order-date">${formatDate(order.orderDate)}</div>
                </div>
                <div class="order-preview">
                    <img src="${previewItem.itemImg}" alt="${previewItem.itemName}" class="order-image">
                    <div class="order-details">
                        <div class="order-name">${previewItem.itemName}</div>
                        ${additionalItems}
                    </div>
                    <div class="order-total">$${order.totalAmount.toFixed(2)}</div>
                </div>
            </div>`;
    }).join('')}
    </div>`;

    container.innerHTML = orderListHTML;

    // Thêm sự kiện click vào các order card
    document.querySelectorAll('.order-card').forEach(card => {
        card.addEventListener('click', function () {
            const orderId = this.dataset.orderId;
            const order = orderHistory.find(o => o.orderId === Number(orderId));
            openOrderModal(order);
        });
    });
}

function formatDate(dateStr) {
    const date = new Date(dateStr);
    return date.toLocaleDateString('en-US', {
        year: 'numeric',
        month: 'short',
        day: 'numeric'
    });
}

// Render an order modal
function openOrderModal(order) {
    const modal = document.getElementById('orderModal');
    const modalContent = document.getElementById('modalContent');

    if (!modal || !modalContent || !order) return;

    // Sử dụng hàm renderOrderItem để render các items trong đơn hàng
    const itemsHTML = order.items.map(item => renderOrderItem(item)).join('');

    const subtotal = order.items.reduce((sum, item) => sum + (item.price * item.quantity), 0);
    const total = order.totalAmount;
    const others = total - subtotal;

    // Tạo phần tóm tắt đơn hàng
    const summaryHTML = `
        <div class="order-summary">
            <div class="summary-row">
                <div>Subtotal</div>
                <div>$${subtotal.toFixed(2)}</div>
            </div>
            <div class="summary-row">
                <div>Others</div>
                <div>$${others.toFixed(2)}</div>
            </div>
            <div class="summary-row">
                <div>Total</div>
                <div>$${total.toFixed(2)}</div>
            </div>
        </div>
        <div class="coffee-icon">
            <span>☕</span>
        </div>
    `;

    // Gắn nội dung vào modalContent
    modalContent.innerHTML = `
        <div class="order-header">
            <h2 class="modal-title">Order #${order.orderId}</h2>
            <div class="order-date">${formatDate(order.orderDate)}</div>
        </div>
        ${itemsHTML}
        ${summaryHTML}
    `;

    modal.classList.add('active');
    setupModalEvents();
}

// Render each item in the modal
function renderOrderItem(item) {
    return `
    <div class="order-item">
        <img src="${item.itemImg}" alt="${item.itemName}" class="item-image">
        <div class="item-details">
            <div class="item-name">${item.itemName}</div>
            <div class="item-specs">Size: ${item.size}</div>
            <div class="item-price">$${item.price.toFixed(2)} each</div>
        </div>
        <div class="item-quantity">×${item.quantity}</div>
    </div>
    `;
}

// Render empty history UI
function renderEmptyHistory(title = 'No Purchase History', message = "You haven't made any purchases yet.") {
    return `
    <div class="empty-history">
        <h3>${title}</h3>
        <p>${message}</p>
        <a href="/Customer/Index" class="browse-menu-btn">Browse Our Menu</a>
        <div class="coffee-icon"><span>☕</span></div>
    </div>
    `;
}

// Setup modal close functionality
function setupModalEvents() {
    document.getElementById('closeModal')?.addEventListener('click', closeModal);
    document.getElementById('orderModal')?.addEventListener('click', e => {
        if (e.target.id === 'orderModal') {
            closeModal();
        }
    });
}

function closeModal() {
    document.getElementById('orderModal')?.classList.remove('active');
}
