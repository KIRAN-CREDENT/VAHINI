using Microsoft.AspNetCore.Identity;
using Vahini.Models;
using Vahini.Services;

namespace Vahini.Data;

public static class DbInitializer
{
    private const string DefaultAdminEmail    = "admin@vahini.com";
    private static readonly string DefaultAdminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "Admin@123!";

    public static async Task SeedAsync(
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext)
    {
        await EnsureRoleAsync(roleManager, WorkflowService.RoleAdmin);
        await EnsureRoleAsync(roleManager, WorkflowService.RoleMember);

        // Seed Organization first
        var hqOrg = dbContext.Organizations.FirstOrDefault(o => o.Name == "Vahini HQ");
        if (hqOrg is null)
        {
            hqOrg = new Organization { Name = "Vahini HQ", AccessCode = "HQ2026" };
            dbContext.Organizations.Add(hqOrg);
            await dbContext.SaveChangesAsync();
        }

        var admin = await userManager.FindByEmailAsync(DefaultAdminEmail);

        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName       = DefaultAdminEmail,
                Email          = DefaultAdminEmail,
                EmailConfirmed = true,
                OrganizationId = hqOrg.Id,
                IsApproved     = true,
                IsOrgAdmin     = true
            };

            var result = await userManager.CreateAsync(admin, DefaultAdminPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create seed admin: {errors}");
            }
        }
        else if (admin.OrganizationId != hqOrg.Id)
        {
            admin.OrganizationId = hqOrg.Id;
            admin.IsApproved = true;
            admin.IsOrgAdmin = true;
            await userManager.UpdateAsync(admin);
        }

        if (!await userManager.IsInRoleAsync(admin, WorkflowService.RoleAdmin))
            await userManager.AddToRoleAsync(admin, WorkflowService.RoleAdmin);

        if (!dbContext.DataBatches.Any())
        {
            var seedBatches = new List<DataBatch>
            {
                new() { OrganizationId = hqOrg.Id, Title = "RLHF_Safety_Batch_001", DataUrl = "https://s3.data/rlhf_safety_001.json", Priority = BatchPriority.High, Status = BatchStatus.Pending },
                new() { OrganizationId = hqOrg.Id, Title = "Hindi_Audio_Transcription_v2", DataUrl = "https://s3.data/hi_transcribe_v2.json", Priority = BatchPriority.Medium, Status = BatchStatus.Pending },
                new() { OrganizationId = hqOrg.Id, Title = "Medical_Summary_Audit", DataUrl = "https://s3.data/med_audit.json", Priority = BatchPriority.High, Status = BatchStatus.InReview },
                new() { OrganizationId = hqOrg.Id, Title = "Image_Labeling_General", DataUrl = "https://s3.data/img_gen.json", Priority = BatchPriority.Low, Status = BatchStatus.Completed, QualityScore = 0.98f },
                new() { OrganizationId = hqOrg.Id, Title = "Financial_Entity_Extraction", DataUrl = "https://s3.data/fin_entities.json", Priority = BatchPriority.Medium, Status = BatchStatus.InProgress, AssignedToId = admin.Id }
            };

            dbContext.DataBatches.AddRange(seedBatches);
            await dbContext.SaveChangesAsync();
        }
    }

    private static async Task EnsureRoleAsync(RoleManager<IdentityRole> rm, string roleName)
    {
        if (!await rm.RoleExistsAsync(roleName))
            await rm.CreateAsync(new IdentityRole(roleName));
    }
}
