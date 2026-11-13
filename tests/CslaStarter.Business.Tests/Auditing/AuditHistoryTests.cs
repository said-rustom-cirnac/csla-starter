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

    private static AuditEntry Entry(long id, params AuditChange[] changes) =>
        new(id, new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero), Guid.CreateVersion7(), "Jane", "Update", "Project", "p-1", changes);

    private CslaTestHost Host() => CslaTestHost.Create(o => o
        .AsPrincipal(TestPrincipals.Admin)
        .ConfigureServices(s => s.AddCslaStarterBusiness().AddSingleton<IAuditHistoryReader>(_reader).AddSingleton<ITypeDal>(_types)));
}
