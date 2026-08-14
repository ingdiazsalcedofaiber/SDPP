using SDPP.BuildingBlocks.Domain;

namespace SDPP.Identity.Domain.Aggregates;

/// <summary>An email domain allowed to log in (e.g. "empresa.com"). <see cref="IsDevOnly"/> marks
/// entries (like a personal "gmail.com" override) that must only ever be honored when the API is
/// running in Development — enforced by the caller passing the current environment, not by this
/// entity, which has no notion of environments.</summary>
public sealed class AllowedDomain : AggregateRoot<Guid>
{
    public string Domain { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public bool IsDevOnly { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public string? Notes { get; private set; }

    private AllowedDomain() { } // EF Core

    public static AllowedDomain Create(string domain, bool isDevOnly, string? notes)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            throw new DomainException("El dominio no puede estar vacío.");
        }

        return new AllowedDomain
        {
            Id = Guid.NewGuid(),
            Domain = domain.Trim().ToLowerInvariant(),
            IsActive = true,
            IsDevOnly = isDevOnly,
            CreatedAtUtc = DateTime.UtcNow,
            Notes = notes,
        };
    }

    public void Deactivate() => IsActive = false;
}
