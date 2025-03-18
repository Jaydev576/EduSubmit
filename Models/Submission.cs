using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduSubmit.Models
{
    public class Submission
    {
        [Key, Column(Order = 1)]
        [ForeignKey("Student")]
        public int StudentId { get; set; }
        public Student? Student { get; set; }

        [Key, Column(Order = 2)]
        [ForeignKey("Assignment")]
        public int AssignmentId { get; set; }
        public Assignment? Assignment { get; set;}

        [Required, DataType(DataType.Date)]
        public DateTime SubmissionDate { get; set; }

        [Required]
        public string FilePath { get; set; }

        [ForeignKey("Class")]
        public int ClassId { get; set; }
        public Class? Class { get; set; }
    }
}