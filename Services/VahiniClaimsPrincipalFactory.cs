using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Vahini.Models;

namespace Vahini.Services;

/// <summary>
/// Bakes multi-tenancy and approval claims directly into the Identity cookie,
/// eliminating the need for extra database round-trips per request.
/// </summary>
public sealed class VahiniClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> optionsAccessor)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(userManager, roleManager, optionsAccessor)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        if (user.OrganizationId.HasValue)
        {
            identity.AddClaim(new Claim("OrganizationId", user.OrganizationId.Value.ToString()));
        }

        identity.AddClaim(new Claim("IsApproved", user.IsApproved.ToString()));
        identity.AddClaim(new Claim("IsOrgAdmin", user.IsOrgAdmin.ToString()));

        return identity;
    }
}
