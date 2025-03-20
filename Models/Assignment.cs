using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduSubmit.Models
{
    public class Assignment
    {
        [Key]
        public int AssignmentId { get; set; }

        [Required, StringLength(100, ErrorMessage = "Title must be less than 100 characters long.")]
        public string Title { get; set; }

        [Required, StringLength(1000, ErrorMessage = "Description must be less than 1000 characters long.")]
        public string Description { get; set; }

        [Required, DataType(DataType.Date)]
        public DateTime DueDate { get; set; }

        [Required]
        public int Points { get; set; }

        [Required, StringLength(50, ErrorMessage = "Subject name must be less than 50 characters long.")]
        public string SubjectName { get; set; }

        [ForeignKey("Class")]
        public int ClassId { get; set; }
        public Class? Class { get; set; }

        // Navigation property
        public ICollection<Grade>? Grades { get; set; }
        public ICollection<Submission>? Submissions { get; set; }


        // Add a status field to track submission
        public bool IsSubmitted { get; set; } = false;
    }
}