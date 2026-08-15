using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Billing;

/// <summary>
/// Abonelik ve kredi islemlerinin hata kodlari.
/// </summary>
internal static class BillingErrors
{
    public static Error OrganizationContextRequired => Error.Forbidden("tenant.organization_required");

    public static Error PlanNotFound => Error.NotFound("billing.plan_not_found");

    public static Error PlanPriceNotFound => Error.Validation("billing.plan_price_not_found");

    /// <summary>Arsivlenmis fiyat yeni abonelikte kullanilamaz.</summary>
    public static Error PlanPriceNotActive => Error.Validation("billing.plan_price_not_active");

    public static Error PlanNotActive => Error.Conflict("billing.plan_not_active");

    /// <summary>Fiyatsiz plan satisa acilamaz.</summary>
    public static Error PlanHasNoActivePrice => Error.Conflict("billing.plan_has_no_active_price");

    /// <summary>Hak tanimi olmayan plan aboneye hicbir sey vermez.</summary>
    public static Error PlanHasNoEntitlement => Error.Conflict("billing.plan_has_no_entitlement");

    public static Error SubjectNotFound => Error.Validation("billing.subject_not_found");

    public static Error CourseNotFound => Error.Validation("billing.course_not_found");

    public static Error SubscriptionNotFound => Error.NotFound("billing.subscription_not_found");

    public static Error LearnerNotFound => Error.Validation("billing.learner_not_found");

    public static Error MembershipNotFound => Error.Validation("billing.membership_not_found");

    /// <summary>
    /// Ogrencinin bu oturuma erisim saglayan aktif aboneligi yok.
    /// </summary>
    public static Error NoActiveSubscription => Error.Forbidden("billing.no_active_subscription");

    /// <summary>
    /// Abonelik var ama plani bu egitimi kapsamiyor.
    /// </summary>
    public static Error CourseNotCovered => Error.Forbidden("billing.course_not_covered");

    /// <summary>
    /// Plan bu ders turu icin hak tanimlamiyor - ornegin yalnizca grup dersi
    /// kapsayan bir planla birebir ders rezervasyonu.
    /// </summary>
    public static Error SessionTypeNotCovered => Error.Forbidden("billing.session_type_not_covered");

    /// <summary>Ders hakki bitmis.</summary>
    public static Error InsufficientCredit => Error.Conflict("billing.insufficient_credit");
}
