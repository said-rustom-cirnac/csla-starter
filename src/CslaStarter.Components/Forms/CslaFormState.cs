namespace CslaStarter.Components.Forms;

/// <summary>
/// What buttons need to know about their form, recreated on every form render (so buttons re-render with it), plus
/// the actions they call.
/// </summary>
public sealed record CslaFormState(bool IsBusy, bool IsDirty, bool CanSave, bool CanDelete, bool CanCreate, ICslaFormActions Actions);

public interface ICslaFormActions
{
    Task<bool> SaveAsync();

    Task CancelAsync();

    /// <summary>Deletes the object (after the caller confirmed). Returns whether it was deleted.</summary>
    Task<bool> DeleteAsync();
}
