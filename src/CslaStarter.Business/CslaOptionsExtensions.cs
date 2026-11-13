using Csla.Configuration;
using CslaStarter.Business.Rules;

namespace CslaStarter.Business;

public static class CslaOptionsExtensions
{
    /// <summary>The app's CSLA rule settings: async rules that throw break with a message instead of ending the process.</summary>
    public static CslaOptions UseCslaStarterRules(this CslaOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.UseUnhandledAsyncRuleExceptionHandler<AsyncRuleFailureHandler>();
    }
}
