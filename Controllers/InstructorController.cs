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

namespace EduSubmit.Controllers
{
    [Authorize(Roles = "Instructor")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class InstructorController : Controller
    {
        private readonly AppDbContext _context;

        public InstructorController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Instructors
        //public async Task<IActionResult> Index()
        //{
        //    //var appDbContext = _context.Instructors.Include(i => i.Organization);
        //    //return View(await appDbContext.ToListAsync());
        //    return View();
        //}

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

        // GET: Instructors/Delete/5
        //public async Task<IActionResult> Delete(int? id)
        //{
        //    if (id == null)
        //    {
        //        return NotFound();
        //    }

        //    var instructor = await _context.Instructors
        //        .Include(i => i.Organization)
        //        .FirstOrDefaultAsync(m => m.InstructorId == id);
        //    if (instructor == null)
        //    {
        //        return NotFound();
        //    }

        //    return View(instructor);
        //}

        //// POST: Instructors/Delete/5
        //[HttpPost, ActionName("Delete")]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> DeleteConfirmed(int id)
        //{
        //    var instructor = await _context.Instructors.FindAsync(id);
        //    if (instructor != null)
        //    {
        //        _context.Instructors.Remove(instructor);
        //    }

        //    await _context.SaveChangesAsync();
        //    return RedirectToAction(nameof(Index));
        //}

        private bool InstructorExists(int id)
        {
            return _context.Instructors.Any(e => e.InstructorId == id);
        }



        // index method of dashboard

        // Instructor Dashboard 
        public IActionResult Index()
        {
            ViewData["ActiveAssignments"] = _context.Assignments
                                            .Where(a => a.DueDate >= DateTime.Now) // Filter for active assignments
                                            .OrderBy(a => a.DueDate)
                                            .Take(3)
                                            .ToList() ?? new List<Assignment>();


            int totalStudents = _context.Students.Count();


            ViewData["StudentCount"] = totalStudents;

            int totalAssignments = _context.Assignments.Count();
            int totalSubmissions = _context.Submissions.Count();

            int totalExpectedSubmissions = totalStudents * totalAssignments; // Expected submissions

            ViewData["SubmissionRate"] = totalExpectedSubmissions > 0
                                         ? Math.Round(((double)totalSubmissions / totalExpectedSubmissions) * 100, 2) // Round to 2 decimal places
                                         : 0;

            var grades = _context.Grades.AsNoTracking().ToList();
            ViewData["AvgGrade"] = grades.Any()
                                   ? Math.Round(grades.Average(g => (double)g.Score), 2) // Round to 2 decimal places
                                   : 0;

            return View("index");
        }


        // Create Assignment (GET)
        public async Task<IActionResult> CreateAssignment()
        {
            var userEmail = User.Identity?.Name;
            var instructor = await _context.Instructors.FirstOrDefaultAsync(i => i.EmailAddress == userEmail);

            if (instructor == null)
            {
                return Unauthorized("Instructor profile not found.");
            }

            ViewBag.InstructorId = instructor.InstructorId; // Pass InstructorId to View
            ViewBag.Classes = new SelectList(await _context.Classes.ToListAsync(), "ClassId", "ClassName");

            return View();
        }


        // Create Assignment (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAssignment(Assignment assignment)
        {
            var userEmail = User.Identity?.Name;
            var instructor = await _context.Instructors.FirstOrDefaultAsync(i => i.EmailAddress == userEmail);

            if (instructor == null)
            {
                return Unauthorized("Instructor profile not found.");
            }

            assignment.InstructorId = instructor.InstructorId; // Assign InstructorId

            if (ModelState.IsValid)
            {
                _context.Assignments.Add(assignment);
                await _context.SaveChangesAsync();
                return RedirectToAction("Assignments");
            }

            ViewBag.Classes = new SelectList(await _context.Classes.ToListAsync(), "ClassId", "ClassName");
            return View(assignment);
        }

        //Edit Assignment (GET)
        public async Task<IActionResult> EditAssignment(int id)
        {
            var userEmail = User.Identity?.Name;
            var instructor = await _context.Instructors.FirstOrDefaultAsync(i => i.EmailAddress == userEmail);

            if (instructor == null)
            {
                return Unauthorized("Instructor profile not found.");
            }

            var assignment = await _context.Assignments.FindAsync(id);

            if (assignment == null) return NotFound();

            // Ensure that only the instructor who created this assignment can edit it
            if (assignment.InstructorId != instructor.InstructorId)
            {
                return Unauthorized("You are not allowed to edit this assignment.");
            }

            ViewBag.Classes = new SelectList(await _context.Classes.ToListAsync(), "ClassId", "ClassName", assignment.ClassId);
            return View(assignment);
        }


        //Edit Assignment (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAssignment(int id, Assignment assignment)
        {
            if (id != assignment.AssignmentId) return NotFound();

            var userEmail = User.Identity?.Name;
            var instructor = await _context.Instructors.FirstOrDefaultAsync(i => i.EmailAddress == userEmail);

            if (instructor == null)
            {
                return Unauthorized("Instructor profile not found.");
            }

            var existingAssignment = await _context.Assignments.FindAsync(id);

            if (existingAssignment == null) return NotFound();

            // Ensure that only the instructor who created this assignment can edit it
            if (existingAssignment.InstructorId != instructor.InstructorId)
            {
                return Unauthorized("You are not allowed to edit this assignment.");
            }

            if (ModelState.IsValid)
            {
                // Keep the original InstructorId
                assignment.InstructorId = existingAssignment.InstructorId;

                _context.Entry(existingAssignment).CurrentValues.SetValues(assignment);
                await _context.SaveChangesAsync();
                return RedirectToAction("Assignments");
            }

            ViewBag.Classes = new SelectList(await _context.Classes.ToListAsync(), "ClassId", "ClassName", assignment.ClassId);
            return View(assignment);
        }



        //Delete Assignment (GET Confirmation)
        //public async Task<IActionResult> DeleteAssignment(int id)
        //{
        //    var assignment = await _context.Assignments.FindAsync(id);
        //    if (assignment == null) return NotFound();

        //    return View(assignment);
        //}

        /*
        [HttpGet]
        public async Task<IActionResult> DeleteAssignment(int id)
        {
            var assignment = await _context.Assignments
                .Include(a => a.Class) // Ensure Class data is loaded
                .FirstOrDefaultAsync(a => a.AssignmentId == id);

            if (assignment == null)
            {
                return NotFound(); // Handle case where assignment is not found
            }

            // Debugging: Check if Class is being retrieved correctly
            Console.WriteLine($"Class Name: {assignment?.Class?.ClassName}");

            return View(assignment); // Pass assignment data to confirmation view
        }


        //POST: Delete Assignment
        [HttpPost, ActionName("DeleteAssignment")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var assignment = await _context.Assignments.FindAsync(id);
            if (assignment == null) return NotFound();

            _context.Assignments.Remove(assignment);
            await _context.SaveChangesAsync();

            return RedirectToAction("Assignments");
        }
        */


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



        // ✅ 7️⃣ List Assignments
        /*
         public async Task<IActionResult> Assignments()
         {
             var assignments = await _context.Assignments.Include(a => a.Class).ToListAsync();
             return View(assignments);
         }
        */

        [HttpGet]
        public async Task<IActionResult> Assignments(int? classId, string subject, DateTime? dueDate, int? minPoints, int? maxPoints)
        {
            var query = _context.Assignments
                .Include(a => a.Class)
                .Include(a => a.Submissions) // Ensure submissions are loaded
                .AsQueryable();

            // 🔹 Apply Filters
            if (classId.HasValue)
                query = query.Where(a => a.ClassId == classId.Value);

            if (!string.IsNullOrEmpty(subject))
            {
                query = query.Where(a => a.SubjectName.Contains(subject));
            }
            if (dueDate.HasValue)
                query = query.Where(a => a.DueDate.Date == dueDate.Value.Date);

            if (minPoints.HasValue)
                query = query.Where(a => a.Points >= minPoints.Value);

            if (maxPoints.HasValue)
                query = query.Where(a => a.Points <= maxPoints.Value);

            // Fetch the filtered assignments
            var assignments = await query.ToListAsync();

            // Load Class dropdown for filtering
            ViewBag.Classes = new SelectList(await _context.Classes.ToListAsync(), "ClassId", "ClassName");

            return View(assignments);
        }









        // ✅ Show all student submissions (Pending grading)
        public async Task<IActionResult> Submissions()
        {
            var pendingSubmissions = await _context.Submissions
                .Include(s => s.Student)
                .Include(s => s.Assignment)
                .Where(s => !_context.Grades.Any(g => g.StudentId == s.StudentId && g.AssignmentId == s.AssignmentId))
                .ToListAsync();

            return View(pendingSubmissions); // ✅ Pass the correct model to the view
        }


        // ✅ Show all graded submissions
        public async Task<IActionResult> GradedSubmissions()
        {
            var gradedSubmissions = await _context.Grades
                .Include(g => g.Student)
                .Include(g => g.Assignment)
                .Where(g => g.Score > 0) // Fetch only graded assignments
                .ToListAsync();

            return View(gradedSubmissions);
        }


        // ✅ Instructor assigns a grade
        public async Task<IActionResult> GradeAssignment(int studentId, int assignmentId)
        {
            // ✅ Retrieve logged-in email
            var userEmail = User.Identity?.Name;
            if (string.IsNullOrEmpty(userEmail))
            {
                return Unauthorized("User email not found. Please log in again.");
            }

            // ✅ Fetch InstructorId from the database
            var instructor = await _context.Instructors.FirstOrDefaultAsync(i => i.EmailAddress == userEmail);
            if (instructor == null)
            {
                return Unauthorized("Instructor profile not found.");
            }

            int instructorId = instructor.InstructorId;

            // ✅ Fetch Existing Grade Entry (if any)
            var grade = await _context.Grades
                .Include(g => g.Student)
                .Include(g => g.Assignment)
                .FirstOrDefaultAsync(g => g.StudentId == studentId && g.AssignmentId == assignmentId);

            if (grade == null)
            {
                var submission = await _context.Submissions
                    .Include(s => s.Student)
                    .Include(s => s.Assignment)
                    .FirstOrDefaultAsync(s => s.StudentId == studentId && s.AssignmentId == assignmentId);

                if (submission == null)
                {
                    return NotFound("Submission not found.");
                }

                grade = new Grade
                {
                    StudentId = studentId,
                    AssignmentId = assignmentId,
                    Score = 0,
                    Remarks = "",
                    Student = submission.Student,
                    Assignment = submission.Assignment,
                    InstructorId = instructorId // ✅ Use correct InstructorId from DB
                };

                _context.Grades.Add(grade);
                await _context.SaveChangesAsync();
            }

            return View(grade);
        }




        // ✅ Save the assigned grade
        /*
        [HttpPost]
        public async Task<IActionResult> GradeAssignment(int studentId, int assignmentId, float score, string remarks)
        {
            var grade = await _context.Grades
            .FirstOrDefaultAsync(g => g.StudentId == studentId && g.AssignmentId == assignmentId);

            if (grade == null) return NotFound();

            grade.Score = score;
            grade.Remarks = remarks;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(GradedSubmissions)); // ✅ Redirect after grading
        }
        */
        [HttpPost]
        public async Task<IActionResult> GradeAssignment(int studentId, int assignmentId, float score, string remarks)
        {
            var grade = await _context.Grades
                .Include(g => g.Assignment)
                .FirstOrDefaultAsync(g => g.StudentId == studentId && g.AssignmentId == assignmentId);

            if (grade == null) return NotFound();

            // ✅ Ensure score is within range 0 to assignment's max points
            if (score < 0 || score > grade.Assignment.Points)
            {
                ModelState.AddModelError("Score", $"Score must be between 0 and {grade.Assignment.Points}.");
                return View(grade); // Show error message
            }

            grade.Score = score;
            grade.Remarks = remarks;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(GradedSubmissions)); // ✅ Redirect after grading
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
        public async Task<IActionResult> StudentProgress(string sortBy = "completion", bool filterLow = false)
        {
            var totalAssignments = await _context.Assignments.CountAsync(); // Total available assignments

            var studentProgressQuery = _context.Students
                .Select(s => new
                {
                    Id = s.StudentId,
                    Name = s.FirstName,
                    LastActive = _context.Submissions
                        .Where(sub => sub.StudentId == s.StudentId)
                        .OrderByDescending(sub => sub.SubmissionDate)
                        .Select(sub => sub.SubmissionDate)
                        .FirstOrDefault(),

                    CompletedSubmissions = _context.Submissions
                        .Where(sub => sub.StudentId == s.StudentId)
                        .Count(),

                    SubmissionCompletionRate = totalAssignments > 0
                        ? (_context.Submissions.Where(sub => sub.StudentId == s.StudentId).Count() * 100) / totalAssignments
                        : 0, // Avoid division by zero

                    AverageGrade = _context.Grades
                        .Where(sub => sub.StudentId == s.StudentId)
                        .Average(sub => (double?)sub.Score) ?? 0
                })
                .AsEnumerable() // Convert to IEnumerable
                .Select(s => (dynamic)s); // Convert to dynamic

            // ✅ Apply Filtering (if selected)
            if (filterLow)
            {
                studentProgressQuery = studentProgressQuery.Where(s => s.SubmissionCompletionRate < 50);
            }

            // ✅ Apply Sorting
            studentProgressQuery = sortBy switch
            {
                "grade" => studentProgressQuery.OrderByDescending(s => s.AverageGrade),
                _ => studentProgressQuery.OrderByDescending(s => s.SubmissionCompletionRate) // Default: Sort by Completion %
            };

            var studentList = studentProgressQuery.ToList();

            // ✅ Compute Class Averages (Prevent Issues in View)
            ViewBag.AverageGrade = studentList.Any() ? studentList.Average(s => (double)s.AverageGrade) : 0;
            ViewBag.AverageSubmissionCompletion = studentList.Any() ? studentList.Average(s => (double)s.SubmissionCompletionRate) : 0;

            return View(studentList); // Pass as List<dynamic>
        }



        //// ✅ 1️⃣ View All Announcements
        //public async Task<IActionResult> Announcements()
        //{
        //    var announcements = await _context.Announcements
        //        .Include(a => a.Instructor)
        //        .Include(a => a.Comments)
        //        .OrderByDescending(a => a.CreatedAt)
        //        .ToListAsync();

        //    return View("~/Views/Instructor/Announcements.cshtml", announcements);
        //}

        //// ✅ 2️⃣ Create Announcement (GET)
        //public IActionResult CreateAnnouncement()
        //{
        //    return View("~/Views/Instructor/CreateAnnouncement.cshtml");
        //}

        //// ✅ 3️⃣ Create Announcement (POST)
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> CreateAnnouncement(Announcement announcement)
        //{
        //    if (ModelState.IsValid)
        //    {
        //        // Get logged-in instructor ID
        //        var userEmail = User.Identity?.Name;
        //        var instructor = await _context.Instructors.FirstOrDefaultAsync(i => i.EmailAddress == userEmail);
        //        if (instructor == null) return Unauthorized();

        //        announcement.InstructorId = instructor.InstructorId;
        //        _context.Add(announcement);
        //        await _context.SaveChangesAsync();
        //        return RedirectToAction(nameof(Announcements));
        //    }
        //    return View("~/Views/Instructor/CreateAnnouncement.cshtml", announcement);
        //}

        //// ✅ 4️⃣ Edit Announcement (GET)
        //public async Task<IActionResult> EditAnnouncement(int id)
        //{
        //    var announcement = await _context.Announcements.FindAsync(id);
        //    if (announcement == null) return NotFound();

        //    return View("~/Views/Instructor/EditAnnouncement.cshtml", announcement);
        //}

        //// ✅ 5️⃣ Edit Announcement (POST)
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> EditAnnouncement(int id, Announcement announcement)
        //{
        //    if (id != announcement.Id) return NotFound();

        //    if (ModelState.IsValid)
        //    {
        //        _context.Update(announcement);
        //        await _context.SaveChangesAsync();
        //        return RedirectToAction(nameof(Announcements));
        //    }
        //    return View("~/Views/Instructor/EditAnnouncement.cshtml", announcement);
        //}

        //// ✅ 6️⃣ Delete Announcement (POST)
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> DeleteAnnouncement(int id)
        //{
        //    var announcement = await _context.Announcements.FindAsync(id);
        //    if (announcement == null) return NotFound();

        //    _context.Announcements.Remove(announcement);
        //    await _context.SaveChangesAsync();
        //    return RedirectToAction(nameof(Announcements));
        //}

        //// ✅ 7️⃣ Like an Announcement
        //[HttpPost]
        //public async Task<IActionResult> LikeAnnouncement(int id)
        //{
        //    var announcement = await _context.Announcements.FindAsync(id);
        //    if (announcement == null) return NotFound();

        //    announcement.LikeCount++;
        //    await _context.SaveChangesAsync();
        //    return RedirectToAction(nameof(Announcements));
        //}

        //// ✅ 8️⃣ Add Comment to Announcement
        //[HttpPost]
        //public async Task<IActionResult> AddComment(int id, string content)
        //{
        //    if (string.IsNullOrWhiteSpace(content))
        //    {
        //        return RedirectToAction(nameof(Announcements));
        //    }

        //    var userEmail = User.Identity?.Name;
        //    var instructor = await _context.Instructors.FirstOrDefaultAsync(i => i.EmailAddress == userEmail);
        //    if (instructor == null) return Unauthorized();

        //    var comment = new Comment
        //    {
        //        AnnouncementId = id,
        //        Content = content,
        //        InstructorId = instructor.InstructorId
        //    };

        //    _context.Comments.Add(comment);
        //    await _context.SaveChangesAsync();
        //    return RedirectToAction(nameof(Announcements));
        //}
    

        //public IActionResult Messages()
        //{
        //    return View();
        //}

        //public IActionResult ReportsAnalytics()
        //{
        //    return View();
        //}

        //public IActionResult Settings()
        //{
        //    return View();
        //}

        // Help & Support Page
        public IActionResult HelpSupport()
        {
            return View();
        }

        
    }
}
