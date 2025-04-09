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
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;

namespace EduSubmit.Controllers
{
    public class SubmissionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly CodeExecutionService _codeExecutionService;
        private readonly HttpClient _httpClient;
        private readonly string _supabaseUrl;
        private readonly string _supabaseKey;
        private readonly string _bucket;

        public SubmissionController(AppDbContext context, IWebHostEnvironment webHostEnvironment, CodeExecutionService codeExecutionService, HttpClient httpClient)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _codeExecutionService = codeExecutionService;
            _httpClient = httpClient;

            _supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL")!;
            _supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_KEY")!;
            _bucket = "edusubmit";
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
                return NotFound();

            if (newFile != null && newFile.Length > 0)
            {
                string className = existingSubmission.Assignment.Class.ClassName;
                string sanitizedClassName = string.Concat(className.Split(Path.GetInvalidFileNameChars()));
                int classId = existingSubmission.Assignment.ClassId;

                bool isCodingAssignment = await IsCodingAssignmentAsync(sanitizedClassName, assignmentId);

                string folderPrefix = isCodingAssignment
                    ? $"CodingAssignments/{classId}_{assignmentId}"
                    : $"NormalAssignments/{sanitizedClassName}";

                string extension = Path.GetExtension(newFile.FileName);
                string originalFileName = Path.GetFileName(newFile.FileName);
                string uniqueIdentifier = Guid.NewGuid().ToString("N");
                string newFileName = $"{uniqueIdentifier}_{studentId}_{assignmentId}_{originalFileName}";
                string supabasePath = $"{folderPrefix}/{newFileName}";

                if (isCodingAssignment)
                {
                    // 🔹 Delete old coding submission: student_{studentId}.{ext}
                    string oldSubmissionPath = $"{folderPrefix}/Submission/student_{studentId}{extension}";
                    await DeleteFileFromSupabase(oldSubmissionPath);

                    // 🔹 Upload new file to Submission folder
                    string codingSubmissionPath = $"{folderPrefix}/Submission/student_{studentId}{extension}";
                    using (var stream = newFile.OpenReadStream())
                    {
                        var uploadSuccess = await UploadFileToSupabase(codingSubmissionPath, stream, newFile.ContentType);
                        if (!uploadSuccess)
                            return BadRequest("Failed to upload coding assignment to Supabase.");
                    }

                    // 🔹 Delete old result file
                    string resultPath = $"{folderPrefix}/Result/student_{studentId}_result.json";
                    await DeleteFileFromSupabase(resultPath);

                    // 🔹 Generate and upload new result (example: placeholder content for now)
                    var resultData = new { status = "Pending", timestamp = DateTime.UtcNow };
                    string resultJson = JsonConvert.SerializeObject(resultData);
                    using (var resultStream = new MemoryStream(Encoding.UTF8.GetBytes(resultJson)))
                    {
                        var uploadSuccess = await UploadFileToSupabase(resultPath, resultStream, "application/json");
                        if (!uploadSuccess)
                            return BadRequest("Failed to upload result file to Supabase.");
                    }

                    // 🔹 Update FilePath to point to student's actual file in "Submission" folder
                    existingSubmission.FilePath = $"{_supabaseUrl}/storage/v1/object/public/edusubmit/{codingSubmissionPath}";
                }
                else
                {
                    // 🔹 Delete existing file(s) for normal assignment
                    await DeletePreviousMatchingFiles(folderPrefix, studentId, assignmentId);

                    // 🔹 Upload file to normal path
                    using (var stream = newFile.OpenReadStream())
                    {
                        var uploadSuccess = await UploadFileToSupabase(supabasePath, stream, newFile.ContentType);
                        if (!uploadSuccess)
                            return BadRequest("Failed to upload new file to Supabase.");
                    }

                    existingSubmission.FilePath = $"{_supabaseUrl}/storage/v1/object/public/edusubmit/{supabasePath}";
                }

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
                    return NotFound();
                throw;
            }

