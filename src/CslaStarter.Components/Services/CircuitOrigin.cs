namespace CslaStarter.Components.Services;

/// <summary>
/// Identifies this circuit in change notices (one per circuit, scoped). Components stamp it into CSLA's ClientContext,
/// so the data layer records it as each change's origin and the circuit can ignore its own echo.
/// </summary>
public sealed class CircuitOrigin
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
}
