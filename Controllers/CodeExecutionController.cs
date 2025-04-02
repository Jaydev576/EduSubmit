using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using EduSubmit.Models;
using EduSubmit.Services;

namespace EduSubmit.Controllers
{
    [ApiController]
    [Route("api/code-execution")]
    public class CodeExecutionController : ControllerBase
    {
        private readonly CodeExecutionService _codeExecutionService;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public CodeExecutionController(CodeExecutionService codeExecutionService, IWebHostEnvironment webHostEnvironment)
        {
            _codeExecutionService = codeExecutionService;
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpPost("execute")]
        public async Task<IActionResult> ExecuteCode([FromBody] CodeExecutionRequest request)
        {
            // Validate the request
            if (string.IsNullOrWhiteSpace(request.Language) || string.IsNullOrWhiteSpace(request.StudentCode) || request.TestCases == null || request.TestCases.Count == 0)
            {
                return BadRequest("Invalid request. Please provide language, code, and test cases.");
            }

            try
            {
                // Retrieve the driver code for the specified language
                string driverCode = await GetDriverCodeAsync(request.Language);
                if (string.IsNullOrWhiteSpace(driverCode))
                {
                    return BadRequest($"Driver code for language {request.Language} not found.");
                }

                // Merge the driver's code with the student's code
                string fullCode = driverCode + "\n" + request.StudentCode;

                // Execute the merged code with the test cases using local execution
                var executionResults = await _codeExecutionService.ExecuteWithDriver(request.Language, fullCode, request.TestCases);

                return Ok(executionResults);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, $"Internal Server Error: {ex.Message}");
            }
        }

        private async Task<string> GetDriverCodeAsync(string language)
        {
            // Determine the file path for the driver code based on the language
            string extension = language.ToLower() switch
            {
                "python" => ".py",
                "java" => ".java",
                "csharp" => ".cs",
                "cpp" => ".cpp",
                _ => throw new NotSupportedException($"Driver code for language {language} is not available.")
            };

            string driverFilePath = Path.Combine(_webHostEnvironment.WebRootPath, "CodingAssignments", "Drivers", $"Driver{extension}");

            // Read the driver code file
            if (System.IO.File.Exists(driverFilePath))
            {
                return await System.IO.File.ReadAllTextAsync(driverFilePath);
            }
            else
            {
                return null;
            }
        }
    }
}