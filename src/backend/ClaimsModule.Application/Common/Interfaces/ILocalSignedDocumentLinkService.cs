namespace ClaimsModule.Application.Common.Interfaces;

/// <summary>
/// Only meaningfully implemented when <c>Storage:Provider</c> is <c>Local</c> — the local
/// filesystem has no native signed-URL capability, so this validates the HMAC signature
/// <c>LocalFileStorageService.GetDownloadUrlAsync</c> embedded in the link it returned, and opens
/// the file for the controller that serves it. Resolved as optional (nullable) by that
/// controller: when Azure Blob is configured, nothing ever calls back into this API for a
/// download — the SAS URL points directly at Azure — so there's nothing to resolve.
/// </summary>
public interface ILocalSignedDocumentLinkService
{
    Stream? TryOpenSignedUrl(string storagePath, long expiresAtUnixSeconds, string signature);
}
