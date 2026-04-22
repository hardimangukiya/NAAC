using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NAAC.Models
{
    public class DataRecord
    {
        [Key]
        public int Id { get; set; }

        public int TableId { get; set; }
        [ForeignKey("TableId")]
        public NAACTable? Table { get; set; }

        public int FacultyId { get; set; }
        [ForeignKey("FacultyId")]
        public User? Faculty { get; set; }

        [Required]
        public string AcademicYear { get; set; } = string.Empty;

        // Stores the row data: {"col1": "value", "col2": 45, ...}
        [Required]
        public string JsonData { get; set; } = "{}";

        [Required]
        public string Status { get; set; } = "Draft"; // Draft, Submitted, Verified, Rejected

        public string? Remarks { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }
}
