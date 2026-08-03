using Learnier.Domain.Catalog;

namespace Learnier.Domain.Billing;

/// <summary>
/// Planin kapsadigi tekil egitim.
/// </summary>
/// <remarks>
/// Alan bazli erisim icin <see cref="PlanSubjectAccess"/> kullanilir; bu tablo
/// "yalnizca su egitim" seklindeki dar kapsamli planlar icindir.
/// </remarks>
public sealed class PlanCourseAccess
{
    private PlanCourseAccess()
    {
    }

    public Guid PlanId { get; private set; }

    public Guid CourseId { get; private set; }

    public SubscriptionPlan Plan { get; private set; } = null!;

    public Course Course { get; private set; } = null!;

    public static PlanCourseAccess Create(Guid planId, Guid courseId)
        => new() { PlanId = planId, CourseId = courseId };
}
