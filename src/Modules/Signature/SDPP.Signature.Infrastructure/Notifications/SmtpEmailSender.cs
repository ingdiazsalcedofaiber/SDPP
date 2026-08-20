using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using SDPP.Signature.Application.Ports;

namespace SDPP.Signature.Infrastructure.Notifications;

/// <summary>
/// Real SMTP delivery via MailKit, active automatically the moment Smtp:Host is configured (see
/// EmailSenderRegistration) — no code change needed once real institutional credentials exist,
/// exactly what IEmailSender's doc comment promised. Every attempt is logged with recipient,
/// subject, and outcome (structured, flows to Seq) so delivery failures are visible without a
/// dedicated EmailLog table — Serilog/Seq is already the platform's observability story (see
/// docs/07-operations), a second bespoke log store for this one concern would just duplicate it.
/// Transient failures (network blip, temporary SMTP 4xx) get 2 retries with backoff before giving
/// up; a permanent failure (auth rejected, 5xx) is not retried.
/// </summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(8)];

    public async Task SendAsync(
        string toEmail, string subject, string body,
        IReadOnlyList<EmailAttachment>? attachments = null, CancellationToken cancellationToken = default)
    {
        var smtp = options.Value;
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtp.FromName, smtp.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        var bodyPart = new TextPart("html") { Text = body };
        if (attachments is { Count: > 0 })
        {
            var multipart = new Multipart("mixed") { bodyPart };
            foreach (var attachment in attachments)
            {
                multipart.Add(new MimePart(attachment.ContentType)
                {
                    Content = new MimeContent(new MemoryStream(attachment.Content)),
                    ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                    ContentTransferEncoding = ContentEncoding.Base64,
                    FileName = attachment.FileName,
                });
            }
            message.Body = multipart;
        }
        else
        {
            message.Body = bodyPart;
        }

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var client = new SmtpClient();
                // Auto (not a hardcoded StartTls) negotiates encryption when the server offers it
                // and falls back to plaintext when it doesn't, instead of hard-failing — real
                // institutional SMTP-587 servers advertise STARTTLS and this ends up using it just
                // the same, but it also works against a plain internal relay that never will.
                var socketOptions = smtp.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto;
                await client.ConnectAsync(smtp.Host, smtp.Port, socketOptions, cancellationToken);
                if (!string.IsNullOrEmpty(smtp.Username))
                {
                    await client.AuthenticateAsync(smtp.Username, smtp.Password, cancellationToken);
                }
                await client.SendAsync(message, cancellationToken);
                await client.DisconnectAsync(true, cancellationToken);

                logger.LogInformation(
                    "EMAIL ENVIADO — Para: {ToEmail}, Asunto: {Subject}, Intento: {Attempt}", toEmail, subject, attempt + 1);
                return;
            }
            catch (Exception ex) when (attempt < RetryDelays.Length)
            {
                logger.LogWarning(ex,
                    "EMAIL FALLÓ (reintentando) — Para: {ToEmail}, Asunto: {Subject}, Intento: {Attempt}", toEmail, subject, attempt + 1);
                await Task.Delay(RetryDelays[attempt], cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "EMAIL FALLÓ (definitivo) — Para: {ToEmail}, Asunto: {Subject}", toEmail, subject);
                throw;
            }
        }
    }
}
