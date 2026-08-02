using Learnier.Domain.Common;

namespace Learnier.Domain.Identity;

/// <summary>
/// Kimligi dogrulanan gercek kisi.
/// </summary>
/// <remarks>
/// Kullanici ile "ogrenci" veya "egitmen" ayni sey degildir. Bir kisi bir kurumda
/// egitmen, baska bir kurumda ogrenci olabilir; bu yuzden rol burada degil
/// <see cref="OrganizationMembership"/> uzerinde tutulur.
/// </remarks>
public sealed class User : AggregateRoot, IAuditableEntity
{
    private readonly List<OrganizationMembership> _memberships = [];

    private User()
    {
        // EF Core icin.
        Email = string.Empty;
        FirstName = string.Empty;
        LastName = string.Empty;
    }

    /// <summary>
    /// E-posta. Buyuk/kucuk harf duyarsiz benzersizlik veritabaninda
    /// citext sutun tipiyle saglanir.
    /// </summary>
    public string Email { get; private set; }

    public string? Phone { get; private set; }

    /// <summary>
    /// Parola ozeti. Harici bir kimlik saglayicisi ile kayit olan kullanicilarda bos olur.
    /// </summary>
    public string? PasswordHash { get; private set; }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public UserStatus Status { get; private set; }

    public DateTimeOffset? EmailVerifiedAt { get; private set; }

    public IReadOnlyCollection<OrganizationMembership> Memberships => _memberships.AsReadOnly();

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public static User Register(string email, string firstName, string lastName, string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new User
        {
            // Normalizasyon: bosluklar kirpilir. Buyuk/kucuk harf donusumu yapilmaz,
            // cunku citext karsilastirmayi zaten duyarsiz yapar ve kullanicinin
            // yazdigi bicimi korumak gorunumde daha dogrudur.
            Email = email.Trim(),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            PasswordHash = passwordHash,
            Status = UserStatus.Pending
        };
    }

    public void ConfirmEmail(DateTimeOffset confirmedAt)
    {
        if (EmailVerifiedAt is not null)
        {
            return;
        }

        EmailVerifiedAt = confirmedAt;

        // Askiya alinmis bir kullanici e-posta dogrulamasiyla aktiflesmemeli.
        if (Status is UserStatus.Pending)
        {
            Status = UserStatus.Active;
        }
    }

    public void ChangePasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
    }

    public void Suspend() => Status = UserStatus.Suspended;

    public void Reinstate() => Status = EmailVerifiedAt is null ? UserStatus.Pending : UserStatus.Active;
}
