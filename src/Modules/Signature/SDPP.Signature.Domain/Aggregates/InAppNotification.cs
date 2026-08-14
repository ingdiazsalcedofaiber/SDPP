using SDPP.BuildingBlocks.Domain;
using SDPP.Signature.Domain.Enums;

namespace SDPP.Signature.Domain.Aggregates;

/// <summary>
/// An in-app notification for one SDPP user — independent aggregate (same "own lifecycle" shape as
/// SavedSignature/SignerAccessChallenge), since it's addressed to a UserId rather than owned by any
/// single envelope. Created by the envelope lifecycle (send/view/sign/complete/decline/cancel/
/// expire/reminder) — see EnvelopeLifecycleJob and the various UseCase handlers that call
/// INotificationRepository.Add directly.
/// </summary>
public sealed class InAppNotification : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = null!;
    public string Message { get; private set; } = null!;
    public Guid? EnvelopeId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }

    private InAppNotification() { } // EF Core

    public static InAppNotification Create(Guid userId, NotificationType type, string title, string message, Guid? envelopeId)
    {
        return new InAppNotification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            EnvelopeId = envelopeId,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    public void MarkRead() => ReadAtUtc ??= DateTime.UtcNow;
}
