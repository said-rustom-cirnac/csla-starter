using System.Globalization;
using Csla;
using Csla.Testing;
using CslaStarter.Business.Auditing;
using CslaStarter.Core.Auditing;
using CslaStarter.Dal;
using CslaStarter.Dal.Types;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Business.Tests.Auditing;

public class AuditHistoryTests
{
    private readonly FakeAuditHistoryReader _reader = new();
    private readonly FakeTypeDal _types = new();

    public AuditHistoryTests() =>
        _types.Rows["ProjectStatusType"] = [new(1, "Planned", "Planifié", true, 1), new(2, "Active", "Actif", true, 2)];

    [Fact]
    public async Task AuditHistory_HiddenRecord_IsNotFound()
    {
        _reader.Hidden.Add("p-hidden");
        using var host = Host();

        var error = await Assert.ThrowsAsync<DataPortalException>(() => host.GetDataPortal<AuditHistory>().FetchAsync("Project", "p-hidden", string.Empty, 1, 25));

        Assert.IsType<NotFoundException>(error.BusinessException);
    }

    [Fact]
    public async Task AuditHistory_TypeCodesShowDescriptions_AndFieldsTheirLabels()
    {
        _reader.Entries.Add(Entry(1, new AuditChange("StatusCode", "1", "2", false, "ProjectStatusType"), new AuditChange("Name", "Old", "New", false, null)));
        using var host = Host();
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-CA");
            var history = await host.GetDataPortal<AuditHistory>().FetchAsync("Project", "p-1", string.Empty, 1, 25);

            var status = history[0].Changes.Single(c => c.Field == "StatusCode");
            Assert.Equal(("Statut", "Planifié", "Actif"), (status.FieldName, status.OldValue, status.NewValue));
            Assert.Equal("Nom", history[0].Changes.Single(c => c.Field == "Name").FieldName);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public async Task AuditHistory_OneField_AndPaging_PassThrough()
    {
        _reader.Entries.Add(Entry(1, new AuditChange("Name", "a", "b", false, null)));
        _reader.Entries.Add(Entry(2, new AuditChange("Budget", null, null, true, null)));
        using var host = Host();

        var history = await host.GetDataPortal<AuditHistory>().FetchAsync("Project", "p-1", "Budget", 1, 10);

        Assert.Equal(("Budget", 1, 10), (_reader.LastQuery!.Property, _reader.LastQuery.Page, _reader.LastQuery.PageSize));
        var change = Assert.Single(Assert.Single(history).Changes);
        Assert.True(change.IsMasked);
        Assert.Equal(1, history.TotalCount);
    }

    [Fact]
    public async Task AuditHistory_LeavesOutFieldsTheUserMayNotRead()
    {
        _reader.Entries.Add(Entry(1, new AuditChange("Name", "a", "b", false, null), new AuditChange("Budget", "10", "20", false, null)));
        _reader.Entries.Add(Entry(2, new AuditChange("Budget", "20", "30", false, null)));
        using var host = Host(TestPrincipals.Client);

        var history = await host.GetDataPortal<AuditHistory>().FetchAsync("Project", "p-1", string.Empty, 1, 25);

        Assert.Contains("Project.Budget", _reader.LastQuery!.HiddenFields!);
        Assert.DoesNotContain("Project.Name", _reader.LastQuery.HiddenFields!);
        Assert.Equal("Name", Assert.Single(Assert.Single(history).Changes).Field);
        Assert.Equal(1, history.TotalCount);
    }

    [Fact]
    public async Task AuditHistory_OneFieldTheUserMayNotRead_IsEmpty()
    {
        _reader.Entries.Add(Entry(1, new AuditChange("Budget", "10", "20", false, null)));
        using var host = Host(TestPrincipals.Client);

        var history = await host.GetDataPortal<AuditHistory>().FetchAsync("Project", "p-1", "Budget", 1, 25);

        Assert.Empty(history);
        Assert.Equal(0, history.TotalCount);
    }

    [Theory]
    [InlineData("User")]
    [InlineData("NoSuchType")]
    public async Task AuditHistory_RecordTheUserMayNotOpen_IsDenied(string entityType)
    {
        using var host = Host(TestPrincipals.Client);

        var error = await Assert.ThrowsAsync<DataPortalException>(() => host.GetDataPortal<AuditHistory>().FetchAsync(entityType, "k-1", string.Empty, 1, 25));

        Assert.IsType<Csla.Security.SecurityException>(error.BusinessException);
        Assert.Null(_reader.LastQuery);
    }

    [Fact]
    public void HistoryOf_NamesTheTypesOwnEntityType()
    {
        using var host = Host();
        var context = host.Services.GetRequiredService<ApplicationContext>();

        Assert.NotEmpty(AuditedTypes.EntityTypes);
        Assert.All(AuditedTypes.EntityTypes, name =>
            Assert.Equal(name, ((CslaStarter.Core.IEntityIdentity)context.CreateInstanceDI(AuditedTypes.Find(name)!)).EntityType));
    }

    private static AuditEntry Entry(long id, params AuditChange[] changes) =>
        new(id, new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero), Guid.CreateVersion7(), "Jane", "Update", "Project", "p-1", changes);

    private CslaTestHost Host(System.Security.Claims.ClaimsPrincipal? user = null) => CslaTestHost.Create(o => o
        .AsPrincipal(user ?? TestPrincipals.Admin)
        .ConfigureServices(s => s.AddCslaStarterBusiness().AddSingleton<IAuditHistoryReader>(_reader).AddSingleton<ITypeDal>(_types)));
}
