using System.Diagnostics;
using CslaStarter.Core.Security;
using Microsoft.AspNetCore.Http;

namespace CslaStarter.Infrastructure.Identity;

/// <summary>The signed-in user of the current HTTP request. The CSLA data portal registers its own context.</summary>
public sealed class HttpContextDataAccessContext(IHttpContextAccessor accessor) : IDataAccessContext
{
    public Guid? UserId => accessor.HttpContext?.User is { } user ? EntraClaims.UserId(user) : null;

    public string UserName => accessor.HttpContext?.User is { Identity.IsAuthenticated: true } user ? EntraClaims.DisplayName(user) : "anonymous";

    public string? CorrelationId => Activity.Current?.TraceId.ToString() ?? accessor.HttpContext?.TraceIdentifier;

    public string? OriginId => null;
}
