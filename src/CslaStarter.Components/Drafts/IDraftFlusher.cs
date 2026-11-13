namespace CslaStarter.Components.Drafts;

/// <summary>Something holding unsaved edits that can write them to a draft at once.</summary>
public interface IDraftFlusher
{
    Task FlushDraftAsync();
}
