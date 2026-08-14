using SDPP.BuildingBlocks.Domain;

namespace SDPP.Identity.Domain.Entities;

/// <summary>Join entity for the many-to-many between <c>User</c> and <c>Role</c> — owned by the
/// <c>User</c> aggregate (role assignment is always a user-scoped operation).</summary>
public sealed class UserRole : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public DateTime AssignedAtUtc { get; private set; }
    public Guid? AssignedByUserId { get; private set; }

    private UserRole() { } // EF Core

    internal static UserRole Create(Guid userId, Guid roleId, Guid? assignedByUserId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        RoleId = roleId,
        AssignedAtUtc = DateTime.UtcNow,
        AssignedByUserId = assignedByUserId,
    };
}
