$(document).ready(function () {
    handleSubcategoryClick();
    fetchRecommendations();
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
                console.log("Thanh cong 1");
                $("#menu-items").html(result);
            },
            error: function (xhr, status, error) {
                console.error("AJAX Error:", status, error);
                console.log(xhr.responseText);
                alert("Không thể tải dữ liệu, vui lòng thử lại.");
            }
        });
    });
}

function fetchRecommendations() {
    $.ajax({
        url: '/Customer/RecommentByUser',
        method: 'GET',
        headers: {
            'Authorization': 'Bearer ' + localStorage.getItem('token')
        },
        success: function (data) {
            console.log("Dữ liệu trả về từ API:", data);
            renderRecommendations(data);
        },
        error: function (xhr) {
            console.error('Lỗi khi lấy gợi ý:', xhr.responseText);
        }
    });
}

function renderRecommendations(products) {
    const $container = $('.recommended-items');
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
                    <button class="add-to-cart">Add to cart</button>
                </div>
            </div>
        `;
        $container.append(itemHtml);
    });
}
