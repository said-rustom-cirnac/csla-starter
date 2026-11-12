using Microsoft.AspNetCore.Authorization;

namespace CslaStarter.Web.Components.Pages;

/// <summary>Where a forbidden request lands. Anonymous, so a denied user never loops back into another denial.</summary>
[AllowAnonymous]
public partial class AccessDenied;
