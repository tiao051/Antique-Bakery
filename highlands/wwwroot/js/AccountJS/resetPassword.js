document.addEventListener('DOMContentLoaded', function() {
    const form = document.getElementById('resetPasswordForm');
    const emailInput = document.getElementById('email');
    const otpInput = document.getElementById('otpCode');
    const newPasswordInput = document.getElementById('newPassword');
    const confirmPasswordInput = document.getElementById('confirmPassword');
    const loadingIndicator = document.getElementById('loadingIndicator');
    const successMessage = document.getElementById('successMessage');
    const errorMessage = document.getElementById('errorMessage');
    const successText = document.getElementById('successText');
    const errorText = document.getElementById('errorText');

    // Password strength elements
    const strengthBar = document.getElementById('strengthFill');
    const strengthText = document.getElementById('strengthText');

    form.addEventListener('submit', async function(e) {
        e.preventDefault();
        
        const email = emailInput.value.trim();
        const otpCode = otpInput.value.trim();
        const newPassword = newPasswordInput.value;
        const confirmPassword = confirmPasswordInput.value;
        
        // Validate inputs
        if (!email || !otpCode || !newPassword || !confirmPassword) {
            showError('Please enter all required information.');
            return;
        }

        if (newPassword.length < 6) {
            showError('Password must be at least 6 characters.');
            return;
        }

        if (newPassword !== confirmPassword) {
            showError('Confirm password does not match.');
            return;
        }

        // Show loading
        showLoading();
        hideMessages();

        try {
            const response = await fetch('/Account/ResetPassword', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({ 
                    email: email,
                    otpCode: otpCode,
                    newPassword: newPassword,
                    confirmPassword: confirmPassword
                })
            });

            const result = await response.json();
            hideLoading();

            if (result.success) {
                showSuccess(result.message);
                // Disable form
                form.style.opacity = '0.6';
                form.style.pointerEvents = 'none';
                
                // Start countdown and redirect
                startCountdown();
            } else {
                showError(result.message);
            }
        } catch (error) {
            hideLoading();
            showError('An error occurred. Please try again.');
            console.error('Error:', error);
        }
    });

    // Password strength checker
    newPasswordInput.addEventListener('input', function() {
        checkPasswordStrength(this.value);
        checkPasswordMatch();
    });

    confirmPasswordInput.addEventListener('input', function() {
        checkPasswordMatch();
    });

    function checkPasswordStrength(password) {
        let strength = 0;
        let strengthLevel = '';
        let color = '';

        if (password.length >= 6) strength += 1;
        if (password.length >= 8) strength += 1;
        if (/[a-z]/.test(password)) strength += 1;
        if (/[A-Z]/.test(password)) strength += 1;
        if (/[0-9]/.test(password)) strength += 1;
        if (/[^A-Za-z0-9]/.test(password)) strength += 1;

        const percentage = (strength / 6) * 100;
        
        if (strength < 2) {
            strengthLevel = 'Very Weak';
            color = '#dc3545';
        } else if (strength < 4) {
            strengthLevel = 'Weak';
            color = '#fd7e14';
        } else if (strength < 5) {
            strengthLevel = 'Fair';
            color = '#ffc107';
        } else if (strength < 6) {
            strengthLevel = 'Good';
            color = '#20c997';
        } else {
            strengthLevel = 'Strong';
            color = '#28a745';
        }

        strengthBar.style.width = percentage + '%';
        strengthBar.style.backgroundColor = color;
        strengthText.textContent = strengthLevel;
        strengthText.style.color = color;
    }

    function checkPasswordMatch() {
        const newPassword = newPasswordInput.value;
        const confirmPassword = confirmPasswordInput.value;

        if (confirmPassword && newPassword !== confirmPassword) {
            confirmPasswordInput.style.borderColor = '#dc3545';
            confirmPasswordInput.style.backgroundColor = '#f8d7da';
        } else if (confirmPassword) {
            confirmPasswordInput.style.borderColor = '#28a745';
            confirmPasswordInput.style.backgroundColor = '#d4edda';
        } else {
            confirmPasswordInput.style.borderColor = '#ddd';
            confirmPasswordInput.style.backgroundColor = '#eee';
        }
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

    // Auto-focus on new password input
    newPasswordInput.focus();
    function startCountdown() {
        let countdown = 3;
        const countdownElement = document.getElementById('countdown');
        
        const updateCountdown = () => {
            if (countdownElement) {
                countdownElement.textContent = countdown;
            }
            countdown--;
            
            if (countdown < 0) {
                window.location.href = '/Account/Index';
            } else {
                setTimeout(updateCountdown, 1000);
            }
        };
        
        updateCountdown();
    }
});
