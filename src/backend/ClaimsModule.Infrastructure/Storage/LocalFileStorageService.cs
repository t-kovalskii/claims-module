using System.Security.Cryptography;
using System.Text;
using ClaimsModule.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace ClaimsModule.Infrastructure.Storage;

/// <summary>
/// Ticket 10's local-filesystem fallback: files land under <see cref="LocalStorageSettings.RootPath"/>,
/// mirroring the <c>{organisation}/{claim}/{guid}_{name}</c> storage path the command handler
/// builds. Since a bare filesystem path has no native signed-URL mechanism (unlike Azure Blob's
/// SAS), this issues its own HMAC-signed link pointing back at this API's
/// <c>LocalDocumentDownloadController</c>, which calls <see cref="TryOpenSignedUrl"/> to validate
/// it before streaming the file.
/// </summary>
public class LocalFileStorageService(IOptions<StorageSettings> options, IHttpContextAccessor httpContextAccessor)
    : IStorageService, ILocalSignedDocumentLinkService
{
    private readonly LocalStorageSettings _settings = options.Value.Local;

    public async Task SaveAsync(string storagePath, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var fullPath = ResolvePath(storagePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, cancellationToken);
    }

    public Task<Uri> GetDownloadUrlAsync(
        string storagePath, string downloadFileName, string contentType, TimeSpan expiry, CancellationToken cancellationToken)
    {
        var expiresAtUnixSeconds = DateTimeOffset.UtcNow.Add(expiry).ToUnixTimeSeconds();
        var signature = ComputeSignature(storagePath, expiresAtUnixSeconds);

        var query = string.Join('&',
            $"path={Uri.EscapeDataString(storagePath)}",
            $"expires={expiresAtUnixSeconds}",
            $"signature={Uri.EscapeDataString(signature)}",
            $"fileName={Uri.EscapeDataString(downloadFileName)}",
            $"contentType={Uri.EscapeDataString(contentType)}");

        return Task.FromResult(new Uri($"{ResolvePublicBaseUrl()}/api/documents/local?{query}"));
    }

    /// <summary>Prefers the current request's own scheme+host over the configured
    /// <see cref="LocalStorageSettings.PublicBaseUrl"/> fallback — a request already knows
    /// exactly which host/port it arrived on, so this can't drift out of sync with whichever
    /// launch profile or port forwarding is actually in effect the way a hardcoded config value
    /// can. The config fallback only matters when this runs with no active HTTP request (there
    /// isn't one in this slice, but SaveAsync/GetDownloadUrlAsync have no such requirement
    /// themselves).</summary>
    private string ResolvePublicBaseUrl()
    {
        var request = httpContextAccessor.HttpContext?.Request;
        if (request is not null)
        {
            return $"{request.Scheme}://{request.Host}";
        }

        return _settings.PublicBaseUrl.TrimEnd('/');
    }

    public Stream? TryOpenSignedUrl(string storagePath, long expiresAtUnixSeconds, string signature)
    {
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresAtUnixSeconds)
        {
            return null;
        }

        var expectedSignature = ComputeSignature(storagePath, expiresAtUnixSeconds);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);
        var suppliedBytes = Encoding.UTF8.GetBytes(signature);

        // Constant-time comparison — the signing key is otherwise reusable to forge a link for
        // any path if an attacker can learn a valid signature byte-by-byte via timing.
        if (expectedBytes.Length != suppliedBytes.Length || !CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes))
        {
            return null;
        }

        var fullPath = ResolvePath(storagePath);
        return File.Exists(fullPath) ? File.OpenRead(fullPath) : null;
    }

    private string ResolvePath(string storagePath)
    {
        var rootPath = Path.IsPathRooted(_settings.RootPath)
            ? _settings.RootPath
            : Path.Combine(AppContext.BaseDirectory, _settings.RootPath);

        var normalizedRelativePath = storagePath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(rootPath, normalizedRelativePath));

        var normalizedRoot = Path.GetFullPath(rootPath) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(normalizedRoot, StringComparison.Ordinal))
        {
            // Defensive: storagePath is always built by our own command handler, never taken
            // verbatim from a client, but this keeps ResolvePath safe even if that ever changes.
            throw new UnauthorizedAccessException("Resolved path escapes the configured storage root.");
        }

        return fullPath;
    }

    private string ComputeSignature(string storagePath, long expiresAtUnixSeconds)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_settings.SigningKey));
        var payload = $"{storagePath}|{expiresAtUnixSeconds}";
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(hash);
    }
}
