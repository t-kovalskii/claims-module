using ClaimsModule.Domain.Common;
using MediatR;

namespace ClaimsModule.Application.Common.Models;

/// <summary>Wraps a dependency-free Domain event so it can be dispatched as a MediatR notification.</summary>
public class DomainEventNotification<TDomainEvent>(TDomainEvent domainEvent) : INotification
    where TDomainEvent : BaseEvent
{
    public TDomainEvent DomainEvent { get; } = domainEvent;
}
