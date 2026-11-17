using Csla;

namespace CslaStarter.Domain.Platform;

/// <summary>The base of a read-only list of read-only business objects.</summary>
public abstract class AppReadOnlyListBase<TList, TItem> : ReadOnlyListBase<TList, TItem>
    where TList : AppReadOnlyListBase<TList, TItem>;
