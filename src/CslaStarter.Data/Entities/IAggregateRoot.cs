namespace CslaStarter.Data.Entities;

/// <summary>The root row of an aggregate. Its PostgreSQL xmin is the aggregate's concurrency version.</summary>
public interface IAggregateRoot
{
    uint Version { get; set; }
}
