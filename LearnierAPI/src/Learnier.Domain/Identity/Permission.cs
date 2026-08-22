using Learnier.Domain.Common;

namespace Learnier.Domain.Identity;

/// <summary>
/// Sistemde tanimli tekil yetki.
/// </summary>
/// <remarks>
/// Izinler koda gomulu sabitlerden turer ve seed ile veritabanina yazilir.
/// Calisma zamaninda yeni izin olusturulmaz: bir iznin karsiligi olan kontrol
/// zaten kodda yazili olmali, aksi halde tanimli ama hicbir seyi korumayan
/// izinler birikirdi.
/// </remarks>
public sealed class Permission : Entity
{
    private Permission()
    {
        Code = string.Empty;
    }

    /// <summary>Ornegin <c>booking.create</c>.</summary>
    public string Code { get; private set; }

    public static Permission Create(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return new Permission { Code = code };
    }
}
