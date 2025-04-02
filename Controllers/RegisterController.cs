using EduSubmit.Models;
using EduSubmit.Data;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EduSubmit.Controllers
{
    public class RegisterController : Controller
    {
        private readonly AppDbContext _context;

        public RegisterController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Register Page
        public async Task<IActionResult> Register()
        {
            await LoadDropdowns();
            return View();
        }





        [HttpGet]
        public JsonResult GetClassesByOrganization(int organizationId)
        {
            if (organizationId == 0)
            {
                return Json(new { error = "Invalid Organization ID" });
            }

            var classes = _context.Classes
                .Where(c => c.OrganizationId == organizationId)
                .Select(c => new { c.ClassId, c.ClassName })
                .ToList();

            if (!classes.Any())
            {
                return Json(new { error = "No classes found for this organization" });
            }

            return Json(classes);
        }



        public IActionResult RegisterStudent()
        {
            ViewBag.Organizations = _context.Organizations
                .Select(o => new SelectListItem { Value = o.OrganizationId.ToString(), Text = o.OrganizationName })
                .ToList();

            return View();
        }


        // Register Student
        [HttpPost]
        public async Task<IActionResult> RegisterStudent(Student student)
        {
            if (ModelState.IsValid)
            {
                if (await _context.Students.AnyAsync(s => s.EmailAddress == student.EmailAddress))
                {
                    ModelState.AddModelError("EmailAddress", "Email is already registered.");
                    await LoadDropdowns(student.OrganizationId);
                    return View("Register");
                }

                student.Password = HashPassword(student.Password);
                _context.Students.Add(student);
                await _context.SaveChangesAsync();
                return RedirectToAction("Login", "Account");
            }

            await LoadDropdowns(student.OrganizationId);
            return View("Register");
        }

        // Register Instructor
        [HttpPost]
        public async Task<IActionResult> RegisterInstructor(Instructor instructor)
        {
            if (ModelState.IsValid)
            {
                if (await _context.Instructors.AnyAsync(i => i.EmailAddress == instructor.EmailAddress))
                {
                    ModelState.AddModelError("EmailAddress", "Email is already registered.");
                    return View("Register");
                }

                instructor.Password = HashPassword(instructor.Password);
                _context.Instructors.Add(instructor);
                await _context.SaveChangesAsync();
                return RedirectToAction("Login", "Account");
            }
            return View("Register");
        }

        // Register Admin
        [HttpPost]
        public async Task<IActionResult> RegisterAdmin(Organization admin)
        {
            if (ModelState.IsValid)
            {
                if (await _context.Organizations.AnyAsync(a => a.Username == admin.Username))
                {
                    ModelState.AddModelError("Username", "Username is already taken.");
                    return View("Register");
                }
                if (await _context.Organizations.AnyAsync(a => a.EmailAddress == admin.EmailAddress))
                {
                    ModelState.AddModelError("EmailAddress", "Account with this Email already exists.");
                    return View("Register");
                }

                admin.Password = HashPassword(admin.Password);
                _context.Organizations.Add(admin);
                await _context.SaveChangesAsync();
                return RedirectToAction("Login", "Account");
            }
            return View("Register");
        }

        // Load dropdowns dynamically
        private async Task LoadDropdowns(int selectedOrganizationId = 0)
        {
            ViewBag.Organizations = new SelectList(await _context.Organizations.ToListAsync(), "OrganizationId", "OrganizationName", selectedOrganizationId);
            ViewBag.Classes = new SelectList(await _context.Classes
                .Where(c => c.OrganizationId == selectedOrganizationId || selectedOrganizationId == 0)
                .ToListAsync(), "ClassId", "ClassName");
        }

        // Password Hashing
        private string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return BitConverter.ToString(bytes).Replace("-", "").ToLower();
            }
        }
    }
}
