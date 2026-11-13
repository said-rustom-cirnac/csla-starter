namespace CslaStarter.Core.Realtime;

/// <summary>Sends committed changes to every instance's hub. The data layer calls it after commit; developers never do.</summary>
public interface IEntityChangeNotifier
{
    Task PublishAsync(IReadOnlyList<EntityChange> changes, CancellationToken cancellationToken);
}
