using System;
using System.ComponentModel.DataAnnotations;

namespace NAAC.Models
{
    public class ActivityLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.Now;

        [Required]
        public string UserName { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = string.Empty;

        [Required]
        public string Action { get; set; } = string.Empty; // Added Record, Updated Record, etc.

        [Required]
        public string Module { get; set; } = string.Empty; // Data Entry, Criteria, etc.

        public string? Criteria { get; set; } // e.g., "5" or "6"
        
        public string? Table { get; set; } // e.g., "5.2.2"

        public string? Details { get; set; }

        [Required]
        public string Status { get; set; } = "Success"; // Success, Failed, Pending

        public int? UserId { get; set; }
        public string? CollegeName { get; set; }
    }
}
