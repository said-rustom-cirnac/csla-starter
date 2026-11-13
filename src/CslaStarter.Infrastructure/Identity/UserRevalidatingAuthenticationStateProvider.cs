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
/// notice names them. A deactivated or deleted user is signed out of the circuit. When only the roles changed, the
/// circuit's principal gets the current roles in place (what the next request's claims transformation would give), so
/// the user keeps their screen and their unsaved work.
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
        var current = await FreshAsync(authenticationState.User, cancellationToken).ConfigureAwait(false);
        if (current is not { IsActive: true })
        {
            return false;
        }

        if (!StillMatches(authenticationState.User, current))
        {
            SetAuthenticationState(Task.FromResult(new AuthenticationState(WithRoles(authenticationState.User, current.RoleCodes))));
        }

        return true;
    }

    /// <summary>The principal with the app identity's role claims replaced by <paramref name="roleCodes"/>; other identities untouched.</summary>
    public static ClaimsPrincipal WithRoles(ClaimsPrincipal principal, IReadOnlySet<int> roleCodes)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(roleCodes);
        return new ClaimsPrincipal(principal.Identities.Select(identity =>
        {
            if (identity.AuthenticationType != RoleClaimsTransformation.AuthenticationType)
            {
                return identity.Clone();
            }

            var replaced = new ClaimsIdentity(identity.Claims.Where(c => c.Type != identity.RoleClaimType), identity.AuthenticationType, identity.NameClaimType, identity.RoleClaimType);
            replaced.AddClaims(roleCodes.Order().Select(code => new Claim(identity.RoleClaimType, code.ToString(CultureInfo.InvariantCulture))));
            return replaced;
        }));
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

    private async Task<DirectoryUser?> FreshAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (EntraClaims.UserId(principal) is not { } userId)
        {
            return null;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IUserDirectory>().GetFreshAsync(userId, cancellationToken).ConfigureAwait(false);
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
