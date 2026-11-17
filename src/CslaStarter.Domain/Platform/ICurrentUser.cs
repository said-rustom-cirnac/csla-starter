namespace CslaStarter.Domain.Platform;

/// <summary>The user the current operation runs for.</summary>
public interface ICurrentUser
{
    bool IsSignedIn { get; }

    /// <summary>The app's own user id, or null when nobody is signed in.</summary>
    Guid? UserId { get; }

    /// <summary>The display name, or an empty string when nobody is signed in.</summary>
    string DisplayName { get; }
}
