using Csla;
using Csla.Core;
using Csla.Rules;
using CslaStarter.Business.Types;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Business.Projects.Rules;

/// <summary>
/// The status must be an active ProjectStatusType, or the status the project had when loaded (it may have been retired
/// since; the project stays savable). Runs only where the user edits, never inside a data portal operation.
/// </summary>
public sealed class ActiveStatus : BusinessRuleAsync
{
    private readonly IPropertyInfo _original;
    private readonly Func<string> _message;

    public ActiveStatus(IPropertyInfo status, IPropertyInfo originalStatus, Func<string> message)
        : base(status)
    {
        _original = originalStatus;
        _message = message;
        InputProperties.Add(status);
        InputProperties.Add(originalStatus);
        RunMode = RunModes.DenyOnServerSidePortal;
    }

    protected override async Task ExecuteAsync(IRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var code = (int)context.InputPropertyValues[PrimaryProperty!]!;
        if (code == (int)context.InputPropertyValues[_original]!)
        {
            return;
        }

        var statuses = await context.ApplicationContext.GetRequiredService<IDataPortal<ProjectStatusTypeList>>().FetchAsync();
        if (statuses.Find(code) is not { IsActive: true })
        {
            context.AddErrorResult(RuleMessages.Format(_message, PrimaryProperty!.FriendlyName));
        }
    }
}
