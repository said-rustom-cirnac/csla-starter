namespace CslaStarter.Core.Security;

/// <summary>Who is making a data change, and from where. The data layer reads it to stamp and audit changes.</summary>
public interface IDataAccessContext
{
    Guid? UserId { get; }

    string UserName { get; }

    /// <summary>Role codes of the user, for record-level access filters. Empty for platform operations.</summary>
    IReadOnlySet<int> RoleCodes { get; }

    string? CorrelationId { get; }

    /// <summary>Identifies the circuit that made a change, so it can ignore its own change notice.</summary>
    string? OriginId { get; }
}
