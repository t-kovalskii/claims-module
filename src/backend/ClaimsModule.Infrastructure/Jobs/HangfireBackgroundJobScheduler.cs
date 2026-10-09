using System.Linq.Expressions;
using ClaimsModule.Application.Common.Interfaces;
using Hangfire;

namespace ClaimsModule.Infrastructure.Jobs;

/// <summary>Ticket 08: the Hangfire-backed implementation of <see cref="IBackgroundJobScheduler"/>
/// — <see cref="IBackgroundJobClient"/> is registered by the Api project's own
/// <c>builder.Services.AddHangfire(...)</c>/<c>AddHangfireServer()</c> call, so this class only
/// needs the lightweight <c>Hangfire.Core</c> package, not the SQL Server storage provider
/// itself.</summary>
public class HangfireBackgroundJobScheduler(IBackgroundJobClient client) : IBackgroundJobScheduler
{
    public string Enqueue<TJob>(Expression<Func<TJob, Task>> methodCall) => client.Enqueue(methodCall);
}
