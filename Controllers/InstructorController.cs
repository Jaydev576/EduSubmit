using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EduSubmit.Data;
using EduSubmit.Models;
using Microsoft.AspNetCore.Authorization;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;
using Amazon.S3.Model;
using System.Net;

namespace EduSubmit.Controllers
{
    [Authorize(Roles = "Instructor")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class InstructorController : Controller
    {
        private readonly AppDbContext _context;
        private readonly string supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL");
        private readonly string supabaseServiceKey = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_KEY");
        private readonly string supabaseBucket = "edusubmit";

        public InstructorController(AppDbContext context)
        {
            _context = context;
        }


        // GET: Instructors/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var instructor = await _context.Instructors
                .Include(i => i.Organization)
                .FirstOrDefaultAsync(m => m.InstructorId == id);
            if (instructor == null)
            {
                return NotFound();
            }

            return View(instructor);
        }

        // GET: Instructors/Create
        public IActionResult Create()
        {
            ViewData["OrganizationId"] = new SelectList(_context.Organizations, "OrganizationId", "OrganizationName");
            return View();
        }

        // POST: Instructors/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("InstructorId,FirstName,LastName,EmailAddress,Password,OrganizationId")] Instructor instructor)
        {
            if (ModelState.IsValid)
            {
                _context.Add(instructor);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["OrganizationId"] = new SelectList(_context.Organizations, "OrganizationId", "OrganizationName", instructor.OrganizationId);
            return View(instructor);
        }

        // GET: Instructors/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var instructor = await _context.Instructors.FindAsync(id);
            if (instructor == null)
            {
                return NotFound();
            }
            ViewData["OrganizationId"] = new SelectList(_context.Organizations, "OrganizationId", "OrganizationName", instructor.OrganizationId);
            return View(instructor);
        }

        // POST: Instructors/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("InstructorId,FirstName,LastName,EmailAddress,Password,OrganizationId")] Instructor instructor)
        {
            if (id != instructor.InstructorId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(instructor);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!InstructorExists(instructor.InstructorId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["OrganizationId"] = new SelectList(_context.Organizations, "OrganizationId", "OrganizationName", instructor.OrganizationId);
            return View(instructor);
        }


        private bool InstructorExists(int id)
        {
            return _context.Instructors.Any(e => e.InstructorId == id);
        }



        // index method of dashboard

        // Instructor Dashboard
        public IActionResult Index()
        {
            string loggedInEmail = User.Identity.Name;

            var instructor = _context.Instructors
                                     .FirstOrDefault(i => i.EmailAddress == loggedInEmail);

            if (instructor == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int instructorId = instructor.InstructorId;

            // Fetch only classes that have assignments created by this instructor
            var instructorClasses = _context.Classes
                .Where(c => _context.Assignments.Any(a => a.InstructorId == instructorId && a.ClassId == c.ClassId))
                .ToList();

            // Fetch all assignments (past + future) for performance calculation
            var allAssignments = _context.Assignments
                .Where(a => a.InstructorId == instructorId)
                .ToList();

            // Fetch only future assignments for the "Active Assignments" section
            var upcomingAssignments = allAssignments
                .Where(a => a.DueDate > DateTime.Now)
                .ToList();

            var assignmentIds = allAssignments.Select(a => a.AssignmentId).ToList();

            var grades = _context.Grades
                .Include(g => g.Assignment)
                .Include(g => g.Student)
                .Where(g => assignmentIds.Contains(g.AssignmentId))
                .ToList();

            // ✅ Get the logged-in student (to calculate personalized average grade)
            var student = _context.Students.FirstOrDefault(s => s.EmailAddress == loggedInEmail);
            int studentId = student?.StudentId ?? -1;

            var classPerformance = instructorClasses.ToDictionary(
                c => c.ClassName,
                c =>
                {
                    // Total assignments should include past & future
                    int totalAssignments = allAssignments.Count(a => a.ClassId == c.ClassId);

                    var classAssignmentIds = allAssignments
                        .Where(a => a.ClassId == c.ClassId)
                        .Select(a => a.AssignmentId)
                        .ToList();

                    // ✅ Only consider grades of the logged-in student for average grade
                    var studentGrades = grades
                        .Where(g => classAssignmentIds.Contains(g.AssignmentId) &&
                                    g.Student.StudentId == studentId)
                        .ToList();

                    double avgGrade = 0;

                    if (studentGrades.Any())
                    {
                        double totalScore = studentGrades.Sum(g => g.Score);
                        double totalMaxScore = studentGrades.Sum(g => g.Assignment.Points);

                        avgGrade = totalMaxScore > 0
                            ? Math.Round((totalScore / totalMaxScore) * 100, 2)
                            : 0;
                    }

                    int totalStudents = _context.Students.Count(s => s.ClassId == c.ClassId);

                    int totalSubmissions = _context.Submissions
                        .Where(s => classAssignmentIds.Contains(s.AssignmentId))
                        .Count();

                    int expectedSubmissions = (totalStudents > 0 && totalAssignments > 0)
                        ? totalStudents * totalAssignments
                        : 1; // Prevent division by zero

                    double submissionRate = Math.Round(((double)totalSubmissions / expectedSubmissions) * 100, 2);

                    return new Tuple<int, double, int, int>(totalAssignments, avgGrade, totalStudents, (int)submissionRate);
                }
            );

            ViewData["ClassPerformance"] = classPerformance;

            // Ensure activeAssignments is always initialized
            var activeAssignments = instructorClasses.ToDictionary(
                c => c.ClassName,
                c =>
                {
                    var classAssignments = upcomingAssignments
                        .Where(a => a.ClassId == c.ClassId)
                        .Select(a => new
                        {
                            a.AssignmentId,
                            a.Title,
                            a.DueDate,
                            SubmissionCount = _context.Submissions.Count(s => s.AssignmentId == a.AssignmentId)
                        })
                        .ToList<dynamic>();

                    return classAssignments;
                }
            );

            ViewData["ActiveAssignments"] = activeAssignments ?? new Dictionary<string, List<dynamic>>();

            return View("Index");
        }


        // Create Assignment (GET)
        public async Task<IActionResult> CreateAssignment(bool isCoding = false)
        {
            var userEmail = User.Identity?.Name;
            var instructor = await _context.Instructors
                .Include(i => i.Organization) // Load Organization details
                .FirstOrDefaultAsync(i => i.EmailAddress == userEmail);

            if (instructor == null)
            {
                return Unauthorized("Instructor profile not found.");
            }

            ViewBag.IsCodingAssignment = isCoding; // Pass flag to view

            // Get classes that belong to the instructor's organization
            var classes = await _context.Classes
                .Where(c => c.OrganizationId == instructor.OrganizationId)
                .ToListAsync();

            ViewBag.InstructorId = instructor.InstructorId;
            ViewBag.OrganizationId = instructor.OrganizationId;
            ViewBag.Classes = new SelectList(classes, "ClassId", "ClassName"); // Pass filtered classes

            return View();
        }



        // Create Assignment (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAssignment(
            Assignment assignment,
            bool isCoding,
            string? ProgrammingLanguage,
            string? TestCases,
            IFormFile? SampleSolution)
        {
            // Get Instructor
            var userEmail = User.Identity?.Name;

            var instructor = await _context.Instructors
                .Include(i => i.Organization)
                .FirstOrDefaultAsync(i => i.EmailAddress == userEmail);

            if (instructor == null)
            {
                return Unauthorized(new { message = "Instructor profile not found." });
            }
            Console.WriteLine(instructor.InstructorId);

            // Assign InstructorId
            assignment.InstructorId = instructor.InstructorId;

            // Validate Non-Coding Assignments
            if (!isCoding)
            {
                ModelState.Remove("ProgrammingLanguage");
                ModelState.Remove("TestCases");
                ModelState.Remove("SampleSolution");
            }

            // Validate Coding Assignments
            if (isCoding)
            {
                if (string.IsNullOrWhiteSpace(ProgrammingLanguage))
                {
                    ModelState.AddModelError("ProgrammingLanguage", "Programming language is required for coding assignments.");
                }
                if (string.IsNullOrWhiteSpace(TestCases))
                {
                    ModelState.AddModelError("TestCases", "Test cases are required for coding assignments.");
                }
                if (SampleSolution == null || SampleSolution.Length == 0)
                {
                    ModelState.AddModelError("SampleSolution", "Sample solution file is required for coding assignments.");
                }
            }

            // Validate Common Fields
            if (string.IsNullOrWhiteSpace(assignment.Title) ||
                string.IsNullOrWhiteSpace(assignment.Description) ||
                assignment.DueDate == default ||
                assignment.Points <= 0 ||
                string.IsNullOrWhiteSpace(assignment.SubjectName) ||
                assignment.ClassId == 0)
            {
                ModelState.AddModelError(string.Empty, "All fields are required.");
            }

            // Check ModelState before processing
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(new { message = "Validation failed", errors });
            }

            // Save Assignment
            _context.Assignments.Add(assignment);
            await _context.SaveChangesAsync();

            // Handle Coding Assignment Files
            if (isCoding)
            {
                await SaveCodingAssignmentFiles(assignment.AssignmentId, assignment.ClassId, ProgrammingLanguage, TestCases, SampleSolution);
            }

            return RedirectToAction("Assignments");
        }

        // Helper function to handle file storage

        private async Task SaveCodingAssignmentFiles(int assignmentId, int classId, string programmingLanguage, string testCases, IFormFile sampleSolution)
        {
            string baseFolder = $"CodingAssignments/{classId}_{assignmentId}";

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", supabaseServiceKey);

            // 1. Upload TestCases.json
            if (!string.IsNullOrWhiteSpace(testCases))
            {
                string[] testCasesArray = testCases.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                                                   .Select(tc => tc.Trim())
                                                   .ToArray();

                string jsonContent = JsonConvert.SerializeObject(testCasesArray, Formatting.Indented);
                var bytes = Encoding.UTF8.GetBytes(jsonContent);
                var content = new ByteArrayContent(bytes);

                // Use "application/json" if it's in allowed MIME types
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                string objectPath = $"{baseFolder}/TestCases.json";
                await UploadFileToSupabaseWithFallback(httpClient, objectPath, content);
            }

            // 2. Upload ProgrammingLanguage.txt
            if (!string.IsNullOrWhiteSpace(programmingLanguage))
            {
                var langBytes = Encoding.UTF8.GetBytes(programmingLanguage);
                var langContent = new ByteArrayContent(langBytes);
                langContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

                string objectPath = $"{baseFolder}/ProgrammingLanguage.txt";
                await UploadFileToSupabaseWithFallback(httpClient, objectPath, langContent);
            }

            // 3. Upload SampleSolution
            if (sampleSolution != null && sampleSolution.Length > 0)
            {
                string extension = GetFileExtension(programmingLanguage);
                string objectPath = $"{baseFolder}/SampleSolution{extension}";

                using var stream = sampleSolution.OpenReadStream();
                var byteContent = new StreamContent(stream);

                // Assign appropriate content type if known, fallback to octet-stream
                string mimeType = GetMimeTypeByExtension(extension);
                byteContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType ?? "application/octet-stream");

                await UploadFileToSupabaseWithFallback(httpClient, objectPath, byteContent);
            }
        }

        private string GetMimeTypeByExtension(string extension)
        {
            return extension.ToLower() switch
            {
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                ".py" => "text/x-python",
                ".java" => "text/x-java-source",
                ".cpp" => "text/x-c++",
                ".cs" => "text/plain", // C# does not have an official MIME type; text/plain is a safe default
                _ => "application/octet-stream"
            };
        }

        private async Task UploadFileToSupabaseWithFallback(HttpClient httpClient, string objectPath, HttpContent content)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"{supabaseUrl}/storage/v1/object/{supabaseBucket}/{objectPath}")
            {
                Content = content
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supabaseServiceKey);
            request.Headers.Add("x-upsert", "true");

