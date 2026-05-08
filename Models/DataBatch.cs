using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vahini.Models;

public enum BatchStatus
{
    Pending,
    InProgress,
    InReview,
    Completed,
    Rejected
}

public enum BatchPriority
{
    Low,
    Medium,
    High
}

public sealed class DataBatch
{
    [Key]
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    [Required]
    [StringLength(256)]
    public required string Title { get; set; }

    [Required]
    [StringLength(2048)]
    public required string DataUrl { get; set; }

    public BatchStatus Status { get; set; } = BatchStatus.Pending;

    public BatchPriority Priority { get; set; } = BatchPriority.Medium;

    [Range(0f, 1f)]
    public float QualityScore { get; set; }

    public string? AssignedToId { get; set; }

    public DateTimeOffset CreatedAt  { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt  { get; set; }  = DateTimeOffset.UtcNow;

    public uint Version { get; set; }
}
