function checkAndShowHideMenu() {
    if (window.innerWidth < 768) {
        $('#tm-nav ul').addClass('hidden');
    } else {
        $('#tm-nav ul').removeClass('hidden');
    }
}

$(function () {
    var tmNav = $('#tm-nav');
    tmNav.singlePageNav();

    checkAndShowHideMenu();
    window.addEventListener('resize', checkAndShowHideMenu);

    $('#menu-toggle').click(function () {
        $('#tm-nav ul').toggleClass('hidden');
    });

    $('#tm-nav ul li').click(function () {
        if (window.innerWidth < 768) {
            $('#tm-nav ul').addClass('hidden');
        }
    });

    $(document).scroll(function () {
        var distanceFromTop = $(document).scrollTop();

        if (distanceFromTop > 100) {
            tmNav.addClass('scroll');
        } else {
            tmNav.removeClass('scroll');
        }
    });
});
