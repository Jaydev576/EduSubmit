using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EduSubmit.Data;
using EduSubmit.Models;
using System.Net.Http;
using System.Net.Http.Headers;

namespace EduSubmit.Controllers
{
    public class GradeController : Controller
    {
        private readonly AppDbContext _context;
        private HttpClient _httpClient;

        string SUPABASE_URL = Environment.GetEnvironmentVariable("SUPABASE_URL");
        string SUPABASE_SERVICE_KEY = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_KEY");

        public GradeController(AppDbContext context, HttpClient httpClient)
        {
            _context = context;
            _httpClient = httpClient;
        }

        // GET: Grades
        public async Task<IActionResult> Index()
        {
            var appDbContext = _context.Grades.Include(g => g.Assignment).Include(g => g.Instructor).Include(g => g.Student);
            return View(await appDbContext.ToListAsync());
        }


        // GET: Grades/Create
        public IActionResult Create()
        {
            ViewData["AssignmentId"] = new SelectList(_context.Assignments, "AssignmentId", "Description");
            ViewData["InstructorId"] = new SelectList(_context.Instructors, "InstructorId", "EmailAddress");
            ViewData["StudentId"] = new SelectList(_context.Students, "StudentId", "EmailAddress");
            return View();
        }

        // POST: Grades/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("StudentId,AssignmentId,Score,Remarks,InstructorId")] Grade grade)
        {
            if (ModelState.IsValid)
            {
                _context.Add(grade);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["AssignmentId"] = new SelectList(_context.Assignments, "AssignmentId", "Description", grade.AssignmentId);
            ViewData["InstructorId"] = new SelectList(_context.Instructors, "InstructorId", "EmailAddress", grade.InstructorId);
            ViewData["StudentId"] = new SelectList(_context.Students, "StudentId", "EmailAddress", grade.StudentId);
            return View(grade);
        }

        // GET: Grades/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var grade = await _context.Grades.FindAsync(id);
            if (grade == null)
            {
                return NotFound();
            }
            ViewData["AssignmentId"] = new SelectList(_context.Assignments, "AssignmentId", "Description", grade.AssignmentId);
            ViewData["InstructorId"] = new SelectList(_context.Instructors, "InstructorId", "EmailAddress", grade.InstructorId);
            ViewData["StudentId"] = new SelectList(_context.Students, "StudentId", "EmailAddress", grade.StudentId);
            return View(grade);
        }

        // POST: Grades/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("StudentId,AssignmentId,Score,Remarks,InstructorId")] Grade grade)
        {
            if (id != grade.StudentId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(grade);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!GradeExists(grade.StudentId))
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
            ViewData["AssignmentId"] = new SelectList(_context.Assignments, "AssignmentId", "Description", grade.AssignmentId);
            ViewData["InstructorId"] = new SelectList(_context.Instructors, "InstructorId", "EmailAddress", grade.InstructorId);
            ViewData["StudentId"] = new SelectList(_context.Students, "StudentId", "EmailAddress", grade.StudentId);
            return View(grade);
        }

        // GET: Grades/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var grade = await _context.Grades
                .Include(g => g.Assignment)
                .Include(g => g.Instructor)
                .Include(g => g.Student)
                .FirstOrDefaultAsync(m => m.StudentId == id);
            if (grade == null)
            {
                return NotFound();
            }

            return View(grade);
        }

        // POST: Grades/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var grade = await _context.Grades.FindAsync(id);
            if (grade != null)
            {
                _context.Grades.Remove(grade);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool GradeExists(int id)
        {
            return _context.Grades.Any(e => e.StudentId == id);
        }

        // GET: Grade/Details?studentId&&assignmentId
        public async Task<IActionResult> Details(int studentId, int assignmentId)
        {
            var grade = await _context.Grades
                .Include(g => g.Student)
                .Include(g => g.Assignment)
                .Include(g => g.Instructor)
                .FirstOrDefaultAsync(g => g.StudentId == studentId && g.AssignmentId == assignmentId);

            if (grade == null)
            {
                return NotFound();
            }

            // 🔸 Supabase Path Check for Coding Assignment
            string codingAssignmentPrefix = $"CodingAssignments/{grade.Student.ClassId}_{assignmentId}/";
            bool isCodingAssignment = await SupabaseFileExists($"{codingAssignmentPrefix}/ProgrammingLanguage.txt");

            ViewBag.BucketPath = $"{SUPABASE_URL}/storage/v1/object/edusubmit";
            ViewBag.IsCodingAssignment = isCodingAssignment;
            return View(grade);
        }

        // Helper method
        private async Task<bool> SupabaseFileExists(string path)
        {
            var request = new HttpRequestMessage(HttpMethod.Head, $"{SUPABASE_URL}/storage/v1/object/edusubmit/{path}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SUPABASE_SERVICE_KEY);
            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
    }
}
