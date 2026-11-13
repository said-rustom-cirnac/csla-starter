using System.Threading.Channels;
using CslaStarter.Core.Realtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CslaStarter.Infrastructure.Realtime;

/// <summary>
/// Carries change notices between app instances over PostgreSQL LISTEN/NOTIFY. Publishing dispatches to this
/// instance's hub first, then NOTIFYs on a pooled connection; a dedicated connection LISTENs and dispatches what other
/// instances sent (its own echoes are dropped by instance id). When the listening connection is lost it reconnects
/// with backoff (0.5 s doubling to 30 s) and raises Resync, because notices sent meanwhile are gone. Presence travels
/// the same way on its own channel.
/// </summary>
public sealed partial class PostgresChangeBackplane(
    NpgsqlDataSource dataSource, IEntityChangeHub hub, TimeProvider time, ILogger<PostgresChangeBackplane> logger)
    : BackgroundService, IEntityChangeNotifier, IPresenceTransport
{
    public const string ChangesChannel = "app_entity_changes";
    public const string PresenceChannel = "app_presence";

    private static readonly TimeSpan FirstRetry = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan LastRetry = TimeSpan.FromSeconds(30);

    // How long the listener waits for a notice before checking that its connection is still alive.
    private static readonly TimeSpan ProbeAfter = TimeSpan.FromSeconds(30);

    // Changes (EntityChange) and presence (PresenceWire) from other instances, in arrival order.
    private readonly Channel<object> _incoming = Channel.CreateBounded<object>(
        new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    private volatile bool _listening;
    private Func<string, PresenceMessage, Task>? _presenceReceived;

    /// <summary>Identifies this instance on the backplane.</summary>
    public string InstanceId { get; } = Guid.NewGuid().ToString("N");

    /// <summary>True while the listening connection is up; readiness reports Degraded otherwise.</summary>
    public bool IsListening => _listening;

    public async Task PublishAsync(IReadOnlyList<EntityChange> changes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        foreach (var change in changes)
        {
            await hub.DispatchAsync(change).ConfigureAwait(false);
        }

        var payloads = changes.Select(c => BackplaneMessage.Serialize(InstanceId, c)).Where(Fits).ToArray();
        await NotifyAsync(ChangesChannel, payloads, cancellationToken).ConfigureAwait(false);
    }

    public Task SendAsync(IReadOnlyList<PresenceMessage> messages, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return NotifyAsync(PresenceChannel, messages.Select(m => PresenceWire.Serialize(InstanceId, m)).Where(Fits).ToArray(), cancellationToken);
    }

    public IDisposable Subscribe(Func<string, PresenceMessage, Task> received)
    {
        ArgumentNullException.ThrowIfNull(received);
        if (Interlocked.CompareExchange(ref _presenceReceived, received, null) is not null)
        {
            throw new InvalidOperationException("The backplane has one presence tracker.");
        }

        return new PresenceSubscription(this, received);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(ListenAsync(stoppingToken), DispatchIncomingAsync(stoppingToken));

    private async Task NotifyAsync(string channel, string[] payloads, CancellationToken cancellationToken)
    {
        if (payloads.Length == 0)
        {
            return;
        }

        var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            using var command = new NpgsqlCommand("SELECT pg_notify(@channel, payload) FROM unnest(@payloads) AS payload", connection);
            command.Parameters.AddWithValue("channel", channel);
            command.Parameters.AddWithValue("payloads", payloads);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ListenAsync(CancellationToken stoppingToken)
    {
        var retry = FirstRetry;
        var missedNotices = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var connection = await dataSource.OpenConnectionAsync(stoppingToken).ConfigureAwait(false);
                await using (connection.ConfigureAwait(false))
                {
                    connection.Notification += OnNotification;
                    using (var listen = new NpgsqlCommand($"LISTEN {ChangesChannel}; LISTEN {PresenceChannel}", connection))
                    {
                        await listen.ExecuteNonQueryAsync(stoppingToken).ConfigureAwait(false);
                    }

                    _listening = true;
                    retry = FirstRetry;
                    if (missedNotices)
                    {
                        missedNotices = false;
                        LogReconnected();
                        await hub.RaiseResyncAsync().ConfigureAwait(false);
                    }

                    while (true)
                    {
                        if (!await connection.WaitAsync(ProbeAfter, stoppingToken).ConfigureAwait(false))
                        {
                            using var probe = new NpgsqlCommand("SELECT 1", connection);
                            await probe.ExecuteNonQueryAsync(stoppingToken).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
#pragma warning disable CA1031 // Any failure of the listening connection means: reconnect.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _listening = false;
                missedNotices = true;
                LogListenerLost(ex, retry);
                try
                {
                    await Task.Delay(retry, time, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                retry = retry * 2 > LastRetry ? LastRetry : retry * 2;
            }
        }

        _listening = false;
        _incoming.Writer.TryComplete();
    }

    // Raised on the listening connection while it waits; keep it short and hand the work to the dispatch loop.
    private void OnNotification(object sender, NpgsqlNotificationEventArgs e)
    {
        if (e.Channel == ChangesChannel && BackplaneMessage.TryDeserialize(e.Payload) is { } message && message.Instance != InstanceId)
        {
            _incoming.Writer.TryWrite(message.ToChange());
        }
        else if (e.Channel == PresenceChannel && PresenceWire.TryDeserialize(e.Payload) is { } presence && presence.Instance != InstanceId)
        {
            _incoming.Writer.TryWrite(presence);
        }
    }

    private async Task DispatchIncomingAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var item in _incoming.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                if (item is EntityChange change)
                {
                    await hub.DispatchAsync(change).ConfigureAwait(false);
                }
                else if (item is PresenceWire presence && Volatile.Read(ref _presenceReceived) is { } received)
                {
                    await DeliverPresenceAsync(received, presence).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task DeliverPresenceAsync(Func<string, PresenceMessage, Task> received, PresenceWire presence)
    {
        try
        {
            await received(presence.Instance, presence.ToMessage()).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // A presence failure must not stop change dispatch.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogPresenceFailed(ex);
        }
    }

    private bool Fits(string payload)
    {
        if (payload.Length <= BackplaneMessage.MaxPayloadLength)
        {
            return true;
        }

        LogPayloadTooLarge(payload.Length);
        return false;
    }

    [LoggerMessage(EventId = 2100, Level = LogLevel.Warning, Message = "Lost the change listener connection; reconnecting in {Retry}. Other instances' changes aren't shown until then.")]
    private partial void LogListenerLost(Exception exception, TimeSpan retry);

    [LoggerMessage(EventId = 2101, Level = LogLevel.Information, Message = "Change listener reconnected; open screens resynchronize.")]
    private partial void LogReconnected();

    [LoggerMessage(EventId = 2103, Level = LogLevel.Warning, Message = "Applying a presence message from another instance failed.")]
    private partial void LogPresenceFailed(Exception exception);

    private sealed class PresenceSubscription(PostgresChangeBackplane backplane, Func<string, PresenceMessage, Task> received) : IDisposable
    {
        public void Dispose() => Interlocked.CompareExchange(ref backplane._presenceReceived, null, received);
    }

    [LoggerMessage(EventId = 2102, Level = LogLevel.Warning, Message = "A change notice of {Length} characters is too large for NOTIFY; other instances won't see it.")]
    private partial void LogPayloadTooLarge(int length);
}
