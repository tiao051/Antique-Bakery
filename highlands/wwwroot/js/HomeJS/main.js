document.getElementById('accountLink').addEventListener('click', function (e) {
    e.preventDefault(); 
    window.location.href = '/account/index';
});

document.querySelectorAll('a[href^="#"]').forEach(anchor => {
    anchor.addEventListener('click', function (e) {
        e.preventDefault();

        document.querySelector(this.getAttribute('href')).scrollIntoView({
            behavior: 'smooth'
        });
    });
});
