using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Models;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Events;
using MediatR;

namespace ClaimsModule.Application.Claims.EventHandlers;

/// <summary>
/// BR-A-02 / this ticket: writes the ClaimAuditLog entry for a created claim, in-process and in
/// the same transaction as the triggering command (ADR-0003) — DispatchDomainEventsInterceptor
/// publishes this notification from within SaveChanges' SavingChanges hook, so adding this
/// entity to the context here still lands in the same SaveChanges batch.
/// </summary>
public class ClaimCreatedEventHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IDateTime dateTime)
    : INotificationHandler<DomainEventNotification<ClaimCreatedEvent>>
{
    public Task Handle(DomainEventNotification<ClaimCreatedEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        context.ClaimAuditLogs.Add(new ClaimAuditLog
        {
            ClaimId = domainEvent.ClaimId,
            OrganizationId = domainEvent.OrganizationId,
            EventType = AuditEventTypes.ClaimCreated,
            Description = $"Claim {domainEvent.ClaimNumber} created.",
            CreatedAt = dateTime.UtcNow,
            CreatedByUserId = currentUserService.UserId
        });

        return Task.CompletedTask;
    }
}
