using Csla;
using Csla.Core;
using Csla.Rules;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Business.Projects.Rules;

/// <summary>
/// The project code must not be used by any other project. Asynchronous: the field shows "Checking…" while it runs.
/// It runs only where the user edits (never inside a data portal operation, which would share that operation's
/// DbContext); saves are guarded by the unique index instead. Users who can't edit projects don't run it, so it can't
/// be used to discover codes of projects they can't see.
/// </summary>
public sealed class UniqueProjectCode : BusinessRuleAsync
{
    private readonly IPropertyInfo _id;
    private readonly Func<string> _message;

    public UniqueProjectCode(IPropertyInfo code, IPropertyInfo id, Func<string> message)
        : base(code)
    {
        _id = id;
        _message = message;
        InputProperties.Add(code);
        InputProperties.Add(id);
        RunMode = RunModes.DenyOnServerSidePortal;
    }

    protected override async Task ExecuteAsync(IRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.InputPropertyValues[PrimaryProperty!] is not string { Length: > 0 } code
            || !BusinessRules.HasPermission(context.ApplicationContext, AuthorizationActions.EditObject, typeof(ProjectCodeExistsCommand)))
        {
            return;
        }

        var id = (Guid)context.InputPropertyValues[_id]!;
        var portal = context.ApplicationContext.GetRequiredService<IDataPortal<ProjectCodeExistsCommand>>();
        var check = await portal.ExecuteAsync(code, id);
        if (check.Exists)
        {
            context.AddErrorResult(RuleMessages.Format(_message, PrimaryProperty!.FriendlyName));
        }
    }
}
