using Learnier.Application.Common.Abstractions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Learnier.Infrastructure.Persistence;

/// <summary>
/// EF Core transaction'ini Application katmanindaki <see cref="ITransaction"/> soyutlamasina baglar.
/// </summary>
internal sealed class EfTransaction(IDbContextTransaction transaction) : ITransaction
{
    public Task CommitAsync(CancellationToken cancellationToken = default)
        => transaction.CommitAsync(cancellationToken);

    public Task RollbackAsync(CancellationToken cancellationToken = default)
        => transaction.RollbackAsync(cancellationToken);

    // Commit edilmeden dispose edilirse EF transaction'i geri alir.
    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}
