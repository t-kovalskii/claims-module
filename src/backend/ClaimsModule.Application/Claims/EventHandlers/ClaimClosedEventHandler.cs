using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Models;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Events;
using MediatR;

namespace ClaimsModule.Application.Claims.EventHandlers;

/// <summary>BR-A-02 / FRS §14.1 / Ticket 06: writes the CLAIM_CLOSED audit entry. NewValue
/// carries the closure reason, per the FRS's own EventType table.</summary>
public class ClaimClosedEventHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IDateTime dateTime)
    : INotificationHandler<DomainEventNotification<ClaimClosedEvent>>
{
    public Task Handle(DomainEventNotification<ClaimClosedEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        context.ClaimAuditLogs.Add(new ClaimAuditLog
        {
            ClaimId = domainEvent.ClaimId,
            OrganizationId = domainEvent.OrganizationId,
            EventType = AuditEventTypes.ClaimClosed,
            Description = string.IsNullOrWhiteSpace(domainEvent.ClosureReason)
                ? "Claim closed."
                : $"Claim closed. Reason: {domainEvent.ClosureReason}",
            OldValue = domainEvent.FromStatus,
            NewValue = domainEvent.ClosureReason,
            CreatedAt = dateTime.UtcNow,
            CreatedByUserId = currentUserService.UserId
        });

        return Task.CompletedTask;
    }
}
