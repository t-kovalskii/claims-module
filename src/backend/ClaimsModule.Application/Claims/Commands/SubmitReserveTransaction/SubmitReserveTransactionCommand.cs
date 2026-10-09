using ClaimsModule.Domain.Entities;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.SubmitReserveTransaction;

/// <summary>
/// FRS §10.2 POST /api/claims/{id}/reserves: opens a reserve component (if none of
/// <see cref="ComponentType"/> exists yet on the claim) or submits a further transaction against
/// an already-open one. Ticket 07.
///
/// Deliberately does not take a client-supplied <c>transactionType</c> field, unlike the FRS's own
/// literal request-body shape (<c>{ component, amount, changeReason, transactionType }</c>):
/// whether this is the component's opening Add or a later Adjust is derived state (does a
/// component of this type already exist on the claim?), not something the client should assert —
/// see this ticket's Comments for the full judgement call.
/// </summary>
public record SubmitReserveTransactionCommand(
    Guid ClaimId,
    ReserveComponentType ComponentType,
    decimal Amount,
    string? ChangeReason) : IRequest<SubmitReserveTransactionResult>;

/// <summary>FRS §10.2: "Returns 201 with the created transaction and its approval status."</summary>
public record SubmitReserveTransactionResult(
    Guid ReserveComponentId,
    Guid ReserveHistoryId,
    string ComponentType,
    string TransactionType,
    decimal Amount,
    decimal PreviousBalance,
    decimal NewBalance,
    string ApprovalStatus,
    decimal CurrentAmount,
    int ChangeSequence);
