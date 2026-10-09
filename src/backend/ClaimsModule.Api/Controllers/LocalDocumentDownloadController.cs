using ClaimsModule.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.Api.Controllers;

/// <summary>
/// Ticket 10's local-filesystem fallback stands in for Azure Blob's SAS URL: the link
/// <c>LocalFileStorageService.GetDownloadUrlAsync</c> returns points here, carrying its own
/// HMAC signature and expiry rather than a bearer token — mirroring a real SAS URL, which is
/// also usable by itself, with no Authorization header. <c>[AllowAnonymous]</c> is therefore
/// correct here, not an oversight: this endpoint is its own authorization mechanism.
///
/// <see cref="ILocalSignedDocumentLinkService"/> is only registered when
/// <c>Storage:Provider</c> is <c>Local</c> (see Infrastructure's DependencyInjection) — resolving
/// it as nullable means an Azure-Blob-configured deployment 404s here instead of throwing, since
/// nothing should ever generate a link pointing at this endpoint in that configuration.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/documents/local")]
public class LocalDocumentDownloadController(ILocalSignedDocumentLinkService? linkService) : ControllerBase
{
    [HttpGet]
    public IActionResult Get(
        [FromQuery] string path,
        [FromQuery] long expires,
        [FromQuery] string signature,
        [FromQuery] string fileName,
        [FromQuery] string contentType)
    {
        if (linkService is null)
        {
            return NotFound();
        }

        var stream = linkService.TryOpenSignedUrl(path, expires, signature);
        if (stream is null)
        {
            return Unauthorized();
        }

        return File(stream, contentType, fileName);
    }
}
