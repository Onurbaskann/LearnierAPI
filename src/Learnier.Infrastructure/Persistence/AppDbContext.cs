using System.Linq.Expressions;
using System.Reflection;
using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Common;
using Learnier.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Persistence;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentTenant currentTenant)
    : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<OrganizationMembership> Memberships => Set<OrganizationMembership>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<MembershipRole> MembershipRoles => Set<MembershipRole>();

    public DbSet<LearnerGuardian> LearnerGuardians => Set<LearnerGuardian>();

    public async Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await Database.BeginTransactionAsync(cancellationToken);
        return new EfTransaction(transaction);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Kaynak dokuman e-posta icin buyuk/kucuk harf duyarsiz benzersizlik istiyor.
        modelBuilder.HasPostgresExtension("citext");

        // Entity konfigurasyonlari Configurations klasorunde, entity basina bir dosya.
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        ApplyTenantQueryFilters(modelBuilder);
        ApplyDerivedTenantQueryFilters(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Tum zaman degerleri timestamptz olarak saklanir ve UTC tasinir.
        // Gosterim katmani kullanicinin saat dilimine cevirir.
        configurationBuilder.Properties<DateTimeOffset>().HaveColumnType("timestamptz");
        configurationBuilder.Properties<DateTimeOffset?>().HaveColumnType("timestamptz");

        base.ConfigureConventions(configurationBuilder);
    }

    /// <summary>
    /// <see cref="ITenantScoped"/> implement eden her varliga aktif organizasyon filtresi ekler.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bunun amaci organizasyon filtresini unutmayi imkansiz kilmak: filtre modelde
    /// tanimli oldugu icin her sorguya otomatik uygulanir, gelistiricinin hatirlamasi gerekmez.
    /// Aktif organizasyon yoksa (giris, kayit gibi kapsam disi istekler) filtre devre disi kalir.
    /// </para>
    /// <para>
    /// Filtre EF 10'un <i>isimli query filter</i> ozelligiyle tanimlanir. Boylece ileride
    /// baska bir filtre (ornegin soft-delete) eklendiginde bu filtre sessizce ezilmez ve
    /// gerektiginde yalnizca bu filtre <c>IgnoreQueryFilters([TenantFilterName])</c> ile
    /// devre disi birakilabilir.
    /// </para>
    /// </remarks>
    private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "e");

            // e => currentTenant.OrganizationId == null || e.OrganizationId == currentTenant.OrganizationId
            var organizationIdProperty = Expression.Property(
                parameter,
                nameof(ITenantScoped.OrganizationId));

            // currentTenant alani closure uzerinden okunur; boylece filtre her sorguda
            // guncel deger ile degerlendirilir, model derlendigi andaki deger ile degil.
            var currentOrganizationId = Expression.Property(
                Expression.Constant(this),
                nameof(CurrentOrganizationId));

            var noTenant = Expression.Not(
                Expression.Property(currentOrganizationId, nameof(Nullable<Guid>.HasValue)));

            var matchesTenant = Expression.Equal(
                organizationIdProperty,
                Expression.Property(currentOrganizationId, nameof(Nullable<Guid>.Value)));

            var filter = Expression.Lambda(
                Expression.OrElse(noTenant, matchesTenant),
                parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(TenantFilterName, filter);
        }
    }

    /// <summary>
    /// Kendi <c>OrganizationId</c> sutunu olmayan, ancak bir iliski uzerinden
    /// organizasyona bagli olan varliklara filtre uygular.
    /// </summary>
    /// <remarks>
    /// Kaynak dokumanin 12. bolumu, organizasyona baska bir iliski uzerinden ulasilabilen
    /// tablolara <c>organization_id</c> eklenmemesini soyluyor. Ancak filtre uygulanmazsa
    /// bu tablolar dogrudan sorgulandiginda organizasyon siniri asilabilir: ornegin
    /// <c>MembershipRole</c> uzerinden baska bir kurumun rol atamalari gorulebilirdi.
    /// Bu yuzden sutun eklemek yerine filtre iliski uzerinden tanimlaniyor.
    /// </remarks>
    private void ApplyDerivedTenantQueryFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MembershipRole>().HasQueryFilter(
            TenantFilterName,
            mr => CurrentOrganizationId == null
                  || mr.Membership.OrganizationId == CurrentOrganizationId);
    }

    /// <summary>
    /// Organizasyon filtresinin adi. <c>IgnoreQueryFilters([AppDbContext.TenantFilterName])</c>
    /// ile yalnizca bu filtre devre disi birakilabilir - platform yoneticisi sorgulari gibi
    /// bilincli olarak organizasyon sinirini asan durumlar icin.
    /// </summary>
    public const string TenantFilterName = "TenantFilter";

    /// <summary>
    /// Query filter ifadesinin okudugu aktif organizasyon.
    /// </summary>
    /// <remarks>
    /// Ifade agacinda deger degil bu uyeye erisim gomulur. EF, DbContext ornegi uzerindeki
    /// uye erisimlerini her sorguda yeniden degerlendirir; yani model onbelleklense de
    /// filtre sorguyu calistiran context'in guncel organizasyonunu kullanir.
    /// </remarks>
    private Guid? CurrentOrganizationId => currentTenant.OrganizationId;
}
