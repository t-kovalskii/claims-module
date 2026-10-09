using ClaimsModule.Application.Claims.Queries.GetClaimDocuments;
using MediatR;

namespace ClaimsModule.Application.Claims.Queries.GetClaimById;

/// <summary>FRS §10.1 GET /api/claims/{id}: full detail (ADR-0002: direct projection). Documents
/// and Reserves are now real typed lists (Tickets 10/07, replacing the original
/// <c>List&lt;object&gt;</c> placeholders — see Ticket 04's Comments).</summary>
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
    List<ReserveComponentDto> Reserves,
    List<ClaimDocumentDto> Documents,
    DateTimeOffset CreatedAt);

/// <summary>Ticket 07: one row per <c>ReserveComponent</c>. <see cref="ApprovalStatus"/> and
/// <see cref="PendingAmount"/> are computed from the latest (by <c>ChangeSequence</c>) entry in
/// <see cref="History"/> — not persisted columns on the component itself (that column,
/// <c>ReserveComponent.Status</c>, is the FRS §9.5 Active/Closed lifecycle flag, exposed
/// separately as <see cref="Status"/>).</summary>
public record ReserveComponentDto(
    Guid Id,
    string ComponentType,
    decimal CurrentAmount,
    string Status,
    string? ApprovalStatus,
    decimal? PendingAmount,
    List<ReserveHistoryDto> History);

/// <summary>Ticket 07: one row per <c>ReserveHistory</c> transaction, reverse-chronological
/// (highest <see cref="ChangeSequence"/> first) within its component.</summary>
public record ReserveHistoryDto(
    Guid Id,
    string TransactionType,
    decimal Amount,
    decimal PreviousBalance,
    decimal NewBalance,
    string ApprovalStatus,
    string? ChangeReason,
    string PostingStatus,
    Guid? SubmittedByUserId,
    Guid? ApprovedByUserId,
    DateTimeOffset? ApprovedAt,
    Guid? RejectedByUserId,
    DateTimeOffset? RejectedAt,
    string? RejectionReason,
    int ChangeSequence,
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
