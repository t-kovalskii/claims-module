using ClaimsModule.Application.Reference.Queries.GetCauseOfLossCodes;
using ClaimsModule.Application.Reference.Queries.GetClaimStatuses;
using ClaimsModule.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.Api.Controllers;

/// <summary>Ticket 03: reference data FNOL intake and the status-transition UI depend on.</summary>
[ApiController]
[Authorize]
[Route("api/reference")]
public class ReferenceController(ISender sender) : ControllerBase
{
    /// <summary>FRS §5.6: only active codes, optionally filtered by peril category.</summary>
    [HttpGet("cause-of-loss-codes")]
    [ProducesResponseType(typeof(List<CauseOfLossCodeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<CauseOfLossCodeDto>>> GetCauseOfLossCodes(
        [FromQuery] PerilCategory? perilCategory,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCauseOfLossCodesQuery(perilCategory), cancellationToken);
        return Ok(result);
    }

    /// <summary>FRS §4.1/4.2: every claim status with its valid next transitions and required permission.</summary>
    [HttpGet("claim-statuses")]
    [ProducesResponseType(typeof(List<ClaimStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<ClaimStatusDto>>> GetClaimStatuses(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetClaimStatusesQuery(), cancellationToken);
        return Ok(result);
    }
}
