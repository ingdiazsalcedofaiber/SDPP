using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Signature.Application.Ports;
using SDPP.Signature.Domain.Enums;

namespace SDPP.Signature.Application.UseCases.Dashboard;

public sealed record EnvelopeDashboardSummary(
    int Drafts, int WaitingForOthers, int WaitingForMe, int Completed, int Declined, int Cancelled, int Expired,
    double? AvgCompletionHours, int DocumentsSent, int SignaturesPerformed);

/// <summary>Backs the "Bandeja de firmas" dashboard — bucket counts (Drafts/WaitingForOthers/
/// WaitingForMe/Completed/Declined/Cancelled/Expired) plus aggregate metrics, scoped to envelopes
/// the caller is involved in (creator or any recipient) within their organization. Unlike
/// ListEnvelopesQuery's scope filter (built for "what needs my attention"), this deliberately
/// includes terminal-state envelopes too — a dashboard needs the full picture, not just what's
/// actionable.</summary>
public sealed record GetDashboardSummaryQuery : IQuery<EnvelopeDashboardSummary>;

public sealed class GetDashboardSummaryHandler(
    ISignatureEnvelopeRepository repository, ICurrentActor currentActor, IOrganizationContextProvider organizationContextProvider)
    : IRequestHandler<GetDashboardSummaryQuery, Result<EnvelopeDashboardSummary>>
{
    public async Task<Result<EnvelopeDashboardSummary>> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        var userId = currentActor.UserId;
        var envelopes = await repository.GetInvolvingUserAsync(userId, organizationContextProvider.GetCurrentOrganizationId(), cancellationToken);

        int drafts = 0, waitingForOthers = 0, waitingForMe = 0, completed = 0, declined = 0, cancelled = 0, expired = 0;
        int documentsSent = 0, signaturesPerformed = 0;
        var completionHoursSum = 0.0;
        var completedWithDuration = 0;

        foreach (var envelope in envelopes)
        {
            var isCreator = envelope.CreatedByUserId == userId;
            var myRecipient = envelope.Recipients.FirstOrDefault(r => r.MatchedUserId == userId);
            var isMyTurn = myRecipient is { Status: RecipientStatus.Sent or RecipientStatus.Viewed };

            switch (envelope.Status)
            {
                case EnvelopeStatus.Draft when isCreator:
                    drafts++;
                    break;
                case EnvelopeStatus.Completed:
                    completed++;
                    if (envelope.CompletedAtUtc is { } completedAt)
                    {
                        completionHoursSum += (completedAt - envelope.CreatedAtUtc).TotalHours;
                        completedWithDuration++;
                    }
                    break;
                case EnvelopeStatus.Declined:
                    declined++;
                    break;
                case EnvelopeStatus.Cancelled:
                    cancelled++;
                    break;
                case EnvelopeStatus.Expired:
                    expired++;
                    break;
                default:
                    if (isMyTurn) waitingForMe++;
                    else waitingForOthers++;
                    break;
            }

            if (isCreator && envelope.SentAtUtc is not null)
            {
                documentsSent++;
            }
            if (myRecipient is not null)
            {
                signaturesPerformed += envelope.DocumentSignatures.Count(s => s.RecipientId == myRecipient.Id);
            }
        }

        double? avgCompletionHours = completedWithDuration > 0 ? Math.Round(completionHoursSum / completedWithDuration, 1) : null;

        return Result.Success(new EnvelopeDashboardSummary(
            drafts, waitingForOthers, waitingForMe, completed, declined, cancelled, expired,
            avgCompletionHours, documentsSent, signaturesPerformed));
    }
}
