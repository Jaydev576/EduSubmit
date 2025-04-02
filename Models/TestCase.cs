namespace EduSubmit.Models
{
    public class TestCase
    {
        public string Input { get; set; } // Input for the test case

        public TestCase() { }

        public TestCase(string input)
        {
            Input = input;
        }
    }
}