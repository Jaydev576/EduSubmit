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

        public async Task<IActionResult> Register()
        {
            ViewBag.Organizations = new SelectList(await _context.Organizations.ToListAsync(), "OrganizationId", "OrganizationName");
            ViewBag.Classes = new SelectList(await _context.Classes.ToListAsync(), "ClassId", "ClassName");

            return View();
        }


        // Register Student
        [HttpPost]
        public IActionResult RegisterStudent(Student student)
        {
            if (ModelState.IsValid)
            {
                if (_context.Students.Any(s => s.EmailAddress == student.EmailAddress))
                {
                    ModelState.AddModelError("EmailAddress", "Email is already registered.");
                    return View("Register");
                }

                student.Password = HashPassword(student.Password);
                _context.Students.Add(student);
                _context.SaveChanges();
                return RedirectToAction("Login", "Account");
            }
            return View("Register");
        }

        // Register Instructor
        [HttpPost]
        public IActionResult RegisterInstructor(Instructor instructor)
        {
            if (ModelState.IsValid)
            {
                if (_context.Instructors.Any(i => i.EmailAddress == instructor.EmailAddress))
                {
                    ModelState.AddModelError("EmailAddress", "Email is already registered.");
                    return View("Register");
                }

                instructor.Password = HashPassword(instructor.Password);
                _context.Instructors.Add(instructor);
                _context.SaveChanges();
                return RedirectToAction("Login", "Account");
            }
            return View("Register");
        }

        // Register Admin
        [HttpPost]
        public IActionResult RegisterAdmin(Organization admin)
        {
            if (ModelState.IsValid)
            {
                if (_context.Organizations.Any(a => a.Username == admin.Username))
                {
                    ModelState.AddModelError("Username", "Username is already taken.");
                    return View("Register");
                }
                if(_context.Organizations.Any(a => a.EmailAddress == admin.EmailAddress))
                {
                    ModelState.AddModelError("EmailAddress", "Account with this Email already exists.");
                    return View("Register");
                }

                admin.Password = HashPassword(admin.Password);
                _context.Organizations.Add(admin);
                _context.SaveChanges();
                return RedirectToAction("Login", "Account");
            }
            return View("Register");
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
