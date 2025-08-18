document.addEventListener("DOMContentLoaded", function () {
    // xu ly chon size
    const sizeButtons = document.querySelectorAll(".size-button");
    // luu gia tri ban dau cua nguyen lieu theo size (để thay đổi cái whats included lại)
    const initialIngredientValues = {};

    document.querySelectorAll(".ingredient-select, .quantity").forEach(element => {
        initialIngredientValues[element.dataset.ingredientId] = element.value || element.textContent;
    });
    // chon mac dinh la 16oz
    function selectDefaultSize() {
        sizeButtons.forEach(btn => btn.classList.remove("selected"));
        const defaultSize = document.querySelector(".size-button.medium");
        if (defaultSize) {
            defaultSize.classList.add("selected");
        }
    }

    sizeButtons.forEach(button => {
        button.addEventListener("click", function () {
            sizeButtons.forEach(btn => btn.classList.remove("selected"));
            this.classList.add("selected");
            console.log(this);

            const selectedSize = this.getAttribute("data-size");
            const itemName = this.getAttribute("data-name");

            fetch(`/Customer/LoadRecipePartial?size=${encodeURIComponent(selectedSize)}&itemName=${encodeURIComponent(itemName)}`,
                {
                    method: "GET"
                })
                .then(response => {
                    if (!response.ok) {
                        throw new Error(`HTTP error! status: ${response.status}`);
                    }
                    return response.text();
                })
                .then(data => {
                    const ingredientsContainer = document.querySelector(".ingredients-container");
                    if (ingredientsContainer) {
                        ingredientsContainer.innerHTML = data;

                        // Sau khi cập nhật nội dung, gán lại event listeners
                        attachQuantityControlEvents();
                    }
                })
                .catch(error => {
                    console.error("Error loading recipe:", error);
                    
                    // Display fallback message when error occurs
                    const ingredientsContainer = document.querySelector(".ingredients-container");
                    if (ingredientsContainer) {
                        ingredientsContainer.innerHTML = `
                            <div class="no-ingredients-message" style="text-align: center; padding: 30px 20px; color: #666; font-style: italic;">
                                <div style="background-color: #f8f9fa; border-radius: 8px; padding: 20px; border-left: 4px solid #00704A;">
                                    <i class="fas fa-info-circle" style="color: #00704A; margin-right: 8px; font-size: 18px;"></i>
                                    <span style="font-size: 16px; line-height: 1.5; font-weight: 500;">
                                        We currently do not support ingredient customization for this product.
                                    </span>
                                </div>
                            </div>
                        `;
                    }
                });
        });
    });

    // load ham
    attachQuantityControlEvents();
    selectDefaultSize();

    // xu ly button tang giam
    function attachQuantityControlEvents() {
        document.querySelectorAll(".quantity-control").forEach(control => {
            const decreaseBtn = control.querySelector(".btn-decrease");
            const increaseBtn = control.querySelector(".btn-increase");
            const quantitySpan = control.querySelector(".quantity");

            const ingredientSection = control.closest(".ingredient-item");
            const ingredientType = ingredientSection?.getAttribute("data-ingredient-type");

            decreaseBtn.addEventListener("click", function () {
                let currentValue = parseFloat(quantitySpan.textContent);

                if (ingredientType === "Main-Pumps") {
                    if (currentValue > 1) {
                        quantitySpan.textContent = (currentValue - 0.5).toFixed(1);
                        updateTitle();
                    }
                }
                else if (ingredientType === "Shots") {
                    if (currentValue > 1) {
                        quantitySpan.textContent = (currentValue - 1).toFixed(1);
                        updateTitle();
                    }
                }
                else {
                    if (currentValue > 0) {
                        quantitySpan.textContent = (currentValue - 0.5).toFixed(1);
                        updateTitle();
                    }
                }
            });

            increaseBtn.addEventListener("click", function () {
                let currentValue = parseFloat(quantitySpan.textContent);
                if (ingredientType === "Main-Pumps") {
                    if (currentValue >= 1) {
                        quantitySpan.textContent = (currentValue + 0.5).toFixed(1);
                        updateTitle();
                    }
                }
                else if (ingredientType === "Shots") {
                    if (currentValue >= 1) {
                        quantitySpan.textContent = (currentValue + 1).toFixed(1);
                        updateTitle();
                    }
                }
                else {
                    if (currentValue > 0) {
                        quantitySpan.textContent = (currentValue + 0.5).toFixed(1);
                        updateTitle();
                    }
                }
            });
        });
    }

    // mục tiêu là lấy được giá trị ban đầu của từng ingredient trong công thức sau đó sẽ so sánh nó có thay đổi với
    // lúc đầu sau khi tăng giảm hoặc thay đổi không. Nếu có thì đổi, ko thì giữ nguyên. Update liên tục
    // // update tieu de theo activities
    // const title = document.getElementById("includedTitle");
    // function updateTitle(){
    //     title.textContent = "Customer Customized";
    // }

    // // theo doi thay doi
    // document.querySelectorAll(".ingredient-select").forEach(select => {
    //     select.addEventListener("change", updateTitle);
    // });
});
$(document).ready(function () {
    $("#addToOrderBtn").click(async function () {
        let itemName = $(".item-name-track").text().trim();
        let size = $(".size-button.selected").data("size");
        let itemImg = document.querySelector(".item-img")?.getAttribute("src") || "";

        if (!itemName || !size) {
            alert("Please select an item and size.");
            return;
        }

        console.log("[DEBUG] Add to Order Clicked:", { itemName, size });

        try {
            // Bước 1: Lấy userId
            let userResponse = await $.get("/Customer/GetUserId");

            if (!userResponse.success || userResponse.userId === 0) {
                alert("User not authenticated!");
                return;
            }

            let userId = userResponse.userId;

            // Bước 2: Lấy giá sản phẩm
            let priceResponse = await $.get("/Customer/GetPrice", { itemName, size });

            if (!priceResponse.success) {
                alert("Failed to fetch price.");
                return;
            }

            let price = priceResponse.price;

            // Bước 3: Thêm vào giỏ hàng
            let orderResponse = await $.post("/Customer/AddToOrder", {
                userId,
                itemName,
                size,
                price,
                quantity: 1,
                itemImg
            });

            console.log("[DEBUG] Order Response:", orderResponse);

            if (orderResponse.success) {
                const cartCountElement = document.querySelector('.cart-count');

                if (cartCountElement) {
                    cartCountElement.classList.add('cart-count-animated');

                    setTimeout(() => {
                        cartCountElement.textContent = orderResponse.cartCount;
                    }, 250);

                    setTimeout(() => {
                        cartCountElement.classList.remove('cart-count-animated');
                    }, 500);
                }

                // Bắn event để các nơi khác có thể cập nhật UI
                $(document).trigger("cartUpdated");
            } else {
                alert(orderResponse.message);
            }
        } catch (error) {
            console.error("Error during add to cart:", error);
            alert("An unexpected error occurred!");
        }
    });
});