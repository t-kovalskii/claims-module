using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Claims.Commands.UploadClaimDocument;

/// <summary>
/// Ticket 10: stores the file via the configured <see cref="IStorageService"/> under a path
/// scoped to organisation and claim, then records its metadata. Not an aggregate write (see
/// <see cref="Domain.Entities.ClaimDocument"/>'s remarks) — writes <c>ClaimDocuments</c> and the
/// matching audit-log row directly via <see cref="IApplicationDbContext"/>, same precedent as
/// <c>SlaBreachScanJob</c>'s direct audit insert, both landing in one <c>SaveChangesAsync</c>
/// batch (ADR-0003).
/// </summary>
public class UploadClaimDocumentCommandHandler(
    IApplicationDbContext context,
    IStorageService storageService,
    ICurrentUserService currentUserService,
    IDateTime dateTime)
    : IRequestHandler<UploadClaimDocumentCommand, UploadClaimDocumentResult>
{
    public async Task<UploadClaimDocumentResult> Handle(
        UploadClaimDocumentCommand request, CancellationToken cancellationToken)
    {
        var organizationId = currentUserService.OrganizationId
            ?? throw new AuthenticationException("No authenticated organisation context.");

        var claimExists = await context.Claims.AnyAsync(c => c.Id == request.ClaimId, cancellationToken);
        if (!claimExists)
        {
            throw new NotFoundException(nameof(Claim), request.ClaimId);
        }

        var sanitizedFileName = Common.FileNameSanitizer.Sanitize(request.FileName);
        var storagePath = $"{organizationId}/{request.ClaimId}/{Guid.NewGuid():N}_{sanitizedFileName}";

        await storageService.SaveAsync(storagePath, request.Content, request.ContentType, cancellationToken);

        var uploadedAt = dateTime.UtcNow;

        var document = new ClaimDocument
        {
            OrganizationId = organizationId,
            ClaimId = request.ClaimId,
            OriginalFileName = request.FileName,
            SanitizedFileName = sanitizedFileName,
            ContentType = request.ContentType,
            SizeBytes = request.SizeBytes,
            StoragePath = storagePath,
            DocumentType = request.DocumentType,
            UploadedByUserId = currentUserService.UserId,
            UploadedAt = uploadedAt
        };

        context.ClaimDocuments.Add(document);

        context.ClaimAuditLogs.Add(new ClaimAuditLog
        {
            ClaimId = request.ClaimId,
            OrganizationId = organizationId,
            EventType = AuditEventTypes.DocumentUploaded,
            Description = $"Document \"{sanitizedFileName}\" uploaded.",
            RelatedEntityId = document.Id,
            RelatedEntityType = nameof(ClaimDocument),
            CreatedAt = uploadedAt,
            CreatedByUserId = currentUserService.UserId
        });

        await context.SaveChangesAsync(cancellationToken);

        return new UploadClaimDocumentResult(
            document.Id,
            document.ClaimId,
            document.OriginalFileName,
            document.DocumentType,
            document.ContentType,
            document.SizeBytes,
            document.UploadedByUserId,
            document.UploadedAt);
    }
}
