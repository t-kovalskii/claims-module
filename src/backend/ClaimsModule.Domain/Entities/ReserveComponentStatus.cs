namespace ClaimsModule.Domain.Entities;

/// <summary>
/// FRS §9.5's <c>ClaimReserveComponents.Status</c> column — the component's own lifecycle state,
/// distinct from the per-transaction <see cref="ReserveApprovalStatus"/> on <see cref="ReserveHistory"/>.
/// Ticket 07 never transitions a component to <see cref="Closed"/> — no "retire this component"
/// action exists in this slice's scope — so every component this ticket creates stays
/// <see cref="Active"/>. Defined now purely for FRS schema fidelity / Ticket 08 compatibility.
/// </summary>
public enum ReserveComponentStatus
{
    Active,
    Closed
}
