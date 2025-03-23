document.addEventListener("DOMContentLoaded", function () {
    try {
        getAuthHeaders();
        getCustomerOrder();
        getRealtimeOrders();
    } catch (error) {
        alert(error.message);
    }
});

function getAuthHeaders() {
    const accessToken = localStorage.getItem("accessToken");
    if (!accessToken) {
        throw new Error("No access token found. Please login again.");
    }
    return {
        "Content-Type": "application/json",
        "Authorization": `Bearer ${accessToken}`,
    };
}

function getRealtimeOrders() {
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/OrderHub")
        .build();

    connection.start().then(function () {
        console.log("Connection to Hub established");
    }).catch(function (err) {
        console.error("SignalR connection failed: " + err.toString());
    });

    connection.on("ReceiveNewOrder", function () {
        console.log("New order received!");
        getCustomerOrder(); 
    });
}

function getCustomerOrder() {
    fetch('/api/AdminApi/getOrder', {
        method: 'GET',
        headers: getAuthHeaders()
    })
    .then(response => {
        if (response.status === 403) {
            throw new Error("You don't have permission to access this resource.");
        } else if (response.status === 401) {
            throw new Error("Your session has expired. Please log in again.");
        }
        return response.json();
    })
    .then(data => {
        console.log("Received Data:", data);
        const tableBody = document.getElementById('admin-table-body');
        tableBody.innerHTML = "";

        data.forEach(order => {
            const row = document.createElement('tr');
            row.classList.add("table-row");

            let statusClass = "";
            switch (order.status) {
                case "Confirmed":
                    statusClass = "status-completed";
                    break;
                case "Pending":
                    statusClass = "status-pending";
                    break;
                case "Cancelled":
                    statusClass = "status-cancelled";
                    break;
                default:
                    statusClass = "status-unknown";
            }

            const date = new Date(order.orderDate);
            const options = {
                day: "2-digit",
                month: "short",
                year: "numeric",
                hour: "2-digit",
                minute: "2-digit",
                hour12: true
            };
            const formatted = new Intl.DateTimeFormat("en-US", options).format(date);

            row.innerHTML = `
            <td id="order-${order.orderId}">#ORD-${order.orderId}</td>
            <td>#CUS-${order.customerId}</td>
            <td>$${order.totalAmount}</td>
            <td>${formatted}</td>
            <td><span class="status ${statusClass}">${order.status}</span></td>
        `;
            tableBody.appendChild(row);
        });
    })
    .catch(error => {
        console.error('Error:', error);
        alert("Error fetching orders: " + error.message);
    });
}
