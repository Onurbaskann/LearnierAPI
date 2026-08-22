using System.Reflection;

namespace Learnier.Application.Common.Security;

/// <summary>
/// <see cref="Permissions"/> icindeki tum izin kodlarini toplar.
/// </summary>
/// <remarks>
/// Liste elle yazilmaz, sabitlerden yansima ile turetilir. Sebebi: kodda yeni bir
/// izin tanimlanip seed listesine eklenmesi unutulursa, o izin veritabaninda
/// bulunmaz ve ona bagli her yetkilendirme sessizce basarisiz olurdu. Tek kaynak
/// <see cref="Permissions"/> sinifi.
/// </remarks>
public static class PermissionCatalog
{
    private static readonly Lazy<IReadOnlyList<string>> LazyCodes = new(Collect);

    /// <summary>Sistemde tanimli tum izin kodlari.</summary>
    public static IReadOnlyList<string> Codes => LazyCodes.Value;

    private static string[] Collect()
        => typeof(Permissions)
            .GetNestedTypes(BindingFlags.Public)
            .SelectMany(group => group.GetFields(BindingFlags.Public | BindingFlags.Static))
            // Yalnizca const string alanlar; ileride eklenecek yardimci uyeler karismasin.
            .Where(field => field is { IsLiteral: true, IsInitOnly: false }
                            && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
}
