using System.Text.RegularExpressions;
using SDPP.BuildingBlocks.Domain;

namespace SDPP.Classification.Domain.ValueObjects;

/// <summary>
/// SHA-256 hash of a document's original bytes. Moved here from the Documents module as part of
/// the "Clasificación de Activos de Información" extraction — Classification is now the sole
/// owner of hash generation/storage; the byte-level computation still happens where the bytes
/// already live (Documents.Application/Conversion Worker), but this module owns what the value
/// means and where it lives at rest.
/// </summary>
public sealed partial class FileHash : ValueObject
{
    public string Value { get; }

    private FileHash(string value) => Value = value;

    public static FileHash FromHex(string sha256Hex)
    {
        if (string.IsNullOrWhiteSpace(sha256Hex) || !Sha256Regex().IsMatch(sha256Hex))
        {
            throw new DomainException("El hash SHA-256 debe tener 64 caracteres hexadecimales.");
        }

        return new FileHash(sha256Hex.ToLowerInvariant());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[a-fA-F0-9]{64}$")]
    private static partial Regex Sha256Regex();
}
