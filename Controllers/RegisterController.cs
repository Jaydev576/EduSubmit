using EduSubmit.Models;
using EduSubmit.Data;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

namespace EduSubmit.Controllers
{
    public class RegisterController : Controller
    {
        private readonly AppDbContext _context;

        public RegisterController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Register()
        {
            return View("~/Views/Shared/Register.cshtml");
        }


        // Register Student
        [HttpPost]
        public IActionResult RegisterStudent(Student student)
        {
            if (ModelState.IsValid)
            {
                student.Password = HashPassword(student.Password);
                _context.Students.Add(student);
                _context.SaveChanges();
                return RedirectToAction("Login", "Account"); // Redirect to login after registration
            }
            return View("Register");
        }

        // Register Instructor
        [HttpPost]
        public IActionResult RegisterInstructor(Instructor instructor)
        {
            if (ModelState.IsValid)
            {
                instructor.Password = HashPassword(instructor.Password);
                _context.Instructors.Add(instructor);
                _context.SaveChanges();
                return RedirectToAction("Login", "Account");
            }
            return View("Index");
        }

        // Register Admin
        [HttpPost]
        public IActionResult RegisterAdmin(Organization admin)
        {
            if (ModelState.IsValid)
            {
                admin.Password = HashPassword(admin.Password);
                _context.Organizations.Add(admin);
                _context.SaveChanges();
                return RedirectToAction("Login", "Account");
            }
            return View("Index");
        }

        // Password Hashing Method
        private string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}
