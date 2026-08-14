using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Signature.Application.Ports;

namespace SDPP.Signature.Application.UseCases.GetEnvelope;

public sealed record EnvelopeCertificateFile(byte[] Content, string FileName);

/// <summary>Backs GET /envelopes/{id}/certificate — the standalone completion-certificate PDF (see
/// SignatureEnvelope.CertificateDocument), same authorization as GetEnvelopeQuery (creator/admin or
/// one of the envelope's own recipients). Only available once the envelope is Completed.</summary>
public sealed record GetEnvelopeCertificateQuery(Guid EnvelopeId) : IQuery<EnvelopeCertificateFile>;

public sealed class GetEnvelopeCertificateHandler(
    ISignatureEnvelopeRepository repository, ICurrentActor currentActor, IOrganizationContextProvider organizationContextProvider)
    : IRequestHandler<GetEnvelopeCertificateQuery, Result<EnvelopeCertificateFile>>
{
    public async Task<Result<EnvelopeCertificateFile>> Handle(GetEnvelopeCertificateQuery request, CancellationToken cancellationToken)
    {
        var envelope = await repository.GetByIdAsync(request.EnvelopeId, cancellationToken);
        if (envelope is null)
        {
            return Result.Failure<EnvelopeCertificateFile>("El sobre no existe.", "ENVELOPE_NOT_FOUND");
        }

        var isRecipient = envelope.Recipients.Any(r => r.MatchedUserId == currentActor.UserId);
        if (!UseCases.EnvelopeAuthorization.CanManage(envelope, currentActor, organizationContextProvider.GetCurrentOrganizationId()) && !isRecipient)
        {
            return Result.Failure<EnvelopeCertificateFile>("No tienes permiso para ver este sobre.", "FORBIDDEN");
        }

        if (envelope.CertificateDocument is null)
        {
            return Result.Failure<EnvelopeCertificateFile>("El certificado todavía no está disponible.", "CERTIFICATE_NOT_READY");
        }

        return Result.Success(new EnvelopeCertificateFile(envelope.CertificateDocument, $"certificado-{envelope.Id}.pdf"));
    }
}
