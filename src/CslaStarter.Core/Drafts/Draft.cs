namespace CslaStarter.Core.Drafts;

/// <summary>
/// A decrypted draft: the serialized business object, and the record's version when it was taken (to warn the user when the
/// record changed since).
/// </summary>
public sealed record Draft(uint? Version, byte[] Payload, DateTimeOffset SavedAt);
