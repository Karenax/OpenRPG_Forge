using System.Text.Json;
using Forge.Application.Assistant;
using Forge.Application.Assets;
using Forge.Application.Validation;
using Forge.Domain.Assets;
using Forge.Domain.Areas;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Gameplay;
using Forge.Domain.Projects;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Rules;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.Mcp.Dialogues;
using Forge.Mcp.Assets;
using Forge.Mcp.Projects;
using Forge.Mcp.Scripts;
using Forge.ModuleFormat.Workspaces;
using Forge.ModuleFormat.Assets;
using Forge.RpgKit.Application.Dialogues;

namespace Forge.Mcp.Tests.Projects;

public sealed class ForgeProjectAgentServiceTests
{
    [Fact]
    public void AssistedDialogueMcpProposesPlansPersistsAndRevertsCanonicalGraph()
    {
        using var workspace = new TestWorkspace();
        var project = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var speaker = project.Blueprints.First(item => item.Kind == BlueprintKind.Creature);
        var dialoguesBefore = project.Dialogues.Count;
        var brief = new AssistedDialogueBrief(
            Guid.Parse("b1260000-0000-4000-8000-000000000090"),
            "dialogue-mcp-assiste",
            "Dialogue MCP assisté",
            speaker.Id.Value,
            "Le gardien révèle un secret puis demande une décision.",
            ["Présenter le secret", "Expliquer le risque", "Demander un choix", "Conclure"],
            ["style:crypt"],
            ["lore:crypt-of-ashes"],
            "fr",
            "dialogue.mcp-assiste",
            "@mcp-author",
            20260805,
            4);

        var proposed = workspace.Project.ProposeAssistedDialogue(new(1, brief));
        var approved = proposed.Proposal!.WithApproval(true, "author-approved:mcp-test:g12-e");
        var planned = workspace.Project.PlanAssistedDialogue(new(
            1,
            workspace.Project.GetCurrentRevision(),
            approved,
            AssistantPermissionLevel.ApplyApprovedPlan));
        var applied = workspace.Project.ApplyAssistedDialoguePlan(planned.PlanId!);
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var reverted = workspace.Project.RevertAssistedDialoguePlan(planned.PlanId!);
        var restored = ProjectSession.Open(workspace.RootDirectory).Session!.Project;

        Assert.True(proposed.Succeeded);
        Assert.True(proposed.Proposal.CanApply);
        Assert.Equal(4, proposed.Proposal.Dialogue.Nodes.Count);
        Assert.Single(proposed.Proposal.Citations);
        Assert.Empty(proposed.Proposal.Lint);
        Assert.True(planned.Succeeded);
        Assert.False(planned.Applied);
        Assert.NotNull(planned.Diff);
        Assert.Equal(4, planned.LocalizationPlaceholders.Count);
        Assert.True(applied.Succeeded);
        Assert.True(applied.Applied);
        Assert.Equal(dialoguesBefore + 1, persisted.Dialogues.Count);
        Assert.True(reverted.Succeeded);
        Assert.True(reverted.Reverted);
        Assert.Equal(dialoguesBefore, restored.Dialogues.Count);
    }

