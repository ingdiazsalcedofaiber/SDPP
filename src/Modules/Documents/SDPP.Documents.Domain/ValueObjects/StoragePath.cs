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

    public const string BucketName = "sdpp-documents";

    public static StoragePath ForDocument(Guid documentId, string originalFileName) =>
        new(BucketName, $"{documentId:N}/{SanitizeForObjectKey(originalFileName)}");

    // The doc comment above promises "never a filesystem path" — this is what actually makes that
    // true, rather than just asserting it. originalFileName is whatever the uploader's browser sent
    // in the multipart Content-Disposition header (fully attacker-controlled, e.g. "../../x" or a
    // name embedding "/"), and previously flowed into the object key completely unsanitized. The
    // GUID prefix already stops any cross-document collision/overwrite, but relying solely on the
    // storage backend's own key canonicalization to prevent an escape was an unverified assumption,
    // not a guarantee — stripping path separators and control characters here removes that
    // assumption instead of documenting around it.
    private static string SanitizeForObjectKey(string originalFileName)
    {
        var name = originalFileName.Replace('\\', '/');
        var lastSlash = name.LastIndexOf('/');
        if (lastSlash >= 0) name = name[(lastSlash + 1)..];

        var sanitized = new string(name.Select(c => char.IsControl(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrEmpty(sanitized) ? "documento" : sanitized;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Bucket;
        yield return ObjectKey;
    }

    public override string ToString() => $"{Bucket}/{ObjectKey}";
}
