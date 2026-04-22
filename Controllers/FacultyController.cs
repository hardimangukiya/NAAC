using Microsoft.AspNetCore.Mvc;
using NAAC.Data;
using NAAC.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace NAAC.Controllers
{
    public class FacultyController : Controller
    {
        private readonly ApplicationDbContext _db;

        public FacultyController(ApplicationDbContext db)
        {
            _db = db;
        }

        private async Task<List<int>> GetAssignedCriteriaIdsAsync(int userId)
        {
            return await _db.CriteriaAssignments
                .Where(a => a.FacultyId == userId && a.IsActive)
                .Select(a => a.CriteriaId)
                .ToListAsync();
        }

        public async Task<IActionResult> Dashboard()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var user = await _db.Users.FindAsync(userId);
            if (user == null || !user.Role.Equals("Faculty", StringComparison.OrdinalIgnoreCase)) return RedirectToAction("Login", "Account");

            var assignedCriteriaIds = await GetAssignedCriteriaIdsAsync(userId.Value);
            
            // Get all tables belonging to assigned criteria
            var assignedTables = await _db.NAACTables
                .Include(t => t.Criteria)
                .Where(t => assignedCriteriaIds.Contains(t.CriteriaId))
                .ToListAsync();

            ViewBag.AssignedTasks = assignedCriteriaIds.Count;
            ViewBag.TotalRecords = await _db.DataRecords.CountAsync(r => r.FacultyId == userId);
            ViewBag.CompletedTasks = await _db.DataRecords.CountAsync(r => r.FacultyId == userId && (r.Status == "Verified" || r.Status == "Finalized"));
            
            var tablesWithData = await _db.DataRecords.Where(r => r.FacultyId == userId).Select(r => r.TableId).Distinct().ToListAsync();
            ViewBag.PendingSubmissions = assignedTables.Count(t => !tablesWithData.Contains(t.Id));
            
            ViewBag.RejectedTasks = await _db.DataRecords.CountAsync(r => r.FacultyId == userId && r.Status == "Rejected");

            // Task Progress Calculation
            var taskProgress = new List<dynamic>();
            foreach (var table in assignedTables)
            {
                var latestRecord = await _db.DataRecords
                    .Where(r => r.TableId == table.Id && r.FacultyId == userId)
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefaultAsync();

                var status = "Not Started";
                var progress = 0;

                if (latestRecord != null)
                {
                    status = latestRecord.Status;
                    progress = latestRecord.Status == "Finalized" ? 100 : (latestRecord.Status == "Verified" ? 90 : (latestRecord.Status == "Submitted" ? 60 : 30));
                }

                taskProgress.Add(new {
                    TableNumber = table.TableNumber,
                    Name = table.Name,
                    Status = status,
                    Progress = progress,
                    TableId = table.Id
                });
            }
            ViewBag.TaskProgress = taskProgress;

            // Recent activity for this faculty
            ViewBag.RecentActivity = await _db.ActivityLogs
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.Timestamp)
                .Take(6)
                .ToListAsync();

            return View();
        }

        public async Task<IActionResult> MyCriteria()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var assignments = await _db.CriteriaAssignments
                .Include(a => a.Criteria)
                .Where(a => a.FacultyId == userId && a.IsActive)
                .ToListAsync();

            return View(assignments);
        }

        public async Task<IActionResult> MyTables()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var criteriaIds = await GetAssignedCriteriaIdsAsync(userId.Value);

            var tables = await _db.NAACTables
                .Include(t => t.Criteria)
                .Where(t => criteriaIds.Contains(t.CriteriaId))
                .OrderBy(t => t.Criteria.Number)
                .ThenBy(t => t.TableNumber)
                .ToListAsync();

            return View(tables);
        }

        public async Task<IActionResult> DataEntry(int? tableId = null)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var criteriaIds = await GetAssignedCriteriaIdsAsync(userId.Value);

            // Fetch available tables for selection
            ViewBag.AvailableTables = await _db.NAACTables
                .Include(t => t.Criteria)
                .Where(t => criteriaIds.Contains(t.CriteriaId))
                .ToListAsync();

            ViewBag.UserCollege = HttpContext.Session.GetString("CollegeName");

            if (tableId.HasValue)
            {
                var table = await _db.NAACTables
                    .Include(t => t.Columns)
                    .Include(t => t.Criteria)
                    .FirstOrDefaultAsync(t => t.Id == tableId && criteriaIds.Contains(t.CriteriaId));
                
                if (table == null) return Unauthorized();

                // Fetch latest rejected record for pre-filling
                var rejectedRecord = await _db.DataRecords
                    .Where(r => r.TableId == tableId && r.FacultyId == userId && r.Status == "Rejected")
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefaultAsync();
                
                // Fetch academic years that already have records (except those needing correction/Rejected)
                var lockedYears = await _db.DataRecords
                    .Where(r => r.TableId == tableId && r.FacultyId == userId && r.Status != "Rejected")
                    .Select(r => r.AcademicYear)
                    .ToListAsync();

                ViewBag.ExistingRecord = rejectedRecord;
                ViewBag.LockedYears = lockedYears;
                return View(table);
            }

            return View();
        }

        public async Task<IActionResult> ViewRecords(int? tableId = null, string? status = null)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var criteriaIds = await GetAssignedCriteriaIdsAsync(userId.Value);

            var query = _db.DataRecords
                .Include(r => r.Table)
                .ThenInclude(t => t.Criteria)
                .Where(r => r.FacultyId == userId && criteriaIds.Contains(r.Table.CriteriaId));

            if (tableId.HasValue)
            {
                query = query.Where(r => r.TableId == tableId);
                ViewBag.CurrentTable = await _db.NAACTables.FindAsync(tableId);
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(r => r.Status == status);
            }

            var records = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
            
            ViewBag.AvailableTables = await _db.NAACTables
                .Include(t => t.Criteria)
                .Where(t => criteriaIds.Contains(t.CriteriaId))
                .ToListAsync();

            ViewBag.CurrentStatus = status;

            return View(records);
        }

        [HttpGet]
        public async Task<IActionResult> GetTableRecordsJson(int? tableId, string? tableNumber)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Unauthorized();

            var query = _db.DataRecords.Include(r => r.Table).Where(r => r.FacultyId == userId);
            
            if (tableId.HasValue) {
                query = query.Where(r => r.TableId == tableId.Value);
            } else if (!string.IsNullOrEmpty(tableNumber)) {
                query = query.Where(r => r.Table.TableNumber == tableNumber);
            } else {
                return BadRequest();
            }

            var records = await query
                .OrderByDescending(r => r.CreatedAt)
                .Take(1)
                .Select(r => new {
                    r.Id,
                    r.AcademicYear,
                    r.JsonData,
                    r.Status,
                    CreatedAt = r.CreatedAt.ToString("MMM dd, yyyy")
                })
                .ToListAsync();

            return Json(records);
        }



        public async Task<IActionResult> PendingTasks()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            // Evaluate missing tables instead of just criteria
            var criteriaIds = await GetAssignedCriteriaIdsAsync(userId.Value);
            
            // Get ALL tables belonging to the mapped criteria
            var allAssignedTables = await _db.NAACTables
                .Include(t => t.Criteria)
                .Where(t => criteriaIds.Contains(t.CriteriaId))
                .ToListAsync();

            // Find all tables that ALREADY have at least one record submitted by this user
            var submittedTableIds = await _db.DataRecords
                .Where(r => r.FacultyId == userId)
                .Select(r => r.TableId)
                .Distinct()
                .ToListAsync();

            // The pending tasks are the tables that have no submissions
            var missingTables = allAssignedTables.Where(t => !submittedTableIds.Contains(t.Id)).ToList();
            
            return View(missingTables);
        }



        [HttpPost]
        public async Task<IActionResult> SaveRecord(int TableId, string AcademicYear, Dictionary<string, string> FormData, [FromServices] IWebHostEnvironment env)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Unauthorized();

            // Security check: Ensure table belongs to an assigned criteria
            var criteriaIds = await GetAssignedCriteriaIdsAsync(userId.Value);
            var table = await _db.NAACTables.FindAsync(TableId);
            if (table == null || !criteriaIds.Contains(table.CriteriaId)) return Unauthorized();

            // Process dynamic file uploads
            foreach (var file in Request.Form.Files)
            {
                if (file.Length > 0)
                {
                    var uploadsFolder = Path.Combine(env.WebRootPath, "uploads");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                    
                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(fileStream);
                    }
                    
                    var match = System.Text.RegularExpressions.Regex.Match(file.Name, @"FormData\[(.*?)\]");
                    if (match.Success) 
                    {
                        FormData[match.Groups[1].Value] = "/uploads/" + uniqueFileName;
                    }
                }
            }

            // Check if a record already exists for this table and year (to handle resubmission)
            var existingRecord = await _db.DataRecords
                .FirstOrDefaultAsync(r => r.TableId == TableId && r.FacultyId == userId && r.AcademicYear == AcademicYear);

            // Global Logic: Check if a record already exists for this year and is NOT rejected
            var duplicateCheck = await _db.DataRecords
                .AnyAsync(r => r.TableId == TableId && r.FacultyId == userId && r.AcademicYear == AcademicYear && r.Status != "Rejected");

            if (duplicateCheck && (existingRecord == null || existingRecord.Status == "Verified" || existingRecord.Status == "Finalized"))
            {
                return BadRequest($"A record for the year {AcademicYear} is already submitted or verified. You cannot create a second record for the same year.");
            }

            if (existingRecord != null)
            {
                if (existingRecord.Status == "Verified")
                {
                    return BadRequest("Verified records cannot be modified. Please contact your HOD if a correction is needed.");
                }

                // Preserve existing file paths if no new file was uploaded
                try 
                {
                    var existingDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(existingRecord.JsonData ?? "{}");
                    if (existingDict != null) {
                        foreach (var key in existingDict.Keys)
                        {
                            if (!string.IsNullOrEmpty(existingDict[key]) && existingDict[key].StartsWith("/uploads/") && string.IsNullOrEmpty(FormData.GetValueOrDefault(key)))
                            {
                                FormData[key] = existingDict[key];
                            }
                        }
                    }
                } 
                catch { }

                // Update existing record
                existingRecord.JsonData = System.Text.Json.JsonSerializer.Serialize(FormData);
                existingRecord.Status = "Submitted";
                existingRecord.UpdatedAt = DateTime.Now;
                existingRecord.Remarks = "Resubmitted after correction";
                _db.DataRecords.Update(existingRecord);
            }
            else
            {
                // Create new record
                var record = new DataRecord
                {
                    TableId = TableId,
                    FacultyId = userId.Value,
                    AcademicYear = AcademicYear,
                    JsonData = System.Text.Json.JsonSerializer.Serialize(FormData),
                    Status = "Submitted",
                    CreatedAt = DateTime.Now
                };
                await _db.DataRecords.AddAsync(record);
            }

            await _db.SaveChangesAsync();

            // Log activity
            _db.ActivityLogs.Add(new ActivityLog {
                Timestamp = DateTime.Now,
                UserName = HttpContext.Session.GetString("UserName") ?? "Faculty",
                Role = "Faculty",
                Action = existingRecord != null ? "Updated Record" : "Added Record",
                Module = "Data Entry",
                Details = $"{(existingRecord != null ? "Updated" : "Added")} data record for {AcademicYear} in table {table.TableNumber}",
                Status = "Success",
                UserId = userId,
                CollegeName = HttpContext.Session.GetString("CollegeName")
            });
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Record captured and submitted for verification!" });
        }
    }
}
