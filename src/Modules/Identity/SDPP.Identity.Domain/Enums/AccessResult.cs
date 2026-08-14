namespace SDPP.Identity.Domain.Enums;

/// <summary>Outcome of a single login attempt — recorded for every attempt, successful or not,
/// so "auditoría de acceso" covers rejections too, not just successful logins.</summary>
public enum AccessResult
{
    Success,
    Failed,
    DomainRejected,
    AccountInactive,
    InvalidToken,
    MfaEnrollmentRequired,
    MfaChallengeIssued,
    MfaVerificationFailed,
}
