using Learnier.Domain.Catalog;
using Learnier.Domain.Common;

namespace Learnier.Domain.Teaching;

/// <summary>
/// Egitmenin hangi alanda, hangi seviyede ders verebildigi.
/// </summary>
/// <remarks>
/// <see cref="LevelId"/> bos birakildiginda egitmen o alanin tum seviyelerinde
/// yetkin sayilir. Bu sayede "Ingilizce A1-B2", "Python baslangic" ve
/// "Matematik tum seviyeler" ayni tabloda ifade edilebilir.
/// </remarks>
public sealed class InstructorSubject : Entity, IAuditableEntity
{
    private InstructorSubject()
    {
    }

    public Guid InstructorProfileId { get; private set; }

    public Guid SubjectId { get; private set; }

    public Guid? LevelId { get; private set; }

    public InstructorSubjectStatus Status { get; private set; }

    public InstructorProfile InstructorProfile { get; private set; } = null!;

    public Subject Subject { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    internal static InstructorSubject Create(Guid instructorProfileId, Guid subjectId, Guid? levelId)
        => new()
        {
            InstructorProfileId = instructorProfileId,
            SubjectId = subjectId,
            LevelId = levelId,
            Status = InstructorSubjectStatus.Active
        };

    public void Deactivate() => Status = InstructorSubjectStatus.Inactive;

    public void Activate() => Status = InstructorSubjectStatus.Active;
}
