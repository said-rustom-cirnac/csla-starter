namespace CslaStarter.Core.Auditing;

/// <summary>Reads the change history of one record (or one of its fields), newest first.</summary>
public interface IAuditHistoryReader
{
    Task<AuditPage> GetAsync(AuditQuery query, CancellationToken cancellationToken);
}
