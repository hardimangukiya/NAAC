document.addEventListener('DOMContentLoaded', function () {
    const loader = document.getElementById('auth-loader');
    const formBody = document.getElementById('form-body');
    const alertBox = document.getElementById('alert-box');

    const step1 = document.getElementById('step-1');
    const step2 = document.getElementById('step-2');
    const step3 = document.getElementById('step-3');

    const timerDisplay = document.getElementById('otp-timer');
    const resendBtn = document.getElementById('resend-otp');

    let timerInterval;
    let otpAttempts = 0;
    const MAX_ATTEMPTS = 5;

    // --- UTILITIES ---
    function showAlert(message, type = 'danger') {
        alertBox.textContent = message;
        alertBox.className = `alert alert-${type} mb-4 small`;
        alertBox.classList.remove('d-none');
        window.scrollTo(0, 0);
    }

    function hideAlert() {
        alertBox.classList.add('d-none');
    }

    function showLoader() {
        loader.style.display = 'block';
        formBody.style.opacity = '0.3';
        formBody.style.pointerEvents = 'none';
        hideAlert();
    }

    function hideLoader() {
        loader.style.display = 'none';
        formBody.style.opacity = '1';
        formBody.style.pointerEvents = 'all';
    }

    function startTimer(duration) {
        clearInterval(timerInterval);
        let timer = duration, minutes, seconds;
        resendBtn.classList.add('disabled');
        
        timerInterval = setInterval(function () {
            minutes = parseInt(timer / 60, 10);
            seconds = parseInt(timer % 60, 10);

            minutes = minutes < 10 ? "0" + minutes : minutes;
            seconds = seconds < 10 ? "0" + seconds : seconds;

            timerDisplay.textContent = minutes + ":" + seconds;

            if (--timer < 0) {
                clearInterval(timerInterval);
                timerDisplay.textContent = "00:00";
                resendBtn.classList.remove('disabled');
            }
        }, 1000);
    }

    // --- OTP BOX AUTO-TABBING ---
    const otpBoxes = document.querySelectorAll('.otp-box-reset');
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

    // --- STEP 1: SEND OTP ---
    document.getElementById('form-step-1').addEventListener('submit', function (e) {
        e.preventDefault();
        const email = document.getElementById('email-input').value;

        showLoader();

        fetch('/Account/SendResetOTP', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: `email=${encodeURIComponent(email)}`
        })
        .then(res => res.json())
        .then(data => {
            hideLoader();
            if (data.success) {
                step1.classList.add('d-none');
                step2.classList.remove('d-none');
                showAlert(data.message, 'success');
                startTimer(120); // 2 minutes
            } else {
                showAlert(data.message);
            }
        })
        .catch(() => {
            hideLoader();
            showAlert("Something went wrong. Please try again.");
        });
    });

    // --- STEP 2: VERIFY OTP ---
    document.getElementById('form-step-2').addEventListener('submit', function (e) {
        e.preventDefault();
        
        if (otpAttempts >= MAX_ATTEMPTS) {
            showAlert("Maximum attempts reached. Please try again later.");
            return;
        }

        const otp = Array.from(otpBoxes).map(box => box.value).join('');
        if (otp.length < 6) {
            showAlert("Please enter the full 6-digit code.");
            return;
        }

        showLoader();

        fetch('/Account/VerifyResetOTP', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: `otp=${encodeURIComponent(otp)}`
        })
        .then(res => res.json())
        .then(data => {
            hideLoader();
            if (data.success) {
                step2.classList.add('d-none');
                step3.classList.remove('d-none');
                hideAlert();
            } else {
                otpAttempts++;
                showAlert(`${data.message} (${MAX_ATTEMPTS - otpAttempts} attempts remaining)`);
            }
        })
        .catch(() => {
            hideLoader();
            showAlert("Connection error.");
        });
    });

    // --- STEP 3: RESET PASSWORD ---
    document.getElementById('form-step-3').addEventListener('submit', function (e) {
        e.preventDefault();
        const newPass = document.getElementById('new-password').value;
        const confirmPass = document.getElementById('confirm-password').value;

        if (newPass !== confirmPass) {
            showAlert("Passwords do not match.");
            return;
        }

        showLoader();

        fetch('/Account/ResetPassword', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: `newPassword=${encodeURIComponent(newPass)}&confirmPassword=${encodeURIComponent(confirmPass)}`
        })
        .then(res => res.json())
        .then(data => {
            hideLoader();
            if (data.success) {
                showAlert(data.message, 'success');
                step3.querySelector('button').disabled = true;
                setTimeout(() => {
                    window.location.href = '/Account/Login';
                }, 3000);
            } else {
                showAlert(data.message);
            }
        })
        .catch(() => {
            hideLoader();
            showAlert("Request failed.");
        });
    });

    // --- RESEND OTP ---
    resendBtn.addEventListener('click', function() {
        if (this.classList.contains('disabled')) return;
        
        const email = document.getElementById('email-input').value;
        showLoader();
        
        // Use the same endpoint
        fetch('/Account/SendResetOTP', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: `email=${encodeURIComponent(email)}`
        })
        .then(res => res.json())
        .then(data => {
            hideLoader();
            showAlert("A new code has been sent.", "success");
            otpAttempts = 0; // Reset attempts on resend
            startTimer(120);
        });
    });
});
