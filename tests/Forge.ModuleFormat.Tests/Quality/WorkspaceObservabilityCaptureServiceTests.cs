using System.Text.Json;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.RuntimePolicies;
using Forge.ModuleFormat.Quality;
using Forge.ModuleFormat.Tests.Serialization;

namespace Forge.ModuleFormat.Tests.Quality;

public sealed class WorkspaceObservabilityCaptureServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OpenRPGForgeObservabilityTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void OpenTelemetry_capture_is_confined_and_covers_editor_runtime_server_and_scripts()
    {
        Directory.CreateDirectory(Path.Combine(_root, "scripts"));
        File.WriteAllText(Path.Combine(_root, "scripts", "observability.forge"), "void main() {}\n");
        var project = ConfigureObservability(TestProjectFactory.CreateValid(), enabled: true,
            tracy: true, openTelemetry: true, includeScripts: true);

        var report = new WorkspaceObservabilityCaptureService().Run(_root, project,
            new(ObservabilityCaptureBackend.OpenTelemetry, 30, IncludeScripts: true, WriteArtifact: true));

        Assert.True(report.Succeeded);
        Assert.False(report.Cancelled);
        Assert.Equal(Enum.GetValues<QualityTraceSurface>(), report.Surfaces);
        Assert.Contains("script", report.Categories);
        Assert.True(report.SpanCount >= 5);
        Assert.NotEmpty(report.Fingerprint);
        var output = Path.Combine(_root, report.ArtifactPath!.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(output));
        using var document = JsonDocument.Parse(File.ReadAllText(output));
        Assert.Equal("open-telemetry-json-v1", document.RootElement.GetProperty("format").GetString());
        Assert.Equal(3, document.RootElement.GetProperty("resourceSpans").GetArrayLength());
    }

    [Fact]
    public void Disabled_incompatible_native_and_cancelled_captures_fail_with_localized_diagnostics()
    {
        Directory.CreateDirectory(_root);
        var service = new WorkspaceObservabilityCaptureService();
        var source = TestProjectFactory.CreateValid();
        var disabled = service.Run(_root, source,
            new(ObservabilityCaptureBackend.OpenTelemetry, 10, false, true));
        Assert.Equal("OBSERVABILITY_DISABLED", Assert.Single(disabled.Diagnostics).Code);
        Assert.Contains("/enabled", disabled.Diagnostics[0].Path, StringComparison.Ordinal);

        var openTelemetryOnly = ConfigureObservability(source, true, tracy: false, openTelemetry: true,
            includeScripts: false);
        var native = service.Run(_root, openTelemetryOnly,
            new(ObservabilityCaptureBackend.TracyNative, 10, false, true));
        Assert.Equal("OBSERVABILITY_TRACY_NOT_ALLOWED", Assert.Single(native.Diagnostics).Code);

        var tracyAllowed = ConfigureObservability(source, true, tracy: true, openTelemetry: true,
            includeScripts: false);
        var unavailable = service.Run(_root, tracyAllowed,
            new(ObservabilityCaptureBackend.TracyNative, 10, false, true));
        Assert.Equal("OBSERVABILITY_TRACY_RUNTIME_MISSING", Assert.Single(unavailable.Diagnostics).Code);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = service.Run(_root, tracyAllowed,
            new(ObservabilityCaptureBackend.OpenTelemetry, 10, false, true), cancellation.Token);
        Assert.True(cancelled.Cancelled);
        Assert.Equal("OBSERVABILITY_CAPTURE_CANCELLED", Assert.Single(cancelled.Diagnostics).Code);
        Assert.False(File.Exists(Path.Combine(_root,
            WorkspaceObservabilityCaptureService.OpenTelemetryArtifactRelativePath
                .Replace('/', Path.DirectorySeparatorChar))));
    }

    private static ProjectDefinition ConfigureObservability(ProjectDefinition project, bool enabled,
        bool tracy, bool openTelemetry, bool includeScripts)
    {
        var sourcePolicies = project.Module.RuntimePolicies;
        var extensions = sourcePolicies.Extensions;
        var post = extensions.PostLaunch;
        var creator = post.CreatorExperience;
        var configuredCreator = new CreatorExperienceProductionProfile(
            creator.Id, creator.SchemaVersion, creator.Water, creator.DeviceQueries, creator.ResourceCommands,
            creator.DynamicAreas, creator.PartyControl, creator.ProgressionTracks, creator.RepositoryMetadata,
            creator.Observability with
            {
                Enabled = enabled,
                TracyCompatible = tracy,
                OpenTelemetryCompatible = openTelemetry,
                IncludeScripts = includeScripts,
            },
            creator.Graphics, "test:observability");
        var configuredPost = new PostLaunchCapabilityProfile(post.Id, post.SchemaVersion, post.Gameplay,
            post.LiveDirector, post.Network, configuredCreator, post.Production, "test:observability");
        var configuredExtensions = new ModuleRuntimeExtensionProfile(extensions.Id, extensions.SchemaVersion,
            extensions.RuleSchemas, extensions.UiDocuments, extensions.JsonDocuments, extensions.SnapshotPolicy,
            "test:observability", extensions.ModernTooling, extensions.OperationalReadiness, configuredPost);
        var policies = new ModuleRuntimePolicyProfile(sourcePolicies.Id, sourcePolicies.SchemaVersion,
            sourcePolicies.Heartbeats, sourcePolicies.Rest, sourcePolicies.SequenceAbort, "test:observability",
            sourcePolicies.Cinematics, sourcePolicies.Gameplay, sourcePolicies.Rules, configuredExtensions,
            sourcePolicies.GameplayEvents, sourcePolicies.World, sourcePolicies.LifecycleEvents);
        return new ProjectDefinition(project.Id, project.Title, project.Module.WithRuntimePolicies(policies),
            project.Blueprints, project.Areas, project.Dialogues, project.Quests);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
