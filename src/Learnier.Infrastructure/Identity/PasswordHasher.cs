using Learnier.Application.Common.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace Learnier.Infrastructure.Identity;

/// <summary>
/// ASP.NET Core Identity'nin parola ozetleyicisini kullanir.
/// </summary>
/// <remarks>
/// <para>
/// Identity yiginindan yalnizca bu parca aliniyor: <c>PasswordHasher</c> iyi test edilmis
/// bir PBKDF2 uygulamasidir ve kendi elimizle kripto yazmaktan cok daha guvenlidir.
/// Identity'nin tablo ve rol modeli ise kullanilmiyor, cunku organizasyon kapsamli
/// RBAC ihtiyacini karsilamiyor.
/// </para>
/// <para>
/// Tip parametresi <see cref="object"/>: varsayilan uygulama ozet hesaplarken kullanici
/// nesnesini kullanmaz, bu sayede Domain'deki User tipine bagimlilik olusmaz.
/// </para>
/// </remarks>
internal sealed class PasswordHasher : IPasswordHasher
{
    private static readonly object HashingSubject = new();

    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(HashingSubject, password);

    public PasswordVerificationOutcome Verify(string hash, string password)
        => _hasher.VerifyHashedPassword(HashingSubject, hash, password) switch
        {
            PasswordVerificationResult.Success => PasswordVerificationOutcome.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordVerificationOutcome.SuccessRehashNeeded,
            _ => PasswordVerificationOutcome.Failed
        };
}
