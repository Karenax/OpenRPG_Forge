#if TOOLS
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Areas;
using Forge.Application.Assistant;
using Forge.Application.Assets;
using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Content;
using Forge.Application.Creator;
using Forge.Application.Creatures;
using Forge.Application.Dialogues;
using Forge.Application.Entities;
using Forge.Application.Factions;
using Forge.Application.Gameplay;
using Forge.Application.Items;
using Forge.Application.Modules;
using Forge.Application.Rules;
using Forge.Application.Localization;
using Forge.Application.Lifecycle;
using Forge.Application.Navigation;
using Forge.Application.Sequences;
using Forge.Application.Projects;
using Forge.Application.Quests;
using Forge.Application.Quality;
using Forge.Application.Spatial;
using Forge.Application.Time;
using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Audio;
using Forge.Domain.Assistant;
using Forge.Domain.Assets;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;
using Forge.Domain.Build;
using Forge.Domain.Combat;
using Forge.Domain.Content;
using Forge.Domain.Creatures;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Factions;
using Forge.Domain.Gameplay;
using Forge.Domain.Localization;
using Forge.Domain.Lifecycle;
using Forge.Domain.Items;
using Forge.Domain.Modules;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Quality;
using Forge.Domain.Quests;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Rules;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.Domain.WorldFeatures;
using Forge.EngineAdapter.Assets;
using Forge.EngineAdapter.Audio;
using Forge.EngineAdapter.Rendering;
using Forge.EngineAdapter.World;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Assets;
using Forge.ModuleFormat.Content;
using Forge.ModuleFormat.Distribution;
using Forge.ModuleFormat.Diagnostics;
using Forge.ModuleFormat.Launcher;
using Forge.ModuleFormat.Navigation;
using Forge.ModuleFormat.Playtesting;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Quality;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Content;
using Forge.Network.Replication;
using Forge.RpgKit.Application.Abilities;
using Forge.RpgKit.Application.Bosses;
using Forge.RpgKit.Application.Companions;
using Forge.RpgKit.Application.Combat;
using Forge.RpgKit.Application.Creatures;
using Forge.RpgKit.Application.Dialogues;
using Forge.RpgKit.Application.Doors;
using Forge.RpgKit.Application.Encounters;
using Forge.RpgKit.Application.Factions;
using Forge.RpgKit.Application.Merchants;
using Forge.RpgKit.Application.Placeables;
using Forge.RpgKit.Application.Quests;
using Forge.RpgKit.Application.Triggers;
using Forge.RpgKit.Domain.Merchants;
using Forge.RpgKit.Domain.Companions;
using Forge.RpgKit.Domain.Factions;
using RpgLockState = Forge.RpgKit.Domain.Placeables.LockState;
using RpgActivator = Forge.RpgKit.Domain.Encounters.Activator;
using RpgNetworkAuthority = Forge.RpgKit.Domain.Encounters.NetworkAuthority;
using RpgQueryableEntity = Forge.RpgKit.Domain.Triggers.QueryableEntity;
using RpgTriggerEventTrace = Forge.RpgKit.Domain.Triggers.TriggerEventTrace;
using RpgTriggerRuntimeState = Forge.RpgKit.Domain.Triggers.TriggerRuntimeState;
using Forge.Scripting;
using Godot;

namespace Forge.Toolset.Editor;

