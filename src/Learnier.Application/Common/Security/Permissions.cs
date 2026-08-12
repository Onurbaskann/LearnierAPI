namespace Learnier.Application.Common.Security;

/// <summary>
/// Sistemdeki izin kodlari.
/// </summary>
/// <remarks>
/// <para>
/// Izinler koda gomulu sabitlerdir; roller ise veritabaninda tutulur ve organizasyon
/// basina ozellestirilebilir. Bu ayrim onemli: yeni bir izin yeni kod gerektirir,
/// yeni bir rol gerektirmez.
/// </para>
/// <para>
/// Kodlar <c>[Authorize(Policy = Permissions.Booking.Create)]</c> seklinde kullanilir.
/// Sabit kullanmak, elle yazilan dizgilerdeki yazim hatalarini derleme zamaninda yakalar.
/// </para>
/// </remarks>
public static class Permissions
{
    public static class Course
    {
        public const string Read = "course.read";
        public const string Manage = "course.manage";
    }

    public static class Session
    {
        public const string Create = "session.create";
        public const string Cancel = "session.cancel";
    }

    public static class Booking
    {
        public const string Create = "booking.create";

        /// <summary>Baskasi adina rezervasyon gorme ve yonetme.</summary>
        public const string ManageAll = "booking.manage_all";
    }

    public static class Student
    {
        public const string ProgressRead = "student.progress.read";
    }

    public static class Subscription
    {
        public const string Manage = "subscription.manage";
    }

    public static class Organization
    {
        public const string MemberManage = "organization.member.manage";
    }

    public static class Club
    {
        public const string Read = "club.read";
        public const string Manage = "club.manage";
        public const string MessageSend = "club.message.send";
    }
}
