using MediatR;

namespace ClaimsModule.Application.Claims.Queries.GetClaimDocumentDownloadUrl;

/// <summary>Ticket 10: "Document retrieval returns a short-lived (1-hour) signed URL rather than
/// streaming bytes through the API."</summary>
public record GetClaimDocumentDownloadUrlQuery(Guid ClaimId, Guid DocumentId) : IRequest<ClaimDocumentDownloadUrlDto>;

public record ClaimDocumentDownloadUrlDto(Uri Url, DateTimeOffset ExpiresAt);
