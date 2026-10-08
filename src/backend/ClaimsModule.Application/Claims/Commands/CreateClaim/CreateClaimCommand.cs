using ClaimsModule.Application.Common.Models;
using ClaimsModule.Domain.Entities;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.CreateClaim;

/// <summary>
/// FRS §5.2 / §10.1 POST /api/claims: FNOL intake in one call — Claim + LossEvent + parties +
/// risk objects, created atomically (ADR-0001's repository + unit-of-work path). No reserve yet
/// (Ticket 07).
/// </summary>
public record CreateClaimCommand(
    Guid? PolicyId,
    CreateClaimLossEventRequest LossEvent,
    Guid? AssignedHandlerUserId,
    List<CreateClaimPartyRequest>? Parties,
    List<CreateClaimRiskObjectRequest>? RiskObjects
) : IRequest<CreateClaimResult>;

public record CreateClaimLossEventRequest(
    DateTimeOffset LossDate,
    string LossDescription,
    string? LossLocation,
    Guid CauseOfLossCodeId,
    decimal? EstimatedLossAmount,
    string? PoliceReportNumber);

public record CreateClaimPartyRequest(
    ClaimPartyRole Role,
    ClaimPartyType PartyType,
    string? FirstName,
    string? LastName,
    string? CompanyName,
    string? Email,
    string? Phone,
    string? Notes);

public record CreateClaimRiskObjectRequest(
    ClaimRiskObjectAssetType AssetType,
    string AssetDescription,
    string? DamageDescription,
    bool IsPrimary,
    string? AssetReference);

/// <summary>FRS §5.2 step 3 / §10.1: 201 response, including the atomically-generated claim
/// number, plus any non-blocking validation warnings raised during intake (FRS §5.4).</summary>
public record CreateClaimResult(Guid Id, string ClaimNumber, string Status) : IHasValidationWarnings
{
    public List<string> Warnings { get; } = [];
}
