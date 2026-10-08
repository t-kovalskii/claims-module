using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Models;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Events;
using MediatR;

namespace ClaimsModule.Application.Claims.EventHandlers;

/// <summary>BR-A-02 / Ticket 06: writes the ClaimAuditLog entry for every status transition
/// except Closed/Reopened, which raise their own, more specific events/handlers.</summary>
public class ClaimStatusChangedEventHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IDateTime dateTime)
    : INotificationHandler<DomainEventNotification<ClaimStatusChangedEvent>>
{
    public Task Handle(DomainEventNotification<ClaimStatusChangedEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        context.ClaimAuditLogs.Add(new ClaimAuditLog
        {
            ClaimId = domainEvent.ClaimId,
            OrganizationId = domainEvent.OrganizationId,
            EventType = AuditEventTypes.StatusChanged,
            Description = string.IsNullOrWhiteSpace(domainEvent.Reason)
                ? $"Claim status changed from {domainEvent.FromStatus} to {domainEvent.ToStatus}."
                : $"Claim status changed from {domainEvent.FromStatus} to {domainEvent.ToStatus}. Reason: {domainEvent.Reason}",
            OldValue = domainEvent.FromStatus,
            NewValue = domainEvent.ToStatus,
            CreatedAt = dateTime.UtcNow,
            CreatedByUserId = currentUserService.UserId
        });

        return Task.CompletedTask;
    }
}