    [Fact]
    public void AssistedAreaBuildMcpRequiresApprovalAndPersistsAtomicPlacementsWithRevert()
    {
        using var workspace = new TestWorkspace();
        var project = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var area = project.Areas[0];
        var blueprint = project.Blueprints.First(item => item.Kind == BlueprintKind.Placeable);
        var entitiesBefore = project.Areas.Sum(item => item.Entities.Count);
        var brief = new AssistedAreaPlanBrief(
            Guid.Parse("b1250000-0000-4000-8000-000000000090"),
            area.Id.Value,
            "Crypte MCP construite",
            AssistedAreaArchetype.Crypt,
            20260805,
            Math.Min(64, area.Dimensions.Width),
            Math.Min(64, area.Dimensions.Depth),
            8,
            [blueprint.Id.Value],
            ["Preserve a navigable route."],
            ["Reach the objective."],
            [$"project:{project.Id}"],
            "mcp-test:g12-d");

        var proposal = workspace.Project.ProposeAssistedAreaPlan(new(1, brief));
        var approved = proposal.Plan!.WithApproval(true, "author-approved:mcp-test:g12-d");
        var current = workspace.Project.GetAssistedAreaPlans();
        var catalog = new AssistedAreaPlanCatalog(current.Catalog.Id, 1, [approved], "mcp-test:g12-d");
        var catalogPlan = workspace.Project.PlanAssistedAreaPlans(new(
            1, current.Revision, catalog, AssistantPermissionLevel.ApplyApprovedPlan));
        Assert.True(workspace.Project.ApplyAssistedAreaPlan(catalogPlan.PlanId!).Succeeded);

        var buildPlan = workspace.Project.PlanAssistedAreaBuild(new(
            1,
            workspace.Project.GetCurrentRevision(),
            approved.Id.ToString("D"),
            AssistantPermissionLevel.ApplyApprovedPlan));
        var applied = workspace.Project.ApplyAssistedAreaBuild(buildPlan.PlanId!);
        var persistedCount = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas.Sum(item => item.Entities.Count);
        var reverted = workspace.Project.RevertAssistedAreaBuild(buildPlan.PlanId!);
        var revertedCount = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas.Sum(item => item.Entities.Count);

        Assert.True(buildPlan.Succeeded);
        Assert.NotEmpty(buildPlan.EntityIds);
        Assert.False(buildPlan.Applied);
        Assert.True(buildPlan.LightingPass);
        Assert.Contains("seed:preserved", buildPlan.SemanticDiff, StringComparison.Ordinal);
        Assert.True(applied.Succeeded);
        Assert.True(applied.Applied);
        Assert.Equal(entitiesBefore + buildPlan.EntityIds.Count, persistedCount);
        Assert.True(reverted.Succeeded);
        Assert.True(reverted.Reverted);
        Assert.Equal(entitiesBefore, revertedCount);
    }

    [Fact]
    public void AssistedAreaPlanMcpProposesPersistsAndRevertsWithoutGeometryMutation()
    {
        using var workspace = new TestWorkspace();
        var project = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var area = project.Areas[0];
        var blueprint = project.Blueprints[0];
        var entitiesBefore = area.Entities.Count;
        var brief = new AssistedAreaPlanBrief(
            Guid.Parse("b1240000-0000-4000-8000-000000000080"),
            area.Id.Value,
            "Crypte MCP",
            AssistedAreaArchetype.Crypt,
            20260805,
            Math.Min(64, area.Dimensions.Width),
            Math.Min(64, area.Dimensions.Depth),
            8,
            [blueprint.Id.Value],
            ["Preserve a navigable route."],
            ["Reach the objective."],
            [$"project:{project.Id}"],
            "mcp-test:g12-c");

        var proposal = workspace.Project.ProposeAssistedAreaPlan(new(1, brief));
        var current = workspace.Project.GetAssistedAreaPlans();
        var catalog = new AssistedAreaPlanCatalog(current.Catalog.Id, 1, [proposal.Plan!], "mcp-test:g12-c");
        var planned = workspace.Project.PlanAssistedAreaPlans(new(
            1, current.Revision, catalog, AssistantPermissionLevel.Configure));
        var applied = workspace.Project.ApplyAssistedAreaPlan(planned.PlanId!);
        var persisted = AssistedAreaPlanStore.Load(workspace.RootDirectory);
        var reverted = workspace.Project.RevertAssistedAreaPlan(planned.PlanId!);

        Assert.True(proposal.Succeeded);
        Assert.False(proposal.GeometryApplied);
        Assert.Equal(0, proposal.EstimatedCostUsd);
        Assert.Contains("geometry-applied:false", proposal.SemanticDiff, StringComparison.Ordinal);
        Assert.True(planned.Succeeded);
        Assert.Equal("assistant/area-plans", planned.Diff?.Path);
        Assert.True(applied.Applied);
        Assert.Single(persisted.Catalog.Plans);
        Assert.Equal(entitiesBefore, ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas[0].Entities.Count);
        Assert.True(reverted.Reverted);
        Assert.Empty(workspace.Project.GetAssistedAreaPlans().Catalog.Plans);
    }

