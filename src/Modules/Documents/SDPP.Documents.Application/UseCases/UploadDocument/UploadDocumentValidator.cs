using FluentValidation;

namespace SDPP.Documents.Application.UseCases.UploadDocument;

public sealed class UploadDocumentValidator : AbstractValidator<UploadDocumentCommand>
{
    // Aligned with the mandatory-fields / file-limits policy in docs/04-use-cases/use-cases.md §2.
    private const long MaxFileSizeBytes = 200 * 1024 * 1024; // 200 MB

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-powerpoint",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "image/png",
        "image/jpeg",
        "image/tiff",
    };

    public UploadDocumentValidator()
    {
        RuleFor(c => c.OriginalFileName).NotEmpty().MaximumLength(255);
        RuleFor(c => c.SizeBytes).GreaterThan(0)
            .WithMessage("El archivo está vacío o no se pudo leer correctamente. Selecciona un archivo con contenido.");
        RuleFor(c => c.SizeBytes).LessThanOrEqualTo(MaxFileSizeBytes)
            .WithMessage($"El archivo excede el tamaño máximo permitido ({MaxFileSizeBytes / 1024 / 1024} MB).");
        RuleFor(c => c.DeclaredContentType)
            .Must(ct => AllowedContentTypes.Contains(ct))
            .WithMessage("Tipo de archivo no soportado.");
    }
}
