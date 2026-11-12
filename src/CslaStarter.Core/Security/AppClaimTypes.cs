namespace CslaStarter.Core.Security;

/// <summary>Claims the app adds to a signed-in principal (by the claims transformation) and reads everywhere else.</summary>
public static class AppClaimTypes
{
    public const string UserId = "csla_starter:user_id";
    public const string Language = "csla_starter:lang";
    public const string Inactive = "csla_starter:inactive";
}
