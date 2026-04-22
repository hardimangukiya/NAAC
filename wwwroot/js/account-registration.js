document.addEventListener('DOMContentLoaded', function () {
    const registrationForm = document.getElementById('registrationForm');
    const registrationSection = document.getElementById('registration-section');
    const otpSection = document.getElementById('otp-section');
    const successSection = document.getElementById('success-section');
    const otpBoxes = document.querySelectorAll('.otp-box');
    const verifyBtn = document.getElementById('verifyBtn');
    const otpError = document.getElementById('otp-error');

    // 1. Password Visibility Toggle
    const passwordToggles = document.querySelectorAll('.password-toggle');
    passwordToggles.forEach(toggle => {
        toggle.addEventListener('click', function () {
            const input = this.parentElement.querySelector('input');
            if (input) {
                const type = input.getAttribute('type') === 'password' ? 'text' : 'password';
                input.setAttribute('type', type);
                this.classList.toggle('bi-eye');
                this.classList.toggle('bi-eye-slash');
            }
        });
    });

    // 2. Mobile Number Validation (Digits only)
    const mobileInput = document.querySelector('input[name="MobileNumber"]');
    if (mobileInput) {
        mobileInput.addEventListener('input', function (e) {
            this.value = this.value.replace(/[^0-9]/g, '');
        });
    }

    // 3. Form Submission (Step 1 -> Step 2)
    if (registrationForm) {
        registrationForm.addEventListener('submit', function (e) {
            e.preventDefault();
            
            // Check if form is valid using jQuery validation (ASP.NET default)
            if ($(registrationForm).valid()) {
                const formData = new FormData(registrationForm);
                
                // Simulate server call
                fetch(registrationForm.action, {
                    method: 'POST',
                    body: formData,
                    headers: {
                        'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                    }
                }).then(response => response.json())
                .then(data => {
                    if (data.success) {
                        // Switch to OTP section
                        registrationSection.style.display = 'none';
                        otpSection.style.display = 'block';
                        otpBoxes[0].focus();
                    }
                });
            }
        });
    }

    // 4. OTP Input Logic (Auto-tabbing)
    otpBoxes.forEach((box, index) => {
        box.addEventListener('input', (e) => {
            if (e.target.value.length === 1 && index < otpBoxes.length - 1) {
                otpBoxes[index + 1].focus();
            }
        });

        box.addEventListener('keydown', (e) => {
            if (e.key === 'Backspace' && !e.target.value && index > 0) {
                otpBoxes[index - 1].focus();
            }
        });
    });

    // 5. OTP Verification (Step 2 -> Step 3)
    if (verifyBtn) {
        verifyBtn.addEventListener('click', function () {
            let otpCode = "";
            otpBoxes.forEach(box => otpCode += box.value);

            if (otpCode.length === 6) {
                // Simulate server verification
                fetch('/Account/VerifyOTP', {
                    method: 'POST',
                    body: new URLSearchParams({ 'otp': otpCode }),
                    headers: {
                        'Content-Type': 'application/x-www-form-urlencoded'
                    }
                }).then(response => response.json())
                .then(data => {
                    if (data.success) {
                        otpSection.style.display = 'none';
                        successSection.style.display = 'block';
                    } else {
                        otpError.style.display = 'block';
                        otpBoxes.forEach(box => box.value = "");
                        otpBoxes[0].focus();
                    }
                });
            }
        });
    }

    // 6. Resend OTP Simulation
    const resendLink = document.getElementById('resendOtp');
    if (resendLink) {
        resendLink.addEventListener('click', function (e) {
            e.preventDefault();
            alert("A new OTP has been sent to your email!");
            otpBoxes.forEach(box => box.value = "");
            otpBoxes[0].focus();
            otpError.style.display = 'none';
        });
    }
});
