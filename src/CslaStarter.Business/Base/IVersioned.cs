namespace CslaStarter.Business.Base;

/// <summary>An aggregate root's concurrency version, so components can tell whether a change notice is news.</summary>
public interface IVersioned
{
    uint Version { get; }
}
