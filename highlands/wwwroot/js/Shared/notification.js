// Notification.js - Notification System Scripts
// Display message for 2 seconds before starting fade-out
setTimeout(function () {
    var flashMessages = document.querySelectorAll('#flash-message');
    flashMessages.forEach(function (message) {
        message.classList.add('fade-out');
        setTimeout(function () {
            message.style.display = 'none';
        }, 1000); // Remove after fade completes
    });
}, 2000);
