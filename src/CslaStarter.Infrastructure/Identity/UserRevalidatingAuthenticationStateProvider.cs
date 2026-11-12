using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CslaStarter.Infrastructure.Identity;

/// <summary>
/// Every 10 minutes, re-checks the circuit's user against the database (no cache). A deactivated user, or one whose
/// roles changed, is signed out of the circuit; the next request signs them in again with their current roles.
/// </summary>
public sealed class UserRevalidatingAuthenticationStateProvider(ILoggerFactory loggerFactory, IServiceScopeFactory scopeFactory)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(10);

    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authenticationState);
        if (EntraClaims.UserId(authenticationState.User) is not { } userId)
        {
            return false;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
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
}
