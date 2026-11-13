using System.Reflection;
using Csla;
using Csla.Reflection;
using CslaStarter.Core.Resources;
using CslaStarter.Dal;

namespace CslaStarter.Business.Errors;

public enum BusinessErrorKind
{
    /// <summary>A permission check refused the operation.</summary>
    AccessDenied,

    /// <summary>Someone else saved the aggregate after it was loaded.</summary>
    Conflict,

    /// <summary>The record doesn't exist, or the user can no longer see it.</summary>
    NotFound,

    /// <summary>The object (or the database's constraints) rejected the values; the message says why.</summary>
    Invalid,

    /// <summary>The operation was cancelled (the user navigated away); nothing to show.</summary>
    Cancelled,

    /// <summary>Anything else: show a reference, log the details.</summary>
    Unexpected,
}

/// <summary>An operation's failure as the UI shows it. <see cref="Message"/> is localized and safe to display.</summary>
public sealed record BusinessError(BusinessErrorKind Kind, string Message, Exception Exception);

/// <summary>
/// Classifies the exceptions a data portal operation can end with, so components don't depend on DAL or CSLA exception
/// types. Wrappers (DataPortalException, AggregateException, CallMethodException, TargetInvocationException) are
/// unwrapped first.
/// </summary>
public static class BusinessErrors
{
    public static BusinessError Classify(Exception exception, string? reference = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var cause = Unwrap(exception);
        return cause switch
        {
            Csla.Security.SecurityException or UnauthorizedAccessException => new(BusinessErrorKind.AccessDenied, Messages.Error_AccessDenied, cause),
            ConcurrencyException => new(BusinessErrorKind.Conflict, Messages.Error_Conflict, cause),
            NotFoundException => new(BusinessErrorKind.NotFound, Messages.Error_NotFound, cause),
            Csla.Rules.ValidationException => new(BusinessErrorKind.Invalid, cause.Message, cause),
            OperationCanceledException => new(BusinessErrorKind.Cancelled, string.Empty, cause),
            _ => new(BusinessErrorKind.Unexpected, Messages.Error_Unexpected.Replace("{0}", reference ?? "-", StringComparison.Ordinal), cause),
        };
    }

    private static Exception Unwrap(Exception exception)
    {
        var current = exception;
        while (true)
        {
            var next = current switch
            {
                DataPortalException portal => portal.BusinessException ?? portal.InnerException,
                AggregateException { InnerExceptions.Count: 1 } aggregate => aggregate.InnerExceptions[0],
                CallMethodException call => call.InnerException,
                TargetInvocationException invocation => invocation.InnerException,
                _ => null,
            };
            if (next is null || ReferenceEquals(next, current))
            {
                return current;
            }

            current = next;
        }
    }
}
