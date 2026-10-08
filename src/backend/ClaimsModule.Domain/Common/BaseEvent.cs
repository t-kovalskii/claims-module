namespace ClaimsModule.Domain.Common;

/// <summary>
/// Plain marker so Domain stays dependency-free; Application wraps these as MediatR
/// notifications (see DomainEventNotification&lt;T&gt;) for in-process dispatch (ADR-0003).
/// </summary>
public abstract class BaseEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
