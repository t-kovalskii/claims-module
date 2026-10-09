using MediatR;

namespace ClaimsModule.Application.Claims.Queries.GetClaimDocuments;

/// <summary>Ticket 10: <c>GET /api/claims/{id}/documents</c> — the documents attached to a
/// claim, newest first. Not paginated: unlike the claims list/audit log, the FRS describes this
/// only as "lists documents with metadata", and a claim's document count is never expected to
/// approach a scale needing it.</summary>
public record GetClaimDocumentsQuery(Guid ClaimId) : IRequest<List<ClaimDocumentDto>>;

public record ClaimDocumentDto(
    Guid Id,
    string FileName,
    string? DocumentType,
    string ContentType,
    long SizeBytes,
    Guid? UploadedByUserId,
    DateTimeOffset UploadedAt);
