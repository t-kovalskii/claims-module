using MediatR;

namespace ClaimsModule.Application.Claims.Commands.UploadClaimDocument;

/// <summary>
/// Ticket 10 / FRS §9.7: attaches a new document to a claim via the configured
/// <c>IStorageService</c>. <paramref name="Content"/> is the controller action's own
/// <c>IFormFile.OpenReadStream()</c> — read synchronously within this one request/handler
/// lifecycle, never persisted as-is beyond the upload itself.
/// </summary>
public record UploadClaimDocumentCommand(
    Guid ClaimId,
    string FileName,
    string ContentType,
    long SizeBytes,
    Stream Content,
    string? DocumentType
) : IRequest<UploadClaimDocumentResult>;

public record UploadClaimDocumentResult(
    Guid Id,
    Guid ClaimId,
    string FileName,
    string? DocumentType,
    string ContentType,
    long SizeBytes,
    Guid? UploadedByUserId,
    DateTimeOffset UploadedAt);
