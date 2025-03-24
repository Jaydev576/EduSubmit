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

namespace EduSubmit.Controllers
{
    public class SubmissionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public SubmissionController(AppDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
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
        public IActionResult Submit(int id)
        {
            var assignment = _context.Assignments.FirstOrDefault(a => a.AssignmentId == id);
            if (assignment == null)
            {
                return NotFound();
            }

            var submission = new Submission
            {
                AssignmentId = id,
                SubmissionDate = DateTime.Now // Default submission time
            };

            return View(submission); // Show a submission form
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Submit(Submission submission, IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    ModelState.AddModelError("File", "Please upload a file.");
                    return View(submission);
                }

                var assignment = _context.Assignments
                                         .Include(a => a.Class)
                                         .FirstOrDefault(a => a.AssignmentId == submission.AssignmentId);

                if (assignment == null || assignment.Class == null)
                {
                    ModelState.AddModelError("File", "Invalid assignment or class.");
                    return View(submission);
                }

                var userEmail = User.FindFirstValue(ClaimTypes.Email)
                              ?? User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");

                var student = _context.Students.FirstOrDefault(s => s.EmailAddress == userEmail);
                if (student == null)
                {
                    ModelState.AddModelError("File", "Student record not found.");
                    return View(submission);
                }

                submission.StudentId = student.StudentId;
                submission.ClassId = assignment.Class.ClassId;

                // 🔹 Check if a submission already exists for this student and assignment
                bool submissionExists = _context.Submissions
                    .Any(s => s.AssignmentId == submission.AssignmentId && s.StudentId == student.StudentId);

                if (submissionExists)
                {
                    ModelState.AddModelError("File", "You have already submitted this assignment.");
                    return View(submission);
                }

                // 🔹 File upload logic
                string uniqueIdentifier = Guid.NewGuid().ToString("N");
                string sanitizedClassName = string.Concat(assignment.Class.ClassName.Split(Path.GetInvalidFileNameChars()));
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", sanitizedClassName);

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                string originalFileName = Path.GetFileName(file.FileName);
                string uniqueFileName = $"{uniqueIdentifier}_{student.StudentId}_{assignment.AssignmentId}_{originalFileName}";
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    file.CopyTo(fileStream);
                }

                // 🔹 Store file path and submission details
                submission.FilePath = $"/uploads/{sanitizedClassName}/{uniqueFileName}";
                submission.SubmissionDate = DateTime.Now;

                _context.Submissions.Add(submission);
                _context.SaveChanges();

                return RedirectToAction("Submissions", "Student");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("File", "An error occurred: " + ex.Message);
                return View(submission);
            }
        }


    }
}
