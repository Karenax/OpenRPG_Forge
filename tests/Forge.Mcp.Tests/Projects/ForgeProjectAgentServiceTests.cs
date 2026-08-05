using System.Text.Json;
using Forge.Domain.Areas;
using Forge.Domain.Dialogues;
using Forge.Domain.Projects;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.Mcp.Dialogues;
using Forge.Mcp.Assets;
using Forge.Mcp.Projects;
using Forge.Mcp.Scripts;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Mcp.Tests.Projects;

public sealed class ForgeProjectAgentServiceTests
{
    [Fact]
    public void SummaryRevisionAndDiagnosticsAreReadOnlyAndStructured()
    {
        using var workspace = new TestWorkspace();

        var summary = workspace.Project.GetSummary();

        Assert.Equal("crypt-of-ashes", summary.ModuleSlug);
        Assert.Equal(0, summary.Revision);
        Assert.True(summary.EntityCount >= 16);
        Assert.Empty(workspace.Project.GetDiagnostics());
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    [Fact]
    public void DiagnosticsExposeCanonicalIdentitySourceAndQuickFixes()
    {
        using var workspace = new TestWorkspace();
        var source = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var emptyArea = new AreaDefinition(
            AreaId.From(Guid.Parse("71500000-0000-4000-8000-000000000001")),
            "diagnostic-area",
            "Diagnostic area",
            []);
        var project = new ProjectDefinition(
            source.Id,
            source.Title,
            source.Module,
            source.Blueprints,
            [.. source.Areas, emptyArea],
            source.Dialogues,
            source.Quests);
        var diagnosticWorkspace = Path.Combine(workspace.RootDirectory, "diagnostic-workspace");
        Assert.True(ProjectSession.Create(diagnosticWorkspace, project).Succeeded);
        var service = ForgeProjectAgentService.Open(diagnosticWorkspace).Service!;

        var diagnostic = Assert.Single(service.GetDiagnostics(), item =>
            item.Code == AreaNavigationValidator.MissingSpawnCode &&
            item.Path!.Contains(emptyArea.Id.ToString(), StringComparison.Ordinal));

        Assert.NotNull(diagnostic.Id);
        Assert.Equal("Error", diagnostic.Severity);
        Assert.Equal(emptyArea.Id.ToString(), diagnostic.SubjectUuid);
        Assert.Equal("AreaNavigationValidator", diagnostic.Source);
        Assert.NotNull(diagnostic.Property);
        Assert.Single(diagnostic.QuickFixes!);
        Assert.Equal("CreatePlayerSpawn", diagnostic.QuickFixes![0].Kind);
        Assert.Equal("derived-project-diagnostic", diagnostic.Provenance);
    }

    [Fact]
    public void AssetCatalogResourceSupportsFacetedReadOnlySearch()
    {
        using var workspace = new TestWorkspace();

        var result = workspace.Project.GetAssets(new AgentAssetCatalogQuery(
            Text: "apple",
            Source: "ambientcg",
            MaximumResults: 10));

        Assert.Equal(1, result.SchemaVersion);
        Assert.Equal(605, result.CatalogCount);
        Assert.Equal(3, result.Sources.Count);
        Assert.Equal(3, result.Items.Count);
        Assert.Empty(result.Diagnostics);
        Assert.All(result.Items, item =>
        {
            Assert.NotEqual(Guid.Empty.ToString(), item.Id);
            Assert.Equal("CC0-1.0", item.LicenseSpdx);
            Assert.True(item.LicenseVerified);
            Assert.Equal(64, item.ContentSha256.Length);
            Assert.Equal("ambientcg", item.Source);
        });
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    [Fact]
    public void RuntimePolicyResourceIsCanonicalAndReadOnly()
    {
        using var workspace = new TestWorkspace();

        var resource = workspace.Project.GetRuntimePolicies();

        Assert.Equal(1, resource.SchemaVersion);
        Assert.Equal(0, resource.Revision);
        Assert.Equal(resource.Module.Id.ToString("D"), resource.Module.Id.ToString());
        Assert.Equal(resource.AreaId, resource.Area.Id.ToString("D"));
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    [Fact]
    public void RuntimePolicyPlanSupportsDryRunApplyInversePersistenceAndAudit()
    {
        using var workspace = new TestWorkspace();
        var resource = workspace.Project.GetRuntimePolicies();
        var global = new HeartbeatScheduleDefinition(Guid.NewGuid(), 1, "World clock", HeartbeatScope.Module,
            true, 2_000, 12, false, "1.0", "OnGlobalHeartbeat", "mcp-test");
        var local = new HeartbeatScheduleDefinition(Guid.NewGuid(), 1, "Area ecology", HeartbeatScope.Area,
            true, 1_000, 8, true, "1.0", "OnAreaHeartbeat", "mcp-test");
        var module = new ModuleRuntimePolicyProfile(resource.Module.Id, 1, [global], resource.Module.Rest,
            resource.Module.SequenceAbort, "mcp-test");
        var area = new AreaRuntimePolicyProfile(resource.Area.Id, 1, [local], resource.Area.Rest, "mcp-test");
        var request = new RuntimePolicyPlanRequest(1, resource.Revision, resource.AreaId, module, area);

        var planned = workspace.Project.PlanRuntimePolicies(request);

        Assert.True(planned.Succeeded);
        Assert.Equal("runtimePolicies", planned.Diff?.Path);
        Assert.Equal("planned", Assert.Single(planned.Audit).Action);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());

        var applied = workspace.Project.ApplyRuntimePolicyPlan(planned.PlanId!);

        Assert.True(applied.Applied);
        Assert.Equal(1, workspace.Project.GetCurrentRevision());
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        Assert.Single(persisted.Module.RuntimePolicies.Heartbeats);
        Assert.Single(persisted.Areas.Single(item => item.Id.ToString() == resource.AreaId).RuntimePolicies.Heartbeats);

        var reverted = workspace.Project.RevertRuntimePolicyPlan(planned.PlanId!);

        Assert.True(reverted.Reverted);
        Assert.Equal(["planned", "apply", "revert"], reverted.Audit.Select(item => item.Action));
        Assert.Equal(2, workspace.Project.GetCurrentRevision());
    }

    [Fact]
    public void CinematicPlanSupportsReadDryRunApplyPersistenceAndRevert()
    {
        using var workspace = new TestWorkspace();
        var resource = workspace.Project.GetCinematics();
        var anchor = new CameraAnchorDefinition(Guid.NewGuid(), "MCP shot",
            new TransformValue(new Vector3Value(1, 4, 5), QuaternionValue.Identity, Vector3Value.One),
            Vector3Value.Zero, CameraFraming.Wide, 55, 2_000, .08f, ["mcp"], "mcp-test");
        var area = new AreaCinematicProfile(resource.Area.Id, 1, [anchor], "mcp-test");
        var sequence = new SequenceDefinition(Guid.NewGuid(), 1, "mcp-shot", "MCP shot", 2_000, true, false,
            [new SequenceTrackDefinition(Guid.NewGuid(), "Camera", SequenceTrackKind.Camera,
                SequenceTargetKind.CameraAnchor, anchor.Id, string.Empty,
                [new SequenceKeyframe(Guid.NewGuid(), 0, null, string.Empty, SequenceEasing.Linear)], "mcp-test")],
            [], "mcp-test");
        var startup = new StartupFlowDefinition(resource.Module.Startup.Id, 1, StartupFlowKind.Sequence,
            sequence.Id, string.Empty, StartupFlowKind.None, null, string.Empty, true, "Passer", "mcp-test");
        var module = new ModuleCinematicProfile(resource.Module.Id, 1, startup, [sequence], "mcp-test");
        var planned = workspace.Project.PlanCinematics(new CinematicPlanRequest(1, resource.Revision,
            resource.AreaId, module, area));

        Assert.True(planned.Succeeded);
        Assert.Equal("runtimePolicies/cinematics", planned.Diff?.Path);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        var applied = workspace.Project.ApplyCinematicPlan(planned.PlanId!);
        Assert.True(applied.Applied);
        Assert.Single(ProjectSession.Open(workspace.RootDirectory).Session!.Project.Module.RuntimePolicies.Cinematics.Sequences);
        var reverted = workspace.Project.RevertCinematicPlan(planned.PlanId!);
        Assert.True(reverted.Reverted);
        Assert.Equal(["planned", "apply", "revert"], reverted.Audit.Select(item => item.Action));
    }

    [Fact]
    public void DialoguePlanSchemaIsValidJsonSchema()
    {
        using var document = JsonDocument.Parse(DialoguePlanSchema.Json);

        Assert.Equal(
            "OpenRPG Forge dialogue mutation plan",
            document.RootElement.GetProperty("title").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("properties").GetProperty("schemaVersion").GetProperty("const").GetInt32());
    }

    [Fact]
    public void DialoguePlanSupportsDryRunDiffApplyInverseAndAudit()
    {
        using var workspace = new TestWorkspace();
        var request = CreatePlan(workspace.RootDirectory);

        var planned = workspace.Project.PlanDialogue(request);

        Assert.True(planned.Succeeded);
        Assert.False(planned.Applied);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        Assert.Equal("add", planned.Diff?.Operation);
        Assert.Equal("remove", planned.Inverse?.Operation);
        Assert.Equal("planned", Assert.Single(planned.Audit).Action);

        var applied = workspace.Project.ApplyDialoguePlan(planned.PlanId!);

        Assert.True(applied.Succeeded);
        Assert.True(applied.Applied);
        Assert.Equal(1, workspace.Project.GetCurrentRevision());
        Assert.Equal(["planned", "apply"], applied.Audit.Select(item => item.Action));
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!;
        Assert.Contains(persisted.Project.Dialogues, item => item.Id.ToString() == request.DialogueId);

        var reverted = workspace.Project.RevertDialoguePlan(planned.PlanId!);

        Assert.True(reverted.Succeeded);
        Assert.True(reverted.Reverted);
        Assert.Equal(2, workspace.Project.GetCurrentRevision());
        Assert.Equal(["planned", "apply", "revert"], reverted.Audit.Select(item => item.Action));
        persisted = ProjectSession.Open(workspace.RootDirectory).Session!;
        Assert.DoesNotContain(persisted.Project.Dialogues, item => item.Id.ToString() == request.DialogueId);
    }

    [Fact]
    public void DialoguePlanRejectsInvalidGraphBeforeMutation()
    {
        using var workspace = new TestWorkspace();
        var request = CreatePlan(workspace.RootDirectory);
        var unreachableId = Guid.Parse("d0e3a712-f41e-4695-ac1a-3db62c01d543").ToString("D");
        request = request with
        {
            Nodes =
            [
                .. request.Nodes,
                new DialogueNodeInput(unreachableId, null, "dialogue.unreachable", "Unreachable"),
            ],
        };

        var result = workspace.Project.PlanDialogue(request);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, item => item.Code == DialogueValidator.UnreachableNodeCode);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    [Fact]
    public void ScriptBindingPlanValidatesRevisionEntityBindingAndSafePath()
    {
        using var workspace = new TestWorkspace();
        var session = ProjectSession.Open(workspace.RootDirectory).Session!;
        var area = Assert.Single(session.Project.Areas);
        var entity = area.Entities.First();
        var request = new ScriptBindingPlanRequest(
            1,
            0,
            area.Id.ToString(),
            entity.Id.ToString(),
            "scriptPath",
            "scripts/interaction.forge");

        var planned = workspace.Project.PlanScriptBinding(request);
        var unsafePlan = workspace.Project.PlanScriptBinding(request with { RelativeScriptPath = "../outside.forge" });

        Assert.True(planned.Succeeded);
        Assert.Equal("replace", planned.Diff?.Operation);
        Assert.False(unsafePlan.Succeeded);
        Assert.Equal("script_binding.path_unsafe", Assert.Single(unsafePlan.Diagnostics).Code);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    private static DialoguePlanRequest CreatePlan(string workspaceDirectory)
    {
        var project = ProjectSession.Open(workspaceDirectory).Session!.Project;
        var speaker = project.Blueprints.First().Id.ToString();
        var dialogueId = Guid.Parse("7b02e466-b43a-442b-a833-8613a1c71951").ToString("D");
        var nodeId = Guid.Parse("659eb07b-5d3f-4f87-80de-26ec4b20c837").ToString("D");
        return new DialoguePlanRequest(
            1,
            0,
            dialogueId,
            "mcp-dialogue",
            "MCP dialogue",
            nodeId,
            [new DialogueNodeInput(nodeId, speaker, "dialogue.mcp", "A dry-run dialogue")]);
    }
}
