namespace Learnier.Domain.Common;

/// <summary>
/// Tum kalici varliklarin ortak taban sinifi.
/// </summary>
public abstract class Entity
{
    /// <summary>
    /// Birincil anahtar. UUID v7 ile uretilir: zaman siralidir, bu yuzden
    /// PostgreSQL B-tree index'inde v4'un aksine sayfa dagilmasina yol acmaz.
    /// </summary>
    public Guid Id { get; protected set; } = Guid.CreateVersion7();

    /// <summary>
    /// Kimlik karsilastirmasi referansa degil Id'ye dayanir; ayni kaydin
    /// farkli ornekleri (ornegin farkli DbContext'lerden) esit sayilir.
    /// </summary>
    public override bool Equals(object? obj)
        => obj is Entity other
           && GetType() == other.GetType()
           && Id != Guid.Empty
           && Id == other.Id;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
