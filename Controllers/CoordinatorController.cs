using Microsoft.AspNetCore.Mvc;
using NAAC.Data;
using NAAC.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NAAC.Controllers
{
    public class CoordinatorController : Controller
    {
        private readonly ApplicationDbContext _db;

        public CoordinatorController(ApplicationDbContext db)
        {
            _db = db;
        }

        private async Task LogActivity(string action, string module, string? criteria = null, string? table = null, string? details = null, string status = "Success")
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userName = HttpContext.Session.GetString("UserName") ?? "System";
            var userRole = HttpContext.Session.GetString("UserRole") ?? "Unknown";
            var college = HttpContext.Session.GetString("UserCollege") ?? "";

            var log = new ActivityLog
            {
                Timestamp = DateTime.Now,
                UserId = userId,
                UserName = userName,
                Role = userRole,
                Action = action,
                Module = module,
                Criteria = criteria,
                Table = table,
                Details = details,
                Status = status,
                CollegeName = college
            };

            _db.ActivityLogs.Add(log);
            await _db.SaveChangesAsync();
        }

        public async Task<IActionResult> Dashboard(string? year = null)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var coordinator = await _db.Users.FindAsync(userId);
            if (coordinator == null || !coordinator.Role.Equals("Coordinator", StringComparison.OrdinalIgnoreCase)) return RedirectToAction("Login", "Account");

            // --- DYNAMIC ACADEMIC YEAR LOGIC ---
            var currentYear = DateTime.Now.Year;
            var currentMonth = DateTime.Now.Month;
            string currentAcademicYear = (currentMonth >= 6) 
                ? $"{currentYear}-{currentYear % 100 + 1}" 
                : $"{currentYear - 1}-{(currentYear % 100)}";

            var academicYears = new List<string>();
            
            for (int i = 0; i < 4; i++)
            {
                int start = currentYear - i;
                int end = (start + 1) % 100;
                academicYears.Add($"{start}-{end:D2}");
            }

            ViewBag.AcademicYears = academicYears;
            // --- COORDINATOR METRICS (LIVE) ---
            var selectedYear = year ?? currentAcademicYear;
            ViewBag.SelectedYear = selectedYear;

            // Counts filtered by Coordinator's department where applicable
            ViewBag.TotalFaculty = await _db.Users.CountAsync(u => u.Role == "Faculty" && u.CollegeName == coordinator.CollegeName);
            ViewBag.TotalCriteria = await _db.Criteria.CountAsync();
            
            // Total Data Records for this department in the selected year
            ViewBag.TotalRecords = await _db.DataRecords
                .Include(r => r.Faculty)
                .CountAsync(r => r.Faculty!.CollegeName == coordinator.CollegeName && r.AcademicYear == selectedYear);

            // Approved from HOD Count: Records with status 'Verified'
            ViewBag.ApprovedFromHODCount = await _db.DataRecords
                .Include(r => r.Faculty)
                .CountAsync(r => r.Faculty!.CollegeName == coordinator.CollegeName && r.Status == "Verified" && r.AcademicYear == selectedYear);

            // Re-fetch these for Readiness Score calculation below
            var assignedCriteriaIds = await _db.CriteriaAssignments
                .Include(a => a.Faculty)
                .Where(a => a.Faculty!.CollegeName == coordinator.CollegeName)
                .Select(a => a.CriteriaId)
                .Distinct()
                .ToListAsync();
            var totalCritCount = await _db.Criteria.CountAsync();

            // --- DYNAMIC READINESS SCORE (Total Avg of all Criteria) ---
            var allCriteria = await _db.Criteria
                .Include(c => c.Tables)
                .ThenInclude(t => t.Columns)
                .ToListAsync();

            double totalAggregatedProgress = 0;
            foreach (var crit in allCriteria)
            {
                bool hasLead = assignedCriteriaIds.Contains(crit.Id);
                int tableCount = crit.Tables.Count;
                int columnCount = crit.Tables.Sum(t => t.Columns.Count);

                int prog = 0;
                if (hasLead) prog += 40;
                if (tableCount >= 1) prog += 30;
                if (columnCount >= tableCount * 2 && tableCount > 0) prog += 30; // Slightly lower thresh for total readiness
                else if (columnCount > 0) prog += 15;
                
                totalAggregatedProgress += prog;
            }
            ViewBag.ReadinessScore = totalCritCount > 0 ? (int)(totalAggregatedProgress / totalCritCount) : 0;

            // --- DYNAMIC STRUCTURAL MONITORING ---
            allCriteria = await _db.Criteria
                .Include(c => c.Tables)
                .ThenInclude(t => t.Columns)
                .ToListAsync();

            var criteriaOverview = new List<dynamic>();
            foreach (var crit in allCriteria.Take(5)) // Top 5 for Dashboard
            {
                bool hasLead = assignedCriteriaIds.Contains(crit.Id);
                int tableCount = crit.Tables.Count;
                int columnCount = crit.Tables.Sum(t => t.Columns.Count);

                // Simple structural progress weight: 
                // 40% for having a lead, 30% for having at least 2 tables, 30% for having columns
                int progress = 0;
                if (hasLead) progress += 40;
                if (tableCount >= 1) progress += 30;
                if (columnCount >= tableCount * 3 && tableCount > 0) progress += 30;
                else if (columnCount > 0) progress += 15;

                criteriaOverview.Add(new { 
                    Name = $"{crit.Number}: {crit.Title}", 
                    Progress = progress, 
                    Color = progress > 70 ? "#10b981" : (progress > 30 ? "#f59e0b" : "#ef4444") 
                });
            }
            ViewBag.CriteriaOverview = criteriaOverview;

            // Dynamic Alerts
            var alerts = new List<string>();
            var criteriaNoTables = allCriteria.Where(c => !c.Tables.Any()).Take(2);
            foreach(var c in criteriaNoTables) alerts.Add($"Criteria {c.Number} has no Structural Tables defined.");
            
            var criteriaNoLead = allCriteria.Where(c => !assignedCriteriaIds.Contains(c.Id)).Take(2);
            foreach(var c in criteriaNoLead) alerts.Add($"Criteria {c.Number} has no Faculty Lead assigned.");
            
            ViewBag.CriticalAlerts = alerts;

            // --- DYNAMIC CHART DATA: Acquisition Trends ---
            var sixMonthsAgo = DateTime.Now.AddMonths(-6);
            var trendData = await _db.DataRecords
                .Include(r => r.Faculty)
                .Where(r => r.Faculty!.CollegeName == coordinator.CollegeName && r.CreatedAt >= sixMonthsAgo)
                .ToListAsync();

            var months = Enumerable.Range(0, 6).Select(i => DateTime.Now.AddMonths(-i).ToString("MMM")).Reverse().ToList();
            var submissionsTrend = new List<int>();
            var documentsTrend = new List<int>();

            foreach (var m in months)
            {
                var monthRecords = trendData.Where(r => r.CreatedAt.ToString("MMM") == m).ToList();
                submissionsTrend.Add(monthRecords.Count);
                documentsTrend.Add(monthRecords.Count(r => r.JsonData.Contains(".pdf") || r.JsonData.Contains(".jpg")));
            }

            ViewBag.ChartMonths = months;
            ViewBag.SubmissionsTrend = submissionsTrend;
            ViewBag.DocumentsTrend = documentsTrend;

            // --- DYNAMIC CHART DATA: Distribution ---
            var distribution = new int[4]; // Teaching, Research, Infrastructure, Support
            var recordsWithCriteria = await _db.DataRecords
                .Include(r => r.Table)
                .ThenInclude(t => t.Criteria)
                .Include(r => r.Faculty)
                .Where(r => r.Faculty!.CollegeName == coordinator.CollegeName)
                .ToListAsync();

            foreach (var r in recordsWithCriteria)
            {
                var cNum = r.Table?.Criteria?.Number ?? "";
                if (cNum.StartsWith("1") || cNum.StartsWith("2")) distribution[0]++;
                else if (cNum.StartsWith("3")) distribution[1]++;
                else if (cNum.StartsWith("4")) distribution[2]++;
                else distribution[3]++;
            }
            ViewBag.DistributionData = distribution;

            // Faculty Mapping Summary
            var assignments = await _db.CriteriaAssignments
                .Include(a => a.Faculty)
                .Include(a => a.Criteria)
                .Where(a => a.Faculty.CollegeName == coordinator.CollegeName)
                .OrderBy(a => a.Criteria.Number)
                .ToListAsync();

            return View(assignments);
        }

        public async Task<IActionResult> ManageCriteria()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || userRole != "Coordinator") 
                return RedirectToAction("Login", "Account");

            var coordinator = await _db.Users.FindAsync(userId);
            if (coordinator == null) return RedirectToAction("Login", "Account");

            // Fetch all Master Criteria along with their structural tables
            var allCriteria = await _db.Criteria
                .Include(c => c.Tables)
                .OrderBy(c => c.Number)
                .ToListAsync();

            // Fetch all Assignments for this department
            var assignments = await _db.CriteriaAssignments
                .Include(a => a.Faculty)
                .Where(a => a.Faculty.CollegeName == coordinator.CollegeName)
                .ToListAsync();

            ViewBag.Assignments = assignments;
            return View(allCriteria);
        }

        public async Task<IActionResult> FacultyList(string? criteriaFilter = null)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || userRole != "Coordinator") 
                return RedirectToAction("Login", "Account");

            var coordinator = await _db.Users.FindAsync(userId);
            if (coordinator == null) return RedirectToAction("Login", "Account");

            // Fetch all Faculty members in the same department along with their criteria assignments
            var query = _db.Users
                .Where(u => u.CollegeName == coordinator.CollegeName && u.Role == "Faculty");

            var facultyList = await query.OrderBy(u => u.FullName).ToListAsync();
            
            // Get all current assignments for these faculty
            var assignments = await _db.CriteriaAssignments
                .Include(a => a.Criteria)
                .Where(a => a.Faculty.CollegeName == coordinator.CollegeName)
                .ToListAsync();

            ViewBag.Assignments = assignments;
            ViewBag.AllCriteria = await _db.Criteria.OrderBy(c => c.Number).ToListAsync();
            ViewBag.ActiveCriteriaFilter = criteriaFilter;

            return View(facultyList);
        }



        public IActionResult DepartmentAnalytics()
        {
            return View();
        }

        public async Task<IActionResult> ActivityLogs(string? search = null, string? module = null, string? actionType = null)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || userRole != "Coordinator") 
                return RedirectToAction("Login", "Account");

            var coordinator = await _db.Users.FindAsync(userId);
            if (coordinator == null) return RedirectToAction("Login", "Account");

            ViewBag.UserCollege = coordinator.CollegeName;

            var query = _db.ActivityLogs
                .Where(l => l.CollegeName == coordinator.CollegeName);

            // Force at least one log if none exist so the user sees something
            if (!await query.AnyAsync() && string.IsNullOrEmpty(search))
            {
                // 1. Initial System Entry
                _db.ActivityLogs.Add(new ActivityLog {
                    Timestamp = DateTime.Now.AddDays(-1),
                    UserName = coordinator.FullName,
                    Role = "Coordinator",
                    Action = "System Initialized",
                    Module = "System",
                    Details = $"Activity monitor established for {coordinator.CollegeName}",
                    Status = "Success",
                    CollegeName = coordinator.CollegeName
                });

                // 2. Back-fill from Records
                var existingRecords = await _db.DataRecords
                    .Include(r => r.Faculty)
                    .Where(r => r.Faculty.CollegeName == coordinator.CollegeName)
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(20)
                    .ToListAsync();

                foreach (var rec in existingRecords)
                {
                    _db.ActivityLogs.Add(new ActivityLog {
                        Timestamp = rec.CreatedAt,
                        UserName = rec.Faculty?.FullName ?? "Faculty",
                        Role = "Faculty",
                        Action = "Added Record",
                        Module = "Data Entry",
                        Details = $"Record submitted for academic year {rec.AcademicYear}",
                        Status = "Success",
                        CollegeName = coordinator.CollegeName
                    });
                }
                await _db.SaveChangesAsync();
                
                // Re-fetch query to include new logs
                query = _db.ActivityLogs.Where(l => l.CollegeName == coordinator.CollegeName);
            }

            // Filtering (Case-Insensitive for robustness)
            if (!string.IsNullOrEmpty(search))
            {
                var s = search.ToLower();
                query = query.Where(l => l.UserName.ToLower().Contains(s) || (l.Details != null && l.Details.ToLower().Contains(s)));
                ViewBag.SearchTerm = search;
            }

            if (!string.IsNullOrEmpty(module))
            {
                query = query.Where(l => l.Module == module);
                ViewBag.ActiveModule = module;
            }

            if (!string.IsNullOrEmpty(actionType))
            {
                query = query.Where(l => l.Action == actionType);
                ViewBag.ActiveAction = actionType;
            }

            var logs = await query
                .OrderByDescending(l => l.Timestamp)
                .Take(500) // Limit to last 500 for performance
                .ToListAsync();

            return View(logs);
        }
        [HttpPost]
        public async Task<IActionResult> CreateCriteria(Criteria criteria)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var role = HttpContext.Session.GetString("UserRole");

            if (userId == null || role != "Coordinator") 
                return RedirectToAction("Login", "Account");

            if (ModelState.IsValid)
            {
                _db.Criteria.Add(criteria);
                await _db.SaveChangesAsync();
                await LogActivity("Created Criteria", "Criteria", criteria.Number, null, $"Defined new criteria structure: {criteria.Title}");
                return RedirectToAction("ManageCriteria");
            }
            return RedirectToAction("ManageCriteria");
        }
        [HttpPost]
        public async Task<IActionResult> EditCriteria(Criteria criteria)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var role = HttpContext.Session.GetString("UserRole");
            if (userId == null || role != "Coordinator") return RedirectToAction("Login", "Account");

            if (ModelState.IsValid)
            {
                _db.Criteria.Update(criteria);
                await _db.SaveChangesAsync();
                await LogActivity("Updated Criteria", "Criteria", criteria.Number, null, $"Modified criteria metadata for: {criteria.Title}");
            }
            return RedirectToAction("ManageCriteria");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCriteria(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var role = HttpContext.Session.GetString("UserRole");
            if (userId == null || role != "Coordinator") return RedirectToAction("Login", "Account");

            var criteria = await _db.Criteria.FindAsync(id);
            if (criteria != null)
            {
                _db.Criteria.Remove(criteria);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction("ManageCriteria");
        }

        public async Task<IActionResult> CriteriaDetails(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userId == null || userRole != "Coordinator") return RedirectToAction("Login", "Account");

            var criteria = await _db.Criteria
                .FirstOrDefaultAsync(c => c.Id == id);

            if (criteria == null) return NotFound();

            // Manually fetch tables with columns because EF Core can be tricky with double levels
            var tables = await _db.NAACTables
                .Include(t => t.Columns)
                .Where(t => t.CriteriaId == id)
                .ToListAsync();

            ViewBag.Tables = tables;
            return View(criteria);
        }

        [HttpPost]
        public async Task<IActionResult> AddTable(NAACTable table)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Coordinator") return RedirectToAction("Login", "Account");

            if (table.CriteriaId > 0)
            {
                table.CreatedAt = DateTime.Now;

                // Fix-up nested relationships: Ensure all columns and sub-columns refer to the same table
                if (table.Columns != null)
                {
                    foreach (var col in table.Columns)
                    {
                        col.Table = table;
                        if (col.SubColumns != null)
                        {
                            foreach (var sub in col.SubColumns)
                            {
                                sub.Table = table;
                            }
                        }
                    }
                }

                _db.NAACTables.Add(table);
                await _db.SaveChangesAsync();
                await LogActivity("Created Table", "Criteria", null, table.TableNumber, $"Added new structural table {table.TableNumber} to system.");
                return RedirectToAction("CriteriaDetails", new { id = table.CriteriaId });
            }
            return RedirectToAction("ManageCriteria");
        }

        [HttpPost]
        public async Task<IActionResult> EditTable(int Id, string TableNumber, string Name, string? Description, int CriteriaId)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Coordinator") return RedirectToAction("Login", "Account");

            try 
            {
                var existingTable = await _db.NAACTables.FirstOrDefaultAsync(t => t.Id == Id);
                if (existingTable != null)
                {
                    existingTable.Name = Name;
                    existingTable.Description = Description;
                    existingTable.TableNumber = TableNumber;
                    
                    await _db.SaveChangesAsync();
                    await LogActivity("Updated Table", "Criteria", null, TableNumber, $"Modified metadata for table {TableNumber}");
                    return RedirectToAction("CriteriaDetails", new { id = CriteriaId });
                }
            }
            catch (Exception)
            {
                // Log error
            }
            return RedirectToAction("CriteriaDetails", new { id = CriteriaId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteTable(int id, int criteriaId)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Coordinator") return RedirectToAction("Login", "Account");

            var table = await _db.NAACTables.FindAsync(id);
            if (table != null)
            {
                _db.NAACTables.Remove(table);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction("CriteriaDetails", new { id = criteriaId });
        }

        [HttpPost]
        public async Task<IActionResult> AddColumn(TableColumn column)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Coordinator") return RedirectToAction("Login", "Account");

            if (column.TableId > 0)
            {
                // Ensure any nested sub-columns have the correct TableId set
                if (column.SubColumns != null)
                {
                    foreach (var sub in column.SubColumns)
                    {
                        sub.TableId = column.TableId;
                    }
                }

                _db.TableColumns.Add(column);
                await _db.SaveChangesAsync();
                
                var table = await _db.NAACTables.FindAsync(column.TableId);
                return RedirectToAction("CriteriaDetails", new { id = table?.CriteriaId });
            }
            return RedirectToAction("ManageCriteria");
        }

        [HttpPost]
        public async Task<IActionResult> EditColumn(TableColumn column)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Coordinator") return RedirectToAction("Login", "Account");

            if (column.Id > 0)
            {
                _db.TableColumns.Update(column);
                await _db.SaveChangesAsync();
                
                var col = await _db.TableColumns.Include(c => c.Table).FirstOrDefaultAsync(c => c.Id == column.Id);
                return RedirectToAction("CriteriaDetails", new { id = col?.Table?.CriteriaId });
            }
            return RedirectToAction("ManageCriteria");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteColumn(int id, int criteriaId)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Coordinator") return RedirectToAction("Login", "Account");

            var col = await _db.TableColumns.FindAsync(id);
            if (col != null)
            {
                _db.TableColumns.Remove(col);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction("CriteriaDetails", new { id = criteriaId });
        }
        public async Task<IActionResult> RecordsLog(string? year = null)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var coordinator = await _db.Users.FindAsync(userId);
            if (coordinator == null) return RedirectToAction("Login", "Account");

            var selectedYear = year ?? (DateTime.Now.Month >= 6 ? $"{DateTime.Now.Year}-{DateTime.Now.Year % 100 + 1}" : $"{DateTime.Now.Year - 1}-{DateTime.Now.Year % 100}");

            var records = await _db.DataRecords
                .Include(r => r.Faculty)
                .Include(r => r.Table)
                    .ThenInclude(t => t.Criteria)
                .Where(r => r.Faculty!.CollegeName == coordinator.CollegeName && r.AcademicYear == selectedYear)
                .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                .ToListAsync();

            ViewBag.SelectedYear = selectedYear;
            return View(records);
        }

        public async Task<IActionResult> Verifications()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var coordinator = await _db.Users.FindAsync(userId);
            if (coordinator == null) return RedirectToAction("Login", "Account");

            var records = await _db.DataRecords
                .Include(r => r.Faculty)
                .Include(r => r.Table)
                    .ThenInclude(t => t.Criteria)
                .Where(r => r.Faculty!.CollegeName == coordinator.CollegeName && r.Status == "Verified")
                .OrderByDescending(r => r.UpdatedAt)
                .ToListAsync();

            return View(records);
        }

        [HttpPost]
        public async Task<IActionResult> FinalizeRecord(int id)
        {
            var record = await _db.DataRecords.FindAsync(id);
            if (record == null) return Json(new { success = false, message = "Record not found" });

            record.Status = "Finalized";
            record.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            await LogActivity("Finalized Record", "Reports", null, null, $"Coordinator finalized record for Table {record.TableId} (Year: {record.AcademicYear})");

            return Json(new { success = true, message = "Record finalized successfully! It is now part of the master report." });
        }

        [HttpPost]
        public async Task<IActionResult> RejectRecord(int id, string remarks)
        {
            var record = await _db.DataRecords.FindAsync(id);
            if (record == null) return Json(new { success = false, message = "Record not found" });

            record.Status = "Pending"; // Send back to faculty
            record.Remarks = remarks;
            record.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            await LogActivity("Rejected Record", "Reports", null, null, $"Record rejected with remarks: {remarks}", "Warning");

            return Json(new { success = true, message = "Record rejected and sent back to faculty for correction." });
        }

        public async Task<IActionResult> Reports()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var coordinator = await _db.Users.FindAsync(userId);
            if (coordinator == null) return RedirectToAction("Login", "Account");

            // Fetch records from faculties in the same college that are in 'Verified' or 'Finalized' status
            var records = await _db.DataRecords
                .Include(r => r.Faculty)
                .Include(r => r.Table)
                    .ThenInclude(t => t.Criteria)
                .Where(r => r.Faculty != null && r.Faculty.CollegeName == coordinator.CollegeName && (r.Status == "Verified" || r.Status == "Finalized"))
                .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                .ToListAsync();

            // Fetch ALL faculty from the same college for the filter dropdown
            ViewBag.FacultyList = await _db.Users
                .Where(u => u.CollegeName == coordinator.CollegeName && u.Role == "Faculty")
                .OrderBy(u => u.FullName)
                .ToListAsync();

            return View(records);
        }
    }
}
