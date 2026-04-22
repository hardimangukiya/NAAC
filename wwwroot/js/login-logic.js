document.addEventListener('DOMContentLoaded', function () {
    const loginForm = document.getElementById('loginForm');
    const otpLoginForm = document.getElementById('otpLoginForm');
    const verifyOtpLoginForm = document.getElementById('verifyOtpLoginForm');
    const loginLoader = document.getElementById('loginLoader');
    const loginContent = document.getElementById('login-form-content');
    const loginError = document.getElementById('loginError');
    const errorText = document.getElementById('errorText');
    
    const showOtpBtn = document.getElementById('showOtpLogin');
    const backToPassBtns = document.querySelectorAll('.backToPassword');
    const passwordSection = document.getElementById('password-login-section');
    const otpSection = document.getElementById('login-otp-section');
    const verifyOtpSection = document.getElementById('login-verify-otp-section');

    const toggleBtn = document.getElementById('toggleLoginPassword');
    const passwordInput = document.getElementById('loginPassword');

    const otpEmailInput = document.getElementById('otpEmailInput');
    const verifyOtpEmailHidden = document.getElementById('verifyOtpEmailHidden');
    const resendBtn = document.getElementById('resendLoginOtp');

    // 1. Password Toggle
    if (toggleBtn) {
        toggleBtn.addEventListener('click', function () {
            const type = passwordInput.getAttribute('type') === 'password' ? 'text' : 'password';
            passwordInput.setAttribute('type', type);
            this.classList.toggle('bi-eye');
            this.classList.toggle('bi-eye-slash');
        });
    }

    // 2. Navigation
    if (showOtpBtn) {
        showOtpBtn.addEventListener('click', () => {
            passwordSection.style.display = 'none';
            otpSection.style.display = 'block';
            verifyOtpSection.style.display = 'none';
            loginError.style.display = 'none';
        });
    }

    backToPassBtns.forEach(btn => {
        btn.addEventListener('click', () => {
            otpSection.style.display = 'none';
            verifyOtpSection.style.display = 'none';
            passwordSection.style.display = 'block';
            loginError.style.display = 'none';
        });
    });

    // 3. Password Login Submission
    if (loginForm) {
        loginForm.addEventListener('submit', function (e) {
            e.preventDefault();
            handleFormSubmission(loginForm);
        });
    }

    // 4. Request OTP Submission
    if (otpLoginForm) {
        otpLoginForm.addEventListener('submit', function (e) {
            e.preventDefault();
            
            loginLoader.style.display = 'block';
            loginContent.style.opacity = '0.3';
            loginError.style.display = 'none';

            const formData = new FormData(otpLoginForm);
            fetch(otpLoginForm.action, {
                method: 'POST',
                body: formData
            })
            .then(response => response.json())
            .then(data => {
                loginLoader.style.display = 'none';
                loginContent.style.opacity = '1';
                if (data.success) {
                    // Transition to Verification step
                    verifyOtpEmailHidden.value = otpEmailInput.value;
                    otpSection.style.display = 'none';
                    verifyOtpSection.style.display = 'block';
                    alert(data.message);
                } else {
                    errorText.innerText = data.message;
                    loginError.style.display = 'block';
                }
            })
            .catch(() => {
                loginLoader.style.display = 'none';
                loginContent.style.opacity = '1';
                errorText.innerText = "An unexpected error occurred.";
                loginError.style.display = 'block';
            });
        });
    }

    // 5. Verify OTP Submission
    if (verifyOtpLoginForm) {
        verifyOtpLoginForm.addEventListener('submit', function (e) {
            e.preventDefault();
            handleFormSubmission(verifyOtpLoginForm);
        });
    }

    // Resend Helper
    if (resendBtn) {
        resendBtn.addEventListener('click', () => {
            otpLoginForm.requestSubmit();
        });
    }

    // Unified fetch handler for final login steps
    function handleFormSubmission(form) {
        loginLoader.style.display = 'block';
        loginContent.style.opacity = '0.3';
        loginError.style.display = 'none';

        const formData = new FormData(form);
        fetch(form.action, {
            method: 'POST',
            body: formData
        })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                setTimeout(() => {
                    window.location.href = data.redirectUrl || '/';
                }, 500);
            } else {
                loginLoader.style.display = 'none';
                loginContent.style.opacity = '1';
                errorText.innerText = data.message;
                loginError.style.display = 'block';
            }
        })
        .catch(error => {
            loginLoader.style.display = 'none';
            loginContent.style.opacity = '1';
            errorText.innerText = "An unexpected error occurred.";
            loginError.style.display = 'block';
        });
    }
});
