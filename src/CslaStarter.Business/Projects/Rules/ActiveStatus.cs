using Csla;
using Csla.Core;
using Csla.Rules;
using CslaStarter.Business.Types;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Business.Projects.Rules;

/// <summary>The status must be an active ProjectStatusType (or the project's original, possibly retired, value).</summary>
public sealed class ActiveStatus : BusinessRuleAsync
{
    private readonly Func<string> _message;

    public ActiveStatus(IPropertyInfo status, Func<string> message)
        : base(status)
    {
        _message = message;
        InputProperties.Add(status);
    }

    protected override async Task ExecuteAsync(IRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var code = (int)context.InputPropertyValues[PrimaryProperty!]!;
        var statuses = await context.ApplicationContext.GetRequiredService<IDataPortal<ProjectStatusTypeList>>().FetchAsync();
        if (statuses.Find(code) is not { IsActive: true })
        {
            context.AddErrorResult(RuleMessages.Format(_message, PrimaryProperty!.FriendlyName));
        }
    }
}
