using Csla;

namespace CslaStarter.Domain.Platform;

/// <summary>
/// The base of an aggregate root. <see cref="Version"/> is the root row's version as loaded; every save sends it
/// back, and the save fails if anyone changed the aggregate since.
/// </summary>
/// <remarks>
/// The name matters. CSLA 10.2's property generator recognises a class by its direct base class's name, which must
/// contain <c>BusinessBase</c>, <c>ReadOnlyBase</c> or <c>CommandBase</c>. With any other name the generator emits
/// an empty partial and the build fails with CS9248.
/// </remarks>
public abstract class AppRootBusinessBase<T> : BusinessBase<T>
    where T : AppRootBusinessBase<T>
{
    public static readonly PropertyInfo<uint> VersionProperty = RegisterProperty<uint>(nameof(Version));

    public uint Version => ReadProperty(VersionProperty);

    protected void LoadVersion(uint version) => LoadProperty(VersionProperty, version);
}
