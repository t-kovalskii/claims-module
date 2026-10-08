using ClaimsModule.Application.Policies.Queries.GetPolicyCoverage;
using ClaimsModule.Application.Policies.Queries.SearchPolicies;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.Api.Controllers;

/// <summary>Ticket 03: simulated policy search + coverage lookup FNOL intake depends on.</summary>
[ApiController]
[Authorize]
[Route("api/policies")]
public class PoliciesController(ISender sender) : ControllerBase
{
    /// <summary>FRS §5.2: partial match on policy number or client name.</summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(List<PolicySummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<PolicySummaryDto>>> Search(
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SearchPoliciesQuery(q), cancellationToken);
        return Ok(result);
    }

    /// <summary>FRS §9: the coverages seeded for a given policy.</summary>
    [HttpGet("{id:guid}/coverage")]
    [ProducesResponseType(typeof(List<PolicyCoverageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<PolicyCoverageDto>>> GetCoverage(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetPolicyCoverageQuery(id), cancellationToken);
        return Ok(result);
    }
}
