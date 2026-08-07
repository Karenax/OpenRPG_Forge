using System.Text.Json;
using Forge.Application.Assistant;
using Forge.Application.Assets;
using Forge.Application.Validation;
using Forge.Domain.Assets;
using Forge.Domain.Areas;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Factions;
using Forge.Domain.Combat;
using Forge.Domain.Dialogues;
using Forge.Domain.Gameplay;
using Forge.Domain.Projects;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Rules;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.Mcp.Abilities;
using Forge.Mcp.Dialogues;
using Forge.Mcp.Bosses;
using Forge.Mcp.Quests;
using Forge.Mcp.Assets;
using Forge.Mcp.Projects;
using Forge.Mcp.Items;
using Forge.Mcp.Scripts;
using Forge.Mcp.WorldFeatures;
using Forge.ModuleFormat.Workspaces;
using Forge.ModuleFormat.Assets;
using Forge.RpgKit.Application.Dialogues;
using Forge.RpgKit.Application.Abilities;
using Forge.RpgKit.Application.Bosses;
using Forge.RpgKit.Application.Quests;
using Forge.Scripting;

namespace Forge.Mcp.Tests.Projects;

public sealed class ForgeProjectAgentServiceTests
{
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
            ], [], [new("neutral", -100, 100, 1)], [],
            new FactionAdvancedProfile(
                Guid.Parse("b2040000-0000-4000-8000-000000000010"), 1, "mcp:test", true,
                [new(Guid.Parse("b2040000-0000-4000-8000-000000000011"), child, true, true, true)],
                [], [], []), true);

        var before = workspace.Project.QueryFactions();
        var planned = workspace.Project.PreviewFactions(new(1, 0, catalog));
        var after = workspace.Project.QueryFactions("ash", 1);

        Assert.Contains(before.Items, item => item.Slug == "ash-watch");
        Assert.True(planned.Succeeded);
        Assert.NotNull(planned.SemanticDiff);
        var unchanged = Assert.Single(after.Items);
        Assert.Equal("ash-watch", unchanged.Slug);
        Assert.Equal(before.Items.Single(item => item.Slug == "ash-watch").FactionId, unchanged.FactionId);
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
        Assert.Equal(6, warden.BindingCount);
        Assert.Equal(1_500, warden.HeartbeatIntervalMilliseconds);
        Assert.Equal("CombatTickCompleted", warden.CombatBoundary);
        Assert.Contains("crypt.braziers_lit", warden.CustomEvents);
        Assert.Empty(missing.Items);
        Assert.True(planned.Succeeded);
        Assert.NotNull(planned.SemanticDiff);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        Assert.Throws<ArgumentOutOfRangeException>(() => workspace.Project.QueryCreatureEvents(limit: 101));
    }

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
