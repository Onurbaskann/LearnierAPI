using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IUserRepository"/>
internal sealed class EfUserRepository(AppDbContext context) : IUserRepository
{
    public async Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        // Buyuk/kucuk harf duyarsizligi kolonun citext tipinden gelir; burada
        // ayrica ToLower cagrilmaz - cagrilsaydi kolonun benzersizlik index'i
        // kullanilamaz ve her giris tam tarama yapardi.
        var normalized = email.Trim();

        // Kullanici izlenir: parola ozeti yenilenmesi gerekirse ayni ornek uzerinde
        // degistirilip kaydedilebilsin.
        return await context.Users
            .FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<UserMembership>> GetActiveMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken)
        => await context.Memberships
            // Giris istegi henuz bir organizasyon kapsaminda degil; filtrenin
            // devre disi kalmasi gereken durum tam olarak budur.
            .IgnoreQueryFilters([AppDbContext.TenantFilterName])
            .AsNoTracking()
            .Where(m => m.UserId == userId
                        && m.Status == MembershipStatus.Active
                        && m.Organization.Status == OrganizationStatus.Active)
            .Select(m => new UserMembership(
                m.Id,
                m.OrganizationId,
                m.Organization.Name,
                m.Organization.Slug,
                m.Roles.Select(r => r.Role.Code).ToList()))
            .ToListAsync(cancellationToken);
}
