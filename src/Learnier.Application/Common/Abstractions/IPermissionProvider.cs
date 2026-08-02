namespace Learnier.Application.Common.Abstractions;

/// <summary>
/// Bir uyeligin sahip oldugu izin kodlarini cozer.
/// </summary>
/// <remarks>
/// <para>
/// Izinler kullaniciya degil <b>uyelige</b> baglidir: ayni kisi bir kurumda egitmen,
/// baska bir kurumda ogrenci olabilir ve izinleri her kurumda farklidir. Bu yuzden
/// izinler token'a gomulmez, istek basina aktif organizasyona gore cozulur.
/// </para>
/// <para>
/// Cozumleme sonucu onbelleklenir; rol veya izin degisikliginde ilgili girdi
/// gecersiz kilinmalidir.
/// </para>
/// </remarks>
public interface IPermissionProvider
{
    Task<IReadOnlySet<string>> GetPermissions(Guid membershipId, CancellationToken cancellationToken);
}
