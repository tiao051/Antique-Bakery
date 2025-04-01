document.addEventListener("DOMContentLoaded", function () {
    const menuItems = document.querySelectorAll(".menu-item");
    menuItems.forEach(item => item.addEventListener("click", handleMenuClick));
    let savedPage = localStorage.getItem("activeMenu") || "/Admin/Index";
    let activeItem = Array.from(menuItems).find(item => item.getAttribute("data-page") === savedPage);

    if (activeItem) {
        activeItem.classList.add("active");
    }
    function handleMenuClick(event) {
        const item = event.currentTarget;
        const page = item.getAttribute("data-page");

        if (page) {
            setActiveMenu(item);
            window.location.href = page;
        }
    }
    function setActiveMenu(item) {
        menuItems.forEach(i => i.classList.remove("active")); 
        item.classList.add("active");
        localStorage.setItem("activeMenu", item.getAttribute("data-page") || "/Admin/Index");
    }
});
