using SDPP.BuildingBlocks.Domain;

namespace SDPP.Signature.Domain.Aggregates;

/// <summary>
/// A signature/initials image an internal SDPP user has saved for reuse across future envelopes
/// ("reutilizar firma previamente registrada"). Append-only per record — replacing one means
/// deleting and creating a new one, there's no in-place edit.
/// </summary>
public sealed class SavedSignature : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public byte[] ImageBytes { get; private set; } = null!;
    public double AspectRatio { get; private set; }
    public string Label { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }

    private SavedSignature() { } // EF Core

    public static SavedSignature Create(Guid userId, byte[] imageBytes, double aspectRatio, string label)
    {
        if (imageBytes.Length == 0)
        {
            throw new DomainException("La imagen de la firma no puede estar vacía.");
        }
        if (aspectRatio <= 0)
        {
            throw new DomainException("La relación de aspecto debe ser mayor a cero.");
        }

        return new SavedSignature
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ImageBytes = imageBytes,
            AspectRatio = aspectRatio,
            Label = string.IsNullOrWhiteSpace(label) ? "Mi firma" : label.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
        };
    }
}
