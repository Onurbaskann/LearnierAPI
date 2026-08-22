using System.Globalization;
using System.Text.RegularExpressions;
using Learnier.Application.Common.Results;
using Microsoft.Extensions.Localization;

namespace Learnier.WebApi.Localization;

/// <summary>
/// Bir <see cref="Error"/> kodunu istegin diline gore metne cevirir.
/// </summary>
/// <remarks>
/// Ceviri bilerek yalnizca bu katmanda yapilir. Application ve Domain katmanlari
/// hata kodu ve parametre uretir, metin uretmez; boylece yeni bir dil eklemek
/// is mantigina hic dokunmadan kaynak dosyasi eklemekten ibaret olur.
/// </remarks>
internal sealed partial class ErrorMessageResolver(IStringLocalizer<ErrorMessages> localizer)
{
    /// <summary>
    /// Kaynak metnindeki <c>{parametreAdi}</c> bicimindeki yer tutucular.
    /// </summary>
    [GeneratedRegex(@"\{(?<name>\w+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderPattern { get; }

    public string Resolve(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var localized = localizer[error.Code];

        // Kaynak bulunamazsa IStringLocalizer anahtarin kendisini dondurur.
        // Kodu oldugu gibi gostermek, bos veya yaniltici bir metin gostermekten iyidir;
        // ayrica eksik cevirinin testte fark edilmesini saglar.
        if (localized.ResourceNotFound)
        {
            return error.Code;
        }

        return error.Parameters.Count == 0
            ? localized.Value
            : FillPlaceholders(localized.Value, error.Parameters);
    }

    private static string FillPlaceholders(string template, IReadOnlyDictionary<string, object?> parameters)
        => PlaceholderPattern.Replace(
            template,
            match => parameters.TryGetValue(match.Groups["name"].Value, out var value)
                ? Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty
                // Karsiligi olmayan yer tutucu oldugu gibi birakilir; bu, kaynak metni ile
                // hata parametreleri arasindaki uyumsuzlugu gorunur kilar.
                : match.Value);
}
