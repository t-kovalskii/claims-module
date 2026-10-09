using ClaimsModule.Application.Claims.Commands.ApproveReserveTransaction;
using ClaimsModule.Application.Claims.Commands.CreateClaim;
using ClaimsModule.Application.Claims.Commands.RejectReserveTransaction;
using ClaimsModule.Application.Claims.Commands.RetractReserveTransaction;
using ClaimsModule.Application.Claims.Commands.SubmitReserveTransaction;
using ClaimsModule.Application.Claims.Commands.TransitionClaimStatus;
using ClaimsModule.Application.Claims.Commands.UploadClaimDocument;
using ClaimsModule.Application.Claims.Queries.GetClaimAuditLog;
using ClaimsModule.Application.Claims.Queries.GetClaimById;
using ClaimsModule.Application.Claims.Queries.GetClaimDocumentDownloadUrl;
using ClaimsModule.Application.Claims.Queries.GetClaimDocuments;
using ClaimsModule.Application.Claims.Queries.GetClaims;
using ClaimsModule.Application.Common.Models;
using ClaimsModule.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.Api.Controllers;

/// <summary>Ticket 04: FNOL intake — create, list, and view claims.</summary>
[ApiController]
[Authorize]
[Route("api/claims")]
public class ClaimsController(ISender sender) : ControllerBase
{
    /// <summary>FRS §5.2/§10.1: creates the Claim, LossEvent, parties, and risk objects
    /// transactionally and returns the atomically-generated claim number.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateClaimResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CreateClaimResult>> Create(CreateClaimCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>FRS §10.1: paginated, filterable claims list.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ClaimSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<ClaimSummaryDto>>> GetAll(
        [FromQuery] ClaimStatus? status,
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        [FromQuery] Guid? assignedHandlerUserId,
        [FromQuery] string? causeOfLossCode,
        [FromQuery] string? search,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedPageSize) = NormalizePaging(page, pageSize);
        var query = new GetClaimsQuery(
            status,
            dateFrom,
            dateTo,
            assignedHandlerUserId,
            causeOfLossCode,
            search,
            normalizedPage,
            normalizedPageSize);

        return Ok(await sender.Send(query, cancellationToken));
    }

    /// <summary>FRS §10.1: full claim detail, including reserves and documents.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ClaimDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClaimDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await sender.Send(new GetClaimByIdQuery(id), cancellationToken));
    }

    /// <summary>FRS §10.1: reverse-chronological, paginated audit log for the claim.</summary>
    [HttpGet("{id:guid}/audit")]
    [ProducesResponseType(typeof(PagedResult<ClaimAuditLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<ClaimAuditLogDto>>> GetAudit(
        Guid id,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedPageSize) = NormalizePaging(page, pageSize);
        var query = new GetClaimAuditLogQuery(id, normalizedPage, normalizedPageSize);
        return Ok(await sender.Send(query, cancellationToken));
    }

    /// <summary>FRS §10.1 / BR-ST-01..04 (Ticket 06): transitions the claim to
    /// <paramref name="body"/>.TargetStatus if the transition table, the caller's role, and the
    /// target status's own conditions all allow it. Body: <c>{ targetStatus, reason? }</c>.</summary>
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(typeof(TransitionClaimStatusResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TransitionClaimStatusResult>> TransitionStatus(
        Guid id, TransitionStatusRequest body, CancellationToken cancellationToken)
    {
        var command = new TransitionClaimStatusCommand(id, body.TargetStatus, body.Reason);
        return Ok(await sender.Send(command, cancellationToken));
    }

    /// <summary>FRS §10.2 / Ticket 07: opens a reserve component or submits a further transaction
    /// against an already-open one. Body: <c>{ componentType, amount, changeReason }</c> — see
    /// SubmitReserveTransactionCommand's remarks for why there's no client-supplied
    /// <c>transactionType</c> field, unlike the FRS's own literal request shape.</summary>
    [HttpPost("{id:guid}/reserves")]
    [ProducesResponseType(typeof(SubmitReserveTransactionResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SubmitReserveTransactionResult>> SubmitReserve(
        Guid id, SubmitReserveRequest body, CancellationToken cancellationToken)
    {
        var command = new SubmitReserveTransactionCommand(id, body.ComponentType, body.Amount, body.ChangeReason);
        var result = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, result);
    }

    /// <summary>FRS §10.2 / Ticket 07: the original submitter retracts their own still-pending
    /// reserve transaction before anyone approves/rejects it.</summary>
    [HttpPost("{id:guid}/reserves/{reserveHistoryId:guid}/retract")]
    [ProducesResponseType(typeof(RetractReserveTransactionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RetractReserveTransactionResult>> RetractReserve(
        Guid id, Guid reserveHistoryId, CancellationToken cancellationToken)
    {
        var command = new RetractReserveTransactionCommand(id, reserveHistoryId);
        return Ok(await sender.Send(command, cancellationToken));
    }

    /// <summary>FRS §10.2 / Ticket 08: a Supervisor/Manager approves a PendingApproval reserve
    /// transaction. <c>[Authorize(Roles = ...)]</c> here is the static half of ADR-0004's
    /// two-layer role check — it blocks Handlers outright; the amount-dependent
    /// Supervisor-vs-Manager split and the aggregate-cap override both live in
    /// Claim.ApproveReserveTransaction instead, since they can't be expressed as a fixed set of
    /// roles on the endpoint itself. Body: <c>{ overrideAggregateCap? }</c>, optional and
    /// ignored unless this approval would actually breach the $10,000,000 cap.</summary>
    [HttpPost("{id:guid}/reserves/{reserveHistoryId:guid}/approve")]
    [Authorize(Roles = "Supervisor,Manager")]
    [ProducesResponseType(typeof(ApproveReserveTransactionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApproveReserveTransactionResult>> ApproveReserve(
        Guid id, Guid reserveHistoryId, ApproveReserveRequest? body, CancellationToken cancellationToken)
    {
        var command = new ApproveReserveTransactionCommand(id, reserveHistoryId, body?.OverrideAggregateCap ?? false);
        return Ok(await sender.Send(command, cancellationToken));
    }

    /// <summary>FRS §10.2 / Ticket 08: a Supervisor/Manager rejects a PendingApproval reserve
    /// transaction with a required reason. Body: <c>{ rejectionReason }</c>.</summary>
    [HttpPost("{id:guid}/reserves/{reserveHistoryId:guid}/reject")]
    [Authorize(Roles = "Supervisor,Manager")]
    [ProducesResponseType(typeof(RejectReserveTransactionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RejectReserveTransactionResult>> RejectReserve(
        Guid id, Guid reserveHistoryId, RejectReserveRequest body, CancellationToken cancellationToken)
    {
        var command = new RejectReserveTransactionCommand(id, reserveHistoryId, body.RejectionReason);
        return Ok(await sender.Send(command, cancellationToken));
    }

    /// <summary>Ticket 10 / FRS §9.7: uploads a document via the configured
    /// <c>IStorageService</c> (Azure Blob or local filesystem — a configuration switch, see
    /// <c>DependencyInjection.AddStorage</c>). Multipart form body: <c>file</c> plus an optional
    /// <c>documentType</c> field.</summary>
    [HttpPost("{id:guid}/documents")]
    [RequestSizeLimit(UploadClaimDocumentCommandValidator.MaxSizeBytes)]
    [ProducesResponseType(typeof(UploadClaimDocumentResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UploadClaimDocumentResult>> UploadDocument(
        Guid id, [FromForm] UploadDocumentRequest body, CancellationToken cancellationToken)
    {
        await using var stream = body.File.OpenReadStream();
        var command = new UploadClaimDocumentCommand(
            id, body.File.FileName, body.File.ContentType, body.File.Length, stream, body.DocumentType);

        var result = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetDocuments), new { id }, result);
    }

    /// <summary>Ticket 10: lists the documents attached to a claim, newest first.</summary>
    [HttpGet("{id:guid}/documents")]
    [ProducesResponseType(typeof(List<ClaimDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ClaimDocumentDto>>> GetDocuments(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await sender.Send(new GetClaimDocumentsQuery(id), cancellationToken));
    }

    /// <summary>Ticket 10: a short-lived (1-hour) signed URL for retrieving the document
    /// directly from the storage provider, never streamed through this API.</summary>
    [HttpGet("{id:guid}/documents/{documentId:guid}/download-url")]
    [ProducesResponseType(typeof(ClaimDocumentDownloadUrlDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClaimDocumentDownloadUrlDto>> GetDocumentDownloadUrl(
        Guid id, Guid documentId, CancellationToken cancellationToken)
    {
        return Ok(await sender.Send(new GetClaimDocumentDownloadUrlQuery(id, documentId), cancellationToken));
    }

    /// <summary>Unbound query-string ints default to 0; callers that omit page/pageSize get the
    /// same page-1/20-per-page default both list endpoints share.</summary>
    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize) =>
        (page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize);
}

/// <summary>FRS §10.1's PUT /api/claims/{id}/status body shape. A route-bound id plus a body
/// can't both be [FromBody], so this is the body-only subset of TransitionClaimStatusCommand.</summary>
public record TransitionStatusRequest(ClaimStatus TargetStatus, string? Reason);

/// <summary>FRS §10.2's POST /api/claims/{id}/reserves body shape — the body-only subset of
/// SubmitReserveTransactionCommand (same route-bound-id-plus-body constraint as above).</summary>
public record SubmitReserveRequest(ReserveComponentType ComponentType, decimal Amount, string? ChangeReason);

/// <summary>FRS §10.2's POST /api/claims/{id}/reserves/{txnId}/approve body shape — optional and
/// entirely omittable; see ClaimsController.ApproveReserve's remarks.</summary>
public record ApproveReserveRequest(bool OverrideAggregateCap);

/// <summary>FRS §10.2's POST /api/claims/{id}/reserves/{txnId}/reject body shape.</summary>
public record RejectReserveRequest(string RejectionReason);

/// <summary>Ticket 10's POST /api/claims/{id}/documents multipart body shape.</summary>
public record UploadDocumentRequest(IFormFile File, string? DocumentType);
