namespace ClaimsModule.Application.Common.Interfaces;

/// <summary>
/// Ticket 10 / spec.md's Document storage decision: Azure Blob and local-filesystem
/// implementations, selected by configuration (<c>Storage:Provider</c>) in
/// ClaimsModule.Infrastructure's DependencyInjection — switching providers is a configuration
/// change only, never a code change.
/// </summary>
public interface IStorageService
{
    Task SaveAsync(string storagePath, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>Returns a short-lived (caller-supplied <paramref name="expiry"/>) signed URL for
    /// retrieving the document directly from the storage provider — never streamed through the
    /// API itself.</summary>
    Task<Uri> GetDownloadUrlAsync(
        string storagePath,
        string downloadFileName,
        string contentType,
        TimeSpan expiry,
        CancellationToken cancellationToken);
}
