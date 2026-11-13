using Csla.Testing;
using CslaStarter.Business.Drafts;
using CslaStarter.Business.Projects;
using CslaStarter.Business.Tests.Projects;
using CslaStarter.Dal.Projects;
using CslaStarter.Dal.Types;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Business.Tests.Drafts;

public class DraftCodecTests
{
    private readonly FakeProjectDal _dal = new();
    private readonly FakeTypeDal _types = new();
    private readonly Guid _projectId = Guid.CreateVersion7();

    public DraftCodecTests()
    {
        _types.Rows["ProjectStatusType"] = [new(1, "Planned", "Planifié", true, 1), new(2, "Active", "Actif", true, 2)];
        _dal.Add(_projectId, "P-1", tasks: [new ProjectTaskDto(Guid.CreateVersion7(), "Open task", false, null, 1)]);
    }

    [Fact]
    public async Task DraftCodec_ProjectWithEdits_RoundTripsDirtyState()
    {
        using var host = CslaTestHost.Create(o => o
            .AsPrincipal(TestPrincipals.Admin)
            .ConfigureCsla(csla => csla.UseCslaStarterRules())
            .ConfigureServices(s => s.AddCslaStarterBusiness().AddSingleton<IProjectDal>(_dal).AddSingleton<ITypeDal>(_types)));
        var project = await host.GetDataPortal<ProjectEdit>().FetchAsync(_projectId);
        project.Name = "Draft name";
        project.AddTask("Draft task");

        var restored = DraftCodec.Deserialize<ProjectEdit>(host.ApplicationContext, DraftCodec.Serialize(host.ApplicationContext, project));

        Assert.True(restored.IsDirty);
        Assert.False(restored.IsNew);
        Assert.Equal(("Draft name", 5u), (restored.Name, restored.Version));
        Assert.Equal(["Open task", "Draft task"], restored.Tasks.Select(t => t.Title));
        Assert.True(restored.Tasks[1].IsNew);
        restored.AddTask("After restoring");
        Assert.Equal(3, restored.Tasks.Count);
    }

    [Fact]
    public void DraftCodec_UnreadablePayload_IsNoDraft()
    {
        using var host = CslaTestHost.Create(o => o.AsPrincipal(TestPrincipals.Admin).ConfigureServices(s => s.AddCslaStarterBusiness()));

        Assert.Null(DraftCodec.TryDeserialize<ProjectEdit>(host.ApplicationContext, [1, 2, 3, 4]));
    }
}
