using Learnier.Domain.Catalog;

namespace Learnier.Domain.Billing;

/// <summary>
/// Planin kapsadigi egitim alani.
/// </summary>
/// <remarks>
/// Kaynak dokumanin 8. bolumu soyut bir <c>target_type</c>/<c>target_id</c> tablosunu
/// acikca onermiyor: yabanci anahtar butunlugu kaybolur ve sorgular zorlasir.
/// Bu yuzden alan ve egitim erisimleri ayri, gercek FK'li tablolarda tutulur.
/// </remarks>
public sealed class PlanSubjectAccess
{
    private PlanSubjectAccess()
    {
    }

    public Guid PlanId { get; private set; }

    public Guid SubjectId { get; private set; }

    public SubscriptionPlan Plan { get; private set; } = null!;

    public Subject Subject { get; private set; } = null!;

    public static PlanSubjectAccess Create(Guid planId, Guid subjectId)
        => new() { PlanId = planId, SubjectId = subjectId };
}
