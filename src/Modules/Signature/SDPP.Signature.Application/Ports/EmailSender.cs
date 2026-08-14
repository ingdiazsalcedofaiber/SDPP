namespace SDPP.Signature.Application.Ports;

/// <summary>
/// Sends transactional email (signing invitations, reminders, completion notices) — today's only
/// implementation (LoggingEmailSender) does NOT actually send anything, since no SMTP provider is
/// configured yet; it logs the attempt so the gap is visible in Seq/logs rather than silently
/// swallowed. Swapping in a real SMTP-backed IEmailSender (e.g. MailKit) once credentials exist
/// means changing ONLY that one implementation — every caller here stays the same.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default);
}
