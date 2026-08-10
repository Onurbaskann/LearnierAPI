namespace Learnier.Application.Common.Models;

/// <summary>
/// Sayfali liste yaniti.
/// </summary>
/// <remarks>
/// Toplam sayı da tasinir: istemcinin sayfa sayisini hesaplayabilmesi icin gerekli.
/// Maliyeti ikinci bir COUNT sorgusu, ama alternatifi istemcinin "daha var mi"
/// bilgisini tahmin etmesi olurdu.
/// </remarks>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);

/// <summary>
/// Sayfalama parametreleri.
/// </summary>
/// <remarks>
/// Ust sinir bilerek var: istemci pageSize=100000 gonderip sunucuyu zorlayamasin.
/// Gecersiz degerler reddedilmek yerine sinirlara cekilir - listeleme, bicimsel bir
/// hata yuzunden tamamen basarisiz olmamali.
/// </remarks>
public sealed record PageRequest
{
    public const int MaxPageSize = 100;

    private readonly int _page = 1;
    private readonly int _pageSize = 20;

    public int Page
    {
        get => _page;
        init => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value switch
        {
            < 1 => 20,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }

    public int Skip => (Page - 1) * PageSize;
}
