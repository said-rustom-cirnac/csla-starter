using Csla;
using CslaStarter.Core.Localization;
using CslaStarter.Core.Resources;

namespace CslaStarter.Business.Base;

/// <summary>
/// Base of an aggregate root. <see cref="Version"/> is the root row's PostgreSQL xmin: every save of the aggregate
/// sends it back, and the save fails if anyone changed any part of the aggregate since it was loaded.
/// </summary>
[Serializable]
public abstract class AppRootBase<T> : AppBusinessBase<T>
    where T : AppRootBase<T>
{
    public static readonly PropertyInfo<uint> VersionProperty = RegisterProperty<uint>(nameof(Version));

    [LocalizedDisplayName(nameof(Labels.Common_Version))]
    public uint Version => GetProperty(VersionProperty);

    protected void LoadVersion(uint version) => LoadProperty(VersionProperty, version);
}
