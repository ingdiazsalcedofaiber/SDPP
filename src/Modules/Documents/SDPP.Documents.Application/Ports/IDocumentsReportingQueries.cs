namespace SDPP.Documents.Application.Ports;

/// <summary>Granularity for time-series reporting queries — resolved to a fixed SQL date-bucketing
/// expression from a small C# switch (see DocumentsReportingQueries), never built from a raw
/// client-supplied string, so there's no injection surface even though this ultimately shapes a
/// GROUP BY clause.</summary>
public enum TimeBucket
{
    Day,
    Week,
    Month,
}

public sealed record StatusBreakdown(int Completed, int InProgress, int Failed);
public sealed record CountByLabel(string Label, int Count);
public sealed record TimeSeriesPoint(DateTime PeriodStart, int Count);

public sealed record RecentConversion(
    Guid JobId, string OriginalFileName, string OperationType, string Status, DateTime CreatedAtUtc, DateTime? CompletedAtUtc);

public sealed record DocumentsOverview(
    int TotalDocuments,
    int TotalConversions,
    StatusBreakdown ConversionsByStatus,
    IReadOnlyList<CountByLabel> ConversionsByType,
    IReadOnlyList<CountByLabel> DocumentsByContentType,
    IReadOnlyList<TimeSeriesPoint> TimeSeries,
    long StorageUsedBytes,
    long? StorageQuotaBytes,
    IReadOnlyList<RecentConversion> RecentConversions);

/// <summary>
/// Read-only aggregation queries backing the executive/personal dashboards (see the
/// dashboard-reporting proposal) — deliberately raw Dapper SQL against the same write database,
/// not EF Core: these are GROUP BY/aggregate reads with no domain invariants to protect, exactly
/// the case DocumentsDbContext's own doc comment earmarked for "query-side reads" from the start.
/// <paramref name="ownerId"/> null means platform-wide (admin scope); a real value scopes every
/// query to that owner (personal scope) — same method serves both, so there is only one SQL
/// definition of "what the numbers mean" to keep in sync.
/// </summary>
public interface IDocumentsReportingQueries
{
    Task<DocumentsOverview> GetOverviewAsync(
        Guid? ownerId, DateTime fromUtc, DateTime toUtc, TimeBucket bucket, CancellationToken cancellationToken = default);
}
