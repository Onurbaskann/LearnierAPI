namespace Learnier.Application.Common.Abstractions;

/// <summary>
/// Parola ozetleme ve dogrulama.
/// </summary>
/// <remarks>
/// Somut algoritma Infrastructure'da secilir; Application yalnizca bu sozlesmeye bakar.
/// Boylece algoritma degistiginde (ornegin PBKDF2'den Argon2'ye) is mantigi etkilenmez.
/// </remarks>
public interface IPasswordHasher
{
    string Hash(string password);

    PasswordVerificationOutcome Verify(string hash, string password);
}

public enum PasswordVerificationOutcome
{
    Failed,

    Success,

    /// <summary>
    /// Parola dogru, ancak ozet eski bir algoritma veya parametre ile uretilmis.
    /// Cagiran taraf yeni ozeti kaydetmeli.
    /// </summary>
    SuccessRehashNeeded
}
