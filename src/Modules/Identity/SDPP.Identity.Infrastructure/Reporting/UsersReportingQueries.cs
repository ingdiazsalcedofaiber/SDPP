using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SDPP.Identity.Application.Ports;

namespace SDPP.Identity.Infrastructure.Reporting;

/// <summary>Raw Dapper aggregate reads against SDPP_Identity for the admin dashboard — same
/// rationale as Documents.Infrastructure.Reporting.DocumentsReportingQueries: aggregate counts with
/// no domain invariants to protect, not worth routing through EF Core's change tracking.</summary>
public sealed class UsersReportingQueries(IConfiguration configuration) : IUsersReportingQueries
{
    public async Task<UsersOverview> GetOverviewAsync(
        DateTime fromUtc, DateTime toUtc, ReportingTimeBucket bucket, CancellationToken cancellationToken = default)
    {
        var bucketExpr = BucketExpression(bucket);

        var sql = $"""
            SELECT COUNT(*) FROM [identity].[Users];

            SELECT COUNT(*) FROM [identity].[Users] WHERE Active = 1;

            SELECT {bucketExpr} AS PeriodStart, COUNT(*) AS Count
            FROM [identity].[Users]
            WHERE CreatedAtUtc >= @FromUtc AND CreatedAtUtc < @ToUtc
            GROUP BY {bucketExpr}
            ORDER BY PeriodStart;
            """;

        await using var connection = new SqlConnection(configuration.GetConnectionString("IdentityDb"));
        var command = new CommandDefinition(sql, new { FromUtc = fromUtc, ToUtc = toUtc }, cancellationToken: cancellationToken);
        await using var multi = await connection.QueryMultipleAsync(command);

        var totalUsers = await multi.ReadSingleAsync<int>();
        var activeUsers = await multi.ReadSingleAsync<int>();
        var timeSeries = (await multi.ReadAsync<UsersTimeSeriesPoint>()).ToList();

        return new UsersOverview(totalUsers, activeUsers, timeSeries);
    }

    /// <summary>Fixed, hardcoded per-bucket SQL date expressions selected via C# switch — never
    /// interpolates the client-facing bucket parameter directly into SQL. Mirrors
    /// DocumentsReportingQueries.BucketExpression exactly.</summary>
    private static string BucketExpression(ReportingTimeBucket bucket) => bucket switch
    {
        ReportingTimeBucket.Day => "CAST(CreatedAtUtc AS DATE)",
        ReportingTimeBucket.Week => "DATEADD(DAY, -(DATEPART(WEEKDAY, CreatedAtUtc) - 1), CAST(CreatedAtUtc AS DATE))",
        ReportingTimeBucket.Month => "DATEFROMPARTS(YEAR(CreatedAtUtc), MONTH(CreatedAtUtc), 1)",
        _ => throw new ArgumentOutOfRangeException(nameof(bucket)),
    };
}
