using System.ComponentModel.DataAnnotations;

namespace NAAC.Models
{
    public class HODRegistrationViewModel
    {
        [Required(ErrorMessage = "Full Name is required")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email Address is required")]
        [EmailAddress(ErrorMessage = "Invalid Email Address")]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mobile Number is required")]
        [RegularExpression(@"^[0-9]{10}$", ErrorMessage = "Mobile Number must be exactly 10 digits")]
        [Display(Name = "Mobile Number")]
        public string MobileNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "College / Institute Name is required")]
        [Display(Name = "College / Institute Name")]
        public string CollegeName { get; set; } = string.Empty;

        [Required(ErrorMessage = "University Name is required")]
        [Display(Name = "University Name")]
        public string UniversityName { get; set; } = string.Empty;
    }
}
