document.addEventListener("DOMContentLoaded", function () {
    getCustomerOrder();
});

function getCustomerOrder() {
    fetch('/api/AdminApi/getOrder')
        .then(response => response.json())
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
                        <td>#USR-${order.orderId}</td>
                        <td>#CUS-${order.customerId}</td>
                        <td>$${order.totalAmount}</td>
                        <td>${formatted}</td>
                        <td><span class="status ${statusClass}">${order.status}</span></td>
                    `;

                tableBody.appendChild(row);
            });
        })
        .catch(error => console.error('Error:', error));
}