            var response = await httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
                return;

            // Fallback if 415 Unsupported Media Type
            if (response.StatusCode == HttpStatusCode.UnsupportedMediaType)
            {
                Console.WriteLine("Unsupported MIME type. Retrying with fallback...");

                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                request = new HttpRequestMessage(HttpMethod.Put, $"{supabaseUrl}/storage/v1/object/{supabaseBucket}/{objectPath}")
                {
                    Content = content
                };

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supabaseServiceKey);
                request.Headers.Add("x-upsert", "true");

                response = await httpClient.SendAsync(request);
            }

            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Upload failed ({objectPath}): {error}");
            }
        }

        // Function to determine the file extension based on programming language
        private string GetFileExtension(string programmingLanguage)
        {
            return programmingLanguage.ToLower() switch
            {
                "c++" => ".cpp",
                "java" => ".java",
                "python" => ".py",
                "c#" => ".cs",
                _ => ".txt"
            };
        }

        //Edit Assignment (GET)
        public async Task<IActionResult> EditAssignment(int id)
        {
            var userEmail = User.Identity?.Name;
            var instructor = await _context.Instructors
                .Include(i => i.Organization)
                .FirstOrDefaultAsync(i => i.EmailAddress == userEmail);

            if (instructor == null)
            {
                return Unauthorized("Instructor profile not found.");
            }

            var assignment = await _context.Assignments.FindAsync(id);
            if (assignment == null) return NotFound();

            if (assignment.InstructorId != instructor.InstructorId)
            {
                return Unauthorized("You are not allowed to edit this assignment.");
            }

            // Fetch classes from the same organization as the instructor
            var classes = await _context.Classes
                .Where(c => c.OrganizationId == instructor.OrganizationId)
                .ToListAsync();

            ViewBag.Classes = new SelectList(classes, "ClassId", "ClassName", assignment.ClassId);
            return View(assignment);
        }



        //Edit Assignment (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAssignment(int id, Assignment assignment)
        {
            if (id != assignment.AssignmentId) return NotFound();

            var userEmail = User.Identity?.Name;
            var instructor = await _context.Instructors
                .Include(i => i.Organization)
                .FirstOrDefaultAsync(i => i.EmailAddress == userEmail);

            if (instructor == null)
            {
                return Unauthorized("Instructor profile not found.");
            }

            var existingAssignment = await _context.Assignments.FindAsync(id);
            if (existingAssignment == null) return NotFound();

            if (existingAssignment.InstructorId != instructor.InstructorId)
            {
                return Unauthorized("You are not allowed to edit this assignment.");
            }

            if (ModelState.IsValid)
            {
                assignment.InstructorId = existingAssignment.InstructorId;

                _context.Entry(existingAssignment).CurrentValues.SetValues(assignment);
                await _context.SaveChangesAsync();
                return RedirectToAction("Assignments");
            }

            // Fetch classes from the same organization as the instructor
            var classes = await _context.Classes
                .Where(c => c.OrganizationId == instructor.OrganizationId)
                .ToListAsync();

            ViewBag.Classes = new SelectList(classes, "ClassId", "ClassName", assignment.ClassId);
            return View(assignment);
        }


        //Delete Assignment (GET Confirmation)
        [HttpGet]
        public async Task<IActionResult> DeleteAssignment(int id)
        {
            var userEmail = User.Identity?.Name;
            var instructor = await _context.Instructors.FirstOrDefaultAsync(i => i.EmailAddress == userEmail);

            if (instructor == null)
            {
                return Unauthorized("Instructor profile not found.");
            }

            var assignment = await _context.Assignments
                .Include(a => a.Class)
                .Include(a => a.Submissions) // Include submissions to check if it has student submissions
                .FirstOrDefaultAsync(a => a.AssignmentId == id);

            if (assignment == null) return NotFound();

            // Ensure that only the instructor who created this assignment can delete it
            if (assignment.InstructorId != instructor.InstructorId)
            {
                return Unauthorized("You are not allowed to delete this assignment.");
            }

            // Prevent deletion if there are submissions
            if (assignment.Submissions != null && assignment.Submissions.Count > 0)
            {
                return BadRequest("Cannot delete this assignment as students have already submitted work.");
            }

            return View(assignment);
        }

        //post
        [HttpPost, ActionName("DeleteAssignment")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userEmail = User.Identity?.Name;
            var instructor = await _context.Instructors.FirstOrDefaultAsync(i => i.EmailAddress == userEmail);

            if (instructor == null)
            {
                return Unauthorized("Instructor profile not found.");
            }

            var assignment = await _context.Assignments
                .Include(a => a.Submissions) // Include submissions to prevent deletion if students have submitted
                .FirstOrDefaultAsync(a => a.AssignmentId == id);

            if (assignment == null) return NotFound();

            // Ensure that only the instructor who created this assignment can delete it
            if (assignment.InstructorId != instructor.InstructorId)
            {
                return Unauthorized("You are not allowed to delete this assignment.");
            }

            // Prevent deletion if there are submissions
            if (assignment.Submissions != null && assignment.Submissions.Count > 0)
            {
                return BadRequest("Cannot delete this assignment as students have already submitted work.");
            }

            _context.Assignments.Remove(assignment);
            await _context.SaveChangesAsync();

            return RedirectToAction("Assignments");
        }

        // List Assignments
        [HttpGet]
        public async Task<IActionResult> Assignments()
        {
            var userEmail = User.Identity?.Name;
            var instructor = await _context.Instructors.FirstOrDefaultAsync(i => i.EmailAddress == userEmail);

            if (instructor == null)
            {
                return Unauthorized("Instructor profile not found.");
            }

            // Get all assignments created by this instructor
            var assignments = await _context.Assignments
                .Include(a => a.Class)
                .Include(a => a.Submissions)
                .Where(a => a.InstructorId == instructor.InstructorId)
                .ToListAsync();

            // Load only classes for which this instructor has created assignments
            var instructorClasses = assignments
                .Where(a => a.Class != null)  // Ensure Class is not null
                .Select(a => a.Class)
                .Distinct()
                .ToList();

            ViewBag.Classes = new SelectList(instructorClasses, "ClassId", "ClassName");

            return View(assignments);
        }


        // ✅ Show all student submissions (Pending grading)
        public async Task<IActionResult> Submissions()
        {
            var userEmail = User.Identity?.Name;
            var instructor = await _context.Instructors
                .FirstOrDefaultAsync(i => i.EmailAddress == userEmail);

            if (instructor == null)
            {
                return Unauthorized("Instructor profile not found.");
            }

            var submissions = await _context.Submissions
                .Include(s => s.Student)
                .Include(s => s.Assignment)
                .Where(s => s.Assignment.InstructorId == instructor.InstructorId)
                .Where(s => !_context.Grades.Any(g => g.AssignmentId == s.AssignmentId && g.StudentId == s.StudentId)) // ✅ Exclude graded submissions
                .ToListAsync();

            return View(submissions);
        }


        // ✅ Show all graded submissions
        public async Task<IActionResult> GradedSubmissions()
        {
            var userEmail = User.Identity?.Name;
            var instructor = await _context.Instructors
                .FirstOrDefaultAsync(i => i.EmailAddress == userEmail);

            if (instructor == null)
            {
                return Unauthorized("Instructor profile not found.");
            }

            // ✅ Only fetch graded submissions for assignments created by the logged-in instructor
            var gradedSubmissions = await _context.Grades
                .Include(g => g.Student)
                .Include(g => g.Assignment)
                .Where(g => g.Assignment.InstructorId == instructor.InstructorId) // 🔹 Restrict graded assignments
                .Where(g => g.Score >= 0) // Fetch only graded assignments
                .ToListAsync();

            return View(gradedSubmissions);
        }


        // ✅ Instructor assigns a grade
        public async Task<IActionResult> GradeAssignment(int studentId, int assignmentId)
        {
            var userEmail = User.Identity?.Name;
            var instructor = await _context.Instructors.FirstOrDefaultAsync(i => i.EmailAddress == userEmail);

            if (instructor == null)
            {
                return Unauthorized("Instructor profile not found.");
            }

            var submission = await _context.Submissions
                .Include(s => s.Student)
                .Include(s => s.Assignment)
                .FirstOrDefaultAsync(s => s.StudentId == studentId && s.AssignmentId == assignmentId);

            if (submission == null || submission.Assignment.InstructorId != instructor.InstructorId)
            {
                return Unauthorized("You can only grade assignments submitted for your own assignments.");
            }

            var grade = await _context.Grades
                .FirstOrDefaultAsync(g => g.StudentId == studentId && g.AssignmentId == assignmentId);

            if (grade == null)
            {
                grade = new Grade
                {
                    StudentId = studentId,
                    AssignmentId = assignmentId,
                    Score = 0,
                    Remarks = "No Remarks",
                    InstructorId = instructor.InstructorId
                };

                _context.Grades.Add(grade);
                await _context.SaveChangesAsync();
            }

            return View(grade);
        }


        // ✅ Save the assigned grade
        [HttpPost]
        public async Task<IActionResult> GradeAssignment(int studentId, int assignmentId, float score, string remarks)
        {
            var userEmail = User.Identity?.Name;
            var instructor = await _context.Instructors
                .FirstOrDefaultAsync(i => i.EmailAddress == userEmail);

            if (instructor == null)
            {
                return Unauthorized("Instructor profile not found.");
            }

            var grade = await _context.Grades
                .Include(g => g.Assignment)
                .FirstOrDefaultAsync(g => g.StudentId == studentId && g.AssignmentId == assignmentId);

            // ✅ Ensure the assignment belongs to the logged-in instructor
            if (grade == null || grade.Assignment.InstructorId != instructor.InstructorId)
            {
                return Unauthorized("You can only grade assignments submitted for your own assignments.");
            }

            // ✅ Prevent grading an assignment outside valid score range
            if (score < 0 || score > grade.Assignment.Points)
            {
                ModelState.AddModelError("Score", $"Score must be between 0 and {grade.Assignment.Points}.");
                return View(grade);
            }

            grade.Score = score;
            grade.Remarks = remarks;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(GradedSubmissions));
        }

        //view submission
        public async Task<IActionResult> ViewSubmission(int studentId, int assignmentId)
        {
            var submission = await _context.Submissions
                .Include(s => s.Student)
                .Include(s => s.Assignment)
                .FirstOrDefaultAsync(s => s.StudentId == studentId && s.AssignmentId == assignmentId);

            if (submission == null)
            {
                return NotFound("Submission not found.");
            }

            return View(submission); // Pass the submission to a new view
        }

        //student progress
        public async Task<IActionResult> StudentProgress(int? classId, string sortBy = "completion", bool filterLow = false)
        {
            string loggedInEmail = User.Identity.Name;

            var instructor = await _context.Instructors
                                           .FirstOrDefaultAsync(i => i.EmailAddress == loggedInEmail);

            if (instructor == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int instructorId = instructor.InstructorId;

            // Fetch class IDs from assignments
            var instructorClassIds = await _context.Assignments
                                                   .Where(a => a.InstructorId == instructorId)
                                                   .Select(a => a.ClassId)
                                                   .Distinct()
                                                   .ToListAsync();

            Console.WriteLine($"Instructor's Class IDs Count: {instructorClassIds.Count}");

            if (!instructorClassIds.Any())
            {
                ViewBag.Classes = new List<object>();
                return View(new List<Dictionary<string, object>>());
            }

            var studentQuery = _context.Students
                                       .Where(s => instructorClassIds.Contains(s.ClassId));

            if (classId.HasValue)
            {
                studentQuery = studentQuery.Where(s => s.ClassId == classId);
            }

            var totalAssignments = await _context.Assignments
                                                 .Where(a => a.InstructorId == instructorId)
                                                 .CountAsync();

            // Step 1: Fetch student list first
            var studentList = await studentQuery.ToListAsync();

            // Step 2: Compute progress data in memory
            var studentProgressList = studentList.Select(s =>
            {
                var studentAssignments = (
                    from g in _context.Grades
                    join a in _context.Assignments on g.AssignmentId equals a.AssignmentId
                    where g.StudentId == s.StudentId && a.InstructorId == instructorId && a.Points > 0
                    select ((double)g.Score / a.Points) * 100
                ).ToList(); // Bring to memory

                var averageGrade = studentAssignments.Any() ? studentAssignments.Average() : 0.0;

                return new Dictionary<string, object>
    {
        { "Id", s.StudentId },
        { "Name", s.FirstName + " " + s.LastName },
        { "ClassName", _context.Classes
            .Where(c => c.ClassId == s.ClassId)
            .Select(c => c.ClassName)
            .FirstOrDefault() ?? "Unknown" },

        { "LastActive", _context.Submissions
            .Where(sub => sub.StudentId == s.StudentId &&
                          _context.Assignments
                                  .Where(a => a.InstructorId == instructorId)
                                  .Select(a => a.AssignmentId)
                                  .Contains(sub.AssignmentId))
            .OrderByDescending(sub => sub.SubmissionDate)
            .Select(sub => (DateTime?)sub.SubmissionDate)
            .FirstOrDefault() ?? DateTime.MinValue },

        { "CompletedSubmissions", _context.Submissions
            .Count(sub => sub.StudentId == s.StudentId &&
                          _context.Assignments
                                  .Where(a => a.InstructorId == instructorId)
                                  .Select(a => a.AssignmentId)
                                  .Contains(sub.AssignmentId)) },

        { "SubmissionCompletionRate", totalAssignments > 0
            ? (double)(_context.Submissions
                .Count(sub => sub.StudentId == s.StudentId &&
                              _context.Assignments
                                      .Where(a => a.InstructorId == instructorId)
                                      .Select(a => a.AssignmentId)
                                      .Contains(sub.AssignmentId)) * 100) / totalAssignments
            : 0.0 },

        { "AverageGrade", averageGrade }
    };
            }).ToList();


            //if (filterLow)
            //{
            //    studentProgressList = studentProgressList.Where(s => (double)s["SubmissionCompletionRate"] < 50).ToList();
            //}

            //studentProgressList = sortBy switch
            //{
            //    "grade" => studentProgressList.OrderByDescending(s => (double)s["AverageGrade"]).ToList(),
            //    _ => studentProgressList.OrderByDescending(s => (double)s["SubmissionCompletionRate"]).ToList()
            //};


            var classList = await _context.Classes
                .Where(c => instructorClassIds.Contains(c.ClassId))
                .Select(c => new Dictionary<string, object>
                {
            { "ClassId", c.ClassId },
            { "ClassName", c.ClassName }
                })
                .ToListAsync();

            Console.WriteLine($"Total Classes Fetched: {classList.Count}");

            ViewBag.Classes = classList;
            ViewBag.SelectedClassId = classId;
            ViewBag.AverageGrade = studentProgressList.Any()
                ? studentProgressList.Average(s => (double)s["AverageGrade"])
                : 0;

            ViewBag.AverageSubmissionCompletion = studentProgressList.Any()
                ? studentProgressList.Average(s => (double)s["SubmissionCompletionRate"])
                : 0;

            return View(studentProgressList);
        }

        // Help & Support Page
        public IActionResult HelpSupport()
        {
            return View();
        }

        //Assignment Details
        public async Task<IActionResult> AssignmentDetails(int id)
        {
            var assignment = await _context.Assignments
                .Include(a => a.Class) // Include class details
                .Include(a => a.Submissions) // Include submissions
                .FirstOrDefaultAsync(a => a.AssignmentId == id);

            if (assignment == null)
            {
                return NotFound("Assignment not found.");
            }

            // Fetch total students in class
            int totalStudents = await _context.Students.CountAsync(s => s.ClassId == assignment.ClassId);

            // Count submitted & pending submissions
            int submittedCount = assignment.Submissions.Count();
            int pendingCount = totalStudents - submittedCount;

            ViewBag.TotalStudents = totalStudents;
            ViewBag.SubmittedCount = submittedCount;
            ViewBag.PendingCount = pendingCount;

            return View(assignment);
        }
    }
}
