namespace CslaStarter.Domain.Platform;

/// <summary>
/// One save's changes. DAL write methods only stage changes; nothing reaches the database until the root
/// business object calls <see cref="CommitAsync"/>, which writes them all in one transaction.
/// </summary>
public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
