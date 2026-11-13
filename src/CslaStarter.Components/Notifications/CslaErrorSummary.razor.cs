using System.Text.Json;
using Csla.Rules;
using CslaStarter.Components.Forms;
using Microsoft.AspNetCore.Components;

namespace CslaStarter.Components.Notifications;

/// <summary>
/// After a save attempt that found errors, lists every error of the object and its children (gcds-error-summary,
/// each linking to its field) and moves focus to it. CslaForm renders it at the top of the form.
/// </summary>
public partial class CslaErrorSummary
{
    private ElementReference _summary;
    private int _focusedFor;

    protected IReadOnlyList<KeyValuePair<string, string>> Errors { get; private set; } = [];

    protected string ErrorLinks => JsonSerializer.Serialize(Errors.ToDictionary(e => e.Key, e => e.Value));

    [CascadingParameter]
    private ICslaFormContext? Form { get; set; }

    // Recreated on every form render, so the summary re-renders with the form (the context above is fixed).
    [CascadingParameter]
    private CslaFormState? State { get; set; }

    protected override void OnParametersSet()
    {
        Errors = Form is { FailedSaves: > 0, Root: { } root } ? Collect(Form, root) : [];
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Errors.Count > 0 && Form!.FailedSaves != _focusedFor)
        {
            _focusedFor = Form.FailedSaves;
            await _summary.FocusAsync();
        }
    }

    private static List<KeyValuePair<string, string>> Collect(ICslaFormContext form, object root)
    {
        var errors = new List<KeyValuePair<string, string>>();
        foreach (var node in BusinessRules.GetAllBrokenRules(root, errorsOnly: true))
        {
            foreach (var rule in node.BrokenRules.Where(r => r.Severity == RuleSeverity.Error && !string.IsNullOrEmpty(r.Property)))
            {
                var link = "#" + form.FieldId(node.Object, rule.Property!);
                if (errors.All(e => e.Key != link))
                {
                    errors.Add(new(link, rule.Description));
                }
            }
        }

        return errors;
    }
}
