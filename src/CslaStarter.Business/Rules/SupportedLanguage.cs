using Csla.Core;
using Csla.Rules;
using CslaStarter.Core.Localization;

namespace CslaStarter.Business.Rules;

/// <summary>The value must be one of the supported cultures (en-CA, fr-CA).</summary>
public sealed class SupportedLanguage : BusinessRule
{
    private readonly Func<string> _message;

    public SupportedLanguage(IPropertyInfo primaryProperty, Func<string> message)
        : base(primaryProperty)
    {
        _message = message;
        InputProperties.Add(primaryProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var value = context.InputPropertyValues[PrimaryProperty!] as string;
        if (value is null || !SupportedCultures.All.Contains(value))
        {
            context.AddErrorResult(string.Format(System.Globalization.CultureInfo.CurrentCulture, _message(), PrimaryProperty!.FriendlyName));
        }
    }
}
