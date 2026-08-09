using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IRefreshTokenRepository"/>
internal sealed class EfRefreshTokenRepository(AppDbContext context) : IRefreshTokenRepository
{
    public async Task<RefreshToken?> FindByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        // Token izlenir: yenileme sirasinda ayni ornek uzerinde iptal isaretlenir.
        return await context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
    }

    public void Add(RefreshToken token) => context.RefreshTokens.Add(token);
}
