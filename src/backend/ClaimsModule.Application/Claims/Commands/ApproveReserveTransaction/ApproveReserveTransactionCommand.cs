using MediatR;

namespace ClaimsModule.Application.Claims.Commands.ApproveReserveTransaction;

/// <summary>FRS §10.2 POST /api/claims/{id}/reserves/{txnId}/approve: a Supervisor/Manager
/// approves a PendingApproval reserve transaction. <see cref="OverrideAggregateCap"/> is the
/// Manager's explicit BR-R-05 opt-in when this approval would push the claim's total approved
/// reserves over $10,000,000 — ignored (and safe to omit) otherwise. Ticket 08.</summary>
public record ApproveReserveTransactionCommand(
    Guid ClaimId,
    Guid ReserveHistoryId,
    bool OverrideAggregateCap) : IRequest<ApproveReserveTransactionResult>;

public record ApproveReserveTransactionResult(
    Guid ReserveComponentId,
    Guid ReserveHistoryId,
    string ApprovalStatus,
    decimal CurrentAmount);
