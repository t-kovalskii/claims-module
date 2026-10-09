using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Models;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Events;
using MediatR;

namespace ClaimsModule.Application.Claims.EventHandlers;

/// <summary>BR-A-02 / FRS §14.1 / Ticket 07: writes the RESERVE_CREATED audit entry for a
/// transaction routed to PendingApproval.</summary>
public class ReserveSubmittedForApprovalEventHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IDateTime dateTime)
    : INotificationHandler<DomainEventNotification<ReserveSubmittedForApprovalEvent>>
{
    public Task Handle(DomainEventNotification<ReserveSubmittedForApprovalEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        context.ClaimAuditLogs.Add(new ClaimAuditLog
        {
            ClaimId = domainEvent.ClaimId,
            OrganizationId = domainEvent.OrganizationId,
            EventType = AuditEventTypes.ReserveCreated,
            Description = $"{domainEvent.ComponentType} reserve transaction of {domainEvent.Amount:C} submitted, pending approval.",
            NewValue = domainEvent.Amount.ToString("F4"),
            RelatedEntityId = domainEvent.ReserveHistoryId,
            RelatedEntityType = nameof(ReserveHistory),
            CreatedAt = dateTime.UtcNow,
            CreatedByUserId = currentUserService.UserId
        });

        return Task.CompletedTask;
    }
}
