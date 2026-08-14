using Microsoft.Extensions.Logging;
using SDPP.Signature.Application.Ports;

namespace SDPP.Signature.Infrastructure.Notifications;

/// <summary>Today's only IEmailSender — no SMTP provider is configured yet, so this logs the
/// attempt (visible in Seq) instead of sending anything, and never pretends to have delivered mail.
/// See IEmailSender's doc comment.</summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "EMAIL NO ENVIADO (sin proveedor SMTP configurado) — Para: {ToEmail}, Asunto: {Subject}", toEmail, subject);
        return Task.CompletedTask;
    }
}
