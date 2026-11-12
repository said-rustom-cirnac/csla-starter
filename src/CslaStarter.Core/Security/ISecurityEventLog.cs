namespace CslaStarter.Core.Security;

/// <summary>Records security-relevant events (sign-in, access denied, role changes...) for audit and SIEM forwarding.</summary>
public interface ISecurityEventLog
{
    Task WriteAsync(SecurityEvent securityEvent, CancellationToken cancellationToken);
}
