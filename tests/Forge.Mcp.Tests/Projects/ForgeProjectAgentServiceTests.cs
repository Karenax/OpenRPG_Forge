using System.Text.Json;
using Forge.Application.Assistant;
using Forge.Application.Assets;
using Forge.Application.Projects;
using Forge.Application.Quality;
using Forge.Application.Validation;
using Forge.Domain.Assets;
using Forge.Domain.Areas;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Factions;
using Forge.Domain.Combat;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Gameplay;
using Forge.Domain.Items;
using Forge.Domain.Lifecycle;
using Forge.Domain.Modules;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Quality;
using Forge.Domain.Quests;
using Forge.Domain.Rules;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.Domain.WorldFeatures;
using Forge.Mcp.Abilities;
using Forge.Mcp.Assistant;
using Forge.Mcp.Dialogues;
using Forge.Mcp.Bosses;
using Forge.Mcp.Quests;
using Forge.Mcp.Assets;
using Forge.Mcp.Projects;
using Forge.Mcp.Items;
using Forge.Mcp.Navigation;
using Forge.Mcp.Scripts;
using Forge.Mcp.WorldFeatures;
using Forge.ModuleFormat.Workspaces;
using Forge.ModuleFormat.Assets;
using Forge.ModuleFormat.Quality;
using Forge.RpgKit.Application.Dialogues;
using Forge.RpgKit.Application.Abilities;
using Forge.RpgKit.Application.Bosses;
using Forge.RpgKit.Application.Quests;
using Forge.Scripting;

namespace Forge.Mcp.Tests.Projects;

public sealed class ForgeProjectAgentServiceTests
{
    [Fact]
    public void EntityScaleResourceAndPlanAreFilteredDryRunAppliedPersistedAndReverted()
    {
        using var workspace = new TestWorkspace();
        var opened = ProjectSession.Open(workspace.RootDirectory).Session!;
        var area = opened.Project.Areas[0];
        var entity = area.Entities.First();
        var before = entity.Transform.Scale;
        var target = new Vector3Value(1.25f, 1.5f, 1.75f);

        var resource = workspace.Project.GetEntityScales(area.Id.ToString(), [entity.Id.ToString()]);
        var plan = workspace.Project.PlanEntityScale(new EntityScalePlanRequest(
            1, 0, area.Id.ToString(), [entity.Id.ToString()], target));

        Assert.Empty(resource.Diagnostics);
        Assert.Equal(before, Assert.Single(resource.Entities).Scale);
        Assert.True(plan.Succeeded, string.Join(" | ", plan.Diagnostics.Select(item => item.Message)));
        Assert.False(plan.Applied);
        Assert.Equal("replace", plan.Diff!.Operation);
        Assert.Equal(before, ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas[0]
            .Entities.Single(item => item.Id == entity.Id).Transform.Scale);

        var applied = workspace.Project.ApplyEntityScalePlan(plan.PlanId!);
        Assert.True(applied.Succeeded);
        Assert.True(applied.Applied);
        Assert.Equal(target, ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas[0]
            .Entities.Single(item => item.Id == entity.Id).Transform.Scale);

        var reverted = workspace.Project.RevertEntityScalePlan(plan.PlanId!);
        Assert.True(reverted.Succeeded);
        Assert.True(reverted.Reverted);
        Assert.Equal(before, ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas[0]
            .Entities.Single(item => item.Id == entity.Id).Transform.Scale);
        Assert.Equal(["planned", "apply", "revert"], reverted.Audit.Select(item => item.Operation));
    }

