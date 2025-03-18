﻿using System;
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

namespace EduSubmit.Controllers
{
    [Authorize(Roles = "Organization")] // Restrict access to admins
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

        // GET: Organization/Dashboard
        public IActionResult Dashboard()
        {
            var username = User.Identity.Name;
            if (username != null)
            {
                var admin = _context.Organizations.FirstOrDefault(a => a.Username == username);
                if (admin != null)
                {
                    ViewBag.AdminId = admin.OrganizationId;
                    ViewBag.AdminName = admin.OrganizationName;

                    // Fetch students belonging to the same organization
                    var students = _context.Students
                        .Where(s => s.OrganizationId == admin.OrganizationId)
                        .Select(s => new
                        {
                            FullName = s.FirstName + " " + s.LastName,
                            s.EmailAddress,
                            ClassName = s.Class.ClassName,
                            s.Organization.OrganizationName,
                            Status = "Active" // Assuming you have an active/inactive status field
                        })
                        .ToList();

                    ViewBag.Students = students;
                    ViewBag.StudentCount = students.Count();
                    ViewBag.TotalStudents = _context.Students.Count();
                }
            }
            return View();
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

        public IActionResult Page1()
        {
            var students = _context.Students.Include(s => s.Class).Include(s => s.Organization).ToList();

            if (students == null)
            {
                students = new List<Student>(); // Ensure Model is never null
            }

            return View(students);
        }


        public IActionResult Page2()
        {
            return View();
        }

        public IActionResult Page3()
        {
            return View();
        }

      



    }
}
