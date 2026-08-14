namespace SDPP.Documents.Application.Ports;

public sealed record ScanResult(bool IsClean, string? ThreatName);

/// <summary>
/// Port to the antimalware engine (ClamAV daemon / corporate EDR, see
/// docs/05-security/threat-model-stride.md and docs/01-architecture/technology-stack.md §1).
/// Every uploaded file is scanned before it is persisted or handed to a conversion engine.
/// </summary>
public interface IVirusScanner
{
    Task<ScanResult> ScanAsync(Stream content, CancellationToken cancellationToken = default);
}
