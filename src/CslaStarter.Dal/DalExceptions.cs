namespace CslaStarter.Dal;

/// <summary>Someone else saved this aggregate after it was loaded (its root version no longer matches).</summary>
public sealed class ConcurrencyException : Exception
{
    public ConcurrencyException()
    {
    }

    public ConcurrencyException(string message)
        : base(message)
    {
    }

    public ConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ConcurrencyException(string entityType, string key)
        : base($"{entityType} {key} was changed by someone else.")
    {
    }
}

/// <summary>The record doesn't exist, or the current user isn't allowed to see it (the two are never told apart).</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException()
    {
    }

    public NotFoundException(string message)
        : base(message)
    {
    }

    public NotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public NotFoundException(string entityType, string key)
        : base($"{entityType} {key} was not found.")
    {
    }
}

/// <summary>A save broke a unique index: another record already has this value (for example, a project code).</summary>
public sealed class DuplicateValueException : Exception
{
    public DuplicateValueException()
    {
    }

    public DuplicateValueException(string message)
        : base(message)
    {
    }

    public DuplicateValueException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public DuplicateValueException(string entityType, string property, Exception? innerException = null)
        : base($"Another {entityType} already has this {property}.", innerException)
    {
        EntityType = entityType;
        Property = property;
    }

    public string EntityType { get; } = string.Empty;

    /// <summary>The property the unique index covers, as the business object names it.</summary>
    public string Property { get; } = string.Empty;
}
