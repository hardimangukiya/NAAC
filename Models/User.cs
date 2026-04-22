using System;
using System.ComponentModel.DataAnnotations;

namespace NAAC.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string MobileNumber { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public string CollegeName { get; set; } = string.Empty;

        [Required]
        public string UniversityName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string Role { get; set; } = "HOD";
        public bool IsEmailVerified { get; set; } = false;
        public int? AddedByUserId { get; set; }

        public string? ProfilePicturePath { get; set; }
    }

    public class UserOTP
    {
        [Key]
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string OTPCode { get; set; } = string.Empty;
        public DateTime ExpiryTime { get; set; }
        public bool IsUsed { get; set; } = false;
    }
}
