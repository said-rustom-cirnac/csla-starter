using System.Globalization;
using CslaStarter.Components.Drafts;
using CslaStarter.Core.Resources;
using Microsoft.AspNetCore.Components;

namespace CslaStarter.Components.Localization;

/// <summary>
/// Switches between English and French. A circuit's culture is fixed, so it reloads the page through
/// <c>/culture/set</c>; before that it writes every open form's draft, and the forms offer them back after the reload.
/// </summary>
public partial class CslaLanguageToggle
{
    private static bool IsFrench => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr";

    private static string TargetCulture => IsFrench ? "en-CA" : "fr-CA";

    private static string TargetLanguage => IsFrench ? "en" : "fr";

    private static string Text => IsFrench ? Messages.Language_English : Messages.Language_French;

    /// <summary>The page in the other language, for the GCDS header's lang-href.</summary>
    public string LanguageHref => $"/culture/set?culture={TargetCulture}&redirectUri={Uri.EscapeDataString(CurrentPath)}";

    private string CurrentPath => "/" + Navigation.ToBaseRelativePath(Navigation.Uri);

    [Inject]
    private DraftFlushRegistry Drafts { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    private async Task ToggleAsync()
    {
        await Drafts.FlushAllAsync();
        Navigation.NavigateTo(LanguageHref, forceLoad: true);
    }
}
