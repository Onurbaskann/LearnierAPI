using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Scheduling;

/// <summary>
/// Planlama ve rezervasyon islemlerinin hata kodlari.
/// </summary>
internal static class SchedulingErrors
{
    public static Error OrganizationContextRequired => Error.Forbidden("tenant.organization_required");

    public static Error CourseNotFound => Error.NotFound("scheduling.course_not_found");

    public static Error ClassGroupNotFound => Error.NotFound("scheduling.class_group_not_found");

    public static Error ClassGroupFull(int capacity)
        => Error.Conflict("scheduling.class_group_full", ("capacity", capacity));

    public static Error LearnerNotFound => Error.Validation("scheduling.learner_not_found");

    public static Error SessionNotFound => Error.NotFound("scheduling.session_not_found");

    public static Error SessionNotCancellable
        => Error.Conflict("scheduling.session_not_cancellable");

    public static Error InstructorNotFound => Error.Validation("scheduling.instructor_not_found");

    /// <summary>
    /// Egitmen ayni saat araliginda baska bir oturuma atanmis.
    /// </summary>
    /// <remarks>
    /// Kaynak dokumanin 13. bolumu bunu normal unique index'in cozemedigi bir
    /// durum olarak isaret ediyor; kontrol islem icinde yapiliyor.
    /// </remarks>
    public static Error InstructorBusy => Error.Conflict("scheduling.instructor_busy");

    /// <summary>Oturum su anda rezervasyona kapali: pencere disinda veya iptal edilmis.</summary>
    public static Error SessionNotBookable => Error.Conflict("scheduling.session_not_bookable");

    public static Error AlreadyBooked => Error.Conflict("scheduling.already_booked");

    public static Error BookingNotFound => Error.NotFound("scheduling.booking_not_found");

    /// <summary>Baskasi adina rezervasyon yapma veya iptal etme yetkisi yok.</summary>
    public static Error BookingNotOwned => Error.Forbidden("scheduling.booking_not_owned");

    public static Error SessionAlreadyStarted => Error.Conflict("scheduling.session_already_started");
}
