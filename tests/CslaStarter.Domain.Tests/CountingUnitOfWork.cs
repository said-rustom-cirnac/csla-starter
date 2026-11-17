using CslaStarter.Domain.Platform;

namespace CslaStarter.Domain.Tests;

/// <summary>A unit of work that writes nothing and counts its commits.</summary>
internal sealed class CountingUnitOfWork : IUnitOfWork
{
    public int Commits { get; private set; }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        Commits++;
        return Task.CompletedTask;
    }
}
