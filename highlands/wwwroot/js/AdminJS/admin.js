document.addEventListener("DOMContentLoaded", function () {
    console.log("DOM fully loaded. Initializing...");

    try {
        getAuthHeaders();
        getCustomerOrder();
        getRealtimeOrders();
        getMonthData();
        let activeFilter = document.querySelector('.chart-filter.active');
        if (!activeFilter) {
            activeFilter = document.querySelector('.chart-filter[data-timeframe="day"]');
            if (activeFilter) activeFilter.classList.add('active');
        }

        if (activeFilter) {
            const timeFrame = activeFilter.getAttribute("data-timeframe") || activeFilter.innerText.toLowerCase();
            getCustomerOrderDetail(timeFrame);
        }

        document.querySelectorAll('.chart-filter').forEach(filter => {
            filter.addEventListener('click', function () {
                handleFilterClick(this);
            });
        });
        document.querySelectorAll(".menu-item").forEach(item => {
            item.addEventListener("click", function () {
                const page = this.getAttribute("data-page");
                if (page) window.location.href = page;
            });
        });
    } catch (error) {
        alert(error.message);
    }
});

function handleFilterClick(selectedFilter) {
    if (selectedFilter.classList.contains('active')) return;

    document.querySelectorAll('.chart-filter').forEach(f => f.classList.remove('active'));
    selectedFilter.classList.add('active');

    const timeFrame = selectedFilter.innerText.trim().toLowerCase();
    getCustomerOrderDetail(timeFrame);
}
function getAuthHeaders() {
    console.log("test");
    const accessToken = localStorage.getItem("accessToken");
    console.log(accessToken)
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
        getMonthData();
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

function getMonthData() {
    fetch('/api/AdminApi/getMonthDetail', {
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
            if (data && data.totalRevenue !== undefined && data.totalOrders !== undefined && data.totalCustomers !== undefined && data.totalQuantity !== undefined) {
                const statValues = document.querySelectorAll(".stat-value");
                if (statValues.length >= 2) {
                    statValues[0].textContent = `$${data.totalRevenue.toLocaleString()}`; 
                    statValues[1].textContent = `${data.totalOrders.toLocaleString()}`; 
                    statValues[2].textContent = `${data.totalCustomers.toLocaleString()}`;
                    statValues[3].textContent = `${data.totalQuantity.toLocaleString()}`;
                }
            } else {
                console.error("Invalid data format:", data);
            }
        })
        .catch(error => console.error("Error fetching month data:", error));
}
async function getCustomerOrderDetail(timeFrame) {
    try {
        console.log(`Fetching data for timeFrame: ${timeFrame}`);
        const response = await fetch(`/api/AdminApi/getOrderDetail/${timeFrame}`, {
            method: 'GET',
            headers: getAuthHeaders()
        });
        if (!response.ok) {
            throw new Error(`Error: ${response.statusText}`);
        }
        const orderDetail = await response.json();
        const labels = orderDetail.map(item => item.subCategory);
        const dataValues = orderDetail.map(item => item.totalRevenue);

        const exquisitePalette = [
            '#FF9FB0', // Rose pink
            '#61CDBB', // Jade green
            '#E8C1A0', // Champagne
            '#F1E15B', // Citrine
            '#8D5B4C', // Cedar
            '#A5AAB0', // Silver mist
            '#5C7AFF', // Hyacinth blue
            '#FEB95F', // Golden apricot
            '#BA68C8', // Orchid
            '#54BFB2', // Aquamarine
            '#FF9FB0', // Cherry blossom
            '#D1BCF5', // Lavender mist
            '#FFD166', // Marigold
            '#06D6A0', // Caribbean green
            '#5B7CF5'  // Periwinkle blue
        ];

        while (exquisitePalette.length < labels.length) {
            exquisitePalette = exquisitePalette.concat(exquisitePalette);
        }

        const backgroundColors = exquisitePalette.slice(0, labels.length);

        const ctx = document.getElementById('revenueChart').getContext('2d');
        window.revenueChart = new Chart(ctx, {
            type: 'pie',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Total Revenue',
                    data: dataValues,
                    backgroundColor: backgroundColors,
                    borderColor: "#ffffff",
                    borderWidth: 2
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                layout: {
                    padding: { left: 50, right: 50 }
                },
                plugins: {
                    legend: {
                        display: true,
                        position: "right",
                        labels: {
                            padding: 20,
                            boxWidth: 20,
                            font: { size: 14 }
                        }
                    }
                }
            }
        });
    } catch (error) {
        console.error("Failed to fetch order details:", error);
        alert(error.message);
    }
}