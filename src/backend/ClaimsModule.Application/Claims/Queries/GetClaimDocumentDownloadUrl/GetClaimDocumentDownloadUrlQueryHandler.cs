using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Claims.Queries.GetClaimDocumentDownloadUrl;

public class GetClaimDocumentDownloadUrlQueryHandler(IApplicationDbContext context, IStorageService storageService)
    : IRequestHandler<GetClaimDocumentDownloadUrlQuery, ClaimDocumentDownloadUrlDto>
{
    private static readonly TimeSpan LinkExpiry = TimeSpan.FromHours(1);

    public async Task<ClaimDocumentDownloadUrlDto> Handle(
        GetClaimDocumentDownloadUrlQuery request, CancellationToken cancellationToken)
    {
        var document = await context.ClaimDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId && d.ClaimId == request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(ClaimDocument), request.DocumentId);

        var url = await storageService.GetDownloadUrlAsync(
            document.StoragePath, document.OriginalFileName, document.ContentType, LinkExpiry, cancellationToken);

        return new ClaimDocumentDownloadUrlDto(url, DateTimeOffset.UtcNow.Add(LinkExpiry));
    }
}
