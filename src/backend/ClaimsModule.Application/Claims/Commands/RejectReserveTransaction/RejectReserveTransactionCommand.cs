using MediatR;

namespace ClaimsModule.Application.Claims.Commands.RejectReserveTransaction;

/// <summary>FRS §10.2 POST /api/claims/{id}/reserves/{txnId}/reject: a Supervisor/Manager
/// rejects a PendingApproval reserve transaction with a required reason. Ticket 08.</summary>
public record RejectReserveTransactionCommand(
    Guid ClaimId,
    Guid ReserveHistoryId,
    string RejectionReason) : IRequest<RejectReserveTransactionResult>;

public record RejectReserveTransactionResult(
    Guid ReserveComponentId,
    Guid ReserveHistoryId,
    string ApprovalStatus);
