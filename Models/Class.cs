using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduSubmit.Models
{
    public class Class
    {
        [Key]
        public int ClassId { get; set; }

        [Required, StringLength(50, ErrorMessage = "Class name must be less than 50 characters long.")]
        public string ClassName { get; set; }

        [ForeignKey("Organization")]
        public int OrganizationId { get; set; }
        public Organization? Organization { get; set; }

        // Navigation property
        public ICollection<Assignment>? Assignments { get; set; }
        public ICollection<Student>? Students { get; set; }
        public ICollection<Submission>? Submissions { get; set; }
    }
}