using Csla;
using CslaStarter.Business.Errors;
using CslaStarter.Core.Resources;
using CslaStarter.Dal;

namespace CslaStarter.Business.Tests.Errors;

public class BusinessErrorsTests
{
    public static TheoryData<Exception, BusinessErrorKind> Cases() => new()
    {
        { new Csla.Security.SecurityException("no"), BusinessErrorKind.AccessDenied },
        { new UnauthorizedAccessException("no"), BusinessErrorKind.AccessDenied },
        { new ConcurrencyException("Project", "1"), BusinessErrorKind.Conflict },
        { new NotFoundException("Project", "1"), BusinessErrorKind.NotFound },
        { new Csla.Rules.ValidationException("Code is already used."), BusinessErrorKind.Invalid },
        { new OperationCanceledException(), BusinessErrorKind.Cancelled },
        { new InvalidOperationException("boom"), BusinessErrorKind.Unexpected },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void BusinessErrors_ClassifiesEachKind_DirectOrWrapped(Exception exception, BusinessErrorKind kind)
    {
        Assert.Equal(kind, BusinessErrors.Classify(exception).Kind);
        Assert.Equal(kind, BusinessErrors.Classify(new AggregateException(new DataPortalException("portal", exception, null))).Kind);
    }

    [Fact]
    public void BusinessErrors_Messages_AreLocalized_AndUnexpectedCarriesTheReference()
    {
        Assert.Equal(Messages.Error_Conflict, BusinessErrors.Classify(new ConcurrencyException()).Message);
        Assert.Equal("Code is already used.", BusinessErrors.Classify(new Csla.Rules.ValidationException("Code is already used.")).Message);
        Assert.Contains("ref-42", BusinessErrors.Classify(new InvalidOperationException("secret detail"), "ref-42").Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret detail", BusinessErrors.Classify(new InvalidOperationException("secret detail"), "ref-42").Message, StringComparison.Ordinal);
    }
}