    [Fact]
    public void StructuredAgentResultIsValidatedAndOfflineReadinessNeedsNoAssistantFile()
    {
        using var workspace = new TestWorkspace();
        var planId = Guid.Parse("a2230000-0000-4000-8000-000000000001");
        var transactionId = Guid.Parse("a2230000-0000-4000-8000-000000000002");
        string Content(string tool = "") => JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["summary"] = "MCP preview",
            ["semanticDiff"] = Array.Empty<object>(),
            ["planId"] = planId,
            ["transactionId"] = transactionId,
            ["permission"] = AssistantPermissionLevel.Propose.ToString(),
            ["promptSha256"] = new string('a', 64),
            ["toolCalls"] = string.IsNullOrEmpty(tool)
                ? Array.Empty<object>()
                : new object[] { new { tool, targetObjectId = Guid.NewGuid(), arguments = new { command = "whoami" } } },
        });
        AgentStructuredResultValidationRequest Request(string content) => new(
            1, "assistant.context-summary", planId, transactionId, AssistantPermissionLevel.Propose,
            "offline-ci", "forge-deterministic-v1", content, 10, 10, 0, "test:r022");

        var exact = workspace.Project.ValidateAgentStructuredResult(Request(Content()));
        var fenced = workspace.Project.ValidateAgentStructuredResult(Request($"```json\n{Content()}\n```"));
        var hostile = workspace.Project.ValidateAgentStructuredResult(Request(Content("shell.execute")));
        var offline = workspace.Project.GetAgentOfflineReadiness();

        Assert.True(exact.Succeeded, string.Join(" | ", exact.Diagnostics.Select(item => item.Message)));
        Assert.True(fenced.Succeeded);
        Assert.True(fenced.Repaired);
        Assert.False(hostile.Succeeded);
        Assert.Contains(hostile.Diagnostics, item => item.Code == AgentResponseSafetyService.ToolDeniedCode);
        Assert.True(offline.Ready, string.Join(" | ", offline.Diagnostics.Select(item => item.Message)));
        Assert.True(offline.ProfileWasMissing);
        Assert.True(offline.ManualAuthoringAvailable);
        Assert.False(offline.NetworkRequired);
        Assert.False(offline.AiMetadataRequiredByBuild);
        Assert.False(offline.AiMetadataRequiredByRuntime);
    }

    [Fact]
    public void AgentTransactionIsPreviewedCommittedPersistedAuditedRedactedAndRolledBackAtomically()
    {
        using var workspace = new TestWorkspace();
        var summary = workspace.Project.GetSummary();
        var moduleId = Guid.Parse(summary.ModuleId);
        var planId = Guid.Parse("a2110000-0000-4000-8000-000000000001");
        var transactionId = Guid.Parse("a2110000-0000-4000-8000-000000000002");
        AgentSemanticCommand Variable(string id, string key, string value, string kind = "String") =>
            new(Guid.Parse(id), "project.set_variable", moduleId, new Dictionary<string, string>
            {
                ["key"] = key,
                ["kind"] = kind,
                ["scope"] = "Save",
                ["value"] = value,
                ["description"] = "Agent transaction test",
                ["requiredAtRuntime"] = "true",
            });
        var plan = new StructuredChangePlan(
            planId, transactionId, 1, "test:r021", 0, AssistantPermissionLevel.ApplyApprovedPlan,
            "Set two governed project variables",
            [
                Variable("a2110000-0000-4000-8000-000000000003", "campaign.chapter", "2", "Integer"),
                Variable("a2110000-0000-4000-8000-000000000004", "secret.token", "hunter2"),
            ],
            ["module selected"], [moduleId], ["state migration"], ["validate project", "reload workspace"]);
        var request = new AgentChangeTransactionBeginRequest(
            1, plan, "codex-test", "author:karen", null, null, false, true, 64, 32, 0, 1);
        var projectPath = Path.Combine(workspace.RootDirectory, "project.json");
        var beforeBytes = File.ReadAllBytes(projectPath);
        var originalKeys = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Module.Variables
            .Select(item => item.Key).ToArray();

        var begun = workspace.Project.BeginAgentChangeTransaction(request);
        var previewed = workspace.Project.PreviewAgentChangeTransaction(transactionId.ToString("D"));

        Assert.True(begun.Succeeded);
        Assert.Equal(AgentTransactionStage.Begun, begun.Stage);
        Assert.True(previewed.Succeeded,
            string.Join(" | ", previewed.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        Assert.Equal(AgentTransactionStage.Previewed, previewed.Stage);
        Assert.Equal(2, previewed.SemanticDiff!.Entries.Count);
        Assert.Contains(previewed.SemanticDiff.Entries,
            item => item.DocumentPath.EndsWith("secret.token", StringComparison.Ordinal) && item.AfterValue == "[REDACTED]");
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        Assert.Equal(beforeBytes, File.ReadAllBytes(projectPath));

        var committed = workspace.Project.CommitAgentChangeTransaction(transactionId.ToString("D"));

        Assert.True(committed.Succeeded);
        Assert.Equal(AgentTransactionStage.Committed, committed.Stage);
        Assert.Equal(1, workspace.Project.GetCurrentRevision());
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Module.Variables;
        Assert.Equal(originalKeys.Append("campaign.chapter").Append("secret.token").Order(StringComparer.Ordinal),
            persisted.Select(item => item.Key));
        var auditText = File.ReadAllText(Path.Combine(workspace.RootDirectory, "assistant", "ai-audit.json"));
        Assert.DoesNotContain("hunter2", auditText, StringComparison.Ordinal);
        var audit = workspace.Project.GetAgentAiAudit();
        Assert.Equal([AgentTransactionStage.Begun, AgentTransactionStage.Previewed, AgentTransactionStage.Committed],
            audit.Entries.Select(item => item.Stage));
        Assert.All(audit.Entries, item => Assert.Equal(committed.RollbackId, item.RollbackId));
        Assert.All(audit.Entries, item => Assert.Equal(["project.set_variable", "project.set_variable"], item.Commands));
        var reopenedAudit = ForgeProjectAgentService.Open(workspace.RootDirectory).Service!.GetAgentAiAudit();
        Assert.Equal(3, reopenedAudit.Entries.Count);
        Assert.Empty(reopenedAudit.Diagnostics);

        var rolledBack = workspace.Project.RollbackAgentChangeTransaction(transactionId.ToString("D"));

        Assert.True(rolledBack.Succeeded);
        Assert.Equal(AgentTransactionStage.RolledBack, rolledBack.Stage);
        Assert.Equal(2, workspace.Project.GetCurrentRevision());
        Assert.Equal(originalKeys,
            ProjectSession.Open(workspace.RootDirectory).Session!.Project.Module.Variables.Select(item => item.Key));
        Assert.Equal(4, workspace.Project.GetAgentAiAudit().Entries.Count);
    }

    [Fact]
    public void AgentTransactionRejectsMissingConfirmationBudgetAndInvalidBatchWithoutPartialState()
    {
        using var workspace = new TestWorkspace();
        var moduleId = Guid.Parse(workspace.Project.GetSummary().ModuleId);
        StructuredChangePlan Plan(Guid transactionId, string kind = "Integer") => new(
            Guid.NewGuid(), transactionId, 1, "test:r021", 0, AssistantPermissionLevel.ApplyApprovedPlan,
            "Set a variable",
            [new AgentSemanticCommand(Guid.NewGuid(), "project.set_variable", moduleId,
                new Dictionary<string, string>
                {
                    ["key"] = "campaign.invalid",
                    ["kind"] = kind,
                    ["scope"] = "Save",
                    ["value"] = "1",
                })],
            ["module selected"], [moduleId], ["invalid type"], ["validate project"]);

        var unconfirmed = workspace.Project.BeginAgentChangeTransaction(new(
            1, Plan(Guid.NewGuid()), "client", "user", null, null, false, false, 1, 1, 0, 1));
        var overBudget = workspace.Project.BeginAgentChangeTransaction(new(
            1, Plan(Guid.NewGuid()), "client", "user", null, null, false, true, 5000, 1, 0, 1));
        var hostilePlan = Plan(Guid.NewGuid(), "NotAType");
        var begun = workspace.Project.BeginAgentChangeTransaction(new(
            1, hostilePlan, "client", "user", null, null, false, true, 1, 1, 0, 1));
        var before = File.ReadAllBytes(Path.Combine(workspace.RootDirectory, "project.json"));
        var originalKeys = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Module.Variables
            .Select(item => item.Key).ToArray();
        var rejectedPreview = workspace.Project.PreviewAgentChangeTransaction(hostilePlan.TransactionId.ToString("D"));

        Assert.Contains(unconfirmed.Diagnostics, item => item.Code == AgentExecutionGovernanceValidator.PermissionDeniedCode);
        Assert.Contains(overBudget.Diagnostics, item => item.Code == AgentExecutionGovernanceValidator.BudgetExceededCode);
        Assert.True(begun.Succeeded);
        Assert.False(rejectedPreview.Succeeded);
        Assert.Equal(AgentTransactionStage.Rejected, rejectedPreview.Stage);
        Assert.Contains(rejectedPreview.Diagnostics, item => item.Code == "assistant.transaction.variable_arguments_invalid");
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        Assert.Equal(originalKeys,
            ProjectSession.Open(workspace.RootDirectory).Session!.Project.Module.Variables.Select(item => item.Key));
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(workspace.RootDirectory, "project.json")));
    }

    [Fact]
    public void AgentSemanticWorkflowProvidesBoundedContextImpactAndNoWritePlanPreview()
    {
        using var workspace = new TestWorkspace();
        var scripts = Path.Combine(workspace.RootDirectory, "scripts");
        Directory.CreateDirectory(scripts);
        File.WriteAllText(Path.Combine(scripts, "curated.forge"), "void main() {}\n");
        File.WriteAllText(Path.Combine(scripts, "ignored.txt"), "must not be ingested\n");
        var summary = workspace.Project.GetSummary();
        var areaId = Guid.Parse(summary.EntryAreaId);
        var projectPath = Path.Combine(workspace.RootDirectory, "project.json");
        var beforeBytes = File.ReadAllBytes(projectPath);

        var context = workspace.Project.GetStructuredAgentContext(new AgentContextQuery(200, [areaId.ToString("D")]));
        var graph = workspace.Project.QueryAgentReferenceGraph(areaId.ToString("D"), 2, 100);
        var tools = workspace.Project.GetSemanticToolCatalog();
        var plan = new StructuredChangePlan(
            Guid.Parse("a2020000-0000-4000-8000-000000000001"),
            Guid.Parse("a2020000-0000-4000-8000-000000000002"), 1, "test:r020", 0,
            AssistantPermissionLevel.ApplyApprovedPlan, "Place a creature",
            [new AgentSemanticCommand(Guid.Parse("a2020000-0000-4000-8000-000000000003"),
                "area.add_entity", areaId, new Dictionary<string, string> { ["blueprint"] = "warden" })],
            ["entry area selected"], [areaId], ["placement collision"], ["validate project"]);
        var preview = workspace.Project.PreviewAgentChangePlan(new AgentChangePlanPreviewRequest(1, plan));

        Assert.Equal(Enum.GetValues<AgentContextResourceKind>(), context.Sections.Select(item => item.Kind));
        Assert.InRange(context.Total, 1, 200);
        Assert.Single(context.Sections.Single(item => item.Kind == AgentContextResourceKind.Scripts).Items);
        Assert.Single(context.Sections.Single(item => item.Kind == AgentContextResourceKind.Selection).Items);
        Assert.Contains(graph.Nodes, item => item.Id == areaId);
        Assert.Equal(5, tools.Tools.Count);
        Assert.True(preview.Succeeded);
        Assert.True(preview.DryRun);
        Assert.Equal(preview.ProjectFingerprintBefore, preview.ProjectFingerprintAfter);
        Assert.Single(preview.SemanticDiff);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        Assert.Equal(beforeBytes, File.ReadAllBytes(projectPath));

        var stale = workspace.Project.PreviewAgentChangePlan(new AgentChangePlanPreviewRequest(1,
            plan with { BaseRevision = 2 }));
        var hostile = workspace.Project.PreviewAgentChangePlan(new AgentChangePlanPreviewRequest(1,
            plan with { Commands = [plan.Commands[0] with { ToolName = "shell.execute", TargetObjectId = Guid.NewGuid() }] }));
        var missingSelection = workspace.Project.GetStructuredAgentContext(new AgentContextQuery(20, [Guid.NewGuid().ToString()]));
        Assert.Contains(stale.Diagnostics, item => item.Code == "assistant.change_plan.revision_conflict");
        Assert.Contains(hostile.Diagnostics, item => item.Code == AgentSemanticWorkflowValidator.ChangePlanToolUnknownCode);
        Assert.Contains(hostile.Diagnostics, item => item.Code == "assistant.change_plan.target_missing");
        Assert.Contains(missingSelection.Diagnostics, item => item.Code == "assistant.context.selection_missing");
        Assert.Equal(beforeBytes, File.ReadAllBytes(projectPath));
    }

    [Fact]
    public void Anchor_resource_is_filterable_bounded_and_preview_remains_dry_run()
    {
        AnchorCatalog? catalog = null;
        string? areaId = null;
        using var workspace = new TestWorkspace(project =>
        {
            var area = project.Areas[0];
            areaId = area.Id.ToString();
            var definition = new AnchorDefinition(Guid.Parse("a9030000-0000-4000-8000-000000000001"), 1,
                "test:r009", "ember-waypoint", new LocalizedText("anchor.ember", "Ember waypoint"),
                AnchorKind.Waypoint, AnchorScope.Area,
                ["ember", "quest"], null, null, false);
            var instance = new AnchorInstance(Guid.Parse("a9030000-0000-4000-8000-000000000002"), 1,
                "test:r009", definition.Id, TransformValue.Identity, 1, EntityPropertyOverrides.Empty, null, null);
            catalog = new AnchorCatalog(Guid.Parse("a9030000-0000-4000-8000-000000000003"), 1,
                "test:r009", area.Id, false, [definition], [instance]);
            var changed = area.WithNavigation(area.Navigation.WithAnchors(catalog));
            return new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
                project.Areas.Select(item => item.Id == area.Id ? changed : item).ToArray(),
                project.Dialogues, project.Quests);
        });

        var resource = workspace.Project.QueryAnchors("ember", 1);
        var preview = workspace.Project.PreviewAnchors(new AnchorPlanRequest(1, 0, areaId!, catalog!));

        var anchor = Assert.Single(resource.Items);
        Assert.Equal("ember-waypoint", anchor.Slug);
        Assert.Equal("Waypoint", anchor.Kind);
        Assert.True(preview.Succeeded);
        Assert.Contains("anchor instance", preview.SemanticDiff!.Summary, StringComparison.Ordinal);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        Assert.Throws<ArgumentOutOfRangeException>(() => workspace.Project.QueryAnchors(limit: 501));
        Assert.False(workspace.Project.PreviewAnchors(new AnchorPlanRequest(2, 0, areaId!, catalog!)).Succeeded);
    }

    [Fact]
    public void R064_waypoint_resource_is_filtered_and_mutation_preview_is_validated_and_read_only()
    {
        WaypointAuthoringProfile? profile = null;
        string? areaId = null;
        using var workspace = new TestWorkspace(project =>
        {
            var area = project.Areas[0];
            areaId = area.Id.ToString();
            var waypoints = new[]
            {
                new WaypointProfile(Guid.Parse("a0647000-0000-4000-8000-000000000011"), 1,
                    "test:r064", "Braise", new(0, 0, 0),
                    new("flag", "#ff8844", "quest", true, 28),
                    new("Braise révélée", "quest", 0, "quest", 2,
                        MarkerVisibilityPolicy.HiddenUntilRevealed, MarkerVisibilityScope.Party,
                        true, false, "quest.crypt.found", "pulse", true), RequiredAtRuntime: true),
                new WaypointProfile(Guid.Parse("a0647000-0000-4000-8000-000000000012"), 1,
                    "test:r064", "Porte", new(2, 0, 0),
                    new("arrow", "#83d6ae", "patrol", true, 24), null, RequiredAtRuntime: true),
            };
            var points = waypoints.Select(item => new PatrolPoint(item.Id, item.Position, 0, 0, 2,
                null, null)).ToArray();
            profile = new WaypointAuthoringProfile(Guid.Parse("a0647000-0000-4000-8000-000000000020"),
                1, "test:r064", area.Id, true, waypoints,
                [new(Guid.Parse("a0647000-0000-4000-8000-000000000021"), 1, "test:r064",
                    PatrolTraversalPolicy.PingPong, points, requiredAtRuntime: true)]);
            var changed = area.WithNavigation(area.Navigation.WithWaypoints(profile));
            return new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
                project.Areas.Select(item => item.Id == area.Id ? changed : item).ToArray(),
                project.Dialogues, project.Quests);
        });

        var resource = workspace.Project.QueryWaypoints("Braise", 10);
        var preview = workspace.Project.PreviewWaypointAuthoring(
            new WaypointAuthoringPlanRequest(1, 0, areaId!, profile!));
        var invalid = workspace.Project.PreviewWaypointAuthoring(
            new WaypointAuthoringPlanRequest(2, 0, areaId!, profile!));

        Assert.Single(resource.Waypoints);
        Assert.Single(resource.PatrolRoutes);
        Assert.Equal("HiddenUntilRevealed", resource.Waypoints[0].VisibilityPolicy);
        Assert.True(preview.Succeeded);
        Assert.Contains("waypoint", preview.SemanticDiff!.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.False(invalid.Succeeded);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        Assert.Throws<ArgumentOutOfRangeException>(() => workspace.Project.QueryWaypoints(limit: 501));
    }

    [Fact]
    public void R065_waypoint_resource_filters_locators_and_exposes_collections_arrival_and_route_diagnostics()
    {
        WaypointAuthoringProfile? profile = null;
        string? areaId = null;
        using var workspace = new TestWorkspace(project =>
        {
            var area = project.Areas[0];
            areaId = area.Id.ToString();
            var waypoints = new[]
            {
                new WaypointProfile(Guid.Parse("a0657000-0000-4000-8000-000000000011"), 1,
                    "test:r065", "Entrée", new(-3, 0, 0),
                    new("flag", "#ff8844", "quest", true, 28), null,
                    Arrival: new(ArrivalFacingMode.Target, 0, "gate", "inspect", 1, "open",
                        "waypoint.arrived")),
                new WaypointProfile(Guid.Parse("a0657000-0000-4000-8000-000000000012"), 1,
                    "test:r065", "Porte", new(3, 0, 0),
                    new("arrow", "#83d6ae", "patrol", true, 26), null),
            };
            var route = new PatrolRoute(Guid.Parse("a0657000-0000-4000-8000-000000000020"),
                1, "test:r065", PatrolTraversalPolicy.PingPong,
                [new(waypoints[0].Id, waypoints[0].Position, 0, 0, 2, "inspect", null),
                    new(waypoints[1].Id, waypoints[1].Position, 1, 180, 2, null, null)]);
            var locators = new[]
            {
                new GenericLocator(Guid.Parse("a0657000-0000-4000-8000-000000000031"), 1,
                    "test:r065", "Cloche", new(0, 0, 2), LocatorPurpose.Audio,
                    ["crypt", "ritual"], null, "audio:bell"),
                new GenericLocator(Guid.Parse("a0657000-0000-4000-8000-000000000032"), 1,
                    "test:r065", "Brasero", new(1, 0, 2), LocatorPurpose.Vfx,
                    ["crypt", "fire"], null, "vfx:ember"),
            };
            var collection = new AnchorCollection(
                Guid.Parse("a0657000-0000-4000-8000-000000000040"), 1, "test:r065", "crypt",
                locators.Select(item => item.Id).ToArray(), "Crypte",
                RequiredTags: ["crypt"]);
            var visualization = new RouteVisualizationProfile(
                Guid.Parse("a0657000-0000-4000-8000-000000000050"), 1, "test:r065",
                true, true, true, true, true, true, true, 1.25);
            profile = new(Guid.Parse("a0657000-0000-4000-8000-000000000060"), 1,
                "test:r065", area.Id, true, waypoints, [route], locators, [collection], visualization);
            var changed = area.WithNavigation(area.Navigation.WithWaypoints(profile));
            return new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
                project.Areas.Select(item => item.Id == area.Id ? changed : item).ToArray(),
                project.Dialogues, project.Quests);
        });

        var all = workspace.Project.QueryWaypoints(areaId, 20);
        var filtered = workspace.Project.QueryWaypoints("Cloche", 20);
        var preview = workspace.Project.PreviewWaypointAuthoring(
            new WaypointAuthoringPlanRequest(1, 0, areaId!, profile!));

        Assert.Equal(2, all.Locators.Count);
        Assert.Single(all.Collections);
        Assert.Single(all.RouteVisualizations);
        Assert.Equal(2, all.PatrolRoutes.Single().Segments.Count);
        Assert.Equal("Target", all.Waypoints.Single(item => item.ArrivalFacing is not null).ArrivalFacing);
        Assert.Equal("Cloche", Assert.Single(filtered.Locators).Name);
        Assert.Single(filtered.Collections);
        Assert.True(preview.Succeeded);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    [Fact]
    public void Post_launch_resource_is_filterable_and_previews_are_read_only()
    {
        using var workspace = new TestWorkspace();

        var network = workspace.Project.GetPostLaunchCapabilities("network");
        var protocol = workspace.Project.PreviewPostLaunchCapability(new PostLaunchPreviewRequest(1, "protocol",
            new Dictionary<string, string>
            {
                ["platform"] = "windows",
                ["minimumVersion"] = "1",
                ["maximumVersion"] = "1",
            }));
        var invalid = workspace.Project.PreviewPostLaunchCapability(new PostLaunchPreviewRequest(2, "soak",
            new Dictionary<string, string>()));

        Assert.Equal("network", network.Section);
        Assert.Contains("protocol=openrpg-net", network.Summary, StringComparison.Ordinal);
        Assert.True(protocol.Succeeded);
        Assert.Equal("POST_PROTOCOL_COMPATIBLE", protocol.Code);
        Assert.False(invalid.Succeeded);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        Assert.Throws<ArgumentOutOfRangeException>(() => workspace.Project.GetPostLaunchCapabilities("unknown"));
    }

    [Fact]
    public void World_feature_resource_is_filtered_bounded_and_mutation_remains_dry_run()
    {
        using var workspace = new TestWorkspace();
        var catalog = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Module.WorldFeatures;

        var resource = workspace.Project.QueryWorldFeatures("script", limit: 1);
        var planned = workspace.Project.PreviewWorldFeatures(new WorldFeaturePlanRequest(1, 0, catalog));

        var script = Assert.Single(resource.Items);
        Assert.Equal("script", script.Kind);
        Assert.Equal(catalog.Scripts.Id.ToString("D"), script.Id);
        Assert.True(planned.Succeeded);
        Assert.Equal("module/worldFeatures", planned.SemanticDiff!.Path);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        Assert.Throws<ArgumentOutOfRangeException>(() => workspace.Project.QueryWorldFeatures(limit: 501));
        Assert.False(workspace.Project.PreviewWorldFeatures(new WorldFeaturePlanRequest(2, 0, catalog)).Succeeded);
    }

    [Fact]
    public void Door_access_event_resource_is_typed_filtered_searchable_and_plan_remains_dry_run()
    {
        using var workspace = new TestWorkspace(project =>
        {
            var catalog = CryptWorldFeatureAuthoringService.Create(project);
            return new ProjectDefinition(project.Id, project.Title, project.Module.WithWorldFeatures(catalog),
                project.Blueprints, project.Areas, project.Dialogues, project.Quests);
        });
        var canonical = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var door = canonical.Module.WorldFeatures.Doors.Single();

        var resource = workspace.Project.QueryWorldFeatures("door", door.Id.ToString("D"), 1);
        var searched = workspace.Project.QueryWorldFeatures("door", "damage:ember-burst", 1);
        var lockEventSearch = workspace.Project.QueryWorldFeatures("door", "DoorUnlocked", 1);
        var planned = workspace.Project.PreviewWorldFeatures(new WorldFeaturePlanRequest(
            1, 0, canonical.Module.WorldFeatures));

        var item = Assert.Single(resource.Items);
        Assert.Equal("True", item.Properties["accessEvents"]);
        Assert.Equal("Locked", item.Properties["lockState"]);
        Assert.Equal("1", item.Properties["credentials"]);
        Assert.Equal("True", item.Properties["consumeCredential"]);
        Assert.Equal("2", item.Properties["rejectionLocales"]);
        Assert.Equal("damage:ember-burst", item.Properties["trapPayload"]);
        Assert.Equal("PortalUseRequested", item.Properties["portalRequested"]);
        Assert.Equal("PortalUseCompleted", item.Properties["portalCompleted"]);
        Assert.Equal("DoorOpened", item.Properties["doorOpened"]);
        Assert.Equal("DoorClosed", item.Properties["doorClosed"]);
        Assert.Equal("DoorLocked", item.Properties["doorLocked"]);
        Assert.Equal("DoorUnlocked", item.Properties["doorUnlocked"]);
        Assert.Equal("True", item.Properties["lockEventsRequiredAtRuntime"]);
        Assert.Equal("True", item.Properties["accessEventsRequiredAtRuntime"]);
        Assert.Single(searched.Items);
        Assert.Single(lockEventSearch.Items);
        Assert.True(planned.Succeeded);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    [Fact]
    public void Ux1Aj_DoorValidationContextIsProjectedAndMcpPreviewCannotBypassCanonicalValidation()
    {
        using var workspace = new TestWorkspace(project =>
        {
            var source = CryptWorldFeatureAuthoringService.Create(project);
            var door = source.Doors.Single();
            var foundation = door.Foundation!;
            var access = door.AccessEvents!;
            var validation = new DoorWorldProfile.RuntimeValidationPolicy(
                foundation.Portal is null ? [] : [foundation.Portal.DestinationId],
                foundation.States.Select(item => item.AnimationId).ToArray(), ["collision:crypt-door"],
                access.Lock.CredentialIds,
                door.Events.Select(item => item.ScriptId).Append(access.Rejection.ScriptId).ToArray());
            var configuredDoor = door with { Validation = validation, ValidationRequiredAtRuntime = true };
            var catalog = WithDoors(source, [configuredDoor]);
            return new ProjectDefinition(project.Id, project.Title, project.Module.WithWorldFeatures(catalog),
                project.Blueprints, project.Areas, project.Dialogues, project.Quests);
        });
        var canonical = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var door = canonical.Module.WorldFeatures.Doors.Single();

        var resource = workspace.Project.QueryWorldFeatures("door", door.Id.ToString("D"), 1);
        var invalidDoor = door with { Validation = new([], [], [], [], []) };
        var invalidCatalog = WithDoors(canonical.Module.WorldFeatures, [invalidDoor]);
        var planned = workspace.Project.PreviewWorldFeatures(new WorldFeaturePlanRequest(1, 0, invalidCatalog));

        var item = Assert.Single(resource.Items);
        Assert.Equal("True", item.Properties["validation"]);
        Assert.Equal("True", item.Properties["validationRequiredAtRuntime"]);
        Assert.Contains("animation:open", item.Properties["knownAnimations"], StringComparison.Ordinal);
        Assert.Equal("collision:crypt-door", item.Properties["knownCollisionProfiles"]);
        Assert.Contains("key:ember", item.Properties["knownCredentials"], StringComparison.Ordinal);
        Assert.Contains("scripts/door_open_rejected", item.Properties["knownScripts"], StringComparison.Ordinal);
        Assert.False(planned.Succeeded);
        Assert.Contains(planned.Diagnostics, diagnostic => diagnostic.Code == "RPG_DOOR_ANIMATION_MISSING" &&
            diagnostic.Path.StartsWith("module/worldFeatures/doors/", StringComparison.Ordinal));
        Assert.Contains(planned.Diagnostics, diagnostic => diagnostic.Code == "RPG_DOOR_COLLISION_PROFILE_MISSING");
        Assert.Contains(planned.Diagnostics, diagnostic => diagnostic.Code == "RPG_DOOR_SCRIPT_MISSING");
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    [Fact]
    public void R083_Encounter_composition_resource_is_filtered_searchable_and_plan_remains_dry_run()
    {
        using var workspace = new TestWorkspace(project =>
        {
            var catalog = CryptWorldFeatureAuthoringService.Create(project);
            return new ProjectDefinition(project.Id, project.Title, project.Module.WithWorldFeatures(catalog),
                project.Blueprints, project.Areas, project.Dialogues, project.Quests);
        });
        var canonical = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var encounter = canonical.Module.WorldFeatures.Encounters.Single();

        var resource = workspace.Project.QueryWorldFeatures("encounter", encounter.Id.ToString("D"), 1);
        var searched = workspace.Project.QueryWorldFeatures("encounter", "party:forge-generic", 1);
        var planned = workspace.Project.PreviewWorldFeatures(new WorldFeaturePlanRequest(
            1, 0, canonical.Module.WorldFeatures));

        var item = Assert.Single(resource.Items);
        Assert.Equal("True", item.Properties["composition"]);
        Assert.Equal("crypt-warden-ambush", item.Properties["wizard"]);
        Assert.Equal("Hard", item.Properties["difficulty"]);
        Assert.Equal("party:forge-generic", item.Properties["partyProvider"]);
        Assert.Equal("1", item.Properties["groups"]);
        Assert.Equal("PopulationMaintained", item.Properties["activation"]);
        Assert.Equal("True", item.Properties["compositionRequiredAtRuntime"]);
        Assert.Single(searched.Items);
        Assert.True(planned.Succeeded);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    [Fact]
    public void R084_Encounter_scenario_resource_is_filtered_searchable_and_plan_remains_dry_run()
    {
        using var workspace = new TestWorkspace(project =>
        {
            var catalog = CryptWorldFeatureAuthoringService.Create(project);
            return new ProjectDefinition(project.Id, project.Title, project.Module.WithWorldFeatures(catalog),
                project.Blueprints, project.Areas, project.Dialogues, project.Quests);
        });
        var canonical = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var encounter = canonical.Module.WorldFeatures.Encounters.Single();

        var resource = workspace.Project.QueryWorldFeatures("encounter", encounter.Id.ToString("D"), 1);
        var lifecycleSearch = workspace.Project.QueryWorldFeatures("encounter", "EncounterExhausted", 1);
        var authoritySearch = workspace.Project.QueryWorldFeatures("encounter", "Server", 1);
        var planned = workspace.Project.PreviewWorldFeatures(new WorldFeaturePlanRequest(
            1, 0, canonical.Module.WorldFeatures));

        var item = Assert.Single(resource.Items);
        Assert.Equal("True", item.Properties["scenario"]);
        Assert.Equal("1", item.Properties["spawnAnchors"]);
        Assert.Equal("Weighted", item.Properties["distribution"]);
        Assert.Equal("Server", item.Properties["activatorAuthority"]);
        Assert.Equal("EncounterEntered", item.Properties["encounterEntered"]);
        Assert.Equal("EncounterExited", item.Properties["encounterExited"]);
        Assert.Equal("EncounterExhausted", item.Properties["encounterExhausted"]);
        Assert.Equal("1", item.Properties["phases"]);
        Assert.Equal("2", item.Properties["waves"]);
        Assert.Equal("True", item.Properties["scenarioRequiredAtRuntime"]);
        Assert.Single(lifecycleSearch.Items);
        Assert.Single(authoritySearch.Items);
        Assert.True(planned.Succeeded);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    [Fact]
    public void R085_Encounter_statistics_resource_is_filtered_searchable_and_plan_remains_dry_run()
    {
        using var workspace = new TestWorkspace(project =>
        {
            var catalog = CryptWorldFeatureAuthoringService.Create(project);
            return new ProjectDefinition(project.Id, project.Title, project.Module.WithWorldFeatures(catalog),
                project.Blueprints, project.Areas, project.Dialogues, project.Quests);
        });
        var canonical = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var encounter = canonical.Module.WorldFeatures.Encounters.Single();

        var resource = workspace.Project.QueryWorldFeatures("encounter", encounter.Id.ToString("D"), 1);
        var searched = workspace.Project.QueryWorldFeatures("encounter", "duration:actors-linear", 1);
        var planned = workspace.Project.PreviewWorldFeatures(new WorldFeaturePlanRequest(
            1, 0, canonical.Module.WorldFeatures));

        var item = Assert.Single(resource.Items);
        Assert.Equal("True", item.Properties["statistics"]);
        Assert.Equal("64", item.Properties["statisticsRuns"]);
        Assert.Equal("840", item.Properties["statisticsSeed"]);
        Assert.Equal("duration:actors-linear", item.Properties["durationEstimator"]);
        Assert.Equal("reward:cost-linear", item.Properties["rewardEstimator"]);
        Assert.Equal("0.5,0.9,0.99", item.Properties["statisticsPercentiles"]);
        Assert.Equal("True", item.Properties["statisticsRequiredAtRuntime"]);
        Assert.Single(searched.Items);
        Assert.True(planned.Succeeded);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    [Fact]
    public void Companion_validation_foundation_control_and_experience_resource_is_typed_filtered_and_plan_is_dry_run()
    {
        using var workspace = new TestWorkspace(project =>
        {
            var catalog = CryptWorldFeatureAuthoringService.Create(project);
            return new ProjectDefinition(project.Id, project.Title, project.Module.WithWorldFeatures(catalog),
                project.Blueprints, project.Areas, project.Dialogues, project.Quests);
        });
        var canonical = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var companion = canonical.Module.WorldFeatures.Companions.Single();

        var resource = workspace.Project.QueryWorldFeatures("companion", companion.Id.ToString("D"), 1);
        var searched = workspace.Project.QueryWorldFeatures("companion", "owner-or-gm", 1);
        var experienceSearched = workspace.Project.QueryWorldFeatures("companion", "screen-reader-high-contrast", 1);
        var planned = workspace.Project.PreviewWorldFeatures(new WorldFeaturePlanRequest(
            1, 0, canonical.Module.WorldFeatures));

        var item = Assert.Single(resource.Items);
        Assert.Equal("True", item.Properties["validation"]);
        Assert.Equal("2", item.Properties["recruitmentPaths"]);
        Assert.Equal("party", item.Properties["ownership"]);
        Assert.Equal("12", item.Properties["commands"]);
        Assert.Equal("recoverable-corpse", item.Properties["death"]);
        Assert.Equal("2", item.Properties["dialogueContexts"]);
        Assert.Equal("True", item.Properties["requiredAtRuntime"]);
        Assert.Equal("True", item.Properties["foundation"]);
        Assert.Equal("True", item.Properties["recruitable"]);
        Assert.Equal("not-recruited", item.Properties["preRecruitmentContext"]);
        Assert.Equal("recruited-by-speaker", item.Properties["ownerContext"]);
        Assert.Equal("capacity-ownership-inventory-position-persistence-atomic", item.Properties["membership"]);
        Assert.Equal("Campaign", item.Properties["persistence"]);
        Assert.Equal("3", item.Properties["companionSlots"]);
        Assert.Equal("1", item.Properties["summonSlots"]);
        Assert.Equal("1", item.Properties["petSlots"]);
        Assert.Equal("1", item.Properties["temporaryAllySlots"]);
        Assert.Equal("8", item.Properties["serverActorLimit"]);
        Assert.Equal("Support:2", item.Properties["roleLimits"]);
        Assert.Equal("True", item.Properties["control"]);
        Assert.Equal("individual", item.Properties["controlOwnership"]);
        Assert.Equal("player:owner", item.Properties["controllers"]);
        Assert.Equal("game-master,party,shared", item.Properties["allowedOwnershipTargets"]);
        Assert.Equal("1", item.Properties["ownershipRevision"]);
        Assert.Equal("6", item.Properties["movementCommands"]);
        Assert.Equal("6", item.Properties["tacticalCommands"]);
        Assert.Equal("4", item.Properties["stances"]);
        Assert.Equal("Support", item.Properties["aiRole"]);
        Assert.Equal("owner-or-gm", item.Properties["aiAuthorization"]);
        Assert.Equal("full", item.Properties["equipmentPermission"]);
        Assert.Equal("1", item.Properties["lockedItems"]);
        Assert.Equal("1", item.Properties["suggestedLoadout"]);
        Assert.Equal("player:owner", item.Properties["equipmentOwner"]);
        Assert.Equal("True", item.Properties["controlRequiredAtRuntime"]);
        Assert.Equal("True", item.Properties["experience"]);
        Assert.Equal("recoverable-corpse", item.Properties["deathMode"]);
        Assert.Equal("30", item.Properties["downedDurationSeconds"]);
        Assert.Equal("resource:ember", item.Properties["resurrectionResource"]);
        Assert.Equal("creature:elyra-heir", item.Properties["replacementCreature"]);
        Assert.Equal("memorial:elyra", item.Properties["memorial"]);
        Assert.Equal("True", item.Properties["dropInventory"]);
        Assert.Equal("10,30,60", item.Properties["cohesionThresholds"]);
        Assert.Equal("party.safe", item.Properties["cohesionAnchors"]);
        Assert.Equal("portraits-health-resources-statuses-quick-commands-reorder", item.Properties["partyHud"]);
        Assert.Equal("keyboard-gamepad", item.Properties["partyHudInputs"]);
        Assert.Equal("screen-reader-high-contrast", item.Properties["partyHudAccessibility"]);
        Assert.Equal("focus,health", item.Properties["partyHudResources"]);
        Assert.Equal("True", item.Properties["experienceRequiredAtRuntime"]);
        Assert.Single(searched.Items);
        Assert.Single(experienceSearched.Items);
        Assert.True(planned.Succeeded);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    [Fact]
    public void Merchant_transaction_resource_is_filtered_typed_and_plan_is_dry_run()
    {
        using var workspace = new TestWorkspace(project =>
        {
            var catalog = CryptWorldFeatureAuthoringService.Create(project);
            return new ProjectDefinition(project.Id, project.Title, project.Module.WithWorldFeatures(catalog),
                project.Blueprints, project.Areas, project.Dialogues, project.Quests);
        });
        var canonical = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var merchant = canonical.Module.WorldFeatures.Merchants.Single();

        var resource = workspace.Project.QueryWorldFeatures("merchant", merchant.Id.ToString("D"), 1);
        var searched = workspace.Project.QueryWorldFeatures("merchant", "merchant-crypt-vigil-nominal", 1);
        var experienceSearched = workspace.Project.QueryWorldFeatures("merchant", "screen-reader-high-contrast", 1);
        var planned = workspace.Project.PreviewWorldFeatures(new WorldFeaturePlanRequest(
            1, 0, canonical.Module.WorldFeatures));

        var item = Assert.Single(resource.Items);
        Assert.Equal("True", item.Properties["transactions"]);
        Assert.Equal("1", item.Properties["stock"]);
        Assert.Equal("True", item.Properties["requiredAtRuntime"]);
        Assert.Equal("True", item.Properties["foundation"]);
        Assert.Equal("crypt-vigil-market", item.Properties["storeSlug"]);
        Assert.Equal("store-vendor-dialogue-interaction-tests", item.Properties["wizard"]);
        Assert.Equal("1", item.Properties["foundationStock"]);
        Assert.Equal("Infinite", item.Properties["supplyModes"]);
        Assert.Equal("2", item.Properties["buyCurve"]);
        Assert.Equal("2", item.Properties["sellCurve"]);
        Assert.Equal("scripts/merchant_rule", item.Properties["customRule"]);
        Assert.Equal("True", item.Properties["foundationRequiredAtRuntime"]);
        Assert.Equal("True", item.Properties["experience"]);
        Assert.Equal("1.0.0", item.Properties["sessionApi"]);
        Assert.Equal("StoreSessionOpened", item.Properties["sessionOpened"]);
        Assert.Equal("StoreSessionClosed", item.Properties["sessionClosed"]);
        Assert.Equal("customer-vendor-price-context-transaction-summary", item.Properties["sessionPayload"]);
        Assert.Equal("keyboard-gamepad-compare-filters-cart-confirmation", item.Properties["storeUi"]);
        Assert.Equal("screen-reader-high-contrast", item.Properties["storeUiAccessibility"]);
        Assert.Equal("name,compare,price,quantity", item.Properties["storeUiColumns"]);
        Assert.Equal("True", item.Properties["experienceRequiredAtRuntime"]);
        Assert.Single(searched.Items);
        Assert.Single(experienceSearched.Items);
        Assert.True(planned.Succeeded);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    [Fact]
    public void Placeable_foundation_resource_is_filtered_typed_and_plan_is_dry_run()
    {
        using var workspace = new TestWorkspace(project =>
        {
            var catalog = CryptWorldFeatureAuthoringService.Create(project);
            return new ProjectDefinition(project.Id, project.Title, project.Module.WithWorldFeatures(catalog),
                project.Blueprints, project.Areas, project.Dialogues, project.Quests);
        });
        var canonical = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var placeable = canonical.Module.WorldFeatures.Placeables.Single();

        var resource = workspace.Project.QueryWorldFeatures("placeable", placeable.Id.ToString("D"), 1);
        var searched = workspace.Project.QueryWorldFeatures("placeable", "PlaceableProtectionOverride", 1);
        var securityEventsSearched = workspace.Project.QueryWorldFeatures("placeable",
            "ContainerContentsChanged", 1);
        var compositionEventsSearched = workspace.Project.QueryWorldFeatures("placeable",
            "RingRequested", 1);
        var planned = workspace.Project.PreviewWorldFeatures(new WorldFeaturePlanRequest(
            1, 0, canonical.Module.WorldFeatures));

        var item = Assert.Single(resource.Items);
        Assert.Equal("True", item.Properties["foundation"]);
        Assert.Equal("interactable", item.Properties["role"]);
        Assert.Equal("assets/placeables/crypt-pillar.glb", item.Properties["asset"]);
        Assert.Equal("crypt-pillar", item.Properties["slug"]);
        Assert.Equal("en-US:Ash pillar;fr-FR:Pilier des cendres", item.Properties["localizedNames"]);
        Assert.Equal("architecture,ash,crypt", item.Properties["tags"]);
        Assert.Equal("Pilier interactif de la crypte.", item.Properties["authorNotes"]);
        Assert.Equal("mesh:crypt-pillar", item.Properties["meshes"]);
        Assert.Equal("material:ash-stone", item.Properties["materials"]);
        Assert.Equal("assets/icons/crypt-pillar.png", item.Properties["icon"]);
        Assert.Equal("assets/portraits/crypt-pillar.png", item.Properties["portrait"]);
        Assert.Equal("vfx:ember-glow", item.Properties["visualEffects"]);
        Assert.Equal("1.25,0,-2.5", item.Properties["position"]);
        Assert.Equal("0,45,0", item.Properties["rotationDegrees"]);
        Assert.Equal("1,1,1", item.Properties["scale"]);
        Assert.Equal("0,0.5,0", item.Properties["pivot"]);
        Assert.Equal("0.25", item.Properties["snapIncrement"]);
        Assert.Equal(string.Empty, item.Properties["parent"]);
        Assert.Equal("True", item.Properties["alignToSurface"]);
        Assert.Equal("animation:crypt-pillar-rise", item.Properties["transformAnimation"]);
        Assert.Equal("True", item.Properties["onUsed"]);
        Assert.Equal("1.0.0", item.Properties["forgeScriptApi"]);
        Assert.Equal("InteractionExecuted", item.Properties["onUsedBinding"]);
        Assert.Equal("True", item.Properties["validation"]);
        Assert.Equal("True", item.Properties["requiredAtRuntime"]);
        Assert.Equal("key:ember", item.Properties["credentialId"]);
        Assert.Equal("Charge", item.Properties["credentialMode"]);
        Assert.Equal("3", item.Properties["credentialCharges"]);
        Assert.Equal(string.Empty, item.Properties["credentialVirtualPermission"]);
        Assert.Equal("True", item.Properties["credentialAudit"]);
        Assert.Equal("False", item.Properties["credentialRequiredAtRuntime"]);
        Assert.Equal("True", item.Properties["operations"]);
        Assert.Equal("slots:24;mass:160;owner:party;persistent:True;replicated:True",
            item.Properties["container"]);
        Assert.Equal("container.deposit,container.open,container.withdraw", item.Properties["containerAccess"]);
        Assert.Equal("inventory:crypt-pillar", item.Properties["inventory"]);
        Assert.Equal("loot:crypt-pillar", item.Properties["lootPolicy"]);
        Assert.Equal("ui:placeable-chest", item.Properties["containerUi"]);
        Assert.Equal("closed", item.Properties["initialVisualState"]);
        Assert.Equal("active,broken,closed,custom,idle,open", item.Properties["visualStates"]);
        Assert.Equal("3", item.Properties["visualTransitions"]);
        Assert.Equal(
            "active=animation:active,broken=animation:broken,closed=animation:closed,custom=animation:custom,idle=animation:idle,open=animation:open",
            item.Properties["visualStateAnimations"]);
        Assert.Equal(
            "active=visual:embers,broken=visual:broken,closed=visual:closed,custom=visual:quest,idle=visual:default,open=visual:open",
            item.Properties["visualStateOverrides"]);
        Assert.Equal("active,closed,custom,idle,open", item.Properties["interactableVisualStates"]);
        Assert.Equal(
            "closed->broken:destroyed@0.25,closed->open:open@1[quest:crypt-open],open->closed:close@1",
            item.Properties["visualTransitionTriggers"]);
        Assert.Equal("health:100/100;armor:10;stages:3;repair:True", item.Properties["damageable"]);
        Assert.Equal("100", item.Properties["maximumHealth"]);
        Assert.Equal("100", item.Properties["currentHealth"]);
        Assert.Equal("10", item.Properties["damageArmor"]);
        Assert.Equal("fire=0.25,force=0.5", item.Properties["damageResistances"]);
        Assert.Equal("0.75,0.5,0.25", item.Properties["visualDamageStages"]);
        Assert.Equal("vfx:ember-shatter", item.Properties["destructionEffect"]);
        Assert.Equal("debris:crypt-pillar", item.Properties["debrisDefinition"]);
        Assert.Equal("True", item.Properties["repairable"]);
        Assert.Equal("20", item.Properties["repairCost"]);
        Assert.Equal("destroy,move", item.Properties["protection"]);
        Assert.Equal("destroy,move", item.Properties["protectedActions"]);
        Assert.Equal("placeable.plot.override", item.Properties["protectionPermissions"]);
        Assert.Equal("Cet objet appartient à l'histoire.", item.Properties["protectionFeedback"]);
        Assert.Equal("True", item.Properties["allowGameMasterOverride"]);
        Assert.Equal("PlaceableProtectionOverride", item.Properties["protectionAudit"]);
        Assert.Equal(string.Empty, item.Properties["primaryFaction"]);
        Assert.Equal(string.Empty, item.Properties["factionMemberships"]);
        Assert.Equal(string.Empty, item.Properties["inheritFactionHostility"]);
        Assert.Equal(string.Empty, item.Properties["unauthorizedFactionActionsAreCrime"]);
        Assert.Equal(string.Empty, item.Properties["factionMembersOnlyAccess"]);
        Assert.Equal(string.Empty, item.Properties["blockFriendlyFactionTargeting"]);
        Assert.Equal(placeable.DialogueId?.ToString("D"), item.Properties["dialogue"]);
        Assert.Equal(string.Empty, item.Properties["conversationBindingCount"]);
        Assert.Equal(string.Empty, item.Properties["conversationDialogueIds"]);
        Assert.Equal(string.Empty, item.Properties["conversationTriggers"]);
        Assert.Equal(string.Empty, item.Properties["conversationEntryPoints"]);
        Assert.Equal(string.Empty, item.Properties["conversationRequiredAtRuntime"]);
        Assert.Equal("Locked;credentials:1;skill:mechanics;code:True;hacking:True;relock:True;jam:True",
            item.Properties["lock"]);
        Assert.Equal("Locked", item.Properties["lockState"]);
        Assert.Equal("key:ember", item.Properties["lockCredentialIds"]);
        Assert.Equal("mechanics", item.Properties["lockSkill"]);
        Assert.Equal("12", item.Properties["lockDifficulty"]);
        Assert.Equal("True", item.Properties["lockHasCode"]);
        Assert.Equal("True", item.Properties["lockHackable"]);
        Assert.Equal("True", item.Properties["lockRelockable"]);
        Assert.Equal("True", item.Properties["lockCanJam"]);
        Assert.Equal("placeable.lock.admin", item.Properties["lockPermissions"]);
        Assert.Equal("True", item.Properties["operationsRequiredAtRuntime"]);
        Assert.Equal("True", item.Properties["securityEvents"]);
        Assert.Contains("payload:damage:ember-burst", item.Properties["trap"], StringComparison.Ordinal);
        Assert.Contains("detect:10", item.Properties["trap"], StringComparison.Ordinal);
        Assert.Contains("disarm:12", item.Properties["trap"], StringComparison.Ordinal);
        Assert.Equal("container:open", item.Properties["trapTrigger"]);
        Assert.Equal("damage:ember-burst", item.Properties["trapPayload"]);
        Assert.Equal("10", item.Properties["trapDetection"]);
        Assert.Equal("12", item.Properties["trapDisarm"]);
        Assert.Equal("30", item.Properties["trapResetSeconds"]);
        Assert.Equal("ash-watch", item.Properties["trapOwner"]);
        Assert.Equal("vfx:ember-burst", item.Properties["trapVisualEffect"]);
        Assert.Equal("ash-watch", item.Properties["trapFriendlyPolicy"]);
        Assert.Equal("True", item.Properties["trapArmed"]);
        Assert.Equal("True", item.Properties["trapOneShot"]);
        Assert.Equal("False", item.Properties["trapRequiredAtRuntime"]);
        Assert.Contains("timing:Open", item.Properties["lootSpawner"], StringComparison.Ordinal);
        Assert.Contains("seed:42", item.Properties["lootSpawner"], StringComparison.Ordinal);
        Assert.Equal("1", item.Properties["lootEntries"]);
        Assert.Equal(placeable.SecurityEvents!.Loot!.LootTableId.ToString("D"),
            item.Properties["lootTableId"]);
        Assert.Equal("Open", item.Properties["lootTiming"]);
        Assert.Equal("42", item.Properties["lootSeed"]);
        Assert.Equal("True", item.Properties["lootPersistGeneratedState"]);
        Assert.Equal("0", item.Properties["lootGeneration"]);
        Assert.Equal(string.Empty, item.Properties["lootSavedResults"]);
        Assert.Contains(
            placeable.SecurityEvents.Loot.LootTable.Entries.Single().ItemDefinitionId.ToString("D"),
            item.Properties["lootTableEntries"],
            StringComparison.Ordinal);
        Assert.Equal("False", item.Properties["lootRequiredAtRuntime"]);
        Assert.Equal("ContainerOpened", item.Properties["containerOpened"]);
        Assert.Equal("ContainerClosed", item.Properties["containerClosed"]);
        Assert.Equal("actor-result-previous-state-authority", item.Properties["containerEventPayload"]);
        Assert.Equal("False", item.Properties["containerEventsRequiredAtRuntime"]);
        Assert.Equal("LockStateChanging", item.Properties["lockStateChanging"]);
        Assert.Equal("LockStateChanged", item.Properties["lockStateChanged"]);
        Assert.Equal("1.0.0", item.Properties["lockEventApi"]);
        Assert.Equal("method-credential-actor-controlled-veto", item.Properties["lockEventPayload"]);
        Assert.Equal("False", item.Properties["lockEventsRequiredAtRuntime"]);
        Assert.Equal("ContainerContentsChanged", item.Properties["contentsChanged"]);
        Assert.Equal("1.0.0", item.Properties["contentsChangedApi"]);
        Assert.Equal("transactional-delta-cause-revision-actor", item.Properties["contentsChangedPayload"]);
        Assert.Equal("False", item.Properties["contentsChangedRequiredAtRuntime"]);
        Assert.Equal("True", item.Properties["securityEventsRequiredAtRuntime"]);
        Assert.Equal("True", item.Properties["compositionEvents"]);
        Assert.Equal("DamageApplied", item.Properties["damageApplied"]);
        Assert.Equal("EntityDestroyed", item.Properties["entityDestroyed"]);
        Assert.Equal("1.0.0", item.Properties["damageEventApi"]);
        Assert.Equal("source-amount-damage-type-previous-current-context",
            item.Properties["damageEventPayload"]);
        Assert.Equal(string.Empty, item.Properties["damageEventsDiagnostic"]);
        Assert.Equal("False", item.Properties["damageEventsRequiredAtRuntime"]);
        Assert.Equal("lever:mechanism:snap:True:capacity:2", item.Properties["attachmentSockets"]);
        Assert.Contains("lever:", item.Properties["attachments"], StringComparison.Ordinal);
        Assert.Contains("prefab:crypt-pillar-lever", item.Properties["attachments"],
            StringComparison.Ordinal);
        Assert.Equal("inspect,use,open,talk,take,push,climb,custom-ring", item.Properties["multiVerbs"]);
        Assert.Contains("RingRequested", item.Properties["verbBindings"], StringComparison.Ordinal);
        Assert.Equal("True", item.Properties["compositionEventsRequiredAtRuntime"]);
        Assert.Single(searched.Items);
        Assert.Single(securityEventsSearched.Items);
        Assert.Single(compositionEventsSearched.Items);
        Assert.True(planned.Succeeded);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    [Fact]
    public void Placeable_direct_damage_authority_is_exposed_to_forgescript_and_mcp()
    {
        using var workspace = new TestWorkspace(project =>
        {
            var source = CryptWorldFeatureAuthoringService.Create(project);
            var original = source.Placeables.Single();
            var direct = original with
            {
                CompositionEvents = null,
                CompositionEventsRequiredAtRuntime = false,
                DamageEvents = original.CompositionEvents!.DamageEvents with
                {
                    DamagedBinding = "DirectDamageApplied",
                    DestroyedBinding = "DirectEntityDestroyed",
                },
                ContentsChangedRequiredAtRuntime = true,
                DamageEventsRequiredAtRuntime = true,
            };
            var catalog = new WorldFeatureCatalog(
                source.Id,
                source.SchemaVersion,
                source.Provenance,
                [direct],
                source.Doors,
                source.Triggers,
                source.Encounters,
                source.VehicleRoutes,
                source.Merchants,
                source.Companions,
                source.Scripts);
            return new ProjectDefinition(project.Id, project.Title, project.Module.WithWorldFeatures(catalog),
                project.Blueprints, project.Areas, project.Dialogues, project.Quests);
        });
        var canonical = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var placeable = canonical.Module.WorldFeatures.Placeables.Single();

        var resource = workspace.Project.QueryWorldFeatures("placeable", placeable.Id.ToString("D"), 1);

        var item = Assert.Single(resource.Items);
        Assert.Equal("InteractionExecuted", item.Properties["onUsedBinding"]);
        Assert.Equal("1.0.0", item.Properties["contentsChangedApi"]);
        Assert.Equal("True", item.Properties["contentsChangedRequiredAtRuntime"]);
        Assert.Equal("False", item.Properties["compositionEvents"]);
        Assert.Equal("DirectDamageApplied", item.Properties["damageApplied"]);
        Assert.Equal("DirectEntityDestroyed", item.Properties["entityDestroyed"]);
        Assert.Equal("1.0.0", item.Properties["damageEventApi"]);
        Assert.Equal("True", item.Properties["damageEventsRequiredAtRuntime"]);
        Assert.Equal(string.Empty, item.Properties["damageEventsDiagnostic"]);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    [Fact]
    public void Trigger_foundation_resource_is_filtered_typed_and_plan_is_dry_run()
    {
        using var workspace = new TestWorkspace(project =>
        {
            var catalog = CryptWorldFeatureAuthoringService.Create(project);
            return new ProjectDefinition(project.Id, project.Title, project.Module.WithWorldFeatures(catalog),
                project.Blueprints, project.Areas, project.Dialogues, project.Quests);
        });
        var canonical = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var trigger = canonical.Module.WorldFeatures.Triggers.Single();

        var resource = workspace.Project.QueryWorldFeatures("trigger", trigger.Id.ToString("D"), 1);
        var trapSearch = workspace.Project.QueryWorldFeatures("trigger", "TrapTriggered", 1);
        var volumeSearch = workspace.Project.QueryWorldFeatures("trigger", "DamageVolumeEntered", 1);
        var planned = workspace.Project.PreviewWorldFeatures(new WorldFeaturePlanRequest(
            1, 0, canonical.Module.WorldFeatures));

        var item = Assert.Single(resource.Items);
        Assert.Equal("True", item.Properties["foundation"]);
        Assert.Equal("ExtrudedPolygon2D", item.Properties["shape"]);
        Assert.Equal("1", item.Properties["actions"]);
        Assert.Equal("True", item.Properties["portal"]);
        Assert.Equal("VolumeEntered", item.Properties["onEnter"]);
        Assert.Equal("VolumeExited", item.Properties["onExit"]);
        Assert.Equal("1.0.0", item.Properties["forgeScriptApi"]);
        Assert.Equal("True", item.Properties["validation"]);
        Assert.Equal("True", item.Properties["requiredAtRuntime"]);
        Assert.Equal("True", item.Properties["advanced"]);
        Assert.Contains("damage:ember-burst", item.Properties["trap"], StringComparison.Ordinal);
        Assert.Contains("gm:True", item.Properties["trap"], StringComparison.Ordinal);
        Assert.Contains("TrapTriggered", item.Properties["trapBindings"], StringComparison.Ordinal);
        Assert.Contains("players:True", item.Properties["targetFilter"], StringComparison.Ordinal);
        Assert.Contains("Damage", item.Properties["gameplayVolumes"], StringComparison.Ordinal);
        Assert.Contains("Checkpoint", item.Properties["gameplayVolumes"], StringComparison.Ordinal);
        Assert.Contains("maximum:100", item.Properties["occupancyDebug"], StringComparison.Ordinal);
        Assert.Equal("True", item.Properties["advancedRequiredAtRuntime"]);
        Assert.Single(trapSearch.Items);
        Assert.Single(volumeSearch.Items);
        Assert.True(planned.Succeeded);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    [Fact]
    public void Quest_and_item_depth_resources_are_filtered_bounded_and_dry_run_only()
    {
        using var workspace = new TestWorkspace();
        var canonical = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var quest = canonical.Quests.Single(item => item.Operations is not null);
        var item = canonical.Blueprints.Single(candidate => candidate.ItemAdvanced is not null);

        var quests = workspace.Project.QueryQuestOperations("crypt", 1);
        var questPlan = workspace.Project.PreviewQuestOperations(new(1, 0, quest.Id.ToString(),
            quest.Operations!));
        var items = workspace.Project.QueryAdvancedItems("ash-key", 1);
        var itemPlan = workspace.Project.PreviewAdvancedItem(new ItemAdvancedPlanRequest(1, 0,
            item.Id.ToString(), item.ItemAdvanced!));

        var questResource = Assert.Single(quests.Items);
        Assert.True(questResource.Repeatable);
        Assert.Equal("Unanimous", questResource.ShareMode);
        Assert.Equal(1, questResource.ExclusiveChoiceCount);
        var itemResource = Assert.Single(items.Items);
        Assert.True(itemResource.Weapon);
        Assert.True(itemResource.Container);
        Assert.True(itemResource.Unique);
        Assert.True(itemResource.Procedural);
        Assert.True(questPlan.Succeeded);
        Assert.True(itemPlan.Succeeded);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        Assert.Throws<ArgumentOutOfRangeException>(() => workspace.Project.QueryQuestOperations(limit: 101));
        Assert.Throws<ArgumentOutOfRangeException>(() => workspace.Project.QueryAdvancedItems(limit: 101));
    }

    [Fact]
    public void Quest_journal_resource_is_filtered_ordered_and_mutation_remains_dry_run()
    {
        using var workspace = new TestWorkspace();
        var canonical = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var quest = canonical.Quests.Single();

        var resource = workspace.Project.QueryQuestJournals(quest.Slug, 1);
        var planned = workspace.Project.PreviewQuestJournal(new(1, 0, quest.Id.ToString(), quest));
        var simulated = workspace.Project.SimulateQuestJournal(new(1, quest.Id.ToString(),
            quest.InstancePolicy.DefaultScope.ToString(),
            "player:mcp", new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero)));
        var invalidSimulation = workspace.Project.SimulateQuestJournal(new(1, quest.Id.ToString(), "Invalid",
            "player:mcp", new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero)));
        var wrongSchema = workspace.Project.PreviewQuestJournal(new(2, 0, quest.Id.ToString(), quest));
        var wrongId = workspace.Project.PreviewQuestJournal(new(1, 0,
            "c7900000-0000-4000-8000-000000000099", quest));

        var item = Assert.Single(resource.Items);
        Assert.Equal(quest.Id.ToString(), item.QuestId);
        Assert.Equal(quest.Category, item.Category);
        Assert.Equal(quest.States.OrderBy(state => state.DisplayOrder).Select(state => state.Id.ToString()),
            item.States.Select(state => state.StateId));
        Assert.Equal(quest.Transitions.OrderBy(transition => transition.DisplayOrder)
            .Select(transition => transition.Id.ToString()), item.Transitions.Select(transition => transition.TransitionId));
        Assert.True(simulated.Succeeded, string.Join(" | ", simulated.Diagnostics.Select(item =>
            $"{item.Code}:{item.Message}")));
        Assert.Equal("AwaitingEvent", simulated.Status);
        Assert.False(invalidSimulation.Succeeded);
        Assert.True(planned.Succeeded);
        Assert.Equal($"quests/{quest.Id}", planned.SemanticDiff!.Path);
        Assert.False(wrongSchema.Succeeded);
        Assert.False(wrongId.Succeeded);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        Assert.Throws<ArgumentOutOfRangeException>(() => workspace.Project.QueryQuestJournals(limit: 101));
    }

    [Fact]
    public void Quest_authoring_resource_is_structured_filterable_and_mutation_remains_dry_run()
    {
        using var workspace = new TestWorkspace(project =>
        {
            var quest = project.Quests.Single();
            var objective = new QuestAuthoringObjective(
                Guid.Parse("c7110000-0000-4000-8000-000000000301"), 1, "test:r071", 1,
                QuestObjectiveKind.Collection, new LocalizedText("quest.r071.objective", "Trouver la clé"),
                1, ["ash-key"], null, 0, [], null, false);
            var participant = new QuestAuthoringParticipant(
                Guid.Parse("c7110000-0000-4000-8000-000000000302"), 1, "test:r071", 1, "guide",
                QuestParticipantBindingKind.Blueprint, "elyra-npc", ["quest:active"], true);
            var profile = new QuestAuthoringProfile(
                Guid.Parse("c7110000-0000-4000-8000-000000000303"), 1, "test:r071", true, true,
                [objective], new QuestAuthoringRewardBundle(
                    Guid.Parse("c7110000-0000-4000-8000-000000000304"), 1, "test:r071", 10,
                    new Dictionary<string, int> { ["ash-key"] = 1 },
                    new Dictionary<string, decimal> { ["ember"] = 5 },
                    new Dictionary<string, double> { ["ash-watch"] = 2 }, ["crypt-open"], ["choice:dawn"]),
                new QuestAuthorNotes("Restore the vigil", "Ember covenant", "author:test",
                    QuestEditorialStatus.Review, ["branch"], ["voice"], ["08_QUEST.md"],
                    [new QuestAuthorReview(Guid.Parse("c7110000-0000-4000-8000-000000000305"), 1,
                        "test:r071", "reviewer:test", QuestEditorialStatus.Review, "Reviewed",
                        DateTimeOffset.Parse("2026-08-11T00:00:00Z"))]), [participant],
                new QuestPlotWizardPlan(Guid.Parse("c7110000-0000-4000-8000-000000000306"), 1,
                    "test:r071", [project.Areas.Single().Slug], [participant.Id], ["crypt-pillar"],
                    [project.Dialogues.Single().Id], [objective.Id], quest.Transitions.Select(item => item.Id).ToArray(),
                    ["nominal", "failure", "repeat", "multiplayer", "save-resume"], false));
            return new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
                project.Areas, project.Dialogues,
                project.Quests.Select(item => item.Id == quest.Id ? item.WithAuthoring(profile) : item).ToArray());
        });
        var canonical = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var quest = canonical.Quests.Single();

        var resource = workspace.Project.QueryQuestJournals("guide", 1);
        var planned = workspace.Project.PreviewQuestJournal(new(1, 0, quest.Id.ToString(), quest));

        var authoring = Assert.Single(resource.Items).Authoring;
        Assert.NotNull(authoring);
        Assert.Equal("Collection", Assert.Single(authoring.Objectives).Kind);
        Assert.Equal("elyra-npc", Assert.Single(authoring.Participants).BindingValue);
        Assert.Equal(5, authoring.Wizard.GeneratedTestScenarios.Count);
        Assert.False(authoring.Wizard.GeneratesOpaqueCode);
        Assert.True(planned.Succeeded);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    [Fact]
    public void Quest_experience_resource_is_structured_filterable_and_mutation_remains_dry_run()
    {
        using var workspace = new TestWorkspace(project =>
        {
            var quest = project.Quests.Single();
            var objective = new QuestAuthoringObjective(Guid.Parse("c7120000-0000-4000-8000-000000000501"),
                1, "test:r072", 1, QuestObjectiveKind.Destination,
                new LocalizedText("quest.r072.objective", "Atteindre la crypte"), 0, [],
                project.Areas[0].Slug, 0, [], null, false);
            var participant = new QuestAuthoringParticipant(Guid.Parse("c7120000-0000-4000-8000-000000000502"),
                1, "test:r072", 1, "guide", QuestParticipantBindingKind.Query, "query:guide", [], true);
            var authoring = new QuestAuthoringProfile(Guid.Parse("c7120000-0000-4000-8000-000000000503"),
                1, "test:r072", true, false, [objective],
                new QuestAuthoringRewardBundle(Guid.Parse("c7120000-0000-4000-8000-000000000504"), 1,
                    "test:r072", 0, new Dictionary<string, int>(), new Dictionary<string, decimal>(),
                    new Dictionary<string, double>(), [], []),
                new QuestAuthorNotes("Design", "Canon", "author:test", QuestEditorialStatus.Review, [], [],
                    ["source"], [new QuestAuthorReview(Guid.Parse("c7120000-0000-4000-8000-000000000505"),
                        1, "test:r072", "reviewer:test", QuestEditorialStatus.Review, "Reviewed",
                        DateTimeOffset.UnixEpoch)]), [participant],
                new QuestPlotWizardPlan(Guid.Parse("c7120000-0000-4000-8000-000000000506"), 1,
                    "test:r072", [project.Areas[0].Slug], [participant.Id], [], [project.Dialogues[0].Id],
                    [objective.Id], quest.Transitions.Select(item => item.Id).ToArray(),
                    ["nominal", "failure", "repeat", "multiplayer", "save-resume"], false));
            quest = quest.WithAuthoring(authoring);
            var resource = new Forge.Domain.Quests.QuestExperienceResource(Guid.Parse("c7120000-0000-4000-8000-000000000507"),
                1, "test:r072", 1, QuestExperienceResourceKind.Asset, "asset:ember-prism", "owner:module",
                true, ["quest:active"]);
            var step = new QuestExperienceStep(Guid.Parse("c7120000-0000-4000-8000-000000000508"), 1,
                "template:r072:v1", 1, new LocalizedText("quest.r072.step", "Inspecter le prisme"),
                [objective.Id], quest.Transitions.Select(item => item.Id).ToArray(), "investigation", 1, true);
            var history = new QuestExperienceHistoryEvent(Guid.Parse("c7120000-0000-4000-8000-000000000509"),
                1, "test:r072", quest.Id, "hidden", "discovered", "dialogue", "player:mcp",
                DateTimeOffset.UnixEpoch, new Dictionary<string, string> { ["seed"] = "72" },
                new LocalizedText("quest.r072.history", "Le prisme est révélé."));
            var marker = new QuestExperienceMarker(Guid.Parse("c7120000-0000-4000-8000-000000000510"),
                1, "test:r072", 1, objective.Id, QuestExperienceMarkerBindingKind.Query,
                "query:ember-prism", QuestExperienceVisibility.Discovered, 4, true,
                new LocalizedText("quest.r072.marker", "Prisme des braises"), new Vector3Value(1, 0, 2));
            var experience = new QuestExperienceProfile(Guid.Parse("c7120000-0000-4000-8000-000000000511"),
                1, "test:r072", true, true, [resource], [step],
                new QuestExperienceVisibilityPolicy(Guid.Parse("c7120000-0000-4000-8000-000000000512"), 1,
                    "test:r072", QuestExperienceVisibility.Hidden,
                    new Dictionary<QuestExperienceVisibility, IReadOnlyList<string>>
                    { [QuestExperienceVisibility.Rumor] = ["heard"] }, true), [history], [marker]);
            return new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
                project.Areas, project.Dialogues, [quest.WithExperience(experience)]);
        });
        var quest = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Quests.Single();

        var resource = workspace.Project.QueryQuestJournals("ember-prism", 1);
        var planned = workspace.Project.PreviewQuestJournal(new(1, 0, quest.Id.ToString(), quest));
        var invalidQuest = quest.WithExperience(quest.Experience! with
        {
            Resources = [quest.Experience.Resources[0] with { ReferenceId = "missing:asset" }],
        });
        var rejected = workspace.Project.PreviewQuestJournal(new(1, 0, quest.Id.ToString(), invalidQuest));
        var canonicalProject = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var invalidProject = new ProjectDefinition(canonicalProject.Id, canonicalProject.Title,
            canonicalProject.Module, canonicalProject.Blueprints, canonicalProject.Areas,
            canonicalProject.Dialogues, [invalidQuest]);
        var localizedDiagnostics = ProjectValidator.Validate(invalidProject);

        var experience = Assert.Single(resource.Items).Experience;
        Assert.NotNull(experience);
        Assert.Equal("Asset", Assert.Single(experience.Resources).Kind);
        Assert.Equal("investigation", Assert.Single(experience.Steps).TemplateId);
        Assert.Equal("Hidden", experience.Visibility.Initial);
        Assert.Equal("dialogue", Assert.Single(experience.History).Cause);
        Assert.Equal("Query", Assert.Single(experience.Markers).BindingKind);
        Assert.True(planned.Succeeded, string.Join(" | ", planned.Diagnostics.Select(item => item.Code)));
        Assert.False(rejected.Succeeded);
        Assert.Contains(rejected.Diagnostics, item => item.Code == QuestExperienceProfileValidator.ReferenceMissingCode);
        Assert.Contains(localizedDiagnostics, item =>
            item.Code == QuestExperienceProfileValidator.ReferenceMissingCode &&
            item.Path.Contains("/experience/resources/", StringComparison.Ordinal));
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    [Fact]
    public void Quest_quality_resource_is_filterable_and_invalid_ai_dependency_is_rejected_in_dry_run()
    {
        using var workspace = new TestWorkspace(project =>
        {
            var quest = project.Quests.Single();
            var objective = new QuestAuthoringObjective(Guid.Parse("c7130000-0000-4000-8000-000000000701"),
                1, "test:r073", 1, QuestObjectiveKind.Boolean,
                new LocalizedText("quest.r073.objective", "Restaurer la veillée"), 1, [], null, 0, [], null, false);
            var participant = new QuestAuthoringParticipant(Guid.Parse("c7130000-0000-4000-8000-000000000702"),
                1, "test:r073", 1, "guide", QuestParticipantBindingKind.Query, "query:guide", [], true);
            var authoring = new QuestAuthoringProfile(Guid.Parse("c7130000-0000-4000-8000-000000000703"),
                1, "test:r073", true, false, [objective],
                new QuestAuthoringRewardBundle(Guid.Parse("c7130000-0000-4000-8000-000000000704"), 1,
                    "test:r073", 0, new Dictionary<string, int>(), new Dictionary<string, decimal>(),
                    new Dictionary<string, double>(), [], []),
                new QuestAuthorNotes("Design", "Canon", "author:test", QuestEditorialStatus.Review, [], [],
                    ["source"], [new QuestAuthorReview(
                        Guid.Parse("c7130000-0000-4000-8000-000000000711"), 1, "test:r073",
                        "reviewer:test", QuestEditorialStatus.Review, "Reviewed", DateTimeOffset.UnixEpoch)]),
                [participant],
                new QuestPlotWizardPlan(Guid.Parse("c7130000-0000-4000-8000-000000000705"), 1,
                    "test:r073", [project.Areas[0].Slug], [participant.Id], [], [project.Dialogues[0].Id],
                    [objective.Id], quest.Transitions.Select(item => item.Id).ToArray(),
                    ["nominal", "failure", "repeat", "multiplayer", "save-resume"], false));
            quest = quest.WithAuthoring(authoring);
            var dependencies = new[]
            {
                new QuestAiDependency(Guid.Parse("c7130000-0000-4000-8000-000000000706"), 1,
                    "test:r073", 1, QuestAiDependencyKind.State, quest.InitialStateId.ToString()),
                new QuestAiDependency(Guid.Parse("c7130000-0000-4000-8000-000000000707"), 1,
                    "test:r073", 2, QuestAiDependencyKind.Participant, participant.Id.ToString("D")),
                new QuestAiDependency(Guid.Parse("c7130000-0000-4000-8000-000000000708"), 1,
                    "test:r073", 3, QuestAiDependencyKind.Zone, project.Areas[0].Slug),
            };
            var plan = QuestQualityRuntimeService.CreateAiPlan(quest,
                new LocalizedText("quest.r073.ai", "Restaurer la veillée avec validation humaine."), dependencies,
                ["08_QUEST.md#orf-qst-025"], ["style:human-review"], "agent:mcp",
                Guid.Parse("c7130000-0000-4000-8000-000000000709"), "preview:quest:r073");
            plan = QuestQualityRuntimeService.Approve(plan, "author:mcp", DateTimeOffset.UnixEpoch);
            var quality = new QuestQualityProfile(Guid.Parse("c7130000-0000-4000-8000-000000000710"),
                1, "test:r073", true, true, QuestQualityRuntimeService.GenerateTests(quest, 7300), plan);
            return new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
                project.Areas, project.Dialogues, [quest.WithQuality(quality)]);
        });
        var canonicalProject = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var quest = canonicalProject.Quests.Single();

        var resource = workspace.Project.QueryQuestJournals("human-review", 1);
        var planned = workspace.Project.PreviewQuestJournal(new(1, 0, quest.Id.ToString(), quest));
        var missingDependency = quest.Quality!.AiPlan.Dependencies[^1] with { ReferenceId = "missing-zone" };
        var invalidQuest = quest.WithQuality(quest.Quality with
        {
            AiPlan = quest.Quality.AiPlan with
            {
                Dependencies = [.. quest.Quality.AiPlan.Dependencies.Take(2), missingDependency],
            },
        });
        var rejected = workspace.Project.PreviewQuestJournal(new(1, 0, quest.Id.ToString(), invalidQuest));
        var invalidProject = new ProjectDefinition(canonicalProject.Id, canonicalProject.Title,
            canonicalProject.Module, canonicalProject.Blueprints, canonicalProject.Areas,
            canonicalProject.Dialogues, [invalidQuest]);
        var localizedDiagnostics = ProjectValidator.Validate(invalidProject);

        var quality = Assert.Single(resource.Items).Quality;
        Assert.NotNull(quality);
        Assert.Equal(5, quality.TestScenarios.Count);
        Assert.Equal("Nominal", quality.TestScenarios[0].Kind);
        Assert.NotEmpty(quality.TestScenarios[0].Steps);
        Assert.NotEmpty(quality.TestScenarios[0].Edges);
        Assert.Equal("ApplyApprovedPlan", quality.AiPlan.Authorization);
        Assert.Equal("author:mcp", quality.AiPlan.ApprovedBy);
        Assert.True(planned.Succeeded, string.Join(" | ", planned.Diagnostics.Select(item => item.Code)));
        Assert.False(rejected.Succeeded);
        Assert.Contains(rejected.Diagnostics, item => item.Code == QuestQualityProfileValidator.ReferenceMissingCode);
        Assert.Contains(localizedDiagnostics, item =>
            item.Code == QuestQualityProfileValidator.ReferenceMissingCode &&
            item.Path.Contains("/quality/aiPlan/dependencies/", StringComparison.Ordinal));
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    [Fact]
    public void DialoguePresentationResourceIsFilteredBoundedAndMutationRemainsDryRun()
    {
        using var workspace = new TestWorkspace();
        var dialogueId = "40000000-0000-4000-8000-000000000001";
        var node = DialogueNodeId.Parse("41000000-0000-4000-8000-000000000001");
        var speaker = BlueprintId.Parse("10000000-0000-4000-8000-000000000004");
        var profile = new DialoguePresentationProfile(
            Guid.Parse("d1330000-0000-4000-8000-000000000010"), 1, "mcp:test", false, true,
            [new(Guid.Parse("d1330000-0000-4000-8000-000000000011"), node, speaker, "talk", 0, 900, 100,
                DialogueGesture.Explain, null, DialogueAnimationInterruptionPolicy.BlendOut, "idle", "mcp:test")],
            [new(Guid.Parse("d1330000-0000-4000-8000-000000000012"), node, speaker, "fr", null, "take-01",
                new("dialogue.elyra.crypt.start", "La crypte nous écoute."), "neutral", "concerned", 900,
                null, null, true, "mcp:test")],
            [new(Guid.Parse("d1330000-0000-4000-8000-000000000013"), node,
                DialogueCameraShotMode.Automatic, null, speaker, CameraFraming.Medium, 52, 900, 150,
                true, true, true, .08f, 10, "mcp:test")], [], []);

        var before = workspace.Project.QueryDialoguePresentations();
        var planned = workspace.Project.PreviewDialoguePresentation(new(1, 0, dialogueId, profile));
        var after = workspace.Project.QueryDialoguePresentations("elyra", 1);
        var byVariant = workspace.Project.QueryDialoguePresentations("elyra-opening", 1);

        var existing = Assert.Single(before.Items);
        Assert.Equal("elyra-crypt-of-ashes", existing.Slug);
        Assert.True(planned.Succeeded);
        Assert.Equal("dialogues/40000000-0000-4000-8000-000000000001/presentation",
            planned.SemanticDiff!.Path);
        var unchanged = Assert.Single(after.Items);
        Assert.Equal(existing.ProfileId, unchanged.ProfileId);
        Assert.Equal(existing.InterjectionRuleCount, unchanged.InterjectionRuleCount);
        Assert.Equal(1, existing.TimedChoiceCount);
        Assert.Equal(1, existing.VariantSetCount);
        Assert.Equal(2, existing.VariantCount);
        Assert.Equal(existing.ProfileId, Assert.Single(byVariant.Items).ProfileId);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        Assert.Throws<ArgumentOutOfRangeException>(() => workspace.Project.QueryDialoguePresentations(limit: 101));
        Assert.False(workspace.Project.PreviewDialoguePresentation(new(2, 0, dialogueId, profile)).Succeeded);
    }

    [Fact]
    public void FactionResourceIsBoundedAndMutationRemainsDryRun()
    {
        using var workspace = new TestWorkspace();
        var parent = Guid.Parse("b2040000-0000-4000-8000-000000000001");
        var child = Guid.Parse("b2040000-0000-4000-8000-000000000002");
        var catalog = new FactionCatalog(
            [
                new(parent, 1, "mcp:test", "ember-guild", "Guilde", false, [], ["guild"]),
                new(child, 1, "mcp:test", "ash-watch", "Veilleurs", false, [parent], ["guard"]),
            ], [new(parent, child, -60, "watch-distrust", "hostile")],
            [new("hostile", -100, -1, 1), new("neutral", 0, 100, 1)], [],
            new FactionAdvancedProfile(
                Guid.Parse("b2040000-0000-4000-8000-000000000010"), 1, "mcp:test", true,
                [new(Guid.Parse("b2040000-0000-4000-8000-000000000011"), child, true, true, true)],
                [], [], []), true,
            [new(Guid.Parse("b2040000-0000-4000-8000-000000000012"), 1, "mcp:r086", "hero",
                parent, child, 20, null, "rescued-watch", PersonalReputationSubjectKind.Player)], true);

        var before = workspace.Project.QueryFactions();
        var planned = workspace.Project.PreviewFactions(new(1, 0, catalog));
        var after = workspace.Project.QueryFactions("ash", 1);

        Assert.Contains(before.Items, item => item.Slug == "ash-watch");
        Assert.True(planned.Succeeded);
        Assert.NotNull(planned.SemanticDiff);
        var unchanged = Assert.Single(after.Items);
        Assert.Equal("ash-watch", unchanged.Slug);
        Assert.Equal(before.Items.Single(item => item.Slug == "ash-watch").FactionId, unchanged.FactionId);
        Assert.Equal(2, before.Relationships!.Count);
        Assert.Contains(before.Relationships, item => item.Score == -75 && item.Disposition == "hostile");
        Assert.Empty(after.Relationships!);
        Assert.Equal(3, after.Bands!.Count);
        Assert.Equal("Player", Assert.Single(after.PersonalReputation!).SubjectKind);
        Assert.Equal("ash-warden-truce", Assert.Single(after.PersonalReputation!).Cause);
        Assert.NotNull(before.ReputationProfileId);
        Assert.True(before.ReputationRequiredAtRuntime);
        Assert.Equal(5, before.GlobalReputation!.Count);
        Assert.Equal(2, before.HeatmapHistory!.Count);
        Assert.Equal("Campaign", before.Reaction!.DefaultScope);
        Assert.Empty(after.GlobalReputation!);
        Assert.All(after.HeatmapHistory!, item => Assert.Empty(item.Relationships));
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        Assert.Throws<ArgumentOutOfRangeException>(() => workspace.Project.QueryFactions(limit: 101));
    }

    [Fact]
    public void CreatureEventResourceIsReadOnlyFilteredAndBounded()
    {
        using var workspace = new TestWorkspace();

        var all = workspace.Project.QueryCreatureEvents();
        var filtered = workspace.Project.QueryCreatureEvents("ash-warden", 1);
        var missing = workspace.Project.QueryCreatureEvents("does-not-exist", 1);
        var planned = workspace.Project.PreviewCreatureEvents(new(1, 0, filtered.Items.Single().BlueprintId,
            ProjectSession.Open(workspace.RootDirectory).Session!.Project.Blueprints
                .Single(item => item.Slug == "ash-warden-boss").CreatureEvents!));

        Assert.Contains(all.Items, item => item.BlueprintSlug == "ash-warden-boss");
        var warden = Assert.Single(filtered.Items);
        Assert.Equal(9, warden.BindingCount);
        Assert.Equal(1_500, warden.HeartbeatIntervalMilliseconds);
        Assert.Equal("CombatTickCompleted", warden.CombatBoundary);
        Assert.Contains("crypt.braziers_lit", warden.CustomEvents);
        Assert.Equal("BeforeDamage", warden.BeforeDamageHandler);
        Assert.Equal("OnDamaged", warden.DamageAppliedHandler);
        Assert.Equal("OnDeath", warden.DiedHandler);
        Assert.Equal(4, warden.BeforeDamageBudgetMilliseconds);
        Assert.Empty(missing.Items);
        Assert.True(planned.Succeeded);
        Assert.NotNull(planned.SemanticDiff);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        Assert.Throws<ArgumentOutOfRangeException>(() => workspace.Project.QueryCreatureEvents(limit: 101));
    }

    [Fact]
    public void CreatureFoundationResourceIsTypedFilteredAndPlanIsDryRun()
    {
        using var workspace = new TestWorkspace();
        var all = workspace.Project.QueryCreatureFoundations();
        var filtered = workspace.Project.QueryCreatureFoundations("ash-warden", 1);
        var missing = workspace.Project.QueryCreatureFoundations("does-not-exist", 1);
        var canonical = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Blueprints
            .Single(item => item.Slug == "ash-warden-boss");
        var planned = workspace.Project.PreviewCreatureFoundation(new(1, 0, canonical.Id.ToString(),
            canonical.CreatureFoundation!));

        Assert.Contains(all.Items, item => item.BlueprintSlug == "ash-warden-boss");
        var warden = Assert.Single(filtered.Items);
        Assert.Equal("Grounded", warden.MovementMode);
        Assert.Equal(84, warden.ResourceState["health"]);
        Assert.Equal("creature.spawned", warden.SpawnEvent);
        Assert.Equal("OnSpawned", warden.SpawnHandler);
        Assert.True(warden.AuthoringCompositionRequiredAtRuntime);
        Assert.Equal("Boss", warden.EncounterRole);
        Assert.Equal(12, warden.ThreatRating);
        Assert.Equal("species.emberborn", warden.SpeciesId);
        Assert.Equal("mesh.ash-warden", warden.MeshAssetId);
        Assert.Equal("a1770000-0000-4000-8000-000000000020", warden.AnimationGraphId);
        Assert.Equal(5, warden.AnimationStateCount);
        Assert.True(warden.RulesCompositionRequiredAtRuntime);
        Assert.Equal("Seated", warden.InitialLifeState);
        Assert.Equal("pose.warden-throne", warden.PoseId);
        Assert.Equal(3, warden.AttributeCount);
        Assert.Equal(4, warden.Avoidance);
        Assert.Equal(2, warden.Armor);
        Assert.Equal(1, warden.AbilityCount);
        Assert.Equal(1, warden.InventoryItemCount);
        Assert.True(warden.InteractionCompositionRequiredAtRuntime);
        Assert.Equal("b2050000-0000-4000-8000-000000000002", warden.PrimaryFactionId);
        Assert.Equal(2, warden.FactionMembershipCount);
        Assert.Equal(4, warden.PerceptionSenseCount);
        Assert.Equal(3, warden.DialogueBindingCount);
        Assert.True(warden.Invulnerable);
        Assert.True(warden.UsesNarrativeDeath);
        Assert.False(warden.DialogueInterruptible);
        Assert.Equal("OnConversation", warden.OnConversationHandler);
        Assert.True(warden.EventAiCompositionRequiredAtRuntime);
        Assert.Equal("Utility", warden.AiControllerKind);
        Assert.Equal("ai.ash-warden", warden.AiControllerDefinitionId);
        Assert.Equal(3, warden.AiBlackboardCount);
        Assert.Equal(3, warden.AiAllowedActionCount);
        Assert.Equal(["Repath", "Avoid", "Teleport", "Abort"], warden.MovementRecoveryOrder);
        Assert.Equal("OnPerceptionDetected", warden.OnPerceptionDetectedHandler);
        Assert.Equal("OnPerceptionLost", warden.OnPerceptionLostHandler);
        Assert.Equal("BeforePhysicalAttacked", warden.BeforePhysicalAttackedHandler);
        Assert.Equal("OnPhysicalAttacked", warden.OnPhysicalAttackedHandler);
        Assert.Equal("BeforeAbilityTargeted", warden.BeforeAbilityTargetedHandler);
        Assert.Equal("OnSpellCastAt", warden.OnSpellCastAtHandler);
        Assert.Equal("OnBlocked", warden.OnBlockedHandler);
        Assert.Empty(missing.Items);
        Assert.True(planned.Succeeded);
        Assert.NotNull(planned.SemanticDiff);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        Assert.Throws<ArgumentOutOfRangeException>(() => workspace.Project.QueryCreatureFoundations(limit: 101));
    }

    [Fact]
    public void ItemFoundationResourceIsTypedFilteredAndPlanIsDryRun()
    {
        using var workspace = new TestWorkspace(project =>
        {
            var item = project.Blueprints.Single(candidate => candidate.Slug == "ash-key");
            var typeId = Guid.Parse("c2470000-0000-4000-8000-000000000001");
            var profile = new ItemFoundationProfile(Guid.Parse("c2470000-0000-4000-8000-000000000002"), 1,
                "test:r044", true, "crypt-of-ashes", "CC0-1.0", new string('e', 64), typeId,
                new(typeId, 1, "test:r044", "ritual-key", ["hand"], ["utility"], "rig.hand"),
                item.Slug, "asset.items.ash-key", "rig.hand", 2, 2, 4, 75, ["hand"],
                [new(Guid.Parse("c2470000-0000-4000-8000-000000000003"), "utility", 1,
                    "effect.ash-key", "OnUse")],
                [new("fr", "Clé des cendres", "Une clé.", "Une clé rituelle.", "Objet inconnu", "Clé", "")],
                ["identified", "durability"],
                new(Guid.Parse("c2880000-0000-4000-8000-000000000041"), 1, "test:r088", true,
                    "crypt-of-ashes", "CC0-1.0", new string('8', 64),
                    new(["archetype", "appearance", "slots", "quality", "level", "property", "provenance",
                        "generation-rules"], ["fixed-seed", "preserve-provenance"]),
                    new(Guid.Parse("c2880000-0000-4000-8000-000000000042"), 1, "test:r088",
                        ["asset.items.ash-key.mesh"], ["asset.materials.iron"], ["hand-r"],
                        "asset.items.ash-key.icon", ["asset.vfx.ash"], "asset.items.ash-key.equipped"),
                    new(1.25, .4, 1, 2, .5, .25), new(10, "ash-key", true),
                    new(3, 3, 2, 2, 5_000, 100, false, "rest-or-refill")));
            profile = profile with { Gameplay = ItemGameplayFixture(), Operations = ItemOperationsFixture() };
            return new ProjectDefinition(project.Id, project.Title, project.Module,
                project.Blueprints.Select(candidate => candidate.Id == item.Id
                    ? candidate.WithItemFoundation(profile)
                    : candidate).ToArray(), project.Areas, project.Dialogues, project.Quests);
        });
        var all = workspace.Project.QueryItemFoundations();
        var filtered = workspace.Project.QueryItemFoundations("ritual-key", 1);
        var missing = workspace.Project.QueryItemFoundations("does-not-exist", 1);
        var canonical = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Blueprints
            .Single(item => item.Slug == "ash-key");
        var planned = workspace.Project.PreviewItemFoundation(new(1, 0, canonical.Id.ToString(),
            canonical.ItemFoundation!));

        var item = Assert.Single(all.Items);
        Assert.Equal("ash-key", item.BlueprintSlug);
        Assert.Equal("ritual-key", item.TypeSlug);
        Assert.Equal(2, item.DefinitionVersion);
        Assert.Contains("fr", item.Locales);
        Assert.Equal("ash-key", item.Creation!.StackCompatibilityKey);
        Assert.Contains("asset.items.ash-key.mesh", item.Creation.MeshAssetIds);
        Assert.Equal(5_000, item.Creation.CooldownMilliseconds);
        Assert.True(item.Gameplay!.CriticalProtections["destruction"]);
        Assert.Equal(8, item.Gameplay.TransferDestinations.Count);
        Assert.Contains("ward", item.Gameplay.ModifierKinds);
        Assert.Equal(2, item.Gameplay.RequiredSlots.Count);
        Assert.Equal(1, item.Operations!.LootEntryCount);
        Assert.Equal(1, item.Operations.InventoryPlacementCount);
        Assert.Equal(9, item.Operations.LifecycleEvents.Count);
        Assert.Single(filtered.Items);
        Assert.Empty(missing.Items);
        Assert.True(planned.Succeeded);
        Assert.NotNull(planned.SemanticDiff);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        Assert.Throws<ArgumentOutOfRangeException>(() => workspace.Project.QueryItemFoundations(limit: 101));
    }

    private static ItemGameplayProfile ItemGameplayFixture() => new(
        Guid.Parse("c2890000-0000-4000-8000-000000000041"), 1, "test:r089", true,
        "crypt-of-ashes", "CC0-1.0", new string('9', 64), new(true, true, true, true, true, true, true),
        new(Enum.GetValues<ItemTransferDestination>().ToDictionary(item => item,
            item => item is ItemTransferDestination.Container or ItemTransferDestination.GameMaster),
            ["item.transfer.quest"]),
        [new(Guid.Parse("c2890000-0000-4000-8000-000000000042"), 1, "test:r089", "ward",
            new Dictionary<string, double> { ["value"] = 8 }, "highest", "night", "+8 ward",
            "BeforeItemModifier", "AfterItemModifier")],
        new(Guid.Parse("c2890000-0000-4000-8000-000000000043"), "point",
            new Dictionary<string, double> { ["focus"] = 1 }, 1, 12_000,
            "ItemAbilityRequested", "ItemAbilityResolved"),
        new([new("hand-r", "forge", "humanoid", 2, ["hands-two"]),
            new("hand-l", "forge", "humanoid", 2, ["hands-two"]),
            new("hands-two", "forge", "humanoid", 3, ["hand-r", "hand-l"])], ["hand-r", "hand-l"]));

    private static ItemOperationsProfile ItemOperationsFixture() => new(Guid.NewGuid(), 1, "test:r090", true,
        "crypt-of-ashes", "CC0-1.0", new string('7', 64),
        new(Guid.NewGuid(), 1, "test:r090",
        [new(Guid.NewGuid(), "relic", 1, 1, 1, 2, "crypt-open", true)]),
        new(Guid.NewGuid(), 1, "test:r090", 0,
        [new(Guid.NewGuid(), "backpack", null, 0, 0, 1, ["quest"], "asset.items.ash-key.preview")],
            true, true, true),
        new("1.0", Enum.GetValues<ItemLifecycleEventType>().Select(kind =>
            new ItemLifecycleBindingPolicy(Guid.NewGuid(), 1, "test:r090", kind, $"On{kind}", "server")).ToArray()));

    [Fact]
    public void WorkerSecurityMcpRequiresPlanPermissionAndPersistsThenRevertsCanonicalPolicy()
    {
        using var workspace = new TestWorkspace();
        var current = workspace.Project.GetExternalWorkerSecurity();
        var updated = current.Policy with
        {
            MaximumJobSeconds = 180,
            MaximumOutputBytes = 256L * 1024 * 1024,
            Provenance = "mcp:g12-l:test",
        };
        var path = Path.Combine(workspace.RootDirectory,
            ExternalWorkerSecurityPolicyStore.RelativePath.Replace('/', Path.DirectorySeparatorChar));

        var denied = workspace.Project.PlanExternalWorkerSecurity(new(
            1, current.Revision, updated, AssistantPermissionLevel.Propose));
        var unsafePlan = workspace.Project.PlanExternalWorkerSecurity(new(
            1, current.Revision, updated with { DenyShell = false }, AssistantPermissionLevel.Configure));
        var missing = workspace.Project.ApplyExternalWorkerSecurityPlan(Guid.NewGuid().ToString("D"));
        var planned = workspace.Project.PlanExternalWorkerSecurity(new(
            1, current.Revision, updated, AssistantPermissionLevel.Configure));

        Assert.Empty(current.Diagnostics);
        Assert.True(current.Policy.DenyFreeFormPython);
        Assert.Equal(SetExternalWorkerSecurityPolicyHandler.PermissionDeniedCode, Assert.Single(denied.Diagnostics).Code);
        Assert.Equal(ExternalWorkerSecurityPolicyValidator.UnsafeExecutionCode, Assert.Single(unsafePlan.Diagnostics).Code);
        Assert.Equal("asset.worker_security.plan_missing", Assert.Single(missing.Diagnostics).Code);
        Assert.True(planned.Succeeded);
        Assert.False(planned.Applied);
        Assert.NotNull(planned.SemanticDiff);
        Assert.NotNull(planned.Inverse);
        Assert.False(string.IsNullOrWhiteSpace(planned.PlanId));
        Assert.False(string.IsNullOrWhiteSpace(planned.TransactionId));
        Assert.Equal(0, planned.EstimatedCostUsd);
        Assert.False(File.Exists(path));

        var applied = workspace.Project.ApplyExternalWorkerSecurityPlan(planned.PlanId!);
        var reloaded = ExternalWorkerSecurityPolicyStore.Load(workspace.RootDirectory);

        Assert.True(applied.Succeeded);
        Assert.True(applied.Applied);
        Assert.True(File.Exists(path));
        Assert.True(reloaded.Succeeded);
        Assert.Equal(180, reloaded.Policy.MaximumJobSeconds);
        Assert.True(reloaded.Policy.DenyShell);
        Assert.False(reloaded.Policy.NetworkAllowed);

        var reverted = workspace.Project.RevertExternalWorkerSecurityPlan(planned.PlanId!);
        var restored = ExternalWorkerSecurityPolicyStore.Load(workspace.RootDirectory);

        Assert.True(reverted.Succeeded);
        Assert.True(reverted.Reverted);
        Assert.Equal(300, restored.Policy.MaximumJobSeconds);
        Assert.Equal(["planned", "apply", "revert"], reverted.Audit.Select(item => item.Action));
    }

    [Fact]
    public void AssistedModel3DMcpStagesGovernedSourceAppliesAndRevertsFourFiles()
    {
        using var workspace = new TestWorkspace();
        var brief = new AssistedModel3DGenerationBrief(
            Guid.Parse("b1330000-0000-4000-8000-000000000090"), "mcp-forge-idol", "MCP Forge Idol",
            Model3DInputKind.Text, "A compact weathered forge idol", null,
            Model3DProviderDefinition.CreateOffline(), new Model3DTopologyPolicy(10_000, [.5m, .25m], true),
            ["lore:crypt-of-ashes"], ["style:readable", "performance:realtime"], "@mcp-author", 20260806);

        var proposed = workspace.Project.ProposeAssistedModel3DGeneration(new(1, brief));
        Assert.True(proposed.Succeeded,
            string.Join(" | ", proposed.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        Assert.False(proposed.Proposal!.CanApply);
        var approved = proposed.Proposal.WithApproval(true, "author-approved:mcp-test:g12-k");
        var planned = workspace.Project.PlanAssistedModel3DGeneration(new(
            1, proposed.BaseRevision, approved, AssistantPermissionLevel.ApplyApprovedPlan));
        var sourcePath = AssistedModel3DGenerationService.ResolvePath(workspace.RootDirectory,
            approved.Receipt.SourceRelativePath, "assets/staging/ai-3d/");
        var receiptPath = AssistedModel3DGenerationService.ResolvePath(workspace.RootDirectory,
            approved.ReceiptRelativePath, "assets/staging/ai-3d/");
        var requestPath = Path.Combine(workspace.RootDirectory, "assets", "roundtrip",
            $"{approved.Configuration.AssetId}.worker-request.json");
        var externalPath = Path.Combine(workspace.RootDirectory,
            AssetExternalEditStore.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        var applied = workspace.Project.ApplyAssistedModel3DGenerationPlan(planned.PlanId!);

        Assert.Equal(5, proposed.Proposal.Tests.Count);
        Assert.All(proposed.Proposal.Tests, item => Assert.True(item.Passed));
        Assert.True(planned.Succeeded);
        Assert.False(planned.Applied);
        Assert.Equal(4, planned.FileDiff.Count);
        Assert.Equal("deterministic-offline-3d", planned.ProviderKey);
        Assert.Equal("CC0-1.0", planned.LicenseSpdx);
        Assert.True(applied.Succeeded);
        Assert.True(applied.Applied);
        Assert.True(File.Exists(sourcePath));
        Assert.True(File.Exists(receiptPath));
        Assert.True(File.Exists(requestPath));
        Assert.True(File.Exists(externalPath));
        Assert.Contains("\"humanAccepted\": true", File.ReadAllText(receiptPath), StringComparison.Ordinal);
        using (var requestDocument = JsonDocument.Parse(File.ReadAllText(requestPath)))
        {
            Assert.DoesNotContain(requestDocument.RootElement.GetProperty("operations").EnumerateArray(),
                item => item.GetString() == "create_primitives");
            Assert.True(requestDocument.RootElement.GetProperty("securityPolicy")
                .GetProperty("denyFreeFormPython").GetBoolean());
        }

        var reverted = workspace.Project.RevertAssistedModel3DGenerationPlan(planned.PlanId!);

        Assert.True(reverted.Succeeded);
        Assert.True(reverted.Reverted);
        Assert.False(File.Exists(sourcePath));
        Assert.False(File.Exists(receiptPath));
        Assert.False(File.Exists(requestPath));
        Assert.False(File.Exists(externalPath));
        Assert.Equal(["planned", "apply", "revert"], reverted.Audit.Select(item => item.Action));
    }

    [Fact]
    public void AssistedBlenderMcpPlansPersistsTypedWorkerRequestAndRevertsAllFiles()
    {
        using var workspace = new TestWorkspace();
        var brief = new AssistedBlenderCreationBrief(
            Guid.Parse("b1320000-0000-4000-8000-000000000090"),
            "mcp-forge-pillar", "MCP Forge Pillar",
            [new BlenderPrimitiveDefinition(
                Guid.Parse("b1320000-0000-4000-8000-000000000091"),
                "pillar", BlenderPrimitiveKind.Cylinder,
                new BlenderVector3(0, 0, 1), new BlenderVector3(0, 0, 0), new BlenderVector3(1, 1, 2))],
            new BlenderMaterialDefinition("forge_stone", new BlenderColor(.32m, .24m, .18m, 1), .05m, .72m),
            BlenderUvProjection.Cube, [.5m, .25m], true,
            ["lore:crypt-of-ashes"], ["style:readable", "performance:realtime"], "@mcp-author", 20260806);

        var proposed = workspace.Project.ProposeAssistedBlenderCreation(new(1, brief));
        Assert.True(proposed.Proposal?.CanApply == true,
            string.Join(" | ", proposed.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        var approved = proposed.Proposal!.WithApproval(true, "author-approved:mcp-test:g12-j");
        var planned = workspace.Project.PlanAssistedBlenderCreation(new(
            1, proposed.BaseRevision, approved, AssistantPermissionLevel.ApplyApprovedPlan));
        var recipePath = AssistedBlenderCreationService.ResolvePath(workspace.RootDirectory, approved.RecipeRelativePath);
        var requestPath = Path.Combine(workspace.RootDirectory, "assets", "roundtrip",
            $"{approved.Configuration.AssetId}.worker-request.json");
        var externalPath = Path.Combine(workspace.RootDirectory, AssetExternalEditStore.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        var applied = workspace.Project.ApplyAssistedBlenderCreationPlan(planned.PlanId!);

        Assert.True(proposed.Succeeded);
        Assert.Equal(5, proposed.Proposal.Tests.Count);
        Assert.All(proposed.Proposal.Tests, item => Assert.True(item.Passed));
        Assert.True(planned.Succeeded);
        Assert.False(planned.Applied);
        Assert.Equal(3, planned.FileDiff.Count);
        Assert.Equal(5, planned.PassedTests);
        Assert.True(applied.Succeeded);
        Assert.True(applied.Applied);
        Assert.True(File.Exists(recipePath));
        Assert.True(File.Exists(requestPath));
        Assert.True(File.Exists(externalPath));
        Assert.Contains("\"create_primitives\"", File.ReadAllText(requestPath), StringComparison.Ordinal);
        Assert.Contains("\"denyShell\": true", File.ReadAllText(requestPath), StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"cylinder\"", File.ReadAllText(recipePath), StringComparison.Ordinal);
        Assert.Single(AssetExternalEditStore.Load(workspace.RootDirectory).Configurations,
            item => item.Operations.Contains(AssetExternalOperation.CreatePrimitives));

        var reverted = workspace.Project.RevertAssistedBlenderCreationPlan(planned.PlanId!);

        Assert.True(reverted.Succeeded);
        Assert.True(reverted.Reverted);
        Assert.False(File.Exists(recipePath));
        Assert.False(File.Exists(requestPath));
        Assert.False(File.Exists(externalPath));
        Assert.Equal(["planned", "apply", "revert"], reverted.Audit.Select(item => item.Action));
    }

    [Fact]
    public void AssistedAbilityMcpTestsPersistsCanonicalCombatMutationAndRevertsIt()
    {
        using var workspace = new TestWorkspace();
        var scriptsDirectory = Path.Combine(workspace.RootDirectory, "scripts");
        Directory.CreateDirectory(scriptsDirectory);
        File.WriteAllText(Path.Combine(scriptsDirectory, "assisted_ability.forge"), "void main() {}\n");
        var before = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var owner = before.Blueprints.First(item => item.Combat is not null && !item.IsReadOnly());
        var item = before.Blueprints.FirstOrDefault(candidate => candidate.Kind == BlueprintKind.Item);
        var beforeAbilityCount = before.Blueprints.Where(candidate => candidate.Combat is not null)
            .Sum(candidate => candidate.Combat!.Abilities.Count);
        var brief = new AssistedAbilityBrief(
            Guid.Parse("b1300000-0000-4000-8000-000000000090"), owner.Id.Value,
            "lance-mcp", "Lance MCP", 0, 5, AbilityTargeting.SingleEnemy,
            [new AbilityEffect(AbilityEffectKind.Damage, 16)],
            "vfx.ability.mcp-lance", "audio.ability.mcp-lance", "scripts/assisted_ability.forge",
            item?.Slug, new HashSet<string>(["ranged", "finisher"], StringComparer.Ordinal),
            "Attaque concentrée avec un coût et une recharge lisibles.",
            ["lore:crypt-of-ashes"], ["style:crypt", "combat:telegraphed"], "@mcp-author", 20260806);

        var proposed = workspace.Project.ProposeAssistedAbility(new(1, brief));
        Assert.True(proposed.Proposal?.CanApply == true,
            string.Join(" | ", proposed.Proposal?.Diagnostics.Select(diagnostic =>
                $"{diagnostic.Code}: {diagnostic.Message}") ?? []));
        var approved = proposed.Proposal!.WithApproval(true, "author-approved:mcp-test:g12-i");
        var planned = workspace.Project.PlanAssistedAbility(new(1, workspace.Project.GetCurrentRevision(), approved,
            AssistantPermissionLevel.ApplyApprovedPlan));
        Assert.True(planned.Succeeded,
            string.Join(" | ", planned.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
        var applied = workspace.Project.ApplyAssistedAbilityPlan(planned.PlanId!);
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var reverted = workspace.Project.RevertAssistedAbilityPlan(planned.PlanId!);
        var restored = ProjectSession.Open(workspace.RootDirectory).Session!.Project;

        Assert.True(proposed.Succeeded);
        Assert.True(proposed.Proposal.CanApply);
        Assert.Equal(5, proposed.Proposal.Tests.Count);
        Assert.All(proposed.Proposal.Tests, scenario => Assert.True(scenario.Passed));
        Assert.True(proposed.Proposal.Balance.Passed);
        Assert.True(planned.Succeeded);
        Assert.False(planned.Applied);
        Assert.NotNull(planned.Diff);
        Assert.Equal(5, planned.PassedTests);
        Assert.True(planned.BalancePassed);
        Assert.True(applied.Succeeded);
        Assert.True(applied.Applied);
        Assert.Equal(beforeAbilityCount + 1, persisted.Blueprints.Where(candidate => candidate.Combat is not null)
            .Sum(candidate => candidate.Combat!.Abilities.Count));
        Assert.Equal(before.Module.RuntimePolicies.Extensions.JsonDocuments.Count + 1,
            persisted.Module.RuntimePolicies.Extensions.JsonDocuments.Count);
        Assert.Contains(persisted.Module.RuntimePolicies.Extensions.JsonDocuments,
            document => document.Key == "ability.lance-mcp" &&
                        document.InitialValue.Contains("\"tests\"", StringComparison.Ordinal));
        Assert.True(reverted.Succeeded);
        Assert.True(reverted.Reverted);
        Assert.Equal(beforeAbilityCount, restored.Blueprints.Where(candidate => candidate.Combat is not null)
            .Sum(candidate => candidate.Combat!.Abilities.Count));
        Assert.Equal(before.Module.RuntimePolicies.Extensions.JsonDocuments.Count,
            restored.Module.RuntimePolicies.Extensions.JsonDocuments.Count);
    }

    [Fact]
    public void AssistedBossMcpProposesSimulatesPersistsAtomicPackageAndRevertsIt()
    {
        using var workspace = new TestWorkspace();
        var scriptsDirectory = Path.Combine(workspace.RootDirectory, "scripts");
        Directory.CreateDirectory(scriptsDirectory);
        foreach (var name in new[] { "boss_phase_one.forge", "boss_phase_two.forge", "boss_death.forge" })
            File.WriteAllText(Path.Combine(scriptsDirectory, name), "void main() {}\n");
        var before = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var area = before.Areas[0];
        var summon = before.Blueprints.First(item => item.Kind == BlueprintKind.Creature);
        var brief = new AssistedBossBrief(
            Guid.Parse("b1290000-0000-4000-8000-000000000090"),
            Guid.Parse("b1290000-0000-4000-8000-000000000091"),
            "souveraine-mcp", "Souveraine MCP", area.Id.Value,
            "La souveraine change de tactique au cœur de l'arène.",
            480, 24, 12, 18,
            ["Trait de braise", "Cercle de feu", "Couronne ardente"],
            ["scripts/boss_phase_one.forge", "scripts/boss_phase_two.forge"],
            [summon.Slug], ["crown-ember", "royal-ash"], "scripts/boss_death.forge",
            ["lore:crypt-of-ashes"], ["style:crypt", "combat:telegraphed"], "@mcp-author", 20260806);

        var proposed = workspace.Project.ProposeAssistedBoss(new(1, brief));
        var approved = proposed.Proposal!.WithApproval(true, "author-approved:mcp-test:g12-h");
        var planned = workspace.Project.PlanAssistedBoss(new(1, workspace.Project.GetCurrentRevision(), approved,
            AssistantPermissionLevel.ApplyApprovedPlan));
        var applied = workspace.Project.ApplyAssistedBossPlan(planned.PlanId!);
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var reverted = workspace.Project.RevertAssistedBossPlan(planned.PlanId!);
        var restored = ProjectSession.Open(workspace.RootDirectory).Session!.Project;

        Assert.True(proposed.Succeeded);
        Assert.True(proposed.Proposal.CanApply);
        Assert.Equal(32, proposed.Proposal.Balance.Runs);
        Assert.True(proposed.Proposal.Balance.Passed);
        Assert.True(planned.Succeeded);
        Assert.False(planned.Applied);
        Assert.NotNull(planned.Diff);
        Assert.Equal(32, planned.BalanceRuns);
        Assert.True(planned.BalancePassed);
        Assert.True(applied.Succeeded);
        Assert.True(applied.Applied);
        Assert.Equal(before.Blueprints.Count + 1, persisted.Blueprints.Count);
        Assert.Equal(before.Dialogues.Count + 1, persisted.Dialogues.Count);
        Assert.Equal(before.Module.RuntimePolicies.Extensions.JsonDocuments.Count + 1,
            persisted.Module.RuntimePolicies.Extensions.JsonDocuments.Count);
        Assert.Contains(persisted.Module.RuntimePolicies.Extensions.JsonDocuments,
            item => item.Key == "boss.souveraine-mcp" && item.InitialValue.Contains("\"phases\"", StringComparison.Ordinal));
        Assert.True(reverted.Succeeded);
        Assert.True(reverted.Reverted);
        Assert.Equal(before.Blueprints.Count, restored.Blueprints.Count);
        Assert.Equal(before.Dialogues.Count, restored.Dialogues.Count);
        Assert.Equal(before.Module.RuntimePolicies.Extensions.JsonDocuments.Count,
            restored.Module.RuntimePolicies.Extensions.JsonDocuments.Count);
    }

    [Fact]
    public void AssistedForgeScriptMcpProposesCompilesTestsPersistsAndRevertsSource()
    {
        using var workspace = new TestWorkspace();
        var relativePath = "scripts/assisted-mcp-test.forge";
        var absolutePath = AssistedForgeScriptCreationService.ResolvePath(workspace.RootDirectory, relativePath);
        var brief = new AssistedForgeScriptBrief(
            Guid.Parse("b1280000-0000-4000-8000-000000000090"),
            relativePath,
            "OnUse",
            "Informer le joueur et enregistrer une variable lors de l'interaction.",
            "ForgeScript MCP prêt.",
            "assistant.mcp.used",
            "true",
            ["lore:crypt-of-ashes"],
            ["style:brief"],
            "@mcp-author",
            20260806);

        var proposed = workspace.Project.ProposeAssistedForgeScript(new(1, brief));
        var approved = proposed.Proposal!.WithApproval(true, "author-approved:mcp-test:g12-g");
        var planned = workspace.Project.PlanAssistedForgeScript(new(
            1,
            workspace.Project.GetCurrentRevision(),
            approved,
            AssistantPermissionLevel.ApplyApprovedPlan));
        var applied = workspace.Project.ApplyAssistedForgeScriptPlan(planned.PlanId!);
        var persisted = File.ReadAllText(absolutePath);
        var compiled = new NativeForgeScriptRuntime().Compile(persisted);
        var reverted = workspace.Project.RevertAssistedForgeScriptPlan(planned.PlanId!);

        Assert.True(proposed.Succeeded);
        Assert.True(proposed.Proposal.CanApply);
        Assert.Equal("1.0.0", proposed.Proposal.ForgeScriptApiVersion);
        Assert.Equal(3, proposed.Proposal.Tests.Count);
        Assert.True(planned.Succeeded);
        Assert.False(planned.Applied);
        Assert.NotNull(planned.Diff);
        Assert.Equal(3, planned.TestIds.Count);
        Assert.True(applied.Succeeded);
        Assert.True(applied.Applied);
        Assert.True(compiled.Success);
        Assert.True(reverted.Succeeded);
        Assert.True(reverted.Reverted);
        Assert.False(File.Exists(absolutePath));
    }

    [Fact]
    public void AssistedQuestMcpProposesPlansPersistsAndRevertsCanonicalQuest()
    {
        using var workspace = new TestWorkspace();
        var project = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var area = project.Areas[0];
        var participant = project.Blueprints.First(item => item.Kind == BlueprintKind.Creature);
        var questsBefore = project.Quests.Count;
        var brief = new AssistedQuestBrief(
            Guid.Parse("b1270000-0000-4000-8000-000000000090"),
            "quete-mcp-assistee",
            "Quête MCP assistée",
            area.Id.Value,
            participant.Id.Value,
            "Le gardien demande de récupérer les braises avant l'effondrement.",
            ["Trouver les braises", "Revenir auprès du gardien"],
            ["lore:crypt-of-ashes"],
            ["style:crypt"],
            "quest.mcp-assistee",
            "@mcp-author",
            20260806,
            250);

        var proposed = workspace.Project.ProposeAssistedQuest(new(1, brief));
        var approved = proposed.Proposal!.WithApproval(true, "author-approved:mcp-test:g12-f");
        var planned = workspace.Project.PlanAssistedQuest(new(
            1,
            workspace.Project.GetCurrentRevision(),
            approved,
            AssistantPermissionLevel.ApplyApprovedPlan));
        var applied = workspace.Project.ApplyAssistedQuestPlan(planned.PlanId!);
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var reverted = workspace.Project.RevertAssistedQuestPlan(planned.PlanId!);
        var restored = ProjectSession.Open(workspace.RootDirectory).Session!.Project;

        Assert.True(proposed.Succeeded);
        Assert.True(proposed.Proposal.CanApply);
        Assert.Equal(4, proposed.Proposal.Quest.States.Count);
        Assert.Equal(3, proposed.Proposal.Quest.Transitions.Count);
        Assert.Equal(5, proposed.Proposal.TestScenarios.Count);
        Assert.Empty(proposed.Proposal.Lint);
        Assert.True(planned.Succeeded);
        Assert.False(planned.Applied);
        Assert.NotNull(planned.Diff);
        Assert.Equal(5, planned.TestScenarios.Count);
        Assert.True(applied.Succeeded);
        Assert.True(applied.Applied);
        Assert.Equal(questsBefore + 1, persisted.Quests.Count);
        Assert.True(reverted.Succeeded);
        Assert.True(reverted.Reverted);
        Assert.Equal(questsBefore, restored.Quests.Count);
    }

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
        Assert.Equal(665, result.CatalogCount);
        Assert.Equal(5, result.Sources.Count);
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
    public void BlueprintPalettePlanIsFilteredDryRunAppliedPersistedAndReverted()
    {
        using var workspace = new TestWorkspace();
        var opened = ProjectSession.Open(workspace.RootDirectory).Session!;
        var entity = opened.Project.Areas.SelectMany(area => area.Entities).First();
        var blueprint = opened.Project.Blueprints.Single(item => item.Id == entity.BlueprintId);

        var resource = workspace.Project.QueryBlueprintPalette(blueprint.Id.ToString(), 10);
        var planned = workspace.Project.PlanBlueprintUpdate(new BlueprintUpdatePlanRequest(
            1, 0, blueprint.Id.ToString(), entity.Id.ToString(), [], "Patch", [], ["Monde/Test"]));

        Assert.Equal(blueprint.Id.ToString(), Assert.Single(resource.Items).BlueprintId);
        Assert.True(planned.Succeeded, string.Join(" | ", planned.Diagnostics.Select(item => item.Message)));
        Assert.Equal("1.0.1", planned.TargetVersion);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        Assert.Equal("1.0.0", ProjectSession.Open(workspace.RootDirectory).Session!.Project.Blueprints
            .Single(item => item.Id == blueprint.Id).Authoring.Version);

        var applied = workspace.Project.ApplyBlueprintUpdatePlan(planned.PlanId!);
        Assert.True(applied.Applied);
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Blueprints
            .Single(item => item.Id == blueprint.Id);
        Assert.Equal("1.0.1", persisted.Authoring.Version);
        Assert.Equal(["Monde/Test"], persisted.Authoring.CategoryPaths);

        var reverted = workspace.Project.RevertBlueprintUpdatePlan(planned.PlanId!);
        Assert.True(reverted.Reverted);
        Assert.Equal(["planned", "apply", "revert"], reverted.Audit.Select(item => item.Action));
        Assert.Equal("1.0.0", ProjectSession.Open(workspace.RootDirectory).Session!.Project.Blueprints
            .Single(item => item.Id == blueprint.Id).Authoring.Version);
    }

    [Fact]
    public void BlueprintPrefabPlanIsFilteredDryRunAppliedPersistedAndReverted()
    {
        using var workspace = new TestWorkspace();
        var project = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var group = project.Blueprints.Where(item => !item.IsReadOnly()).GroupBy(item => item.Kind)
            .First(items => items.Count() >= 2);
        var target = group.First();
        var baseBlueprint = group.Skip(1).First();
        var nodeId = Guid.Parse("b0580000-0000-4000-8000-000000000501");
        var request = new BlueprintPrefabPlanRequest(1, 0, target.Id.ToString(), baseBlueprint.Id.ToString(),
            [new AgentPrefabVariantOverride("Material", "body", "asset:ember"),
             new AgentPrefabVariantOverride("Stat", "armor", "18")], true,
            [new AgentPrefabCompositionNode(nodeId.ToString("D"), baseBlueprint.Id.ToString(), null, "floor",
                2, 0, 3)], ["socket-compatible"], ["intensity"], ["scripts/assemble.as"], true,
            new AgentAssetBudgetMetrics(12_000, 2, 3, 8_000_000, 1, 3, 2),
            [new AgentAssetPlatformBudget("Windows", 20_000, 4, 6, 16_000_000, 2, 4, 3, true)],
            true, ["Crypt/Architecture", "Smoke/Prefabs"]);

        var planned = workspace.Project.PlanBlueprintPrefab(request);

        Assert.True(planned.Succeeded, string.Join(" | ", planned.Diagnostics.Select(item => item.Message)));
        Assert.Equal("replace", planned.Diff?.Operation);
        Assert.Equal(2, planned.Preview!.VariantOverrides.Count);
        Assert.Single(planned.Preview.CompositionNodes);
        Assert.Single(planned.Preview.PlatformBudgets);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        Assert.Empty(workspace.Project.QueryBlueprintPrefabs(target.Id.ToString(), 10).Items);
        Assert.Null(ProjectSession.Open(workspace.RootDirectory).Session!.Project.Blueprints
            .Single(item => item.Id == target.Id).Authoring.PrefabVariant);

        var applied = workspace.Project.ApplyBlueprintPrefabPlan(planned.PlanId!);

        Assert.True(applied.Applied);
        var resource = workspace.Project.QueryBlueprintPrefabs("Smoke/Prefabs", 10);
        Assert.Equal(target.Id.ToString(), Assert.Single(resource.Items).BlueprintId);
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Blueprints
            .Single(item => item.Id == target.Id);
        Assert.Equal(baseBlueprint.Id, persisted.Authoring.PrefabVariant!.BaseBlueprintId);
        Assert.Single(persisted.Authoring.PrefabComposition!.Nodes);
        Assert.Single(persisted.Authoring.AssetBudget!.Platforms);

        var reverted = workspace.Project.RevertBlueprintPrefabPlan(planned.PlanId!);

        Assert.True(reverted.Reverted);
        Assert.Equal(["planned", "apply", "revert"], reverted.Audit.Select(item => item.Action));
        var restored = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Blueprints
            .Single(item => item.Id == target.Id);
        Assert.Null(restored.Authoring.PrefabVariant);
        Assert.Null(restored.Authoring.PrefabComposition);
        Assert.Null(restored.Authoring.AssetBudget);
    }

    [Fact]
    public void EditorProductivityPlanIsSourcedDryRunAppliedPersistedAndReverted()
    {
        using var workspace = new TestWorkspace();
        var project = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var areaId = project.Module.EntryAreaId.ToString();
        var initial = workspace.Project.QueryEditorProductivity("placement");
        Assert.NotEmpty(initial.HelpTopics);
        Assert.All(initial.HelpTopics, topic => Assert.NotEmpty(topic.Citations));
        var request = new EditorProductivityPlanRequest(1, 0, "Artist", [], ["Trigger", "Navigation"],
            true, true, "DeterministicScatter", "SurfaceNormal", 1.5, 5, 2026, 0.1,
            false, "Horizontal", 0.5, areaId, null, true, true, "placement.repeated");

        var planned = workspace.Project.PlanEditorProductivity(request);

        Assert.True(planned.Succeeded, string.Join(" | ", planned.Diagnostics.Select(item => item.Message)));
        Assert.Equal("replace", planned.Diff?.Operation);
        Assert.Equal("Artist", planned.Preview!.Role);
        Assert.True(planned.Preview.BrushMode);
        Assert.Equal("SurfaceNormal", planned.Preview.Alignment);
        Assert.False(File.Exists(Path.Combine(workspace.RootDirectory, EditorWorkspaceProfileStore.RelativePath)));

        var applied = workspace.Project.ApplyEditorProductivityPlan(planned.PlanId!);

        Assert.True(applied.Applied);
        var persisted = EditorWorkspaceProfileStore.TryLoad(workspace.RootDirectory);
        Assert.NotNull(persisted);
        Assert.Equal(Forge.Domain.Authoring.EditorRole.Artist, persisted.Role);
        Assert.True(persisted.RepeatedPlacementProfile.ContinuousStroke);
        Assert.True(workspace.Project.QueryEditorProductivity().ActiveProfile.SourcedAssistant);

        var reverted = workspace.Project.RevertEditorProductivityPlan(planned.PlanId!);

        Assert.True(reverted.Reverted);
        Assert.Equal(["planned", "apply", "revert"], reverted.Audit.Select(item => item.Operation));
        Assert.Equal(Forge.Domain.Authoring.EditorRole.Builder,
            EditorWorkspaceProfileStore.TryLoad(workspace.RootDirectory)!.Role);
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
    public void R051ProjectGameplayEventsAreFilteredAndUseCanonicalDryRunApplyRevert()
    {
        using var workspace = new TestWorkspace();
        var runtime = workspace.Project.GetRuntimePolicies();
        var all = workspace.Project.QueryProjectGameplayEvents();
        var ready = workspace.Project.QueryProjectGameplayEvents("player.ready");
        var missing = workspace.Project.QueryProjectGameplayEvents("unknown.event");

        Assert.True(all.Found);
        Assert.Equal(14, all.Bindings.Count);
        Assert.Equal("player.ready", Assert.Single(ready.Bindings).EventName);
        Assert.False(missing.Found);
        Assert.Equal("project.events.not_found", Assert.Single(missing.Diagnostics).Code);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());

        var changedEvents = new ProjectGameplayEventProfile(
            runtime.Module.GameplayEvents.Id,
            1,
            "mcp:r051",
            true,
            true,
            false,
            false,
            true,
            runtime.Module.GameplayEvents.Bindings.Select(binding => binding with
            {
                Provenance = "mcp:r051",
                Handler = binding.EventName == "player.ready" ? "OnPlayerReadyMcp" : binding.Handler,
            }).ToArray());
        var module = new ModuleRuntimePolicyProfile(runtime.Module.Id, runtime.Module.SchemaVersion,
            runtime.Module.Heartbeats, runtime.Module.Rest, runtime.Module.SequenceAbort, "mcp:r051",
            runtime.Module.Cinematics, runtime.Module.Gameplay, runtime.Module.Rules, runtime.Module.Extensions,
            changedEvents);
        var planned = workspace.Project.PlanRuntimePolicies(new RuntimePolicyPlanRequest(
            1, runtime.Revision, runtime.AreaId, module, runtime.Area));

        Assert.True(planned.Succeeded);
        Assert.False(planned.Applied);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        var applied = workspace.Project.ApplyRuntimePolicyPlan(planned.PlanId!);
        Assert.True(applied.Applied);
        Assert.Equal("OnPlayerReadyMcp",
            Assert.Single(workspace.Project.QueryProjectGameplayEvents("OnPlayerReadyMcp").Bindings).Handler);
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Module.RuntimePolicies.GameplayEvents;
        Assert.True(persisted.RequiredAtRuntime);
        Assert.False(persisted.UseDownedState);
        Assert.False(persisted.AutoRespawn);

        var reverted = workspace.Project.RevertRuntimePolicyPlan(planned.PlanId!);
        Assert.True(reverted.Reverted);
        Assert.Equal(14, workspace.Project.QueryProjectGameplayEvents().Bindings.Count);
    }

    [Fact]
    public void R052ProjectWorldAndPlayerDocumentationUseFilteredCanonicalPlans()
    {
        using var workspace = new TestWorkspace();
        var runtime = workspace.Project.GetRuntimePolicies();
        var schema = new Forge.Domain.Lifecycle.CustomEventSchema(Guid.Parse("52000000-0000-4000-8000-000000000052"), 1,
            "mcp:r052", "crypt", "braziers_lit", 2,
            new Dictionary<string, string> { ["count"] = "int" }, false, "1.0", "OnBraziersLit",
            EventAuthorityScope.Server);
        var world = new ProjectWorldProfile(runtime.Module.Id, 1, "mcp:r052", true, true, [schema],
            runtime.Module.World.Clock with
            {
                Provenance = "mcp:r052",
                Speed = 4,
                AreaTimeZoneOffsets = new Dictionary<string, int> { [runtime.AreaId] = 60 },
            });
        var module = new ModuleRuntimePolicyProfile(runtime.Module.Id, runtime.Module.SchemaVersion,
            runtime.Module.Heartbeats, runtime.Module.Rest, runtime.Module.SequenceAbort, "mcp:r052",
            runtime.Module.Cinematics, runtime.Module.Gameplay, runtime.Module.Rules, runtime.Module.Extensions,
            runtime.Module.GameplayEvents, world);
        var worldPlan = workspace.Project.PlanRuntimePolicies(new RuntimePolicyPlanRequest(
            1, runtime.Revision, runtime.AreaId, module, runtime.Area));

        Assert.True(worldPlan.Succeeded);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        Assert.True(workspace.Project.ApplyRuntimePolicyPlan(worldPlan.PlanId!).Applied);
        var filtered = workspace.Project.QueryProjectWorld("braziers_lit");
        Assert.True(filtered.Succeeded);
        Assert.Equal("OnBraziersLit", Assert.Single(filtered.CustomEvents).Handler);
        Assert.False(workspace.Project.QueryProjectWorld("missing-event").Succeeded);
        Assert.Equal(4, ProjectSession.Open(workspace.RootDirectory).Session!.Project.Module.RuntimePolicies.World.Clock.Speed);
        Assert.True(workspace.Project.RevertRuntimePolicyPlan(worldPlan.PlanId!).Reverted);

        var metadata = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Module.Metadata;
        var localized = metadata.Localizations.Select(item => new ProjectLocalizedMetadata(item.Locale, item.Name,
            item.Summary, "Synopsis R052", "Crédits", "Installation", "Règles", "Contrôles R052",
            "Licences", "Avertissements", "Compatibilité")).ToArray();
        var docsPlan = workspace.Project.PlanProjectDocumentation(new ProjectDocumentationPlanRequest(
            1, workspace.Project.GetCurrentRevision(), metadata.DefaultLocale, localized));
        Assert.True(docsPlan.Succeeded);
        Assert.Equal(2, workspace.Project.GetCurrentRevision());
        Assert.True(workspace.Project.ApplyProjectDocumentationPlan(docsPlan.PlanId!).Applied);
        var docs = workspace.Project.QueryProjectDocumentation("Contrôles R052");
        Assert.True(docs.Succeeded);
        Assert.Equal(2, docs.Localizations.Count);
        Assert.All(docs.Localizations, item => Assert.Equal("Contrôles R052", item.Controls));
        Assert.Equal("Contrôles R052", ProjectSession.Open(workspace.RootDirectory).Session!.Project.Module.Metadata
            .GetDefaultLocalization().Controls);
        var docsReverted = workspace.Project.RevertProjectDocumentationPlan(docsPlan.PlanId!);
        Assert.True(docsReverted.Reverted);
        Assert.Equal(["planned", "apply", "revert"], docsReverted.Audit.Select(item => item.Action));
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
        var foundation = source.Foundation with
        {
            Cameras = source.Foundation.Cameras with { DefaultProfileKey = "isometric", Provenance = "mcp-test" },
            Provenance = "mcp-test",
        };
        var simulation = source.Simulation with
        {
            Targeting = source.Simulation.Targeting with { Mode = TargetingMode.Chain, Provenance = "mcp-test:r066" },
            Provenance = "mcp-test:r066",
        };
        var session = source.Session with
        {
            Persistence = source.Session.Persistence with
            {
                Scope = PersistenceScope.Server,
                Authority = SaveAuthority.Server,
                ConflictPolicy = SaveConflictPolicy.ServerWins,
                Provenance = "mcp-test:r067",
            },
            Provenance = "mcp-test:r067",
        };
        var profile = new ModuleGameplayControlProfile(source.Id, 1, source.Swimming, source.Climbing,
            source.RadialMenu, time, combat, "mcp-test", foundation, simulation, session);

        var cameraResources = workspace.Project.QueryGameplayFoundation("iso", "camera");
        Assert.Single(cameraResources.Items);
        Assert.Equal("isometric", cameraResources.Items[0].Key);
        var simulationResources = workspace.Project.QueryGameplayFoundation("flight", "simulation-flight");
        Assert.Single(simulationResources.Items);
        Assert.Equal(source.Simulation.Flight.Id, simulationResources.Items[0].Id);
        var sessionResources = workspace.Project.QueryGameplayFoundation("persistence", "session-persistence");
        Assert.Single(sessionResources.Items);
        Assert.Equal(source.Session.Persistence.Id, sessionResources.Items[0].Id);

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
        Assert.Equal("isometric", persisted.Foundation.Cameras.DefaultProfileKey);
        Assert.Equal(TargetingMode.Chain, persisted.Simulation.Targeting.Mode);
        Assert.Equal(PersistenceScope.Server, persisted.Session.Persistence.Scope);
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
        var sourcePackage = Assert.Single(source.Plugins.Packages);
        var package = new RulesetPluginPackage(sourcePackage.Id, 1, sourcePackage.Key, "1.1.0",
            sourcePackage.ContentPackageId, sourcePackage.Attributes, sourcePackage.Effects,
            sourcePackage.Abilities, sourcePackage.Inventories, sourcePackage.ProgressionTracks,
            sourcePackage.UiPanels, "mcp-test");
        var plugins = new RulesetPluginCatalog(source.Plugins.Id, 1, true, true, package.Key, [package], "mcp-test");
        var module = new ModuleRulesProfile(source.Id, 1, source.Progression, [difficulty], difficulty.Key,
            source.Mutators, "mcp-test", plugins);
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
        Assert.Equal("1.1.0", Assert.Single(persisted.Module.RuntimePolicies.Rules.Plugins.Packages).Version);
        Assert.Single(workspace.Project.QueryRulesetPlugins("health").Packages);
        var reverted = workspace.Project.RevertRulesPlan(planned.PlanId!);
        Assert.True(reverted.Reverted);
        Assert.Equal(["planned", "apply", "revert"], reverted.Audit.Select(item => item.Action));
    }

    [Fact]
    public void ProjectVariablePlanSupportsFilteredReadDryRunApplyPersistenceAndRevert()
    {
        using var workspace = new TestWorkspace();
        var variable = new ProjectVariableDefinition(ProjectVariableId.From(Guid.NewGuid()), "party.chapter",
            ProjectVariableValueKind.Integer, ProjectVariableScope.Party, "1", "Current chapter", "mcp-test", 1,
            ProjectVariableReplicationMode.Party, true, true);

        var planned = workspace.Project.PlanProjectVariables(
            new ProjectVariablePlanRequest(1, workspace.Project.GetCurrentRevision(), [variable]));

        Assert.True(planned.Succeeded);
        Assert.Equal("module/configuration", planned.Diff?.Path);
        Assert.Empty(workspace.Project.QueryProjectVariables("party.chapter").Variables);
        var applied = workspace.Project.ApplyProjectVariablePlan(planned.PlanId!);
        Assert.True(applied.Applied);
        var resource = workspace.Project.QueryProjectVariables("chapter", "party");
        var returned = Assert.Single(resource.Variables);
        Assert.Equal(ProjectVariableReplicationMode.Party, returned.Replication);
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Module.Variables;
        Assert.Equal("party.chapter", Assert.Single(persisted).Key);
        var reverted = workspace.Project.RevertProjectVariablePlan(planned.PlanId!);
        Assert.True(reverted.Reverted);
        Assert.Equal(["planned", "apply", "revert"], reverted.Audit.Select(item => item.Action));
    }

    [Fact]
    public void ProjectFoundationPlanSupportsMetadataReadDryRunApplyPersistenceAndRevert()
    {
        using var workspace = new TestWorkspace();
        var project = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var area = project.Areas[0];
        var spawn = area.Entities.Single(entity =>
            project.Blueprints.Single(blueprint => blueprint.Id == entity.BlueprintId).Kind == BlueprintKind.PlayerSpawn);
        var foundation = ProjectFoundationProfile.CreateRpgDefault(
            Guid.Parse("a0240000-0000-4000-8000-000000000001"), project.Id, project.Module.Slug,
            area.Id, spawn.Id, $"module.{project.Module.Slug}") with
        {
            RenderPreset = ProjectRenderPreset.Quality,
            NetworkMode = ProjectNetworkMode.PeerToPeer,
        };

        var before = workspace.Project.GetProjectFoundation();
        var planned = workspace.Project.PlanProjectFoundation(
            new ProjectFoundationPlanRequest(1, before.Revision, foundation));

        Assert.NotEmpty(before.Metadata.Localizations);
        Assert.Null(before.Foundation);
        Assert.True(planned.Succeeded);
        Assert.Equal("module/metadata/foundation", planned.Diff?.Path);
        Assert.Null(workspace.Project.GetProjectFoundation().Foundation);
        var applied = workspace.Project.ApplyProjectFoundationPlan(planned.PlanId!);
        Assert.True(applied.Applied);
        var resource = workspace.Project.GetProjectFoundation();
        Assert.Equal(ProjectRenderPreset.Quality, resource.Foundation!.RenderPreset);
        Assert.Equal(project.Id, resource.Identity!.ProjectId);
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Module.Metadata.Foundation;
        Assert.Equal(ProjectNetworkMode.PeerToPeer, persisted!.NetworkMode);
        var reverted = workspace.Project.RevertProjectFoundationPlan(planned.PlanId!);
        Assert.True(reverted.Reverted);
        Assert.Null(ProjectSession.Open(workspace.RootDirectory).Session!.Project.Module.Metadata.Foundation);
        Assert.Equal(["planned", "apply", "revert"], reverted.Audit.Select(item => item.Action));
    }

    [Fact]
    public void AreaFoundationPlanSupportsFilteredReadDryRunApplyPersistenceAndRevert()
    {
        using var workspace = new TestWorkspace();
        var project = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var area = project.Areas[0];
        var original = area.Foundation;
        var candidate = new AreaFoundationProfile(original.Id, 1, "agent:r028-test",
            original.Creation with
            {
                KitKey = "agent-modular-kit",
                Biome = "agent-crypt",
                RuntimeBudgetMegabytes = 768,
            },
            new(original.TileKit.Id, 1, "agent:r028-test", 2, [], [], [], [], []),
            new(new(Guid.Parse("a0283000-0000-4000-8000-000000000001"), "area.entered",
                "scripts/agent_entered.forge", "1.0.0", false, "agent:r028-test"), null), [], []);

        var before = workspace.Project.GetAreaFoundation(area.Id.ToString());
        var planned = workspace.Project.PlanAreaFoundation(
            new AreaFoundationPlanRequest(1, before.Revision, area.Id.ToString(), candidate));

        Assert.Empty(before.Diagnostics);
        Assert.Equal(original, before.Foundation);
        Assert.True(planned.Succeeded);
        Assert.Equal($"areas/{area.Id}/foundation", planned.Diff?.Path);
        Assert.Equal(original, workspace.Project.GetAreaFoundation(area.Id.ToString()).Foundation);
        var applied = workspace.Project.ApplyAreaFoundationPlan(planned.PlanId!);
        Assert.True(applied.Applied);
        var resource = workspace.Project.GetAreaFoundation(area.Id.ToString());
        Assert.Equal("agent-modular-kit", resource.RuntimeView!.KitKey);
        Assert.Equal("scripts/agent_entered.forge", resource.RuntimeView.EnteredScript);
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas[0].Foundation;
        Assert.Equal(candidate, persisted);
        var reverted = workspace.Project.RevertAreaFoundationPlan(planned.PlanId!);
        Assert.True(reverted.Reverted);
        Assert.Equal(original, ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas[0].Foundation);
        Assert.Equal(["planned", "apply", "revert"], reverted.Audit.Select(item => item.Action));
    }

    [Fact]
    public void R053AreaTileEditingUsesFilteredFoundationPlanPersistenceAndRevert()
    {
        using var workspace = new TestWorkspace();
        var project = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var area = project.Areas[0];
        var original = area.Foundation;
        var editing = AreaTileEditingProfile.CreateDefault(area.Id);
        editing = new AreaTileEditingProfile(editing.Id, editing.SchemaVersion, "agent:r053",
            editing.Groups, editing.Tools with
            {
                Operation = AreaTileEditOperationKind.RemapKit,
                ReplacementKitKey = "agent-remapped-kit",
            }, editing.AutoFit with { Seed = 53053 }, editing.Properties,
            editing.Resize with { Anchor = "NorthWest", MigrateInstances = true });
        var kit = new ModularTileKitProfile(original.TileKit.Id, original.TileKit.SchemaVersion, "agent:r053",
            original.TileKit.GridSizeMeters, original.TileKit.Definitions, original.TileKit.ConnectionRules,
            original.TileKit.Placements, original.TileKit.Groups, original.TileKit.Brushes, editing);
        var candidate = new AreaFoundationProfile(original.Id, original.SchemaVersion, "agent:r053",
            original.Creation with { KitKey = "agent-remapped-kit" }, kit, original.Lifecycle,
            original.Volumes, original.Dependencies, true);

        var before = workspace.Project.GetAreaFoundation(area.Id.ToString());
        var planned = workspace.Project.PlanAreaFoundation(new AreaFoundationPlanRequest(
            1, before.Revision, area.Id.ToString(), candidate));

        Assert.True(planned.Succeeded);
        Assert.False(planned.Applied);
        Assert.Null(workspace.Project.GetAreaFoundation(area.Id.ToString()).Foundation!.TileKit.Editing);
        var applied = workspace.Project.ApplyAreaFoundationPlan(planned.PlanId!);
        Assert.True(applied.Applied);
        var resource = workspace.Project.GetAreaFoundation(area.Id.ToString());
        Assert.Equal(53053, resource.Foundation!.TileKit.Editing!.AutoFit.Seed);
        Assert.Equal("NorthWest", resource.Foundation.TileKit.Editing.Resize.Anchor);
        Assert.True(resource.RuntimeView!.TileEditingRequired);
        Assert.Equal(0, resource.RuntimeView.EditableGroupCount);
        Assert.Equal(53053, ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas[0]
            .Foundation.TileKit.Editing!.AutoFit.Seed);

        var reverted = workspace.Project.RevertAreaFoundationPlan(planned.PlanId!);
        Assert.True(reverted.Reverted);
        Assert.Null(ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas[0]
            .Foundation.TileKit.Editing);
        Assert.Equal(["planned", "apply", "revert"], reverted.Audit.Select(item => item.Action));
    }

    [Fact]
    public void R054AreaEnvironmentUsesFilteredSemanticPlanPersistenceAndRevert()
    {
        using var workspace = new TestWorkspace();
        var project = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var area = project.Areas[0];
        var original = area.Environment;
        var authoring = new AreaEnvironmentAuthoringProfile(
            Guid.Parse("a0543000-0000-4000-8000-000000000001"), 1, "agent:r054",
            new(original.Classification, "custom:agent-moonlit", "agent-environment-pack",
                true, true, true, true, true, true),
            new("physical-agent-moonlit", "#30405f", "#ffe2b5", false,
                [new(0, 0, .8f, -45), new(12, 1.4f, 0, 65), new(24, 0, .8f, -45)],
                [new(Guid.Parse("a0543000-0000-4000-8000-000000000002"), 18,
                    "world.agent-sunset", "scripts/agent_sunset.forge", true)], [], []),
            new("#b7c9dd", "#263248", new("asset:agent-fog-noise", 12, .1f, .05f, 1.2f), [],
                new Dictionary<string, AreaFogQualityTier> { ["desktop"] = AreaFogQualityTier.Ultra }), true);
        var candidate = new AreaEnvironmentProfile(AreaEnvironmentProfile.CurrentSchemaVersion,
            original.Classification, [.. original.Tags, "agent-moonlit"],
            new(DayNightMode.Clock, 30, .4f, 1.4f, 5400, true, true),
            new(FogMode.Volumetric, .3f, 4, 90, 1.5f), original.Audio, original.Rendering,
            original.Sky, original.Wind, original.Weather, original.Water, original.Reverb, authoring, true);

        var before = workspace.Project.GetAreaEnvironment(area.Id.ToString());
        var planned = workspace.Project.PlanAreaEnvironment(new AreaEnvironmentPlanRequest(
            1, before.Revision, area.Id.ToString(), candidate));

        Assert.Empty(before.Diagnostics);
        Assert.Equal(original.Authoring.Id, before.Environment!.Authoring.Id);
        Assert.Equal(original.Authoring.Classification.Key, before.RuntimeView!.ClassificationKey);
        Assert.True(planned.Succeeded, string.Join(" | ", planned.Diagnostics.Select(item => item.Message)));
        Assert.False(planned.Applied);
        Assert.Contains("physical-agent-moonlit", planned.Diff!.Summary, StringComparison.Ordinal);
        Assert.Equal(original.Authoring.Id,
            workspace.Project.GetAreaEnvironment(area.Id.ToString()).Environment!.Authoring.Id);
        var applied = workspace.Project.ApplyAreaEnvironmentPlan(planned.PlanId!);
        Assert.True(applied.Applied);
        var resource = workspace.Project.GetAreaEnvironment(area.Id.ToString());
        Assert.Equal("custom:agent-moonlit", resource.RuntimeView!.ClassificationKey);
        Assert.Equal(3, resource.RuntimeView.CurvePointCount);
        Assert.Equal("asset:agent-fog-noise", resource.Environment!.Authoring.Fog.Noise!.AssetId);
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas[0].Environment;
        Assert.Equal(candidate.Authoring.Id, persisted.Authoring.Id);
        Assert.Equal(candidate.Authoring.Lighting.PresetKey, persisted.Authoring.Lighting.PresetKey);

        var reverted = workspace.Project.RevertAreaEnvironmentPlan(planned.PlanId!);
        Assert.True(reverted.Reverted);
        var restored = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas[0].Environment;
        Assert.Equal(original.Authoring.Id, restored.Authoring.Id);
        Assert.Equal(original.Authoring.Lighting.PresetKey, restored.Authoring.Lighting.PresetKey);
        Assert.Equal(["planned", "apply", "revert"], reverted.Audit.Select(item => item.Action));
    }

    [Fact]
    public void R055AreaExperienceUsesFilteredSemanticPlanPersistenceAndRevert()
    {
        using var workspace = new TestWorkspace();
        var project = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var area = project.Areas[0];
        var original = area.Authoring;
        var room = new AreaVisibilityRoomProfile(Guid.Parse("a0556000-0000-4000-8000-000000000001"),
            "Agent room", TransformValue.Identity, false, 96, new Dictionary<string, string>());
        var experience = new AreaExperienceAuthoringProfile(
            Guid.Parse("a0556000-0000-4000-8000-000000000002"), 1, "agent:r055",
            [new(Guid.Parse("a0556000-0000-4000-8000-000000000003"), "Agent combat",
                new Dictionary<string, float> { ["music"] = .8f }, .75f,
                $"area.{area.Slug}.combat", "agent:r055")],
            new([room], [], [], true, true, true, 16, 128),
            new(AreaLodSourceMode.Hybrid, [20, 50, 100], 1_500_000, 96, 24, .15f, true, true,
                [new(Guid.Parse("a0556000-0000-4000-8000-000000000004"), "asset:agent-column",
                    AreaLodSourceMode.Generated, [1, .5f], [12000, 6000], .2f, "agent:r055")]), true);
        var eventName = $"area.{area.Slug}.alarm";
        var candidate = new AreaAuthoringProfile(1,
            [new(Guid.Parse("a0556000-0000-4000-8000-000000000005"), eventName,
                "schema://area/alarm/v1", "scripts/agent_alarm.forge", 1, [eventName], true, "agent:r055")],
            new(true, true, "hybrid-2d-3d", 2,
                [new(Guid.Parse("a0556000-0000-4000-8000-000000000006"), "Agent shrine", 16, 0, 0,
                    false)], Guid.Parse("a0556000-0000-4000-8000-000000000007"), 1, "agent:r055",
                true, true, AreaMapExplorationScope.Party, 8, "area.map.reveal"), original.Layers, experience);

        var before = workspace.Project.GetAreaExperienceAuthoring(area.Id.ToString());
        var planned = workspace.Project.PlanAreaExperienceAuthoring(new AreaExperienceAuthoringPlanRequest(
            1, before.Revision, area.Id.ToString(), candidate));

        Assert.Empty(before.Diagnostics);
        Assert.Equal(original.Experience.Id, before.Authoring!.Experience.Id);
        Assert.True(planned.Succeeded, string.Join(" | ", planned.Diagnostics.Select(item => item.Message)));
        Assert.False(planned.Applied);
        Assert.Contains("audio snapshot", planned.Diff!.Summary, StringComparison.Ordinal);
        Assert.Equal(original.Experience.Id,
            workspace.Project.GetAreaExperienceAuthoring(area.Id.ToString()).Authoring!.Experience.Id);
        var applied = workspace.Project.ApplyAreaExperienceAuthoringPlan(planned.PlanId!);
        Assert.True(applied.Applied);
        var resource = workspace.Project.GetAreaExperienceAuthoring(area.Id.ToString());
        Assert.Single(resource.RuntimeView!.AudioSnapshots);
        Assert.Equal(1, resource.RuntimeView.VisibilityRooms);
        Assert.Equal(1, resource.RuntimeView.LodOverrides);
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas[0].Authoring;
        Assert.Equal(candidate.Experience.Id, persisted.Experience.Id);

        var reverted = workspace.Project.RevertAreaExperienceAuthoringPlan(planned.PlanId!);
        Assert.True(reverted.Reverted);
        var restored = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas[0].Authoring;
        Assert.Equal(original.Experience.Id, restored.Experience.Id);
        Assert.Equal(["planned", "apply", "revert"], reverted.Audit.Select(item => item.Action));
    }

    [Fact]
    public void R056AreaLayersUseFilteredSemanticPlanPersistenceAndRevert()
    {
        using var workspace = new TestWorkspace();
        var project = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var area = project.Areas[0];
        var original = area.Authoring;
        var root = Guid.Parse("a0566000-0000-4000-8000-000000000001");
        var child = Guid.Parse("a0566000-0000-4000-8000-000000000002");
        var candidate = new AreaAuthoringProfile(original.SchemaVersion, original.UserEvents, original.Map,
        [
            new(root, "Architecture", null, true, false, "agent", [], provenance: "agent:r056"),
            new(child, "Actors", root, true, false, "agent", [area.Entities[0].Id],
                provenance: "agent:r056", exportEnabled: false),
        ], original.Experience, true);

        var before = workspace.Project.GetAreaExperienceAuthoring(area.Id.ToString());
        var planned = workspace.Project.PlanAreaExperienceAuthoring(new AreaExperienceAuthoringPlanRequest(
            1, before.Revision, area.Id.ToString(), candidate));

        Assert.Empty(before.Diagnostics);
        Assert.True(planned.Succeeded, string.Join(" | ", planned.Diagnostics.Select(item => item.Message)));
        Assert.False(planned.Applied);
        Assert.Contains("2 editor layer", planned.Diff!.Summary, StringComparison.Ordinal);
        Assert.Empty(workspace.Project.GetAreaExperienceAuthoring(area.Id.ToString()).Authoring!.Layers);
        var applied = workspace.Project.ApplyAreaExperienceAuthoringPlan(planned.PlanId!);
        Assert.True(applied.Applied);
        var resource = workspace.Project.GetAreaExperienceAuthoring(area.Id.ToString());
        Assert.Equal(2, resource.Authoring!.Layers.Count);
        Assert.True(resource.Authoring.RequireLayerRuntimeMetadata);
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas[0].Authoring;
        Assert.Equal(root, persisted.Layers.Single(item => item.Id == child).ParentId);
        Assert.False(persisted.Layers.Single(item => item.Id == child).ExportEnabled);

        var reverted = workspace.Project.RevertAreaExperienceAuthoringPlan(planned.PlanId!);
        Assert.True(reverted.Reverted);
        var restored = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas[0].Authoring;
        Assert.Empty(restored.Layers);
        Assert.Equal(["planned", "apply", "revert"], reverted.Audit.Select(item => item.Action));
    }

    [Fact]
    public void AreaRuntimeReadAndPlayFromHerePlanAreFilteredDryRunPersistentAndRevertible()
    {
        using var workspace = new TestWorkspace();
        var project = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var area = project.Areas[0];
        var player = project.Blueprints.Single(item => item.Kind == BlueprintKind.PlayerSpawn);
        var original = area.Playtest;
        var candidate = new AreaPlaytestProfile(original.Id, 1, "agent:r029-test", true, false,
            PlayFromHereStartMode.Camera, null,
            new TransformValue(new Vector3Value(8, 2, -4), QuaternionValue.Identity, Vector3Value.One),
            null, player.Id, "agent-camera", []);

        var before = workspace.Project.GetAreaRuntime(area.Id.ToString());
        var planned = workspace.Project.PlanAreaPlaytest(
            new AreaPlaytestPlanRequest(1, before.Revision, area.Id.ToString(), candidate));

        Assert.Empty(before.Diagnostics);
        Assert.Equal(area.Id.ToString(), before.AreaId);
        Assert.Equal(9, before.Readiness!.CategoryCounts.Count);
        Assert.Equal(4, before.Navigation!.AgentProfiles);
        Assert.True(planned.Succeeded);
        Assert.Equal($"areas/{area.Id}/playtest", planned.Diff?.Path);
        Assert.Equal(original, workspace.Project.GetAreaRuntime(area.Id.ToString()).Playtest);
        var applied = workspace.Project.ApplyAreaPlaytestPlan(planned.PlanId!);
        Assert.True(applied.Applied);
        Assert.Equal(candidate,
            ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas[0].Playtest);
        var reverted = workspace.Project.RevertAreaPlaytestPlan(planned.PlanId!);
        Assert.True(reverted.Reverted);
        Assert.Equal(original,
            ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas[0].Playtest);
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
        Assert.Equal(717, source.ModernTooling.QualityAssurance.AuroraParityCoverage.ExpectedFeatureCount);
        Assert.Equal(7, source.ModernTooling.QualityAssurance.MaliciousFixtures.Families.Count);
        Assert.True(source.ModernTooling.QualityAssurance.AcceptanceGates.RequireFeatureEvidence);
        var rule = new DataDrivenRuleSchema(Guid.NewGuid(), 1, "casting.focus", RuleSchemaKind.Casting,
            "{\"type\":\"object\"}", "{\"resource\":\"focus\"}", true, "mcp-test");
        var readiness = source.OperationalReadiness;
        var recovery = new CrashRecoveryPolicy(readiness.Recovery.Id, 1, 45, 8,
            true, true, true, "mcp-test:r060");
        var updatedReadiness = new OperationalReadinessProfile(readiness.Id, 1, readiness.TaskManifests,
            readiness.CrashArtifacts, readiness.ProjectHealth, recovery, readiness.ContentSynchronization,
            "mcp-test:r060");
        var profile = new ModuleRuntimeExtensionProfile(source.Id, 1, [rule], [], [], source.SnapshotPolicy,
            "mcp-test", source.ModernTooling, updatedReadiness, source.PostLaunch);

        var planned = workspace.Project.PlanRuntimeExtensions(new RuntimeExtensionPlanRequest(1,
            resource.Revision, profile));

        Assert.True(planned.Succeeded);
        Assert.Equal("module/runtimePolicies/extensions", planned.Diff?.Path);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        var applied = workspace.Project.ApplyRuntimeExtensionPlan(planned.PlanId!);
        Assert.True(applied.Applied);
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Module.RuntimePolicies.Extensions;
        Assert.Equal("casting.focus", Assert.Single(persisted.RuleSchemas).Key);
        Assert.Equal(45, persisted.OperationalReadiness.Recovery.AutosaveIntervalSeconds);
        Assert.Equal(8, persisted.OperationalReadiness.Recovery.RetainedSnapshots);
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
        var nodeProperties = document.RootElement.GetProperty("properties").GetProperty("nodes")
            .GetProperty("items").GetProperty("properties");
        Assert.True(nodeProperties.TryGetProperty("role", out _));
        Assert.True(nodeProperties.TryGetProperty("variants", out _));
        var choiceProperties = nodeProperties.GetProperty("choices").GetProperty("items").GetProperty("properties");
        Assert.True(choiceProperties.TryGetProperty("priority", out _));
        Assert.True(choiceProperties.TryGetProperty("conditionCombination", out _));
        Assert.True(choiceProperties.TryGetProperty("conditions", out _));
        Assert.True(choiceProperties.TryGetProperty("actions", out _));
        var authoringProperties = document.RootElement.GetProperty("properties").GetProperty("authoring")
            .GetProperty("properties");
        Assert.True(authoringProperties.TryGetProperty("participants", out _));
        var authoringNodeProperties = authoringProperties.GetProperty("nodes").GetProperty("items")
            .GetProperty("properties");
        Assert.Equal(5, authoringNodeProperties.GetProperty("evaluationMode").GetProperty("enum").GetArrayLength());
        Assert.True(authoringNodeProperties.TryGetProperty("tokens", out _));
        Assert.True(authoringNodeProperties.TryGetProperty("wizardEntries", out _));
        Assert.True(authoringNodeProperties.TryGetProperty("notes", out _));
        var experienceProperties = document.RootElement.GetProperty("properties").GetProperty("experience")
            .GetProperty("properties");
        Assert.True(experienceProperties.TryGetProperty("lifecycle", out _));
        Assert.True(experienceProperties.TryGetProperty("search", out _));
        Assert.Equal(6, experienceProperties.GetProperty("review").GetProperty("properties")
            .GetProperty("badges").GetProperty("items").GetProperty("enum").GetArrayLength());
        Assert.True(experienceProperties.TryGetProperty("memorySlots", out _));
        Assert.True(experienceProperties.TryGetProperty("skillChecks", out _));
    }

    [Fact]
    public void DialogueGraphQueryIsFilteredBoundedAndReadOnly()
    {
        using var workspace = new TestWorkspace();
        var beforeRevision = workspace.Project.GetCurrentRevision();

        var result = workspace.Project.QueryDialogues("elyra", 1);

        var dialogue = Assert.Single(result.Items);
        Assert.Equal(1, result.SchemaVersion);
        Assert.Equal(beforeRevision, result.ProjectRevision);
        Assert.NotEmpty(dialogue.Nodes);
        Assert.InRange(dialogue.Edges.Count, 1, dialogue.Nodes.Sum(node => node.ChoiceCount));
        Assert.All(dialogue.Nodes, node => Assert.False(string.IsNullOrWhiteSpace(node.Role)));
        Assert.All(dialogue.Nodes, node => Assert.True(node.LocalizationVariantCount >= 0 && node.OnEnterActionCount >= 0));
        Assert.All(dialogue.Edges, edge => Assert.True(edge.ConditionCount >= 0 && edge.ActionCount >= 0));
        Assert.Equal(beforeRevision, workspace.Project.GetCurrentRevision());
        Assert.Throws<ArgumentOutOfRangeException>(() => workspace.Project.QueryDialogues(limit: 101));
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
    public void DialoguePlanPreservesRolesReferencesAndPriority()
    {
        using var workspace = new TestWorkspace();
        var project = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var speaker = project.Blueprints.First().Id.ToString();
        var dialogueId = "7b02e466-b43a-442b-a833-8613a1c71952";
        var entryId = "659eb07b-5d3f-4f87-80de-26ec4b20c838";
        var endId = "659eb07b-5d3f-4f87-80de-26ec4b20c839";
        var request = new DialoguePlanRequest(1, 0, dialogueId, "mcp-rich-dialogue", "MCP rich dialogue", entryId,
        [
            new DialogueNodeInput(entryId, speaker, "dialogue.rich.entry", "Choose", Choices:
            [
                new("659eb07b-5d3f-4f87-80de-26ec4b20c840", "dialogue.rich.flow", "Continue", endId,
                    Conditions:
                    [
                        new("HasItem", "ash-key", "true"),
                        new("QuestStateEquals", "crypt", "completed", Negated: true),
                    ],
                    Actions: [new("SetRelation", "elyra", "trusted"), new("PlayAudio", "elyra.accept", "play")],
                    EdgeKind: "Flow", Priority: 20, ConditionCombination: "All",
                    Variants: [new("fr", "any", "Other", "Continuer")]),
                new("659eb07b-5d3f-4f87-80de-26ec4b20c841", "dialogue.rich.reference", "Inspect", endId,
                    EdgeKind: "Reference", Priority: 10),
            ], Role: "PlayerChoice"),
            new DialogueNodeInput(endId, null, "dialogue.rich.end", "End", Role: "End"),
        ]);

        var planned = workspace.Project.PlanDialogue(request);
        var applied = workspace.Project.ApplyDialoguePlan(planned.PlanId!);
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Dialogues
            .Single(item => item.Id.ToString() == dialogueId);

        Assert.True(planned.Succeeded);
        Assert.True(applied.Applied);
        Assert.Equal(DialogueNodeRole.PlayerChoice, persisted.Nodes[0].Role);
        Assert.Equal([20, 10], persisted.Nodes[0].Choices.Select(choice => choice.Priority));
        Assert.Equal(DialogueEdgeKind.Reference, persisted.Nodes[0].Choices[1].EdgeKind);
        Assert.Equal("Continuer", persisted.Nodes[0].Choices[0].Text.Resolve("fr"));
        Assert.True(persisted.Nodes[0].Choices[0].Conditions[1].Negated);
        Assert.Equal(DialogueConditionCombination.All, persisted.Nodes[0].Choices[0].ConditionCombination);
        Assert.Equal([DialogueActionKind.SetRelation, DialogueActionKind.PlayAudio],
            persisted.Nodes[0].Choices[0].Actions.Select(item => item.Kind));
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
    public void R068DialogueAuthoringIsFilteredPlannedAppliedPersistedAndReverted()
    {
        using var workspace = new TestWorkspace();
        var request = CreatePlan(workspace.RootDirectory);
        var nodeId = request.EntryNodeId;
        var authoring = new DialogueAuthoringInput(
            "d0683000-0000-4000-8000-000000000001", true, true,
            [new("d0683000-0000-4000-8000-000000000002", "guide", "Entity", "entity:elyra")],
            [new("d0683000-0000-4000-8000-000000000003", nodeId, "guide", "FirstValid",
                [new("d0683000-0000-4000-8000-000000000004", "hero", "EntityName", true)],
                [], [],
                new("d0683000-0000-4000-8000-000000000005", "Présenter Elyra au joueur.", "warm",
                    "brief:r068", "Review", ["Valider le ton"], "@karen"))]);
        request = request with
        {
            Nodes = [request.Nodes[0] with { FallbackText = "Bienvenue, {{hero}}." }],
            Authoring = authoring,
        };
        var invalid = workspace.Project.PlanDialogue(request with
        {
            Nodes = [request.Nodes[0] with { FallbackText = "Bienvenue." }],
        });

        var planned = workspace.Project.PlanDialogue(request);
        var applied = workspace.Project.ApplyDialoguePlan(planned.PlanId!);
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Dialogues
            .Single(item => item.Id.ToString() == request.DialogueId);
        var queried = workspace.Project.QueryDialogues("hero", 5);
        var reverted = workspace.Project.RevertDialoguePlan(planned.PlanId!);

        Assert.False(invalid.Succeeded);
        Assert.Contains(invalid.Diagnostics, item => item.Code == DialogueAuthoringProfileValidator.TokenCode);
        Assert.True(planned.Succeeded);
        Assert.Equal("add", planned.Diff?.Operation);
        Assert.True(applied.Applied);
        Assert.True(persisted.Authoring!.RequiredAtRuntime);
        Assert.Equal(DialogueTextTokenType.EntityName,
            persisted.Authoring.Nodes.Single().Tokens.Single().Type);
        var resource = Assert.Single(queried.Items).Authoring!;
        Assert.Equal("FirstValid", Assert.Single(resource.Nodes).EvaluationMode);
        Assert.Contains("hero:EntityName:True", Assert.Single(resource.Nodes).Tokens);
        Assert.Equal("@karen", Assert.Single(resource.Nodes).AuthorMention);
        Assert.True(reverted.Reverted);
        Assert.DoesNotContain(ProjectSession.Open(workspace.RootDirectory).Session!.Project.Dialogues,
            item => item.Id.ToString() == request.DialogueId);
    }

    [Fact]
    public void R069DialogueExperienceIsFilteredPlannedAppliedPersistedAndReverted()
    {
        using var workspace = new TestWorkspace();
        var request = CreatePlan(workspace.RootDirectory);
        var nodeId = request.EntryNodeId;
        var experience = new DialogueExperienceInput(
            "d0693000-0000-4000-8000-000000000001", true, true,
            new("d0693000-0000-4000-8000-000000000002",
                [new("SetVariable", "dialogue.completed", "true")],
                [new("RunScript", "dialogue-aborted.forge", "reason")], true, true),
            new("d0693000-0000-4000-8000-000000000003", "dry-run", false, true, [], [], [],
                "preview"),
            new("d0693000-0000-4000-8000-000000000004",
                ["Condition", "Action", "Voice", "Localization", "Error", "Link"], [], true),
            [new("d0693000-0000-4000-8000-000000000005", "Player", "player:current", "trust",
                "Integer", "Campaign", "0")],
            [new("d0693000-0000-4000-8000-000000000006", nodeId, "persuasion", 12, "warden", true,
                "FullExplanation", nodeId, nodeId)]);
        request = request with { Experience = experience };
        var invalid = workspace.Project.PlanDialogue(request with
        {
            Experience = experience with
            {
                MemorySlots = [experience.MemorySlots[0] with { DefaultValue = "not-an-integer" }],
            },
        });

        var planned = workspace.Project.PlanDialogue(request);
        var applied = workspace.Project.ApplyDialoguePlan(planned.PlanId!);
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Dialogues
            .Single(item => item.Id.ToString() == request.DialogueId);
        var queried = workspace.Project.QueryDialogues("dry-run", 5);
        var reverted = workspace.Project.RevertDialoguePlan(planned.PlanId!);

        Assert.False(invalid.Succeeded);
        Assert.Contains(invalid.Diagnostics, item => item.Code == DialogueExperienceProfileValidator.MemoryCode);
        Assert.True(planned.Succeeded);
        Assert.Equal("add", planned.Diff?.Operation);
        Assert.True(applied.Applied);
        Assert.True(persisted.Experience!.RequiredAtRuntime);
        Assert.True(persisted.Experience.Lifecycle.RollbackOnAbort);
        Assert.Equal(DialogueMemoryDuration.Campaign, Assert.Single(persisted.Experience.MemorySlots).Duration);
        var resource = Assert.Single(queried.Items).Experience!;
        Assert.Equal("dry-run", resource.SearchPattern);
        Assert.Equal(6, resource.ReviewBadges.Count);
        Assert.Contains("persuasion", Assert.Single(resource.SkillChecks));
        Assert.True(reverted.Reverted);
        Assert.DoesNotContain(ProjectSession.Open(workspace.RootDirectory).Session!.Project.Dialogues,
            item => item.Id.ToString() == request.DialogueId);
    }

    [Fact]
    public void R070DialogueProductionIsFilteredPlannedAppliedPersistedAndReverted()
    {
        using var workspace = new TestWorkspace();
        var request = CreatePlan(workspace.RootDirectory);
        var nodeId = request.EntryNodeId;
        var contractId = "d0703000-0000-4000-8000-000000000001";
        var production = new DialogueProductionInput(
            "d0703000-0000-4000-8000-000000000002", true, true,
            [new(contractId, "embers", 2,
                [new("d0703000-0000-4000-8000-000000000003", "trust", "Integer", "Input", true)],
                ["party:trust"],
                [new("d0703000-0000-4000-8000-000000000004", "trust accepted",
                    new Dictionary<string, string> { ["trust"] = "4" },
                    new Dictionary<string, string>())])],
            [new("d0703000-0000-4000-8000-000000000005", nodeId, contractId,
                new Dictionary<string, string> { ["trust"] = "party:trust" },
                new Dictionary<string, string>())],
            [new("d0703000-0000-4000-8000-000000000006", nodeId, "Quest", "quest:crypt",
                "advance", "2", "Advance the crypt quest")],
            new("d0703000-0000-4000-8000-000000000007", 64,
                new Dictionary<string, string> { ["quest:crypt"] = "active" },
                [new("d0703000-0000-4000-8000-000000000008", "session:crypt", "Session", [nodeId],
                    new Dictionary<string, string> { ["quest:crypt"] = "active" })]),
            new("d0703000-0000-4000-8000-000000000009", [], [], [], []),
            [new("d0703000-0000-4000-8000-000000000010",
                "d0703000-0000-4000-8000-000000000011",
                "d0703000-0000-4000-8000-000000000012", "AuthorApply", "Promise under the crypt",
                ["offer"], ["style:embers"], ["context#dialogue"], "add-subgraph:embers", [], true, false)]);
        request = request with
        {
            Nodes = [request.Nodes[0] with { Role = "Subgraph", SubgraphKey = "embers" }],
            Production = production,
        };
        var invalid = workspace.Project.PlanDialogue(request with
        {
            Production = production with { Coverage = production.Coverage with { MaximumDepth = 0 } },
        });

        var planned = workspace.Project.PlanDialogue(request);
        var applied = workspace.Project.ApplyDialoguePlan(planned.PlanId!);
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Dialogues
            .Single(item => item.Id.ToString() == request.DialogueId);
        var queried = workspace.Project.QueryDialogues("embers", 5);
        var reverted = workspace.Project.RevertDialoguePlan(planned.PlanId!);

        Assert.False(invalid.Succeeded);
        Assert.Contains(invalid.Diagnostics, item => item.Code == DialogueProductionProfileValidator.CoverageCode);
        Assert.True(planned.Succeeded);
        Assert.Equal("add", planned.Diff?.Operation);
        Assert.True(applied.Applied);
        Assert.True(persisted.Production!.RequiredAtRuntime);
        Assert.Equal(2, Assert.Single(persisted.Production.Subgraphs).Version);
        Assert.Equal(DialogueConsequenceTargetKind.Quest, Assert.Single(persisted.Production.Consequences).Kind);
        var resource = Assert.Single(queried.Items).Production!;
        Assert.Contains(resource.Subgraphs, item => item.StartsWith("embers:v2", StringComparison.Ordinal));
        Assert.Contains(resource.Consequences, item => item.Contains("quest:crypt", StringComparison.Ordinal));
        Assert.Contains(resource.AiPlans, item => item.Contains("AuthorApply", StringComparison.Ordinal));
        Assert.True(reverted.Reverted);
        Assert.DoesNotContain(ProjectSession.Open(workspace.RootDirectory).Session!.Project.Dialogues,
            item => item.Id.ToString() == request.DialogueId);
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

    [Fact]
    public void AssetGovernanceResourceIsFilteredAndPlanIsAlwaysDryRun()
    {
        using var workspace = new TestWorkspace();
        var assetId = AssetCatalogItemId.ForExternalId("test:mcp-governance");
        var record = new AssetGovernanceRecord(1, AssetGovernanceId.ForAsset(assetId), assetId,
            AssetPackageManifestId.ForAsset(assetId), "CC0-1.0", new string('d', 64), "Author",
            "https://example.invalid/mcp-asset", "permission:mcp-fixture", "No modifications.", false,
            string.Empty, string.Empty, 8192,
            new("incoming/model.glb", "model/gltf-binary", "model/gltf-binary", 12, 8192, "scanner", "clean",
                "valid", AssetQuarantineStatus.Approved, "reviewer", DateTimeOffset.UnixEpoch),
            new(AssetOriginKind.OpenLicensed, true, false, string.Empty));
        Assert.True(AssetGovernanceStore.Save(workspace.RootDirectory, [record]).Succeeded);

        var resource = workspace.Project.GetAssetGovernance(new(assetId.ToString()[..8], "OpenLicensed", "Approved", 1));
        var plan = workspace.Project.PlanAssetGovernance(new(1, 0, record with { Modifications = "Normalized." }));

        Assert.Equal(record, Assert.Single(resource.Records));
        Assert.Equal(1, resource.Total);
        Assert.True(plan.Succeeded);
        Assert.True(plan.DryRun);
        Assert.Equal("replace", plan.Diff!.Operation);
        Assert.Equal(record, Assert.Single(AssetGovernanceStore.Load(workspace.RootDirectory).Records));
        Assert.Empty(workspace.Project.GetAssetGovernance(new(MaximumResults: 301)).Records);
    }

    [Fact]
    public void QualityFoundationResourceAndPlanAreStructuredAndAlwaysDryRun()
    {
        using var workspace = new TestWorkspace();
        var resource = workspace.Project.GetQualityFoundation();
        var validation = workspace.Project.ValidateCanonicalProject();
        var changed = resource.Profile with { Provenance = "mcp:qa-preview" };

        var plan = workspace.Project.PlanQualityFoundation(new(1, 0, changed));
        var stale = workspace.Project.PlanQualityFoundation(new(1, 4, changed));
        var unsupported = workspace.Project.PlanQualityFoundation(new(7, 0, changed));

        Assert.Empty(resource.Diagnostics);
        Assert.True(validation.Passed, string.Join("; ", validation.Diagnostics.Select(item => $"{item.Code}:{item.Path}")));
        Assert.Equal(5, validation.CategoryCounts.Count);
        Assert.NotEmpty(validation.Fingerprint);
        Assert.Empty(QualityFoundationProfileValidator.Validate(resource.Profile));
        Assert.True(plan.Succeeded);
        Assert.True(plan.DryRun);
        Assert.Equal("replace", plan.Diff!.Operation);
        Assert.Equal(SetQualityFoundationHandler.RevisionConflictCode, Assert.Single(stale.Diagnostics).Code);
        Assert.Equal("qa.foundation.plan.schema_unsupported", Assert.Single(unsupported.Diagnostics).Code);
        var ownerId = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Id.Value;
        Assert.True(QualityFoundationProfileStore.Load(workspace.RootDirectory,
            ownerId).FileMissing);
    }

    private static WorldFeatureCatalog WithDoors(WorldFeatureCatalog source,
        IReadOnlyList<DoorWorldProfile> doors) => new(source.Id, source.SchemaVersion, source.Provenance,
        source.Placeables, doors, source.Triggers, source.Encounters, source.VehicleRoutes, source.Merchants,
        source.Companions, source.Scripts);

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
