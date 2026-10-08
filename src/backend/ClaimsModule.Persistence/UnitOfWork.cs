using ClaimsModule.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence;

public class UnitOfWork(IApplicationDbContext context) : IUnitOfWork
{
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var result = await operation(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return result;
    }
}
