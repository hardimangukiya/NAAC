using System.ComponentModel.DataAnnotations;

namespace NAAC.Models
{
    public class SystemSetting
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string SystemName { get; set; } = "NAAC Portal";

        [StringLength(255)]
        public string? SystemLogo { get; set; } = "/images/naac-logo.png";

        public string? InstitutionName { get; set; } = "National Assessment and Accreditation Council";

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
