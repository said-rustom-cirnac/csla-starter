using System.Reflection;
using CslaStarter.Core;

namespace CslaStarter.Components.Forms;

/// <summary>The form's state its fields read: the view model, which fields were touched, changed or just updated by someone else.</summary>
public sealed class CslaFormContext<T> : ICslaFormContext
    where T : class
{
    private static readonly TimeSpan HighlightFor = TimeSpan.FromSeconds(5);

    private readonly string _formId;
    private readonly TimeProvider _time;
    private readonly HashSet<string> _touched = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object?> _loaded = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _highlightedUntil = new(StringComparer.Ordinal);

    internal CslaFormContext(string formId, AppViewModel<T> viewModel, TimeProvider time)
    {
        _formId = formId;
        _time = time;
        ViewModel = viewModel;
        Snapshot();
    }

    public AppViewModel<T> ViewModel { get; }

    public T Model => ViewModel.Model ?? throw new InvalidOperationException("The form has no object yet.");

    /// <summary>A save was attempted: every field shows its errors.</summary>
    public bool Submitted { get; internal set; }

    public string FieldId(object model, string propertyName) =>
        ReferenceEquals(model, ViewModel.Model)
            ? $"{_formId}-{propertyName}"
            : $"{_formId}-{(model as IEntityIdentity)?.EntityKey ?? "child"}-{propertyName}";

    public Csla.Blazor.IPropertyInfo PropertyInfo(object model, string propertyName) => new Csla.Blazor.PropertyInfo(model, propertyName, " ");

    public bool ShowError(object model, string propertyName) => Submitted || _touched.Contains(FieldId(model, propertyName));

    public bool IsChanged(object model, string propertyName) =>
        ReferenceEquals(model, ViewModel.Model) && _loaded.TryGetValue(propertyName, out var loaded) && !Equals(loaded, Read(model, propertyName));

    public bool IsHighlighted(object model, string propertyName) =>
        _highlightedUntil.TryGetValue(FieldId(model, propertyName), out var until) && _time.GetUtcNow() < until;

    public void Touch(object model, string propertyName) => _touched.Add(FieldId(model, propertyName));

    /// <summary>Marks root properties someone else just changed; they stay highlighted for 5 seconds.</summary>
    public void Highlight(IEnumerable<string> propertyNames)
    {
        ArgumentNullException.ThrowIfNull(propertyNames);
        var until = _time.GetUtcNow() + HighlightFor;
        foreach (var name in propertyNames)
        {
            _highlightedUntil[FieldId(ViewModel.Model!, name)] = until;
        }
    }

    /// <summary>
    /// After a load, save, cancel or reload: the values of <paramref name="baseline"/> (default: the current object)
    /// become the "loaded" ones, and nothing is touched.
    /// </summary>
    public void Snapshot(object? baseline = null)
    {
        _touched.Clear();
        _loaded.Clear();
        Submitted = false;
        if ((baseline ?? ViewModel.Model) is not { } model)
        {
            return;
        }

        foreach (var property in model.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && IsSimple(p.PropertyType)))
        {
            _loaded[property.Name] = property.GetValue(model);
        }
    }

    private static object? Read(object model, string propertyName) => model.GetType().GetProperty(propertyName)?.GetValue(model);

    private static bool IsSimple(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive || underlying.IsEnum || underlying == typeof(string) || underlying == typeof(decimal)
            || underlying == typeof(DateOnly) || underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset) || underlying == typeof(Guid);
    }
}
