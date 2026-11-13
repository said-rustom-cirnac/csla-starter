using System.ComponentModel;
using Csla;
using Csla.Core;
using Csla.Rules;

namespace CslaStarter.Components.Forms;

/// <summary>
/// CSLA's Blazor view model with what forms also need: Cancel as a real undo (object lifetime is managed), dirty and
/// savable state, a busy event (CSLA's IsBusy raises none), and permissions for this object rather than its type.
/// </summary>
public class AppViewModel<T> : Csla.Blazor.ViewModel<T>
    where T : class
{
    private bool _canEdit;
    private bool _canDelete;
    private bool _canCreate;

    public AppViewModel(ApplicationContext applicationContext)
        : base(applicationContext)
    {
        ManageObjectLifetime = true;
    }

    /// <summary>Raised when the object or any of its properties starts or stops being busy (an async rule running).</summary>
    public event Action? BusyChanged;

    public bool IsDirty => Model is ITrackStatus { IsDirty: true };

    /// <summary>Dirty, valid, not busy, and the user may save it.</summary>
    public bool IsSavable => Model is ITrackStatus { IsSavable: true };

    public bool CanEdit => _canEdit;

    public bool CanDelete => _canDelete;

    public bool CanCreate => _canCreate;

    /// <summary>Discards every change since the object was loaded (or last saved), children included.</summary>
    public Task CancelAsync()
    {
        DoCancel();
        return Task.CompletedTask;
    }

    /// <summary>Recomputes the permissions, for example after the user's roles changed.</summary>
    public void RefreshPermissions()
    {
        var model = Model;
        _canEdit = model is not null && BusinessRules.HasPermission(ApplicationContext, AuthorizationActions.EditObject, model);
        _canDelete = model is not null && BusinessRules.HasPermission(ApplicationContext, AuthorizationActions.DeleteObject, model);
        _canCreate = BusinessRules.HasPermission(ApplicationContext, AuthorizationActions.CreateObject, typeof(T));
    }

    protected override void OnModelChanging(T? oldValue, T? newValue)
    {
        base.OnModelChanging(oldValue, newValue);
        if (oldValue is INotifyBusy oldBusy)
        {
            oldBusy.BusyChanged -= OnBusyChanged;
        }

        if (newValue is INotifyBusy newBusy)
        {
            newBusy.BusyChanged += OnBusyChanged;
        }
    }

    protected override void OnModelChanged()
    {
        base.OnModelChanged();
        RefreshPermissions();
    }

    private void OnBusyChanged(object? sender, BusyChangedEventArgs e) => BusyChanged?.Invoke();
}
