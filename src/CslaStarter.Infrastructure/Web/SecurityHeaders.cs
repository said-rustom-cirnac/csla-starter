using CslaStarter.Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Infrastructure.Web;

/// <summary>Protected B baseline response headers, including a strict, nonce-based Content-Security-Policy.</summary>
public static class SecurityHeaders
{
    // {provider} is the sign-in provider's origin (AuthenticationSetup.ProviderOrigin): form-action also covers the
    // redirect that ends a sign-out POST there, so without it the browser blocks signing out.
    public const string ContentSecurityPolicy =
        "default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; script-src 'self' 'nonce-{nonce}'; " +
        "style-src 'self' 'nonce-{nonce}'; img-src 'self' data:; font-src 'self'; connect-src 'self'; " +
        "form-action 'self' {provider}; upgrade-insecure-requests";

    public static IServiceCollection AddCslaStarterSecurityHeaders(this IServiceCollection services)
    {
        services.AddScoped<CspNonce>();
        // GC HTTPS guidance: at least one year.
        services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(365);
            options.IncludeSubDomains = true;
        });
        return services;
    }

    /// <summary>
    /// Sets the headers when the response starts, so they survive the exception handler clearing the response and
    /// re-executing the error page (which then also gets its own scope's nonce).
    /// </summary>
    public static IApplicationBuilder UseCslaStarterSecurityHeaders(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var policy = ContentSecurityPolicy.Replace(
            "{provider}", AuthenticationSetup.ProviderOrigin(app.ApplicationServices.GetRequiredService<IConfiguration>()), StringComparison.Ordinal);
        return app.Use((context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var nonce = context.RequestServices.GetRequiredService<CspNonce>().Value;
                var headers = context.Response.Headers;
                headers.ContentSecurityPolicy = policy.Replace("{nonce}", nonce, StringComparison.Ordinal);
                headers.XContentTypeOptions = "nosniff";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), microphone=(), payment=(), usb=()";
                headers["Cross-Origin-Opener-Policy"] = "same-origin";
                return Task.CompletedTask;
            });
            return next(context);
        });
    }
}
