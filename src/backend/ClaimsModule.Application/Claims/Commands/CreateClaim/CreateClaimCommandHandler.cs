using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Claims.Commands.CreateClaim;

/// <summary>
/// ADR-0001: the repository + unit-of-work path — the only command handler in this ticket that
/// mutates the Claim aggregate. Wraps the atomic claim-number counter (ADR-0008) and the
/// aggregate insert in one explicit transaction so a failed creation never leaves a consumed,
/// gapped claim number behind.
/// </summary>
public class CreateClaimCommandHandler(
    IUnitOfWork unitOfWork,
    IClaimRepository claimRepository,
    IClaimNumberGenerator claimNumberGenerator,
    IApplicationDbContext context,
    ICurrentUserService currentUserService)
    : IRequestHandler<CreateClaimCommand, CreateClaimResult>
{
    public Task<CreateClaimResult> Handle(CreateClaimCommand request, CancellationToken cancellationToken)
    {
        var organizationId = currentUserService.OrganizationId
            ?? throw new AuthenticationException("No authenticated organisation context.");

        return unitOfWork.ExecuteInTransactionAsync(ct => CreateAsync(request, organizationId, ct), cancellationToken);
    }

    private async Task<CreateClaimResult> CreateAsync(
        CreateClaimCommand request, Guid organizationId, CancellationToken cancellationToken)
    {
        string? policyNumber = null;
        string? clientNameFromPolicy = null;

        if (request.PolicyId is not null)
        {
            var policy = await context.Policies
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.PolicyId, cancellationToken);

            policyNumber = policy?.PolicyNumber;
            clientNameFromPolicy = policy?.ClientName;
        }

        // Judgement call (see Ticket 04's Comments): when no policy is linked, ClientName is
        // derived from the first Claimant party rather than left blank, since the claims list
        // and detail screens always display it.
        var clientName = clientNameFromPolicy
            ?? ResolveClientNameFromParties(request.Parties)
            ?? "Unknown";

        var claimNumber = await claimNumberGenerator.GenerateAsync(organizationId, cancellationToken);

        var lossEvent = new LossEvent
        {
            OrganizationId = organizationId,
            LossDate = request.LossEvent.LossDate,
            LossDescription = request.LossEvent.LossDescription,
            LossLocation = request.LossEvent.LossLocation,
            CauseOfLossCodeId = request.LossEvent.CauseOfLossCodeId,
            EstimatedLossAmount = request.LossEvent.EstimatedLossAmount,
            PoliceReportNumber = request.LossEvent.PoliceReportNumber
        };

        var parties = (request.Parties ?? []).Select(p => new ClaimParty
        {
            OrganizationId = organizationId,
            Role = p.Role,
            PartyType = p.PartyType,
            FirstName = p.FirstName,
            LastName = p.LastName,
            CompanyName = p.CompanyName,
            Email = p.Email,
            Phone = p.Phone,
            Notes = p.Notes,
            IsActive = true
        });

        var riskObjects = (request.RiskObjects ?? []).Select(r => new ClaimRiskObject
        {
            OrganizationId = organizationId,
            AssetType = r.AssetType,
            AssetDescription = r.AssetDescription,
            DamageDescription = r.DamageDescription,
            IsPrimary = r.IsPrimary,
            AssetReference = r.AssetReference
        });

        var claim = Claim.Create(
            organizationId,
            claimNumber,
            request.PolicyId,
            policyNumber,
            clientName,
            request.AssignedHandlerUserId,
            lossEvent,
            parties,
            riskObjects);

        claimRepository.Add(claim);

        // Triggers DispatchDomainEventsInterceptor (ClaimCreated -> ClaimAuditLog insert) in the
        // same SaveChanges batch, and therefore the same transaction (ADR-0003).
        await context.SaveChangesAsync(cancellationToken);

        return new CreateClaimResult(claim.Id, claim.ClaimNumber, claim.Status.ToString());
    }

    private static string? ResolveClientNameFromParties(List<CreateClaimPartyRequest>? parties)
    {
        var claimant = parties?.FirstOrDefault(p => p.Role == ClaimPartyRole.Claimant);
        if (claimant is null)
        {
            return null;
        }

        var name = claimant.PartyType == ClaimPartyType.Company
            ? claimant.CompanyName
            : $"{claimant.FirstName} {claimant.LastName}".Trim();

        return string.IsNullOrWhiteSpace(name) ? null : name;
    }
}
