using CslaStarter.Core;

namespace CslaStarter.Components.Base;

/// <summary>A component that holds a CSLA object, and so knows the entity type and key it works on.</summary>
public abstract class CslaBaseComponent<T> : CslaBaseComponent
    where T : class
{
    /// <summary>The object the component currently shows, or null before it loads.</summary>
    protected abstract T? Current { get; }

    protected string? EntityType => (Current as IEntityIdentity)?.EntityType;

    protected string? EntityKey => (Current as IEntityIdentity)?.EntityKey;

    protected override string? TelemetryEntityType => EntityType;
}
