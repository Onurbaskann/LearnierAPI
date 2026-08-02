namespace Learnier.Application.Common.Results;

/// <summary>
/// Hatanin turu. HTTP durum koduna cevrim WebApi katmaninda yapilir;
/// Application katmani HTTP kavramlarini bilmez.
/// </summary>
public enum ErrorType
{
    /// <summary>Girdi bicimsel olarak gecersiz.</summary>
    Validation,

    /// <summary>Kaynak bulunamadi.</summary>
    NotFound,

    /// <summary>Mevcut durum bu islemi imkansiz kiliyor (ornegin kontenjan dolu).</summary>
    Conflict,

    /// <summary>Kimlik dogrulanmis ama bu islem icin yetki yok.</summary>
    Forbidden,

    /// <summary>Kimlik dogrulanmamis.</summary>
    Unauthorized
}
