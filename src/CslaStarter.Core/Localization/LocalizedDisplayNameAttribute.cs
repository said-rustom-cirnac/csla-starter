using System.ComponentModel;
using System.Globalization;
using CslaStarter.Core.Resources;

namespace CslaStarter.Core.Localization;

/// <summary>
/// A display name read from <see cref="Labels"/> in the current UI culture. CSLA reads DisplayName on every
/// access, so labels follow the user's language with nothing cached.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Event)]
public sealed class LocalizedDisplayNameAttribute(string key) : DisplayNameAttribute
{
    public string Key { get; } = key;

    public override string DisplayName =>
        Labels.ResourceManager.GetString(Key, CultureInfo.CurrentUICulture) ?? Key;
}
