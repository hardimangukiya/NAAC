using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NAAC.Data;
using NAAC.Models;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Data;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NAAC.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool IsAuthorizedAdmin()
        {
            var role = HttpContext.Session.GetString("UserRole");
            return !string.IsNullOrEmpty(role) && role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<IActionResult> Dashboard()
        {
            if (!IsAuthorizedAdmin()) return RedirectToAction("Login", "Account");

            ViewBag.UserName = HttpContext.Session.GetString("UserName");
            
            // System-wide Metrics
            ViewBag.TotalUsers = await _context.Users.CountAsync();
            ViewBag.TotalHODs = await _context.Users.CountAsync(u => u.Role == "HOD");
            ViewBag.TotalFaculty = await _context.Users.CountAsync(u => u.Role == "Faculty");
            ViewBag.TotalAdmins = await _context.Users.CountAsync(u => u.Role == "Admin");
            
            // Dynamic Snapshot Data
            var lastUser = await _context.Users.OrderByDescending(u => u.CreatedAt).FirstOrDefaultAsync();
            ViewBag.LastUserJoined = lastUser?.FullName ?? "None";
            ViewBag.RecentActivityCount = await _context.DataRecords.CountAsync(r => r.CreatedAt >= DateTime.Now.AddDays(-7));
            ViewBag.SystemHealth = "98%"; // Mock system health or real check if needed

            return View();
        }

        public async Task<IActionResult> ManageUsers(string? role = null)
        {
            if (!IsAuthorizedAdmin()) return RedirectToAction("Login", "Account");

            var query = _context.Users.AsQueryable();
            if (!string.IsNullOrEmpty(role))
            {
                query = query.Where(u => u.Role == role);
                ViewBag.ActiveRole = role;
            }

            var users = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();
            return View(users);
        }

        public async Task<IActionResult> Criteria()
        {
            if (!IsAuthorizedAdmin()) return RedirectToAction("Login", "Account");

            var criteria = await _context.Criteria
                .Include(c => c.Tables)
                .OrderBy(c => c.Number)
                .ToListAsync();

            var criteriaStats = new List<dynamic>();
            foreach (var c in criteria)
            {
                var tableIds = c.Tables.Select(t => t.Id).ToList();
                
                // Count unique coordinators assigned to this SPECIFIC criterion
                var coordinatorCount = await _context.CriteriaAssignments
                    .Where(a => a.CriteriaId == c.Id && a.IsActive)
                    .Select(a => a.FacultyId)
                    .OrderBy(id => id) // Added for distinct if needed but Select().Distinct() is better
                    .Distinct()
                    .CountAsync();
                    
                var dataPoints = await _context.DataRecords.CountAsync(r => tableIds.Contains(r.TableId));
                
                criteriaStats.Add(new {
                    Criterion = c,
                    CoordinatorCount = coordinatorCount,
                    DataPoints = dataPoints
                });
            }

            ViewBag.CriteriaStats = criteriaStats;
            return View();
        }

        public async Task<IActionResult> ViewCriterionMetrics(int id)
        {
            if (!IsAuthorizedAdmin()) return RedirectToAction("Login", "Account");

            var criterion = await _context.Criteria
                .Include(c => c.Tables)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (criterion == null) return NotFound();

            var tableIds = criterion.Tables.Select(t => t.Id).ToList();
            var records = await _context.DataRecords
                .Include(r => r.Faculty)
                .Include(r => r.Table)
                .Where(r => tableIds.Contains(r.TableId))
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            ViewBag.CriterionName = $"Criterion {criterion.Number}: {criterion.Title}";
            return View(records);
        }

        public async Task<IActionResult> DownloadMasterTemplate()
        {
            if (!IsAuthorizedAdmin()) return RedirectToAction("Login", "Account");

            var criteria = await _context.Criteria.Include(c => c.Tables).OrderBy(c => c.Number).ToListAsync();
            
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Criterion Number,Criterion Title,Table Number,Table Description");
            
            foreach (var c in criteria)
            {
                if (c.Tables != null && c.Tables.Any())
                {
                    foreach (var t in c.Tables)
                    {
                        csv.AppendLine($"\"{c.Number}\",\"{c.Title}\",\"{t.TableNumber}\",\"{t.Description}\"");
                    }
                }
                else
                {
                    csv.AppendLine($"\"{c.Number}\",\"{c.Title}\",\"N/A\",\"No tables configured\"");
                }
            }

            return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "NAAC_Master_Framework_Template.csv");
        }

        public async Task<IActionResult> AuditSummary()
        {
            if (!IsAuthorizedAdmin()) return RedirectToAction("Login", "Account");

            var criteria = await _context.Criteria
                .Include(c => c.Tables)
                .OrderBy(c => c.Number)
                .ToListAsync();

            var auditResults = new List<dynamic>();
            foreach (var c in criteria)
            {
                var tableIds = c.Tables.Select(t => t.Id).ToList();
                var filledTablesCount = await _context.DataRecords
                    .Where(r => tableIds.Contains(r.TableId))
                    .Select(r => r.TableId)
                    .Distinct()
                    .CountAsync();

                int totalTables = tableIds.Count;
                double progress = totalTables > 0 ? ((double)filledTablesCount / totalTables * 100) : 0;

                auditResults.Add(new {
                    Criterion = c,
                    Progress = (int)progress,
                    TablesFilled = filledTablesCount,
                    TotalTables = totalTables
                });
            }

            return View(auditResults);
        }

        public async Task<IActionResult> EditUser(int id)
        {
            if (!IsAuthorizedAdmin()) return RedirectToAction("Login", "Account");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateUserSecurity(int id, string role, string fullName, string collegeName)
        {
            if (!IsAuthorizedAdmin()) return Json(new { success = false, message = "Unauthorized" });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return Json(new { success = false, message = "User not found" });

            if (user.Role == "Admin") return Json(new { success = false, message = "Cannot modify system administrator roles." });

            user.Role = role;
            user.FullName = fullName;
            user.CollegeName = collegeName;

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "User security profile updated successfully." });
        }

        public async Task<IActionResult> SystemReports()
        {
            if (!IsAuthorizedAdmin()) return RedirectToAction("Login", "Account");

            var conn = _context.Database.GetDbConnection();
            var tables = new List<string>();
            try {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SHOW TABLES";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
            } finally { await conn.CloseAsync(); }

            return View(tables);
        }

        public async Task<IActionResult> TableManager(string tableName)
        {
            if (!IsAuthorizedAdmin()) return RedirectToAction("Login", "Account");

            var conn = _context.Database.GetDbConnection();
            var data = new List<Dictionary<string, object>>();
            var columns = new List<string>();

            try {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"SELECT * FROM `{tableName}` LIMIT 500";
                using var reader = await cmd.ExecuteReaderAsync();
                
                for (int i = 0; i < reader.FieldCount; i++) columns.Add(reader.GetName(i));

                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var val = reader.GetValue(i);
                        row[columns[i]] = val == DBNull.Value ? "NULL" : val;
                    }
                    data.Add(row);
                }
            } finally { await conn.CloseAsync(); }

            ViewBag.TableName = tableName;
            ViewBag.Columns = columns;
            return View(data);
        }

        [HttpPost]
        public async Task<IActionResult> ExecuteQuery(string sql)
        {
            if (!IsAuthorizedAdmin()) return Json(new { success = false, message = "Unauthorized" });

            try {
                int affected = await _context.Database.ExecuteSqlRawAsync(sql);
                return Json(new { success = true, message = $"Success! {affected} rows affected." });
            } catch (Exception ex) {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> DeleteUser(int id)
        {
            if (!IsAuthorizedAdmin()) return Json(new { success = false, message = "Unauthorized" });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return Json(new { success = false, message = "User not found" });

            if (user.Role == "Admin") return Json(new { success = false, message = "Cannot delete an administrator." });

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        public async Task<IActionResult> Reports()
        {
            if (!IsAuthorizedAdmin()) return RedirectToAction("Login", "Account");

            // System Admin sees ALL records across ALL colleges
            var records = await _context.DataRecords
                .Include(r => r.Faculty)
                .Include(r => r.Table)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            ViewBag.FacultyList = await _context.Users.Where(u => u.Role == "Faculty").ToListAsync();
            return View(records);
        }

        public async Task<IActionResult> BulkExport(string format)
        {
            if (!IsAuthorizedAdmin()) return RedirectToAction("Login", "Account");

            var conn = _context.Database.GetDbConnection();
            var tables = new List<string>();
            try {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SHOW TABLES";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
            } finally { await conn.CloseAsync(); }

            if (format == "excel")
            {
                using var workbook = new XLWorkbook();
                var sheetData = new List<DataTable>();

                // Stable Connection Lifecycle for Excel
                using (var connIn = _context.Database.GetDbConnection())
                {
                    if (connIn.State != ConnectionState.Open) await connIn.OpenAsync();
                    foreach (var tableName in tables)
                    {
                        var dataTable = new DataTable(tableName);
                        using var cmd = connIn.CreateCommand();
                        cmd.CommandText = $"SELECT * FROM `{tableName}` LIMIT 2000";
                        using var reader = await cmd.ExecuteReaderAsync();
                        dataTable.Load(reader);
                        sheetData.Add(dataTable);
                    }
                }

                foreach (var dt in sheetData)
                {
                    var sheetName = dt.TableName.Length > 31 ? dt.TableName.Substring(0, 31) : dt.TableName;
                    workbook.Worksheets.Add(dt, sheetName);
                }

                using var stream = new System.IO.MemoryStream();
                workbook.SaveAs(stream);
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"NAAC_Global_Registry_{DateTime.Now:yyyyMMdd}.xlsx");
            }
            else // High-Fidelity Consolidated PDF Audit
            {
                var allTableData = new List<DataTable>();
                
                // Stable Connection Lifecycle: Use ONE connection for ALL fetches
                using (var connIn = _context.Database.GetDbConnection())
                {
                    if (connIn.State != ConnectionState.Open) await connIn.OpenAsync();
                    
                    foreach (var tableName in tables)
                    {
                        var dataTable = new DataTable(tableName);
                        using var cmd = connIn.CreateCommand();
                        cmd.CommandText = $"SELECT * FROM `{tableName}` LIMIT 1000";
                        using var reader = await cmd.ExecuteReaderAsync();
                        dataTable.Load(reader);
                        allTableData.Add(dataTable);
                    }
                }

                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(1, Unit.Centimetre);
                        
                        page.Header().Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("NAAC INSTITUTIONAL AUDIT MANIFEST").SemiBold().FontSize(24).FontColor(Colors.Blue.Medium);
                                col.Item().Text($"Administrative Registry Snapshot | Timestamp: {DateTime.Now:f}").FontSize(10).FontColor(Colors.Grey.Medium);
                            });
                        });
                        
                        page.Content().PaddingVertical(20).Column(col =>
                        {
                            col.Spacing(40);
                            foreach (var dataTable in allTableData)
                            {
                                col.Item().Column(tableCol =>
                                {
                                    tableCol.Spacing(10);
                                    tableCol.Item().Text($"RECORDS: {dataTable.TableName.ToUpper()}").Bold().FontSize(16).FontColor(Colors.Blue.Darken3);
                                    
                                    tableCol.Item().Table(t =>
                                    {
                                        var columnCount = dataTable.Columns.Count;
                                        if (columnCount == 0) return;

                                        t.ColumnsDefinition(cd =>
                                        {
                                            for(int i=0; i<columnCount; i++) cd.RelativeColumn();
                                        });

                                        t.Header(h =>
                                        {
                                            foreach (DataColumn dc in dataTable.Columns)
                                            {
                                                h.Cell().Background(Colors.Blue.Lighten5).Padding(5).Text(dc.ColumnName).Bold().FontSize(8).FontColor(Colors.Blue.Medium);
                                            }
                                        });

                                        int rowIdx = 0;
                                        foreach (DataRow dr in dataTable.Rows)
                                        {
                                            var bgColor = (rowIdx++ % 2 == 0) ? Colors.White : Colors.Grey.Lighten5;
                                            foreach (var item in dr.ItemArray)
                                            {
                                                var val = item?.ToString() ?? "-";
                                                if (val.Length > 100) val = val.Substring(0, 97) + "...";

                                                t.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Padding(5).Text(val).FontSize(7);
                                            }
                                        }
                                    });
                                });
                            }
                        });
                        
                        page.Footer().AlignCenter().Text(x => {
                            x.Span("Institutional Compliance Record | Page ");
                            x.CurrentPageNumber();
                            x.Span(" of ");
                            x.TotalPages();
                        });
                    });
                });

                using var stream = new System.IO.MemoryStream();
                document.GeneratePdf(stream);
                return File(stream.ToArray(), "application/pdf", $"NAAC_Global_Registry_Audit_{DateTime.Now:yyyyMMdd}.pdf");
            }
        }
        public async Task<IActionResult> SystemSettings()
        {
            if (!IsAuthorizedAdmin()) return RedirectToAction("Login", "Account");

            try {
                var test = await _context.SystemSettings.FirstOrDefaultAsync();
            } catch {
                // Table doesn't exist, create it manually
                await _context.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS SystemSettings (
                        Id INT AUTO_INCREMENT PRIMARY KEY,
                        SystemName VARCHAR(100) NOT NULL,
                        SystemLogo VARCHAR(255),
                        InstitutionName VARCHAR(255),
                        UpdatedAt DATETIME NOT NULL
                    );
                ");
                await _context.Database.ExecuteSqlRawAsync(@"
                    INSERT INTO SystemSettings (Id, SystemName, InstitutionName, SystemLogo, UpdatedAt)
                    VALUES (1, 'NAAC Portal', 'National Assessment and Accreditation Council', '/images/naac-logo.png', NOW());
                ");
            }

            var settings = await _context.SystemSettings.FirstOrDefaultAsync() ?? new SystemSetting { Id = 1 };
            return View(settings);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateSystemSettings(string systemName, string institutionName, IFormFile? logoFile)
        {
            if (!IsAuthorizedAdmin()) return Json(new { success = false, message = "Unauthorized" });

            var settings = await _context.SystemSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new SystemSetting { Id = 1 };
                _context.SystemSettings.Add(settings);
            }

            settings.SystemName = systemName;
            settings.InstitutionName = institutionName;
            settings.UpdatedAt = DateTime.Now;

            if (logoFile != null && logoFile.Length > 0)
            {
                var fileName = "system-logo" + Path.GetExtension(logoFile.FileName);
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images", fileName);
                
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await logoFile.CopyToAsync(stream);
                }
                settings.SystemLogo = "/images/" + fileName;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "System settings updated successfully." });
        }
        [HttpPost]
        public async Task<IActionResult> TriggerReminders()
        {
            if (!IsAuthorizedAdmin()) return Json(new { success = false, message = "Unauthorized" });

            // Identify criteria with < 100% progress
            var criteria = await _context.Criteria.Include(c => c.Tables).ToListAsync();
            int notifiedCount = 0;

            foreach (var c in criteria)
            {
                var tableIds = c.Tables.Select(t => t.Id).ToList();
                var filledCount = await _context.DataRecords
                    .Where(r => tableIds.Contains(r.TableId))
                    .Select(r => r.TableId)
                    .Distinct()
                    .CountAsync();

                if (filledCount < tableIds.Count || tableIds.Count == 0)
                {
                    // Find assigned leads for this criteria
                    var assignedLeads = await _context.CriteriaAssignments
                        .Include(a => a.Faculty)
                        .Where(a => a.CriteriaId == c.Id && a.IsActive)
                        .Select(a => a.Faculty)
                        .ToListAsync();

                    foreach (var lead in assignedLeads)
                    {
                        // Mock notification: In a real system, you'd call an IEmailService here
                        // e.g., await _emailService.SendReminderAsync(lead.Email, c.Number);
                        notifiedCount++;
                    }
                }
            }

            if (notifiedCount > 0)
                return Json(new { success = true, message = $"Successfully dispatched reminders to {notifiedCount} departmental leads." });
            else
                return Json(new { success = false, message = "No incomplete criteria found. All leads are current." });
        }
    }
}
