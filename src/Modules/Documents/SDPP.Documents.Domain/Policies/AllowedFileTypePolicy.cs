namespace SDPP.Documents.Domain.Policies;

/// <summary>
/// Upload-time allow-list — extension is the primary, reliable signal (the browser/client-declared
/// MIME type is easy to spoof and inconsistent across browsers for the same file, so it's only used
/// as a secondary "does it look like an obviously wrong type" check, never the sole gate). This is
/// independent of and in addition to the antimalware scan (<see cref="UploadDocument"/>'s
/// <c>IVirusScanner</c> gate) — ClamAV catches malicious *content*; this catches disallowed *kinds*
/// of file regardless of whether their content is clean (e.g. a clean .exe is still not a document).
/// </summary>
public static class AllowedFileTypePolicy
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Office / text documents
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".txt", ".csv", ".rtf", ".odt", ".ods", ".odp",
        // Images (scans, evidence attachments)
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff",
        // Structured data / archives used for evidence packages and interchange
        ".xml", ".json", ".zip", ".eml", ".msg",
    };

    // Belt-and-suspenders against an obviously wrong declared content-type even when the extension
    // passes (e.g. a renamed .exe uploaded as "report.pdf") — deliberately coarse (prefix match),
    // real payload safety is ClamAV's job, not this string check.
    private static readonly string[] BlockedContentTypePrefixes =
    [
        "application/x-msdownload", "application/x-executable", "application/x-sh",
        "application/x-bat", "application/vnd.microsoft.portable-executable",
    ];

    public static bool IsAllowed(string originalFileName, string declaredContentType)
    {
        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            return false;
        }

        return !BlockedContentTypePrefixes.Any(blocked =>
            declaredContentType.StartsWith(blocked, StringComparison.OrdinalIgnoreCase));
    }
}
