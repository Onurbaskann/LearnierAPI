using System.Text.Json;
using System.Text.Json.Serialization;

namespace Learnier.IntegrationTests;

/// <summary>
/// Testlerin API ile ayni JSON sozlesmesini kullanmasini saglar.
/// </summary>
/// <remarks>
/// API enum'lari metin olarak tasiyor (bkz. Program.cs). <c>ReadFromJsonAsync</c>
/// varsayilan ayarlarla sayi bekledigi icin, bu ayar olmadan enum iceren her yanit
/// cozulemez ve test gercek bir hata varmis gibi kirilir.
/// </remarks>
internal static class TestJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
