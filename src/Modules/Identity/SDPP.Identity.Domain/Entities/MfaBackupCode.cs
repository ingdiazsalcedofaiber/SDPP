using SDPP.BuildingBlocks.Domain;

namespace SDPP.Identity.Domain.Entities;

/// <summary>One single-use recovery code, owned by the <c>User</c> aggregate (same ownership shape
/// as <c>UserRole</c>). Only <see cref="CodeHash"/> (SHA-256) is ever persisted — the plaintext
/// code exists only in the one-time enrollment response, never stored anywhere.</summary>
public sealed class MfaBackupCode : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public string CodeHash { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UsedAtUtc { get; private set; }

    private MfaBackupCode() { } // EF Core

    internal static MfaBackupCode Create(Guid userId, string codeHash) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        CodeHash = codeHash,
        CreatedAtUtc = DateTime.UtcNow,
    };

    public void MarkUsed() => UsedAtUtc ??= DateTime.UtcNow;
}
