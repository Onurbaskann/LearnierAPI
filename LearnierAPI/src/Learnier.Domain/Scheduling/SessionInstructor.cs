using Learnier.Domain.Common;
using Learnier.Domain.Teaching;

namespace Learnier.Domain.Scheduling;

/// <summary>
/// Oturuma atanmis egitmen.
/// </summary>
/// <remarks>
/// Oturum-egitmen iliskisi cogul tutulur: webinar veya buyuk grup derslerinde
/// asistan ve moderator de gorev alabilir. Bu yuzden oturuma tek bir
/// <c>instructor_id</c> kolonu konmamistir.
/// </remarks>
public sealed class SessionInstructor : Entity, IAuditableEntity
{
    private SessionInstructor()
    {
    }

    public Guid SessionId { get; private set; }

    public Guid InstructorProfileId { get; private set; }

    public SessionInstructorRole Role { get; private set; }

    public LessonSession Session { get; private set; } = null!;

    public InstructorProfile InstructorProfile { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    internal static SessionInstructor Create(Guid sessionId, Guid instructorProfileId, SessionInstructorRole role)
        => new()
        {
            SessionId = sessionId,
            InstructorProfileId = instructorProfileId,
            Role = role
        };
}
