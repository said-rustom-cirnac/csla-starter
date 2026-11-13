namespace CslaStarter.Dal;

/// <summary>One page of a list, with the total count of rows the user may see.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);
