namespace CslaStarter.Domain.Platform;

/// <summary>A DAL found no row of <c>type</c> with <c>key</c>.</summary>
public sealed class NotFoundException(string type, object key)
    : Exception($"{type} {key} was not found.");

/// <summary>Someone else saved the row of <c>type</c> with <c>key</c> after it was loaded.</summary>
public sealed class ConcurrencyException(string type, object key)
    : Exception($"{type} {key} was changed by someone else after it was loaded.");
