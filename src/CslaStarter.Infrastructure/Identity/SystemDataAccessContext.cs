using System.Diagnostics;
using CslaStarter.Core.Security;

namespace CslaStarter.Infrastructure.Identity;

/// <summary>Changes made by the platform itself (first sign-in provisioning, bootstrap-admin), not by a signed-in user.</summary>
public sealed class SystemDataAccessContext(string actor) : IDataAccessContext
{
    public Guid? UserId => null;

    public string UserName { get; } = actor;

    public string? CorrelationId => Activity.Current?.TraceId.ToString();

    public string? OriginId => null;
}
