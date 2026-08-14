using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using SDPP.BuildingBlocks.Application;
using SDPP.BuildingBlocks.Contracts.Identity;
using SDPP.Identity.Application.Configuration;
using SDPP.Identity.Application.Ports;
using SDPP.Identity.Application.Text;
using SDPP.Identity.Domain;
using SDPP.Identity.Domain.Aggregates;
using SDPP.Identity.Domain.Entities;
using SDPP.Identity.Domain.Enums;

namespace SDPP.Identity.Application.Services;

public sealed record LoginContext(
    string Provider, string ProviderId, string Email, string FullName, string? PhotoUrl,
    bool EmailVerified, string? HostedDomain, string? IpAddress, string? UserAgent, bool IsDevelopment);

public sealed record UserDto(Guid Id, string FullName, string Email, string? PhotoUrl, string Domain, IReadOnlyList<string> Roles);

public sealed record LoginOutcome(
    bool Success, string? ErrorCode, string? ErrorMessage,
    UserDto? User, string? AccessToken, DateTime? AccessTokenExpiresAtUtc, string? RawRefreshToken, Guid? SessionId,
    IReadOnlyList<string>? BackupCodes = null)
{
    public static LoginOutcome Failed(string errorCode, string errorMessage) => new(false, errorCode, errorMessage, null, null, null, null, null);

    public static LoginOutcome Succeeded(
        UserDto user, string accessToken, DateTime accessTokenExpiresAtUtc, string rawRefreshToken, Guid sessionId, IReadOnlyList<string>? backupCodes = null) =>
        new(true, null, null, user, accessToken, accessTokenExpiresAtUtc, rawRefreshToken, sessionId, backupCodes);
}

/// <summary>First-factor result: with MFA mandatory platform-wide, a Google login never resolves
/// directly into a session anymore — it always ends in one of these two challenge states (or a
/// failure). Only <see cref="ILoginCompletionService.CompleteMfaEnrollmentAsync"/>/
/// <see cref="ILoginCompletionService.VerifyMfaAsync"/>, once the second factor is proven, produce
/// a real <see cref="LoginOutcome"/>.</summary>
public sealed record FirstFactorOutcome(
    bool Success, string? ErrorCode, string? ErrorMessage,
    bool RequiresEnrollment, string? RawChallengeToken, DateTime? ChallengeExpiresAtUtc,
    string? QrCodeDataUri, string? ManualEntryKey)
{
    public static FirstFactorOutcome Failed(string errorCode, string errorMessage) =>
        new(false, errorCode, errorMessage, false, null, null, null, null);

    public static FirstFactorOutcome EnrollmentRequired(string rawChallengeToken, DateTime expiresAtUtc, string qrCodeDataUri, string manualEntryKey) =>
        new(true, null, null, true, rawChallengeToken, expiresAtUtc, qrCodeDataUri, manualEntryKey);

    public static FirstFactorOutcome ChallengeRequired(string rawChallengeToken, DateTime expiresAtUtc) =>
        new(true, null, null, false, rawChallengeToken, expiresAtUtc, null, null);
}

/// <summary>
/// The single shared core the real Google login funnels through (see docs on the auth architecture,
/// "Authentication Service → User Service → Session Manager → Role Manager → Audit Service").
/// Resolves domain policy → upserts the user → checks Active → issues an MFA challenge (mandatory
/// from the first login, see <see cref="AuthenticateFirstFactorAsync"/>) → and only once that
/// second factor is proven (<see cref="CompleteMfaEnrollmentAsync"/>/<see cref="VerifyMfaAsync"/>)
/// issues the real session, all inside one transaction per step.
/// </summary>
public interface ILoginCompletionService
{
    Task<FirstFactorOutcome> AuthenticateFirstFactorAsync(LoginContext context, CancellationToken cancellationToken = default);

    Task<LoginOutcome> CompleteMfaEnrollmentAsync(
        string rawChallengeToken, string code, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);

    Task<LoginOutcome> VerifyMfaAsync(
        string rawChallengeToken, string code, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);
}

