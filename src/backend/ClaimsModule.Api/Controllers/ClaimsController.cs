using ClaimsModule.Application.Claims.Commands.CreateClaim;
using ClaimsModule.Application.Claims.Commands.TransitionClaimStatus;
using ClaimsModule.Application.Claims.Queries.GetClaimAuditLog;
using ClaimsModule.Application.Claims.Queries.GetClaimById;
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

    /// <summary>FRS §10.1: full claim detail (reserves/documents are empty until Tickets 07/10).</summary>
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

    /// <summary>Unbound query-string ints default to 0; callers that omit page/pageSize get the
    /// same page-1/20-per-page default both list endpoints share.</summary>
    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize) =>
        (page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize);
}

/// <summary>FRS §10.1's PUT /api/claims/{id}/status body shape. A route-bound id plus a body
/// can't both be [FromBody], so this is the body-only subset of TransitionClaimStatusCommand.</summary>
public record TransitionStatusRequest(ClaimStatus TargetStatus, string? Reason);
