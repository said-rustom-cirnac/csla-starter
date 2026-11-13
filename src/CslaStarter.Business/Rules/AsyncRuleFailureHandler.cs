using Csla.Rules;
using CslaStarter.Core.Resources;
using Microsoft.Extensions.Logging;

namespace CslaStarter.Business.Rules;

/// <summary>
/// An asynchronous rule that throws (a database timeout, say) breaks with "couldn't be checked, try again" instead of
/// escaping: CSLA runs async rules fire-and-forget, so an unhandled exception would end the process.
/// </summary>
internal sealed partial class AsyncRuleFailureHandler(ILogger<AsyncRuleFailureHandler>? logger = null) : IUnhandledAsyncRuleExceptionHandler
{
    public bool CanHandle(Exception exception, IBusinessRuleBase executingRule) => true;

    public ValueTask Handle(Exception exception, IBusinessRuleBase executingRule, IRuleContext ruleContext)
    {
        ArgumentNullException.ThrowIfNull(executingRule);
        ArgumentNullException.ThrowIfNull(ruleContext);
        if (logger is not null)
        {
            LogRuleFailed(logger, exception, executingRule.RuleName);
        }

        var property = executingRule.PrimaryProperty?.FriendlyName ?? string.Empty;
        ruleContext.AddErrorResult(Messages.Common_CheckFailed.Replace("{0}", property, StringComparison.Ordinal));
        return ValueTask.CompletedTask;
    }

    [LoggerMessage(EventId = 3000, Level = LogLevel.Error, Message = "Business rule {Rule} failed; the field shows \"couldn't be checked\".")]
    private static partial void LogRuleFailed(ILogger logger, Exception exception, string rule);
}
