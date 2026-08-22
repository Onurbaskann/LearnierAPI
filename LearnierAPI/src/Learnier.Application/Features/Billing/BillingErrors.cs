using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Billing;

/// <summary>
/// Plan yonetiminin hata kodlari.
/// </summary>
internal static class BillingErrors
{
    public static Error OrganizationContextRequired => Error.Forbidden("tenant.organization_required");

    public static Error PlanNotFound => Error.NotFound("billing.plan_not_found");

    /// <summary>Fiyatsiz plan satisa acilamaz.</summary>
    public static Error PlanHasNoActivePrice => Error.Conflict("billing.plan_has_no_active_price");

    /// <summary>Hak tanimi olmayan plan aboneye hicbir sey vermez.</summary>
    public static Error PlanHasNoEntitlement => Error.Conflict("billing.plan_has_no_entitlement");

    public static Error SubjectNotFound => Error.Validation("billing.subject_not_found");

    public static Error CourseNotFound => Error.Validation("billing.course_not_found");

    /// <summary>Alan ve egitimden tam olarak biri verilmeli.</summary>
    public static Error AccessTargetInvalid => Error.Validation("billing.access_target_invalid");

    public static Error CurrencyInvalid => Error.Validation("billing.currency_invalid");

    public static Error AmountInvalid => Error.Validation("billing.amount_invalid");

    public static Error BillingIntervalCountInvalid =>
        Error.Validation("billing.billing_interval_count_invalid");

    /// <summary>Sayili hakta adet zorunlu; aksi halde "kac ders" sorusu yanitsiz kalir.</summary>
    public static Error QuantityRequired => Error.Validation("billing.quantity_required");

    public static Error QuantityInvalid => Error.Validation("billing.quantity_invalid");

    /// <summary>Sinirsiz erisimde adet anlamsiz; verilmesi karisikliga yol acar.</summary>
    public static Error QuantityNotAllowed => Error.Validation("billing.quantity_not_allowed");
}
