using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EduSubmit.Data;
using EduSubmit.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using System.Security.Claims;
using Newtonsoft.Json;
using EduSubmit.Services;
using System.Text.RegularExpressions;

namespace EduSubmit.Controllers
{
    public class SubmissionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly CodeExecutionService _codeExecutionService;

        public SubmissionController(AppDbContext context, IWebHostEnvironment webHostEnvironment, CodeExecutionService codeExecutionService)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _codeExecutionService = codeExecutionService;
        }

        // GET: Submission
        public IActionResult Index()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email)
                          ?? User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");

            var student = _context.Students.FirstOrDefault(s => s.EmailAddress == userEmail);

            if (student == null)
            {
                return NotFound();
            }

            // 🔹 Fetch only submitted assignments for this student
            var submittedAssignmentIds = _context.Submissions
                                                 .Where(s => s.StudentId == student.StudentId)
                                                 .Select(s => s.AssignmentId)
                                                 .ToHashSet(); // Use HashSet for fast lookup

            var submittedAssignments = _context.Assignments
                                               .Include(a => a.Class)
                                               .Where(a => submittedAssignmentIds.Contains(a.AssignmentId))
                                               .ToList();

            return View(submittedAssignments);
        }




        // GET: Submission/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var submission = await _context.Submissions
                .Include(s => s.Assignment)
                .Include(s => s.Class)
                .Include(s => s.Student)
                .FirstOrDefaultAsync(m => m.StudentId == id);
            if (submission == null)
            {
                return NotFound();
            }

            return View(submission);
        }

        // GET: Submission/Create
        public IActionResult Create()
        {
            ViewData["AssignmentId"] = new SelectList(_context.Assignments, "AssignmentId", "Description");
            ViewData["ClassId"] = new SelectList(_context.Classes, "ClassId", "ClassName");
            ViewData["StudentId"] = new SelectList(_context.Students, "StudentId", "EmailAddress");
            return View();
        }

        // POST: Submission/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("StudentId,AssignmentId,SubmissionDate,FilePath,ClassId")] Submission submission)
        {
            if (ModelState.IsValid)
            {
                _context.Add(submission);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["AssignmentId"] = new SelectList(_context.Assignments, "AssignmentId", "Description", submission.AssignmentId);
            ViewData["ClassId"] = new SelectList(_context.Classes, "ClassId", "ClassName", submission.ClassId);
            ViewData["StudentId"] = new SelectList(_context.Students, "StudentId", "EmailAddress", submission.StudentId);
            return View(submission);
        }

        // GET: Submission/Edit
        public async Task<IActionResult> Edit(int assignmentId, int studentId)
        {
            var submission = await _context.Submissions
                .Include(s => s.Assignment)
                .Include(s => s.Student)
                .FirstOrDefaultAsync(s => s.AssignmentId == assignmentId && s.StudentId == studentId);

            if (submission == null)
            {
                return NotFound();
            }

            return View(submission);
        }

        // POST: Submission/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int assignmentId, int studentId, IFormFile newFile)
        {
            var existingSubmission = await _context.Submissions
                .Include(s => s.Assignment)
                .ThenInclude(a => a.Class)
                .FirstOrDefaultAsync(s => s.AssignmentId == assignmentId && s.StudentId == studentId);

            if (existingSubmission == null || existingSubmission.Assignment == null || existingSubmission.Assignment.Class == null)
            {
                return NotFound();
            }

            if (newFile != null && newFile.Length > 0)
            {
                string sanitizedClassName = string.Concat(existingSubmission.Assignment.Class.ClassName.Split(Path.GetInvalidFileNameChars()));
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", sanitizedClassName);

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // Delete the old file if it exists
                if (!string.IsNullOrEmpty(existingSubmission.FilePath))
                {
                    string oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath, existingSubmission.FilePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                // Generate a new unique filename
                string uniqueIdentifier = Guid.NewGuid().ToString("N");
                string originalFileName = Path.GetFileName(newFile.FileName);
                string newFileName = $"{uniqueIdentifier}_{studentId}_{assignmentId}_{originalFileName}";
                string newFilePath = Path.Combine(uploadsFolder, newFileName);

                // Save the new file
                using (var stream = new FileStream(newFilePath, FileMode.Create))
                {
                    await newFile.CopyToAsync(stream);
                }

                // Update file path and submission date
                existingSubmission.FilePath = $"/uploads/{sanitizedClassName}/{newFileName}";
                existingSubmission.SubmissionDate = DateTime.Now;
            }

            try
            {
                _context.Update(existingSubmission);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Submissions.Any(s => s.AssignmentId == assignmentId && s.StudentId == studentId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToAction("Submissions", "Student");
        }

        // Helper Method
        private bool SubmissionExists(int assignmentId, int studentId)
        {
            return _context.Submissions.Any(s => s.AssignmentId == assignmentId && s.StudentId == studentId);
        }




        // GET: Submission/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var submission = await _context.Submissions
                .Include(s => s.Assignment)
                .Include(s => s.Class)
                .Include(s => s.Student)
                .FirstOrDefaultAsync(m => m.StudentId == id);
            if (submission == null)
            {
                return NotFound();
            }

            return View(submission);
        }

        // POST: Submission/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var submission = await _context.Submissions.FindAsync(id);
            if (submission != null)
            {
                _context.Submissions.Remove(submission);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Submission/Submit/{id}
        [HttpGet]
        public async Task<IActionResult> Submit(int id)
        {
            var assignment = await _context.Assignments.FindAsync(id);
            if (assignment == null)
            {
                return NotFound();
            }

            string assignmentFolder = Path.Combine("wwwroot/CodingAssignments", $"{assignment.ClassId.ToString()}_{id.ToString()}");
            bool isCodingAssignment = Directory.Exists(assignmentFolder);

            ViewBag.IsCodingAssignment = isCodingAssignment;

            if (isCodingAssignment)
            {
                string testCasesPath = Path.Combine(assignmentFolder, "TestCases.json");
                string languagePath = Path.Combine(assignmentFolder, "ProgrammingLanguage.txt");

                if (System.IO.File.Exists(testCasesPath) && System.IO.File.Exists(languagePath))
                {
                    string testCasesJson = await System.IO.File.ReadAllTextAsync(testCasesPath);
                    ViewBag.ProgrammingLanguage = await System.IO.File.ReadAllTextAsync(languagePath);
                    ViewBag.TestCases = JsonConvert.DeserializeObject<List<string>>(testCasesJson);
                }
            }
            var submission = new Submission
            {
                AssignmentId = assignment.AssignmentId
            };

            return View(submission);
        }

        // POST: Submission/Submit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int assignmentId, IFormFile file)
        {
            var userEmail = User.Identity?.Name;
            var student = await _context.Students.FirstOrDefaultAsync(s => s.EmailAddress == userEmail);

            if (student == null)
            {
                return Unauthorized("Student profile not found.");
            }

            // 🔹 Define Paths
            string assignmentPath = Path.Combine(_webHostEnvironment.WebRootPath, "CodingAssignments", $"{student.ClassId}_{assignmentId}");
            string resultPath = Path.Combine(assignmentPath, "Results");
            string submissionPath = Path.Combine(assignmentPath, "Submissions");
            string teacherResultFile = Path.Combine(resultPath, "teacher_result.json");

            // 🔹 Ensure Directories Exist
            Directory.CreateDirectory(resultPath);
            Directory.CreateDirectory(submissionPath);

            // 🔹 Check if Coding Assignment
            bool isCodingAssignment = System.IO.File.Exists(Path.Combine(assignmentPath, "ProgrammingLanguage.txt"));
            if (!isCodingAssignment)
            {
                return await HandleNormalAssignmentSubmission(student.StudentId, assignmentId, file, submissionPath);
            }

            // 🔹 Retrieve Programming Language
            string languagePath = Path.Combine(assignmentPath, "ProgrammingLanguage.txt");
            if (!System.IO.File.Exists(languagePath))
            {
                return BadRequest("Programming language file not found.");
            }
            string programmingLanguage = await System.IO.File.ReadAllTextAsync(languagePath);

            // 🔹 Retrieve Test Cases
            string testCasesPath = Path.Combine(assignmentPath, "TestCases.json");
            if (!System.IO.File.Exists(testCasesPath))
            {
                return BadRequest("Test cases file not found.");
            }
            var testCaseStrings = JsonConvert.DeserializeObject<List<string>>(await System.IO.File.ReadAllTextAsync(testCasesPath));
            var testCases = testCaseStrings.Select(input => new TestCase(input)).ToList();

            // 🔹 Check if Teacher's Sample Code is Executed
            if (!System.IO.File.Exists(teacherResultFile))
            {
                string teacherCodePath = Path.Combine(assignmentPath, $"SampleSolution{GetFileExtension(programmingLanguage)}");
                if (!System.IO.File.Exists(teacherCodePath))
                {
                    return BadRequest("Teacher's sample code not found.");
                }

                string teacherCode = await System.IO.File.ReadAllTextAsync(teacherCodePath);
                Console.WriteLine(teacherCode);
                var teacherResult = await _codeExecutionService.ExecuteCodeAndFetchResults(programmingLanguage, teacherCode, testCases);
                await System.IO.File.WriteAllTextAsync(teacherResultFile, JsonConvert.SerializeObject(teacherResult));
            }

            // 🔹 Handle Student Submission
            return await HandleCodingAssignmentSubmission(student.StudentId, assignmentId, file, submissionPath, assignmentPath, programmingLanguage, testCases);
        }


        private async Task<IActionResult> HandleNormalAssignmentSubmission(int studentId, int assignmentId, IFormFile file, string submissionPath)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Invalid file upload.");
            }

            var assignment = await _context.Assignments
                                           .Include(a => a.Class)
                                           .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId);

            if (assignment == null || assignment.Class == null)
            {
                return BadRequest("Invalid assignment or class.");
            }

            bool submissionExists = await _context.Submissions
                .AnyAsync(s => s.AssignmentId == assignmentId && s.StudentId == studentId);

            if (submissionExists)
            {
                return BadRequest("You have already submitted this assignment.");
            }

            string uniqueIdentifier = Guid.NewGuid().ToString("N");
            string sanitizedClassName = string.Concat(assignment.Class.ClassName.Split(Path.GetInvalidFileNameChars()));
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", sanitizedClassName);

            Directory.CreateDirectory(uploadsFolder);

            string originalFileName = Path.GetFileName(file.FileName);
            string uniqueFileName = $"{uniqueIdentifier}_{studentId}_{assignmentId}_{originalFileName}";
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Save submission to the database
            var submission = new Submission
            {
                StudentId = studentId,
                AssignmentId = assignmentId,
                ClassId = assignment.Class.ClassId,
                FilePath = $"/uploads/{sanitizedClassName}/{uniqueFileName}",
                SubmissionDate = DateTime.Now
            };

            _context.Submissions.Add(submission);
            await _context.SaveChangesAsync();

            return RedirectToAction("Submissions", "Student");
        }


        private async Task<IActionResult> HandleCodingAssignmentSubmission(int studentId, int assignmentId, IFormFile file, string submissionPath, string assignmentPath, string programmingLanguage, List<TestCase> testCases)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Invalid file upload.");
            }

            var assignment = await _context.Assignments
                                           .Include(a => a.Class)
                                           .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId);

            if (assignment == null || assignment.Class == null)
            {
                return BadRequest("Invalid assignment or class.");
            }

            string extension = GetFileExtension(programmingLanguage);
            string studentFilePath = Path.Combine(submissionPath, $"student_{studentId}{extension}");

            using (var stream = new FileStream(studentFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 🔹 Read Student Code
            string studentCode = await System.IO.File.ReadAllTextAsync(studentFilePath);
            Console.WriteLine(studentCode);

            // 🔹 Execute code using Judge0
            var studentExecutionResults = await _codeExecutionService.ExecuteCodeAndFetchResults(programmingLanguage, studentCode, testCases);
            if (studentExecutionResults != null && studentExecutionResults.Count > 0)
            {
                foreach (var kvp in studentExecutionResults)
                {
                    Console.WriteLine($"Token: {kvp.Key}, Output: {kvp.Value}");
                }
            }
            else
            {
                Console.WriteLine("No results found in the execution results.");
            }

            // 🔹 Load Teacher's Results
            string resultPath = Path.Combine(assignmentPath, "Results");
            string teacherResultFile = Path.Combine(resultPath, "teacher_result.json");
            Dictionary<string, string> teacherExecutionResults = new();

            if (System.IO.File.Exists(teacherResultFile))
            {
                string teacherResultJson = await System.IO.File.ReadAllTextAsync(teacherResultFile);
                teacherExecutionResults = JsonConvert.DeserializeObject<Dictionary<string, string>>(teacherResultJson) ?? new Dictionary<string, string>();
            }
            else
            {
                return BadRequest("Teacher's result file not found.");
            }

            // 🔹 Compare Outputs
            var finalResults = new List<object>();
            foreach (var testCase in testCases)
            {
                string input = testCase.Input;
                string studentOutput = studentExecutionResults.ContainsKey(input) ? studentExecutionResults[input] : "N/A";
                string teacherOutput = teacherExecutionResults.ContainsKey(input) ? teacherExecutionResults[input] : "N/A";

                Console.WriteLine($"Input: {input}, Student Output: {studentOutput}, Teacher Output: {teacherOutput}");

                finalResults.Add(new
                {
                    Input = input,
                    StudentOutput = studentOutput,
                    TeacherOutput = teacherOutput,
                    IsCorrect = studentOutput.Trim() == teacherOutput.Trim()
                });
            }

            // 🔹 Save Student Results
            Directory.CreateDirectory(resultPath);
            string studentResultFile = Path.Combine(resultPath, $"student_{studentId}_result.json");
            await System.IO.File.WriteAllTextAsync(studentResultFile, JsonConvert.SerializeObject(finalResults));

            studentFilePath = studentFilePath.Replace(_webHostEnvironment.WebRootPath, "").Replace("\\", "/");

            // 🔹 Check if submission already exists
            var existingSubmission = await _context.Submissions.FirstOrDefaultAsync(s => s.AssignmentId == assignmentId && s.StudentId == studentId);
            if (existingSubmission != null)
            {
                existingSubmission.FilePath = studentFilePath;
                existingSubmission.SubmissionDate = DateTime.Now;
                _context.Submissions.Update(existingSubmission);
            }
            else
            {
                var submission = new Submission
                {
                    StudentId = studentId,
                    AssignmentId = assignmentId,
                    ClassId = assignment.Class.ClassId,
                    FilePath = studentFilePath,
                    SubmissionDate = DateTime.Now
                };
                _context.Submissions.Add(submission);
            }
            await _context.SaveChangesAsync();

            await AutoGradeAssignment(studentId, assignmentId, assignmentPath, assignment.InstructorId, assignment.Points);

            return RedirectToAction("Grades", "Student");
        }


        private async Task AutoGradeAssignment(int studentId, int assignmentId, string assignmentPath, int instructorId, int totalScore)
        {
            string resultPath = Path.Combine(assignmentPath, "Results");
            string studentResultFile = Path.Combine(resultPath, $"student_{studentId}_result.json");

            if (!System.IO.File.Exists(studentResultFile))
            {
                Console.WriteLine("[ERROR] Student result file not found!");
                return;
            }

            var studentResults = JsonConvert.DeserializeObject<List<dynamic>>(await System.IO.File.ReadAllTextAsync(studentResultFile));
            if (studentResults == null || studentResults.Count == 0)
            {
                Console.WriteLine("[ERROR] No test cases found in student result file!");
                return;
            }

            int totalTestCases = studentResults.Count;
            int passedTestCases = studentResults.Count(tc => tc.IsCorrect == true);
            float obtainedScore = ((float)passedTestCases / totalTestCases) * totalScore;
            float percentage = (obtainedScore / totalScore) * 100;

            string remarks = percentage switch
            {
                >= 90 => "Excellent performance!",
                >= 75 => "Great job! Keep it up!",
                >= 60 => "Satisfactory, but room for improvement.",
                >= 45 => "Needs improvement. Keep practicing!",
                _ => "Poor performance. Consider reviewing the concepts."
            };

            var existingGrade = await _context.Grades.FirstOrDefaultAsync(g => g.AssignmentId == assignmentId && g.StudentId == studentId);
            if (existingGrade != null)
            {
                existingGrade.Score = obtainedScore;
                existingGrade.Remarks = remarks;
                _context.Grades.Update(existingGrade);
            }
            else
            {
                var grade = new Grade
                {
                    StudentId = studentId,
                    AssignmentId = assignmentId,
                    Score = obtainedScore,
                    Remarks = remarks,
                    InstructorId = instructorId
                };
                _context.Grades.Add(grade);
            }
            await _context.SaveChangesAsync();
        }


        // Helper method to get file extension
        private string GetFileExtension(string language)
        {
            return language.ToLower() switch
            {
                "python" => ".py",
                "java" => ".java",
                "c++" => ".cpp",
                "c#" => ".cs",
                _ => ".txt"  // Default to .txt if unknown
            };
        }

    }
}
