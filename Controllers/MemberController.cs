using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vahini.Data;
using Vahini.Models;
using Vahini.Services;

namespace Vahini.Controllers;

[Authorize(Roles = "Member")]
public sealed class MemberController(
    ApplicationDbContext db,
    WorkflowService workflow,
    ILogger<MemberController> logger) : Controller
{
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        try
        {
            var batches = await db.DataBatches
                .AsNoTracking()
                .Where(b =>
                    b.Status == BatchStatus.Pending ||
                    (b.Status == BatchStatus.InProgress && b.AssignedToId == userId))
                .OrderByDescending(b => b.Priority)
                .ThenBy(b => b.CreatedAt)
                .ToListAsync();

            return View(batches);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching work queue for user {UserId}.", userId);
            TempData["Error"] = "Unable to load work queue. Please try again later.";
            return View(new List<DataBatch>());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Claim(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        try
        {
            var batch = await db.DataBatches.FindAsync(id);
            if (batch is null)
                return NotFound();

            if (!workflow.CanTransition(batch.Status, BatchStatus.InProgress, WorkflowService.RoleMember))
            {
                TempData["Error"] = "This batch is no longer available to claim.";
                return RedirectToAction(nameof(Index));
            }

            batch.Status       = BatchStatus.InProgress;
            batch.AssignedToId = userId;
            batch.UpdatedAt    = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync();
            logger.LogInformation("Batch {Id} transitioned to {Status} by User {User}.", batch.Id, batch.Status, User.Identity?.Name);
            TempData["Success"] = $"You have claimed \"{batch.Title}\".";
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "Concurrency conflict claiming batch {Id} by {User}.", id, User.Identity?.Name);
            TempData["Error"] = "This batch was claimed by someone else. Please choose another.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error claiming batch {Id} for user {UserId}.", id, userId);
            TempData["Error"] = "An error occurred while claiming the batch.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        try
        {
            var batch = await db.DataBatches.FindAsync(id);
            if (batch is null)
                return NotFound();

            if (batch.AssignedToId != userId)
                return Forbid();

            if (!workflow.CanTransition(batch.Status, BatchStatus.InReview, WorkflowService.RoleMember))
            {
                TempData["Error"] = $"Batch cannot be submitted from status '{batch.Status}'.";
                return RedirectToAction(nameof(Index));
            }

            batch.Status    = BatchStatus.InReview;
            batch.UpdatedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync();

            logger.LogInformation("Batch {Id} transitioned to {Status} by User {User}.", batch.Id, batch.Status, User.Identity?.Name);
            TempData["Success"] = $"\"{batch.Title}\" submitted for review.";
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "Concurrency conflict submitting batch {Id} by {User}.", id, User.Identity?.Name);
            TempData["Error"] = "The batch was modified by another user. Please try again.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error submitting batch {Id} for user {UserId}.", id, userId);
            TempData["Error"] = "An error occurred while submitting the batch.";
        }

        return RedirectToAction(nameof(Index));
    }
}
