using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EduSubmit.Data;
using EduSubmit.Models;
using System.Security.Claims;

namespace EduSubmit.Controllers
{
    public class AssignmentController : Controller
    {
        private readonly AppDbContext _context;

        public AssignmentController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Assignments
        public IActionResult Index()
        {
            var userEmail = User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userEmail))
            {
                return Unauthorized();
            }

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

            var assignments = _context.Assignments
                .Where(a => a.ClassId == classId)
                .ToList();

            var submittedAssignmentIds = _context.Submissions
                .Where(s => s.StudentId == studentId)
                .Select(s => s.AssignmentId)
                .ToList();

            int pendingAssignments = assignments.Count(a => !submittedAssignmentIds.Contains(a.AssignmentId));
            int submittedAssignments = assignments.Count(a => submittedAssignmentIds.Contains(a.AssignmentId));

            double overallGrade = _context.Grades
                .Where(g => g.StudentId == studentId)
                .Join(_context.Assignments,
                      g => g.AssignmentId,
                      a => a.AssignmentId,
                      (g, a) => new
                      {
                          Score = (double?)g.Score ?? 0,
                          TotalPoints = (double?)a.Points ?? 1
                      })
                .ToList()
                .Select(g => (g.Score / g.TotalPoints) * 100)
                .DefaultIfEmpty(0)
                .Average();

            var recentAssignments = assignments
                .OrderByDescending(a => a.DueDate)
                .Take(3)
                .Select(a => new
                {
                    Id = a.AssignmentId,
                    Title = a.Title,
                    Description = a.Description,
                    DueDate = a.DueDate,
                    IsSubmitted = submittedAssignmentIds.Contains(a.AssignmentId) // ✅ Store submission status
                })
                .ToList();

            ViewBag.PendingAssignments = pendingAssignments;
            ViewBag.SubmittedAssignments = submittedAssignmentIds; // ✅ Store submitted assignment IDs
            ViewBag.OverallGrade = Math.Round(overallGrade, 2);
            ViewBag.RecentAssignments = recentAssignments;

            return View(assignments);
        }





        // GET: Assignments/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var assignment = await _context.Assignments
                .Include(a => a.Class)
                .FirstOrDefaultAsync(m => m.AssignmentId == id);

            if (assignment == null)
            {
                return NotFound();
            }

            return View(assignment);
        }

        // GET: Assignments/Create
        public IActionResult Create()
        {
            ViewData["ClassId"] = new SelectList(_context.Classes, "ClassId", "ClassName");
            return View();
        }

        // POST: Assignments/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("AssignmentId,Title,Description,DueDate,Points,SubjectName,ClassId")] Assignment assignment)
        {
            if (ModelState.IsValid)
            {
                _context.Add(assignment);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["ClassId"] = new SelectList(_context.Classes, "ClassId", "ClassName", assignment.ClassId);
            return View(assignment);
        }

        // GET: Assignments/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var assignment = await _context.Assignments.FindAsync(id);
            if (assignment == null)
            {
                return NotFound();
            }
            ViewData["ClassId"] = new SelectList(_context.Classes, "ClassId", "ClassName", assignment.ClassId);
            return View(assignment);
        }

        // POST: Assignments/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("AssignmentId,Title,Description,DueDate,Points,SubjectName,ClassId")] Assignment assignment)
        {
            if (id != assignment.AssignmentId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(assignment);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AssignmentExists(assignment.AssignmentId))
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
            ViewData["ClassId"] = new SelectList(_context.Classes, "ClassId", "ClassName", assignment.ClassId);
            return View(assignment);
        }

        // GET: Assignments/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var assignment = await _context.Assignments
                .Include(a => a.Class)
                .FirstOrDefaultAsync(m => m.AssignmentId == id);
            if (assignment == null)
            {
                return NotFound();
            }

            return View(assignment);
        }

        // POST: Assignments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var assignment = await _context.Assignments.FindAsync(id);
            if (assignment != null)
            {
                _context.Assignments.Remove(assignment);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool AssignmentExists(int id)
        {
            return _context.Assignments.Any(e => e.AssignmentId == id);
        }
    }
}
