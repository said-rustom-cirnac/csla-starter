using System.Reflection;
using CslaStarter.Components.Interop;
using CslaStarter.Core;
using CslaStarter.Core.Security;

namespace CslaStarter.Components.Fields;

/// <summary>
/// A text field for a <see cref="SensitiveAttribute"/> property: rendered as a mask on the server, so the value never
/// reaches the browser until a user with a reveal role clicks Show, which writes a SensitiveReveal security event.
/// </summary>
public partial class CslaSensitiveField
{
    private const string MaskText = "••••••";

    protected bool Revealed { get; private set; }

    private SensitiveAttribute? Sensitive => Model.GetType().GetProperty(PropertyName)?.GetCustomAttribute<SensitiveAttribute>();

    private bool CanReveal => Sensitive is { } sensitive && CurrentUser.HasAnyRole(sensitive.RevealRoles);

    private string Mask => string.IsNullOrEmpty(Text) ? string.Empty : MaskText;

    private async Task RevealAsync()
    {
        if (!CanReveal)
        {
            return;
        }

        var identity = Model as IEntityIdentity;
        await WriteSecurityEventAsync(SecurityEventKind.SensitiveReveal, $"{identity?.EntityType} {identity?.EntityKey} {PropertyName}");
        Revealed = true;
    }

    private void OnInput(GcdsEventArgs e) => SetText(e.Value);
}
