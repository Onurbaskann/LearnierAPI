namespace Learnier.Application.Common.Abstractions;

/// <summary>
/// Istegi yapan kullanici.
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// Kullanici kimligi. Kimlik dogrulanmamis isteklerde <see langword="null"/>.
    /// </summary>
    Guid? UserId { get; }

    bool IsAuthenticated { get; }
}
