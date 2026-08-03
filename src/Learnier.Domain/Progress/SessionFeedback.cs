using Learnier.Domain.Common;
using Learnier.Domain.Identity;
using Learnier.Domain.Scheduling;
using Learnier.Domain.Teaching;

namespace Learnier.Domain.Progress;

/// <summary>
/// Oturum sonrasi verilen degerlendirme.
/// </summary>
/// <remarks>
/// <see cref="TargetInstructorProfileId"/> bos birakildiginda degerlendirme oturumun
/// kendisine yapilmis sayilir. Dolu oldugunda belirli bir egitmeni hedefler; ayni
/// oturumda birden fazla egitmen gorev alabildigi icin bu ayrim gerekli.
/// </remarks>
public sealed class SessionFeedback : Entity
{
    /// <summary>Puanlamada kullanilabilecek en dusuk deger.</summary>
    public const int MinRating = 1;

    /// <summary>Puanlamada kullanilabilecek en yuksek deger.</summary>
    public const int MaxRating = 5;

    private SessionFeedback()
    {
    }

    public Guid SessionId { get; private set; }

    /// <summary>Degerlendirmeyi yazan: ogrenci, veli veya egitmen.</summary>
    public Guid AuthorUserId { get; private set; }

    public Guid? TargetInstructorProfileId { get; private set; }

    public int Rating { get; private set; }

    public string? Comment { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public LessonSession Session { get; private set; } = null!;

    public User Author { get; private set; } = null!;

    public InstructorProfile? TargetInstructor { get; private set; }

    public static SessionFeedback Create(
        Guid sessionId,
        Guid authorUserId,
        int rating,
        DateTimeOffset createdAt,
        Guid? targetInstructorProfileId = null,
        string? comment = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rating, MinRating);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rating, MaxRating);

        return new SessionFeedback
        {
            SessionId = sessionId,
            AuthorUserId = authorUserId,
            Rating = rating,
            Comment = comment?.Trim(),
            TargetInstructorProfileId = targetInstructorProfileId,
            CreatedAt = createdAt
        };
    }
}
