using Csla;
using Csla.Core;
using Csla.Rules;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Business.Projects.Rules;

/// <summary>
/// The project code must not be used by any other project. Asynchronous: the field shows "Checking…" while it runs.
/// The check goes through the data portal (<see cref="ProjectCodeExistsCommand"/>), so it gets its own scope.
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
    }

    protected override async Task ExecuteAsync(IRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.InputPropertyValues[PrimaryProperty!] is not string { Length: > 0 } code)
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
