using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace CslaStarter.Infrastructure.Telemetry;

/// <summary>
/// OpenTelemetry traces, metrics and logs for ASP.NET Core, Blazor circuits, HttpClient, Npgsql and the app's
/// own CslaStarter.* sources. Exported over OTLP only when OTEL_EXPORTER_OTLP_ENDPOINT is set.
/// </summary>
public static class TelemetrySetup
{
    public const string ServiceName = "CslaStarter";

    public static IServiceCollection AddCslaStarterTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var builder = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddNpgsql()
                .AddSource("Microsoft.AspNetCore.Components", "Microsoft.AspNetCore.Components.Server.Circuits", $"{ServiceName}.*"))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("Npgsql", "Microsoft.AspNetCore.Components", "Microsoft.AspNetCore.Components.Lifecycle", "Microsoft.AspNetCore.Components.Server.Circuits", $"{ServiceName}.*"))
            .WithLogging();

        if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            builder.UseOtlpExporter();
        }

        return services;
    }
}
