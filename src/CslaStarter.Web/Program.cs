using CslaStarter.Core.Security;
using CslaStarter.Data;
using CslaStarter.Infrastructure.DataProtection;
using CslaStarter.Infrastructure.Identity;
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
builder.Services.AddCslaStarterData(appConnection);
builder.Services.AddScoped<IDataAccessContext, HttpContextDataAccessContext>();
builder.Services.AddCslaStarterDataProtection(builder.Configuration, builder.Environment);
builder.Services.AddCslaStarterAuthentication(builder.Configuration, builder.Environment);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
return 0;

/// <summary>Entry point; partial so integration tests can host the app with WebApplicationFactory.</summary>
public partial class Program;
