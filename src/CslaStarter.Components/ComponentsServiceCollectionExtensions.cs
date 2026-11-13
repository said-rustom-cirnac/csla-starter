using CslaStarter.Components.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Components;

public static class ComponentsServiceCollectionExtensions
{
    /// <summary>The per-circuit services the components share: toasts, announcements and the circuit's origin id.</summary>
    public static IServiceCollection AddCslaStarterComponents(this IServiceCollection services)
    {
        services.AddScoped<IToastService, ToastService>();
        services.AddScoped<ILiveAnnouncer, LiveAnnouncer>();
        services.AddScoped<CircuitOrigin>();
        return services;
    }
}
