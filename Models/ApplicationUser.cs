using Microsoft.AspNetCore.Identity;

namespace Vahini.Models;

public sealed class ApplicationUser : IdentityUser
{
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public bool IsApproved { get; set; } = false;
    public bool IsOrgAdmin { get; set; } = false;
}
