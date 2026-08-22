namespace Learnier.Domain.Catalog;

/// <remarks>
/// Identity katmanindaki gerekce burada da gecerli: enum degerleri veritabaninda
/// tam sayi olarak degil metin olarak saklanir.
/// </remarks>
public enum SubjectStatus
{
    Active,

    Archived
}

/// <summary>
/// Egitimin sunulus bicimi. Rezervasyon ve kapasite kurallari buna gore degisir.
/// </summary>
public enum CourseType
{
    /// <summary>Donemsel, sabit sinifli ve mufredat sirasi takip edilen egitim.</summary>
    Structured,

    /// <summary>Sabit sinif olmadan tek tek katilinan oturumlar.</summary>
    DropIn,

    /// <summary>Birebir ders.</summary>
    Private
}

public enum CourseStatus
{
    Draft,

    Published,

    Archived
}
