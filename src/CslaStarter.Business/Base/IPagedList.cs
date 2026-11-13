namespace CslaStarter.Business.Base;

/// <summary>One page of a server-paged list, with the total across all pages (after record-level access).</summary>
public interface IPagedList
{
    int TotalCount { get; }
}
