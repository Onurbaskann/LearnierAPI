namespace Learnier.Application.Common.Abstractions;

/// <summary>
/// Izin onbelleginin gecersiz kilinmasi.
/// </summary>
/// <remarks>
/// <see cref="IPermissionProvider"/>'dan ayri tutuluyor: okuma yolu her yetkilendirme
/// kontrolunde calisir, gecersiz kilma ise yalnizca rol degistiren birkac use-case'te.
/// Ayirmak, izin okuyan siniflarin yanlislikla onbellek dusurmesini de engeller.
/// </remarks>
public interface IPermissionCacheInvalidator
{
    /// <summary>
    /// Bir uyeligin izin onbellegini duser.
    /// </summary>
    /// <remarks>
    /// Rol atandiginda veya kaldirildiginda cagrilmali. Atlanirsa kaldirilan bir
    /// yetki onbellek suresi boyunca kullanilmaya devam ederdi.
    /// </remarks>
    ValueTask InvalidateAsync(Guid membershipId, CancellationToken cancellationToken);
}
