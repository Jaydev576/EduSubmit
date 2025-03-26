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

            // ✅ Fetch only classes that have assignments created by this instructor
            var instructorClasses = _context.Classes
                .Where(c => _context.Assignments.Any(a => a.InstructorId == instructorId && a.ClassId == c.ClassId))
                .ToList();

            // ✅ Fetch ALL assignments (past + future) for performance calculation
            var allAssignments = _context.Assignments
                .Where(a => a.InstructorId == instructorId)
                .ToList();

            // ✅ Fetch only FUTURE assignments for the "Active Assignments" section
            var upcomingAssignments = allAssignments
                .Where(a => a.DueDate > DateTime.Now)
                .ToList();

            var assignmentIds = allAssignments.Select(a => a.AssignmentId).ToList();

            var grades = _context.Grades
                .Where(g => assignmentIds.Contains(g.AssignmentId))
                .ToList();

            var classPerformance = instructorClasses.ToDictionary(
                c => c.ClassName,
                c =>
                {
                    // ✅ Total assignments should include past & future assignments
                    int totalAssignments = allAssignments.Count(a => a.ClassId == c.ClassId);

                    var classAssignmentIds = allAssignments.Where(a => a.ClassId == c.ClassId)
                                                           .Select(a => a.AssignmentId)
                                                           .ToList();

                    // ✅ Fix avgGrade calculation
                    double avgGrade = classAssignmentIds.Any() && grades.Any(g => classAssignmentIds.Contains(g.AssignmentId))
                        ? grades.Where(g => classAssignmentIds.Contains(g.AssignmentId)).Average(g => g.Score)
                        : 0;

                    int totalStudents = _context.Students.Count(s => s.ClassId == c.ClassId);

                    int totalSubmissions = _context.Submissions
                        .Where(s => classAssignmentIds.Contains(s.AssignmentId))
                        .Count();

                    // ✅ Ensure expected submissions is correctly calculated
                    int expectedSubmissions = (totalStudents > 0 && totalAssignments > 0)
                        ? totalStudents * totalAssignments
                        : 1; // Prevent division by zero

                    double submissionRate = Math.Round(((double)totalSubmissions / expectedSubmissions) * 100, 2);

                    return new Tuple<int, double, int, int>(totalAssignments, avgGrade, totalStudents, (int)submissionRate);
                }
            );

            ViewData["ClassPerformance"] = classPerformance;

            // ✅ Ensure `activeAssignments` is always initialized
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
        
        [HttpGet]
        public async Task<IActionResult> Assignments(int? classId, string subject, DateTime? dueDate, int? minPoints, int? maxPoints)
        {
            var userEmail = User.Identity?.Name;
            var instructor = await _context.Instructors.FirstOrDefaultAsync(i => i.EmailAddress == userEmail);

            if (instructor == null)
            {
                return Unauthorized("Instructor profile not found.");
            }

            var query = _context.Assignments
                .Include(a => a.Class)
                .Include(a => a.Submissions)
                .Where(a => a.InstructorId == instructor.InstructorId) // Restrict assignments to logged-in instructor
                .AsQueryable();

            // 🔹 Apply Filters
            if (classId.HasValue)
                query = query.Where(a => a.ClassId == classId.Value);

            if (!string.IsNullOrEmpty(subject))
                query = query.Where(a => a.SubjectName.Contains(subject));

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

            // ✅ Fetch class IDs from assignments
            var instructorClassIds = await _context.Assignments
                                                   .Where(a => a.InstructorId == instructorId)
                                                   .Select(a => a.ClassId)
                                                   .Distinct()
                                                   .ToListAsync();

            Console.WriteLine($"Instructor's Class IDs Count: {instructorClassIds.Count}"); // Debugging

            if (!instructorClassIds.Any())
            {
                ViewBag.Classes = new List<object>();  // Ensure View doesn't break
                return View(new List<Dictionary<string, object>>());
            }

            // ✅ Get students only in instructor's classes
            var studentQuery = _context.Students
                                       .Where(s => instructorClassIds.Contains(s.ClassId));

            if (classId.HasValue)
            {
                studentQuery = studentQuery.Where(s => s.ClassId == classId);
            }

            var totalAssignments = await _context.Assignments
                                                 .Where(a => a.InstructorId == instructorId)
                                                 .CountAsync();

            var studentProgressList = await studentQuery
            .Select(s => new Dictionary<string, object>
            {
        { "Id", s.StudentId },
        { "Name", s.FirstName + " " + s.LastName },
        { "ClassName", _context.Classes.Where(c => c.ClassId == s.ClassId).Select(c => c.ClassName).FirstOrDefault() ?? "Unknown" },
        { "LastActive", _context.Submissions
            .Where(sub => sub.StudentId == s.StudentId && _context.Assignments.Where(a => a.InstructorId == instructorId).Select(a => a.AssignmentId).Contains(sub.AssignmentId))
            .OrderByDescending(sub => sub.SubmissionDate)
            .Select(sub => (DateTime?)sub.SubmissionDate)
            .FirstOrDefault() ?? DateTime.MinValue },
        { "CompletedSubmissions", _context.Submissions
            .Where(sub => sub.StudentId == s.StudentId && _context.Assignments.Where(a => a.InstructorId == instructorId).Select(a => a.AssignmentId).Contains(sub.AssignmentId))
            .Count() },
        { "SubmissionCompletionRate", totalAssignments > 0
            ? (double)(_context.Submissions.Where(sub => sub.StudentId == s.StudentId && _context.Assignments.Where(a => a.InstructorId == instructorId).Select(a => a.AssignmentId).Contains(sub.AssignmentId)).Count() * 100) / totalAssignments
            : 0.0 },
        { "AverageGrade", _context.Grades
            .Where(g => g.StudentId == s.StudentId && _context.Assignments.Where(a => a.InstructorId == instructorId).Select(a => a.AssignmentId).Contains(g.AssignmentId))
            .Average(g => (double?)g.Score) ?? 0.0 }
            }).ToListAsync();

            if (filterLow)
            {
                studentProgressList = studentProgressList.Where(s => (double)s["SubmissionCompletionRate"] < 50).ToList();
            }

            studentProgressList = sortBy switch
            {
                "grade" => studentProgressList.OrderByDescending(s => (double)s["AverageGrade"]).ToList(),
                _ => studentProgressList.OrderByDescending(s => (double)s["SubmissionCompletionRate"]).ToList()
            };

            // ✅ Fetch Class List for Dropdown
            var classList = await _context.Classes
                .Where(c => instructorClassIds.Contains(c.ClassId))
                .Select(c => new Dictionary<string, object>
                {
                    { "ClassId", c.ClassId },
                    { "ClassName", c.ClassName }
                })
                .ToListAsync();

            //ViewBag.Classes = classList; // Store in ViewBag


            Console.WriteLine($"Total Classes Fetched: {classList.Count}"); // Debugging

            ViewBag.Classes = classList;
            ViewBag.SelectedClassId = classId;
            ViewBag.AverageGrade = studentProgressList.Any() ? studentProgressList.Average(s => (double)s["AverageGrade"]) : 0;
            ViewBag.AverageSubmissionCompletion = studentProgressList.Any() ? studentProgressList.Average(s => (double)s["SubmissionCompletionRate"]) : 0;

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
