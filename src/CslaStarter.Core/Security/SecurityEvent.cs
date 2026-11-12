namespace CslaStarter.Core.Security;

public sealed record SecurityEvent(SecurityEventKind Kind, Guid? UserId, string? UserName, string Detail, string? CorrelationId);
