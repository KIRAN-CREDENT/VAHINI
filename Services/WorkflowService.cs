using Vahini.Models;

namespace Vahini.Services;

public sealed class WorkflowService
{
    public const string RoleMember = "Member";
    public const string RoleAdmin  = "Admin";

    private static readonly Dictionary<(BatchStatus From, BatchStatus To), string> AllowedTransitions =
        new()
        {
            { (BatchStatus.Pending,    BatchStatus.InProgress), RoleMember },
            { (BatchStatus.InProgress, BatchStatus.InReview),   RoleMember },
            { (BatchStatus.InReview,   BatchStatus.Completed),  RoleAdmin  },
            { (BatchStatus.InReview,   BatchStatus.Rejected),   RoleAdmin  },
        };

    public bool CanTransition(BatchStatus current, BatchStatus next, string userRole)
    {
        if (current == next) return false;

        if (!AllowedTransitions.TryGetValue((current, next), out var requiredRole))
            return false;

        return requiredRole switch
        {
            RoleAdmin  => userRole == RoleAdmin,
            RoleMember => userRole is RoleMember or RoleAdmin,
            _          => false
        };
    }

    public IEnumerable<BatchStatus> GetReachableStates(BatchStatus current, string userRole) =>
        AllowedTransitions
            .Where(kv => kv.Key.From == current && CanTransition(current, kv.Key.To, userRole))
            .Select(kv => kv.Key.To);
}
