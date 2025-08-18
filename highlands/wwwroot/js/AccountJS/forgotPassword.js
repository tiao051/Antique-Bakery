document.addEventListener('DOMContentLoaded', function() {
    const form = document.getElementById('forgotPasswordForm');
    const emailInput = document.getElementById('email');
    const loadingIndicator = document.getElementById('loadingIndicator');
    const successMessage = document.getElementById('successMessage');
    const errorMessage = document.getElementById('errorMessage');
    const successText = document.getElementById('successText');
    const errorText = document.getElementById('errorText');

    form.addEventListener('submit', async function(e) {
        e.preventDefault();
        
        const email = emailInput.value.trim();
        
        // Validate email
        if (!email) {
            showError('Please enter your email.');
            return;
        }

        if (!isValidEmail(email)) {
            showError('Invalid email format.');
            return;
        }

        // Show loading
        showLoading();
        hideMessages();

        try {
            const response = await fetch('/Account/ForgotPassword', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({ email: email })
            });

            const result = await response.json();
            hideLoading();

            if (result.success) {
                showSuccess(result.message);
                // Redirect to verify OTP page after 2 seconds
                setTimeout(() => {
                    window.location.href = `/Account/VerifyOtp?email=${encodeURIComponent(email)}`;
                }, 2000);
            } else {
                showError(result.message);
            }
        } catch (error) {
            hideLoading();
            showError('An error occurred. Please try again.');
            console.error('Error:', error);
        }
    });

    function showLoading() {
        loadingIndicator.style.display = 'block';
    }

    function hideLoading() {
        loadingIndicator.style.display = 'none';
    }

    function showSuccess(message) {
        successText.textContent = message;
        successMessage.style.display = 'block';
        errorMessage.style.display = 'none';
    }

    function showError(message) {
        errorText.textContent = message;
        errorMessage.style.display = 'block';
        successMessage.style.display = 'none';
    }

    function hideMessages() {
        successMessage.style.display = 'none';
        errorMessage.style.display = 'none';
    }

    function isValidEmail(email) {
        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        return emailRegex.test(email);
    }

    // Auto-focus on email input
    emailInput.focus();
});
