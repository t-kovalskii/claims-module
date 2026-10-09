using MediatR;

namespace ClaimsModule.Application.Claims.Commands.RetractReserveTransaction;

/// <summary>FRS §10.2 POST /api/claims/{id}/reserves/{txnId}/retract: the original submitter
/// retracts their own still-pending reserve transaction (ApprovalStatus → Cancelled) so a revised
/// amount can be submitted as a new transaction. Ticket 07.</summary>
public record RetractReserveTransactionCommand(Guid ClaimId, Guid ReserveHistoryId) : IRequest<RetractReserveTransactionResult>;

public record RetractReserveTransactionResult(Guid ReserveComponentId, Guid ReserveHistoryId, decimal CurrentAmount);
