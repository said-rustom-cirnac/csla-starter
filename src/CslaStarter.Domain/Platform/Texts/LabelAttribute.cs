using System.ComponentModel;

namespace CslaStarter.Domain.Platform.Texts;

/// <summary>Names a property's label by its English source string; the text is translated on every read.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class LabelAttribute(string source) : DisplayNameAttribute(source)
{
    public override string DisplayName => Text.T(Source);

    public string Source { get; } = source;
}
