using Learnier.Domain.Common;
using Learnier.Domain.Identity;

namespace Learnier.Domain.Billing;

/// <summary>
/// Bir kullanicinin veya kurumun aktif abonelik kaydi.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="OrganizationId"/> egitimi <b>saglayan</b> kurumdur (kiraci).
/// <see cref="SubscriberOrganizationId"/> ise abonelik satin <b>alan</b> kurumdur.
/// Ikisi farkli kavramlar: bireysel abonelikte ilki dolu, ikincisi bostur.
/// </para>
/// <para>
/// Abone ya kullanici ya kurumdur; ikisi birden veya hicbiri olamaz. Bu kural
/// veritabaninda check constraint ile korunur - uygulama hatasi veriyi bozamasin.
/// </para>
/// </remarks>
public sealed class Subscription : AggregateRoot, IAuditableEntity, ITenantScoped
{
    private readonly List<SubscriptionSeat> _seats = [];

    private Subscription()
    {
    }

    public Guid OrganizationId { get; private set; }

    /// <summary>Bireysel abonelikte abone kullanici.</summary>
    public Guid? SubscriberUserId { get; private set; }

    /// <summary>Kurumsal abonelikte abone kurum.</summary>
    public Guid? SubscriberOrganizationId { get; private set; }

    public Guid PlanPriceId { get; private set; }

    public SubscriptionStatus Status { get; private set; }

    public DateTimeOffset StartsAt { get; private set; }

    /// <summary>Icinde bulunulan faturalama doneminin baslangici.</summary>
    public DateTimeOffset CurrentPeriodStart { get; private set; }

    public DateTimeOffset CurrentPeriodEnd { get; private set; }

    /// <summary>Donem sonunda yenilenmeyecek.</summary>
    public bool CancelAtPeriodEnd { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public string? PaymentProvider { get; private set; }

    public string? ProviderSubscriptionId { get; private set; }

    public PlanPrice PlanPrice { get; private set; } = null!;

    public User? SubscriberUser { get; private set; }

    public IReadOnlyCollection<SubscriptionSeat> Seats => _seats.AsReadOnly();

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public static Subscription CreateForUser(
        Guid organizationId,
        Guid subscriberUserId,
        Guid planPriceId,
        DateTimeOffset startsAt,
        DateTimeOffset currentPeriodEnd)
        => Create(organizationId, subscriberUserId, null, planPriceId, startsAt, currentPeriodEnd);

    public static Subscription CreateForOrganization(
        Guid organizationId,
        Guid subscriberOrganizationId,
        Guid planPriceId,
        DateTimeOffset startsAt,
        DateTimeOffset currentPeriodEnd)
        => Create(organizationId, null, subscriberOrganizationId, planPriceId, startsAt, currentPeriodEnd);

    private static Subscription Create(
        Guid organizationId,
        Guid? subscriberUserId,
        Guid? subscriberOrganizationId,
        Guid planPriceId,
        DateTimeOffset startsAt,
        DateTimeOffset currentPeriodEnd)
    {
        if (subscriberUserId is null == subscriberOrganizationId is null)
        {
            throw new ArgumentException(
                "Abonelik ya bir kullaniciya ya bir kuruma ait olmalidir.",
                nameof(subscriberUserId));
        }

        if (currentPeriodEnd <= startsAt)
        {
            throw new ArgumentException(
                "Donem bitisi baslangictan sonra olmalidir.",
                nameof(currentPeriodEnd));
        }

        return new Subscription
        {
            OrganizationId = organizationId,
            SubscriberUserId = subscriberUserId,
            SubscriberOrganizationId = subscriberOrganizationId,
            PlanPriceId = planPriceId,
            StartsAt = startsAt,
            CurrentPeriodStart = startsAt,
            CurrentPeriodEnd = currentPeriodEnd,
            Status = SubscriptionStatus.Pending
        };
    }

    public void Activate() => Status = SubscriptionStatus.Active;

    public void MarkPastDue() => Status = SubscriptionStatus.PastDue;

    /// <summary>
    /// Donemi ileri tasir. Yeni donemin ders haklari ayrica ledger'a yazilmalidir.
    /// </summary>
    public void RenewPeriod(DateTimeOffset periodStart, DateTimeOffset periodEnd)
    {
        if (periodEnd <= periodStart)
        {
            throw new ArgumentException(
                "Donem bitisi baslangictan sonra olmalidir.",
                nameof(periodEnd));
        }

        CurrentPeriodStart = periodStart;
        CurrentPeriodEnd = periodEnd;
        Status = SubscriptionStatus.Active;
    }

    /// <summary>
    /// Aboneligi iptal eder. <paramref name="immediately"/> false ise erisim donem
    /// sonuna kadar surer - odenmis donemin hakki geri alinmaz.
    /// </summary>
    public void Cancel(DateTimeOffset cancelledAt, bool immediately)
    {
        CancelledAt = cancelledAt;

        if (immediately)
        {
            Status = SubscriptionStatus.Cancelled;
            CancelAtPeriodEnd = false;
            return;
        }

        CancelAtPeriodEnd = true;
    }

    public void SetProvider(string provider, string providerSubscriptionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerSubscriptionId);

        PaymentProvider = provider.Trim().ToLowerInvariant();
        ProviderSubscriptionId = providerSubscriptionId.Trim();
    }

    /// <summary>
    /// Kurumsal abonelikte bir uyelige koltuk atar.
    /// </summary>
    public SubscriptionSeat AssignSeat(Guid membershipId, DateTimeOffset assignedAt)
    {
        var existing = _seats.Find(s => s.MembershipId == membershipId && s.RevokedAt is null);
        if (existing is not null)
        {
            return existing;
        }

        var seat = SubscriptionSeat.Create(Id, membershipId, assignedAt);
        _seats.Add(seat);
        return seat;
    }
}
