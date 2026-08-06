using Forge.Application.Assistant;
using Forge.Application.Commands;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;

namespace Forge.Application.Tests.Assistant;

public sealed class AssistedAreaPlanWorkflowTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 8, 5, 15, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(AssistedAreaArchetype.Crypt, "door")]
    [InlineData(AssistedAreaArchetype.Village, "path")]
    public async Task OfflineProviderProposesDeterministicConnectedPlanWithoutGeometry(
        AssistedAreaArchetype archetype,
        string traversal)
    {
        var project = Project();
        var brief = Brief(project, archetype);
        var profile = AssistantWorkspaceProfile.CreateDefault();
        var provider = new DeterministicAssistantProvider(profile.ActiveProvider);

        var first = await AssistedAreaPlanProposalService.ProposeAsync(
            brief, project, LoreIndexDefinition.CreateDefault(), provider, profile.ActiveProvider, false, false);
        var second = await AssistedAreaPlanProposalService.ProposeAsync(
            brief, project, LoreIndexDefinition.CreateDefault(), provider, profile.ActiveProvider, false, false);

        Assert.True(first.Succeeded);
        Assert.NotNull(first.Plan);
        Assert.Equal(8, first.Plan.Nodes.Count);
        Assert.Equal(first.Plan.Nodes.Select(item => (item.Id, item.Kind, item.X, item.Z)),
            second.Plan!.Nodes.Select(item => (item.Id, item.Kind, item.X, item.Z)));
        Assert.Contains(first.Plan.Links, item => item.Traversal.Contains(traversal, StringComparison.Ordinal));
        Assert.False(first.Plan.ApprovedForConstruction);
        Assert.Equal(0, first.EstimatedCostUsd);
        Assert.Contains("geometry-applied:false", first.SemanticDiff, StringComparison.Ordinal);
        Assert.Equal(64, first.PromptSha256.Length);
    }

    [Fact]
    public async Task CommandRequiresDryRunAndExplicitApprovalPermissionThenSupportsHistory()
    {
        var project = Project();
        var profile = AssistantWorkspaceProfile.CreateDefault();
        var proposal = await AssistedAreaPlanProposalService.ProposeAsync(
            Brief(project, AssistedAreaArchetype.Crypt), project, LoreIndexDefinition.CreateDefault(),
            new DeterministicAssistantProvider(profile.ActiveProvider), profile.ActiveProvider, false, false);
        var plan = proposal.Plan!;
        var catalog = new AssistedAreaPlanCatalog(Guid.NewGuid(), 1, [plan], "test:g12-c");
        var history = new AssistedAreaPlanCommandHistory();
        var handler = new SetAssistedAreaPlansHandler(new FixedClock(Timestamp));
        var transaction = Guid.NewGuid();
        var previewPlan = Guid.NewGuid();
        SetAssistedAreaPlansCommand Command(bool dryRun, AssistedAreaPlanCatalog value, AssistantPermissionLevel permission) =>
            new(CommandId.From(Guid.NewGuid()), transaction, previewPlan, history.State.Revision, value, permission, dryRun);

        var refused = handler.Handle(history.State, Command(false, catalog, AssistantPermissionLevel.Configure), project);
        var preview = handler.Handle(history.State, Command(true, catalog, AssistantPermissionLevel.Configure), project);
        var applied = handler.Handle(history.State, Command(false, catalog, AssistantPermissionLevel.Configure), project);

        Assert.Equal(SetAssistedAreaPlansHandler.PlanNotPreviewedCode, refused.Diagnostics[0].Code);
        Assert.True(preview.Succeeded);
        Assert.True(applied.Succeeded);
        history.Record(applied);
        Assert.Single(history.State.Catalog.Plans);
        Assert.True(history.Undo());
        Assert.Empty(history.State.Catalog.Plans);
        Assert.True(history.Redo());

        var approved = new AssistedAreaPlanCatalog(catalog.Id, 1,
            [plan.WithApproval(true, "author-approved:g12-c")], "test:g12-c");
        var denied = handler.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), Guid.NewGuid(), Guid.NewGuid(), history.State.Revision,
                approved, AssistantPermissionLevel.Configure, true), project);
        Assert.Equal(SetAssistedAreaPlansHandler.PermissionDeniedCode, denied.Diagnostics[0].Code);
    }

    [Fact]
    public async Task MissingAreaBlueprintAndLoreReferencesHaveStableDiagnostics()
    {
        var project = Project();
        var proposal = AssistedAreaPlanGenerator.Generate(
            Brief(project, AssistedAreaArchetype.Crypt) with
            {
                AreaId = Guid.NewGuid(),
                KitBlueprintIds = [Guid.NewGuid()],
                LoreSourceIds = ["quest:00000000-0000-4000-8000-000000000099"],
            },
            project,
            "offline-ci",
            0,
            "test",
            new string('a', 64),
            1,
            1);

        Assert.False(proposal.Succeeded);
        Assert.Contains(proposal.Diagnostics, item => item.Code == AssistedAreaPlanProjectValidator.AreaMissingCode);
        Assert.Contains(proposal.Diagnostics, item => item.Code == AssistedAreaPlanProjectValidator.BlueprintMissingCode);
        Assert.Contains(proposal.Diagnostics, item => item.Code == AssistedAreaPlanProjectValidator.LoreSourceMissingCode);
        await Task.CompletedTask;
    }

    private static AssistedAreaPlanBrief Brief(ProjectDefinition project, AssistedAreaArchetype archetype) => new(
        Guid.Parse("b1240000-0000-4000-8000-000000000010"),
        project.Areas[0].Id.Value,
        archetype == AssistedAreaArchetype.Crypt ? "Crypte du Serment" : "Village des Braises",
        archetype,
        20260805,
        64,
        64,
        8,
        [project.Blueprints[0].Id.Value],
        ["Preserve a navigable route from entry to exit."],
        ["Reach the central objective and return safely."],
        [$"project:{project.Id}"],
        "test:g12-c");

    private static ProjectDefinition Project()
    {
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("b1240000-0000-4000-8000-000000000020")),
            "plan-test",
            "Zone de plan",
            [],
            AreaKind.Exterior,
            new AreaDimensions(96, 96));
        var blueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("b1240000-0000-4000-8000-000000000021")),
            "kit-stone",
            "Kit pierre",
            BlueprintKind.Placeable);
        return new ProjectDefinition(
            ProjectId.From(Guid.Parse("b1240000-0000-4000-8000-000000000022")),
            "Projet de plan",
            [blueprint],
            [area]);
    }
}
