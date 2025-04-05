using EduSubmit.Models;
using EduSubmit.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace EduSubmit.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;

        public AccountController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string email, string password, string role)
        {
            string hashedPassword = HashPassword(password);
            object user = null;

            switch (role)
            {
                case "Student":
                    user = _context.Students.FirstOrDefault(s => s.EmailAddress == email && s.Password == hashedPassword);
                    break;
                case "Instructor":
                    user = _context.Instructors.FirstOrDefault(i => i.EmailAddress == email && i.Password == hashedPassword);
                    break;
                case "Organization":
                    user = _context.Organizations.FirstOrDefault(a => a.EmailAddress == email && a.Password == hashedPassword);
                    break;
            }

            if (user != null)
                return AuthenticateUser(email, role, role, "Index");

            ViewBag.Error = "Invalid email or password.";
            return View();
        }


        public IActionResult Logout()
        {
            HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        private IActionResult AuthenticateUser(string email, string role, string controller, string action)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, email),
                new Claim(ClaimTypes.Role, role)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return RedirectToAction(action, controller);
        }

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




        // GET: Show Change Password Form
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(string EmailAddress, string OldPassword, string NewPassword, string ConfirmPassword)
        {
            if (string.IsNullOrEmpty(EmailAddress))
            {
                ViewBag.Message = "Email is required!";
                ViewBag.IsSuccess = false;
                return View();
            }

            if (NewPassword != ConfirmPassword)
            {
                ViewBag.Message = "New Password and Confirm Password do not match!";
                ViewBag.IsSuccess = false;
                return View();
            }

            var student = await _context.Students.FirstOrDefaultAsync(s => s.EmailAddress == EmailAddress);
            if (student == null)
            {
                ViewBag.Message = "User not found!";
                ViewBag.IsSuccess = false;
                return View();
            }

            string hashedOldPassword = HashPassword(OldPassword);
            if (student.Password != hashedOldPassword)
            {
                ViewBag.Message = "Old password is incorrect!";
                ViewBag.IsSuccess = false;
                return View();
            }

            // Hash and update the password
            student.Password = HashPassword(NewPassword);
            _context.Students.Update(student);
            await _context.SaveChangesAsync();

            ViewBag.Message = "Password changed successfully!";
            ViewBag.IsSuccess = true;
            return View();
        }


    }
}
