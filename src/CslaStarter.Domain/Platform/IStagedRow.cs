namespace CslaStarter.Domain.Platform;

/// <summary>A row a DAL write method has staged. Its <see cref="Version"/> is valid only after the unit of work commits.</summary>
public interface IStagedRow
{
    uint Version { get; }
}
