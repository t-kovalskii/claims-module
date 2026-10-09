using ClaimsModule.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Claims.Queries.GetClaimDocuments;

/// <summary>Shared projection behind both <see cref="GetClaimDocumentsQueryHandler"/> and
/// <c>GetClaimByIdQueryHandler</c>'s Documents section — kept in one place so the two reads stay
/// identical rather than drifting as separately-maintained copies.</summary>
public static class ClaimDocumentProjection
{
    public static Task<List<ClaimDocumentDto>> GetForClaimAsync(
        this IApplicationDbContext context, Guid claimId, CancellationToken cancellationToken) =>
        context.ClaimDocuments
            .AsNoTracking()
            .Where(d => d.ClaimId == claimId)
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new ClaimDocumentDto(
                d.Id,
                d.OriginalFileName,
                d.DocumentType,
                d.ContentType,
                d.SizeBytes,
                d.UploadedByUserId,
                d.UploadedAt))
            .ToListAsync(cancellationToken);
}
