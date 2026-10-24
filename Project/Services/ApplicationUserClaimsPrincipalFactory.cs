using System.Security.Claims;
using FabriTrack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace FabriTrack.Services
{
    public class ApplicationUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
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
            var identity = await base.GenerateClaimsAsync(user);
            if (user.RequirePasswordChange)
                identity.AddClaim(new Claim(PasswordChangeRequiredClaim.Type, "true"));
            return identity;
        }
    }
}
