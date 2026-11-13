using System.Globalization;
using System.Security.Claims;
using CslaStarter.Core.Realtime;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CslaStarter.Infrastructure.Identity;

/// <summary>
/// Re-checks the circuit's user against the database (no cache) every 10 minutes, and at once when an access-changed
/// notice names them. A deactivated user, or one whose roles changed, is signed out of the circuit; the next request
/// signs them in again with their current roles.
/// </summary>
public sealed partial class UserRevalidatingAuthenticationStateProvider : RevalidatingServerAuthenticationStateProvider
{
    private static readonly ClaimsPrincipal SignedOut = new(new ClaimsIdentity());

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEntityChangeHub? _hub;
    private readonly ILogger _logger;
    private readonly Lock _gate = new();
    private IDisposable? _accessNotices;

    public UserRevalidatingAuthenticationStateProvider(ILoggerFactory loggerFactory, IServiceScopeFactory scopeFactory, IEntityChangeHub? hub = null)
        : base(loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _scopeFactory = scopeFactory;
        _hub = hub;
        _logger = loggerFactory.CreateLogger<UserRevalidatingAuthenticationStateProvider>();
        AuthenticationStateChanged += WatchAccess;
    }

    /// <summary>True once the circuit listens for access-changed notices about its user.</summary>
    public bool IsWatchingAccess
    {
        get
        {
            lock (_gate)
            {
                return _accessNotices is not null;
            }
        }
    }

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(10);

    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authenticationState);
        if (EntraClaims.UserId(authenticationState.User) is not { } userId)
        {
            return false;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var current = await scope.ServiceProvider.GetRequiredService<IUserDirectory>().GetFreshAsync(userId, cancellationToken);
        return current is not null && StillMatches(authenticationState.User, current);
    }

    public static bool StillMatches(ClaimsPrincipal principal, DirectoryUser current)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(current);
        var claimed = principal.FindAll(ClaimTypes.Role)
            .Select(c => int.TryParse(c.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code) ? code : -1)
            .ToHashSet();
        return current.IsActive && claimed.SetEquals(current.RoleCodes);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            AuthenticationStateChanged -= WatchAccess;
            Replace(null);
        }

        base.Dispose(disposing);
    }

    private async void WatchAccess(Task<AuthenticationState> task)
    {
        try
        {
            var state = await task.ConfigureAwait(false);
            var userId = EntraClaims.UserId(state.User);
            Replace(userId is { } id && _hub is not null
                ? _hub.Subscribe(ChangeTopic.ForRecord(EntityChange.UserAccessType, id.ToString()), _ => RevalidateNowAsync())
                : null);
        }
#pragma warning disable CA1031 // An event handler must never throw; the periodic revalidation still runs.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogWatchFailed(_logger, ex);
        }
    }

    private async Task RevalidateNowAsync()
    {
        var state = await GetAuthenticationStateAsync().ConfigureAwait(false);
        if (!await ValidateAuthenticationStateAsync(state, CancellationToken.None).ConfigureAwait(false))
        {
            SetAuthenticationState(Task.FromResult(new AuthenticationState(SignedOut)));
        }
    }

    private void Replace(IDisposable? subscription)
    {
        IDisposable? previous;
        lock (_gate)
        {
            previous = _accessNotices;
            _accessNotices = subscription;
        }

        previous?.Dispose();
    }

    [LoggerMessage(EventId = 1200, Level = LogLevel.Warning, Message = "Could not watch access changes for this circuit's user.")]
    private static partial void LogWatchFailed(ILogger logger, Exception exception);
}
