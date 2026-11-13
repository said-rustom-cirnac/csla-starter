namespace CslaStarter.Core.Drafts;

/// <summary>One draft per user and record. A user can only reach their own drafts.</summary>
public sealed record DraftKey(Guid UserId, string EntityType, string EntityKey);
