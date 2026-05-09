using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vahini.Data;
using Vahini.Models;
using Vahini.Services;
using Vahini.ViewModels;

namespace Vahini.Controllers;

[Authorize(Roles = "Admin")]
public sealed class AdminController(
    ApplicationDbContext db,
    WorkflowService workflow,
    ILogger<AdminController> logger) : Controller
{
    public async Task<IActionResult> Index()
    {
        try
        {
            var orgIdClaim = User.FindFirst("OrganizationId")?.Value;
            if (!Guid.TryParse(orgIdClaim, out var orgId)) return Forbid();

            var (pending, inProgress, inReview, completed, recent) = await FetchDashboardDataAsync();

            var pendingApprovals = await db.Users
                .AsNoTracking()
                .Where(u => u.OrganizationId == orgId && !u.IsApproved)
                .ToListAsync();

            var vm = new AdminDashboardViewModel
            {
                TotalPending    = pending,
                TotalInProgress = inProgress,
                TotalInReview   = inReview,
                TotalCompleted  = completed,
                RecentBatches   = recent,
                PendingApprovals = pendingApprovals
            };

            return View(vm);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching dashboard data.");
            TempData["Error"] = "Unable to load dashboard data. Please try again later.";
            return View(new AdminDashboardViewModel());
        }
    }

    [HttpGet]
    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(nameof(DataBatch.Title), nameof(DataBatch.DataUrl), nameof(DataBatch.Priority))]
        DataBatch model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var batch = new DataBatch
        {
            Title    = model.Title,
            DataUrl  = model.DataUrl,
            Priority = model.Priority,
            Status   = BatchStatus.Pending
        };

        try
        {
            db.DataBatches.Add(batch);
            await db.SaveChangesAsync();

            logger.LogInformation("Batch {Id} created by Admin.", batch.Id);
            TempData["Success"] = $"Batch \"{batch.Title}\" created successfully.";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[VAHINI BATCH CREATION FATAL ERROR]\n{ex.ToString()}\n");
            logger.LogError(ex, "Error creating batch {Title}.", batch.Title);
            TempData["Error"] = "An error occurred while creating the batch.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(Guid id, string action, float qualityScore = 0f)
    {
        try
        {
            var batch = await db.DataBatches.FindAsync(id);
            if (batch is null)
                return NotFound();

            var targetStatus = action switch
            {
                "Approve" => BatchStatus.Completed,
                "Reject"  => BatchStatus.Rejected,
                _         => (BatchStatus?)null
            };

            if (targetStatus is null)
            {
                ModelState.AddModelError(string.Empty, "Invalid review action.");
                return BadRequest(ModelState);
            }

            if (!workflow.CanTransition(batch.Status, targetStatus.Value, WorkflowService.RoleAdmin))
            {
                TempData["Error"] = $"Batch cannot be transitioned from {batch.Status} via action '{action}'.";
                return RedirectToAction(nameof(Index));
            }

            batch.Status       = targetStatus.Value;
            batch.QualityScore = Math.Clamp(qualityScore, 0f, 1f);
            batch.UpdatedAt    = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync();

            logger.LogInformation("Batch {Id} transitioned to {Status} by User {User}.", batch.Id, targetStatus.Value, User.Identity?.Name);
            TempData["Success"] = $"Batch \"{batch.Title}\" {(targetStatus == BatchStatus.Completed ? "approved" : "rejected")}.";
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "Concurrency conflict reviewing batch {Id}.", id);
            TempData["Error"] = "The batch was modified by another user. Please try again.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reviewing batch {Id}.", id);
            TempData["Error"] = "An error occurred while reviewing the batch.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveUser(string id)
    {
        var orgIdClaim = User.FindFirst("OrganizationId")?.Value;
        if (!Guid.TryParse(orgIdClaim, out var orgId)) return Forbid();

        var user = await db.Users.FindAsync(id);
        if (user is null || user.OrganizationId != orgId)
            return NotFound();

        user.IsApproved = true;
        await db.SaveChangesAsync();

        logger.LogInformation("User {UserId} approved by Admin {AdminId}.", user.Id, User.Identity?.Name);
        TempData["Success"] = $"User {user.Email} has been approved.";
        
        return RedirectToAction(nameof(Index));
    }

    private async Task<(int Pending, int InProgress, int InReview, int Completed, List<DataBatch> Recent)>
        FetchDashboardDataAsync()
    {
        var counts = await db.DataBatches
            .AsNoTracking()
            .GroupBy(b => b.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(k => k.Status, v => v.Count);

        var recent = await db.DataBatches
            .AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .Take(10)
            .ToListAsync();

        return (
            counts.GetValueOrDefault(BatchStatus.Pending, 0),
            counts.GetValueOrDefault(BatchStatus.InProgress, 0),
            counts.GetValueOrDefault(BatchStatus.InReview, 0),
            counts.GetValueOrDefault(BatchStatus.Completed, 0),
            recent
        );
    }
}
