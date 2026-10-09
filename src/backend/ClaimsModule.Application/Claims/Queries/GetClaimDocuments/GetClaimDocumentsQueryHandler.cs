using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Claims.Queries.GetClaimDocuments;

public class GetClaimDocumentsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetClaimDocumentsQuery, List<ClaimDocumentDto>>
{
    public async Task<List<ClaimDocumentDto>> Handle(GetClaimDocumentsQuery request, CancellationToken cancellationToken)
    {
        var claimExists = await context.Claims.AsNoTracking().AnyAsync(c => c.Id == request.ClaimId, cancellationToken);
        if (!claimExists)
        {
            throw new NotFoundException(nameof(Claim), request.ClaimId);
        }

        return await context.GetForClaimAsync(request.ClaimId, cancellationToken);
    }
}