public sealed class LoginCompletionService(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    ISessionRepository sessionRepository,
    IAllowedDomainRepository allowedDomainRepository,
    IAccessAuditLogRepository accessAuditLogRepository,
    IMfaChallengeRepository mfaChallengeRepository,
    IAccessTokenIssuer accessTokenIssuer,
    ITotpService totpService,
    IMfaSecretProtector mfaSecretProtector,
    IUnitOfWork unitOfWork,
    IIntegrationEventPublisher integrationEventPublisher,
    IOptions<IdentityPolicyOptions> options)
    : ILoginCompletionService
{
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(10);
    private const int BackupCodeCount = 10;

    public async Task<FirstFactorOutcome> AuthenticateFirstFactorAsync(LoginContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var domain = ResolveDomain(context.HostedDomain, context.Email);
        var includeDevOnly = context.IsDevelopment && options.Value.AllowDevDomainOverride;

        if (!await allowedDomainRepository.IsAllowedAsync(domain, includeDevOnly, cancellationToken))
        {
            await RecordAttemptAsync(null, context, AccessResult.DomainRejected, stopwatch, null, cancellationToken);
            return FirstFactorOutcome.Failed("DOMAIN_NOT_ALLOWED", $"El dominio '{domain}' no está autorizado para acceder a la plataforma.");
        }

        var user = await userRepository.GetByGoogleIdAsync(context.ProviderId, cancellationToken)
            ?? await userRepository.GetByEmailAsync(context.Email, cancellationToken);

        if (user is null)
        {
            var usuarioRole = await roleRepository.GetByNameAsync(WellKnownRoles.Usuario, cancellationToken)
                ?? throw new InvalidOperationException($"El rol '{WellKnownRoles.Usuario}' no está sembrado.");

            user = User.Create(context.ProviderId, context.FullName, context.Email, context.PhotoUrl, domain, context.EmailVerified, usuarioRole.Id);
            userRepository.Add(user);

            if (options.Value.BootstrapAdminEmails.Contains(context.Email, StringComparer.OrdinalIgnoreCase))
            {
                var adminRole = await roleRepository.GetByNameAsync(WellKnownRoles.Administrador, cancellationToken)
                    ?? throw new InvalidOperationException($"El rol '{WellKnownRoles.Administrador}' no está sembrado.");
                user.GrantRoleIfMissing(adminRole.Id);
            }
        }
        else
        {
            user.RecordLogin(context.FullName, context.PhotoUrl, context.EmailVerified);
        }

        if (!user.Active)
        {
            await RecordAttemptAsync(user.Id, context, AccessResult.AccountInactive, stopwatch, null, cancellationToken);
            return FirstFactorOutcome.Failed("ACCOUNT_INACTIVE", "Tu cuenta está desactivada. Contacta a un administrador.");
        }

        var rawChallengeToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        if (!user.MfaEnabled)
        {
            var secret = totpService.GenerateSecret();
            user.BeginMfaEnrollment(mfaSecretProtector.Encrypt(secret.Base32Secret));

            var challenge = MfaChallenge.Issue(user.Id, HashToken(rawChallengeToken), MfaChallengePurpose.Enrollment, ChallengeLifetime);
            mfaChallengeRepository.Add(challenge);

            await RecordAttemptAsync(user.Id, context, AccessResult.MfaEnrollmentRequired, stopwatch, null, cancellationToken);

            var qrCodeDataUri = totpService.BuildQrCodeDataUri(user.Email, secret.Base32Secret);
            return FirstFactorOutcome.EnrollmentRequired(rawChallengeToken, challenge.ExpiresAtUtc, qrCodeDataUri, secret.ManualEntryKey);
        }
        else
        {
            var challenge = MfaChallenge.Issue(user.Id, HashToken(rawChallengeToken), MfaChallengePurpose.Login, ChallengeLifetime);
            mfaChallengeRepository.Add(challenge);

            await RecordAttemptAsync(user.Id, context, AccessResult.MfaChallengeIssued, stopwatch, null, cancellationToken);

            return FirstFactorOutcome.ChallengeRequired(rawChallengeToken, challenge.ExpiresAtUtc);
        }
    }

    public async Task<LoginOutcome> CompleteMfaEnrollmentAsync(
        string rawChallengeToken, string code, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var challenge = await mfaChallengeRepository.GetByTokenHashAsync(HashToken(rawChallengeToken), cancellationToken);
        if (challenge is null || challenge.Purpose != MfaChallengePurpose.Enrollment || !challenge.IsUsable)
        {
            return LoginOutcome.Failed("MFA_CHALLENGE_INVALID", "La sesión de activación de MFA expiró o ya no es válida. Vuelve a iniciar sesión.");
        }

        var user = await userRepository.GetByIdAsync(challenge.UserId, cancellationToken)
            ?? throw new InvalidOperationException("El usuario del challenge de MFA no existe.");

        if (user.MfaSecretEncrypted is null || !totpService.ValidateCode(mfaSecretProtector.Decrypt(user.MfaSecretEncrypted), code))
        {
            challenge.RegisterFailedAttempt();
            await RecordAttemptAsync(user.Id, ipAddress, userAgent, AccessResult.MfaVerificationFailed, stopwatch, null, cancellationToken, user.FullName, user.Email);
            return LoginOutcome.Failed("INVALID_CODE", "El código ingresado no es válido.");
        }

        var backupCodes = GenerateBackupCodes();
        user.ConfirmMfaEnrollment(backupCodes.Select(HashToken).ToList());
        challenge.Consume();

        return await IssueSessionAsync(user, ipAddress, userAgent, stopwatch, cancellationToken, backupCodes);
    }

    public async Task<LoginOutcome> VerifyMfaAsync(
        string rawChallengeToken, string code, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var challenge = await mfaChallengeRepository.GetByTokenHashAsync(HashToken(rawChallengeToken), cancellationToken);
        if (challenge is null || challenge.Purpose != MfaChallengePurpose.Login || !challenge.IsUsable)
        {
            return LoginOutcome.Failed("MFA_CHALLENGE_INVALID", "La sesión de verificación expiró o ya no es válida. Vuelve a iniciar sesión.");
        }

        var user = await userRepository.GetByIdAsync(challenge.UserId, cancellationToken)
            ?? throw new InvalidOperationException("El usuario del challenge de MFA no existe.");

        var validTotp = user.MfaSecretEncrypted is not null && totpService.ValidateCode(mfaSecretProtector.Decrypt(user.MfaSecretEncrypted), code);
        var validBackupCode = !validTotp && user.ConsumeBackupCodeIfValid(HashToken(NormalizeBackupCode(code)));

        if (!validTotp && !validBackupCode)
        {
            challenge.RegisterFailedAttempt();
            await RecordAttemptAsync(user.Id, ipAddress, userAgent, AccessResult.MfaVerificationFailed, stopwatch, null, cancellationToken, user.FullName, user.Email);
            return LoginOutcome.Failed("INVALID_CODE", "El código ingresado no es válido.");
        }

        challenge.Consume();

        return await IssueSessionAsync(user, ipAddress, userAgent, stopwatch, cancellationToken);
    }

    /// <summary>The tail every successful MFA completion shares: mint the JWT, open a real
    /// Session, and audit Success — this is exactly what the pre-MFA CompleteAsync used to do
    /// unconditionally, now reached only once the second factor is proven.</summary>
    private async Task<LoginOutcome> IssueSessionAsync(
        User user, string? ipAddress, string? userAgent, Stopwatch stopwatch, CancellationToken cancellationToken, IReadOnlyList<string>? backupCodes = null)
    {
        var allRoles = await roleRepository.GetAllAsync(cancellationToken);
        var roleNames = user.Roles.Select(ur => allRoles.First(r => r.Id == ur.RoleId).Name).ToList();

        var issued = accessTokenIssuer.Issue(user.Id, user.FullName, user.Email, user.Domain, user.Department, roleNames);

        var rawRefreshToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var session = Session.Issue(
            user.Id, HashToken(rawRefreshToken), ipAddress, userAgent,
            UserAgentHeuristics.ParseOperatingSystem(userAgent), TimeSpan.FromHours(options.Value.SessionAbsoluteLifetimeHours));
        sessionRepository.Add(session);

        await RecordAttemptAsync(user.Id, ipAddress, userAgent, AccessResult.Success, stopwatch, session.Id, cancellationToken, user.FullName, user.Email);

        return LoginOutcome.Succeeded(
            new UserDto(user.Id, user.FullName, user.Email, user.PhotoUrl, user.Domain, roleNames),
            issued.Token, issued.ExpiresAtUtc, rawRefreshToken, session.Id, backupCodes);
    }

    private static IReadOnlyList<string> GenerateBackupCodes()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I — avoids transcription errors
        var codes = new List<string>(BackupCodeCount);
        for (var i = 0; i < BackupCodeCount; i++)
        {
            var bytes = RandomNumberGenerator.GetBytes(8);
            codes.Add(new string(bytes.Select(b => alphabet[b % alphabet.Length]).ToArray()));
        }
        return codes;
    }

    private static string NormalizeBackupCode(string code) => code.Trim().ToUpperInvariant();

    private async Task RecordAttemptAsync(
        Guid? userId, LoginContext context, AccessResult result, Stopwatch stopwatch, Guid? sessionId, CancellationToken cancellationToken) =>
        await RecordAttemptAsync(userId, context.IpAddress, context.UserAgent, result, stopwatch, sessionId, cancellationToken, context.FullName, context.Email);

    private async Task RecordAttemptAsync(
        Guid? userId, string? ipAddress, string? userAgent, AccessResult result, Stopwatch stopwatch, Guid? sessionId,
        CancellationToken cancellationToken, string fullName = "", string email = "")
    {
        accessAuditLogRepository.Add(AccessAuditLogEntry.Record(
            userId, fullName, email, ipAddress, userAgent, result, "Google", (int)stopwatch.ElapsedMilliseconds, sessionId));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await integrationEventPublisher.PublishAsync(new AccessAttemptRecordedV1(
            Guid.NewGuid(), DateTime.UtcNow, userId, fullName, email, ipAddress, userAgent,
            result.ToString(), "Google", (int)stopwatch.ElapsedMilliseconds), cancellationToken);
    }

    /// <summary>Google's <c>hd</c> claim (hosted domain) only exists for Workspace accounts —
    /// personal Gmail logins fall back to the part after "@" in the email, which for gmail.com
    /// accounts is exactly "gmail.com" and is what the dev-only override / allowed-domains list
    /// matches against.</summary>
    private static string ResolveDomain(string? hostedDomain, string email) =>
        !string.IsNullOrWhiteSpace(hostedDomain) ? hostedDomain : email[(email.IndexOf('@') + 1)..];

    private static string HashToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));
}
