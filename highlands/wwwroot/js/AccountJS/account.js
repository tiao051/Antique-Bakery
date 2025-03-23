document.addEventListener('DOMContentLoaded', function () {
    const signUpButton = document.getElementById('signUp');
    const signInButton = document.getElementById('signIn');
    const container = document.getElementById('container');
    // Hiệu ứng chuyển đổi giữa Sign Up và Sign In
    signUpButton.addEventListener('click', () => {
        container.classList.add('right-panel-active');
    });
    signInButton.addEventListener('click', () => {
        container.classList.remove('right-panel-active');
    });
});

loginForm.addEventListener("submit", async function (e) {
    e.preventDefault();

    const formData = new FormData(this);
    const data = {};
    formData.forEach((value, key) => {
        data[key] = value;
    });

    try {
        const response = await fetch("/Account/Login", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(data),
        });

        if (!response.ok) {
            throw new Error("Login failed! Please check your credentials.");
        }

        const result = await response.json();
        localStorage.setItem("accessToken", result.accessToken);
        localStorage.setItem("refreshToken", result.refreshToken);

        const roleRedirects = {
            1: "/Admin/Index",
            2: "/Manager/Index",
            3: "/Customer/Index",
        };

        if (!result.roleId || !roleRedirects[result.roleId]) {
            console.error("❌ Invalid roleId:", result.roleId);
            alert("Login successful, but no valid role found!");
            return;
        }

        // Redirect ngay mà không cần thêm token vào header
        window.location.href = roleRedirects[result.roleId];

    } catch (error) {
        alert(error.message);
    }
});