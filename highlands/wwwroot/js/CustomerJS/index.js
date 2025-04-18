// ====================== ENTRY POINT ======================
$(document).ready(function () {
    initRecommendations();      
    handleSubcategoryClick();   
    fetchRecommendationsByUser();   
    fetchReconmendationsByTime();
});

function handleSubcategoryClick() {
    $(document).on("click", "a[data-subcategory]", function (e) {
        e.preventDefault();
        const subcategory = $(this).data("subcategory");

        $.ajax({
            url: '/Customer/MenuItems',
            type: 'GET',
            data: { subcategory },
            success: function (result) {
                $("#menu-items").html(result);
            },
            error: function (xhr, status, error) {
                console.error("AJAX Error:", status, error);
                alert("Không thể tải dữ liệu, vui lòng thử lại.");
            }
        });
    });
}

function fetchRecommendationsByUser() {
    $.ajax({
        url: '/Customer/RecommentByUser',
        method: 'GET',
        headers: {
            'Authorization': 'Bearer ' + localStorage.getItem('token')
        },
        success: function (data, textStatus, xhr) {
            if (xhr.status === 404) {
                $('#render-user-rec').empty(); 
                $('#render-user-rec').append('<p>Chưa có món gì để gợi ý, thử mua thử một vài món nhé! 😎.</p>');
                return;
            }
            renderRecommendationsByUser(data);
        },
        error: function (xhr, textStatus, errorThrown) {
            if (xhr.status === 404) {
                $('#render-user-rec').empty(); 
                $('#render-user-rec').append('<p>Chưa có món gì để gợi ý, thử mua thử một vài món nhé! 😎.</p>');
            } else {
                console.error('Lỗi khi lấy gợi ý:', xhr.responseText);
            }
        }
    });
}

function renderRecommendationsByUser(products) {
    const $container = $('#render-user-rec');
    $container.empty();

    if (!products || products.length === 0) {
        $container.append('<p>Không có gợi ý nào.</p>');
        return;
    }

    products.forEach(product => {
        const itemHtml = `
            <div class="recommended-item">
                <img src="${product.img}" alt="${product.name}" />
                <div class="item-details">
                    <h3>${product.name}</h3>
                    <button 
                        class="add-to-cart"
                        data-name="${product.name}"
                        data-subcategory="${product.subcategory || 'Other'}">
                        Add to cart
                    </button>
                </div>
            </div>
        `;
        $container.append(itemHtml);
    });

    // Gắn sự kiện click cho nút Add to cart
    $container.find('.add-to-cart').on('click', function () {
        const itemName = $(this).data('name');
        const subcategory = $(this).data('subcategory');
        const size = "M";

        if (!itemName || !subcategory) {
            console.error('Thiếu thông tin sản phẩm!');
            return;
        }

        const url = `/Customer/ItemSelected?Subcategory=${encodeURIComponent(subcategory)}&ItemName=${encodeURIComponent(itemName)}&size=${size}`;
        window.location.href = url;
    });
}
function fetchReconmendationsByTime() {
    const currentHour = new Date().getHours();

    $.ajax({
        url: '/Customer/RecommentByTime',
        method: 'GET',
        data: {
            hour: currentHour
        },
        success: function (data) {
            console.log('JS nhan thanh cong');
            data.forEach(function (product) {
                console.log('Product:', product);
            });

            renderRecommendationsByTime(data);
        },
        error: function (xhr) {
            console.error('Loi:', xhr.responseText);
        }
    });
}
function renderRecommendationsByTime(products) {
    const $container = $('#render-time-rec'); 
    $container.empty();

    if (!products || products.length === 0) {
        $container.append('<p>Không có gợi ý nào.</p>');
        return;
    }

    products.forEach(product => {
        const itemHtml = `
            <div class="recommended-item">
                <img src="${product.img}" alt="${product.name}" />
                <div class="item-details">
                    <h3>${product.name}</h3>
                    <button 
                        class="add-to-cart"
                        data-name="${product.name}"
                        data-subcategory="${product.subcategory || 'Other'}">
                        Add to cart
                    </button>
                </div>
            </div>
        `;
        $container.append(itemHtml);
    });

    $container.find('.add-to-cart').on('click', function () {
        const itemName = $(this).data('name');
        const subcategory = $(this).data('subcategory');
        const size = "M";

        if (!itemName || !subcategory) {
            console.error('Thiếu thông tin sản phẩm!');
            return;
        }

        const url = `/Customer/ItemSelected?Subcategory=${encodeURIComponent(subcategory)}&ItemName=${encodeURIComponent(itemName)}&size=${size}`;
        window.location.href = url;
    });
}

function getDOMReferences() {
    return {
        timeRecommendations: document.getElementById('time-recommendations'),
        timePeriodText: document.getElementById('time-period'),
        timeIcon: document.querySelector('#time-recommendations .banner-icon:last-child'),
        timeRecommendationDivs: document.querySelectorAll('.time-recommendation'),
        userSection: document.getElementById("user-recommendations"),
        timeSection: document.getElementById("time-recommendations")
    };
}

function getCurrentTimePeriod() {
    const hour = new Date().getHours();

    if (hour >= 5 && hour < 11)
        return { period: 'Morning', elementId: 'morning-items', icon: '☕' };
    if (hour >= 11 && hour < 14)
        return { period: 'Noon', elementId: 'noon-items', icon: '🍽️' };
    if (hour >= 14 && hour < 18)
        return { period: 'Afternoon', elementId: 'afternoon-items', icon: '🍰' };
    return { period: 'Evening', elementId: 'evening-items', icon: '🌙' };
}

function updateTimeContent(dom) {
    const timeInfo = getCurrentTimePeriod();
    dom.timePeriodText.textContent = timeInfo.period;
    dom.timeIcon.textContent = timeInfo.icon;

    dom.timeRecommendationDivs.forEach(item => item.style.display = 'none');

    const activePanel = document.getElementById(timeInfo.elementId);
    if (activePanel) {
        activePanel.style.display = 'block';
    }
}

function toggleRecommendations(dom) {
    const showingUser = dom.userSection.classList.contains("active");
    const userHovering = dom.userSection.matches(':hover');
    const timeHovering = dom.timeSection.matches(':hover');

    if (!userHovering && !timeHovering) {
        dom.userSection.classList.toggle("active", !showingUser);
        dom.timeSection.classList.toggle("active", showingUser);

        if (showingUser) updateTimeContent(dom);
    }
}

function initRecommendations() {
    const dom = getDOMReferences();

    dom.timeRecommendations.classList.add('hidden-panel');
    updateTimeContent(dom);

    setInterval(() => toggleRecommendations(dom), 2000);
}
