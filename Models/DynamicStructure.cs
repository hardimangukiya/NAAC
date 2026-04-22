using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NAAC.Models
{
    public class NAACTable
    {
        [Key]
        public int Id { get; set; }

        public int CriteriaId { get; set; }
        [ForeignKey("CriteriaId")]
        public Criteria? Criteria { get; set; }

        public string? TableNumber { get; set; } // e.g. 5.1.1(A)

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Relationship to columns
        public ICollection<TableColumn> Columns { get; set; } = new List<TableColumn>();
    }

    public class TableColumn
    {
        [Key]
        public int Id { get; set; }

        public int TableId { get; set; }
        [ForeignKey("TableId")]
        public NAACTable? Table { get; set; }

        public int? ParentColumnId { get; set; }
        [ForeignKey("ParentColumnId")]
        public TableColumn? ParentColumn { get; set; }

        [Required]
        public string HeaderName { get; set; } = string.Empty;

        [StringLength(250)]
        public string? Description { get; set; } // Short guidance

        [Required]
        public string DataType { get; set; } = "Text"; // Text, Number, Date, File

        public string? DropdownOptions { get; set; } // Comma-separated values for Dropdown type

        public int? DependsOnColumnId { get; set; } // ID of the column this depends on
        public string? DependsOnValue { get; set; } // The value that triggers visibility

        public int Order { get; set; }

        // Recursive children for hierarchical headers
        public ICollection<TableColumn> SubColumns { get; set; } = new List<TableColumn>();
    }
}
