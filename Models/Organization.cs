using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduSubmit.Models
{
    public class Organization
    {
        [Key]
        public int OrganizationId { get; set; }

        [Required, StringLength(75, ErrorMessage = "Organization name must be less than 75 characters long.")]
        public string OrganizationName { get; set; }

        [Required, StringLength(50, ErrorMessage = "Username name must be less than 50 characters long.")]
        public string Username { get; set; }

        [Required, DataType(DataType.Password)]
        public string Password { get; set; }

        // Navigation property
        public ICollection<Instructor>? Instructors { get; set; }
        public ICollection<Student>? Students { get; set; }
        public ICollection<Class>? Classes { get; set; }
    }
}