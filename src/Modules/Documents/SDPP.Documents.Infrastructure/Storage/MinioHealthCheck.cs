using Microsoft.Extensions.Diagnostics.HealthChecks;
using Minio;
using Minio.DataModel.Args;
using SDPP.Documents.Domain.ValueObjects;

namespace SDPP.Documents.Infrastructure.Storage;

/// <summary>Real object-storage connectivity probe — cheap BucketExistsAsync round-trip, same
/// "no internal details in the response" discipline as DbContextHealthCheck.</summary>
public sealed class MinioHealthCheck(IMinioClient minioClient) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var exists = await minioClient.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(StoragePath.BucketName), cancellationToken);
            return exists ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
        }
        catch
        {
            return HealthCheckResult.Unhealthy();
        }
    }
}
