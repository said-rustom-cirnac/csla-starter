namespace CslaStarter.Domain.Platform.Users;

/// <summary>The app's account for a person who has just signed in.</summary>
public sealed record SignedInUser(Guid Id, string DisplayName, bool IsActive);

/// <summary>Finds the account of a person signing in, creating it at their first sign-in.</summary>
public interface IUserDirectory
{
    /// <summary>
    /// The account for <paramref name="tenantId"/> and <paramref name="objectId"/>. A first sign-in creates it, active and
    /// named <paramref name="displayName"/>; a later one returns it unchanged, keeping a name edited in the app.
    /// Safe when the same person's first sign-ins race: they all get the one account.
    /// </summary>
    Task<SignedInUser> EnsureAsync(string tenantId, string objectId, string displayName, CancellationToken ct);
}
