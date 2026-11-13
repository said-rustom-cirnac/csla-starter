namespace CslaStarter.Core.Drafts;

/// <summary>
/// Unsaved edits kept on the server (never in the browser), encrypted, for 8 hours. Every call is checked against the
/// signed-in user: another user's key throws <see cref="UnauthorizedAccessException"/>.
/// </summary>
public interface IDraftStore
{
    /// <summary>Creates or replaces the draft; it expires 8 hours after this save.</summary>
    Task SaveAsync(DraftKey key, uint? version, byte[] payload, CancellationToken cancellationToken);

    /// <summary>The draft, or null when there is none or it expired.</summary>
    Task<Draft?> GetAsync(DraftKey key, CancellationToken cancellationToken);

    Task DeleteAsync(DraftKey key, CancellationToken cancellationToken);
}
