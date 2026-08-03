using Learnier.Domain.Common;

namespace Learnier.Domain.Catalog;

/// <summary>
/// Bir egitim alanindaki seviye tanimi.
/// </summary>
/// <remarks>
/// Seviyeler alana bagli tanimlanir; boylece Ingilizcede A1-C2, matematikte
/// "5. sinif", yazilimda "beginner-advanced" ayni tabloda tutulabilir.
/// Organizasyona <see cref="Subject"/> uzerinden ulasildigi icin ayrica
/// <c>OrganizationId</c> tasimaz.
/// </remarks>
public sealed class Level : Entity, IAuditableEntity
{
    private Level()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    public Guid SubjectId { get; private set; }

    /// <summary>Alan icinde benzersiz kisa kod, ornegin <c>A1</c>.</summary>
    public string Code { get; private set; }

    public string Name { get; private set; }

    /// <summary>Seviyelerin dogal sirasi. Karsilastirma bu alan uzerinden yapilir.</summary>
    public int SortOrder { get; private set; }

    public Subject Subject { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public static Level Create(Guid subjectId, string code, string name, int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Level
        {
            SubjectId = subjectId,
            Code = code.Trim(),
            Name = name.Trim(),
            SortOrder = sortOrder
        };
    }
}
