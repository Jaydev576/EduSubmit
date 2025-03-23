using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using EduSubmit.Data;
using EduSubmit.Models;
using System.Security.Cryptography;
using System.Text;
using OfficeOpenXml;
using System.IO;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using AspNetCoreGeneratedDocument;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EduSubmit.Controllers
{
    [Authorize(Roles = "Organization")] // Restrict access to admins
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class OrganizationController : Controller
    {
        private readonly AppDbContext _context;

        public OrganizationController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Organization
        public async Task<IActionResult> Index()
        {
            LoadDashboard(); // Call the method to set ViewBag values
            return View(await _context.Organizations.ToListAsync());
        }

        // GET: Organization/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var organization = await _context.Organizations.FindAsync(id);
            return organization == null ? NotFound() : View(organization);
        }

        // GET: Organization/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Organization/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("OrganizationId,OrganizationName,Username,Password")] Organization organization)
        {
            if (ModelState.IsValid)
            {
                organization.Password = HashPassword(organization.Password);
                _context.Add(organization);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(organization);
        }

        // GET: Organization/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var organization = await _context.Organizations.FindAsync(id);
            return organization == null ? NotFound() : View(organization);
        }

        // POST: Organization/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("OrganizationId,OrganizationName,Username,Password")] Organization organization)
        {
            if (id != organization.OrganizationId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    organization.Password = HashPassword(organization.Password);
                    _context.Update(organization);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!OrganizationExists(organization.OrganizationId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(organization);
        }

        // GET: Organization/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var organization = await _context.Organizations.FindAsync(id);
            return organization == null ? NotFound() : View(organization);
        }

        // POST: Organization/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var organization = await _context.Organizations.FindAsync(id);
            if (organization != null)
            {
                _context.Organizations.Remove(organization);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }






        public IActionResult UploadXL()
        {
            return View();
        }



        [HttpPost]
        public async Task<IActionResult> UploadXL(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Please upload a valid Excel file.");
            }

            var students = new List<Student>();

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                using (var package = new ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets[0]; // First sheet
                    int rowCount = worksheet.Dimension.Rows;

                    for (int row = 2; row <= rowCount; row++) // Start from row 2 (assuming row 1 has headers)
                    {
                        var student = new Student
                        {
                            FirstName = worksheet.Cells[row, 1].Text,
                            LastName = worksheet.Cells[row, 2].Text,
                            EmailAddress = worksheet.Cells[row, 3].Text,
                            DateOfBirth = DateTime.Parse(worksheet.Cells[row, 4].Text),
                            Password = HashPassword(worksheet.Cells[row, 5].Text),
                            OrganizationId = int.Parse(worksheet.Cells[row, 6].Text),
                            ClassId = int.Parse(worksheet.Cells[row, 7].Text)
                        };

                        students.Add(student);
                    }
                }
            }

            // Save students to database
            await _context.Students.AddRangeAsync(students);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Dashboard)); // Redirect after upload
        }


        //////////////////////////////////////////////////////////////
   



        // GET: Organization/Dashboard
        [HttpGet]
        public IActionResult Dashboard()
        {
            return LoadDashboard();
        }

        // POST: Organization/Dashboard
        [HttpPost]
        //[ValidateAntiForgeryToken] // Uncomment this for security
        public IActionResult DashboardPost()
        {
            return LoadDashboard();
        }

        // Common method to avoid duplicate code
        //private IActionResult LoadDashboard()
        //{
        //    var email = User.Identity.Name;
        //    if (email != null)
        //    {
        //        var admin = _context.Organizations.FirstOrDefault(a => a.EmailAddress == email);
        //        if (admin != null)
        //        {
        //            ViewBag.AdminId = admin.OrganizationId;
        //            ViewBag.AdminName = admin.OrganizationName;

        //            // Fetching counts dynamically from the database
        //            ViewBag.TotalStudents = _context.Students.Count(s => s.OrganizationId == admin.OrganizationId);
        //            ViewBag.ActiveTeachers = _context.Instructors.Count(t => t.OrganizationId == admin.OrganizationId);
        //            ViewBag.TotalClasses = _context.Classes.Count(c => c.OrganizationId == admin.OrganizationId);
        //            ViewBag.Submissions = _context.Submissions.Count(s => s.OrganizationId == admin.OrganizationId);

        //            return View("Index"); // Ensure you return the correct view
        //        }
        //    }

        //    return RedirectToAction("Login"); // Redirect to login if no admin is found
        //}


        private IActionResult LoadDashboard()
        {
            var email = User.Identity.Name;
            if (email != null)
            {
                var admin = _context.Organizations.FirstOrDefault(a => a.EmailAddress == email);
                if (admin != null)
                {
                    ViewBag.AdminId = admin.OrganizationId;
                    ViewBag.AdminName = admin.OrganizationName;

                    // Fetching counts dynamically
                    ViewBag.TotalStudents = _context.Students.Count(s => s.OrganizationId == admin.OrganizationId);
                    ViewBag.ActiveTeachers = _context.Instructors.Count(t => t.OrganizationId == admin.OrganizationId);
                    ViewBag.TotalClasses = _context.Classes.Count(c => c.OrganizationId == admin.OrganizationId);

                    // Get submission count by joining Submissions and Classes
                    ViewBag.Submissions = _context.Submissions
                        .Count(s => _context.Classes
                            .Any(c => c.ClassId == s.ClassId && c.OrganizationId == admin.OrganizationId));

                    return View("Index"); // Ensure you return the correct view
                }
            }

            return RedirectToAction("Login"); // Redirect to login if no admin is found
        }



        // Utility: Hash Password
        private string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes) builder.Append(b.ToString("x2"));
                return builder.ToString();
            }
        }

        private bool OrganizationExists(int id)
        {
            return _context.Organizations.Any(e => e.OrganizationId == id);
        }

        public IActionResult ManageUsers()
        {
            int loggedInOrgId = GetLoggedInOrganizationId(); // Get the logged-in organization's ID

            var students = _context.Students
                                   .Where(s => s.OrganizationId == loggedInOrgId)
                                   .ToList(); // Filter only students from the logged-in organization

            return View(students);
        }

        // Example method to get logged-in organization ID
        //private int GetLoggedInOrganizationId()
        //{
        //    return HttpContext.Session.GetInt32("OrganizationId") ?? 0; // Retrieve Organization ID from session
        //}


        public IActionResult ManageClasses()
        {
            var organizationId = GetLoggedInOrganizationId();
            if (organizationId == 0) return NotFound("Organization not found.");

            var classes = _context.Classes
                .Where(c => c.OrganizationId == organizationId)
                .Include(c => c.Students) // Include Students for counting
                .Select(c => new
                {
                    ClassId = c.ClassId,
                    ClassName = c.ClassName,
                    TotalStudents = c.Students.Count()
                })
                .ToList();

            ViewBag.Classes = classes;
            return View();
        }


        public IActionResult Page3()
        {
            return View();
        }









        //private int GetLoggedInOrganizationId()
        //{
        //    var username = User.Identity.Name; // Get logged-in organization's username
        //    var organization = _context.Organizations.FirstOrDefault(o => o.Username == username);

        //    return organization?.OrganizationId ?? 0; // Return OrganizationId or 0 if not found
        //}



        private int GetLoggedInOrganizationId()
        {
            var email = User.Identity.Name; // Get logged-in organization's email
            var organization = _context.Organizations.FirstOrDefault(o => o.EmailAddress == email); // Match with Email column

            return organization?.OrganizationId ?? 0; // Return OrganizationId or 0 if not found
        }



        ///////////////////////////////////////////////////////////


        [HttpGet("Organization/EditStudent/{studentId}")]
        public async Task<IActionResult> EditStudent(int studentId)
        {
            var student = await _context.Students
                .Include(s => s.Organization)
                .Include(s => s.Class)
                .FirstOrDefaultAsync(s => s.StudentId == studentId);

            if (student == null)
            {
                return NotFound();
            }
            ViewBag.Organizations = _context.Organizations.ToList();
            ViewBag.Classes = _context.Classes.ToList();
            return View(student);
        }

        // POST: Organization/EditStudent/{studentId}
        [HttpPost, ActionName("EditStudent")]

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStudent(int studentId, [Bind("StudentId,FirstName,LastName,EmailAddress,DateOfBirth,Password,OrganizationId,ClassId")] Student student)
        {
            if (studentId != student.StudentId)
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
                    if (!_context.Students.Any(e => e.StudentId == studentId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction("ManageUsers"); // Redirect to student list page
            }
            return View(student);
        }


        [HttpGet("Organization/DeletetStudent/{studentId}")]
        public IActionResult DeleteStudent(int studentId)
        {
            var orgId = GetLoggedInOrganizationId();
            var student = _context.Students.FirstOrDefault(s => s.StudentId == studentId && s.OrganizationId == orgId);

            if (student == null)
            {
                return NotFound();
            }

            return View(student);
        }

        [HttpPost, ActionName("DeleteStudentConfirmed")]
        public IActionResult DeleteStudentConfirmed(Student student)
        {
            var orgId = GetLoggedInOrganizationId();
            var existingStudent = _context.Students.FirstOrDefault(s => s.StudentId == student.StudentId && s.OrganizationId == orgId);

            if (existingStudent == null)
            {
                return Unauthorized();
            }

            _context.Students.Remove(existingStudent);
            _context.SaveChanges();

            return RedirectToAction("ManageUsers");
        }

        public IActionResult AddStudent()
        {
            ViewBag.Classes = new SelectList(_context.Classes, "ClassId", "ClassName");
            ViewBag.Organizations = new SelectList(_context.Organizations, "OrganizationId", "OrganizationName");

            return View();
        }




        [HttpPost]
        public IActionResult AddStudent(Student student)
        {
            if (ModelState.IsValid)
            {
                _context.Students.Add(student);
                _context.SaveChanges();
                return RedirectToAction("Index"); // Redirect to student list
            }

            // Re-populate ViewBag to avoid null reference in the dropdowns
            ViewBag.Classes = new SelectList(_context.Classes, "ClassId", "ClassName");
            ViewBag.Organizations = new SelectList(_context.Organizations, "OrganizationId", "OrganizationName");

            return View(student);
        }






        [HttpPost]
        public IActionResult ClassList()
        {
            var organizationId = GetLoggedInOrganizationId();
            if (organizationId == 0) return NotFound("Organization not found.");

            var classes = _context.Classes
                .Where(c => c.OrganizationId == organizationId)
                .Select(c => new
                {
                    ClassId = c.ClassId,
                    ClassName = c.ClassName,
                    TotalStudents = _context.Students.Count(s => s.ClassId == c.ClassId) // Count students for each class
                })
                .ToList();

            ViewBag.Classes = classes; // Store in ViewBag
            return View();
        }






        public IActionResult CreateClass()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]

        public async Task<IActionResult> CreateClass([Bind("ClassId,ClassName,OrganizationId")] Class @class)
        {
            if (ModelState.IsValid)
            {
                _context.Add(@class);
                _context.SaveChangesAsync();
                return RedirectToAction("ManageClasses");
            }
            return View();
        }




        // GET: Class/Edit/5
        public async Task<IActionResult> EditClass(int id)
        {
            var cls = await _context.Classes.Include(c => c.Organization).FirstOrDefaultAsync(c => c.ClassId == id);
            if (cls == null)
            {
                return NotFound();
            }

            ViewBag.OrganizationName = cls.Organization.OrganizationName; // Pass Organization Name to View

            return View(cls);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditClass(int id, [Bind("ClassId,ClassName")] Class cls) // Removed OrganizationId from binding
        {
            if (id != cls.ClassId)
            {
                return NotFound();
            }

            var existingClass = await _context.Classes.FindAsync(id);
            if (existingClass == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    existingClass.ClassName = cls.ClassName; // Only update ClassName, not OrganizationId
                    _context.Update(existingClass);
                    await _context.SaveChangesAsync();
                    return RedirectToAction("ManageClasses");
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError("", "Error updating data.");
                }
            }

            return View(cls);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [HttpPost]

        public async Task<IActionResult> DeleteClass(int id)
        {
            var cls = await _context.Classes.FindAsync(id);
            if (cls == null)
            {
                return NotFound();
            }

            try
            {
                _context.Classes.Remove(cls);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                TempData["ErrorMessage"] = "Cannot delete this class because it has related records.";
            }

            return RedirectToAction("ManageClasses");
        }







    }
}
