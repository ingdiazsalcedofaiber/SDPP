using Minio;
using Minio.DataModel.Args;
using SDPP.Documents.Application.Ports;
using SDPP.Documents.Domain.ValueObjects;

namespace SDPP.Documents.Infrastructure.Storage;

/// <summary>
/// S3-compatible object storage backed by an on-prem MinIO cluster (see
/// docs/01-architecture/technology-stack.md §1) — no cloud storage provider is ever involved.
/// </summary>
public sealed class MinIoBlobStorage(IMinioClient minioClient) : IBlobStorage
{
    public async Task SaveAsync(StoragePath path, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        await EnsureBucketExistsAsync(path.Bucket, cancellationToken);

        var putArgs = new PutObjectArgs()
            .WithBucket(path.Bucket)
            .WithObject(path.ObjectKey)
            .WithStreamData(content)
            .WithObjectSize(content.Length)
            .WithContentType(contentType)
            // Server-side encryption at rest (AES-256) — see docs/00-overview.md §4.1.
            .WithHeaders(new Dictionary<string, string> { ["x-amz-server-side-encryption"] = "AES256" });

        await minioClient.PutObjectAsync(putArgs, cancellationToken);
    }

    public async Task<Stream> OpenReadAsync(StoragePath path, CancellationToken cancellationToken = default)
    {
        var buffer = new MemoryStream();
        var getArgs = new GetObjectArgs()
            .WithBucket(path.Bucket)
            .WithObject(path.ObjectKey)
            .WithCallbackStream(async (stream, ct) => await stream.CopyToAsync(buffer, ct));

        await minioClient.GetObjectAsync(getArgs, cancellationToken);
        buffer.Position = 0;
        return buffer;
    }

    public async Task DeleteAsync(StoragePath path, CancellationToken cancellationToken = default)
    {
        var removeArgs = new RemoveObjectArgs().WithBucket(path.Bucket).WithObject(path.ObjectKey);
        await minioClient.RemoveObjectAsync(removeArgs, cancellationToken);
    }

    private async Task EnsureBucketExistsAsync(string bucket, CancellationToken cancellationToken)
    {
        var existsArgs = new BucketExistsArgs().WithBucket(bucket);
        if (!await minioClient.BucketExistsAsync(existsArgs, cancellationToken))
        {
            await minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), cancellationToken);
        }
    }
}
