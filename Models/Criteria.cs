using System.ComponentModel.DataAnnotations;

namespace NAAC.Models
{
    public class Criteria
    {
        public int Id { get; set; }

        [Required]
        public string Number { get; set; } = string.Empty; // e.g., 5.1.1

        [Required]
        public string Title { get; set; } = string.Empty; // e.g., Student Support

        public string? Description { get; set; }

        // Navigation property for structural tables
        public virtual ICollection<NAACTable> Tables { get; set; } = new List<NAACTable>();
    }
}
