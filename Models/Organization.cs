using System.ComponentModel.DataAnnotations;

namespace Vahini.Models;

public sealed class Organization
{
    [Key]
    public Guid Id { get; init; } = Guid.NewGuid();

    [Required]
    [StringLength(100)]
    public required string Name { get; set; }

    [Required]
    [StringLength(6, MinimumLength = 6)]
    public required string AccessCode { get; set; }
    
    public ICollection<ApplicationUser> Members { get; set; } = [];
    public ICollection<DataBatch> Batches { get; set; } = [];
}
