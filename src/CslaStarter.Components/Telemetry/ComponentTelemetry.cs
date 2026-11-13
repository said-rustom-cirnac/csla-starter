using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CslaStarter.Components.Telemetry;

/// <summary>Spans and durations for component operations (load, save, delete, refresh), tagged with component and entity type.</summary>
public static class ComponentTelemetry
{
    public const string Name = "CslaStarter.Components";

    public static readonly ActivitySource Source = new(Name);

    private static readonly Meter Meter = new(Name);

    public static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("cslastarter.component.operation.duration", "s", "How long component operations take.");
}
