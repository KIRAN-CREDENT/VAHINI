using Vahini.Models;

namespace Vahini.ViewModels;

// Aggregated snapshot of the VAHINI pipeline presented on the Admin dashboard.
// All counts are fetched concurrently — see AdminController.Index.
public sealed class AdminDashboardViewModel
{
    // ── Pipeline Counters ─────────────────────────────────────────────────────

    public int TotalPending    { get; init; }
    public int TotalInProgress { get; init; }
    public int TotalInReview   { get; init; }
    public int TotalCompleted  { get; init; }

    // Derived total — eliminates the need for a separate COUNT(*) query.
    public int GrandTotal => TotalPending + TotalInProgress + TotalInReview + TotalCompleted;

    // ── Recent Activity ───────────────────────────────────────────────────────

    // Latest 10 batches across all statuses, ordered newest-first.
    public List<DataBatch> RecentBatches { get; init; } = [];

    public List<ApplicationUser> PendingApprovals { get; init; } = [];
}
