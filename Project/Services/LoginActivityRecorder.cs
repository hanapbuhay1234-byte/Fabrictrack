using FabriTrack.Data;
using FabriTrack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace FabriTrack.Services
{
    public class LoginActivityRecorder
    {
        private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(90);
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<LoginActivityRecorder> _logger;

        public LoginActivityRecorder(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor,
            ILogger<LoginActivityRecorder> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<bool> RecordAsync(
            ApplicationUser? user,
            string eventType,
            string? accountIdentifier = null,
            CancellationToken cancellationToken = default)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var userAgent = httpContext?.Request.Headers.UserAgent.ToString();
            if (userAgent?.Length > 512)
                userAgent = userAgent[..512];
            var ipAddress = httpContext?.Connection.RemoteIpAddress?.ToString();
            if (ipAddress?.Length > 45)
                ipAddress = ipAddress[..45];
            var normalizedIdentifier = accountIdentifier?.Trim();
            if (normalizedIdentifier?.Length > 256)
                normalizedIdentifier = normalizedIdentifier[..256];
            var accountName = user?.FullName;
            if (accountName?.Length > 256)
                accountName = accountName[..256];

            var now = DateTime.UtcNow;
            var activity = new LoginActivity
            {
                UserId = user?.Id,
                AccountName = accountName,
                AccountIdentifier = normalizedIdentifier,
                EventType = eventType,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                OccurredAt = now
            };

            try
            {
                _context.LoginActivities.Add(activity);
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Could not persist login activity {EventType}.", eventType);
                return false;
            }
            catch (DbException exception)
            {
                _logger.LogError(exception, "Could not persist login activity {EventType}.", eventType);
                return false;
            }

            try
            {
                await _context.LoginActivities
                    .Where(expired => expired.OccurredAt < now - RetentionPeriod)
                    .ExecuteDeleteAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Login activity was recorded, but expired entries could not be pruned.");
            }
            catch (DbException exception)
            {
                _logger.LogError(exception, "Login activity was recorded, but expired entries could not be pruned.");
            }

            _logger.LogInformation(
                "Recorded login activity {EventType} for account {UserId}.",
                eventType,
                user?.Id ?? "unrecognized");
            return true;
        }
    }
}