    [Fact]
    public void LoreIndexMcpUsesReadPlanApplyQueryAndRevertContract()
    {
        using var workspace = new TestWorkspace();
        var summary = workspace.Project.GetSummary();
        var current = workspace.Project.GetLoreIndex();
        var entry = new LoreEntryDefinition(
            Guid.Parse("b1230000-0000-4000-8000-000000000070"),
            1,
            LoreEntryKind.WorldBible,
            "serment-braise",
            "Serment de braise",
            "Le serment protège la Crypte et relie ses gardiens.",
            [$"project:{summary.ProjectId}"],
            [],
            ["braise", "canon"],
            "mcp-test:g12-b");
        var configured = new LoreIndexDefinition(
            current.Index.Id,
            1,
            true,
            8_000,
            [entry],
            "mcp-test:g12-b");

        var planned = workspace.Project.PlanLoreIndex(new LoreIndexPlanRequest(
            1,
            current.Revision,
            configured,
            AssistantPermissionLevel.Configure));
        var applied = workspace.Project.ApplyLoreIndexPlan(planned.PlanId!);
        var context = workspace.Project.QueryLoreIndex("braise crypte");
        var reverted = workspace.Project.RevertLoreIndexPlan(planned.PlanId!);

        Assert.True(planned.Succeeded);
        Assert.False(planned.Applied);
        Assert.NotNull(planned.Diff);
        Assert.NotNull(planned.Inverse);
        Assert.True(Guid.TryParse(planned.TransactionId, out _));
        Assert.True(applied.Succeeded);
        Assert.True(applied.Applied);
        Assert.Single(context.SourceIds);
        Assert.Equal(1, context.EntryCount);
        Assert.Equal(64, context.ContentSha256.Length);
        Assert.Contains("serment", context.Content, StringComparison.Ordinal);
        Assert.True(reverted.Succeeded);
        Assert.True(reverted.Reverted);
        Assert.Empty(workspace.Project.GetLoreIndex().Index.Entries);
    }

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
    public void Project_health_is_structured_read_only_and_accepts_test_evidence()
    {
        using var workspace = new TestWorkspace();

        var report = workspace.Project.GetProjectHealth(testsPassed: true);

        Assert.Equal(ProjectHealthStatus.Healthy, report.Status);
        Assert.True(report.BuildAllowed);
        Assert.Equal(6, report.Domains.Count);
        Assert.Equal(64, report.Fingerprint.Length);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    [Fact]
    public void Quality_lab_is_deterministic_complete_and_read_only()
    {
        using var workspace = new TestWorkspace();

        var first = workspace.Project.RunQualityLab();
        var second = workspace.Project.RunQualityLab();

        Assert.True(first.Passed);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(64, first.Fingerprint.Length);
        Assert.NotEmpty(first.Localization.Coverage);
        Assert.NotEmpty(first.Localization.PseudoSnapshots);
        Assert.NotEmpty(first.Dialogues);
        Assert.NotEmpty(first.Quests);
        Assert.True(first.Encounters.TotalRuns >= 9);
        Assert.True(first.Scripts.Passed);
        Assert.Null(first.PseudoLocalizationSnapshotPath);
        Assert.False(File.Exists(Path.Combine(workspace.RootDirectory, ".forge", "quality", "pseudo-localization.svg")));
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    [Fact]
    public void Performance_lab_is_deterministic_complete_and_read_only()
    {
        using var workspace = new TestWorkspace();

        var first = workspace.Project.RunPerformanceLab();
        var second = workspace.Project.RunPerformanceLab();

        Assert.True(first.Passed);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(9, first.Network.Scenarios.Count);
        Assert.Equal(8, first.VisualReviews.Cases.Count);
        Assert.Equal(9, first.Benchmarks.Measurements.Count);
        Assert.Equal(9, first.Benchmarks.Budgets.Count);
        Assert.NotEmpty(first.TraceSpans);
        Assert.Null(first.PerformanceReportPath);
        Assert.Null(first.VisualManifestPath);
        Assert.Null(first.TracePath);
        Assert.False(Directory.Exists(Path.Combine(workspace.RootDirectory, ".forge", "quality")));
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    [Fact]
    public void Governance_lab_is_deterministic_complete_and_read_only()
    {
        using var workspace = new TestWorkspace();

        var first = workspace.Project.RunGovernanceLab();
        var second = workspace.Project.RunGovernanceLab();

        Assert.True(first.Passed);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.True(first.Accessibility.Passed);
        Assert.True(first.SecurityFuzz.Passed);
        Assert.Equal(112, first.SecurityFuzz.TotalCases);
        Assert.Equal(7, first.SecurityFuzz.Boundaries.Count);
        Assert.True(first.Dependencies.Passed);
        Assert.Null(first.ReportPath);
        Assert.False(File.Exists(Path.Combine(workspace.RootDirectory, ".forge", "quality", "governance-report.json")));
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
            Assert.False(string.IsNullOrWhiteSpace(item.Author));
            Assert.NotNull(item.InstalledPackIds);
        });
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    [Fact]
    public void AssetInstallPlanUsesCommandDryRunApplyPersistenceRevertAndLicenseAudit()
    {
        using var workspace = new TestWorkspace();
        var item = LoadCatalogItem("ambientcg:3DApple001");
        Assert.True(AssetPackageManifestStore.Save(
            workspace.RootDirectory,
            [CreateManifest(workspace.RootDirectory, item)]).Succeeded);

        var planned = workspace.Project.PlanAssetInstall(
            new AgentAssetInstallPlanRequest(1, 0, item.Id.ToString()));

        Assert.True(planned.Succeeded);
        Assert.Equal("add", planned.Diff?.Operation);
        Assert.Equal("CC0-1.0", planned.LicenseSpdx);
        Assert.Equal("planned", Assert.Single(planned.Audit).Action);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        Assert.DoesNotContain(ProjectSession.Open(workspace.RootDirectory).Session!.Project.Blueprints,
            blueprint => AssetBlueprintFactory.TryGetAssetId(blueprint, out var id) && id == item.Id);

        var applied = workspace.Project.ApplyAssetInstallPlan(planned.PlanId!);

        Assert.True(applied.Applied);
        Assert.Equal(1, workspace.Project.GetCurrentRevision());
        Assert.Contains(ProjectSession.Open(workspace.RootDirectory).Session!.Project.Blueprints,
            blueprint => AssetBlueprintFactory.TryGetAssetId(blueprint, out var id) && id == item.Id);

        var reverted = workspace.Project.RevertAssetInstallPlan(planned.PlanId!);

        Assert.True(reverted.Reverted);
        Assert.Equal(["planned", "apply", "revert"], reverted.Audit.Select(entry => entry.Action));
        Assert.All(reverted.Audit, entry => Assert.Equal("CC0-1.0", entry.LicenseSpdx));
        Assert.DoesNotContain(ProjectSession.Open(workspace.RootDirectory).Session!.Project.Blueprints,
            blueprint => AssetBlueprintFactory.TryGetAssetId(blueprint, out var id) && id == item.Id);
    }

    [Fact]
    public void AssetInstallPlanRejectsManifestWhoseLicenseDoesNotMatchTheCatalog()
    {
        using var workspace = new TestWorkspace();
        var item = LoadCatalogItem("ambientcg:3DApple001");
        Assert.True(AssetPackageManifestStore.Save(
            workspace.RootDirectory,
            [CreateManifest(workspace.RootDirectory, item, licenseSpdx: "MIT")]).Succeeded);

        var result = workspace.Project.PlanAssetInstall(
            new AgentAssetInstallPlanRequest(1, 0, item.Id.ToString()));

        Assert.False(result.Succeeded);
        Assert.Equal(InstallAssetBlueprintHandler.GovernanceInvalidCode, Assert.Single(result.Diagnostics).Code);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    [Fact]
    public void AssetInstallPlanRejectsMissingGovernedModelWithoutMutation()
    {
        using var workspace = new TestWorkspace();
        var item = LoadCatalogItem("ambientcg:3DApple001");
        var manifest = CreateManifest(workspace.RootDirectory, item);
        File.Delete(Path.Combine(workspace.RootDirectory, "assets", "imported", "mcp-plan", "model.glb"));
        Assert.True(AssetPackageManifestStore.Save(workspace.RootDirectory, [manifest]).Succeeded);

        var result = workspace.Project.PlanAssetInstall(
            new AgentAssetInstallPlanRequest(1, 0, item.Id.ToString()));

        Assert.False(result.Succeeded);
        Assert.Equal(GovernedAssetModelVerifier.FileMissingCode, Assert.Single(result.Diagnostics).Code);
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
    public void GameplayControlPlanSupportsReadDryRunApplyPersistenceAndRevert()
    {
        using var workspace = new TestWorkspace();
        var resource = workspace.Project.GetGameplayControls();
        var source = resource.Profile;
        var time = new TimeControlPolicy(source.TimeControl.Id, 1, true, true, .3f, true, 16, true,
            MultiplayerPausePolicy.Vote, .5f, ["session.player"], "mcp-test");
        var combat = new CombatModeProfile(source.CombatModes.Id, 1, "turn-based",
            source.CombatModes.Providers, "mcp-test");
        var profile = new ModuleGameplayControlProfile(source.Id, 1, source.Swimming, source.Climbing,
            source.RadialMenu, time, combat, "mcp-test");

        var planned = workspace.Project.PlanGameplayControls(
            new GameplayControlPlanRequest(1, resource.Revision, profile));

        Assert.True(planned.Succeeded);
        Assert.Equal("module/runtimePolicies/gameplay", planned.Diff?.Path);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        var applied = workspace.Project.ApplyGameplayControlPlan(planned.PlanId!);
        Assert.True(applied.Applied);
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Module.RuntimePolicies.Gameplay;
        Assert.Equal("turn-based", persisted.CombatModes.DefaultProviderId);
        Assert.Equal(.3f, persisted.TimeControl.SlowMotionScale);
        var reverted = workspace.Project.RevertGameplayControlPlan(planned.PlanId!);
        Assert.True(reverted.Reverted);
        Assert.Equal(["planned", "apply", "revert"], reverted.Audit.Select(item => item.Action));
    }

    [Fact]
    public void RulesPlanSupportsReadDryRunApplyPersistenceAndRevert()
    {
        using var workspace = new TestWorkspace();
        var resource = workspace.Project.GetRules();
        var source = resource.Module;
        var active = source.ActiveDifficulty;
        var difficulty = new DifficultyProfile(active.Id, 1, active.Key, "Tactique", 1.2, 1.4, 1.15,
            .9, .8, SaveRestrictionPolicy.CheckpointsOnly, FriendlyFireMode.PartyOnly,
            active.Accessibility, "mcp-test");
        var module = new ModuleRulesProfile(source.Id, 1, source.Progression, [difficulty], difficulty.Key,
            source.Mutators, "mcp-test");
        var combat = resource.Area.Combat;
        var area = new AreaRulesProfile(resource.Area.Id, 1, resource.Area.Perception,
            new AreaCombatPolicy(combat.Id, 1, AreaPvpMode.DuelsOnly, FriendlyFireMode.Disabled,
                true, true, true, true, "mcp-test"), "mcp-test");

        var planned = workspace.Project.PlanRules(new RulesProfilePlanRequest(1, resource.Revision,
            resource.AreaId, module, area));
        Assert.True(planned.Succeeded);
        Assert.Equal("rules", planned.Diff?.Path);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        var applied = workspace.Project.ApplyRulesPlan(planned.PlanId!);
        Assert.True(applied.Applied);
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        Assert.Equal(1.4, persisted.Module.RuntimePolicies.Rules.ActiveDifficulty.IncomingDamageMultiplier);
        Assert.Equal(AreaPvpMode.DuelsOnly, persisted.Areas[0].RuntimePolicies.Rules.Combat.PvpMode);
        var reverted = workspace.Project.RevertRulesPlan(planned.PlanId!);
        Assert.True(reverted.Reverted);
        Assert.Equal(["planned", "apply", "revert"], reverted.Audit.Select(item => item.Action));
    }

    [Fact]
    public void AreaExperiencePlanSupportsReadDryRunApplyPersistenceAndRevert()
    {
        using var workspace = new TestWorkspace();
        var resource = workspace.Project.GetAreaExperience();
        var source = resource.Profile;
        var cue = new AudioCueDefinition(Guid.NewGuid(), "ambient.owl", string.Empty, "Ambience", .8f, false,
            string.Empty, string.Empty, "1.0", "mcp-test");
        var profile = new AreaExperienceProfile(source.Id, 1, [cue], [], [], [], source.Loading, "mcp-test");

        var planned = workspace.Project.PlanAreaExperience(new AreaExperiencePlanRequest(1, resource.Revision,
            resource.AreaId, profile));
        Assert.True(planned.Succeeded);
        Assert.Contains("experience", planned.Diff?.Path);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        var applied = workspace.Project.ApplyAreaExperiencePlan(planned.PlanId!);
        Assert.True(applied.Applied);
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        Assert.Equal("ambient.owl", Assert.Single(persisted.Areas[0].RuntimePolicies.Experience.AudioCues).CueId);
        var reverted = workspace.Project.RevertAreaExperiencePlan(planned.PlanId!);
        Assert.True(reverted.Reverted);
        Assert.Equal(["planned", "apply", "revert"], reverted.Audit.Select(item => item.Action));
    }

    [Fact]
    public void RuntimeExtensionPlanSupportsFilteredReadDryRunApplyPersistenceAndRevert()
    {
        using var workspace = new TestWorkspace();
        var resource = workspace.Project.GetRuntimeExtensions();
        var source = resource.Profile;
        var rule = new DataDrivenRuleSchema(Guid.NewGuid(), 1, "casting.focus", RuleSchemaKind.Casting,
            "{\"type\":\"object\"}", "{\"resource\":\"focus\"}", true, "mcp-test");
        var profile = new ModuleRuntimeExtensionProfile(source.Id, 1, [rule], [], [], source.SnapshotPolicy,
            "mcp-test");

        var planned = workspace.Project.PlanRuntimeExtensions(new RuntimeExtensionPlanRequest(1,
            resource.Revision, profile));

        Assert.True(planned.Succeeded);
        Assert.Equal("module/runtimePolicies/extensions", planned.Diff?.Path);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        var applied = workspace.Project.ApplyRuntimeExtensionPlan(planned.PlanId!);
        Assert.True(applied.Applied);
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Module.RuntimePolicies.Extensions;
        Assert.Equal("casting.focus", Assert.Single(persisted.RuleSchemas).Key);
        var reverted = workspace.Project.RevertRuntimeExtensionPlan(planned.PlanId!);
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

    private static AssetCatalogItem LoadCatalogItem(string externalId)
    {
        var repositoryRoot = AssetCatalogLoader.FindRepositoryRoot(AppContext.BaseDirectory)
            ?? throw new DirectoryNotFoundException("Asset catalog repository root was not found.");
        var catalog = AssetCatalogLoader.Load(repositoryRoot).Catalog
            ?? throw new InvalidOperationException("Asset catalog did not load.");
        return catalog.Items.Single(item => item.ExternalId == externalId);
    }

    private static AssetPackageManifest CreateManifest(
        string workspaceRoot,
        AssetCatalogItem item,
        string? licenseSpdx = null)
    {
        var relativePath = "assets/imported/mcp-plan/model.glb";
        var absolutePath = Path.Combine(workspaceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        var content = "OpenRPG Forge governed MCP asset model"u8.ToArray();
        File.WriteAllBytes(absolutePath, content);
        var contentSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content))
            .ToLowerInvariant();
        var chunk = new AssetManifestChunk(
            Guid.Parse("a83e0000-0000-4000-8000-000000000001"),
            AssetChunkRole.Model,
            true,
            relativePath,
            contentSha256,
            content.Length,
            content.Length,
            AssetChunkCompression.None,
            [AssetTargetPlatform.Any]);
        return new AssetPackageManifest(
            AssetPackageManifest.CurrentSchemaVersion,
            AssetPackageManifestId.ForAsset(item.Id),
            item.Id,
            "mcp-test:asset-install",
            licenseSpdx ?? item.LicenseSpdx,
            item.ContentSha256,
            new Dictionary<AssetTargetPlatform, long> { [AssetTargetPlatform.Any] = 4096 },
            [chunk],
            null,
            null,
            null,
            null);
    }
}
