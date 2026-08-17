using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Security;
using Learnier.Domain.Billing;
using Learnier.Domain.Catalog;
using Learnier.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Learnier.Infrastructure.Persistence.Seeding;

/// <summary>
/// Gelistirme ortami icin ornek kurum ve test hesaplari olusturur.
/// </summary>
/// <remarks>
/// <para>
/// Yalnizca Development'ta calisir. Buradaki parolalar gercek sir degildir; ayni
/// hesaplar istemci tarafinda da test hesabi olarak kullaniliyor. Yine de bu sinifin
/// uretimde calismamasi cagrildigi yerde garanti altina alinir.
/// </para>
/// <para>
/// Referans verisinden (izin ve roller) ayri tutulmasinin sebebi budur: biri her
/// ortamda gerekli, digeri yalnizca gelistirmede.
/// </para>
/// </remarks>
internal sealed partial class DevelopmentDataSeeder(
    AppDbContext context,
    IPasswordHasher passwordHasher,
    IClock clock,
    ILogger<DevelopmentDataSeeder> logger)
{
    private const string OrganizationSlug = "learnier";
    private const string DemoStudentEmail = "ogrenci@hotmail.com";
    private const string EnglishSubjectName = "İngilizce";
    private const string EnglishPlanName = "Demo İngilizce Paketi";

    [LoggerMessage(Level = LogLevel.Information, Message = "Seed: development organization created.")]
    private static partial void LogOrganizationCreated(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seed: development user {Email} created.")]
    private static partial void LogUserCreated(ILogger logger, string email);

    /// <summary>
    /// Istemcideki test hesaplariyla ayni olacak sekilde tanimli hesaplar.
    /// </summary>
    private static readonly DevelopmentAccount[] Accounts =
    [
        new("admin@hotmail.com", "admin123", "Learnier", "Admin", SystemRoles.OrganizationAdmin),
        new("ogrenci@hotmail.com", "ogrenci123", "Deniz", "Yilmaz", SystemRoles.Student),
        new("ogretmen@hotmail.com", "ogretmen123", "Emine", "Tekin", SystemRoles.Instructor),
        // Paketsiz panel durumunu test etmek icin kullanilan hesap; abonelik verilmez.
        new("paketsiz@hotmail.com", "paketsiz123", "Kaan", "Aydin", SystemRoles.Student)
    ];

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var organization = await EnsureOrganizationAsync(cancellationToken);
        var roles = await LoadSystemRolesAsync(cancellationToken);

        foreach (var account in Accounts)
        {
            await EnsureAccountAsync(account, organization, roles, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        await EnsureDemoEnglishPackageAsync(organization, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureDemoEnglishPackageAsync(
        Organization organization,
        CancellationToken cancellationToken)
    {
        var subject = await context.Subjects.FirstOrDefaultAsync(
            s => s.OrganizationId == organization.Id && s.Name == EnglishSubjectName,
            cancellationToken);

        if (subject is null)
        {
            subject = Subject.Create(organization.Id, EnglishSubjectName, "ingilizce");
            context.Subjects.Add(subject);
        }

        var plan = await context.SubscriptionPlans
            .Include(p => p.Prices)
            .Include(p => p.Entitlements)
            .FirstOrDefaultAsync(
                p => p.OrganizationId == organization.Id && p.Name == EnglishPlanName,
                cancellationToken);

        PlanPrice price;
        if (plan is null)
        {
            plan = SubscriptionPlan.CreateLessonPackage(
                organization.Id,
                EnglishPlanName,
                monthlyLessonCredits: 12,
                lessonDurationMinutes: 50,
                "Geliştirme öğrenci hesabı için İngilizce erişimi.");
            plan.Activate();
            price = plan.AddPrice("TRY", 0, BillingInterval.Month, 12, clock.UtcNow);
            context.SubscriptionPlans.Add(plan);
        }
        else
        {
            plan.ConfigureLessonPackage(12, 50);
            price = plan.Prices.FirstOrDefault(p => p.Status == PlanPriceStatus.Active)
                ?? plan.AddPrice("TRY", 0, BillingInterval.Month, 12, clock.UtcNow);
        }

        var hasSubjectAccess = await context.PlanSubjectAccess.AnyAsync(
            access => access.PlanId == plan.Id && access.SubjectId == subject.Id,
            cancellationToken);

        if (!hasSubjectAccess)
        {
            context.PlanSubjectAccess.Add(PlanSubjectAccess.Create(plan.Id, subject.Id));
        }

        var studentId = await context.Users
            .Where(u => u.Email == DemoStudentEmail)
            .Select(u => u.Id)
            .SingleAsync(cancellationToken);

        var subscription = await context.Subscriptions.FirstOrDefaultAsync(
            subscription =>
                subscription.SubscriberUserId == studentId
                && subscription.PlanPrice.PlanId == plan.Id
                && subscription.Status == SubscriptionStatus.Active
                && subscription.CurrentPeriodEnd > clock.UtcNow,
            cancellationToken);

        if (subscription is null)
        {
            subscription = Subscription.CreateForUser(
                organization.Id,
                studentId,
                price.Id,
                clock.UtcNow,
                clock.UtcNow.AddYears(10));
            subscription.Activate();
            context.Subscriptions.Add(subscription);
        }

        var hasInitialGrant = await context.CreditLedger.AnyAsync(
            entry => entry.SubscriptionId == subscription.Id
                     && entry.TransactionType == CreditTransactionType.PeriodGrant,
            cancellationToken);

        if (!hasInitialGrant)
        {
            context.CreditLedger.Add(CreditLedgerEntry.Grant(
                subscription.Id,
                studentId,
                Learnier.Domain.Scheduling.SessionType.Private,
                plan.MonthlyLessonCredits!.Value,
                clock.UtcNow,
                clock.UtcNow.AddMonths(1),
                subscription.CurrentPeriodStart));
        }
    }

    private async Task<Organization> EnsureOrganizationAsync(CancellationToken cancellationToken)
    {
        var existing = await context.Organizations
            .FirstOrDefaultAsync(o => o.Slug == OrganizationSlug, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var organization = Organization.Create(
            "Learnier",
            OrganizationSlug,
            OrganizationType.Provider,
            "Europe/Istanbul",
            "TRY");

        context.Organizations.Add(organization);
        LogOrganizationCreated(logger);

        return organization;
    }

    private async Task<Dictionary<string, Role>> LoadSystemRolesAsync(CancellationToken cancellationToken)
        => await context.Roles
            .Where(r => r.OrganizationId == null)
            .ToDictionaryAsync(r => r.Code, StringComparer.Ordinal, cancellationToken);

    private async Task EnsureAccountAsync(
        DevelopmentAccount account,
        Organization organization,
        Dictionary<string, Role> roles,
        CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Email == account.Email, cancellationToken);

        if (user is null)
        {
            user = User.Register(
                account.Email,
                account.FirstName,
                account.LastName,
                passwordHasher.Hash(account.Password));

            // Gelistirme hesaplari dogrulama adimini beklemeden kullanilabilmeli.
            user.ConfirmEmail(clock.UtcNow);

            context.Users.Add(user);
            LogUserCreated(logger, account.Email);
        }

        var membership = await context.Memberships
            // Aktif organizasyon yok; filtre zaten devre disi kalir, yine de
            // niyet acikca belirtiliyor.
            .IgnoreQueryFilters([AppDbContext.TenantFilterName])
            .Include(m => m.Roles)
            .FirstOrDefaultAsync(
                m => m.UserId == user.Id && m.OrganizationId == organization.Id,
                cancellationToken);

        membership ??= organization.AddMember(user.Id, MembershipStatus.Active, clock.UtcNow);

        if (!roles.TryGetValue(account.RoleCode, out var role))
        {
            throw new InvalidOperationException(
                $"Sistem rolu bulunamadi: {account.RoleCode}. Once sistem verisi tohumlanmalidir.");
        }

        AssignRole(membership, role.Id);
    }

    /// <summary>
    /// Uyelige rol atar ve baglanti kaydini acikca ekler.
    /// </summary>
    /// <remarks>
    /// Gerekcesi <see cref="SystemDataSeeder"/> icindeki ayni desenle aynidir:
    /// istemcide uretilen anahtar yuzunden, kaydedilmis bir uyelige sonradan eklenen
    /// baglanti aksi halde INSERT yerine bos bir UPDATE uretir.
    /// </remarks>
    private void AssignRole(OrganizationMembership membership, Guid roleId)
    {
        if (membership.Roles.Any(r => r.RoleId == roleId))
        {
            return;
        }

        membership.AssignRole(roleId);

        var link = membership.Roles.First(r => r.RoleId == roleId);
        context.Add(link);
    }

    private sealed record DevelopmentAccount(
        string Email,
        string Password,
        string FirstName,
        string LastName,
        string RoleCode);
}
