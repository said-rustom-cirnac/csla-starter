using Microsoft.AspNetCore.Components.Server.Circuits;

namespace CslaStarter.Infrastructure.Sessions;

/// <summary>Every inbound circuit activity (clicks, typing, JS interop calls) counts as user activity.</summary>
public sealed class SessionActivityCircuitHandler(SessionTimeoutMonitor monitor) : CircuitHandler
{
    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(Func<CircuitInboundActivityContext, Task> next) =>
        context =>
        {
            monitor.RecordActivity();
            return next(context);
        };
}
