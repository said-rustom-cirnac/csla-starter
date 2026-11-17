using Csla;

namespace CslaStarter.Domain.Platform;

/// <summary>The base of a read-only business object, such as one row of a list.</summary>
public abstract class AppReadOnlyBase<T> : ReadOnlyBase<T>
    where T : AppReadOnlyBase<T>;
