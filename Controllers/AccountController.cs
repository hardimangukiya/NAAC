using Microsoft.AspNetCore.Mvc;
using NAAC.Models;
using NAAC.Data;
using NAAC.Services;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using BCrypt.Net;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace NAAC.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailService _email;
        private readonly IWebHostEnvironment _environment;

        public AccountController(ApplicationDbContext db, IEmailService email, IWebHostEnvironment environment)
        {
            _db = db;
            _email = email;
            _environment = environment;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(HODRegistrationViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Check if user already exists
                if (await _db.Users.AnyAsync(u => u.Email == model.Email))
                {
                    return Json(new { success = false, message = "Email is already registered." });
                }

                // Generate 6-digit OTP
                string otpCode = new Random().Next(100000, 999999).ToString();

                // Save OTP to DB
                var otpEntry = new UserOTP
                {
                    Email = model.Email,
                    OTPCode = otpCode,
                    ExpiryTime = DateTime.UtcNow.AddMinutes(10),
                    IsUsed = false
                };
                _db.UserOTPs.Add(otpEntry);
                await _db.SaveChangesAsync();

                // Send OTP Email
                await _email.SendOTPAsync(model.Email, otpCode);

                // Store model in session temporarily
                HttpContext.Session.SetString("RegistrationData", JsonConvert.SerializeObject(model));

                return Json(new { success = true, message = "OTP sent successfully to your email." });
            }
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> VerifyOTP(string otp)
        {
            var regDataJson = HttpContext.Session.GetString("RegistrationData");
            if (string.IsNullOrEmpty(regDataJson))
            {
                return Json(new { success = false, message = "Session expired. Please restart registration." });
            }

            var model = JsonConvert.DeserializeObject<HODRegistrationViewModel>(regDataJson);
            if (model == null) return Json(new { success = false, message = "Invalid registration data." });

            var otpEntry = await _db.UserOTPs
                .Where(o => o.Email == model.Email && o.OTPCode == otp && !o.IsUsed && o.ExpiryTime > DateTime.UtcNow)
                .OrderByDescending(o => o.ExpiryTime)
                .FirstOrDefaultAsync();

            if (otpEntry != null)
            {
                otpEntry.IsUsed = true;
                _db.UserOTPs.Update(otpEntry);

                // Create User
                var user = new User
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    MobileNumber = model.MobileNumber,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                    CollegeName = model.CollegeName,
                    UniversityName = model.UniversityName,
                    Role = "HOD",
                    IsEmailVerified = true
                };

                _db.Users.Add(user);
                await _db.SaveChangesAsync();

                // Set Session for Dashboard
                HttpContext.Session.SetInt32("UserId", user.Id);
                HttpContext.Session.SetString("UserRole", user.Role);
                HttpContext.Session.SetString("UserName", user.FullName);
                HttpContext.Session.SetString("UserProfilePic", user.ProfilePicturePath ?? "");

                // Clear registration data
                HttpContext.Session.Remove("RegistrationData");

                return Json(new { success = true, message = "Registration Successful! Redirecting...", redirectUrl = $"/HOD/Dashboard" });
            }
            return Json(new { success = false, message = "Invalid or expired OTP." });
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, bool rememberMe)
        {
            try
            {
                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                {
                    return Json(new { success = false, message = "Email and Password are required" });
                }

                string normalizedEmail = email?.Trim().ToLower() ?? "";
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
                if (user != null && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                {
                    HttpContext.Session.SetInt32("UserId", user.Id);
                    HttpContext.Session.SetString("UserRole", user.Role);
                    HttpContext.Session.SetString("UserName", user.FullName);
                    HttpContext.Session.SetString("UserProfilePic", user.ProfilePicturePath ?? "");

                    string redirectUrl = user.Role.Equals("HOD", StringComparison.OrdinalIgnoreCase) ? "/HOD/Dashboard" : $"/{user.Role}/Dashboard";
                    return Json(new { success = true, role = user.Role, redirectUrl = redirectUrl });
                }

                return Json(new { success = false, message = "Invalid email or password" });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = "System Error: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> LoginWithOTP(string email)
        {
            if (string.IsNullOrEmpty(email)) return Json(new { success = false, message = "Email is required" });

            string normalizedEmail = email?.Trim().ToLower() ?? "";
            var userExists = await _db.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail);
            if (!userExists) return Json(new { success = false, message = "Email not found." });

            string otpCode = new Random().Next(100000, 999999).ToString();
            
            var otpEntry = new UserOTP
            {
                Email = normalizedEmail,
                OTPCode = otpCode,
                ExpiryTime = DateTime.UtcNow.AddMinutes(10),
                IsUsed = false
            };
            _db.UserOTPs.Add(otpEntry);
            await _db.SaveChangesAsync();

            await _email.SendOTPAsync(normalizedEmail, otpCode);

            return Json(new { success = true, message = "OTP sent to your registered email" });
        }

        [HttpPost]
        public async Task<IActionResult> VerifyLoginOTP(string email, string otp)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(otp))
            {
                return Json(new { success = false, message = "Email and OTP are required" });
            }

            string normalizedEmail = email.Trim().ToLower();
            var otpEntry = await _db.UserOTPs
                .Where(o => o.Email.ToLower() == normalizedEmail && o.OTPCode == otp && !o.IsUsed && o.ExpiryTime > DateTime.UtcNow)
                .OrderByDescending(o => o.ExpiryTime)
                .FirstOrDefaultAsync();

            if (otpEntry != null)
            {
                otpEntry.IsUsed = true;
                _db.UserOTPs.Update(otpEntry);
                await _db.SaveChangesAsync();

                var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
                if (user != null)
                {
                    // Set Session
                    HttpContext.Session.SetInt32("UserId", user.Id);
                    HttpContext.Session.SetString("UserRole", user.Role);
                    HttpContext.Session.SetString("UserName", user.FullName);
                    HttpContext.Session.SetString("UserProfilePic", user.ProfilePicturePath ?? "");

                    string redirectUrl = user.Role.Equals("HOD", StringComparison.OrdinalIgnoreCase) ? "/HOD/Dashboard" : $"/{user.Role}/Dashboard";
                    return Json(new { success = true, redirectUrl = redirectUrl });
                }
            }

            return Json(new { success = false, message = "Invalid or expired OTP." });
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return RedirectToAction("Login");

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfileDetails(string fullName, string mobileNumber, string collegeName, string universityName)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Json(new { success = false, message = "Session expired." });

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return Json(new { success = false, message = "User not found." });

            user.FullName = fullName;
            user.MobileNumber = mobileNumber;
            user.CollegeName = collegeName;
            user.UniversityName = universityName;

            _db.Users.Update(user);
            await _db.SaveChangesAsync();

            // Update Session
            HttpContext.Session.SetString("UserName", user.FullName);

            return Json(new { success = true, message = "Profile updated successfully!" });
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            if (HttpContext.Session.GetInt32("UserId") == null) return RedirectToAction("Login");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string oldPassword, string newPassword, string confirmPassword)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Json(new { success = false, message = "Session expired." });

            if (newPassword != confirmPassword) return Json(new { success = false, message = "Passwords do not match." });

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return Json(new { success = false, message = "User not found." });

            if (!BCrypt.Net.BCrypt.Verify(oldPassword, user.PasswordHash))
            {
                return Json(new { success = false, message = "Incorrect old password." });
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            _db.Users.Update(user);
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Password updated successfully!" });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfilePicture(IFormFile profilePic)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Json(new { success = false, message = "Session expired." });

            if (profilePic == null || profilePic.Length == 0) return Json(new { success = false, message = "No file selected." });

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return Json(new { success = false, message = "User not found." });

            // Ensure directory exists
            var uploads = Path.Combine(_environment.WebRootPath, "uploads", "profiles");
            if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);

            // Delete old pic if exists
            if (!string.IsNullOrEmpty(user.ProfilePicturePath))
            {
                var oldPath = Path.Combine(_environment.WebRootPath, user.ProfilePicturePath.TrimStart('/'));
                if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
            }

            // Save new pic
            var fileName = $"profile_{userId}_{DateTime.Now.Ticks}{Path.GetExtension(profilePic.FileName)}";
            var filePath = Path.Combine(uploads, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await profilePic.CopyToAsync(stream);
            }

            user.ProfilePicturePath = $"/uploads/profiles/{fileName}";
            _db.Users.Update(user);
            await _db.SaveChangesAsync();

            // Update Session
            HttpContext.Session.SetString("UserProfilePic", user.ProfilePicturePath);

            return Json(new { success = true, message = "Profile picture updated!", filePath = user.ProfilePicturePath });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProfilePicture()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Json(new { success = false, message = "Session expired." });

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return Json(new { success = false, message = "User not found." });

            if (!string.IsNullOrEmpty(user.ProfilePicturePath))
            {
                var fullPath = Path.Combine(_environment.WebRootPath, user.ProfilePicturePath.TrimStart('/'));
                if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
                
                user.ProfilePicturePath = null;
                _db.Users.Update(user);
                await _db.SaveChangesAsync();

                HttpContext.Session.SetString("UserProfilePic", "");
            }

            return Json(new { success = true, message = "Profile picture removed." });
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SendResetOTP(string email)
        {
            try {
                string normalizedEmail = email?.Trim().ToLower() ?? "";
                if (string.IsNullOrEmpty(normalizedEmail)) return Json(new { success = false, message = "Email is required" });

                var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
                if (user == null) return Json(new { success = false, message = "Email address not found in our system." });

                string otpCode = new Random().Next(100000, 999999).ToString();
                
                var otpEntry = new UserOTP
                {
                    Email = normalizedEmail,
                    OTPCode = otpCode,
                    ExpiryTime = DateTime.UtcNow.AddMinutes(10),
                    IsUsed = false
                };
                _db.UserOTPs.Add(otpEntry);
                await _db.SaveChangesAsync();

                // Attempt to send email
                await _email.SendOTPAsync(normalizedEmail, otpCode);

                // Store email in session to know who we're resetting for
                HttpContext.Session.SetString("ResetEmail", email);

                return Json(new { success = true, message = "Verification code sent to your email." });
            }
            catch (Exception ex) {
                // Return detailed error only if needed, otherwise generic
                return Json(new { success = false, message = "System Error: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> VerifyResetOTP(string otp)
        {
            var email = HttpContext.Session.GetString("ResetEmail");
            if (string.IsNullOrEmpty(email)) return Json(new { success = false, message = "Session expired. Please try again." });

            string normalizedEmail = email.Trim().ToLower();
            var otpEntry = await _db.UserOTPs
                .Where(o => o.Email.ToLower() == normalizedEmail && o.OTPCode == otp && !o.IsUsed && o.ExpiryTime > DateTime.UtcNow)
                .OrderByDescending(o => o.ExpiryTime)
                .FirstOrDefaultAsync();

            if (otpEntry != null)
            {
                otpEntry.IsUsed = true;
                _db.UserOTPs.Update(otpEntry);
                await _db.SaveChangesAsync();

                // Mark verification as complete in session
                HttpContext.Session.SetString("ResetVerified", "true");

                return Json(new { success = true, message = "Code verified successfully." });
            }

            return Json(new { success = false, message = "Invalid or expired code." });
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string newPassword, string confirmPassword)
        {
            var email = HttpContext.Session.GetString("ResetEmail");
            var verified = HttpContext.Session.GetString("ResetVerified");

            if (string.IsNullOrEmpty(email) || verified != "true")
            {
                return Json(new { success = false, message = "Unauthorized request or session expired." });
            }

            if (newPassword != confirmPassword) return Json(new { success = false, message = "Passwords do not match." });

            string normalizedEmail = email?.Trim().ToLower() ?? "";
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
            if (user == null) return Json(new { success = false, message = "User not found." });

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            _db.Users.Update(user);
            await _db.SaveChangesAsync();

            // Clear reset session
            HttpContext.Session.Remove("ResetEmail");
            HttpContext.Session.Remove("ResetVerified");

            return Json(new { success = true, message = "Password updated successfully! Redirecting to login..." });
        }

        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}



