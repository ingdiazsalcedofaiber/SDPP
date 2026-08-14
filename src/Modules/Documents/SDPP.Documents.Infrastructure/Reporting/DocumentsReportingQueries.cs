using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SDPP.Documents.Application.Ports;
using SDPP.Documents.Domain.Enums;

namespace SDPP.Documents.Infrastructure.Reporting;

public sealed class DocumentsReportingQueries(IConfiguration configuration) : IDocumentsReportingQueries
{
    public async Task<DocumentsOverview> GetOverviewAsync(
        Guid? ownerId, DateTime fromUtc, DateTime toUtc, TimeBucket bucket, CancellationToken cancellationToken = default)
    {
        var bucketExpr = BucketExpression(bucket);

        var sql = $"""
            SELECT COUNT(*) FROM documents.LogicalDocuments
            WHERE (@OwnerId IS NULL OR OwnerId = @OwnerId);

            SELECT COUNT(*) FROM documents.ConversionJobs cj
            JOIN documents.DocumentInstances d ON d.Id = cj.DocumentId
            WHERE (@OwnerId IS NULL OR d.OwnerId = @OwnerId);

            SELECT cj.Status, COUNT(*) AS Count FROM documents.ConversionJobs cj
            JOIN documents.DocumentInstances d ON d.Id = cj.DocumentId
            WHERE (@OwnerId IS NULL OR d.OwnerId = @OwnerId) AND cj.CreatedAtUtc BETWEEN @FromUtc AND @ToUtc
            GROUP BY cj.Status;

            SELECT cj.OperationType AS Label, COUNT(*) AS Count FROM documents.ConversionJobs cj
            JOIN documents.DocumentInstances d ON d.Id = cj.DocumentId
            WHERE (@OwnerId IS NULL OR d.OwnerId = @OwnerId) AND cj.CreatedAtUtc BETWEEN @FromUtc AND @ToUtc
            GROUP BY cj.OperationType
            ORDER BY Count DESC;

            SELECT d.ContentType AS Label, COUNT(*) AS Count FROM documents.DocumentInstances d
            WHERE (@OwnerId IS NULL OR d.OwnerId = @OwnerId) AND d.CreatedAtUtc BETWEEN @FromUtc AND @ToUtc
            GROUP BY d.ContentType
            ORDER BY Count DESC;

            SELECT {bucketExpr} AS PeriodStart, COUNT(*) AS Count FROM documents.ConversionJobs cj
            JOIN documents.DocumentInstances d ON d.Id = cj.DocumentId
            WHERE (@OwnerId IS NULL OR d.OwnerId = @OwnerId) AND cj.CreatedAtUtc BETWEEN @FromUtc AND @ToUtc
            GROUP BY {bucketExpr}
            ORDER BY PeriodStart;

            SELECT ISNULL(SUM(d.SizeBytes), 0) FROM documents.DocumentInstances d
            WHERE (@OwnerId IS NULL OR d.OwnerId = @OwnerId);

            SELECT TOP 10 cj.Id AS JobId, d.OriginalFileName, cj.OperationType, cj.Status, cj.CreatedAtUtc, cj.CompletedAtUtc
            FROM documents.ConversionJobs cj
            JOIN documents.DocumentInstances d ON d.Id = cj.DocumentId
            WHERE (@OwnerId IS NULL OR d.OwnerId = @OwnerId)
            ORDER BY cj.CreatedAtUtc DESC;
            """;

        await using var connection = new SqlConnection(configuration.GetConnectionString("DocumentsDb"));
        var parameters = new { OwnerId = ownerId, FromUtc = fromUtc, ToUtc = toUtc };

        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
        await using var multi = await connection.QueryMultipleAsync(command);

        var totalDocuments = await multi.ReadSingleAsync<int>();
        var totalConversions = await multi.ReadSingleAsync<int>();
        var statusRows = (await multi.ReadAsync<(byte Status, int Count)>()).ToList();
        var conversionsByType = (await multi.ReadAsync<CountByLabel>()).ToList();
        var documentsByContentType = (await multi.ReadAsync<CountByLabel>()).ToList();
        var timeSeries = (await multi.ReadAsync<TimeSeriesPoint>()).ToList();
        var storageUsedBytes = await multi.ReadSingleAsync<long>();
        var recentConversions = (await multi.ReadAsync<RawRecentConversion>())
            .Select(r => new RecentConversion(
                r.JobId, r.OriginalFileName, r.OperationType, ((ConversionJobStatus)r.Status).ToString(), r.CreatedAtUtc, r.CompletedAtUtc))
            .ToList();

        var completed = statusRows.Where(s => (ConversionJobStatus)s.Status == ConversionJobStatus.Completed).Sum(s => s.Count);
        var failed = statusRows.Where(s => (ConversionJobStatus)s.Status is ConversionJobStatus.Failed or ConversionJobStatus.Rejected).Sum(s => s.Count);
        var inProgress = statusRows.Sum(s => s.Count) - completed - failed;

        // No real per-user quota system exists yet — a configured default is the pragmatic
        // "used vs available" ceiling for the personal dashboard; platform-wide totals (ownerId
        // null) don't get a quota bar, just the raw used figure.
        long? storageQuotaBytes = ownerId is null
            ? null
            : configuration.GetValue("Documents:DefaultStorageQuotaBytes", 5L * 1024 * 1024 * 1024);

        return new DocumentsOverview(
            totalDocuments, totalConversions, new StatusBreakdown(completed, inProgress, failed),
            conversionsByType, documentsByContentType, timeSeries, storageUsedBytes, storageQuotaBytes, recentConversions);
    }

    private static string BucketExpression(TimeBucket bucket) => bucket switch
    {
        TimeBucket.Day => "CAST(cj.CreatedAtUtc AS DATE)",
        TimeBucket.Week => "DATEADD(DAY, -(DATEPART(WEEKDAY, cj.CreatedAtUtc) - 1), CAST(cj.CreatedAtUtc AS DATE))",
        TimeBucket.Month => "DATEFROMPARTS(YEAR(cj.CreatedAtUtc), MONTH(cj.CreatedAtUtc), 1)",
        _ => throw new ArgumentOutOfRangeException(nameof(bucket), bucket, "Bucket de tiempo no soportado."),
    };

    private sealed record RawRecentConversion(
        Guid JobId, string OriginalFileName, string OperationType, byte Status, DateTime CreatedAtUtc, DateTime? CompletedAtUtc);
}
