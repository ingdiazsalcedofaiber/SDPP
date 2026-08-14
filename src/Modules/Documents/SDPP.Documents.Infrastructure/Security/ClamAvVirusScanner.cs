using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using SDPP.Documents.Application.Ports;

namespace SDPP.Documents.Infrastructure.Security;

public sealed class ClamAvOptions
{
    public string Host { get; set; } = "clamav";
    public int Port { get; set; } = 3310;
    public int ChunkSizeBytes { get; set; } = 8192;
}

/// <summary>
/// Talks to a `clamd` daemon using the INSTREAM protocol — the antimalware gate that runs before
/// any file reaches storage or a conversion engine (see docs/05-security/dlp-engine.md §3.1 and
/// docs/07-operations/kubernetes-architecture.md §2, clamd reachable only inside sdpp-data).
/// </summary>
public sealed class ClamAvVirusScanner(IOptions<ClamAvOptions> options) : IVirusScanner
{
    private readonly ClamAvOptions _options = options.Value;

    public async Task<ScanResult> ScanAsync(Stream content, CancellationToken cancellationToken = default)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(_options.Host, _options.Port, cancellationToken);
        await using var networkStream = client.GetStream();

        await networkStream.WriteAsync("zINSTREAM\0"u8.ToArray(), cancellationToken);

        var buffer = new byte[_options.ChunkSizeBytes];
        int bytesRead;
        while ((bytesRead = await content.ReadAsync(buffer, cancellationToken)) > 0)
        {
            var sizeHeader = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(bytesRead);
            await networkStream.WriteAsync(BitConverter.GetBytes(sizeHeader), cancellationToken);
            await networkStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        // zero-length chunk terminates the stream
        await networkStream.WriteAsync(BitConverter.GetBytes(0), cancellationToken);

        using var reader = new StreamReader(networkStream, Encoding.ASCII, leaveOpen: true);
        var response = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;

        // clamd replies "stream: OK" when clean, "stream: <Threat> FOUND" otherwise.
        if (response.Contains("FOUND", StringComparison.OrdinalIgnoreCase))
        {
            var threatName = response.Replace("stream:", string.Empty).Replace("FOUND", string.Empty).Trim();
            return new ScanResult(IsClean: false, threatName);
        }

        return new ScanResult(IsClean: true, ThreatName: null);
    }
}
