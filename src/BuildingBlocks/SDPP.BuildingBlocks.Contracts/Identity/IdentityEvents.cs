namespace SDPP.BuildingBlocks.Contracts.Identity;

/// <summary>Published for every login attempt — successful or not — so Audit's hash-chained
/// trail covers rejections (domain not allowed, inactive account, invalid token) as well as
/// successful logins, not just the fast local copy Identity.Api keeps for its own admin panel.</summary>
public sealed record AccessAttemptRecordedV1(
    Guid EventId,
    DateTime OccurredAtUtc,
    Guid? UserId,
    string FullNameSnapshot,
    string Email,
    string? IpAddress,
    string? UserAgent,
    string Result,
    string Provider,
    int DurationMs) : IIntegrationEvent;

/// <summary>Published when an administrator activates/deactivates a user from the admin panel.</summary>
public sealed record UserStatusChangedV1(
    Guid EventId,
    DateTime OccurredAtUtc,
    Guid UserId,
    string Email,
    bool Active,
    Guid ChangedByUserId) : IIntegrationEvent;

/// <summary>Published when an administrator changes a user's roles from the admin panel.</summary>
public sealed record UserRolesChangedV1(
    Guid EventId,
    DateTime OccurredAtUtc,
    Guid UserId,
    string Email,
    IReadOnlyList<string> Roles,
    Guid ChangedByUserId) : IIntegrationEvent;
