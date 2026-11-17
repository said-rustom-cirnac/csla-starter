namespace CslaStarter.Domain.Platform.Security;

/// <summary>The claims the app adds to a principal at sign-in and reads everywhere else.</summary>
public static class AppClaimTypes
{
    public const string UserId = "csla_starter:user_id";
    public const string DisplayName = "csla_starter:display_name";
}
