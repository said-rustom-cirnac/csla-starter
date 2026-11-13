using CslaStarter.Data;
using CslaStarter.Data.Drafts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CslaStarter.Infrastructure.Realtime;

/// <summary>Deletes expired drafts at startup and then every hour. Reads ignore expired drafts in between.</summary>
public sealed partial class DraftCleanupService(AppDatabase database, TimeProvider time, ILogger<DraftCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Every, time);
        try
        {
            do
            {
                await CleanAsync(stoppingToken).ConfigureAwait(false);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task CleanAsync(CancellationToken stoppingToken)
    {
        try
        {
            var deleted = await DraftStore.DeleteExpiredAsync(database, time, stoppingToken).ConfigureAwait(false);
            if (deleted > 0)
            {
                LogDeleted(deleted);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // A failed run is retried at the next tick; expired drafts are already unreadable.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogFailed(ex);
        }
    }

    [LoggerMessage(EventId = 2300, Level = LogLevel.Information, Message = "Deleted {Count} expired drafts.")]
    private partial void LogDeleted(int count);

    [LoggerMessage(EventId = 2301, Level = LogLevel.Warning, Message = "Deleting expired drafts failed; retrying in an hour.")]
    private partial void LogFailed(Exception exception);
}
