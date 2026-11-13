namespace CslaStarter.Core;

/// <summary>What a business object is, for change notifications, presence, drafts and history: a type name and a key.</summary>
public interface IEntityIdentity
{
    string EntityType { get; }

    string EntityKey { get; }
}
