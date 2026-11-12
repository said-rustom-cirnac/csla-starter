using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Infrastructure.Web;

/// <summary>Protected B baseline response headers, including a strict, nonce-based Content-Security-Policy.</summary>
public static class SecurityHeaders
{
    public const string ContentSecurityPolicy =
        "default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; script-src 'self' 'nonce-{nonce}'; " +
        "style-src 'self'; img-src 'self' data:; font-src 'self'; connect-src 'self'; " +
        "form-action 'self' https://login.microsoftonline.com; upgrade-insecure-requests";

    public static IServiceCollection AddCslaStarterSecurityHeaders(this IServiceCollection services) =>
        services.AddScoped<CspNonce>();

    public static IApplicationBuilder UseCslaStarterSecurityHeaders(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            var nonce = context.RequestServices.GetRequiredService<CspNonce>().Value;
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = ContentSecurityPolicy.Replace("{nonce}", nonce, StringComparison.Ordinal);
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), microphone=(), payment=(), usb=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            return next(context);
        });
}
