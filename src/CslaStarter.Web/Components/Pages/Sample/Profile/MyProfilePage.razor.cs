using CslaStarter.Business.Users;
using CslaStarter.Core.Resources;
using Microsoft.AspNetCore.Components;

namespace CslaStarter.Web.Components.Pages.Sample.Profile;

/// <summary>The signed-in user's own profile: display name and preferred language.</summary>
public partial class MyProfilePage
{
    private static readonly IReadOnlyList<KeyValuePair<string, string>> Languages =
        [new("en-CA", Messages.Language_English), new("fr-CA", Messages.Language_French)];

    [Inject]
    private Csla.IDataPortal<UserProfile> Portal { get; set; } = default!;

    private Task<UserProfile> Load() => Portal.FetchAsync();
}
