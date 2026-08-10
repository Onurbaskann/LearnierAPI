using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IEmailVerificationTokenRepository"/>
internal sealed class EfEmailVerificationTokenRepository(AppDbContext context)
    : IEmailVerificationTokenRepository
{
    public async Task<EmailVerificationToken?> FindByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        // Token izlenir: dogrulama sirasinda ayni ornek uzerinde tuketilmis isaretlenir.
        return await context.EmailVerificationTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
    }

    public void Add(EmailVerificationToken token) => context.EmailVerificationTokens.Add(token);
}
