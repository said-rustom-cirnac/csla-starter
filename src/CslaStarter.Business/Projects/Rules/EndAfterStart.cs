using Csla.Core;
using Csla.Rules;

namespace CslaStarter.Business.Projects.Rules;

/// <summary>The end date (primary property) cannot be before the start date.</summary>
public sealed class EndAfterStart : BusinessRule
{
    private readonly IPropertyInfo _start;
    private readonly Func<string> _message;

    public EndAfterStart(IPropertyInfo end, IPropertyInfo start, Func<string> message)
        : base(end)
    {
        _start = start;
        _message = message;
        InputProperties.Add(end);
        InputProperties.Add(start);
    }

    protected override void Execute(IRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.InputPropertyValues[PrimaryProperty!] is DateOnly end
            && context.InputPropertyValues[_start] is DateOnly start
            && end < start)
        {
            context.AddErrorResult(RuleMessages.Format(_message, PrimaryProperty!.FriendlyName));
        }
    }
}
