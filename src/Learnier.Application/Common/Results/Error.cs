namespace Learnier.Application.Common.Results;

/// <summary>
/// Bir is kurali ihlalini temsil eder.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bu tip bilerek kullaniciya gosterilecek metin tasimaz.</b> Yalnizca bir
/// <see cref="Code"/> ve bicimlendirmede kullanilacak <see cref="Parameters"/> tutar.
/// Metne cevrim WebApi katmaninda, istegin diline gore yapilir.
/// </para>
/// <para>
/// Neden boyle: ikinci dil eklendiginde Application ve Domain katmanlarina hic
/// dokunmak gerekmez. Mesaj burada tutulsaydi her handler'i yeniden yazmak gerekirdi.
/// </para>
/// <example>
/// <code>
/// Error.Conflict("booking.session_full", ("capacity", session.Capacity))
/// </code>
/// karsiligi <c>Errors.tr.resx</c> icinde:
/// <c>booking.session_full = "Bu oturumun kontenjani ({capacity} kisi) dolmustur."</c>
/// </example>
/// </remarks>
public sealed record Error
{
    private static readonly IReadOnlyDictionary<string, object?> NoParameters =
        new Dictionary<string, object?>();

    private Error(string code, ErrorType type, IReadOnlyDictionary<string, object?> parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        Code = code;
        Type = type;
        Parameters = parameters;
    }

    /// <summary>
    /// Resource dosyasindaki anahtar. Ornek: <c>booking.session_full</c>.
    /// Nokta ile ayrilmis, kucuk harfli, snake_case parcalardan olusur.
    /// </summary>
    public string Code { get; }

    public ErrorType Type { get; }

    /// <summary>
    /// Cevrilmis metne yerlestirilecek degerler. Anahtarlar resource metnindeki
    /// yer tutucu adlariyla eslesir.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; }

    public static Error Validation(string code, params ReadOnlySpan<(string Name, object? Value)> parameters)
        => new(code, ErrorType.Validation, ToDictionary(parameters));

    public static Error NotFound(string code, params ReadOnlySpan<(string Name, object? Value)> parameters)
        => new(code, ErrorType.NotFound, ToDictionary(parameters));

    public static Error Conflict(string code, params ReadOnlySpan<(string Name, object? Value)> parameters)
        => new(code, ErrorType.Conflict, ToDictionary(parameters));

    public static Error Forbidden(string code, params ReadOnlySpan<(string Name, object? Value)> parameters)
        => new(code, ErrorType.Forbidden, ToDictionary(parameters));

    public static Error Unauthorized(string code, params ReadOnlySpan<(string Name, object? Value)> parameters)
        => new(code, ErrorType.Unauthorized, ToDictionary(parameters));

    private static IReadOnlyDictionary<string, object?> ToDictionary(
        ReadOnlySpan<(string Name, object? Value)> parameters)
    {
        if (parameters.IsEmpty)
        {
            return NoParameters;
        }

        var result = new Dictionary<string, object?>(parameters.Length, StringComparer.Ordinal);
        foreach (var (name, value) in parameters)
        {
            result[name] = value;
        }

        return result;
    }
}