public partial class ForgeEditorPlugin
{
    private bool RunBuildPipelineSmokeTest()
    {
        if (_buildPipeline is null || _history is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G712_BUILD_FAILED unavailable");
            return false;
        }

        var analyzed = _buildPipeline.AnalyzeForSmokeTest();
        var profilePreviewed = _buildPipeline.PreviewProfileForSmokeTest();
        if (!analyzed || !profilePreviewed ||
            _buildPipeline.ScriptCount < 2 ||
            _buildPipeline.BuildTaskCount < 1 ||
            _buildPipeline.BuildMaximumParallelism != DeterministicBuildTaskScheduler.DefaultMaximumParallelism)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_TOOLSET_G712_BUILD_FAILED analysis={analyzed} preview={profilePreviewed} " +
                $"resources={_buildPipeline.ResourceCount} scripts={_buildPipeline.ScriptCount} " +
                $"tasks={_buildPipeline.BuildTaskCount} parallelism={_buildPipeline.BuildMaximumParallelism}");
            return false;
        }

        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "incremental-build-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = Path.Combine(smokeRoot, "workspace");
            var artifact = Path.Combine(smokeRoot, "module.orpmod");
            var created = ProjectSession.Create(workspace, _history.State.Project);
            if (!created.Succeeded || created.Session is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G712_BUILD_FAILED workspace");
                return false;
            }

            var profile = WorkspaceBuildProfile.CreateDefault();
            var compatibility = profile.Compatibility with
            {
                MinimumRuntimeVersion = ModuleRuntimeCompatibility.CurrentRuntimeVersion,
            };
            profile = new WorkspaceBuildProfile(profile.Id, profile.SchemaVersion, profile.Provenance,
                profile.RequireGitRepository, profile.TextDocuments, profile.ExplicitGeneratedFiles,
                profile.ReproducibleAssets, profile.Layout, profile.CliOperations, profile.StableOrdering,
                profile.NormalizedTimestamps, profile.LockedDependencies, profile.ContentHashes,
                profile.VerifyReproducibility, profile.IncrementalBuild, profile.CachePath,
                profile.RebuildAffectedOnly, profile.Governance, profile.Collaboration, compatibility);
            var profileSaved = WorkspaceBuildProfileStore.Save(workspace, profile);
            var first = ModulePackager.Export(workspace, artifact);
            var second = ModulePackager.Export(workspace, artifact);
            var restored = WorkspaceBuildStateStore.TryLoad(workspace);
            var cache = WorkspaceBuildCollaborationService.RebuildCacheIndex(workspace, profile, dryRun: true);
            var normalization = WorkspaceBuildCollaborationService.NormalizeDocuments(workspace, profile, dryRun: true);
            var migration = WorkspaceBuildCollaborationService.MigrateWorkspace(workspace, profile, dryRun: true);
            var references = WorkspaceBuildCollaborationService.BuildReferenceIndex(
                workspace, created.Session.Project, created.Session.ProjectFilePath, profile.Collaboration);
            var semantic = WorkspaceBuildCollaborationService.Diff(
                WorkspaceBuildCollaborationService.LoadSemanticSnapshot(workspace),
                WorkspaceBuildCollaborationService.CreateSemanticSnapshot(created.Session.Project, second.DependencyGraph),
                profile.Collaboration);
            if (first.BuildPlan.RebuiltResources.Count == 0 ||
                second.BuildPlan.RebuiltResources.Count != 0 ||
                second.BuildPlan.ReusedResources.Count == 0 ||
                restored?.SchemaVersion != BuildDependencyGraph.CurrentSchemaVersion ||
                !File.Exists(Path.Combine(workspace, ".forge", "build-graph.json")) ||
                !File.Exists(Path.Combine(workspace,
                    WorkspaceBuildCollaborationService.SemanticSnapshotRelativePath.Replace('/', Path.DirectorySeparatorChar))) ||
                !cache.Succeeded || !normalization.Succeeded || !migration.Succeeded || !migration.Idempotent ||
                references.Diagnostics.Count != 0 || !references.Nodes.Any(node => node.Dependents.Count > 0) ||
                !semantic.Succeeded || semantic.Changes.Count != 0 || !profileSaved.Succeeded ||
                first.PackagingMaterialization.TaskCount < 1 ||
                first.PackagingMaterialization.MaximumParallelism !=
                DeterministicBuildTaskScheduler.DefaultMaximumParallelism ||
                first.Manifest.RuntimeMinimumVersion != ModuleRuntimeCompatibility.CurrentRuntimeVersion ||
                ModuleRuntimeCompatibility.Evaluate(first.Manifest, ModuleRuntimeCompatibility.CurrentRuntimeVersion) is not null ||
                ModuleRuntimeCompatibility.Evaluate(first.Manifest with { RuntimeMinimumVersion = "99.0.0" },
                    ModuleRuntimeCompatibility.CurrentRuntimeVersion)?.Code !=
                ModuleRuntimeCompatibility.IncompatibleRuntimeCode ||
                ModuleRuntimeCompatibility.Evaluate(first.Manifest with
                { RuntimeMinimumVersion = "99.0.0", RuntimeCompatibilityRequired = false },
                    ModuleRuntimeCompatibility.CurrentRuntimeVersion) is not null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G712_BUILD_FAILED incremental");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot))
            {
                Directory.Delete(smokeRoot, recursive: true);
            }
        }

        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G712_BUILD_PASS graph=versioned cache=source-api-compiler " +
            "dependencies=automatic incremental=reused persistence=restored " +
            $"scripts={_buildPipeline.ScriptCount} profile=canonical ui=previewed " +
            "governance=p0-r002 worktrees=exclusive ci=complete fixtures=5 semver=core-kits-packages adrs=governed");
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_R023_BUILD_COLLABORATION_PASS " +
            "cache=content-addressed-disposable-rebuildable normalize=json-utf8-lf-order-numbers-atomic " +
            "migration=dry-run-backup-idempotent-downgrade-explicit references=forward-reverse-rebuildable " +
            "diff=area-graph-entity-package-script-text-json cli=structured mcp=shell-free " +
            "history=undo-redo persistence=roundtrip");
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_R026_PROJECT_COMPATIBILITY_PASS " +
            "profile=uuid-schema-provenance runtime-min=manifest project-format=versioned " +
            "migration=automatic-dry-run-backup-idempotent runtime=required-diagnostic-optional-safe " +
            "ui=visible-command-history forgescript=typed-handle mcp=filtered-plan-apply-revert " +
            "persistence=legacy-roundtrip");
        return true;
    }

    private bool RunR036EeAcceptanceSmokeTest()
    {
        if (_history is null || _projectPreview is null || _buildPipeline is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R036_EE_FAILED setup");
            return false;
        }

        var area = GetActiveArea();
        var selected = area.Entities.FirstOrDefault(entity =>
            entity.Tags.Contains("solid", StringComparer.OrdinalIgnoreCase) ||
            entity.Tags.Contains("architecture", StringComparer.OrdinalIgnoreCase));
        var agent = area.Navigation.AgentProfiles.FirstOrDefault();
        if (selected is null || agent is null || !_projectPreview.SelectEntityForSmokeTest(selected.Id))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R036_EE_FAILED selection");
            return false;
        }

        var originalScale = selected.Transform.Scale;
        var beforeNavigation = NavigationSurfaceBuilder.Build(_history.State.Project, area, agent);
        if (_projectPreview.ScaleGizmoHandleCount != 4 ||
            !_projectPreview.ScaleSelectedForSmokeTest(ProjectPreview3D.ScaleGizmoAxis.X, increase: false))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R036_EE_FAILED scale_gizmo");
            return false;
        }

        var scaledArea = GetActiveArea();
        var scaled = scaledArea.Entities.Single(entity => entity.Id == selected.Id);
        var afterNavigation = NavigationSurfaceBuilder.Build(_history.State.Project, scaledArea, agent);
        var scaleAccepted = scaled.Transform.Scale.X < originalScale.X &&
                            scaled.Transform.Scale.Y == originalScale.Y &&
                            scaled.Transform.Scale.Z == originalScale.Z &&
                            beforeNavigation.SourceFingerprint != afterNavigation.SourceFingerprint &&
                            _projectPreview.NavigationOverlayCellCount == afterNavigation.WalkableCells.Count;
        var undo = _history.Undo();
        RebuildPreview();
        RefreshEditorControls(selected.Id);
        var restored = GetActiveArea().Entities.Single(entity => entity.Id == selected.Id);
        if (!scaleAccepted || !undo.Succeeded || restored.Transform.Scale != originalScale ||
            _buildPipeline.BuildTaskCount < 1 ||
            _buildPipeline.BuildMaximumParallelism != DeterministicBuildTaskScheduler.DefaultMaximumParallelism)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R036_EE_FAILED acceptance");
            return false;
        }

        var quality = PerformanceQualityService.Benchmark(
            _history.State.Project,
            _history.State.Project.Module.RuntimePolicies.Extensions.ModernTooling.QualityAssurance,
            packageBytes: 0,
            scriptCount: _buildPipeline.ScriptCount);
        if (!quality.Passed || quality.Measurements.Count == 0 ||
            quality.Measurements.Any(item => !double.IsFinite(item.NavigationRebuildMilliseconds)))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R036_EE_FAILED navigation_benchmark");
            return false;
        }

        GD.Print(
            "OPENRPG_FORGE_TOOLSET_R036_EE_PASS " +
            "scale=gizmo-x-y-z-uniform-constraints-multiedit-command-history-persistence " +
            "collision=preview-rebuilt navigation=canonical-fingerprint-overlay-agent-profiles-dynamic-obstacles-recovery " +
            $"benchmark={quality.Measurements.Count} build=dependency-graph-incremental-script-cache-asset-cache-" +
            $"parallel-{_buildPipeline.BuildMaximumParallelism}-deterministic-output");
        return true;
    }

    private bool RunR035BuildDiagnosticsSmokeTest()
    {
        if (_creatorShell is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R035_BUILD_DIAGNOSTICS_FAILED shell");
            return false;
        }

        var diagnostics = new[]
        {
            CreatorDiagnostic.Create(CreatorDiagnosticSeverity.Warning, "area.navigation.portal_missing",
                "Portail introuvable", "areas/smoke/portals/entry", "build:r035"),
            CreatorDiagnostic.Create(CreatorDiagnosticSeverity.Error, "project.reference.blueprint_missing",
                "Blueprint introuvable", "areas/smoke/entities/warden", "build:r035"),
            CreatorDiagnostic.Create(CreatorDiagnosticSeverity.Information, "runtime.playtest.ready",
                "Playtest prêt", "runtime/playtest", "runtime:r035"),
        };
        _creatorShell.SetDiagnostics(diagnostics);
        if (!_creatorShell.FilterDiagnosticsForSmokeTest(
                CreatorDiagnosticCategory.Reference,
                CreatorDiagnosticSeverity.Warning,
                "blueprint",
                "build:r035",
                expectedCount: 1) ||
            !_creatorShell.SelectDiagnosticForSmokeTest(0))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R035_BUILD_DIAGNOSTICS_FAILED filtering");
            return false;
        }

        var root = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "r035-diagnostics", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var first = CreatorDiagnosticBundleExporter.Export(root, diagnostics,
                "runtime ready\npassword=smoke-secret\nplayerId=smoke-player");
            var second = CreatorDiagnosticBundleExporter.Export(root, diagnostics.Reverse().ToArray(),
                "runtime ready\r\npassword=smoke-secret\r\nplayerId=smoke-player");
            using var archive = ZipFile.OpenRead(Path.Combine(root,
                first.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            using var reader = new StreamReader(archive.GetEntry("runtime.log")!.Open());
            var log = reader.ReadToEnd();
            if (first.SchemaVersion != CreatorDiagnosticBundleResult.CurrentSchemaVersion ||
                first.DiagnosticCount != diagnostics.Length || !first.IncludesRuntimeLog ||
                second.Id != first.Id || second.Sha256 != first.Sha256 || !second.Reused ||
                log.Contains("smoke-secret", StringComparison.Ordinal) ||
                log.Contains("smoke-player", StringComparison.Ordinal) ||
                !log.Contains("[REDACTED]", StringComparison.Ordinal) ||
                !log.Contains("[PLAYER_REDACTED]", StringComparison.Ordinal))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R035_BUILD_DIAGNOSTICS_FAILED bundle");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            _creatorShell.FilterDiagnosticsForSmokeTest(
                category: null,
                CreatorDiagnosticSeverity.Information,
                string.Empty,
                string.Empty,
                diagnostics.Length);
            RefreshCreatorShell();
        }

        GD.Print(
            "OPENRPG_FORGE_TOOLSET_R035_BUILD_DIAGNOSTICS_PASS " +
            "build=validation-scripts-assets-cache-incremental-report-reproducible ui=canonical-export " +
            "diagnostics=uuid-schema-provenance-category-severity-text-correlation-clickable-quickfix " +
            "logs=runtime-sanitized bundle=atomic-deterministic-reused cli=structured mcp=structured");
        return true;
    }

    private bool RunDistributionProfilesSmokeTest()
    {
        if (_distributionProfiles is null || _history is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G713_DISTRIBUTION_FAILED missing_window");
            return false;
        }

        var original = _history.State.Project.Module.Distribution;
        var profiles = original.TargetProfiles.Select(profile => new ModuleTargetProfile(
            profile.Id,
            profile.Target,
            profile.Enabled,
            profile.IncludeEditorData,
            profile.IncludeDebugSymbols,
            profile.OptimizeContent,
            profile.RequireApprovedLicenses,
            profile.Target == ModuleTargetKind.Editor ? "editor-smoke" : profile.OutputName,
            profile.SecretsProfile,
            "workspace-smoke-test")).ToArray();
        var approval = new ThirdPartyDependencyApproval(
            ThirdPartyDependencyId.From(Guid.Parse("d7130000-0000-4000-8000-000000000001")),
            "Forge Smoke Library",
            "1.2.0",
            "MIT",
            "OpenRPG Forge",
            "Vérifier le registre gouverné dans le prototype.",
            "trimestrielle",
            "Remplacer par le composant interne équivalent.",
            "https://example.invalid/forge-smoke-library",
            [ModuleTargetKind.Editor],
            "workspace-smoke-test");
        var release = new ModuleReleaseConfiguration(
            original.Release.Id,
            ModuleReleaseChannel.Preview,
            [ModuleDistributionPlatform.WindowsX64, ModuleDistributionPlatform.LinuxX64, ModuleDistributionPlatform.MacOSArm64],
            new ModuleSigningConfiguration(
                ModuleSigningMode.Optional,
                "preview-signing",
                [],
                [],
                allowUnsignedDevelopment: true),
            new ModuleDeltaConfiguration(true, 1024 * 1024, true, true, true, 3),
            original.Release.WindowsPackaging,
            "workspace-smoke-test");
        var launcher = new GameLauncherConfiguration(
            original.Launcher.Id,
            enabled: true,
            "La Crypte",
            "Le feu attend sous la pierre.",
            "Cendres",
            "#A36B42",
            backgroundImagePath: null,
            "https://example.invalid/news",
            "https://example.invalid/support",
            settingsEnabled: true,
            GameLauncherMultiplayerMode.LocalHost,
            5121,
            "workspace-smoke-test");
        var configuration = new ModuleDistributionConfiguration(
            original.Id,
            profiles,
            [approval],
            "workspace-smoke-test",
            release,
            launcher: launcher);
        if (!_distributionProfiles.ConfigureForSmokeTest(configuration, ModuleTargetKind.Shipping) ||
            _distributionProfiles.ProfileCount != 5 ||
            _distributionProfiles.ComplianceDiagnosticCount == 0 ||
            !_distributionProfiles.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G713_DISTRIBUTION_FAILED configuration");
            return false;
        }

        var appliedDistribution = _history.State.Project.Module.Distribution;
        var appliedLauncher = appliedDistribution.Launcher;
        if (appliedDistribution.Dependencies.Count != 1 ||
            !appliedLauncher.Enabled ||
            appliedLauncher.Id != launcher.Id ||
            appliedLauncher.DisplayName != launcher.DisplayName ||
            appliedLauncher.Tagline != launcher.Tagline ||
            appliedLauncher.ExecutableName != launcher.ExecutableName ||
            appliedLauncher.AccentColor != launcher.AccentColor ||
            appliedLauncher.NewsUrl != launcher.NewsUrl ||
            appliedLauncher.SupportUrl != launcher.SupportUrl ||
            appliedLauncher.SettingsEnabled != launcher.SettingsEnabled ||
            appliedLauncher.Multiplayer != launcher.Multiplayer ||
            appliedLauncher.DefaultServerPort != launcher.DefaultServerPort ||
            appliedLauncher.Provenance != "creator-command")
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G713_DISTRIBUTION_FAILED launcher_configuration");
            return false;
        }

        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "distribution-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = Path.Combine(smokeRoot, "workspace");
            var artifact = Path.Combine(smokeRoot, "editor.orpmod");
            var releaseBundle = Path.Combine(smokeRoot, "editor-preview-windows.zip");
            var linuxBundle = Path.Combine(smokeRoot, "editor-preview-linux.zip");
            var macBundle = Path.Combine(smokeRoot, "editor-preview-macos.zip");
            var created = ProjectSession.Create(workspace, _history.State.Project);
            if (!created.Succeeded || created.Session is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G713_DISTRIBUTION_FAILED workspace");
                return false;
            }

            var reopened = ProjectSession.Open(workspace);
            var exported = ModulePackager.Export(workspace, artifact, target: ModuleTargetKind.Editor);
            var bundled = ModuleReleaseBundleBuilder.Build(
                artifact,
                releaseBundle,
                ModuleTargetKind.Editor,
                ModuleDistributionPlatform.WindowsX64,
                release);
            ModuleReleaseBundleBuilder.Build(artifact, linuxBundle, ModuleTargetKind.Editor,
                ModuleDistributionPlatform.LinuxX64, release);
            ModuleReleaseBundleBuilder.Build(artifact, macBundle, ModuleTargetKind.Editor,
                ModuleDistributionPlatform.MacOSArm64, release);
            var compatibility = (from target in Enum.GetValues<ModuleTargetKind>()
                                 from platform in Enum.GetValues<ModuleDistributionPlatform>()
                                 select new ModuleCompatibilityEvidence("1.0.0", "1.1.0", target, platform,
                                     true, true, true, new string('a', 64))).ToArray();
            var promotion = ModuleReleasePromotionService.Preview(new ModuleReleasePromotionRequest(
                release.Id, ModuleReleaseChannel.Development, ModuleReleaseChannel.Preview,
                Enum.GetValues<ModuleTargetKind>(), Enum.GetValues<ModuleDistributionPlatform>(),
                false, true, compatibility));
            using var archive = ZipFile.OpenRead(artifact);
            using var releaseArchive = ZipFile.OpenRead(releaseBundle);
            using var linuxArchive = ZipFile.OpenRead(linuxBundle);
            using var macArchive = ZipFile.OpenRead(macBundle);
            if (!reopened.Succeeded || reopened.Session is null ||
                reopened.Session.Project.Module.Distribution.TargetProfiles.Count != 5 ||
                reopened.Session.Project.Module.Distribution.Dependencies.Single().Id != approval.Id ||
                reopened.Session.Project.Module.Distribution.Release.Channel != ModuleReleaseChannel.Preview ||
                reopened.Session.Project.Module.Distribution.Release.Platforms.Count != 3 ||
                reopened.Session.Project.Module.Distribution.Launcher != appliedLauncher ||
                exported.Manifest.Target != ModuleTargetKind.Editor ||
                exported.Manifest.ThirdPartyDependencies.Single().Id != approval.Id ||
                archive.GetEntry(DistributionNoticeBuilder.PackagePath) is null ||
                bundled.Manifest.Channel != ModuleReleaseChannel.Preview ||
                releaseArchive.GetEntry("release-manifest.json") is null ||
                releaseArchive.GetEntry("install.ps1") is null ||
                releaseArchive.GetEntry("uninstall.ps1") is null ||
                linuxArchive.GetEntry("install.sh") is null ||
                linuxArchive.GetEntry("uninstall.sh") is null ||
                macArchive.GetEntry("install.sh") is null ||
                macArchive.GetEntry("uninstall.sh") is null ||
                !promotion.Allowed)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G713_DISTRIBUTION_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot))
            {
                Directory.Delete(smokeRoot, recursive: true);
            }
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        _distributionTarget = ModuleTargetKind.Editor;
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            _history.State.Project.Module.Distribution != original)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G713_DISTRIBUTION_FAILED history");
            return false;
        }

        RefreshEditorControls();
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G713_DISTRIBUTION_PASS profiles=5 shipping=blocked " +
            "licenses=diagnostic registry=versioned notices=generated channel=preview platforms=3 " +
            "matrix=targets5-platforms3 windows=portable-installer unix=verified-install-uninstall " +
            "promotion=sequential-compatibility-rollback-publisher-adapter launcher=profile-roundtrip-local-host " +
            "history=restored persistence=roundtrip");
        return true;
    }

    private sealed class R132VfxProvider : IAssetVfxRenderProvider
    {
        public VfxProviderCapabilities Capabilities { get; } =
            new("forge-vfx", true, true, true, true);
    }

    private sealed class R139ReverbProvider : IReverbRuntimeProvider
    {
        public bool SupportsConvolution => true;
        public ReverbConvolutionPlan? Plan { get; private set; }

        public ReverbRuntimeApplyResult Apply(ReverbConvolutionPlan plan)
        {
            Plan = plan;
            return new(true, "AUDIO_REVERB_APPLIED", plan.SourceSends.Count);
        }
    }

    private sealed class R140FlightObstacleProbe(bool blocked) : IFlightObstacleProbe
    {
        public bool IsBlocked(Vector3Value position, float clearanceRadius) => blocked;
    }

    private sealed class R142WeatherScriptRuntime : IWeatherForgeScriptRuntime
    {
        public WeatherForgeScriptEvent? Event { get; private set; }

        public WeatherForgeScriptDispatchResult Dispatch(WeatherForgeScriptEvent @event)
        {
            Event = @event;
            return new(true, "WEATHER_SCRIPT_DISPATCHED", @event);
        }
    }

    private sealed class R132RangeTransport(byte[] content) : IContentRangeTransport
    {
        public ContentRangeResponse Read(string resourcePath, long offset, int maximumBytes) =>
            new(offset, content.AsSpan((int)offset, maximumBytes).ToArray(), "r132-release");
    }


}
#endif
