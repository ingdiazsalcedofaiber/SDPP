using SDPP.BuildingBlocks.Domain;

namespace SDPP.Documents.Domain.ValueObjects;

/// <summary>
/// Opaque reference to a blob in object storage. Never a filesystem path — this is what lets the
/// storage backend change (MinIO, NAS, future S3-compatible target) without the domain caring
/// (see docs/03-data/er-model.md §2).
/// </summary>
public sealed class StoragePath : ValueObject
{
    public string Bucket { get; }
    public string ObjectKey { get; }

    public StoragePath(string bucket, string objectKey)
    {
        if (string.IsNullOrWhiteSpace(bucket)) throw new DomainException("El bucket de almacenamiento es obligatorio.");
        if (string.IsNullOrWhiteSpace(objectKey)) throw new DomainException("La clave de objeto es obligatoria.");

        Bucket = bucket;
        ObjectKey = objectKey;
    }

    public static StoragePath ForDocument(Guid documentId, string originalFileName) =>
        new("sdpp-documents", $"{documentId:N}/{originalFileName}");

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Bucket;
        yield return ObjectKey;
    }

    public override string ToString() => $"{Bucket}/{ObjectKey}";
}
