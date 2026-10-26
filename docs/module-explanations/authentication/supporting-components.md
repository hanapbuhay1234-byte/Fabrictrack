# Supporting authentication models and services

These components provide the stored account state, temporary-password gate, and separate sign-in activity records used by the pages in this folder.

## `ApplicationUser`

**Live file:** [`ApplicationUser.cs`](../../../Project/Models/ApplicationUser.cs)

```csharp
using Microsoft.AspNetCore.Identity;

namespace FabriTrack.Models
{
    // Extends ASP.NET Identity's user record with fields required by FabriTrack.
    public class ApplicationUser : IdentityUser
    {
        // Friendly name used in the UI and activity snapshots.
        public string FullName { get; set; } = string.Empty;

        // Private stored filename for the account's profile photo; not the image itself.
        public string? ProfilePhotoFileName { get; set; }

        // FabriTrack account-level access switch, checked before password sign-in.
        public bool IsActive { get; set; } = true;

        // Set for temporary/reset passwords and cleared by ChangePasswordModel.
        public bool RequirePasswordChange { get; set; }
    }

    // Central names prevent role checks from scattering string literals.
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Staff = "Staff";
    }
}
```

## Forced-password-change claim and claims factory

**Live files:** [`PasswordChangeRequiredClaim.cs`](../../../Project/Models/PasswordChangeRequiredClaim.cs), [`ApplicationUserClaimsPrincipalFactory.cs`](../../../Project/Services/ApplicationUserClaimsPrincipalFactory.cs)

```csharp
// PasswordChangeRequiredClaim.cs
namespace FabriTrack.Models
{
    public static class PasswordChangeRequiredClaim
    {
        // A named claim lets the request middleware detect temporary-password users.
        public const string Type = "fabritrack:password-change-required";
    }
}

// ApplicationUserClaimsPrincipalFactory.cs
using System.Security.Claims;
using FabriTrack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace FabriTrack.Services
{
    // Identity calls this factory when it builds the authenticated ClaimsPrincipal.
    public class ApplicationUserClaimsPrincipalFactory
        : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
    {
        public ApplicationUserClaimsPrincipalFactory(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IOptions<IdentityOptions> options)
            : base(userManager, roleManager, options)
        {
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
        {
            // Preserve standard Identity claims and role claims.
            var identity = await base.GenerateClaimsAsync(user);

            // Store the temporary-password requirement in the authenticated principal
            // so request middleware can enforce the change on every request.
            if (user.RequirePasswordChange)
                identity.AddClaim(new Claim(PasswordChangeRequiredClaim.Type, "true"));
            return identity;
        }
    }
}
```

## Request gate and Identity configuration

**Live file:** [`Program.cs`](../../../Project/Program.cs)

The following excerpt shows the authentication-related setup. It is an excerpt, not a replacement copy of the complete application startup file.

```csharp
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;

    // Identity applies its password validators to new passwords and resets.
    options.Password.RequiredLength = 10;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.User.RequireUniqueEmail = true;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Replace the default claims factory with one that adds the forced-change claim.
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>,
    ApplicationUserClaimsPrincipalFactory>();

// Use the same activity recorder from sign-in and sign-out handlers.
builder.Services.AddScoped<LoginActivityRecorder>();

// Revalidate the Identity security stamp on each validation cycle.
builder.Services.Configure<SecurityStampValidatorOptions>(
    options => options.ValidationInterval = TimeSpan.Zero);

app.UseAuthentication();
app.Use(async (context, next) =>
{
    // A user with the temporary-password claim can only visit the password-change
    // and logout routes until the claim is removed by a refreshed sign-in.
    if (context.User.Identity?.IsAuthenticated == true &&
        context.User.HasClaim(PasswordChangeRequiredClaim.Type, "true") &&
        !context.Request.Path.StartsWithSegments("/Identity/Account/ChangePassword") &&
        !context.Request.Path.StartsWithSegments("/Identity/Account/Logout"))
    {
        context.Response.Redirect("/Identity/Account/ChangePassword");
        return;
    }

    await next();
});
app.UseAuthorization();
```

The lockout threshold and duration are not explicitly set in this excerpt; Identity's configured/default `LockoutOptions` determine those values. `LoginModel` passes `lockoutOnFailure: true` when checking passwords.

## Login Activity entity and recorder

**Live files:** [`LoginActivity.cs`](../../../Project/Models/LoginActivity.cs), [`LoginActivityRecorder.cs`](../../../Project/Services/LoginActivityRecorder.cs)

`LoginActivity` is separate from the business `AuditLog` entity. Its fields capture an event type and time, optional user/account snapshots, and limited request context. The recorder never accepts or stores password or authenticator-code values.

### Complete annotated `LoginActivity` model

```csharp
using System.ComponentModel.DataAnnotations;

namespace FabriTrack.Models
{
    public class LoginActivity
    {
        // Database primary key; generated by the database.
        public long LoginActivityId { get; set; }

        // Null for events where no known account matched the attempted identifier.
        [StringLength(450)]
        public string? UserId { get; set; }

        // Snapshot means historical activity still has a readable name if the
        // account is later renamed or deleted.
        [StringLength(256)]
        public string? AccountName { get; set; }

        // Optional submitted account identifier for administrator investigation.
        // Passwords and authenticator codes must never be placed here.
        [StringLength(256)]
        public string? AccountIdentifier { get; set; }

        // Examples include successful sign-in, failed sign-in, and signed out.
        [Required, StringLength(50)]
        public string EventType { get; set; } = string.Empty;

        // Remote address as seen by the app server; forwarded headers are not
        // trusted unless proxy configuration explicitly establishes that trust.
        [StringLength(45)]
        public string? IpAddress { get; set; }

        // Capped request user-agent snapshot, not a guaranteed precise device ID.
        [StringLength(512)]
        public string? UserAgent { get; set; }

        // Activity timestamps are written in UTC by LoginActivityRecorder.
        public DateTime OccurredAt { get; set; }
    }
}
```

### Complete annotated `LoginActivityRecorder` service

```csharp
using FabriTrack.Data;
using FabriTrack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace FabriTrack.Services
{
    public class LoginActivityRecorder
    {
        // Keep recent activity and periodically remove older rows after a new write.
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

        // Returns false only when the new event itself could not be persisted.
        // Callers decide whether to proceed with their main operation; logout, for
        // example, still signs out when activity persistence fails.
        public async Task<bool> RecordAsync(
            ApplicationUser? user,
            string eventType,
            string? accountIdentifier = null,
            CancellationToken cancellationToken = default)
        {
            var httpContext = _httpContextAccessor.HttpContext;

            // Capture only request metadata, never posted passwords or 2FA codes.
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

            // Persist the new record first so retention cleanup failure cannot
            // prevent the event that triggered cleanup from being stored.
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

            // Delete only expired LoginActivity rows; this does not touch Audit Logs
            // or other business data. A cleanup failure is reported separately.
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
```

### Recording and retention behavior

1. Reads the request's remote IP and user-agent, then caps their lengths to fit the schema.
2. Builds an event with a UTC timestamp and optional user/account snapshots.
3. Saves the event, logging a database failure and returning `false` if the write fails.
4. Prunes activity older than 90 days after a successful write. Pruning failure is logged separately and does not undo the new event.
