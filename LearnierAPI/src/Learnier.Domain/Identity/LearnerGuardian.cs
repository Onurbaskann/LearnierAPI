using Learnier.Domain.Common;

namespace Learnier.Domain.Identity;

/// <summary>
/// Ogrenci ile velisi arasindaki iliski.
/// </summary>
/// <remarks>
/// Bu iliski RBAC'den ayridir ve onu tamamlar: "veli" rolu <i>ne yapabilecegini</i>,
/// bu tablo ise <i>hangi ogrenci icin</i> yapabilecegini belirler. Ikisi ayrilmazsa
/// bir veli tum ogrencilere erisebilirdi.
/// </remarks>
public sealed class LearnerGuardian : Entity, IAuditableEntity
{
    private LearnerGuardian()
    {
    }

    public Guid LearnerUserId { get; private set; }

    public Guid GuardianUserId { get; private set; }

    public GuardianRelationship RelationshipType { get; private set; }

    /// <summary>Birincil iletisim kurulacak veli.</summary>
    public bool IsPrimary { get; private set; }

    /// <summary>Ogrenci adina rezervasyon yapabilir.</summary>
    public bool CanBook { get; private set; }

    /// <summary>Ogrencinin ilerleme kayitlarini gorebilir.</summary>
    public bool CanViewProgress { get; private set; }

    public User Learner { get; private set; } = null!;

    public User Guardian { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public static LearnerGuardian Create(
        Guid learnerUserId,
        Guid guardianUserId,
        GuardianRelationship relationshipType,
        bool isPrimary = false,
        bool canBook = true,
        bool canViewProgress = true)
    {
        if (learnerUserId == guardianUserId)
        {
            throw new ArgumentException(
                "Bir kullanici kendi velisi olamaz.",
                nameof(guardianUserId));
        }

        return new LearnerGuardian
        {
            LearnerUserId = learnerUserId,
            GuardianUserId = guardianUserId,
            RelationshipType = relationshipType,
            IsPrimary = isPrimary,
            CanBook = canBook,
            CanViewProgress = canViewProgress
        };
    }

    public void UpdatePermissions(bool canBook, bool canViewProgress)
    {
        CanBook = canBook;
        CanViewProgress = canViewProgress;
    }
}
