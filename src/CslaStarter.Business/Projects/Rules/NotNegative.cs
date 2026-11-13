using Csla.Core;
using Csla.Rules;

namespace CslaStarter.Business.Projects.Rules;

/// <summary>An optional amount that, when set, cannot be below zero.</summary>
public sealed class NotNegative : BusinessRule
{
    private readonly Func<string> _message;

    public NotNegative(IPropertyInfo amount, Func<string> message)
        : base(amount)
    {
        _message = message;
        InputProperties.Add(amount);
    }

    protected override void Execute(IRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.InputPropertyValues[PrimaryProperty!] is decimal value && value < 0)
        {
            context.AddErrorResult(RuleMessages.Format(_message, PrimaryProperty!.FriendlyName));
        }
    }
}
