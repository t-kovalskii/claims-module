# In-process domain events via MediatR, no outbox

`Claim` raises domain events (`ClaimCreated`, `ReserveApproved`, `ReserveRejected`, etc.) consumed by audit logging and Hangfire job enqueueing. We dispatch these as MediatR `INotification`s in-process, within the same transaction as the triggering command's `SaveChangesAsync`, rather than persisting them to an outbox table for separate processing.

An outbox exists to guarantee reliable delivery across a process or service boundary — e.g. publishing to a broker that another service consumes. Every consumer of these events lives in the same process and the same SQL Server database. Hangfire's own job storage is already durable and transactional against that same database, so enqueueing a Hangfire job from a domain event handler already gets the reliability guarantee an outbox would provide. Adding one on top would solve a distributed-systems problem this single-service greenfield slice doesn't have.
