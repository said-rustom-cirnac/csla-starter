using Csla.Configuration;
using CslaStarter.Business;
using CslaStarter.Data;
using CslaStarter.Infrastructure.Database;
using CslaStarter.Infrastructure.DataProtection;
using CslaStarter.Infrastructure.Health;
using CslaStarter.Infrastructure.Identity;
using CslaStarter.Infrastructure.Localization;
using CslaStarter.Infrastructure.Realtime;
using CslaStarter.Infrastructure.Sessions;
using CslaStarter.Infrastructure.Telemetry;
using CslaStarter.Infrastructure.Web;
using CslaStarter.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// One-off command run by the deployer: dotnet CslaStarter.Web.dll bootstrap-admin --tenant <tid> --object-id <oid>
if (args is [BootstrapAdminCommand.Name, .. var commandArgs])
{
    using var loggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
    return await BootstrapAdminCommand.RunAsync(builder.Configuration, commandArgs, loggerFactory, CancellationToken.None);
}

var appConnection = builder.Configuration.GetConnectionString("App")
    ?? throw new InvalidOperationException("ConnectionStrings:App is missing.");

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = builder.Environment.IsDevelopment());
builder.Services.AddHttpContextAccessor();
builder.Services.AddCsla(options => options
    .UseCslaStarterRules()
    .AddAspNetCore()
    .AddServerSideBlazor(blazor => blazor.UseCslaPermissionsPolicy = true)
    .DataPortal(dataPortal =>
    {
        dataPortal.UseLegacyOperationMethods = false;
        dataPortal.AddServerSideDataPortal();
    }));
builder.Services.AddCslaStarterBusiness();
builder.Services.AddCslaStarterData(
    appConnection,
    builder.Configuration.GetValue<bool>("Database:UseEntraAuthentication") ? EntraDatabaseAuthentication.Configure : null);
builder.Services.AddCslaStarterDataProtection(builder.Configuration, builder.Environment);
builder.Services.AddCslaStarterAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddCslaStarterSecurityHeaders();
builder.Services.AddCslaStarterLocalization();
builder.Services.AddCslaStarterSessions(builder.Configuration);
builder.Services.AddCslaStarterRealtime(builder.Configuration);
builder.Services.AddCslaStarterHealth();
builder.Services.AddCslaStarterTelemetry(builder.Configuration);

var app = builder.Build();

app.UseCslaStarterSecurityHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseRequestLocalization();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets().AllowAnonymous();
app.MapCultureEndpoint();
app.MapCslaStarterHealth();
app.MapRazorComponents<App>()
    // Our own CSP header already sets frame-ancestors 'none'; don't let Blazor add a second policy.
    .AddInteractiveServerRenderMode(options => options.ContentSecurityFrameAncestorsPolicy = null);

await app.RunAsync();
return 0;

/// <summary>Entry point; partial so integration tests can host the app with WebApplicationFactory.</summary>
public partial class Program;
