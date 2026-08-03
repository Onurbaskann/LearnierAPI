namespace Learnier.Domain.Progress;

/// <summary>
/// Mufredat konusunun neye dayanarak tamamlanmis sayildigi.
/// </summary>
public enum LessonCompletionSource
{
    /// <summary>Konunun islendigi oturuma katilim uzerinden otomatik.</summary>
    SessionAttendance,

    /// <summary>Egitmen isaretledi.</summary>
    Instructor,

    /// <summary>Ogrenci kendi isaretledi.</summary>
    SelfReported
}
