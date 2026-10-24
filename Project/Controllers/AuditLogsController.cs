using System.Globalization;
using System.Text;
using FabriTrack.Data;
using FabriTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FabriTrack.Controllers
{
    [Authorize(Roles = Roles.Admin + "," + Roles.Staff)]
    public class AuditLogsController : Controller
    {
        private const int PageSize = 25;
        private readonly ApplicationDbContext _context;

        public AuditLogsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(
            string? search,
            string? actor,
            [FromQuery(Name = "action")] string? action,
            string? entityType,
            DateTime? startDate,
            DateTime? endDate,
            int page = 1)
        {
            var model = new AuditLogViewModel
            {
                Search = search?.Trim(),
                Actor = actor,
                Action = action,
                EntityType = entityType,
                StartDate = startDate?.Date,
                EndDate = endDate?.Date,
                Page = Math.Max(1, page),
                PageSize = PageSize
            };

            await PopulateFilterOptionsAsync(model);
            if (model.StartDate.HasValue && model.EndDate.HasValue && model.StartDate > model.EndDate)
            {
                ModelState.AddModelError(nameof(model.EndDate), "The end date must be on or after the start date.");
                return View(model);
            }

            var query = FilterLogs(model);
            model.TotalRecords = await query.CountAsync();
            model.Page = Math.Min(model.Page, model.TotalPages);
            model.Entries = await query
                .OrderByDescending(entry => entry.OccurredAt)
                .ThenByDescending(entry => entry.AuditLogId)
                .Skip((model.Page - 1) * model.PageSize)
                .Take(model.PageSize)
                .ToListAsync();

            return View(model);
        }

        public async Task<IActionResult> Export(
            string? search,
            string? actor,
            [FromQuery(Name = "action")] string? action,
            string? entityType,
            DateTime? startDate,
            DateTime? endDate)
        {
            var model = new AuditLogViewModel
            {
                Search = search?.Trim(),
                Actor = actor,
                Action = action,
                EntityType = entityType,
                StartDate = startDate?.Date,
                EndDate = endDate?.Date
            };

            if (model.StartDate.HasValue && model.EndDate.HasValue && model.StartDate > model.EndDate)
            {
                TempData["Error"] = "The end date must be on or after the start date.";
                return RedirectToAction(nameof(Index), new
                {
                    search, actor, action, entityType, startDate, endDate
                });
            }

            var entries = await FilterLogs(model)
                .OrderByDescending(entry => entry.OccurredAt)
                .ThenByDescending(entry => entry.AuditLogId)
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Date (UTC),Actor,Action,Record type,Record,Record ID,Details");
            foreach (var entry in entries)
            {
                csv.Append(CsvField(entry.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))).Append(',')
                    .Append(CsvField(entry.ActorName)).Append(',')
                    .Append(CsvField(entry.Action)).Append(',')
                    .Append(CsvField(entry.EntityType)).Append(',')
                    .Append(CsvField(entry.EntityName ?? string.Empty)).Append(',')
                    .Append(CsvField(entry.EntityId ?? string.Empty)).Append(',')
                    .Append(CsvField(entry.Details ?? string.Empty)).AppendLine();
            }

            return File(
                new UTF8Encoding(true).GetBytes(csv.ToString()),
                "text/csv; charset=utf-8",
                $"audit-logs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
        }

        private IQueryable<AuditLog> FilterLogs(AuditLogViewModel filters)
        {
            var query = _context.AuditLogs.AsNoTracking();
            if (!User.IsInRole(Roles.Admin))
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                query = userId == null
                    ? query.Where(entry => false)
                    : query.Where(entry => entry.ActorUserId == userId);
            }

            if (!string.IsNullOrWhiteSpace(filters.Search))
            {
                var term = filters.Search;
                query = query.Where(entry =>
                    entry.ActorName.Contains(term) ||
                    entry.Action.Contains(term) ||
                    entry.EntityType.Contains(term) ||
                    (entry.EntityName != null && entry.EntityName.Contains(term)) ||
                    (entry.EntityId != null && entry.EntityId.Contains(term)) ||
                    (entry.Details != null && entry.Details.Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(filters.Actor))
                query = query.Where(entry => entry.ActorName == filters.Actor);
            if (!string.IsNullOrWhiteSpace(filters.Action))
                query = query.Where(entry => entry.Action == filters.Action);
            if (!string.IsNullOrWhiteSpace(filters.EntityType))
                query = query.Where(entry => entry.EntityType == filters.EntityType);
            if (filters.StartDate.HasValue)
                query = query.Where(entry => entry.OccurredAt >= filters.StartDate.Value);
            if (filters.EndDate.HasValue)
            {
                var endExclusive = filters.EndDate.Value.AddDays(1);
                query = query.Where(entry => entry.OccurredAt < endExclusive);
            }

            return query;
        }

        private async Task PopulateFilterOptionsAsync(AuditLogViewModel model)
        {
            var visibleLogs = FilterLogs(new AuditLogViewModel());
            model.Actors = await visibleLogs
                .Select(entry => entry.ActorName)
                .Distinct()
                .OrderBy(name => name)
                .Take(200)
                .ToListAsync();
            model.Actions = await visibleLogs
                .Select(entry => entry.Action)
                .Distinct()
                .OrderBy(name => name)
                .ToListAsync();
            model.EntityTypes = await visibleLogs
                .Select(entry => entry.EntityType)
                .Distinct()
                .OrderBy(name => name)
                .ToListAsync();
        }

        private static string CsvField(string value)
        {
            if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
                value = $"'{value}";
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }
}
