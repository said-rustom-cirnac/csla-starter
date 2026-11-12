using System.Globalization;
using CslaStarter.Core.Localization;
using CslaStarter.Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Infrastructure.Localization;

/// <summary>
/// en-CA and fr-CA. The culture comes from the culture cookie, then the signed-in user's preferred language,
/// then en-CA. A circuit's culture is fixed when it starts, so switching goes through /culture/set and a reload.
/// </summary>
public static class LocalizationSetup
{
    public static IServiceCollection AddCslaStarterLocalization(this IServiceCollection services)
    {
        services.AddLocalization();
        services.Configure<RequestLocalizationOptions>(options =>
        {
            var cultures = SupportedCultures.All.Select(CultureInfo.GetCultureInfo).ToList();
            options.DefaultRequestCulture = new RequestCulture(SupportedCultures.English);
            options.SupportedCultures = cultures;
            options.SupportedUICultures = cultures;
            options.RequestCultureProviders =
            [
                new CookieRequestCultureProvider(),
                new CustomRequestCultureProvider(context =>
                {
                    var language = context.User.FindFirst(EntraClaims.LanguageClaim)?.Value;
                    return Task.FromResult(language is not null && SupportedCultures.All.Contains(language)
                        ? new ProviderCultureResult(language)
                        : null);
                }),
            ];
        });
        return services;
    }

    public static IEndpointConventionBuilder MapCultureEndpoint(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/culture/set", (string? culture, string? redirectUri, HttpContext context) =>
        {
            if (culture is not null && SupportedCultures.All.Contains(culture))
            {
                context.Response.Cookies.Append(
                    CookieRequestCultureProvider.DefaultCookieName,
                    CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                    new CookieOptions { Secure = true, HttpOnly = true, SameSite = SameSiteMode.Lax, IsEssential = true, MaxAge = TimeSpan.FromDays(365) });
            }

            return Results.Redirect(IsLocal(redirectUri) ? redirectUri! : "/");
        }).AllowAnonymous();

    /// <summary>
    /// Only same-site paths: "/x" but never "//host", "/\host", absolute URLs, or any control character (browsers
    /// strip tabs and newlines, so "/\t/host" would become "//host").
    /// </summary>
    public static bool IsLocal(string? url) =>
        !string.IsNullOrEmpty(url)
        && url[0] == '/'
        && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'))
        && !url.AsSpan().ContainsAnyInRange('\0', '\u001f')
        && !url.Contains('\u007f', StringComparison.Ordinal);
}
