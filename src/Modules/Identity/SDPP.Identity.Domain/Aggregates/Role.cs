using SDPP.BuildingBlocks.Domain;

namespace SDPP.Identity.Domain.Aggregates;

/// <summary>
/// A role stored in the database (not hardcoded) — administrators can add more later through the
/// admin panel. <see cref="IsSystemRole"/> marks the 2 seeded defaults (Usuario/Administrador,
/// plus the pre-existing Auditor role every other module's RBAC already checks) as
/// non-deletable, everything else is free-form.
/// </summary>
public sealed class Role : AggregateRoot<Guid>
{
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsSystemRole { get; private set; }

    private Role() { } // EF Core

    public static Role Create(string name, string? description, bool isSystemRole)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("El nombre del rol no puede estar vacío.");
        }

        return new Role
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            IsSystemRole = isSystemRole,
        };
    }
}
