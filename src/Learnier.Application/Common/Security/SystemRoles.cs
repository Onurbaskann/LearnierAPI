namespace Learnier.Application.Common.Security;

/// <summary>
/// Kaynak dokumanin 3. bolumunde sayilan sistem rolleri ve varsayilan izinleri.
/// </summary>
/// <remarks>
/// <para>
/// Roller veritabaninda tutulur ve organizasyon basina ozellestirilebilir; buradaki
/// liste yalnizca <b>baslangic</b> tanimidir. Bir kurum kendi rolunu olusturdugunda
/// bu sinif degismez.
/// </para>
/// <para>
/// Izin dagilimi bilerek dar tutuldu: bir rol, isini yapmak icin gereken en az izne
/// sahip. Eksik izin fark edilir ve eklenir; fazla izin ise sessizce durur.
/// </para>
/// </remarks>
public static class SystemRoles
{
    public const string PlatformAdmin = "platform_admin";
    public const string OrganizationOwner = "organization_owner";
    public const string OrganizationAdmin = "organization_admin";
    public const string EducationManager = "education_manager";
    public const string Instructor = "instructor";
    public const string Student = "student";
    public const string Guardian = "guardian";
    public const string SupportAgent = "support_agent";
    public const string FinanceManager = "finance_manager";

    /// <summary>
    /// Rol kodu → gorunen ad ve varsayilan izinler.
    /// </summary>
    /// <remarks>
    /// Gorunen ad yalnizca yedektir; arayuz rolun cevirisini kaynak dosyasindan
    /// gostermeli (bkz. <c>Role.Name</c> aciklamasi).
    /// </remarks>
    public static IReadOnlyList<SystemRoleDefinition> All { get; } =
    [
        // Platform ve kurum yoneticileri tum izinlere sahip. Ayri ayri saymak yerine
        // katalogun tamami veriliyor ki yeni bir izin eklendiginde de kapsansinlar.
        new(PlatformAdmin, "Platform Yoneticisi", PermissionCatalog.Codes),
        new(OrganizationOwner, "Kurum Sahibi", PermissionCatalog.Codes),
        new(OrganizationAdmin, "Kurum Yoneticisi", PermissionCatalog.Codes),

        new(EducationManager, "Egitim Yoneticisi",
        [
            Permissions.Course.Read,
            Permissions.Course.Manage,
            Permissions.Session.Create,
            Permissions.Session.Cancel,
            Permissions.Booking.ManageAll,
            Permissions.Student.ProgressRead,
            Permissions.Club.Read,
            Permissions.Club.Manage,
            Permissions.Club.MessageSend
        ]),

        new(Instructor, "Egitmen",
        [
            Permissions.Course.Read,
            Permissions.Session.Create,
            Permissions.Session.Cancel,
            Permissions.Student.ProgressRead,
            Permissions.Club.Read,
            Permissions.Club.MessageSend
        ]),

        new(Student, "Ogrenci",
        [
            Permissions.Course.Read,
            Permissions.Booking.Create,
            Permissions.Club.Read,
            Permissions.Club.MessageSend
        ]),

        // Veli, sorumlu oldugu ogrenci adina rezervasyon yapabilir ve ilerlemesini
        // gorebilir. Hangi ogrenci oldugu RBAC ile degil learner_guardians ile belirlenir.
        new(Guardian, "Veli",
        [
            Permissions.Course.Read,
            Permissions.Booking.Create,
            Permissions.Student.ProgressRead
        ]),

        new(SupportAgent, "Destek Temsilcisi",
        [
            Permissions.Course.Read,
            Permissions.Booking.ManageAll,
            Permissions.Student.ProgressRead
        ]),

        new(FinanceManager, "Finans Yoneticisi",
        [
            Permissions.Subscription.Manage
        ])
    ];
}

/// <param name="Code">Makine tarafindan kullanilan sabit kod.</param>
/// <param name="Name">Gorunen ad - yedek deger.</param>
/// <param name="Permissions">Rolun varsayilan izin kodlari.</param>
public sealed record SystemRoleDefinition(
    string Code,
    string Name,
    IReadOnlyList<string> Permissions);
