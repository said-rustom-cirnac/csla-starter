namespace CslaStarter.Core.Realtime;

public enum ChangeKind
{
    Inserted,
    Updated,
    Deleted,

    /// <summary>A user's roles or record memberships changed. The notice's key is that user's id.</summary>
    AccessChanged,
}

/// <summary>
/// A hint that an aggregate root changed, published after commit. It carries no field data: receivers re-read
/// through the normal access rules. Optimistic concurrency, not this notice, is the guarantee.
/// </summary>
/// <remarks>
/// <c>Version</c> is the root's row version after the change (null when deleted or not versioned). <c>OriginId</c> is the
/// circuit that made the change, so it can ignore its own echo.
/// </remarks>
public sealed record EntityChange(
    string EntityType,
    string EntityKey,
    ChangeKind Kind,
    uint? Version,
    Guid? ChangedByUserId,
    string ChangedByName,
    string? OriginId)
{
    /// <summary>Entity type of <see cref="ChangeKind.AccessChanged"/> notices; the key is the user id.</summary>
    public const string UserAccessType = "UserAccess";

    public ChangeTopic RecordTopic => ChangeTopic.ForRecord(EntityType, EntityKey);

    public ChangeTopic TypeTopic => ChangeTopic.ForType(EntityType);

    public static EntityChange UserAccess(Guid userId, Guid? changedByUserId, string changedByName, string? originId) =>
        new(UserAccessType, userId.ToString(), ChangeKind.AccessChanged, null, changedByUserId, changedByName, originId);
}
