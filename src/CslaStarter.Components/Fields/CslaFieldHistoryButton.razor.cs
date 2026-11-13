using CslaStarter.Components.Forms;
using CslaStarter.Core.Resources;
using Microsoft.AspNetCore.Components;

namespace CslaStarter.Components.Fields;

/// <summary>The history icon next to a field, when its form has <c>FieldHistory</c> on.</summary>
public partial class CslaFieldHistoryButton
{
    [Parameter, EditorRequired]
    public object Model { get; set; } = default!;

    [Parameter, EditorRequired]
    public string Property { get; set; } = string.Empty;

    [Parameter]
    public string? Label { get; set; }

    [CascadingParameter]
    private ICslaFormContext? Form { get; set; }

    private string AriaLabel => $"{Messages.Field_History}: {Label ?? Property}";

    private void Open() => Form?.OpenHistory(Model, Property);
}
