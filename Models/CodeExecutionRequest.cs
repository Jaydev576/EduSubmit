using System.Collections.Generic;

namespace EduSubmit.Models
{
    public class CodeExecutionRequest
    {
        public string Language { get; set; }
        public string StudentCode { get; set; }
        public List<TestCase> TestCases { get; set; }
    }
}