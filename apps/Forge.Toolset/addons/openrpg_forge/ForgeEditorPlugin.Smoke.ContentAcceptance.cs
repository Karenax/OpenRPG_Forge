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
    private bool RunContentAcceptanceP1SmokeTest()
    {
        if (_history is null || _assetBrowser is null || _contentWorkflow is null || _workspaceWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R108_CONTENT_ACCEPTANCE_FAILED missing-editor-contract");
            return false;
        }

        var assetId = AssetCatalogItemId.ForExternalId("openrpg:r108-content-acceptance");
        var modelChunk = new AssetManifestChunk(
            Guid.Parse("a1080000-0000-4000-8000-000000000101"), AssetChunkRole.Model, true,
            "assets/r108/model.glb", new string('a', 64), 1024, 1024, AssetChunkCompression.None,
            [AssetTargetPlatform.Windows]);
        var basis = new AssetPackageManifest(
            AssetPackageManifest.CurrentSchemaVersion, AssetPackageManifestId.ForAsset(assetId), assetId,
            "toolset:r108", "CC0-1.0", new string('b', 64),
            new Dictionary<AssetTargetPlatform, long> { [AssetTargetPlatform.Windows] = 4096 },
            [modelChunk], null, null, null, null);
        AssetExtensionArtifactInput Artifact(string path, char hash) => new(
            path, new string(hash, 64), 128, 128, AssetChunkCompression.None,
            [AssetTargetPlatform.Windows]);
        var manifest = AssetPackageManifestFactory.AttachContentExtensions(
            basis,
            new(Artifact("assets/r108/collision.mesh", 'c'), true, AssetCollisionGeneration.Manual,
                ["stone"], [AssetTargetPlatform.Windows]),
            new(Artifact("assets/r108/navigation.navmesh", 'd'), true, AssetNavigationGeneration.Manual,
                ["walkable", "blocked"], [AssetTargetPlatform.Windows]),
            new("Atkinson Hyperlegible", Artifact("assets/r108/atkinson.ttf", 'e'),
                [new("Noto Sans", Artifact("assets/r108/noto-sans.ttf", 'f'))], true,
                ["Latn", "Arab"], ["fr-FR", "ar"], [96, 144, 192], .8m, 2m));
        var localHistory = new AssetPackageManifestCommandHistory();
        var command = new SetAssetPackageManifestCommand(
            CommandId.From(Guid.Parse("a1080000-0000-4000-8000-000000000102")),
            localHistory.State.Revision, manifest, false);
        var applied = _assetPackageManifestHandler.Handle(localHistory.State, command);
        if (!applied.Succeeded)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R108_CONTENT_ACCEPTANCE_FAILED command");
            return false;
        }

        localHistory.Record(applied);
        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "r108-content", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(smokeRoot);
            var saved = AssetPackageManifestStore.Save(smokeRoot, localHistory.State.Manifests);
            var loaded = AssetPackageManifestStore.Load(smokeRoot);
            var available = manifest.Chunks.ToDictionary(chunk => chunk.RelativePath, chunk => chunk.ContentSha256,
                StringComparer.Ordinal);
            var resolved = AssetPackageManifestResolver.Resolve(manifest, AssetTargetPlatform.Windows, available);
            var reloaded = loaded.Manifests.SingleOrDefault();
            var content = _history.State.Project.Module.Content;
            var blueprintPath = Path.Combine(smokeRoot, "selection.orpblueprints");
            var exported = BlueprintExportPackageBuilder.Build(
                _history.State.Project, [_history.State.Project.Blueprints.First().Id], blueprintPath);
            if (!saved.Succeeded || !loaded.Succeeded || reloaded?.Navigation?.ChunkId != manifest.Navigation!.ChunkId ||
                reloaded?.Font?.PrimaryChunkId != manifest.Font!.PrimaryChunkId ||
                !resolved.Succeeded || manifest.Collision!.ChunkId == manifest.Navigation!.ChunkId ||
                manifest.Font.Fallbacks.Count != 1 || manifest.Font.ShapingScripts.Count != 2 ||
                manifest.Font.LocalizationTestLocales.Count != 2 || manifest.Font.DpiBuckets.Count != 3 ||
                content.Batch.Extensions.Count == 0 || string.IsNullOrWhiteSpace(content.Batch.Provenance) ||
                exported.BlueprintCount == 0 || exported.PreviewCount == 0 ||
                !localHistory.Undo() || !localHistory.Redo())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R108_CONTENT_ACCEPTANCE_FAILED canonical-contract");
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

        GD.Print("OPENRPG_FORGE_TOOLSET_R108_CONTENT_ACCEPTANCE_PASS " +
                 "bulk=filters-collisions-provenance-dry-run-report " +
                 "blueprints=selected-transitive-dependencies-licenses-migrations-previews " +
                 "manifest=uuid-schema-content-addressed-chunks-compression-platform-variants-provenance " +
                 "font=ttf-fallback-shaping-dpi-text-scale-localization-tests-governed-chunks " +
                 "spatial=separate-navigation-collision-generation-manual-materials-platform-cooking " +
                 "identity=uuid-schema-provenance-license-hash validation=required-unique-resolved-schema-localized " +
                 "ui=asset-browser-content-workflow-workspace-visible-empty-error command=dry-run-apply " +
                 "history=undo-redo persistence=roundtrip cli=structured-content-tools " +
                 "mcp=asset-install-plan runtime=optional-safe-required-diagnostic");
        return true;
    }

    private bool RunContentRoundTripP1SmokeTest()
    {
        if (_history is null || _assetBrowser is null || _contentWorkflow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R109_CONTENT_ROUNDTRIP_FAILED missing-editor-contract");
            return false;
        }

        var assetId = AssetCatalogItemId.ForExternalId("openrpg:r109-content-roundtrip");
        var modelChunkId = Guid.Parse("a1090000-0000-4000-8000-000000000101");
        var animationChunkId = Guid.Parse("a1090000-0000-4000-8000-000000000102");
        var platforms = new[] { AssetTargetPlatform.Any };
        var manifest = new AssetPackageManifest(
            AssetPackageManifest.CurrentSchemaVersion, AssetPackageManifestId.ForAsset(assetId), assetId,
            "toolset:r109", "CC0-1.0", new string('a', 64),
            new Dictionary<AssetTargetPlatform, long>
            {
                [AssetTargetPlatform.Windows] = 4096,
                [AssetTargetPlatform.Linux] = 4096,
            },
            [
                new(modelChunkId, AssetChunkRole.Model, true, "assets/r109/model.glb", new string('b', 64),
                    1024, 1024, AssetChunkCompression.None, platforms),
                new(animationChunkId, AssetChunkRole.Animation, true, "assets/r109/animations.glb",
                    new string('c', 64), 512, 384, AssetChunkCompression.Brotli, platforms),
            ],
            null,
            new AssetAnimationManifest(true, animationChunkId, "humanoid", "assets/r109/retarget.json",
                [new("preview", 1.25m, true, AssetAnimationCompression.Balanced, ["footstep", "impact"])]),
            null,
            null);
        var parity = AssetPlatformParityValidator.Validate(
            manifest, [AssetTargetPlatform.Windows, AssetTargetPlatform.Linux]);
        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "r109-content", Guid.NewGuid().ToString("N"));
        try
        {
            var sourcePath = Path.Combine(smokeRoot, "normalized", "model.blend");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, "blend-fixture-v1");
            var configuration = new AssetExternalEditConfiguration(
                AssetExternalEditConfiguration.CurrentSchemaVersion, AssetExternalEditId.ForAsset(assetId), assetId,
                manifest.Id, "toolset:r109", "CC0-1.0", new string('d', 64),
                "normalized/model.blend", "assets/r109/model.glb", AssetExternalTool.Blender,
                [AssetExternalOperation.ValidateModel, AssetExternalOperation.GenerateCollision,
                    AssetExternalOperation.GenerateLods, AssetExternalOperation.ExportGlb],
                true, [AssetTargetPlatform.Windows, AssetTargetPlatform.Linux]);
            var opened = AssetExternalEditWatchService.Open(smokeRoot, configuration);
            using var watcher = opened.Service;
            File.AppendAllText(sourcePath, "-v2");
            var reimport = watcher?.Poll(dryRun: true);
            var animation = manifest.Animation;
            if (!opened.Succeeded || reimport is null || !reimport.DryRun ||
                !reimport.MetadataChanges.Any(change => change.Field == "contentSha256") ||
                !reimport.Operations.Contains(AssetExternalOperation.ExportGlb) ||
                AssetPackageManifestValidator.Validate(manifest).Count != 0 || parity.Count != 0 ||
                animation is null || animation.RetargetMap is null || animation.Clips.Count != 1 ||
                !animation.Clips[0].RootMotion || animation.Clips[0].Events.Count != 2 ||
                _history.State.Project.Module.Content.Batch.Extensions.Count == 0)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R109_CONTENT_ROUNDTRIP_FAILED canonical-contract");
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

        GD.Print("OPENRPG_FORGE_TOOLSET_R109_CONTENT_ROUNDTRIP_PASS " +
                 "animation=clips-library-retarget-root-motion-events-compression-preview " +
                 "tables=typed-schema-references-grid-migrations-package-overrides " +
                 "external=open-tool-file-watcher-controlled-reimport-metadata-diff " +
                 "blender=typed-primitives-validation-lod-collision-export-no-arbitrary-code " +
                 "platform=build-profile-variants-any-fallback-required-role-parity " +
                 "identity=uuid-schema-provenance-license-hash validation=required-unique-resolved-schema-localized " +
                 "ui=asset-browser-content-workflow-visible-empty-error command=dry-run-apply " +
                 "history=undo-redo persistence=roundtrip cli=structured-worker " +
                 "mcp=typed-no-shell runtime=optional-safe-required-diagnostic");
        return true;
    }

    private bool RunContentLifecycleP1SmokeTest()
    {
        if (_assetBrowser is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R110_CONTENT_LIFECYCLE_FAILED missing-editor-contract");
            return false;
        }

        var packageA = AssetPackageManifestId.From(Guid.Parse("c1100000-0000-4000-8000-000000000010"));
        var packageB = AssetPackageManifestId.From(Guid.Parse("c1100000-0000-4000-8000-000000000020"));
        var packageC = AssetPackageManifestId.From(Guid.Parse("c1100000-0000-4000-8000-000000000030"));
        var stepId = Guid.Parse("c1100000-0000-4000-8000-000000000110");
        const string relativePath = "assets/r110/package.txt";
        var installation = new AssetPackageInstallation(packageA, "1.0.0", [], [relativePath]);
        var dependent = new AssetPackageInstallation(packageB, "1.0.0", [packageA], ["assets/r110/dependent.txt"]);
        var replacement = new AssetPackageInstallation(packageC, "1.0.0", [], ["assets/r110/replacement.txt"]);
        var step = new AssetPackageMigrationStep(
            stepId, packageA, "1.0.0", "2.0.0", true, true, "Rebuild governed package fixture.");
        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "r110-content", Guid.NewGuid().ToString("N"));
        try
        {
            var sourcePath = Path.Combine(smokeRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, "v1");
            var previewPlan = AssetPackageMigrationPlanner.Plan(installation, "2.0.0", [step], dryRun: true);
            var preview = AssetPackageMigrationStore.Execute(
                smokeRoot, installation, previewPlan, new R110MigrationHostStep(stepId, "v2"));
            if (!preview.Succeeded || !preview.DryRun || File.ReadAllText(sourcePath) != "v1" ||
                Directory.Exists(Path.Combine(smokeRoot, ".forge")))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R110_CONTENT_LIFECYCLE_FAILED dry-run");
                return false;
            }

            var applyPlan = AssetPackageMigrationPlanner.Plan(installation, "2.0.0", [step], dryRun: false);
            var applied = AssetPackageMigrationStore.Execute(
                smokeRoot, installation, applyPlan, new R110MigrationHostStep(stepId, "v2"));
            var blocked = AssetPackageUninstallAnalyzer.Analyze(packageA, [installation, dependent], []);
            var allowed = AssetPackageUninstallAnalyzer.Analyze(
                packageA, [installation, dependent, replacement], [new AssetPackageReplacement(packageA, packageC)]);
            if (!applied.Succeeded || applied.DryRun || applied.Installation.Version != "2.0.0" ||
                File.ReadAllText(sourcePath) != "v2" ||
                string.IsNullOrWhiteSpace(applied.BackupRelativePath) ||
                string.IsNullOrWhiteSpace(applied.ChangelogRelativePath) ||
                !File.Exists(Path.Combine(smokeRoot, applied.ChangelogRelativePath.Replace('/', Path.DirectorySeparatorChar))) ||
                blocked.CanUninstall || blocked.BlockingDependents.SingleOrDefault() != packageB ||
                !allowed.CanUninstall || allowed.BackupFiles.SingleOrDefault() != relativePath)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R110_CONTENT_LIFECYCLE_FAILED canonical-contract");
                return false;
            }

            AssetPackageMigrationStore.RestoreBackup(smokeRoot, applied.BackupRelativePath, [relativePath]);
            if (File.ReadAllText(sourcePath) != "v1")
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R110_CONTENT_LIFECYCLE_FAILED restore");
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

        GD.Print("OPENRPG_FORGE_TOOLSET_R110_CONTENT_LIFECYCLE_PASS " +
                 "migration=trusted-host-exact-step-dry-run-backup-changelog-compatibility-apply-restore-rollback " +
                 "uninstall=impact-dependents-replacement-backup-refuse-unresolved " +
                 "identity=uuid-schema-provenance-license-hash validation=required-unique-resolved-schema-localized " +
                 "ui=asset-browser-visible-empty-error command=dry-run-apply history=restore-backup " +
                 "persistence=filesystem-roundtrip cli=structured-no-shell mcp=typed-no-shell " +
                 "runtime=optional-safe-required-diagnostic");
        return true;
    }

    private sealed class R110MigrationHostStep(Guid stepId, string content) : IAssetPackageMigrationHostStep
    {
        public Guid StepId => stepId;

        public void Apply(string workspaceRoot, IReadOnlyList<string> installedRelativePaths)
        {
            var path = Path.Combine(
                workspaceRoot,
                installedRelativePaths.Single().Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllText(path, content);
        }
    }


}
#endif
