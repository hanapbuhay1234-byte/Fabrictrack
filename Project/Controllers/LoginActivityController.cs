using System.Globalization;
using System.Security.Claims;
using System.Text;
using FabriTrack.Data;
using FabriTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace FabriTrack.Controllers
{
    [Authorize(Roles = Roles.Admin + "," + Roles.Staff)]
    public class LoginActivityController : Controller
    {
        private const int PageSize = 25;
        private readonly ApplicationDbContext _context;

        public LoginActivityController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(
            string? userId,
            string? eventType,
            DateTime? startDate,
            DateTime? endDate,
            int page = 1)
        {
            var model = new LoginActivityViewModel
            {
                UserId = userId,
                EventType = eventType,
                StartDate = startDate?.Date,
                EndDate = endDate?.Date,
                Page = Math.Max(1, page),
                PageSize = PageSize
            };

            await PopulateFiltersAsync(model);
            if (model.StartDate.HasValue && model.EndDate.HasValue && model.StartDate > model.EndDate)
            {
                ModelState.AddModelError(nameof(model.EndDate), "The end date must be on or after the start date.");
                return View(model);
            }

            var query = FilterActivities(model);
            model.TotalRecords = await query.CountAsync();
            model.Page = Math.Min(model.Page, model.TotalPages);
            model.Entries = await query
                .OrderByDescending(entry => entry.OccurredAt)
                .ThenByDescending(entry => entry.LoginActivityId)
                .Skip((model.Page - 1) * model.PageSize)
                .Take(model.PageSize)
                .AsNoTracking()
                .ToListAsync();

            return View(model);
        }

        public async Task<IActionResult> Export(
            string? userId,
            string? eventType,
            DateTime? startDate,
            DateTime? endDate)
        {
            var filters = new LoginActivityViewModel
            {
                UserId = userId,
                EventType = eventType,
                StartDate = startDate?.Date,
                EndDate = endDate?.Date
            };

            if (filters.StartDate.HasValue && filters.EndDate.HasValue && filters.StartDate > filters.EndDate)
            {
                TempData["Error"] = "The end date must be on or after the start date.";
                return RedirectToAction(nameof(Index), new { userId, eventType, startDate, endDate });
            }

            var entries = await FilterActivities(filters)
                .OrderByDescending(entry => entry.OccurredAt)
                .ThenByDescending(entry => entry.LoginActivityId)
                .AsNoTracking()
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Date (UTC),Event,Account,IP address,Device");
            foreach (var entry in entries)
            {
                csv.Append(CsvField(entry.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))).Append(',')
                    .Append(CsvField(entry.EventType)).Append(',')
                    .Append(CsvField(entry.AccountName ?? entry.AccountIdentifier ?? "Unrecognized account")).Append(',')
                    .Append(CsvField(entry.IpAddress ?? string.Empty)).Append(',')
                    .Append(CsvField(entry.UserAgent ?? string.Empty)).AppendLine();
            }

            return File(
                new UTF8Encoding(true).GetBytes(csv.ToString()),
                "text/csv; charset=utf-8",
                $"login-activity-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
        }

        private IQueryable<LoginActivity> FilterActivities(LoginActivityViewModel filters)
        {
            var query = _context.LoginActivities.AsQueryable();
            if (!User.IsInRole(Roles.Admin))
            {
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                query = currentUserId == null
                    ? query.Where(entry => false)
                    : query.Where(entry => entry.UserId == currentUserId);
            }
            else if (!string.IsNullOrWhiteSpace(filters.UserId))
            {
                query = query.Where(entry => entry.UserId == filters.UserId);
            }

            if (!string.IsNullOrWhiteSpace(filters.EventType))
                query = query.Where(entry => entry.EventType == filters.EventType);
            if (filters.StartDate.HasValue)
                query = query.Where(entry => entry.OccurredAt >= filters.StartDate.Value);
            if (filters.EndDate.HasValue)
                query = query.Where(entry => entry.OccurredAt < filters.EndDate.Value.AddDays(1));

            return query;
        }

        private async Task PopulateFiltersAsync(LoginActivityViewModel model)
        {
            model.EventTypes = new List<string>
            {
                "Successful sign-in",
                "Failed sign-in",
                "Account locked out",
                "Disabled account sign-in blocked",
                "Signed out"
            };

            if (User.IsInRole(Roles.Admin))
            {
                var users = await _context.LoginActivities
                    .AsNoTracking()
                    .Where(entry => entry.UserId != null)
                    .Select(entry => new { entry.UserId, entry.AccountName })
                    .Distinct()
                    .OrderBy(entry => entry.AccountName)
                    .Take(500)
                    .ToListAsync();
                model.Users = users.Select(entry => new SelectListItem
                {
                    Value = entry.UserId!,
                    Text = entry.AccountName ?? "Former user"
                }).ToList();
            }
        }

        private static string CsvField(string value)
        {
            if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
                value = $"'{value}";
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }
}
