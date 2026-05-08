using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Vahini.Models;

namespace Vahini.Data;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IHttpContextAccessor httpContextAccessor) 
    : IdentityDbContext<ApplicationUser>(options)
{
    private readonly Guid _currentOrgId = Guid.TryParse(
        httpContextAccessor.HttpContext?.User?.FindFirst("OrganizationId")?.Value, out var id) 
        ? id : Guid.Empty;

    private readonly bool _isUserApproved = bool.TryParse(
        httpContextAccessor.HttpContext?.User?.FindFirst("IsApproved")?.Value, out var approved) 
        && approved;

    public DbSet<DataBatch> DataBatches => Set<DataBatch>();
    public DbSet<Organization> Organizations => Set<Organization>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<DataBatch>().Where(e => e.State == EntityState.Added))
        {
            if (entry.Entity.OrganizationId == Guid.Empty && _currentOrgId != Guid.Empty)
            {
                entry.Entity.OrganizationId = _currentOrgId;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Data Isolation Shield
        builder.Entity<DataBatch>().HasQueryFilter(b => b.OrganizationId == _currentOrgId && _isUserApproved);

        builder.Entity<DataBatch>(entity =>
        {
            entity.ToTable("data_batches");
            entity.HasKey(b => b.Id);
            
            entity.Property(b => b.Version).IsRowVersion();

            entity.Property(b => b.Title).HasMaxLength(256).IsRequired();
            entity.Property(b => b.DataUrl).HasMaxLength(2048).IsRequired();
            
            entity.Property(b => b.QualityScore).HasColumnType("real");
            
            entity.Property(b => b.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(b => b.Priority).HasConversion<string>().HasMaxLength(16);
            
            entity.Property(b => b.CreatedAt).HasDefaultValueSql("now()");

            entity.HasIndex(b => b.Status).HasDatabaseName("ix_data_batches_status");
            entity.HasIndex(b => b.AssignedToId).HasDatabaseName("ix_data_batches_assigned_to_id");
            entity.HasIndex(b => new { b.Status, b.Priority }).HasDatabaseName("ix_data_batches_status_priority");
        });
    }
}
