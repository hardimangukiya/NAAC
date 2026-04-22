using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NAAC.Models
{
    public class CriteriaAssignment
    {
        public int Id { get; set; }

        [Required]
        public int CriteriaId { get; set; }

        [ForeignKey("CriteriaId")]
        public virtual Criteria? Criteria { get; set; }

        [Required]
        public int FacultyId { get; set; }

        [ForeignKey("FacultyId")]
        public virtual User? Faculty { get; set; }

        public DateTime AssignedDate { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true;
    }
}
