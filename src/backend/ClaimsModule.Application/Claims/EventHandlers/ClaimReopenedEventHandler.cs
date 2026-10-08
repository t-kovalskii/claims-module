using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Models;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Events;
using MediatR;

namespace ClaimsModule.Application.Claims.EventHandlers;

/// <summary>BR-A-02 / FRS §14.1 / Ticket 06: writes the CLAIM_REOPENED audit entry. NewValue
/// carries the reopen reason, per the FRS's own EventType table. The claim's persisted status
/// is already Open by the time this runs (see Claim.ApplyTransition's judgement call).</summary>
public class ClaimReopenedEventHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IDateTime dateTime)
    : INotificationHandler<DomainEventNotification<ClaimReopenedEvent>>
{
    public Task Handle(DomainEventNotification<ClaimReopenedEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        context.ClaimAuditLogs.Add(new ClaimAuditLog
        {
            ClaimId = domainEvent.ClaimId,
            OrganizationId = domainEvent.OrganizationId,
            EventType = AuditEventTypes.ClaimReopened,
            Description = $"Claim reopened and returned to Open. Reason: {domainEvent.ReopenReason}",
            OldValue = "Closed",
            NewValue = domainEvent.ReopenReason,
            CreatedAt = dateTime.UtcNow,
            CreatedByUserId = currentUserService.UserId
        });

        return Task.CompletedTask;
    }
}
