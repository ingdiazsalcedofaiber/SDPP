using SDPP.BuildingBlocks.Domain;
using SDPP.Signature.Domain.Enums;

namespace SDPP.Signature.Domain.Aggregates;

/// <summary>
/// A single placeholder placed on the source PDF at envelope-creation time, assigned to one
/// recipient — child entity of <see cref="SignatureEnvelope"/>. Filled in by its recipient at
/// signing time (see <see cref="Fill"/>); nothing is drawn onto the actual PDF bytes until the
/// envelope completes and every field's resolved value/image is embedded in one pass (see
/// IPdfEnvelopeEmbeddingEngine) — this entity is the durable source of truth for that content in
/// the meantime.
/// </summary>
public sealed class EnvelopeField : Entity<Guid>
{
    private static readonly FieldType[] ImageBearingTypes = [FieldType.Signature, FieldType.Initials, FieldType.Stamp];

    public Guid EnvelopeId { get; private set; }
    public Guid RecipientId { get; private set; }
    public FieldType Type { get; private set; }
    public int PageNumber { get; private set; }

    /// <summary>Fractions (0..1) of the page's width/height — same resolution-independent
    /// convention as the original single-signer module's PlacementOverlay.</summary>
    public double PositionX { get; private set; }
    public double PositionY { get; private set; }
    public double Width { get; private set; }
    public double Height { get; private set; }

    public bool Required { get; private set; }

    /// <summary>Text/date(ISO 8601)/"true"-"false" content — populated for every field type except
    /// the image-bearing ones (Signature/Initials/Stamp use <see cref="SignatureImage"/> instead).</summary>
    public string? Value { get; private set; }

    /// <summary>Small PNG, only for Signature/Initials/Stamp fields.</summary>
    public byte[]? SignatureImage { get; private set; }

    public SignatureMethodUsed? SignatureMethod { get; private set; }
    public DateTime? FilledAtUtc { get; private set; }

    /// <summary>SHA-256 over this field's canonical fill context (envelope/document/signer/
    /// coordinates/timestamps/IP/etc.) — assigned by the Application layer right after Fill(),
    /// since computing it needs envelope/recipient context this entity doesn't itself hold.</summary>
    public string? SignatureHash { get; private set; }

    public bool IsImageBearing => ImageBearingTypes.Contains(Type);

    /// <summary>FilledAtUtc is always set in the SAME Fill() call that sets Value/SignatureImage
    /// (or, for LegalApprovalStamp, is the ONLY thing Fill() sets) — a single source of truth that
    /// works for every field type without a type-specific branch here.</summary>
    public bool IsFilled => FilledAtUtc is not null;

    private EnvelopeField() { } // EF Core

    internal static EnvelopeField Create(
        Guid envelopeId, Guid recipientId, FieldType type, int pageNumber,
        double positionX, double positionY, double width, double height, bool required)
    {
        if (pageNumber < 1)
        {
            throw new DomainException("El número de página debe ser mayor o igual a 1.");
        }
        EnsureUnitFractions(positionX, positionY, width, height);

        return new EnvelopeField
        {
            Id = Guid.NewGuid(),
            EnvelopeId = envelopeId,
            RecipientId = recipientId,
            Type = type,
            PageNumber = pageNumber,
            PositionX = positionX,
            PositionY = positionY,
            Width = width,
            Height = height,
            Required = required,
        };
    }

    /// <summary>Repositions/resizes an already-placed field — the visual editor's drag/resize
    /// handle calls this instead of delete-and-recreate, which would otherwise lose the field's
    /// identity for no reason. Only reachable while the parent envelope is still Draft (see
    /// SignatureEnvelope.UpdateField).</summary>
    internal void UpdatePosition(double positionX, double positionY, double width, double height)
    {
        EnsureUnitFractions(positionX, positionY, width, height);
        PositionX = positionX;
        PositionY = positionY;
        Width = width;
        Height = height;
    }

    private static void EnsureUnitFractions(double positionX, double positionY, double width, double height)
    {
        foreach (var (name, value) in new[] { (nameof(positionX), positionX), (nameof(positionY), positionY), (nameof(width), width), (nameof(height), height) })
        {
            if (value < 0 || value > 1)
            {
                throw new DomainException($"'{name}' debe estar entre 0 y 1 (fracción de la página), recibido: {value}.");
            }
        }
    }

    internal void Fill(string? value, byte[]? signatureImage, SignatureMethodUsed? signatureMethod)
    {
        if (Type == FieldType.LegalApprovalStamp)
        {
            // Never accepts client-supplied value/image bytes — see FieldType.LegalApprovalStamp's
            // doc comment. Filling this field is only ever an explicit confirmation click; the
            // actual graphic is generated server-side at document assembly.
            FilledAtUtc = DateTime.UtcNow;
            return;
        }

        if (IsImageBearing)
        {
            if (signatureImage is null || signatureImage.Length == 0)
            {
                throw new DomainException($"El campo de tipo '{Type}' requiere una imagen.");
            }
            if (signatureMethod is null)
            {
                throw new DomainException("Debe indicarse el método de firma usado para este campo.");
            }
            SignatureImage = signatureImage;
            SignatureMethod = signatureMethod;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new DomainException($"El campo de tipo '{Type}' requiere un valor.");
            }
            Value = value;
        }

        FilledAtUtc = DateTime.UtcNow;
    }

    internal void AssignSignatureHash(string hash) => SignatureHash = hash;
}
