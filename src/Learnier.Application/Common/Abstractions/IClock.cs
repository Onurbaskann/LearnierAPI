namespace Learnier.Application.Common.Abstractions;

/// <summary>
/// Sistem saatine erisim.
/// </summary>
/// <remarks>
/// <c>DateTimeOffset.UtcNow</c> dogrudan cagrilmaz: rezervasyon penceresi ve iptal
/// son tarihi gibi zamana bagli kurallarin test edilebilmesi icin saat enjekte edilir.
/// </remarks>
public interface IClock
{
    /// <summary>Su anki zaman, her zaman UTC.</summary>
    DateTimeOffset UtcNow { get; }
}
