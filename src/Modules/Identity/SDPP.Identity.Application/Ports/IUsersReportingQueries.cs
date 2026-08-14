namespace SDPP.Identity.Application.Ports;

/// <summary>Same shape/purpose as Documents.Application.Ports.TimeBucket — kept as its own copy
/// rather than a shared reference, consistent with how this codebase already keeps each bounded
/// context's small cross-cutting enums independent (see ClassificationLevel).</summary>
public enum ReportingTimeBucket
{
    Day,
    Week,
    Month,
}

public sealed record UsersTimeSeriesPoint(DateTime PeriodStart, int Count);

public sealed record UsersOverview(int TotalUsers, int ActiveUsers, IReadOnlyList<UsersTimeSeriesPoint> NewUsersTimeSeries);

/// <summary>Read-only aggregate queries for the admin dashboard's "usuarios" panel — raw Dapper SQL
/// against SDPP_Identity, same rationale as Documents.Application.Ports.IDocumentsReportingQueries
/// (aggregate reads with no domain invariants to protect, no reason to route through EF Core's
/// change tracking).</summary>
public interface IUsersReportingQueries
{
    Task<UsersOverview> GetOverviewAsync(DateTime fromUtc, DateTime toUtc, ReportingTimeBucket bucket, CancellationToken cancellationToken = default);
}
