namespace Learnier.Application.Common.Abstractions;

/// <summary>
/// Degisiklikleri kalici hale getirme ve transaction yonetimi.
/// </summary>
/// <remarks>
/// Transaction jenerik bir pipeline'a birakilmaz. Rezervasyon gibi yaris kosulu
/// olan akislarda handler transaction'i acikca acar ve satir kilidi alir; boylece
/// kilidin kapsami ve suresi bilincli olarak kontrol edilir.
/// </remarks>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Yeni bir transaction baslatir. Cagiran taraf <c>await using</c> ile kullanmali
    /// ve basarili yolda <see cref="ITransaction.CommitAsync"/> cagirmalidir.
    /// </summary>
    Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Acik bir veritabani transaction'i. Commit edilmeden dispose edilirse geri alinir.
/// </summary>
public interface ITransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}
