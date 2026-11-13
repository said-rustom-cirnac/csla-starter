using System.Diagnostics;
using System.Security.Claims;
using Csla;
using CslaStarter.Core.Security;

namespace CslaStarter.Business.Security;

/// <summary>
/// Who is making a data change, read from CSLA's <see cref="ApplicationContext"/>. Each data portal call runs in its
/// own DI scope, but CSLA flows the caller's principal (and ClientContext) into it, so this is right in every scope.
/// </summary>
public sealed class CslaDataAccessContext(ApplicationContext applicationContext) : IDataAccessContext
{
    /// <summary>The ClientContext key components set to identify their circuit (to ignore their own change notices).</summary>
    public const string OriginKey = "csla_starter:origin";

    private ClaimsPrincipal Principal => applicationContext.User as ClaimsPrincipal ?? new ClaimsPrincipal();

    public Guid? UserId => PrincipalReader.UserId(Principal);

    public string UserName => Principal.Identity?.IsAuthenticated == true ? PrincipalReader.DisplayName(Principal) : "anonymous";

    public IReadOnlySet<int> RoleCodes => PrincipalReader.RoleCodes(Principal);

    public string? CorrelationId => Activity.Current?.TraceId.ToString();

    public string? OriginId => applicationContext.ClientContext.TryGetValue(OriginKey, out var origin) ? origin?.ToString() : null;
}
