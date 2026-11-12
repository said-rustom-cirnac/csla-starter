namespace CslaStarter.Data.Entities;

/// <summary>Rows that carry who created and last changed them. The audit interceptor fills these in.</summary>
public interface IAuditedEntity
{
    DateTimeOffset CreatedOn { get; set; }

    string CreatedBy { get; set; }

    DateTimeOffset ModifiedOn { get; set; }

    string ModifiedBy { get; set; }
}