            return RedirectToAction("Submissions", "Student");
        }

        // Helper Method
        private async Task<bool> IsCodingAssignmentAsync(string className, int assignmentId)
        {
            string prefix = $"CodingAssignments/{className}_{assignmentId}/";
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_supabaseUrl}/storage/v1/object/list/edusubmit?prefix={prefix}&limit=1");
            request.Headers.Add("apikey", _supabaseKey);
            request.Headers.Add("Authorization", $"Bearer {_supabaseKey}");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return false;

            var content = await response.Content.ReadAsStringAsync();
            var files = JsonConvert.DeserializeObject<List<dynamic>>(content);
            return files != null && files.Count > 0;
        }

        private async Task DeleteFileFromSupabase(string filePath)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_supabaseUrl}/storage/v1/object/delete/edusubmit");
            request.Headers.Add("apikey", _supabaseKey);
            request.Headers.Add("Authorization", $"Bearer {_supabaseKey}");

            var payload = JsonConvert.SerializeObject(new[] { filePath });
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            await _httpClient.SendAsync(request);
        }


        private bool SubmissionExists(int assignmentId, int studentId)
        {
            return _context.Submissions.Any(s => s.AssignmentId == assignmentId && s.StudentId == studentId);
        }

        private async Task<bool> UploadFileToSupabase(string filePath, Stream fileStream, string contentType)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_supabaseUrl}/storage/v1/object/edusubmit/{filePath}")
            {
                Content = new StreamContent(fileStream)
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            request.Headers.Add("Authorization", $"Bearer {_supabaseKey}");

            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }

        private async Task DeletePreviousMatchingFiles(string folderPrefix, int studentId, int assignmentId)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_supabaseUrl}/storage/v1/object/list/edusubmit?prefix={folderPrefix}/");
            request.Headers.Add("apikey", _supabaseKey);
            request.Headers.Add("Authorization", $"Bearer {_supabaseKey}");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return;

            var content = await response.Content.ReadAsStringAsync();
            var files = JsonConvert.DeserializeObject<List<dynamic>>(content);

            var matchingFiles = files?
                .Where(f => f.name != null && f.name.ToString().Contains($"_{studentId}_{assignmentId}_"))
                .Select(f => $"{folderPrefix}/{f.name}")
                .ToList();

            if (matchingFiles != null && matchingFiles.Count > 0)
            {
                var deleteRequest = new HttpRequestMessage(HttpMethod.Post, $"{_supabaseUrl}/storage/v1/object/delete/edusubmit");
                deleteRequest.Headers.Add("apikey", _supabaseKey);
                deleteRequest.Headers.Add("Authorization", $"Bearer {_supabaseKey}");
                deleteRequest.Content = new StringContent(JsonConvert.SerializeObject(matchingFiles), Encoding.UTF8, "application/json");

                await _httpClient.SendAsync(deleteRequest);
            }
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
        public async Task<IActionResult> Submit(int id)
        {
            var assignment = await _context.Assignments.FindAsync(id);
            if (assignment == null)
                return NotFound();

            string folderPath = $"CodingAssignments/{assignment.ClassId}_{assignment.AssignmentId}";
            bool isCodingAssignment = await SupabaseFileExists($"{folderPath}/ProgrammingLanguage.txt");

            ViewBag.IsCodingAssignment = isCodingAssignment;

            if (isCodingAssignment)
            {
                string testCasesJson = await DownloadFileAsString($"{folderPath}/TestCases.json");
                string programmingLanguage = await DownloadFileAsString($"{folderPath}/ProgrammingLanguage.txt");

                ViewBag.TestCases = JsonConvert.DeserializeObject<List<string>>(testCasesJson);
                ViewBag.ProgrammingLanguage = programmingLanguage;
            }

            var submission = new Submission { AssignmentId = assignment.AssignmentId };
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
                return Unauthorized("Student profile not found.");

            string baseFolder = $"CodingAssignments/{student.ClassId}_{assignmentId}";
            string resultFolder = $"{baseFolder}/Results";
            string submissionFolder = $"{baseFolder}/Submissions";
            string teacherResultPath = $"{resultFolder}/teacher_result.json";

            // 🔍 Check if Coding Assignment
            if (!await SupabaseFileExists($"{baseFolder}/ProgrammingLanguage.txt"))
                return await HandleNormalAssignmentSubmission(student.StudentId, assignmentId, file);

            // 📁 Load metadata
            string programmingLanguage = await DownloadFileAsString($"{baseFolder}/ProgrammingLanguage.txt");
            string testCasesJson = await DownloadFileAsString($"{baseFolder}/TestCases.json");

            var testCaseStrings = JsonConvert.DeserializeObject<List<string>>(testCasesJson);
            var testCases = testCaseStrings.Select(tc => new TestCase(tc)).ToList();

            // 🧪 Run Teacher Code if Needed
            if (!await SupabaseFileExists(teacherResultPath))
            {
                string teacherCodePath = $"{baseFolder}/SampleSolution{GetFileExtension(programmingLanguage)}";
                if (!await SupabaseFileExists(teacherCodePath))
                    return BadRequest("Teacher's sample code not found.");

                string teacherCode = await DownloadFileAsString(teacherCodePath);
                var teacherResult = await _codeExecutionService.ExecuteCodeAndFetchResults(programmingLanguage, teacherCode, testCases);

                await UploadStringToSupabase(teacherResultPath, JsonConvert.SerializeObject(teacherResult), "application/json");
            }

            // 🧾 Student Submission Handling
            return await HandleCodingAssignmentSubmission(student.StudentId, assignmentId, file, submissionFolder, baseFolder, programmingLanguage, testCases);
        }

        // Helper methods for Supabase
        private async Task<bool> SupabaseFileExists(string path)
        {
            var request = new HttpRequestMessage(HttpMethod.Head, $"{_supabaseUrl}/storage/v1/object/{_bucket}/{path}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseKey);
            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }

        private async Task<string> DownloadFileAsString(string path)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_supabaseUrl}/storage/v1/object/{_bucket}/{path}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseKey);
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        private async Task UploadStringToSupabase(string path, string content, string contentType)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"{_supabaseUrl}/storage/v1/object/{_bucket}/{path}")
            {
                Content = new StringContent(content, Encoding.UTF8, contentType)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseKey);
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }



        private async Task<IActionResult> HandleNormalAssignmentSubmission(int studentId, int assignmentId, IFormFile file)
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

            // ✅ Check if already submitted
            bool submissionExists = await _context.Submissions
                .AnyAsync(s => s.AssignmentId == assignmentId && s.StudentId == studentId);

            if (submissionExists)
            {
                return BadRequest("You have already submitted this assignment.");
            }

            // ✅ Prepare Supabase path
            string uniqueIdentifier = Guid.NewGuid().ToString("N");
            string sanitizedClassName = string.Concat(assignment.Class.ClassName.Split(Path.GetInvalidFileNameChars()));
            string originalFileName = Path.GetFileName(file.FileName);
            string uniqueFileName = $"{uniqueIdentifier}_{studentId}_{assignmentId}_{originalFileName}";
            string supabasePath = $"NormalAssignments/{sanitizedClassName}/{uniqueFileName}";

            // ✅ Upload to Supabase
            bool uploadSuccess = await UploadFileToSupabase(supabasePath, file);
            if (!uploadSuccess)
            {
                return StatusCode(500, "Failed to upload file to Supabase.");
            }

            // ✅ Save submission to database
            var submission = new Submission
            {
                StudentId = studentId,
                AssignmentId = assignmentId,
                ClassId = assignment.Class.ClassId,
                FilePath = $"{_supabaseUrl}/storage/v1/object/public/{_bucket}/{supabasePath}", // Direct URL
                SubmissionDate = DateTime.Now
            };

            _context.Submissions.Add(submission);
            await _context.SaveChangesAsync();

            return RedirectToAction("Submissions", "Student");
        }

        // Uploading file to Supabase
        private async Task<bool> UploadFileToSupabase(string path, IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    Console.WriteLine("Upload failed: File is null or empty.");
                    return false;
                }

                using var stream = file.OpenReadStream();
                using var content = new StreamContent(stream);
                content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");

                var request = new HttpRequestMessage(HttpMethod.Put, $"{_supabaseUrl}/storage/v1/object/{_bucket}/{Uri.EscapeUriString(path)}")
                {
                    Content = content
                };

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseKey);
                request.Headers.Add("x-upsert", "true");

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Upload failed ({response.StatusCode}): {errorBody}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in UploadFileToSupabase: {ex.Message}");
                return false;
            }
        }

        private async Task<IActionResult> HandleCodingAssignmentSubmission(
            int studentId, int assignmentId, IFormFile file, string submissionPath, string assignmentPath, string programmingLanguage, List<TestCase> testCases)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Invalid file upload.");

            var assignment = await _context.Assignments
                                           .Include(a => a.Class)
                                           .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId);
            if (assignment == null || assignment.Class == null)
                return BadRequest("Invalid assignment or class.");

            // 🔹 Generate Supabase paths
            string extension = GetFileExtension(programmingLanguage);
            string studentFileName = $"student_{studentId}{extension}";
            string studentFilePath = $"{submissionPath}/{studentFileName}";

            // 🔹 Upload student file to Supabase
            bool uploadSuccess = await UploadFileToSupabase(studentFilePath, file);
            if (!uploadSuccess)
                return StatusCode(500, "Failed to upload student code file to Supabase.");

            // 🔹 Read Student Code
            string studentCode;
            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                studentCode = await reader.ReadToEndAsync();
            }

            Console.WriteLine(studentCode);

            // 🔹 Execute student code using Judge0
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

            // 🔹 Load Teacher Results from Supabase
            // 🔹 Load or Generate Teacher Results from Supabase
            string teacherResultPath = $"{assignmentPath}/Results/teacher_result.json";
            Dictionary<string, string> teacherExecutionResults = new();

            if (await SupabaseFileExists(teacherResultPath))
            {
                string teacherResultJson = await DownloadFileAsString(teacherResultPath);
                teacherExecutionResults = JsonConvert.DeserializeObject<Dictionary<string, string>>(teacherResultJson)
                                          ?? new Dictionary<string, string>();
            }
            else
            {
                // 🔸 Teacher result missing → run teacher code

                string teacherFilePath = $"{assignmentPath}/SampleSolution{GetFileExtension(programmingLanguage)}";

                if (!await SupabaseFileExists(teacherFilePath))
                    return BadRequest("Teacher's code not found in Supabase.");

                string teacherCode = await DownloadFileAsString(teacherFilePath);

                var executionResults = await _codeExecutionService.ExecuteCodeAndFetchResults(programmingLanguage, teacherCode, testCases);
                if (executionResults == null || executionResults.Count == 0)
                    return StatusCode(500, "Failed to execute teacher's code.");

                teacherExecutionResults = executionResults;

                // 🔸 Save teacher results to Supabase
                string serializedTeacherResults = JsonConvert.SerializeObject(teacherExecutionResults);
                using var teacherResultStream = new MemoryStream(Encoding.UTF8.GetBytes(serializedTeacherResults));

                var resultFormFile = new FormFile(teacherResultStream, 0, teacherResultStream.Length, null, "teacher_result.json")
                {
                    Headers = new HeaderDictionary(),
                    ContentType = "application/json"
                };

                bool uploadSuccessTeacher = await UploadFileToSupabase(teacherResultPath, resultFormFile);
                if (!uploadSuccessTeacher)
                    return StatusCode(500, "Failed to upload teacher result to Supabase.");
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

            // 🔹 Save Student Result to Supabase
            string studentResultPath = $"{assignmentPath}/Results/student_{studentId}_result.json";
            var resultJson = JsonConvert.SerializeObject(finalResults);
            using (var resultStream = new MemoryStream(Encoding.UTF8.GetBytes(resultJson)))
            {
                var formFile = new FormFile(resultStream, 0, resultStream.Length, null, "result.json")
                {
                    Headers = new HeaderDictionary(),
                    ContentType = "application/json"
                };

                bool resultUploadSuccess = await UploadFileToSupabase(studentResultPath, formFile);
                if (!resultUploadSuccess)
                    return StatusCode(500, "Failed to upload student result to Supabase.");
            }

            // 🔹 Store student file path (as public Supabase URL)
            string publicStudentFileUrl = $"{_supabaseUrl}/storage/v1/object/public/{_bucket}/{studentFilePath}";

            // 🔹 Check for existing submission
            var existingSubmission = await _context.Submissions
                                                   .FirstOrDefaultAsync(s => s.AssignmentId == assignmentId && s.StudentId == studentId);

            if (existingSubmission != null)
            {
                existingSubmission.FilePath = publicStudentFileUrl;
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
                    FilePath = publicStudentFileUrl,
                    SubmissionDate = DateTime.Now
                };
                _context.Submissions.Add(submission);
            }

            await _context.SaveChangesAsync();

            // 🔹 Grade assignment
            await AutoGradeAssignment(studentId, assignmentId, assignmentPath, assignment.InstructorId, assignment.Points);

            return RedirectToAction("Grades", "Student");
        }


        private async Task AutoGradeAssignment(int studentId, int assignmentId, string assignmentPath, int instructorId, int totalScore)
        {
            string studentResultFilePath = $"{assignmentPath}/Results/student_{studentId}_result.json";

            // 🔸 Check existence of result file in Supabase
            bool exists = await SupabaseFileExists(studentResultFilePath);
            if (!exists)
            {
                Console.WriteLine("[ERROR] Student result file not found in Supabase!");
                return;
            }

            // 🔸 Download and deserialize result file
            string resultJson = await DownloadFileAsString(studentResultFilePath);
            if (string.IsNullOrWhiteSpace(resultJson))
            {
                Console.WriteLine("[ERROR] Empty result file.");
                return;
            }

            var studentResults = JsonConvert.DeserializeObject<List<dynamic>>(resultJson);
            if (studentResults == null || studentResults.Count == 0)
            {
                Console.WriteLine("[ERROR] No test cases found in student result file!");
                return;
            }

            // 🔸 Score calculation
            int totalTestCases = studentResults.Count;
            int passedTestCases = studentResults.Count(tc => tc.IsCorrect == true);
            float obtainedScore = ((float)passedTestCases / totalTestCases) * totalScore;
            float percentage = (obtainedScore / totalScore) * 100;

            string remarks = percentage switch
            {
                >= 90 => "Excellent performance!",
                >= 75 => "Great job! Keep it up!",
                >= 60 => "Satisfactory, but room for improvement.",
                >= 50 => "Needs improvement. Keep practicing!",
                _ => "Poor performance. Consider reviewing the concepts."
            };

            // 🔸 Save grade in database
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
