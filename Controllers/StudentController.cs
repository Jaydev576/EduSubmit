using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using EduSubmit.Data;
using EduSubmit.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EduSubmit.Controllers
{
    [Authorize(Roles = "Student")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class StudentController : Controller
    {
        private readonly AppDbContext _context;

        public StudentController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Student
        public IActionResult Index()
        {
            // Get the logged-in user's email from claims
            var userEmail = User.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(userEmail))
            {
                return Unauthorized(); // Ensure the user is authenticated
            }

            // Fetch the StudentId using the email
            var student = _context.Students
                .Where(s => s.EmailAddress == userEmail)
                .Select(s => new { s.StudentId, s.ClassId })
                .FirstOrDefault();

            if (student == null)
            {
                return NotFound("Student not found");
            }

            int studentId = student.StudentId;
            int classId = student.ClassId;

            // Fetch assignments for the student's class
            var assignments = _context.Assignments
                .Where(a => a.ClassId == classId)
                .ToList();

            // Count pending and submitted assignments
            int pendingAssignments = assignments.Count(a => !a.IsSubmitted);
            int submittedAssignments = assignments.Count(a => a.IsSubmitted);

            // Calculate overall grade percentage
            var grades = _context.Grades
                .Where(g => g.StudentId == studentId)
                .Join(_context.Assignments, // Join with Assignments table
                      g => g.AssignmentId,
                      a => a.AssignmentId,
                      (g, a) => new
                      {
                          Score = g.Score,  // Student's score
                          TotalPoints = a.Points // Total points for the assignment
                      })
                .ToList();

            double overallGrade = grades.Any()
                ? grades.Sum(g => (double)g.Score / g.TotalPoints * 100) / grades.Count
                : 0.0;

            // Get recent assignments (limit to last 5)
            var recentAssignments = assignments
                .OrderByDescending(a => a.DueDate)
                .Take(5)
                .Select(a => new
                {
                    Id = a.AssignmentId,
                    Title = a.Title,
                    Status = a.IsSubmitted ? "Submitted" : "Pending"
                })
                .ToList();

            // Store data in ViewBag
            ViewBag.PendingAssignments = pendingAssignments;
            ViewBag.SubmittedAssignments = submittedAssignments;
            ViewBag.OverallGrade = Math.Round(overallGrade, 2); // Round for better display
            ViewBag.RecentAssignments = recentAssignments;

            return View();
        }


        // GET: Student/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var student = await _context
                .Students.Include(s => s.Class)
                .Include(s => s.Organization)
                .FirstOrDefaultAsync(m => m.StudentId == id);
            if (student == null)
            {
                return NotFound();
            }

            return View(student);
        }

        // GET: Student/Create
        public IActionResult Create()
        {
            ViewData["ClassId"] = new SelectList(_context.Classes, "ClassId", "ClassName");
            ViewData["OrganizationId"] = new SelectList(
                _context.Organizations,
                "OrganizationId",
                "OrganizationName"
            );
            return View();
        }

        // POST: Student/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind(
                "StudentId,FirstName,LastName,EmailAddress,DateOfBirth,Password,OrganizationId,ClassId"
            )]
                Student student
        )
        {
            if (ModelState.IsValid)
            {
                _context.Add(student);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["ClassId"] = new SelectList(
                _context.Classes,
                "ClassId",
                "ClassName",
                student.ClassId
            );
            ViewData["OrganizationId"] = new SelectList(
                _context.Organizations,
                "OrganizationId",
                "OrganizationName",
                student.OrganizationId
            );
            return View(student);
        }

        // GET: Student/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var student = await _context.Students.FindAsync(id);
            if (student == null)
            {
                return NotFound();
            }
            ViewData["ClassId"] = new SelectList(
                _context.Classes,
                "ClassId",
                "ClassName",
                student.ClassId
            );
            ViewData["OrganizationId"] = new SelectList(
                _context.Organizations,
                "OrganizationId",
                "OrganizationName",
                student.OrganizationId
            );
            return View(student);
        }

        // POST: Student/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind(
                "StudentId,FirstName,LastName,EmailAddress,DateOfBirth,Password,OrganizationId,ClassId"
            )]
                Student student
        )
        {
            if (id != student.StudentId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(student);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!StudentExists(student.StudentId))
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
            ViewData["ClassId"] = new SelectList(
                _context.Classes,
                "ClassId",
                "ClassName",
                student.ClassId
            );
            ViewData["OrganizationId"] = new SelectList(
                _context.Organizations,
                "OrganizationId",
                "OrganizationName",
                student.OrganizationId
            );
            return View(student);
        }

        // GET: Student/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var student = await _context
                .Students.Include(s => s.Class)
                .Include(s => s.Organization)
                .FirstOrDefaultAsync(m => m.StudentId == id);
            if (student == null)
            {
                return NotFound();
            }

            return View(student);
        }

        // POST: Student/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student != null)
            {
                _context.Students.Remove(student);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool StudentExists(int id)
        {
            return _context.Students.Any(e => e.StudentId == id);
        }

        // GET: Assignments
        public async Task<IActionResult> Assignments()
        {
            var assignments = await _context.Assignments.Include(a => a.Class).ToListAsync();
            return View(assignments);
        }

        // GET: Submissions
        public async Task<IActionResult> Submissions()
        {
            var submissions = await _context
                .Submissions.Include(s => s.Assignment)
                .Include(s => s.Student)
                .ToListAsync();
            return View(submissions);
        }

        // GET: Grades
        public async Task<IActionResult> Grades()
        {
            var grades = await _context
                .Grades.Include(g => g.Student)
                .Include(g => g.Assignment)
                .ToListAsync();
            return View(grades);
        }

        // GET: Calendar
        public IActionResult Calendar()
        {
            return View();
        }
    }
}
