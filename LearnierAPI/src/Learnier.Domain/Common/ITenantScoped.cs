namespace Learnier.Domain.Common;

/// <summary>
/// Bir organizasyona (kiraciya) ait olan varliklar.
/// Bu arayuzu implement eden her varlik EF global query filter ile otomatik olarak
/// aktif organizasyona gore filtrelenir; boylece organizasyon filtresini unutmak imkansiz hale gelir.
/// </summary>
/// <remarks>
/// Bu arayuz her tabloya eklenmez. Kaynak dokumanin 12. bolumu geregi yalnizca
/// organizasyona baska bir iliski uzerinden ulasilamayan tablolar isaretlenir.
/// Ornegin <c>CourseModule</c>, <c>CourseId</c> uzerinden organizasyona ulastigi icin
/// ayrica <c>OrganizationId</c> tasimaz - bu normalizasyon acisindan gereksiz tekrar olurdu.
/// </remarks>
public interface ITenantScoped
{
    Guid OrganizationId { get; }
}
