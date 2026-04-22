using Microsoft.AspNetCore.Mvc;
using NAAC.Data;
using NAAC.Models;
using NAAC.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NAAC.Controllers
{
    public class HODController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailService _email;

        public HODController(ApplicationDbContext db, IEmailService email)
        {
            _db = db;
            _email = email;
        }

        public async Task<IActionResult> Dashboard(string? year = null)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            // Base query for current year/user
            var hod = await _db.Users.FindAsync(userId);
            if (hod == null) return RedirectToAction("Login", "Account");

            // Base query: All users in the HOD's college (excluding the HOD themselves)
            var query = _db.Users.Where(u => u.CollegeName == hod.CollegeName && u.Id != userId);
            
            // Dynamic Academic Year Logic (Current + Past 5 Years)
            var academicYears = new List<string>();
            var currentYear = DateTime.Now.Year;
            
            for (int i = 0; i < 4; i++)
            {
                int start = currentYear - i;
                int end = (start + 1) % 100;
                academicYears.Add($"{start}-{end:D2}");
            }
            ViewBag.AcademicYears = academicYears;
            var selectedYear = string.IsNullOrEmpty(year) ? academicYears[0] : year;
            ViewBag.SelectedYear = selectedYear;
            
            // Staff Breakdown - Filtered by department
            ViewBag.TotalStaff = await query.CountAsync();
            ViewBag.TotalFaculty = await query.CountAsync(u => u.Role == "Faculty");
            ViewBag.TotalCoordinators = await query.CountAsync(u => u.Role == "Coordinator");
            // Count HODs in the same college (including current user for total institutional footprint)
            ViewBag.TotalHODs = await _db.Users.CountAsync(u => u.CollegeName == hod.CollegeName && u.Role == "HOD");
            
            // Finalized Records: Filtered by the selected academic year for consistent reporting
            ViewBag.AllRecords = await _db.DataRecords
                .Include(r => r.Faculty)
                .Where(r => r.Faculty != null && r.Faculty.CollegeName == hod.CollegeName && r.Status == "Verified" && r.AcademicYear == selectedYear)
                .CountAsync();
            
            // Pending Records: Year-specific (helps HOD manage current workload)
            ViewBag.PendingRecords = await _db.DataRecords
                .Include(r => r.Faculty)
                .Where(r => r.Faculty != null && r.Faculty.CollegeName == hod.CollegeName && r.Status == "Submitted" && r.AcademicYear == selectedYear)
                .CountAsync();
            
            // --- REAL NAAC READINESS LOGIC --- (Also filtering criteria progress by total college data)
            var allCriteria = await _db.Criteria
                .Include(c => c.Tables)
                .ToListAsync();
            
            var criteriaReadiness = new List<dynamic>();
            double totalReadinessPercentage = 0;

            foreach (var crit in allCriteria)
            {
                var tableIds = crit.Tables.Select(t => t.Id).ToList();
                var filledTablesCount = await _db.DataRecords
                    .Include(r => r.Faculty)
                    .Where(r => tableIds.Contains(r.TableId) && r.Faculty.CollegeName == hod.CollegeName && r.AcademicYear == selectedYear)
                    .Select(r => r.TableId)
                    .Distinct()
                    .CountAsync();

                int totalTables = tableIds.Count;
                double readiness = totalTables > 0 ? ((double)filledTablesCount / totalTables * 100) : 0;
                
                totalReadinessPercentage += readiness;

                criteriaReadiness.Add(new {
                    Number = crit.Number,
                    Title = crit.Title,
                    Readiness = (int)readiness
                });
            }

            int finalReadinessScore = allCriteria.Count > 0 ? (int)(totalReadinessPercentage / allCriteria.Count) : 0;
            ViewBag.ReadinessScore = finalReadinessScore;
            ViewBag.CriteriaReadiness = criteriaReadiness.OrderBy(c => c.Readiness).ToList();

            // Suggestions Logic
            var lowestCriteria = criteriaReadiness
                .Where(c => c.Readiness < 100)
                .OrderBy(c => c.Readiness)
                .FirstOrDefault();
            
            ViewBag.ReadinessSuggestion = lowestCriteria != null 
                ? $"Focus on Criteria {lowestCriteria.Number} ({lowestCriteria.Title}) to significantly boost your overall readiness."
                : "All criteria are fully documented. Your department is audit-ready!";

            // CHART DATA: Records per Table — real data from DB grouped by table number
            var chartRawData = await _db.DataRecords
                .Include(r => r.Faculty)
                .Include(r => r.Table)
                .Where(r => r.Faculty != null 
                         && r.Faculty.CollegeName == hod.CollegeName
                         && r.Table != null)
                .GroupBy(r => r.Table.TableNumber)
                .Select(g => new { Label = g.Key, Count = g.Count() })
                .ToListAsync();

            // Sort naturally by table number string (e.g. 5.1.1 before 5.1.2)
            var sortedChart = chartRawData
                .OrderBy(x => x.Label)
                .ToList();

            ViewBag.CriteriaLabels = sortedChart.Select(x => x.Label ?? "?").ToArray();
            ViewBag.CriteriaValues = sortedChart.Select(x => x.Count).ToArray();

            // 3. Get actual assignments for the summary table with calculated progress
            var rawAssignments = await _db.CriteriaAssignments
                .Include(a => a.Faculty)
                .Include(a => a.Criteria)
                    .ThenInclude(c => c.Tables)
                .Where(a => a.Faculty.CollegeName == hod.CollegeName && a.IsActive)
                .OrderBy(a => a.Criteria.Number)
                .Take(5)
                .ToListAsync();

            var assignmentsWithProgress = new List<dynamic>();
            foreach (var a in rawAssignments)
            {
                var tableIds = a.Criteria.Tables.Select(t => t.Id).ToList();
                var filledTablesCount = await _db.DataRecords
                    .Include(r => r.Faculty)
                    .Where(r => tableIds.Contains(r.TableId) && r.Faculty.CollegeName == hod.CollegeName)
                    .Select(r => r.TableId)
                    .Distinct()
                    .CountAsync();

                int totalTables = tableIds.Count;
                int progress = totalTables > 0 ? (int)((double)filledTablesCount / totalTables * 100) : 0;
                
                // Add base 10% just for being assigned
                if (progress == 0) progress = 10;

                assignmentsWithProgress.Add(new {
                    Assignment = a,
                    Progress = progress
                });
            }
            ViewBag.AssignmentsProgress = assignmentsWithProgress;

            // 4. Get Dynamic Activity Logs
            ViewBag.RecentActivity = await _db.ActivityLogs
                .Where(l => l.CollegeName == hod.CollegeName)
                .OrderByDescending(l => l.Timestamp)
                .Take(3)
                .ToListAsync();

            // 5. Data Issues Detection (Dynamic & Year-Filtered)
            var allCriteriaIds = await _db.Criteria.Select(c => c.Id).ToListAsync();
            var criteriaWithData = await _db.DataRecords
                .Include(r => r.Faculty)
                .Where(r => r.Faculty.CollegeName == hod.CollegeName && r.AcademicYear == selectedYear)
                .Select(r => r.Table.CriteriaId)
                .Distinct()
                .ToListAsync();

            var noDataCriteriaIds = allCriteriaIds.Except(criteriaWithData).Take(3).ToList();
            ViewBag.NoDataCriteria = await _db.Criteria
                .Where(c => noDataCriteriaIds.Contains(c.Id))
                .OrderBy(c => c.Number)
                .Select(c => c.Number)
                .ToListAsync();

            // Tables with 0 records for this college and SELECTED YEAR
            var tablesWithData = await _db.DataRecords
                .Include(r => r.Faculty)
                .Where(r => r.Faculty.CollegeName == hod.CollegeName && r.AcademicYear == selectedYear)
                .Select(r => r.TableId)
                .Distinct()
                .ToListAsync();

            ViewBag.IncompleteTables = await _db.NAACTables
                .Where(t => !tablesWithData.Contains(t.Id))
                .Take(2)
                .Select(t => t.TableNumber)
                .ToListAsync();

            // 6. Detailed Alerts (Dynamic)
            // Data Missing Alert: Find a table assigned but with 0 records
            var missingTable = await _db.NAACTables
                .Include(t => t.Criteria)
                .Where(t => !tablesWithData.Contains(t.Id))
                .OrderBy(t => t.TableNumber)
                .FirstOrDefaultAsync();

            if (missingTable != null)
            {
                ViewBag.DataMissingTitle = $"Table {missingTable.TableNumber} Missing";
                ViewBag.DataMissingText = $"Criteria {missingTable.Criteria?.Number} has no records uploaded yet.";
            } else {
                ViewBag.DataMissingTitle = "Data Complete";
                ViewBag.DataMissingText = "No structural tables are currently empty.";
            }

            // Not Updated Alert: Find the record with the oldest UpdateAt
            var oldestRecord = await _db.DataRecords
                .Include(r => r.Table)
                .Where(r => r.Faculty.CollegeName == hod.CollegeName)
                .OrderBy(r => r.UpdatedAt ?? r.CreatedAt)
                .FirstOrDefaultAsync();

            if (oldestRecord != null)
            {
                var lastDate = oldestRecord.UpdatedAt ?? oldestRecord.CreatedAt;
                int daysAgo = (DateTime.Now - lastDate).Days;
                ViewBag.NotUpdatedTitle = $"Table {oldestRecord.Table?.TableNumber} Old";
                ViewBag.NotUpdatedText = $"Last update: {daysAgo}d ago ({lastDate:MMM dd})";
            } else {
                ViewBag.NotUpdatedTitle = "Activity Fresh";
                ViewBag.NotUpdatedText = "All submitted data is recent.";
            }

            return View();
        }

        public async Task<IActionResult> ManageUsers(string? role = null, string? search = null, string? dateFilter = null)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var hod = await _db.Users.FindAsync(userId);
            if (hod == null) return RedirectToAction("Login", "Account");

            var query = _db.Users.Where(u => u.CollegeName == hod.CollegeName && u.Id != userId);

            // Role Filtering
            if (!string.IsNullOrEmpty(role))
            {
                query = query.Where(u => u.Role == role);
                ViewBag.ActiveFilter = role;
            }

            // Search by Name/Email
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u => u.FullName.Contains(search) || u.Email.Contains(search));
                ViewBag.SearchTerm = search;
            }

            // Date Filtering
            if (!string.IsNullOrEmpty(dateFilter))
            {
                var today = DateTime.Today;
                switch (dateFilter)
                {
                    case "Today":
                        query = query.Where(u => u.CreatedAt >= today);
                        break;
                    case "Week":
                        var firstDayOfWeek = today.AddDays(-(int)today.DayOfWeek);
                        query = query.Where(u => u.CreatedAt >= firstDayOfWeek);
                        break;
                    case "Month":
                        var firstDayOfMonth = new DateTime(today.Year, today.Month, 1);
                        query = query.Where(u => u.CreatedAt >= firstDayOfMonth);
                        break;
                }
                ViewBag.ActiveDateFilter = dateFilter;
            }

            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            return View(users);
        }

        [HttpPost]
        public async Task<IActionResult> AddUser(string fullName, string email, string role, string mobileNumber)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Json(new { success = false, message = "Session expired." });

            if (await _db.Users.AnyAsync(u => u.Email == email))
            {
                return Json(new { success = false, message = "Email already exists." });
            }

            var hod = await _db.Users.FindAsync(userId);
            string tempPassword = $"Fac@{new Random().Next(1000, 9999)}";

            var user = new User
            {
                FullName = fullName,
                Email = email,
                Role = role,
                MobileNumber = mobileNumber,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword),
                CollegeName = hod?.CollegeName ?? "",
                UniversityName = hod?.UniversityName ?? "",
                AddedByUserId = userId,
                IsEmailVerified = true
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // Send Credentials Email
            try {
                await _email.SendLoginCredentialsAsync(email, tempPassword, role);
            } catch {
                // Log error or notify HOD that email failed but user was created
            }

            return Json(new { success = true, message = "User added and credentials emailed successfully!" });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Json(new { success = false, message = "Session expired." });

            var user = await _db.Users.FindAsync(id);
            if (user != null && user.AddedByUserId == userId)
            {
                _db.Users.Remove(user);
                await _db.SaveChangesAsync();
                return Json(new { success = true, message = "User deleted successfully!" });
            }

            return Json(new { success = false, message = "User not found or access denied." });
        }

        [HttpGet]
        public async Task<IActionResult> GetUser(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Json(null);

            var user = await _db.Users.FindAsync(id);
            if (user == null || user.AddedByUserId != userId) return Json(null);

            return Json(new {
                id = user.Id,
                fullName = user.FullName,
                email = user.Email,
                role = user.Role,
                mobileNumber = user.MobileNumber
            });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateUser(int id, string fullName, string email, string role, string mobileNumber)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Json(new { success = false, message = "Session expired." });

            var user = await _db.Users.FindAsync(id);
            if (user == null || user.AddedByUserId != userId)
            {
                return Json(new { success = false, message = "User not found or access denied." });
            }

            // Check if email is being changed and if new email already exists
            if (user.Email != email && await _db.Users.AnyAsync(u => u.Email == email))
            {
                return Json(new { success = false, message = "New email already exists." });
            }

            user.FullName = fullName;
            user.Email = email;
            user.Role = role;
            user.MobileNumber = mobileNumber;

            _db.Users.Update(user);
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "User updated successfully!" });
        }

        [HttpGet]
        public async Task<IActionResult> CriteriaAssignment()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            // 1. Get ONLY faculties added by this HOD who are NOT yet assigned
            var assignedFacultyIds = await _db.CriteriaAssignments.Where(a => a.IsActive).Select(a => a.FacultyId).ToListAsync();
            ViewBag.FacultyList = await _db.Users
                .Where(u => u.AddedByUserId == userId && u.Role == "Faculty" && !assignedFacultyIds.Contains(u.Id))
                .ToListAsync();

            // 2. Get ONLY criteria that are NOT yet assigned
            var assignedCriteriaIds = await _db.CriteriaAssignments.Where(a => a.IsActive).Select(a => a.CriteriaId).ToListAsync();
            ViewBag.CriteriaList = await _db.Criteria
                .Where(c => !assignedCriteriaIds.Contains(c.Id))
                .OrderBy(c => c.Number).ToListAsync();

            // 3. Get active assignments for the table
            var assignments = await _db.CriteriaAssignments
                .Include(a => a.Faculty)
                .Include(a => a.Criteria)
                .Where(a => a.Faculty.AddedByUserId == userId && a.IsActive)
                .OrderByDescending(a => a.AssignedDate)
                .ToListAsync();

            return View(assignments);
        }

        [HttpPost]
        public async Task<IActionResult> AssignCriterion(int facultyId, int criteriaId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Json(new { success = false, message = "Session expired." });

            // Validate ownership
            var faculty = await _db.Users.FirstOrDefaultAsync(u => u.Id == facultyId && u.AddedByUserId == userId && u.Role == "Faculty");
            if (faculty == null) return Json(new { success = false, message = "Valid faculty member not found." });

            var criteriaExists = await _db.Criteria.AnyAsync(c => c.Id == criteriaId);
            if (!criteriaExists) return Json(new { success = false, message = "Criteria not found." });

            // 1-to-1 Multi-Point Validation:
            // Check if THIS FACULTY already has an assignment
            var facultyHasTask = await _db.CriteriaAssignments.AnyAsync(a => a.FacultyId == facultyId && a.IsActive);
            if (facultyHasTask) return Json(new { success = false, message = "This faculty member is already assigned to a criterion." });

            // Check if THIS CRITERIA is already assigned to ANYONE
            var criteriaIsTaken = await _db.CriteriaAssignments.AnyAsync(a => a.CriteriaId == criteriaId && a.IsActive);
            if (criteriaIsTaken) return Json(new { success = false, message = "This criterion is already assigned to another faculty member." });

            var assignment = new CriteriaAssignment
            {
                FacultyId = facultyId,
                CriteriaId = criteriaId,
                AssignedDate = DateTime.Now,
                IsActive = true
            };

            _db.CriteriaAssignments.Add(assignment);
            await _db.SaveChangesAsync();

            // Log activity
            _db.ActivityLogs.Add(new ActivityLog {
                Timestamp = DateTime.Now,
                UserName = HttpContext.Session.GetString("UserName") ?? "HOD",
                Role = "HOD",
                Action = "Assigned Lead",
                Module = "Criteria",
                Criteria = (await _db.Criteria.FindAsync(criteriaId))?.Number,
                Details = $"Assigned {faculty.FullName} as lead for Criteria {criteriaId}",
                Status = "Success",
                UserId = userId,
                CollegeName = faculty.CollegeName
            });
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Task assigned successfully!" });
        }

        [HttpPost]
        public async Task<IActionResult> UnassignCriterion(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Json(new { success = false, message = "Session expired." });

            var assignment = await _db.CriteriaAssignments
                .Include(a => a.Faculty)
                .FirstOrDefaultAsync(a => a.Id == id && a.Faculty.AddedByUserId == userId);

            if (assignment == null) return Json(new { success = false, message = "Assignment not found." });

            _db.CriteriaAssignments.Remove(assignment);
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Task unassigned successfully!" });
        }

        public async Task<IActionResult> Approvals()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var hod = await _db.Users.FindAsync(userId);
            if (hod == null) return RedirectToAction("Login", "Account");

            // Fetch records from faculties in the same college that are in 'Submitted' status
            var records = await _db.DataRecords
                .Include(r => r.Faculty)
                .Include(r => r.Table)
                    .ThenInclude(t => t.Criteria)
                .Where(r => r.Faculty != null && r.Faculty.CollegeName == hod.CollegeName && r.Status == "Submitted")
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(records);
        }

        [HttpPost]
        public async Task<IActionResult> VerifyRecord(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Json(new { success = false, message = "Session expired." });

            var record = await _db.DataRecords.FindAsync(id);
            if (record == null) return Json(new { success = false, message = "Record not found." });

            record.Status = "Verified";
            record.UpdatedAt = DateTime.Now;
            record.Remarks = "Verified by HOD";

            _db.DataRecords.Update(record);
            await _db.SaveChangesAsync();

            // Log activity
            _db.ActivityLogs.Add(new ActivityLog {
                Timestamp = DateTime.Now,
                UserName = HttpContext.Session.GetString("UserName") ?? "HOD",
                Role = "HOD",
                Action = "Verified Record",
                Module = "Approvals",
                Details = $"Approved record for table {record.TableId}",
                Status = "Success",
                UserId = userId,
                CollegeName = HttpContext.Session.GetString("CollegeName")
            });
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Record verified successfully!" });
        }

        [HttpPost]
        public async Task<IActionResult> RejectRecord(int id, string remarks)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Json(new { success = false, message = "Session expired." });

            var record = await _db.DataRecords.FindAsync(id);
            if (record == null) return Json(new { success = false, message = "Record not found." });

            record.Status = "Rejected";
            record.UpdatedAt = DateTime.Now;
            record.Remarks = remarks;

            _db.DataRecords.Update(record);
            await _db.SaveChangesAsync();

            // Log activity
            _db.ActivityLogs.Add(new ActivityLog {
                Timestamp = DateTime.Now,
                UserName = HttpContext.Session.GetString("UserName") ?? "HOD",
                Role = "HOD",
                Action = "Rejected Record",
                Module = "Approvals",
                Details = $"Rejected record for table {record.TableId}. Reason: {remarks}",
                Status = "Warning",
                UserId = userId,
                CollegeName = HttpContext.Session.GetString("CollegeName")
            });
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Record rejected and sent back to faculty." });
        }

        public async Task<IActionResult> AllRecords(string? year = null, string? tableFilter = null)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var hod = await _db.Users.FindAsync(userId);
            if (hod == null) return RedirectToAction("Login", "Account");

            // Academic Year Options
            var academicYears = new List<string>();
            var currentYear = DateTime.Now.Year;
            for (int i = 0; i < 4; i++)
            {
                int start = currentYear - i;
                int end = (start + 1) % 100;
                academicYears.Add($"{start}-{end:D2}");
            }
            ViewBag.AcademicYears = academicYears;
            var selectedYear = string.IsNullOrEmpty(year) ? academicYears[0] : year;
            ViewBag.SelectedYear = selectedYear;

            // Fetch all Verified records for this college, filtered by the selected year
            var query = _db.DataRecords
                .Include(r => r.Faculty)
                .Include(r => r.Table)
                    .ThenInclude(t => t.Criteria)
                .Where(r => r.Faculty != null 
                         && r.Faculty.CollegeName == hod.CollegeName 
                         && r.Status == "Verified"
                         && r.AcademicYear == selectedYear);

            // Table filter
            if (!string.IsNullOrEmpty(tableFilter))
            {
                if (int.TryParse(tableFilter, out int tableId))
                    query = query.Where(r => r.TableId == tableId);
                ViewBag.TableFilter = tableFilter;
            }

            var records = await query.OrderByDescending(r => r.UpdatedAt).ToListAsync();

            // Build table list for dropdown
            ViewBag.TableList = await _db.NAACTables
                .Include(t => t.Criteria)
                .OrderBy(t => t.Criteria.Number)
                .ThenBy(t => t.TableNumber)
                .ToListAsync();

            ViewBag.TotalCount = records.Count;

            return View(records);
        }

        public async Task<IActionResult> Reports()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var hod = await _db.Users.FindAsync(userId);
            if (hod == null) return RedirectToAction("Login", "Account");

            // Fetch records from faculties in the same college that are in 'Verified' status
            var records = await _db.DataRecords
                .Include(r => r.Faculty)
                .Include(r => r.Table)
                    .ThenInclude(t => t.Criteria)
                .Where(r => r.Faculty != null && r.Faculty.CollegeName == hod.CollegeName && (r.Status == "Verified" || r.Status == "Finalized"))
                .OrderByDescending(r => r.UpdatedAt)
                .ToListAsync();

            // Fetch ALL faculty from the same college for the filter dropdown
            ViewBag.FacultyList = await _db.Users
                .Where(u => u.CollegeName == hod.CollegeName && u.Role == "Faculty")
                .OrderBy(u => u.FullName)
                .ToListAsync();

            // Log access to reports for security/audit
            _db.ActivityLogs.Add(new ActivityLog {
                Timestamp = DateTime.Now,
                UserName = HttpContext.Session.GetString("UserName") ?? "HOD",
                Role = "HOD",
                Action = "Generated Report",
                Module = "Reports",
                Details = $"HOD accessed/generated departmental summary report",
                Status = "Success",
                UserId = userId,
                CollegeName = hod.CollegeName
            });
            await _db.SaveChangesAsync();

            return View(records);
        }
    }
}
