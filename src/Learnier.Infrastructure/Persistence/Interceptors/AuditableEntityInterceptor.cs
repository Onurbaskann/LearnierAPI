using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Learnier.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Denetim alanlarini kaydetme aninda otomatik doldurur.
/// </summary>
/// <remarks>
/// Bu alanlarin elle set edilmesi gerekmez ve set edilmemeli: tek yerden doldurulmasi
/// tutarliligi garanti eder ve her handler'da tekrar eden kodu ortadan kaldirir.
/// </remarks>
internal sealed class AuditableEntityInterceptor(
    IClock clock,
    ICurrentUser currentUser)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditFields(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = clock.UtcNow;
        var userId = currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = userId;
                    break;

                // Sahip oldugu bir koleksiyon degistiginde varligin kendisi Modified
                // gorunmeyebilir; bu yuzden owned entity degisiklikleri de guncelleme sayilir.
                case EntityState.Modified:
                case EntityState.Unchanged when entry.References.Any(r => r.TargetEntry?.State is EntityState.Added or EntityState.Modified):
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    break;

                default:
                    break;
            }
        }
    }
}
