namespace Learnier.Domain.Common;

/// <summary>
/// Olusturulma ve guncellenme bilgisi tutulan varliklar.
/// Alanlari <c>AuditableEntityInterceptor</c> otomatik doldurur; elle set edilmemeli.
/// </summary>
/// <remarks>
/// Kaynak dokumanin 14. bolumu tablo basina ayri history tablosu onermiyor.
/// Bu alanlar cogu senaryo icin yeterli denetim izini saglar.
/// </remarks>
public interface IAuditableEntity
{
    DateTimeOffset CreatedAt { get; set; }

    Guid? CreatedBy { get; set; }

    DateTimeOffset? UpdatedAt { get; set; }

    Guid? UpdatedBy { get; set; }
}
