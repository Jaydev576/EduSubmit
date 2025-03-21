using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduSubmit.Models
{
    public class Instructor
    {
        [Key]
        public int InstructorId { get; set; }

        [Required, StringLength(50, ErrorMessage = "First name must be less than 50 characters long.")]
        public string FirstName { get; set; }

        [Required, StringLength(50, ErrorMessage = "Last name must be less than 50 characters long.")]
        public string LastName { get; set; }

        [Required, EmailAddress]
        public string EmailAddress { get; set; }

        [Required, DataType(DataType.Password)]
        public string Password { get; set; }

        [ForeignKey("Organization")]
        public int OrganizationId { get; set; }
        public Organization? Organization { get; set; }

        // Navigation property
        public ICollection<Grade>? Grades { get; set; }
        public ICollection<Assignment>? Assignments { get; set; }
    }
}