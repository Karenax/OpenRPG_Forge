using System.Text.Json.Nodes;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.RuntimeExtensions;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class RuntimeExtensionPersistenceTests
{
    [Fact]
    public void Project_round_trip_preserves_canonical_runtime_extension_documents()
    {
        var project = TestProjectFactory.CreateValid();
        var state = new ProjectState(project, new ProjectRevision(0));
        var source = project.Module.RuntimePolicies.Extensions;
        var widget = new ScriptableUiWidget(Guid.NewGuid(), "status", ScriptableUiWidgetKind.Label, null,
            "Status", "player.status", false, 0, "player");
        var readinessSource = source.OperationalReadiness;
        var task = new AgentTaskManifest(Guid.NewGuid(), 1, "release-module", "Release the validated module.",
            ["project.json", "areas"], ["Divers"], ["forge package validate"], "release-team", [], "test");
        var readiness = new OperationalReadinessProfile(readinessSource.Id, 1, [task],
            readinessSource.CrashArtifacts, readinessSource.ProjectHealth, readinessSource.Recovery,
            readinessSource.ContentSynchronization, "test");
        var profile = new ModuleRuntimeExtensionProfile(source.Id, 1,
            [new DataDrivenRuleSchema(Guid.NewGuid(), 1, "weapon.sword", RuleSchemaKind.Weapon,
                "{\"type\":\"object\"}", "{\"damage\":8}", false, "test")],
            [new ScriptableUiDocument(Guid.NewGuid(), 1, "hud", "HUD", "forge-dark", false,
                [widget], [], "test")],
            [new RuntimeJsonDocumentDefinition(Guid.NewGuid(), 1, "campaign", "{\"type\":\"object\"}",
                "{\"chapter\":1}", 4096, 8, true, "1.0", "test")], source.SnapshotPolicy, "test",
            source.ModernTooling, readiness);
        var handler = new SetRuntimeExtensionsHandler(new SystemClock());
        var applied = handler.Handle(state, new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()),
            state.Revision, profile, false));

        var json = ProjectJsonSerializer.Serialize(applied.State.Project);
        var restored = ProjectJsonSerializer.Deserialize(json).Module.RuntimePolicies.Extensions;

        Assert.Equal(ProjectJsonSerializer.CurrentFormatVersion,
            JsonNode.Parse(json)!["formatVersion"]!.GetValue<string>());
        Assert.Equal("weapon.sword", Assert.Single(restored.RuleSchemas).Key);
        Assert.Equal("hud", Assert.Single(restored.UiDocuments).Key);
        Assert.Equal("campaign", Assert.Single(restored.JsonDocuments).Key);
        Assert.Contains("blackboard", restored.SnapshotPolicy.Components);
        Assert.Equal("2.0", restored.ModernTooling.UiToolkit.ToolkitVersion);
        Assert.True(restored.ModernTooling.Compiler.Incremental);
        Assert.Equal(3, restored.ModernTooling.QualityAssurance.PlatformBudgets.Count);
        Assert.True(restored.ModernTooling.QualityAssurance.Profiler.OneClickCapture);
        Assert.Equal(7, restored.ModernTooling.QualityAssurance.MaliciousFixtures.Families.Count);
        Assert.Equal(717, restored.ModernTooling.QualityAssurance.AuroraParityCoverage.ExpectedFeatureCount);
        Assert.True(restored.ModernTooling.QualityAssurance.AcceptanceGates.RequireLinkedTests);
        Assert.Equal("release-module", Assert.Single(restored.OperationalReadiness.TaskManifests).Key);
        Assert.True(restored.OperationalReadiness.Recovery.QuarantineCorruptFiles);
    }

    [Fact]
    public void Pre_extension_project_migrates_to_stable_empty_profile()
    {
        var project = TestProjectFactory.CreateValid();
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(project))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.PreRuntimeExtensionsFormatVersion;
        document["project"]!["module"]!["runtimePolicies"]!.AsObject().Remove("extensions");

        var first = ProjectJsonSerializer.Deserialize(document.ToJsonString()).Module.RuntimePolicies.Extensions;
        var second = ProjectJsonSerializer.Deserialize(document.ToJsonString()).Module.RuntimePolicies.Extensions;

        Assert.Equal(project.Module.Id.Value, first.Id);
        Assert.Empty(first.RuleSchemas);
        Assert.Empty(first.UiDocuments);
        Assert.Empty(first.JsonDocuments);
        Assert.Equal(first.SnapshotPolicy.Id, second.SnapshotPolicy.Id);
    }

    [Fact]
    public void Pre_modern_tooling_project_migrates_to_deterministic_defaults()
    {
        var project = TestProjectFactory.CreateValid();
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(project))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.PreModernToolingFormatVersion;
        document["project"]!["module"]!["runtimePolicies"]!["extensions"]!.AsObject().Remove("modernTooling");

        var first = ProjectJsonSerializer.Deserialize(document.ToJsonString()).Module.RuntimePolicies.Extensions.ModernTooling;
        var second = ProjectJsonSerializer.Deserialize(document.ToJsonString()).Module.RuntimePolicies.Extensions.ModernTooling;

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("2.0", first.UiToolkit.ToolkitVersion);
        Assert.Equal("docs/generated", first.Documentation.OutputDirectory);
    }

    [Fact]
    public void Pre_operational_readiness_project_migrates_to_deterministic_defaults()
    {
        var project = TestProjectFactory.CreateValid();
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(project))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.PreOperationalReadinessFormatVersion;
        document["project"]!["module"]!["runtimePolicies"]!["extensions"]!.AsObject()
            .Remove("operationalReadiness");

        var first = ProjectJsonSerializer.Deserialize(document.ToJsonString()).Module.RuntimePolicies.Extensions
            .OperationalReadiness;
        var second = ProjectJsonSerializer.Deserialize(document.ToJsonString()).Module.RuntimePolicies.Extensions
            .OperationalReadiness;

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.TaskManifests.Single().Id, second.TaskManifests.Single().Id);
        Assert.True(first.ContentSynchronization.Resumable);
        Assert.True(first.Recovery.OfferRecoveryOnOpen);
    }

    [Fact]
    public void Pre_quality_assurance_project_migrates_to_deterministic_defaults()
    {
        var project = TestProjectFactory.CreateValid();
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(project))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.PreQualityAssuranceFormatVersion;
        document["project"]!["module"]!["runtimePolicies"]!["extensions"]!["modernTooling"]!
            .AsObject().Remove("qualityAssurance");

        var first = ProjectJsonSerializer.Deserialize(document.ToJsonString()).Module.RuntimePolicies.Extensions
            .ModernTooling.QualityAssurance;
        var second = ProjectJsonSerializer.Deserialize(document.ToJsonString()).Module.RuntimePolicies.Extensions
            .ModernTooling.QualityAssurance;

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Network.Id, second.Network.Id);
        Assert.Equal(9, first.Network.LatencyTicks.Count * first.Network.LossPercentages.Count);
        Assert.Equal(3, first.PlatformBudgets.Count);
        Assert.True(first.Profiler.OneClickCapture);
    }

    [Fact]
    public void Pre_governance_quality_project_migrates_accessibility_and_fuzzing_to_stable_defaults()
    {
        var project = TestProjectFactory.CreateValid();
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(project))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.PreGovernanceQualityFormatVersion;
        var quality = document["project"]!["module"]!["runtimePolicies"]!["extensions"]!["modernTooling"]!
            ["qualityAssurance"]!.AsObject();
        quality.Remove("accessibility");
        quality.Remove("securityFuzz");
        quality.Remove("clickableDiagnostics");
        quality.Remove("maliciousFixtures");
        quality.Remove("auroraParityCoverage");
        quality.Remove("acceptanceGates");

        var first = ProjectJsonSerializer.Deserialize(document.ToJsonString()).Module.RuntimePolicies.Extensions
            .ModernTooling.QualityAssurance;
        var second = ProjectJsonSerializer.Deserialize(document.ToJsonString()).Module.RuntimePolicies.Extensions
            .ModernTooling.QualityAssurance;

        Assert.Equal(first.Accessibility.Id, second.Accessibility.Id);
        Assert.Equal(first.SecurityFuzz.Id, second.SecurityFuzz.Id);
        Assert.Equal(first.ClickableDiagnostics.Id, second.ClickableDiagnostics.Id);
        Assert.Equal(first.MaliciousFixtures.Id, second.MaliciousFixtures.Id);
        Assert.Equal(first.AuroraParityCoverage.Id, second.AuroraParityCoverage.Id);
        Assert.Equal(first.AcceptanceGates.Id, second.AcceptanceGates.Id);
        Assert.True(first.Accessibility.EditorKeyboardOnly);
        Assert.True(first.Accessibility.RuntimeSubtitles);
        Assert.Equal(7, first.SecurityFuzz.Boundaries.Count);
        Assert.Equal(16, first.SecurityFuzz.CasesPerBoundary);
        Assert.Equal(7, first.MaliciousFixtures.Families.Count);
        Assert.Equal(717, first.AuroraParityCoverage.ExpectedFeatureCount);
        Assert.Contains("G8", first.AcceptanceGates.RequiredGates);
    }

    [Fact]
    public void Post_launch_capabilities_round_trip_and_pre_profile_documents_migrate_deterministically()
    {
        var project = TestProjectFactory.CreateValid();
        var source = project.Module.RuntimePolicies.Extensions;
        var post = source.PostLaunch;
        var gameplay = new GameplayProductionProfile(post.Gameplay.Id, 1,
            post.Gameplay.ZeroGravity with { Enabled = true },
            post.Gameplay.Vehicles with { Enabled = true }, post.Gameplay.Mounts,
            [new GameplayProductionProfile.AchievementDefinition(Guid.NewGuid(), "first-step", "module.started", 1,
                null, false)], post.Gameplay.CuratedPacks with
                {
                    Channel = "candidate",
                    Compatibility = "2.x",
                    AllowedLicenses = ["MIT", "CC0-1.0"],
                    RequireTrustedSignature = true,
                    AllowOfflineFallback = false
                }, "test");
        var director = new LiveDirectorProductionProfile(post.LiveDirector.Id, 1,
            post.LiveDirector.ForcedRest with { Enabled = true, MaximumTargets = 24 },
            post.LiveDirector.LevelAdjustment with
            {
                Enabled = true,
                MaximumDelta = 7,
                RequireRollbackSnapshot = true
            },
            [new LiveDirectorProductionProfile.SavedGroup(Guid.NewGuid(), "companions",
                "tag:companions", 3, 24)],
            post.LiveDirector.AppearanceOverrides with
            {
                Enabled = true,
                MinimumScale = .75f,
                MaximumScale = 1.75f
            },
            post.LiveDirector.SessionRecording with
            {
                Enabled = true,
                ExportFormat = "orp-replay",
                MaximumMinutes = 180,
                RedactPrivateData = true
            }, "test");
        var network = new NetworkProductionProfile(post.Network.Id, 1,
            post.Network.Voice with { Enabled = true, MaximumBitrateKbps = 112 },
            [new NetworkProductionProfile.ShardDefinition(Guid.NewGuid(), "crypt-eu",
                [project.Module.EntryAreaId.Value], 48, "eu-west")],
            post.Network.HotReload with
            {
                Enabled = true,
                MaximumPackageMegabytes = 256,
                DrainTimeoutSeconds = 45
            },
            post.Network.ServerExtensions with
            {
                Enabled = true,
                AllowedCapabilities = ["metrics", "world-read"],
                CpuBudgetMilliseconds = 75
            },
            post.Network.Protocol with { MaximumVersion = 3 }, "test");
        var configured = new PostLaunchCapabilityProfile(post.Id, 1, gameplay, director, network,
            post.CreatorExperience, post.Production, "test");
        var extensions = new ModuleRuntimeExtensionProfile(source.Id, 1, source.RuleSchemas, source.UiDocuments,
            source.JsonDocuments, source.SnapshotPolicy, "test", source.ModernTooling,
            source.OperationalReadiness, configured);
        var policies = project.Module.RuntimePolicies;
        var updatedPolicies = new Forge.Domain.RuntimePolicies.ModuleRuntimePolicyProfile(policies.Id, 1,
            policies.Heartbeats, policies.Rest, policies.SequenceAbort, "test", policies.Cinematics,
            policies.Gameplay, policies.Rules, extensions);
        var updated = new Forge.Domain.Projects.ProjectDefinition(project.Id, project.Title,
            project.Module.WithRuntimePolicies(updatedPolicies), project.Blueprints, project.Areas,
            project.Dialogues, project.Quests);

        var json = ProjectJsonSerializer.Serialize(updated);
        var restored = ProjectJsonSerializer.Deserialize(json).Module.RuntimePolicies.Extensions.PostLaunch;
        Assert.True(restored.Gameplay.ZeroGravity.Enabled);
        Assert.Equal("first-step", Assert.Single(restored.Gameplay.Achievements).Key);
        Assert.Equal("candidate", restored.Gameplay.CuratedPacks.Channel);
        Assert.Equal(["CC0-1.0", "MIT"], restored.Gameplay.CuratedPacks.AllowedLicenses);
        Assert.False(restored.Gameplay.CuratedPacks.AllowOfflineFallback);
        Assert.True(restored.LiveDirector.ForcedRest.Enabled);
        Assert.Equal(7, restored.LiveDirector.LevelAdjustment.MaximumDelta);
        Assert.Equal(.75f, restored.LiveDirector.AppearanceOverrides.MinimumScale);
        Assert.Equal("companions", Assert.Single(restored.LiveDirector.SavedGroups).Key);
        Assert.True(restored.LiveDirector.SessionRecording.Enabled);
        Assert.True(restored.LiveDirector.SessionRecording.RedactPrivateData);
        Assert.True(restored.Network.Voice.Enabled);
        Assert.Equal(112, restored.Network.Voice.MaximumBitrateKbps);
        Assert.Equal("crypt-eu", Assert.Single(restored.Network.Shards).Key);
        Assert.True(restored.Network.HotReload.Enabled);
        Assert.Equal(45, restored.Network.HotReload.DrainTimeoutSeconds);
        Assert.True(restored.Network.ServerExtensions.Enabled);
        Assert.Equal(["metrics", "world-read"], restored.Network.ServerExtensions.AllowedCapabilities);
        Assert.Equal(3, restored.Network.Protocol.MaximumVersion);
        Assert.Contains("\"formatVersion\": \"0.6.38\"", json, StringComparison.Ordinal);

        var old = JsonNode.Parse(json)!.AsObject();
        old["formatVersion"] = ProjectJsonSerializer.PrePostLaunchCapabilitiesFormatVersion;
        old["project"]!["module"]!["runtimePolicies"]!["extensions"]!.AsObject().Remove("postLaunch");
        var first = ProjectJsonSerializer.Deserialize(old.ToJsonString()).Module.RuntimePolicies.Extensions.PostLaunch;
        var second = ProjectJsonSerializer.Deserialize(old.ToJsonString()).Module.RuntimePolicies.Extensions.PostLaunch;
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.CreatorExperience.Graphics.Id, second.CreatorExperience.Graphics.Id);
        Assert.False(first.Production.MediaGeneration.Enabled);
    }
}
