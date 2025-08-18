document.addEventListener('DOMContentLoaded', function() {
    const form = document.getElementById('verifyOtpForm');
    const emailInput = document.getElementById('email');
    const otpInput = document.getElementById('otpCode');
    const resendBtn = document.getElementById('resendOtpBtn');
    const loadingIndicator = document.getElementById('loadingIndicator');
    const successMessage = document.getElementById('successMessage');
    const errorMessage = document.getElementById('errorMessage');
    const successText = document.getElementById('successText');
    const errorText = document.getElementById('errorText');

    let resendCooldown = false;

    form.addEventListener('submit', async function(e) {
        e.preventDefault();
        
        const email = emailInput.value.trim();
        const otpCode = otpInput.value.trim();
        
        // Validate inputs
        if (!email || !otpCode) {
            showError('Please enter all required information.');
            return;
        }

        if (otpCode.length !== 6) {
            showError('OTP code must be 6 characters.');
            return;
        }

        // Show loading
        showLoading();
        hideMessages();

        try {
            const response = await fetch('/Account/VerifyOtp', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({ 
                    email: email,
                    otpCode: otpCode 
                })
            });

            const result = await response.json();
            hideLoading();

            if (result.success) {
                showSuccess(result.message);
                // Redirect to reset password page after 1 second
                setTimeout(() => {
                    window.location.href = `/Account/ResetPassword?email=${encodeURIComponent(email)}&otp=${encodeURIComponent(otpCode)}`;
                }, 1000);
            } else {
                showError(result.message);
                otpInput.focus();
                otpInput.select();
            }
        } catch (error) {
            hideLoading();
            showError('An error occurred. Please try again.');
            console.error('Error:', error);
        }
    });

    // Resend OTP functionality
    resendBtn.addEventListener('click', async function() {
        if (resendCooldown) {
            showError('Please wait 60 seconds before resending OTP.');
            return;
        }

        const email = emailInput.value.trim();
        if (!email) {
            showError('Invalid email.');
            return;
        }

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
                showSuccess('New OTP code has been sent to your email.');
                otpInput.value = '';
                otpInput.focus();
                
                // Start cooldown
                startResendCooldown();
            } else {
                showError(result.message);
            }
        } catch (error) {
            hideLoading();
            showError('An error occurred while resending OTP.');
            console.error('Error:', error);
        }
    });

    function startResendCooldown() {
        resendCooldown = true;
        let countdown = 60;
        
        const originalText = resendBtn.innerHTML;
        
        const updateButton = () => {
            resendBtn.innerHTML = `<i class="fas fa-clock"></i> Resend in ${countdown}s`;
            resendBtn.style.opacity = '0.6';
            countdown--;
            
            if (countdown < 0) {
                resendCooldown = false;
                resendBtn.innerHTML = originalText;
                resendBtn.style.opacity = '1';
            } else {
                setTimeout(updateButton, 1000);
            }
        };
        
        updateButton();
    }

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

    // Auto-focus on OTP input and format input
    otpInput.focus();
    
    // Only allow numbers in OTP input
    otpInput.addEventListener('input', function() {
        this.value = this.value.replace(/[^0-9]/g, '');
        if (this.value.length > 6) {
            this.value = this.value.slice(0, 6);
        }
    });
});
