using Csla.Core;
using Csla.Rules;

namespace CslaStarter.Business.Projects.Rules;

/// <summary>
/// A project is closed only by <see cref="CloseProjectCommand"/>, which checks for open tasks and who may close it.
/// Editing the status to Closed is refused; a project that was already closed stays valid.
/// </summary>
public sealed class ClosedByCommandOnly : BusinessRule
{
    private readonly IPropertyInfo _original;
    private readonly int _closed;
    private readonly Func<string> _message;

    public ClosedByCommandOnly(IPropertyInfo status, IPropertyInfo originalStatus, int closedCode, Func<string> message)
        : base(status)
    {
        _original = originalStatus;
        _closed = closedCode;
        _message = message;
        InputProperties.Add(status);
        InputProperties.Add(originalStatus);
    }

    protected override void Execute(IRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if ((int)context.InputPropertyValues[PrimaryProperty!]! == _closed && (int)context.InputPropertyValues[_original]! != _closed)
        {
            context.AddErrorResult(_message());
        }
    }
}
