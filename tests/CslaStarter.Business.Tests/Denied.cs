namespace CslaStarter.Business.Tests;

/// <summary>Asserts an operation was refused for lack of permission, however CSLA wraps the refusal.</summary>
internal static class Denied
{
    public static async Task Assert(Func<Task> operation)
    {
        var error = await Xunit.Assert.ThrowsAnyAsync<Exception>(operation);
        var refusal = error is Csla.DataPortalException portal ? portal.BusinessException : error;
        Xunit.Assert.IsType<Csla.Security.SecurityException>(refusal);
    }
}
