namespace SDPP.Signature.Infrastructure.Notifications;

/// <summary>Bound from the "Smtp" config section (Smtp__Host, Smtp__Port, ... as env vars — see
/// .env.example). Left with Host empty by default so dev/test environments keep using
/// LoggingEmailSender until a real institutional mail server is configured — see
/// EmailSenderRegistration for the switch.</summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "SDPP";
    // STARTTLS is the common institutional-SMTP default (port 587); set true only for implicit
    // TLS on port 465.
    public bool UseSsl { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}
