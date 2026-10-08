namespace ClaimsModule.Application.Common.Interfaces;

/// <summary>
/// Wraps an explicit database transaction for commands whose work spans more than one
/// independently-committing operation — e.g. the atomic claim-number counter increment
/// (ADR-0008) plus the Claim aggregate insert, which must commit or roll back together so a
/// failed claim creation never leaves a consumed, gapped claim number behind.
/// </summary>
public interface IUnitOfWork
{
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken);
}
