using System.Text;

namespace ClaimsModule.Application.Common;

/// <summary>Ticket 10's "filename sanitised against path traversal" requirement: strips any
/// directory component (so a client-supplied <c>../../etc/passwd</c> or <c>a/b.txt</c> can't
/// escape the per-claim storage prefix a caller builds around the result) and restricts the
/// remaining name to a safe character set, independent of which <c>IStorageService</c> is
/// active.</summary>
public static class FileNameSanitizer
{
    private const int MaxLength = 200;

    public static string Sanitize(string originalFileName)
    {
        // Path.GetFileName drops any directory segments (including "..") regardless of which
        // separator style the client sent, on any OS this API runs on.
        var nameOnly = System.IO.Path.GetFileName(originalFileName.Replace('\\', '/'));

        var builder = new StringBuilder(nameOnly.Length);
        foreach (var c in nameOnly)
        {
            if (char.IsLetterOrDigit(c) || c is '.' or '_' or '-' or ' ')
            {
                builder.Append(c);
            }
            else
            {
                builder.Append('_');
            }
        }

        var sanitized = builder.ToString().Trim();
        if (sanitized.Length > MaxLength)
        {
            sanitized = sanitized[..MaxLength];
        }

        return string.IsNullOrWhiteSpace(sanitized) ? "unnamed-file" : sanitized;
    }
}
