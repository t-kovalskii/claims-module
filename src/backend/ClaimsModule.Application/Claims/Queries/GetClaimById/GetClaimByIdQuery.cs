using MediatR;

namespace ClaimsModule.Application.Claims.Queries.GetClaimById;

/// <summary>FRS §10.1 GET /api/claims/{id}: full detail (ADR-0002: direct projection). Reserves
/// and documents return empty collections until Tickets 07/10 land.</summary>
public record GetClaimByIdQuery(Guid Id) : IRequest<ClaimDetailDto>;

public record ClaimDetailDto(
    Guid Id,
    string ClaimNumber,
    string Status,
    Guid? PolicyId,
    string? PolicyNumber,
    string ClientName,
    Guid? AssignedHandlerUserId,
    LossEventDto LossEvent,
    List<ClaimPartyDto> Parties,
    List<ClaimRiskObjectDto> RiskObjects,
    List<object> Reserves,
    List<object> Documents,
    DateTimeOffset CreatedAt);

public record LossEventDto(
    DateTimeOffset LossDate,
    string LossDescription,
    string? LossLocation,
    Guid CauseOfLossCodeId,
    string CauseOfLossCode,
    string CauseOfLossName,
    decimal? EstimatedLossAmount,
    string? PoliceReportNumber);

public record ClaimPartyDto(
    Guid Id,
    string Role,
    string PartyType,
    string? FirstName,
    string? LastName,
    string? CompanyName,
    string? Email,
    string? Phone,
    string? Notes,
    bool IsActive);

public record ClaimRiskObjectDto(
    Guid Id,
    string AssetType,
    string AssetDescription,
    string? DamageDescription,
    bool IsPrimary,
    string? AssetReference);
