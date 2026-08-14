using SDPP.BuildingBlocks.Domain;
using SDPP.Identity.Domain.Entities;

namespace SDPP.Identity.Domain.Aggregates;

/// <summary>
/// A platform user, always provisioned from a Google login — there is no username/password
/// registration path anywhere in this aggregate. Every field except <see cref="Department"/>
/// (Google never exposes it) is populated straight from the validated Google ID token and
/// refreshed on every login (see <see cref="RecordLogin"/>), so the rest of the platform never
/// needs to ask the user for their name or email again.
/// </summary>
public sealed class User : AggregateRoot<Guid>
{
    private readonly List<UserRole> _roles = [];
    private readonly List<MfaBackupCode> _mfaBackupCodes = [];

    public string GoogleId { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string? PhotoUrl { get; private set; }
    public string Domain { get; private set; } = null!;
    public string? Department { get; private set; }
    public bool EmailVerified { get; private set; }
    public bool Active { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastLoginAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>MFA is mandatory platform-wide from the very first login — this only tracks
    /// whether enrollment has been *confirmed* yet, not whether a secret has been generated
    /// (see <see cref="BeginMfaEnrollment"/>, which stores a pending secret before this flips).</summary>
    public bool MfaEnabled { get; private set; }

    /// <summary>AES-GCM ciphertext of the TOTP secret (see IMfaSecretProtector) — never the raw
    /// secret. Set as soon as enrollment begins, even before <see cref="ConfirmMfaEnrollment"/>.</summary>
    public string? MfaSecretEncrypted { get; private set; }

    public DateTime? MfaEnrolledAtUtc { get; private set; }

    public IReadOnlyList<UserRole> Roles => _roles.AsReadOnly();
    public IReadOnlyList<MfaBackupCode> MfaBackupCodes => _mfaBackupCodes.AsReadOnly();

    private User() { } // EF Core

    public static User Create(
        string googleId, string fullName, string email, string? photoUrl, string domain, bool emailVerified, Guid initialRoleId)
    {
        if (string.IsNullOrWhiteSpace(googleId) || string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("El usuario requiere un Google ID y un correo válidos.");
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            GoogleId = googleId,
            FullName = fullName,
            Email = email,
            PhotoUrl = photoUrl,
            Domain = domain,
            EmailVerified = emailVerified,
            Active = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        user._roles.Add(UserRole.Create(user.Id, initialRoleId, assignedByUserId: null));
        return user;
    }

    /// <summary>Refreshes the profile snapshot from Google and stamps the login time — called on
    /// every successful login, not only the first one, per "actualizar automáticamente" requirement.</summary>
    public void RecordLogin(string fullName, string? photoUrl, bool emailVerified)
    {
        FullName = fullName;
        PhotoUrl = photoUrl;
        EmailVerified = emailVerified;
        LastLoginAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetActive(bool active)
    {
        Active = active;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Adds a role only if the user doesn't already have it — used for the
    /// bootstrap-admin-email grant, which must never duplicate the row on repeat logins.</summary>
    public void GrantRoleIfMissing(Guid roleId)
    {
        if (_roles.Any(r => r.RoleId == roleId))
        {
            return;
        }
        _roles.Add(UserRole.Create(Id, roleId, assignedByUserId: null));
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Replaces the full role set. Self-demotion (an admin removing their own
    /// Administrador role) is intentionally NOT blocked here — the aggregate has no way to know
    /// which role Guid means "Administrador"; that check belongs to the
    /// UpdateUserRoles use case, which knows both the acting user and the role's name.</summary>
    public void ReplaceRoles(IReadOnlyCollection<Guid> roleIds, Guid? changedByUserId)
    {
        _roles.Clear();
        foreach (var roleId in roleIds.Distinct())
        {
            _roles.Add(UserRole.Create(Id, roleId, changedByUserId));
        }
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Stores a freshly-generated (still unconfirmed) encrypted TOTP secret. Safe to call
    /// again before confirmation — a user who never scans the QR and retries login simply gets a
    /// fresh secret/QR each time, overwriting the abandoned one.</summary>
    public void BeginMfaEnrollment(string encryptedSecret)
    {
        MfaSecretEncrypted = encryptedSecret;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Activates MFA after the first correct TOTP code and replaces the backup code set —
    /// called exactly once, from the enrollment-confirmation use case.</summary>
    public void ConfirmMfaEnrollment(IReadOnlyCollection<string> backupCodeHashes)
    {
        MfaEnabled = true;
        MfaEnrolledAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
        _mfaBackupCodes.Clear();
        foreach (var hash in backupCodeHashes)
        {
            _mfaBackupCodes.Add(MfaBackupCode.Create(Id, hash));
        }
    }

    /// <summary>Consumes a backup code if it exists and hasn't been used yet — single use, per the
    /// "códigos de respaldo" recovery mechanism (no admin-reset path exists, see auth design docs).</summary>
    public bool ConsumeBackupCodeIfValid(string codeHash)
    {
        var match = _mfaBackupCodes.FirstOrDefault(c => c.CodeHash == codeHash && c.UsedAtUtc is null);
        if (match is null)
        {
            return false;
        }

        match.MarkUsed();
        UpdatedAtUtc = DateTime.UtcNow;
        return true;
    }
}
