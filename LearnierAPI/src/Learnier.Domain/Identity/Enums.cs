namespace Learnier.Domain.Identity;

/// <remarks>
/// Enum degerleri veritabaninda tam sayi olarak degil metin olarak saklanir.
/// Sebep: tam sayi kullanildiginda enum uyelerinin sirasi degistiginde mevcut
/// satirlarin anlami sessizce kayar. Metin saklamak ayrica veritabanini
/// dogrudan inceleyen kisi icin okunur kilar.
/// </remarks>
public enum UserStatus
{
    /// <summary>Kayit olmus ancak e-postasi henuz dogrulanmamis.</summary>
    Pending,

    Active,

    Suspended
}

public enum OrganizationType
{
    /// <summary>Egitim veren kurum.</summary>
    Provider,

    /// <summary>Calisanlari icin abonelik alan sirket.</summary>
    CorporateCustomer,

    School,

    /// <summary>Baska bir kurumun subesi.</summary>
    Branch
}

public enum OrganizationStatus
{
    Active,

    Suspended
}

public enum MembershipStatus
{
    /// <summary>Davet edilmis, henuz kabul etmemis.</summary>
    Invited,

    Active,

    Suspended
}

public enum GuardianRelationship
{
    Parent,

    LegalGuardian,

    Other
}
