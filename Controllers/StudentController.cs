﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using EduSubmit.Data;
using EduSubmit.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;

namespace EduSubmit.Controllers
{
    [Authorize(Roles = "Student")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class StudentController : Controller
    {
        private readonly AppDbContext _context;
        private IWebHostEnvironment _webHostEnvironment;
        string supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL");
        string supabaseServiceKey = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_KEY");
        string accessKey = Environment.GetEnvironmentVariable("ACCESS_KEY");
        string projectRef = Environment.GetEnvironmentVariable("PROJECT_REF");

        public StudentController(AppDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Student
        public IActionResult Index()
        {
            // Get the logged-in user's email from claims
            var userEmail = User.FindFirst(ClaimTypes.Name)?.Value
                         ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

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

            // Get submitted assignment IDs from the Submissions table
            var submittedAssignmentIds = _context.Submissions
                .Where(s => s.StudentId == studentId)
                .Select(s => s.AssignmentId)
                .ToList();

            // 🔹 Calculate counts without using IsSubmitted column
            int pendingAssignments = assignments.Count(a => !submittedAssignmentIds.Contains(a.AssignmentId));
            int submittedAssignments = assignments.Count(a => submittedAssignmentIds.Contains(a.AssignmentId));

            // 🔹 Calculate overall grade percentage
            var grades = _context.Grades
                .Where(g => g.StudentId == studentId)
                .Join(_context.Assignments,
                      g => g.AssignmentId,
                      a => a.AssignmentId,
                      (g, a) => new
                      {
                          Score = (double?)g.Score ?? 0,  // Handle null Score
                          TotalPoints = (double?)a.Points ?? 1 // Avoid division by zero
                      })
                .ToList();

            double overallGrade = grades.Any()
                ? grades.Sum(g => (g.Score / g.TotalPoints) * 100) / grades.Count
                : 0.0;

            // 🔹 Get 3 recent assignments, tracking submission status
            var recentAssignments = assignments
                .OrderByDescending(a => a.DueDate)
                .Take(4)
                .Select(a => new
                {
                    Id = a.AssignmentId,
                    Title = a.Title,
                    Description = a.Description,
                    DueDate = a.DueDate,
                    Status = submittedAssignmentIds.Contains(a.AssignmentId) ? "Submitted" : "Pending"
                })
                .ToList();



            // 🔹 Store data in ViewBag
            ViewBag.PendingAssignments = pendingAssignments;
            ViewBag.SubmittedAssignments = submittedAssignments;
            ViewBag.OverallGrade = Math.Round(overallGrade, 2);
            ViewBag.RecentAssignments = recentAssignments.Cast<object>().ToList();
            ViewBag.SubmittedAssignmentIds = submittedAssignmentIds;

            return View(assignments);
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
        public IActionResult Assignments()
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
                .Include(a => a.Class)
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


        // GET: Submissions
        public async Task<IActionResult> Submissions()
        {
            // 1. Get user email from claims
            string? userEmail = User?.Claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(userEmail))
            {
                return Unauthorized("User email not found in claims.");
            }

            // 2. Get corresponding StudentId
            var student = await _context.Students.FirstOrDefaultAsync(s => s.EmailAddress == userEmail);
            if (student == null)
            {
                return NotFound("Student not found for the logged-in user.");
            }

            int studentId = student.StudentId;

            // 3. Get student submissions
            var submissions = await _context.Submissions
                .Include(s => s.Assignment)
                .Include(s => s.Student)
                .Where(s => s.StudentId == studentId)
                .ToListAsync();

            // 4. Prepare S3 client using Supabase S3-compatible API
            var assignmentTypeDictionary = new Dictionary<int, bool>();

            var credentials = new Amazon.Runtime.BasicAWSCredentials(
                accessKey: projectRef,
                secretKey: accessKey
            );

            var config = new Amazon.S3.AmazonS3Config
            {
                ServiceURL = $"{supabaseUrl}/storage/v1/s3",
                ForcePathStyle = true,
                RegionEndpoint = Amazon.RegionEndpoint.USEast1 // required by SDK, ignored by Supabase
            };

            using var s3Client = new Amazon.S3.AmazonS3Client(credentials, config);

            foreach (var submission in submissions)
            {
                if (submission.Assignment != null)
                {
                    int classId = submission.Assignment.ClassId;
                    int assignmentId = submission.AssignmentId;
                    string prefix = $"CodingAssignments/{classId}_{assignmentId}/";

                    var listRequest = new Amazon.S3.Model.ListObjectsV2Request
                    {
                        BucketName = "edusubmit",
                        Prefix = prefix,
                        MaxKeys = 1
                    };

                    try
                    {
                        var listResponse = await s3Client.ListObjectsV2Async(listRequest);
                        assignmentTypeDictionary[assignmentId] = listResponse.S3Objects.Any();
                    }
                    catch (Exception)
                    {
                        assignmentTypeDictionary[assignmentId] = false;
                    }
                }
            }

            ViewBag.IsCodingAssignment = assignmentTypeDictionary;
            return View(submissions);
        }

        // GET: Grades
        public async Task<IActionResult> Grades()
        {
            // 1. Get the email from claims
            string? userEmail = User?.Claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(userEmail))
            {
                return Unauthorized("User email not found in claims.");
            }

            // 2. Fetch the student by email
            var student = await _context.Students.FirstOrDefaultAsync(s => s.EmailAddress == userEmail);
            if (student == null)
            {
                return NotFound("Student not found for the logged-in user.");
            }

            int studentId = student.StudentId;

            // 3. Get grades only for the current student
            var grades = await _context.Grades
                .Include(g => g.Student)
                .Include(g => g.Assignment)
                .Where(g => g.StudentId == studentId)
                .ToListAsync();

            // 4. Check Supabase for coding assignments
            var codingAssignments = new Dictionary<int, bool>();

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", supabaseServiceKey);

            foreach (var grade in grades)
            {
                if (grade.Assignment != null)
                {
                    int classId = student.ClassId;
                    int assignmentId = grade.AssignmentId;
                    string prefix = $"CodingAssignments/{classId}_{assignmentId}/";

                    string requestUri = $"{supabaseUrl}/storage/v1/object/list/edusubmit?prefix={Uri.EscapeDataString(prefix)}&limit=1";

                    var response = await httpClient.GetAsync(requestUri);
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var files = JsonConvert.DeserializeObject<List<object>>(content);

                        bool folderExists = files != null && files.Count > 0;
                        codingAssignments.TryAdd(assignmentId, folderExists);
                    }
                    else
                    {
                        codingAssignments.TryAdd(assignmentId, false);
                    }
                }
            }

            ViewBag.IsCodingAssignments = codingAssignments;

            return View(grades);
        }

        // GET: Calendar
        public ActionResult Calendar()
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

            int studentClassId = student.ClassId; // Assuming Student table has ClassId

            // Fetch assignments related to the student's class
            var assignments = _context.Assignments
                .Where(a => a.ClassId == studentClassId) // Filter by class ID
                .ToList();

            return View(assignments);
        }

    }
}
