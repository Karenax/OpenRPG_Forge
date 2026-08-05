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
}
