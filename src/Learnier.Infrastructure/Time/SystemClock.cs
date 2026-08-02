using Learnier.Application.Common.Abstractions;

namespace Learnier.Infrastructure.Time;

/// <summary>
/// Gercek sistem saati. Testlerde sahte bir <see cref="IClock"/> ile degistirilir.
/// </summary>
internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
