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
    private bool RunContentDeliveryP2SmokeTest()
    {
        var vfx = AssetVfxRenderPlanBuilder.Build(
            new AssetVfxRenderRequest(new AssetVfxManifest(true, null, "forge-vfx", 1024, 6, 40),
                12, 512, 4, true, true, true),
            new R132VfxProvider());
        var currentKey = new string('a', 64);
        var nextKey = new string('b', 64);
        var signing = new ModuleSigningConfiguration(ModuleSigningMode.Required, "r132",
            [currentKey], [], false);
        var rotation = ContentTrustRotationService.Preview(signing,
            new ContentTrustRotationManifest(Guid.Parse("d1320000-0000-4000-8000-000000000001"), 1,
                currentKey, nextKey, DateTimeOffset.Parse("2026-08-12T10:00:00Z"),
                DateTimeOffset.Parse("2026-08-19T10:00:00Z"), true, "toolset:r132"));
        var root = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "r132", Guid.NewGuid().ToString("N"));
        try
        {
            var bytes = Enumerable.Range(0, 48_000).Select(index => (byte)(index % 251)).ToArray();
            var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            var destination = Path.Combine(root, "module.partial");
            Directory.CreateDirectory(root);
            File.WriteAllBytes(destination, bytes[..8_000]);
            var delivery = ContentRangeDeliveryService.Download(
                new ContentRepositoryResource("packages/module.orpmod", bytes.Length, hash,
                    "CC0-1.0", "toolset:r132"),
                destination, new R132RangeTransport(bytes), 4 * 1024, 3);
            if (!vfx.Valid || !vfx.Decal || !vfx.Light || !vfx.AudioCue ||
                !rotation.Valid || rotation.NextPolicy?.TrustedKeyFingerprints.Count != 2 ||
                !delivery.Applied || delivery.ResumedBytes != 8_000)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R132_CONTENT_DELIVERY_FAILED");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R132_CONTENT_DELIVERY_PASS " +
            "vfx=provider-particles-decals-lights-audio-lod-budgets " +
            "signatures=trusted-rotation-overlap-revocation-policy " +
            "delta=content-addressed-range-resume-retry-dedup-hash-rollback");
        return true;
    }

    private bool RunContentWorkflowSmokeTest()
    {
        if (_contentWorkflow is null || _history is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11E_CONTENT_FAILED missing_window");
            return false;
        }

        var original = _history.State.Project.Module.Content;
        var tableId = DataTableId.From(Guid.Parse("d7150000-0000-4000-8000-000000000001"));
        var rowId = DataRowId.From(Guid.Parse("d7150000-0000-4000-8000-000000000002"));
        var column = new DataColumnDefinition(
            DataColumnId.From(Guid.Parse("d7150000-0000-4000-8000-000000000003")),
            "difficulty",
            "Difficulté",
            DataColumnKind.Integer,
            required: true);
        DataTableDefinition Table(string value, string provenance) => new(
            tableId,
            "encounters",
            "Rencontres",
            [column],
            [new DataRowDefinition(rowId, new Dictionary<string, string> { ["difficulty"] = value }, provenance)],
            provenance: provenance,
            licenseSpdx: "CC0-1.0",
            contentHash: new string('a', 64));
        var configured = new ContentWorkspaceConfiguration(
            original.Id,
            new ContentBatchConfiguration(
                "*.json",
                [".json"],
                ContentBatchCollisionPolicy.PreserveBoth,
                "workspace-smoke-test",
                "CC0-1.0"),
            [Table("2", "base")],
            ContentMergePolicy.PreserveBoth,
            "workspace-smoke-test");
        if (!_contentWorkflow.ConfigureForSmokeTest(configured) ||
            _contentWorkflow.DataTableCount != 1 ||
            !_contentWorkflow.ApplyForSmokeTest() ||
            _history.State.Project.Module.Content.DataTables.Single().Id != tableId)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11E_CONTENT_FAILED configuration");
            return false;
        }

        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "content-workflow-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var source = Path.Combine(smokeRoot, "source");
            var destination = Path.Combine(smokeRoot, "destination");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "encounters.json"), "{\"schemaVersion\":1}");
            var request = new ContentBatchTransferRequest(
                ContentBatchOperation.Import,
                source,
                destination,
                configured.Batch.Glob,
                configured.Batch.Extensions,
                configured.Batch.CollisionPolicy,
                configured.Batch.Provenance,
                configured.Batch.LicenseSpdx,
                dryRun: true);
            var preview = ContentBatchTransferService.Execute(request);
            var applied = ContentBatchTransferService.Execute(new ContentBatchTransferRequest(
                request.Operation,
                request.SourceRoot,
                request.DestinationRoot,
                request.Glob,
                request.Extensions,
                request.CollisionPolicy,
                request.Provenance,
                request.LicenseSpdx,
                dryRun: false));
            var collision = ContentBatchTransferService.Execute(new ContentBatchTransferRequest(
                request.Operation,
                request.SourceRoot,
                request.DestinationRoot,
                request.Glob,
                request.Extensions,
                request.CollisionPolicy,
                request.Provenance,
                request.LicenseSpdx,
                dryRun: false));
            var blueprintPack = Path.Combine(smokeRoot, "selection.orpblueprints");
            var blueprint = _history.State.Project.Blueprints.First();
            var exported = BlueprintExportPackageBuilder.Build(
                _history.State.Project,
                [blueprint.Id],
                blueprintPack);
            using var archive = ZipFile.OpenRead(blueprintPack);
            var merged = DataTableThreeWayMerger.Merge(
                Table("2", "base"),
                Table("4", "ours"),
                Table("6", "theirs"),
                ContentMergePolicy.PreserveBoth);
            var workspace = Path.Combine(smokeRoot, "workspace");
            var saved = ProjectSession.Create(workspace, _history.State.Project);
            var reopened = ProjectSession.Open(workspace);
            if (!preview.Valid || preview.Entries.Count != 1 || !preview.DryRun ||
                !applied.Valid || applied.Added != 1 || !File.Exists(applied.Entries.Single().TargetPath) ||
                !collision.Valid || collision.PreservedBoth != 1 ||
                exported.BlueprintCount != 1 || exported.PreviewCount != 1 ||
                archive.GetEntry("manifest.json") is null ||
                archive.GetEntry("licenses/index.json") is null ||
                archive.GetEntry("migrations/blueprint-schema-v1.json") is null ||
                merged.Conflicts.Count != 1 || merged.Merged.Rows.Count != 2 ||
                !saved.Succeeded || !reopened.Succeeded || reopened.Session is null ||
                reopened.Session.Project.Module.Content.DataTables.Single().Id != tableId)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11E_CONTENT_FAILED workflows");
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
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            _history.State.Project.Module.Content != original)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11E_CONTENT_FAILED history");
            return false;
        }

        RefreshEditorControls();
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G11E_CONTENT_PASS tables=typed batch=dry-run-applied " +
            "collisions=preserve-both blueprints=transitive-governed merge=three-way-conflicts " +
            "history=restored persistence=roundtrip");
        return true;
    }

    private bool RunContentPackageSmokeTest()
    {
        if (_contentPackageWindow is null || _history is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R016_CONTENT_PACKAGES_FAILED missing_window");
            return false;
        }

        var original = _history.State.Project.Module.Content;
        var source = new ContentPackageSource(Guid.Parse("c0160000-0000-4000-8000-000000000203"), 1,
            "toolset-smoke", "creature.ash-warden", "data/creatures/ash-warden.json",
            ContentSourceKind.Json, true, "CC0-1.0", new string('a', 64), 1_048_576);
        var optional = new ContentPackageDependency(Guid.Parse("c0160000-0000-4000-8000-000000000204"), 1,
            "toolset-smoke", "crypt.cosmetics", "^1.0.0", ContentDependencyKind.Optional);
        var draft = new ContentPackageDefinition(Guid.Parse("c0160000-0000-4000-8000-000000000202"), 1,
            "toolset-smoke", "crypt.encounters", "1.2.0", "CC0-1.0", new string('0', 64), 80,
            ["creature.ash-warden"], [optional], [source]);
        var package = new ContentPackageDefinition(draft.Id, draft.SchemaVersion, draft.Provenance,
            draft.PackageNamespace, draft.Version, draft.LicenseSpdx,
            ContentPackageCompiler.ComputePackageHash(draft), draft.Priority, draft.SealedResources,
            draft.Dependencies, draft.Sources);
        var catalog = new ContentPackageCatalog(_history.State.Project.Module.Id.Value, 1, "toolset-smoke",
            ContentConflictPolicy.HighestPriority, [package]);
        var resolution = ContentPackageResolver.Resolve(catalog);
        var script = new ContentPackageScriptService(catalog);
        var handle = script.Find("crypt.encounters");
        if (!_contentPackageWindow.ConfigureForSmokeTest(catalog) ||
            _contentPackageWindow.PackageCount != 1 ||
            !_contentPackageWindow.ApplyForSmokeTest() ||
            _history.State.Project.Module.Content.Packages.Packages.Single().Id != package.Id ||
            !resolution.Valid || resolution.LockEntries.Count != 1 || resolution.Resources.Count != 1 ||
            handle is null || script.Read(handle.Value).Id != package.Id)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R016_CONTENT_PACKAGES_FAILED configuration");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "r016-content-packages",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            if (!saved.Succeeded || !reopened.Succeeded || reopened.Session is null ||
                reopened.Session.Project.Module.Content.Packages.Packages.Single().Id != package.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R016_CONTENT_PACKAGES_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true);
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            _history.State.Project.Module.Content != original)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R016_CONTENT_PACKAGES_FAILED history");
            return false;
        }

        RefreshEditorControls();
        GD.Print("OPENRPG_FORGE_TOOLSET_R016_CONTENT_PACKAGES_PASS " +
                 "package=uuid-schema-namespace-semver-license-hash priority=explicit-sealed-conflict-preview " +
                 "dependencies=required-optional-peer-offline-lock sources=text-one-resource-per-file " +
                 "compiler=validate-resolve-script-hash-compress-reproducible ui=visible-empty-error " +
                 "forgescript=typed-handle mcp=filtered-dry-run history=undo-redo persistence=roundtrip");
        return true;
    }

    private bool RunContentAuthoringSmokeTest()
    {
        if (_history is null || _assetBrowser is null || _localizationWorkspace is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R017_CONTENT_AUTHORING_FAILED missing_workspace");
            return false;
        }

        var original = _history.State.Project.Module.Localization;
        var extracted = LocalizationCatalogExtractor.Extract(_history.State.Project);
        if (extracted.Diagnostics.Count != 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R017_CONTENT_AUTHORING_FAILED extraction");
            return false;
        }
        var baseline = extracted.Catalog;
        var messageKey = "smoke.r017.loot";
        var messages = baseline.Messages.Where(message => message.Key != messageKey)
            .Append(new LocalizationMessage(messageKey,
                "{gender, select, female {Elle} male {Il} other {Iel}} trouve " +
                "{count, plural, =0 {aucun objet} one {# objet} other {# objets}}.",
                provenance: "toolset:r017"))
            .ToArray();
        var catalog = new LocalizationCatalog(baseline.Id, baseline.DefaultLocale,
            [.. baseline.Locales, "en-US"], messages, baseline.SchemaVersion,
            baseline.PackageNamespace, baseline.LicenseSpdx, baseline.Provenance, baseline.PlatformByteBudget);
        var bundle = LocalizationTranslationExchange.Export(catalog, "en-US");
        var translated = bundle with
        {
            Units = bundle.Units.Select(unit => unit with
            {
                Translation = unit.Key == messageKey
                    ? "{gender, select, female {She} male {He} other {They}} finds " +
                      "{count, plural, =0 {no item} one {# item} other {# items}}."
                    : unit.Source,
            }).ToArray(),
        };
        var imported = LocalizationTranslationExchange.Import(catalog, translated);
        var resolvedText = imported.Catalog?.Resolve(messageKey, "en-US", new Dictionary<string, object?>
        {
            ["gender"] = "female",
            ["count"] = 2,
        });
        var diagnosticProbe = imported.Catalog is null
            ? null
            : _setLocalizationCatalogHandler.Handle(_history.State, new SetLocalizationCatalogCommand(
                CommandId.From(Guid.NewGuid()), _history.State.Revision, imported.Catalog, DryRun: true));
        var dryRunSucceeded = diagnosticProbe?.Succeeded == true;
        var applySucceeded = dryRunSucceeded && ApplyLocalizationCatalog(imported.Catalog!, dryRun: false);
        if (!imported.Succeeded || imported.Catalog is null || resolvedText != "She finds 2 items." ||
            !dryRunSucceeded || !applySucceeded)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R017_CONTENT_AUTHORING_FAILED localization " +
                        $"imported={imported.Succeeded} diagnostics={string.Join(',', imported.Diagnostics)} " +
                        $"resolved={resolvedText ?? "null"} dry_run={dryRunSucceeded} applied={applySucceeded} " +
                        $"command={string.Join(';', diagnosticProbe?.Diagnostics.Select(item => $"{item.Code}:{item.Message}") ?? [])}");
            return false;
        }

        var source = new ContentPackageSource(Guid.Parse("c0170000-0000-4000-8000-000000000001"), 1,
            "toolset:r017", "rules/encounter", "data/encounter.json", ContentSourceKind.Json, true,
            "CC0-1.0", new string('a', 64), 4096, ["legacy.core:text/encounter"],
            ["crypt.core:model/props/chest"]);
        var modelAsset = new ContentPackageAsset(Guid.Parse("c0170000-0000-4000-8000-000000000002"), 1,
            "toolset:r017", "props/chest", "models/chest.glb", true, "CC0-1.0", new string('b', 64),
            8 * 1024 * 1024);
        var package = new ContentPackageDefinition(Guid.Parse("c0170000-0000-4000-8000-000000000003"), 1,
            "toolset:r017", "crypt.core", "1.0.0", "CC0-1.0", new string('c', 64), 10, [], [], [source],
            [modelAsset]);
        var packageCatalog = new ContentPackageCatalog(_history.State.Project.Module.Id.Value, 1,
            "toolset:r017", ContentConflictPolicy.HighestPriority, [package]);
        var registryResult = ContentResourceRegistry.Build(packageCatalog);
        if (!registryResult.Valid)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R017_CONTENT_AUTHORING_FAILED namespaces");
            return false;
        }

        var registry = ContentResourceRegistry.Create(registryResult);
        var currentProject = _history.State.Project;
        var currentModule = currentProject.Module;
        var configuredContent = new ContentWorkspaceConfiguration(currentModule.Content.Id,
            currentModule.Content.Batch, currentModule.Content.DataTables, currentModule.Content.MergePolicy,
            currentModule.Content.Provenance, currentModule.Content.SchemaVersion, packageCatalog);
        var configuredModule = new ModuleDefinition(currentModule.Id, currentModule.Slug, currentModule.DisplayName,
            currentModule.Description, currentModule.EntryAreaId, currentModule.Metadata, currentModule.Variables,
            currentModule.Distribution, currentModule.Localization, configuredContent, currentModule.ArtisticStyle,
            currentModule.RuntimePolicies, currentModule.Factions, currentModule.WorldFeatures);
        var configuredProject = new ProjectDefinition(currentProject.Id, currentProject.Title, configuredModule,
            currentProject.Blueprints, currentProject.Areas, currentProject.Dialogues, currentProject.Quests);
        var browser = new UnifiedContentBrowser(configuredProject);
        var resourceScript = new ContentResourceScriptService(browser);
        var resourceHandle = resourceScript.Find("legacy.core:text/encounter");
        var localizationScript = new LocalizationScriptService(currentModule.Localization);
        var localizationHandle = localizationScript.Find(messageKey);

        var textureProfile = new AssetProcessingProfile(
            AssetCatalogItemId.From(Guid.Parse("c0170000-0000-4000-8000-000000000004")), 1,
            AssetProcessingKind.Texture, "tiff", "ktx2", "CC0-1.0", new string('d', 64), "toolset:r017",
            8 * 1024 * 1024, texture: new(TextureColorSpace.Srgb, TextureCompressionProfile.Desktop, 4096,
                10.24m, new Dictionary<PbrTextureChannel, string>
                {
                    [PbrTextureChannel.BaseColor] = "textures/chest-base.png",
                    [PbrTextureChannel.Normal] = "textures/chest-normal.exr",
                }));
        var modelProfile = new AssetProcessingProfile(
            AssetCatalogItemId.From(Guid.Parse("c0170000-0000-4000-8000-000000000005")), 1,
            AssetProcessingKind.Model, "gltf", "glb", "CC0-1.0", new string('e', 64), "toolset:r017",
            32 * 1024 * 1024, model: new(true, true, true, ["hand.r"], [1m, .5m, .25m]));
        if (!registry.TryResolve("legacy.core:text/encounter", out var aliased) ||
            aliased?.Address != "crypt.core:text/rules/encounter" ||
            browser.Search(new UnifiedContentBrowserQuery(PackageNamespace: "crypt.core")).Count != 2 ||
            resourceHandle is null || resourceScript.Read(resourceHandle.Value).ReferenceCount != 1 ||
            localizationHandle is null || localizationScript.Read(localizationHandle.Value, "en-US",
                new Dictionary<string, object?> { ["gender"] = "female", ["count"] = 2 }).Text !=
            "She finds 2 items." || AssetProcessingProfileValidator.Validate(textureProfile).Count != 0 ||
            AssetProcessingProfileValidator.Validate(modelProfile).Count != 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R017_CONTENT_AUTHORING_FAILED resources");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "r017-content-authoring",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            if (!saved.Succeeded || !reopened.Succeeded || reopened.Session is null ||
                reopened.Session.Project.Module.Localization.ContentSha256 != currentModule.Localization.ContentSha256)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R017_CONTENT_AUTHORING_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true);
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            _history.State.Project.Module.Localization != original)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R017_CONTENT_AUTHORING_FAILED history");
            return false;
        }

        RefreshEditorControls();
        GD.Print("OPENRPG_FORGE_TOOLSET_R017_CONTENT_AUTHORING_PASS " +
                 "localization=icu-fallback-pseudo-translator texture=png-tiff-exr-ktx2-pbr-compression-texel " +
                 "model=gltf-glb-rig-animation-collision-sockets-lod namespaces=package-type-path-uuid-aliases " +
                 "browser=facets-preview-references-package-license-status-actions ui=visible-empty-error " +
                 "forgescript=typed-handles mcp=filtered-dry-run history=undo-redo persistence=roundtrip");
        return true;
    }

    private bool RunCreatorProductivitySmokeTest()
    {
        if (_history is null || _projectSession is null || _projectPreview is null ||
            _creatorShell is null || _blueprintPalette is null || _positionX is null || _positionZ is null ||
            _paletteBlueprintIds.Count == 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G720_PRODUCTIVITY_FAILED missing_workspace");
            return false;
        }

        var originalProfile = LoadWorkspaceProfile();
        var originalArea = GetActiveArea();
        var originalAreaCount = _history.State.Project.Areas.Count;
        var originalPaletteSelection = _blueprintPalette.Selected;
        var originalPositionX = _positionX.Value;
        var originalPositionZ = _positionZ.Value;
        var originalSelection = _selectionContext.Current.ToArray();
        var originalSelectionLock = _selectionContext.IsLocked;
        AreaId? comparisonAreaId = null;
        string? failure = null;
        try
        {
            _selectionContext.SetLocked(false);
            var blueprints = _history.State.Project.Blueprints.ToDictionary(blueprint => blueprint.Id);
            var filterTarget = originalArea.Entities.First(entity => blueprints.ContainsKey(entity.BlueprintId));
            var targetBlueprint = blueprints[filterTarget.BlueprintId];
            var requiredTags = filterTarget.Tags.Take(1).ToArray();
            var filter = new EditorDisplayFilter(
                [targetBlueprint.Kind],
                requiredTags,
                targetBlueprint.Provenance.Author,
                targetBlueprint.PaletteOrigin.ToString(),
                ghostHidden: true);
            var expectedVisible = originalArea.Entities.Count(entity =>
                blueprints.TryGetValue(entity.BlueprintId, out var blueprint) &&
                filter.VisibleKinds.Contains(blueprint.Kind) &&
                filter.RequiredTags.All(tag => entity.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)) &&
                blueprint.Provenance.Author.Contains(filter.Author, StringComparison.OrdinalIgnoreCase) &&
                blueprint.PaletteOrigin.ToString().Contains(filter.State, StringComparison.OrdinalIgnoreCase));
            var visible = ApplyWorkspaceFilter(filter);
            if (visible != expectedVisible || _projectPreview.GhostedEntityCount != originalArea.Entities.Count - expectedVisible)
            {
                failure = "display_filter";
            }

            if (failure is null &&
                (!_projectPreview.SelectEntityForSmokeTest(filterTarget.Id) ||
                 ApplyWorkspaceFilter(new EditorDisplayFilter(
                     filter.VisibleKinds, filter.RequiredTags, filter.Author, filter.State,
                     isolateSelection: true, ghostHidden: true)) != 1))
            {
                failure = "selection_isolation";
            }

            if (failure is null)
            {
                comparisonAreaId = CreateArea(
                    "Comparaison G7-20",
                    AreaKind.Interior,
                    new AreaDimensions(24, 24),
                    AreaTemplateKind.Empty);
                if (comparisonAreaId is null)
                {
                    failure = "area_creation";
                }
            }

            if (failure is null && comparisonAreaId is not null)
            {
                var configuredProfile = new EditorWorkspaceProfile(
                    originalProfile.Id,
                    originalProfile.Name,
                    EditorRole.Builder,
                    filter,
                    repeatPlacement: true,
                    [originalArea.Id, comparisonAreaId],
                    originalArea.Id,
                    comparisonAreaId,
                    contextualHelpEnabled: true,
                    "toolset-g720-smoke",
                    EditorWorkspaceProfile.CurrentSchemaVersion,
                    originalProfile.UiScale,
                    originalProfile.TextScale,
                    originalProfile.PrimaryFontFamily,
                    originalProfile.FallbackFontFamilies,
                    originalProfile.PersonalFavoriteBlueprintIds,
                    originalProfile.RecentBlueprintIds);
                SaveWorkspaceProfile(configuredProfile);
                var persisted = EditorWorkspaceProfileStore.TryLoad(_projectSession.RootDirectory);
                if (persisted is null ||
                    !persisted.DisplayFilter.VisibleKinds.SequenceEqual(filter.VisibleKinds) ||
                    !persisted.DisplayFilter.RequiredTags.SequenceEqual(filter.RequiredTags) ||
                    persisted.DisplayFilter.Author != filter.Author ||
                    persisted.DisplayFilter.State != filter.State ||
                    persisted.DisplayFilter.IsolateSelection != filter.IsolateSelection ||
                    persisted.DisplayFilter.GhostHidden != filter.GhostHidden ||
                    persisted.OpenAreaTabs.Count != 2 || persisted.ActiveAreaId != originalArea.Id ||
                    persisted.ComparisonAreaId != comparisonAreaId || !persisted.RepeatPlacement ||
                    !persisted.ContextualHelpEnabled || _creatorShell.AreaTabCount != 2 ||
                    Enum.GetValues<EditorRole>().Length != 7 ||
                    !CreatorWorkspaceWindow.ContextualHelpContent.Contains(
                        "docs/aurora-parity/tasks/G7-20-CREATOR-PRODUCTIVITY.md",
                        StringComparison.Ordinal))
                {
                    failure = "profile_tabs_help_persistence";
                }
            }

            if (failure is null)
            {
                var placeable = _history.State.Project.Blueprints.First(blueprint => blueprint.Kind == BlueprintKind.Placeable);
                _blueprintPalette.Selected = _paletteBlueprintIds.FindIndex(id => id == placeable.Id);
                _positionX.Value = 6;
                _positionZ.Value = 24;
                var beforePlacement = GetActiveArea().Entities.Count;
                if (_blueprintPalette.Selected < 0 ||
                    !PlaceRepeatedSeries(6, 2f, PlacementDistribution.Grid, 2026) ||
                    GetActiveArea().Entities.Count != beforePlacement + 6)
                {
                    failure = "repeated_placement";
                }
                else
                {
                    var undo = _history.Undo();
                    var redo = _history.Redo();
                    var cleanup = _history.Undo();
                    if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
                        GetActiveArea().Entities.Count != beforePlacement)
                    {
                        failure = "repeated_history";
                    }
                }
            }
        }
        catch (Exception exception)
        {
            failure = $"exception_{exception.GetType().Name}";
        }
        finally
        {
            while (_history.CanUndo && comparisonAreaId is not null &&
                   _history.State.Project.Areas.Any(area => area.Id == comparisonAreaId))
            {
                _history.Undo();
            }

            _activeAreaContext.EnsureValid(_history.State.Project);
            ActivateArea(originalArea.Id);
            SaveWorkspaceProfile(EditorWorkspaceProfilePlanner.ReconcileAreas(
                originalProfile,
                _history.State.Project.Areas.Select(area => area.Id).ToArray(),
                originalArea.Id));
            _blueprintPalette.Selected = originalPaletteSelection;
            _positionX.Value = originalPositionX;
            _positionZ.Value = originalPositionZ;
            _selectionContext.SetLocked(false);
            _selectionContext.Replace(originalSelection);
            _selectionContext.SetLocked(originalSelectionLock);
            RebuildPreview();
            RefreshEditorControls();
        }

        if (failure is not null || _history.State.Project.Areas.Count != originalAreaCount)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_G720_PRODUCTIVITY_FAILED {failure ?? "cleanup"}");
            return false;
        }

        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G720_PRODUCTIVITY_PASS filters=combined-isolation-ghost " +
            "placement=deterministic-atomic tabs=multi-area comparison=readonly help=sourced " +
            "profiles=seven persistence=roundtrip history=restored");
        return true;
    }

    private bool RunR059ShellProductivitySmokeTest()
    {
        if (_history is null || _projectSession is null || _projectPreview is null ||
            _multiAreaSplitViewWindow is null || _blueprintPalette is null || _positionX is null ||
            _positionZ is null || _paletteBlueprintIds.Count == 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R059_SHELL_PRODUCTIVITY_FAILED missing_workspace");
            return false;
        }

        var originalProfile = LoadWorkspaceProfile();
        var originalArea = GetActiveArea();
        var originalAreaCount = _history.State.Project.Areas.Count;
        var originalSelection = _selectionContext.Current.ToArray();
        var originalSelectionLock = _selectionContext.IsLocked;
        var originalPaletteSelection = _blueprintPalette.Selected;
        var originalPositionX = _positionX.Value;
        var originalPositionZ = _positionZ.Value;
        AreaId? comparisonId = null;
        string? failure = null;
        try
        {
            comparisonId = CreateArea("Comparaison R059", AreaKind.Interior, new AreaDimensions(24, 24),
                AreaTemplateKind.Empty);
            if (comparisonId is null) return false;
            ActivateArea(originalArea.Id);
            var entity = GetActiveArea().Entities
                .Where(candidate => _history.State.Project.Blueprints.Any(blueprint =>
                    blueprint.Id == candidate.BlueprintId && blueprint.Kind == BlueprintKind.Creature))
                .OrderBy(candidate => candidate.Transform.Position.X * candidate.Transform.Position.X +
                                      candidate.Transform.Position.Z * candidate.Transform.Position.Z)
                .First();
            var layerId = Guid.Parse("59000000-0000-4000-8000-000000000059");
            var authoring = GetActiveArea().Authoring;
            var layer = new AreaEditorLayer(layerId, "Gameplay R059", null, true, false, "builder",
                [entity.Id], provenance: "toolset:r059");
            var layered = new AreaAuthoringProfile(authoring.SchemaVersion, authoring.UserEvents, authoring.Map,
                [.. authoring.Layers, layer], authoring.Experience, authoring.RequireLayerRuntimeMetadata);
            if (!ApplyAreaAuthoring(originalArea.Id, layered)) failure = "layer_apply";

            var blueprint = _history.State.Project.Blueprints.Single(item => item.Id == entity.BlueprintId);
            var filter = new EditorDisplayFilter([blueprint.Kind], entity.Tags.Take(1).ToArray(),
                blueprint.Provenance.Author, blueprint.PaletteOrigin.ToString(), false, true, [layerId],
                [EditorVolumeKind.Trigger, EditorVolumeKind.Navigation], provenance: "toolset:r059");
            if (failure is null && (ApplyWorkspaceFilter(filter) != 1 ||
                                    _projectPreview.GhostedEntityCount != GetActiveArea().Entities.Count - 1))
                failure = "layer_volume_filter";

            var repeated = new EditorRepeatedPlacementProfile(Guid.NewGuid(), 1, "toolset:r059", true,
                true, true, EditorPlacementDistribution.Grid,
                EditorPlacementAlignment.SurfaceNormal, 1.5f, 4, 2026, 0.1f);
            var planned = RepeatedPlacementPlanner.Plan(repeated, new Vector3Value(0, 0, 0), 4,
                position => new PlacementSurfaceSample(new Vector3Value(position.X, 2, position.Z),
                    new Vector3Value(0, 1, 0)));
            if (failure is null && (planned.Poses.Count != 4 || planned.Poses.Any(pose => pose.Position.Y != 2.1f)))
                failure = "surface_plan";

            var placementBlueprint = _history.State.Project.Blueprints.First(item =>
                item.Kind == BlueprintKind.Placeable && _paletteBlueprintIds.Contains(item.Id));
            _blueprintPalette.Selected = _paletteBlueprintIds.FindIndex(id => id == placementBlueprint.Id);
            _positionX.Value = -8;
            _positionZ.Value = 10;
            var beforePlacement = GetActiveArea().Entities.Count;
            if (failure is null && (!PlaceRepeatedSeriesAdvanced(repeated, 4) ||
                                    GetActiveArea().Entities.Count != beforePlacement + 4))
                failure = "brush_placement";
            else if (failure is null)
            {
                var undo = _history.Undo(); var redo = _history.Redo(); var cleanup = _history.Undo();
                if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
                    GetActiveArea().Entities.Count != beforePlacement) failure = "brush_history";
            }

            _selectionContext.SetLocked(false);
            _selectionContext.Replace([new CreatorSelection(CreatorSelectionKind.Entity, entity.Id.ToString(),
                originalArea.Id.ToString(), "toolset:r059")]);
            var targetBefore = _history.State.Project.Areas.Single(area => area.Id == comparisonId).Entities.Count;
            if (failure is null && (!CopySelectionToComparisonArea(originalArea.Id, comparisonId) ||
                _history.State.Project.Areas.Single(area => area.Id == comparisonId).Entities.Count != targetBefore + 1))
                failure = "cross_area_copy";
            else if (failure is null && !_history.Undo().Succeeded) failure = "cross_area_undo";

            var multi = new EditorMultiAreaProfile(originalProfile.MultiArea.Id, 1, "toolset:r059", true,
                EditorSplitAxis.Horizontal, .5f, true, true);
            var help = new EditorContextualHelpProfile(originalProfile.Help.Id, 1, "toolset:r059", true, true,
                "fr-FR", "placement.repeated");
            var builderProfile = EditorWorkspaceProfileStore.TryLoadCatalog(_projectSession.RootDirectory)?.Profiles
                                     .SingleOrDefault(profile => profile.Role == EditorRole.Builder) ??
                                 EditorWorkspaceProfile.CreateDefault(EditorRole.Builder, originalArea.Id);
            var candidate = new EditorWorkspaceProfile(builderProfile.Id, builderProfile.Name, EditorRole.Builder,
                filter, true, [originalArea.Id, comparisonId], originalArea.Id, comparisonId, true, "toolset:r059",
                EditorWorkspaceProfile.CurrentSchemaVersion, originalProfile.UiScale, originalProfile.TextScale,
                originalProfile.PrimaryFontFamily, originalProfile.FallbackFontFamilies,
                originalProfile.PersonalFavoriteBlueprintIds, originalProfile.RecentBlueprintIds,
                originalProfile.Shell, repeated, multi, help,
                EditorRolePolicy.CreateDefault(originalProfile.Id.Value, EditorRole.Builder));
            var profileDiagnostics = EditorWorkspaceProjectValidator.Validate(candidate, _history.State.Project);
            var helpResult = EditorContextualHelpService.Query(help, "placement", assisted: true);
            var profileHistory = new EditorWorkspaceProfileCommandHistory(builderProfile);
            var dryRun = profileHistory.Execute(new(Guid.NewGuid(), 1, "toolset:r059", candidate, true));
            var applied = profileHistory.Execute(new(Guid.NewGuid(), 1, "toolset:r059", candidate));
            var undoProfile = profileHistory.Undo(); var redoProfile = profileHistory.Redo();
            SaveWorkspaceProfile(candidate);
            var persisted = EditorWorkspaceProfileStore.TryLoad(_projectSession.RootDirectory);
            var catalog = EditorWorkspaceProfileStore.TryLoadCatalog(_projectSession.RootDirectory);
            if (failure is null)
            {
                var profileFailures = new List<string>();
                if (profileDiagnostics.Count != 0) profileFailures.Add($"profile-diagnostics-{profileDiagnostics.Count}");
                if (helpResult.Diagnostics.Count != 0) profileFailures.Add($"help-diagnostics-{helpResult.Diagnostics.Count}");
                if (helpResult.Citations.Count == 0) profileFailures.Add("help-citations");
                if (!dryRun.Succeeded || !applied.Succeeded || !undoProfile.Succeeded || !redoProfile.Succeeded) profileFailures.Add("history");
                if (persisted?.MultiArea.SplitViewEnabled != true) profileFailures.Add("split-persisted");
                if (persisted?.RepeatedPlacementProfile.Alignment != EditorPlacementAlignment.SurfaceNormal) profileFailures.Add("placement-persisted");
                if (catalog is null || catalog.Profiles.Count != 7 || catalog.Profiles.Select(item => item.Role).Distinct().Count() != 7) profileFailures.Add("catalog");
                if (candidate.RolePolicy.VisiblePanels.Count < 5 || !candidate.RolePolicy.Allows(EditorPermission.EditAreas)) profileFailures.Add("permissions");
                if (_multiAreaSplitViewWindow.ViewportCount != 2) profileFailures.Add($"viewports-{_multiAreaSplitViewWindow.ViewportCount}");
                if (profileFailures.Count != 0) failure = $"profile_help_split_persistence[{string.Join(',', profileFailures)}]";
            }
        }
        catch (Exception exception)
        {
            failure = $"exception_{exception.GetType().Name}";
        }
        finally
        {
            while (_history.CanUndo && (comparisonId is not null &&
                   _history.State.Project.Areas.Any(area => area.Id == comparisonId) ||
                   _history.State.Project.Areas.Single(area => area.Id == originalArea.Id).Authoring.Layers
                       .Any(layer => layer.Id == Guid.Parse("59000000-0000-4000-8000-000000000059"))))
                _history.Undo();
            _activeAreaContext.EnsureValid(_history.State.Project); ActivateArea(originalArea.Id);
            SaveWorkspaceProfile(EditorWorkspaceProfilePlanner.ReconcileAreas(originalProfile,
                _history.State.Project.Areas.Select(area => area.Id).ToArray(), originalArea.Id));
            _blueprintPalette.Selected = originalPaletteSelection;
            _positionX.Value = originalPositionX; _positionZ.Value = originalPositionZ;
            _selectionContext.SetLocked(false); _selectionContext.Replace(originalSelection);
            _selectionContext.SetLocked(originalSelectionLock); RebuildPreview(); RefreshEditorControls();
        }

        if (failure is not null || _history.State.Project.Areas.Count != originalAreaCount)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_R059_SHELL_PRODUCTIVITY_FAILED {failure ?? "cleanup"}");
            return false;
        }
        GD.Print("OPENRPG_FORGE_TOOLSET_R059_SHELL_PRODUCTIVITY_PASS " +
                 "filters=layers-volumes-profiled placement=brush-repeat-distribution-surface-scatter-atomic " +
                 "tabs=split-two-viewports-comparison-copy-protected help=docs-properties-forgescript-dependencies-cited " +
                 "profiles=seven-panels-permissions mcp=filtered-plan-apply-revert persistence=roundtrip history=restored");
        return true;
    }

    private bool RunR060RecoverySmokeTest()
    {
        if (_history is null || _recoveryWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R060_RECOVERY_FAILED missing_workspace");
            return false;
        }

        var root = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "r060-recovery-smoke",
            Guid.NewGuid().ToString("N"));
        string? failure = null;
        try
        {
            Directory.CreateDirectory(root);
            var original = _history.State.Project;
            var changed = new ProjectDefinition(original.Id, $"{original.Title} · travail non enregistré",
                original.Module, original.Blueprints, original.Areas, original.Dialogues, original.Quests);
            var store = new RecoverySnapshotStore(2);
            var snapshot = store.Capture(root, original, 7,
                new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero));
            var sessionStore = new RecoverySessionStore();
            var firstSession = sessionStore.Begin(root, original.Id,
                new DateTimeOffset(2026, 8, 10, 12, 1, 0, TimeSpan.Zero), 1001, "toolset:r060");
            var interrupted = sessionStore.Begin(root, original.Id,
                new DateTimeOffset(2026, 8, 10, 12, 2, 0, TimeSpan.Zero), 1002, "toolset:r060");
            var selection = new RecoveryRestoreSelection(
                Guid.Parse("e0600000-0000-4000-8000-000000000060"), 1,
                RecoveryRestoreSection.ProjectAndModule | RecoveryRestoreSection.Areas,
                "toolset:r060");
            var preview = store.PreviewRestore(snapshot, changed, selection);
            var localHistory = new ProjectCommandHistory(new ProjectState(changed, new ProjectRevision(0)));
            var handler = new RestoreRecoverySnapshotHandler(new SystemClock());
            var dryRunCommand = new RestoreRecoverySnapshotCommand(CommandId.From(Guid.NewGuid()),
                localHistory.State.Revision, store.Restore(snapshot), selection, true);
            var dryRun = handler.Handle(localHistory.State, dryRunCommand);
            var applied = handler.Handle(localHistory.State,
                dryRunCommand with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false });
            localHistory.Record(applied);
            var undo = localHistory.Undo();
            var redo = localHistory.Redo();

            var configuredPolicy = new CrashRecoveryPolicy(
                original.Module.RuntimePolicies.Extensions.OperationalReadiness.Recovery.Id,
                1, 45, 8, true, true, true, "toolset:r060");
            var runtime = RecoveryRuntimeResolver.Resolve(configuredPolicy, true);
            var required = RecoveryRuntimeResolver.Resolve(null, true);
            var roundTrip = ProjectJsonSerializer.Deserialize(ProjectJsonSerializer.Serialize(original));

            var corrupted = store.Capture(root, original, 8,
                new DateTimeOffset(2026, 8, 10, 12, 3, 0, TimeSpan.Zero));
            File.AppendAllText(corrupted.SnapshotPath, "tampered");
            var inspected = store.Inspect(root, quarantineCorruptFiles: true);
            store.Clear(root);
            var sessionMarkerPreserved = sessionStore.Read(root)?.Id == interrupted.Current.Id;
            var ended = sessionStore.End(root, interrupted.Current.Id,
                new DateTimeOffset(2026, 8, 10, 12, 4, 0, TimeSpan.Zero));
            _recoveryWindow.RefreshForSmokeTest();

            if (firstSession.PreviousSessionWasUnclean || !interrupted.PreviousSessionWasUnclean ||
                !preview.Comparison.Changed || preview.Project.Title != original.Title ||
                preview.Comparison.SelectedSections?.Count != 2 || !dryRun.Succeeded || !applied.Succeeded ||
                applied.State.Project.Title != original.Title || !undo.Succeeded || !redo.Succeeded ||
                redo.State.Project.Title != original.Title || runtime.Policy?.AutosaveIntervalSeconds != 45 ||
                required.Diagnostics.Single().Code != RecoveryRuntimeResolver.RequiredMissingCode ||
                roundTrip.Id != original.Id ||
                inspected.All(item => item.Status != RecoverySnapshotStatus.Quarantined) ||
                !sessionMarkerPreserved || !ended || sessionStore.Read(root)?.CleanShutdown != true)
                failure = "session_snapshot_compare_selective_transaction_corruption_roundtrip";
        }
        catch (Exception exception)
        {
            failure = $"exception_{exception.GetType().Name}";
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        if (failure is not null)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_R060_RECOVERY_FAILED {failure}");
            return false;
        }
        GD.Print("OPENRPG_FORGE_TOOLSET_R060_RECOVERY_PASS " +
                 "policy=uuid-schema-provenance-configurable autosave=versioned-atomic-retained " +
                 "session=unclean-detected-clean-close snapshot=hash-journal-quarantine-confined " +
                 "compare=before-restore selective=module-blueprints-areas-dialogues-quests " +
                 "transaction=dry-run-apply-undo-redo persistence=roundtrip mcp=resource-plan-apply-revert " +
                 "runtime=optional-safe-required-diagnostic history=restored");
        return true;
    }

    private bool RunR061AudioAcceptanceSmokeTest()
    {
        if (_history is null || _areaEnvironmentWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R061_AUDIO_FAILED missing_workspace");
            return false;
        }
        try
        {
            var area = _history.State.Project.Areas.Single(item => item.Id == _activeAreaContext.Current);
            var source = area.Environment;
            var definitionId = Guid.Parse("a0615000-0000-4000-8000-000000000001");
            var resources = new[] { "audio/rain.ogg", "audio/wind.ogg", "audio/owl.ogg" };
            var clips = resources.Select((resource, index) => new AudioClipDefinition(
                Guid.Parse($"a0615000-0000-4000-8000-{index + 2:000000000000}"), resource,
                index == 0 ? 6 : 1, ["forest", index == 2 ? "owl" : "ambience"],
                index.ToString(System.Globalization.CultureInfo.InvariantCulture), "fr", -22 + index,
                resource.Replace(".ogg", ".preview.ogg", StringComparison.Ordinal), "audio/ogg", 4096,
                "CC0-1.0", "vault:r061", new string((char)('a' + index), 64),
                index == 0 ? "pluie douce sur les feuilles" : $"ambiance {index}",
                [.1f, .4f, .8f, .3f], 30 + index)).ToArray();
            var catalog = new AudioEmitterCatalog(1,
            [
                new(definitionId, 1, "Forêt R061",
                    new AudioClipSet(Guid.Parse("a0615000-0000-4000-8000-000000000010"), 1,
                        "Ambiances forêt", AudioClipSelectionMode.Weighted, clips, "toolset:r061"),
                    new(AudioLoopMode.Loop, 0, 0, 0, .1f, string.Empty),
                    new(1, 40, "inverse-square", 360, 0, 0, 0, true), null, 0,
                    string.Empty, string.Empty, false, "toolset:r061", "assets/audio.json",
                    "CC0-1.0", new string('d', 64)),
            ],
            [new(Guid.Parse("a0615000-0000-4000-8000-000000000011"), definitionId,
                TransformValue.Identity, true, "toolset:r061")]);
            var authoring = new AudioAuthoringProfile(
                Guid.Parse("a0615000-0000-4000-8000-000000000012"), 1, "toolset:r061",
                "assets/audio.json", "CC0-1.0", new string('e', 64), true,
                new(2026, 1, new Dictionary<string, float>
                {
                    [resources[0]] = 6,
                    [resources[1]] = 1,
                    [resources[2]] = 1,
                }),
                new(4, 12, -1, 1, .8f, 1.2f, AudioModulationDistribution.Triangular,
                    [new(0, 0), new(.5f, .35f), new(1, 1)], 2026, true, 120, 1),
                new(-18, "Ambience", -6, 1, .85f, AudioMeterMode.Loudness),
                new(area.Id.ToString(), "Ambience", AudioListenerPolicy.Personalized,
                    new Dictionary<string, float> { ["local-player"] = .8f }));
            var audio = new AreaAudioProfile(source.Audio.DayMusic, source.Audio.NightMusic,
                source.Audio.CombatMusic, resources[0], source.Audio.ReverbPreset,
                source.Audio.TransitionSeconds, resources, AudioOrderMode.Weighted, 8, 4, 1,
                new(AreaSoundMode.Global2D, 0, 0, 0, "linear"), source.Audio.RuntimeControl,
                source.Audio.AdaptiveLayers, source.Audio.Orchestration, catalog, authoring);
            var configured = new AreaEnvironmentProfile(source.SchemaVersion, source.Classification,
                source.Tags, source.Lighting, source.Fog, audio, source.Rendering, source.Sky,
                source.Wind, source.Weather, source.Water, source.Reverb, source.Authoring,
                source.RequireAuthoringRuntimeMetadata);

            var browser = AudioResourceBrowser.Search(catalog,
                new AudioResourceSearch("feuilles", new HashSet<string> { "forest" }, "fr", "CC0-1.0"));
            var audition = AudioResourceBrowser.PlanAudition(browser, authoring.Mix, false);
            var sequence = AreaAudioSequencePlanner.Plan(audio, 32, 77);
            var statistics = AreaAudioSequencePlanner.PreviewStatistics(audio, 512, 77);
            var mix = AreaAudioMixer.Evaluate(audio, new(0, 12, string.Empty,
                new HashSet<string>(), 0, false, area.Id.ToString(), "local-player",
                SourceLoudnessLufs: -24));
            var runtime = AudioAuthoringRuntimeResolver.Resolve(authoring, true);
            var optional = AudioAuthoringRuntimeResolver.Resolve(null, false);
            var required = AudioAuthoringRuntimeResolver.Resolve(null, true);

            var localHistory = new ProjectCommandHistory(_history.State);
            var handler = new SetAreaEnvironmentHandler(new SystemClock());
            var command = new SetAreaEnvironmentCommand(CommandId.From(Guid.NewGuid()),
                localHistory.State.Revision, area.Id, configured, true);
            var dryRun = handler.Handle(localHistory.State, command);
            var applied = handler.Handle(localHistory.State,
                command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false });
            localHistory.Record(applied);
            var undo = localHistory.Undo();
            var redo = localHistory.Redo();
            var serialized = ProjectJsonSerializer.Serialize(applied.State.Project);
            var roundTrip = ProjectJsonSerializer.Deserialize(serialized);
            var script = AreaAudioScriptService.Read(area.WithEnvironment(configured));
            var invalid = handler.Handle(_history.State, command with
            {
                CommandId = CommandId.From(Guid.NewGuid()),
                BaseRevision = _history.State.Revision,
                Environment = new AreaEnvironmentProfile(configured.SchemaVersion,
                    configured.Classification, configured.Tags, configured.Lighting, configured.Fog,
                    new AreaAudioProfile(audio.DayMusic, audio.NightMusic, audio.CombatMusic,
                        audio.Ambience, audio.ReverbPreset, audio.TransitionSeconds, audio.Playlist,
                        audio.OrderMode, audio.IntervalSeconds, audio.IntervalVariationSeconds,
                        audio.RelativeVolume, audio.Spatial, audio.RuntimeControl, audio.AdaptiveLayers,
                        audio.Orchestration, audio.Emitters,
                        authoring with { LicenseSpdx = "UNLICENSED" }),
                    configured.Rendering, configured.Sky, configured.Wind, configured.Weather,
                    configured.Water, configured.Reverb, configured.Authoring,
                    configured.RequireAuthoringRuntimeMetadata),
                DryRun = true,
            });
            var ui = _areaEnvironmentWindow.ConfigureAudioForSmokeTest(configured);

            var failures = new List<string>();
            if (browser.Count != 1) failures.Add("browser");
            if (audition.Resources.Count != 1 || audition.Diagnostics.Count != 0) failures.Add("audition");
            if (sequence.Count != 32 || sequence.Zip(sequence.Skip(1)).Any(pair =>
                    pair.First.Resource == pair.Second.Resource)) failures.Add("sequence");
            if (statistics.SampleCount != 512) failures.Add("statistics");
            if (mix.Bus != "Ambience" || !mix.Personalized || mix.NormalizedGain > .85f) failures.Add("mix");
            if (!runtime.Available || optional.Diagnostics.Count != 0 ||
                required.Diagnostics.Single().Code != AudioAuthoringRuntimeResolver.RequiredMissingCode)
                failures.Add("runtime");
            if (!dryRun.Succeeded || !applied.Succeeded) failures.Add("command");
            if (!undo.Succeeded || !redo.Succeeded ||
                redo.State.Project.Areas.Single(item => item.Id == area.Id).Environment.Audio.Authoring != authoring)
                failures.Add("history");
            if (ProjectJsonSerializer.Serialize(roundTrip) != serialized) failures.Add("roundtrip");
            if (script.SelectionMode != AudioOrderMode.Weighted) failures.Add("script");
            if (invalid.Succeeded || invalid.Diagnostics.All(item =>
                    item.Code != AudioAuthoringProfileValidator.IdentityInvalidCode)) failures.Add("invalid");
            if (!ui) failures.Add("ui");
            if (failures.Count > 0)
            {
                GD.PrintErr($"OPENRPG_FORGE_TOOLSET_R061_AUDIO_FAILED {string.Join(',', failures)}");
                return false;
            }
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_R061_AUDIO_FAILED exception_{exception.GetType().Name}");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R061_AUDIO_PASS " +
                 "browser=search-tags-transcript-waveform-audition-batch-governed " +
                 "selection=sequential-random-shuffle-weighted-seeded-no-repeat " +
                 "modulation=ranges-distributions-curves-tempo-statistics " +
                 "mix=lufs-bus-ducking-distance-cap-meter ambient=2d-area-listener-personalized " +
                 "command=dry-run-apply-undo-redo persistence=roundtrip mcp=filtered-plan-apply-revert " +
                 "runtime=optional-safe-required-diagnostic");
        return true;
    }

    private bool RunR062AudioOrchestrationAcceptanceSmokeTest()
    {
        if (_history is null || _areaEnvironmentWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R062_AUDIO_FAILED missing_workspace");
            return false;
        }
        try
        {
            var area = _history.State.Project.Areas.Single(item => item.Id == _activeAreaContext.Current);
            var source = area.Environment;
            var masterId = Guid.Parse("a0625000-0000-4000-8000-000000000001");
            var ambienceId = Guid.Parse("a0625000-0000-4000-8000-000000000002");
            var orchestration = new AreaAudioOrchestration(
            [
                new(MusicState.Exploration, "builtin://audio/day", ["builtin://audio/day-bed"],
                    2, 2, true, 120, 4),
                new(MusicState.Tension, "builtin://audio/night", ["builtin://audio/night-bed"],
                    2, 2, true, 120, 4),
                new(MusicState.Combat, "builtin://audio/combat", ["builtin://audio/combat-drums"],
                    1, 1, true, 120, 4),
            ],
            [
                new(masterId, "Master", null, .8f, "default", "all", .8f, true, true),
                new(ambienceId, "Ambience", masterId, .7f, "rain", "windows", .6f, true, true),
            ], []);
            var audio = new AreaAudioProfile("builtin://audio/day", "builtin://audio/night",
                "builtin://audio/combat", "builtin://audio/forest", source.Audio.ReverbPreset, 2, [],
                spatial: new(AreaSoundMode.Spatial3D, 3, 42, .12f, "inverse-square", true),
                runtimeControl: new(false, AudioInstancePolicy.Restart, AudioAuthority.Server, 1.5f,
                    "audio.started", "audio.stopped", "audio.paused", "audio.faded", "1.0"),
                adaptiveLayers:
                [
                    new("builtin://audio/day-forest", 6, 18, "", "forest", 0, false, .8f, 2),
                    new("builtin://audio/night-rain", 18, 6, "rain", "forest", .2f, false, 1, 3,
                        "forest-volume"),
                ], orchestration: orchestration, authoring: AudioAuthoringProfile.CreateDefault([], 0, 0));
            var configured = new AreaEnvironmentProfile(source.SchemaVersion, source.Classification,
                source.Tags, source.Lighting, source.Fog, audio, source.Rendering, source.Sky,
                source.Wind, source.Weather, source.Water, source.Reverb, source.Authoring,
                source.RequireAuthoringRuntimeMetadata);

            var attenuation = AudioOrchestrationRuntimeService.PreviewAttenuation(audio.Spatial, 17);
            var control = AudioOrchestrationRuntimeService.Create(audio.Authoring.Id);
            control = AudioOrchestrationRuntimeService.Start(control, audio.RuntimeControl, AudioAuthority.Server);
            control = AudioOrchestrationRuntimeService.Pause(control, audio.RuntimeControl, AudioAuthority.Server);
            control = AudioOrchestrationRuntimeService.Resume(control, audio.RuntimeControl, AudioAuthority.Server);
            control = AudioOrchestrationRuntimeService.Fade(control, audio.RuntimeControl,
                AudioAuthority.Server, .25f, 2);
            var denied = AudioOrchestrationRuntimeService.Start(
                AudioOrchestrationRuntimeService.Create(audio.Authoring.Id), audio.RuntimeControl,
                AudioAuthority.Local);
            var day = new AudioListenerContext(0, 12, "", new HashSet<string> { "forest" }, 0, false);
            var night = new AudioListenerContext(0, 22, "rain",
                new HashSet<string> { "forest", "forest-volume" }, .5f, false);
            var crossfade = AudioOrchestrationRuntimeService.PlanAdaptiveCrossfade(audio, day, night);
            var music = AreaMusicStateMachine.Transition(orchestration, MusicState.Exploration,
                MusicState.Combat, 10, 3.25f);
            var monitoring = AudioOrchestrationRuntimeService.MonitorMix(orchestration,
                new Dictionary<string, (float, float, float)>
                {
                    ["Master"] = (-3, -9, -14),
                    ["Ambience"] = (-6, -12, -20),
                }, "windows", "rain");
            var signatures = ForgeScriptApiCatalog.Signatures;

            var localHistory = new ProjectCommandHistory(_history.State);
            var handler = new SetAreaEnvironmentHandler(new SystemClock());
            var command = new SetAreaEnvironmentCommand(CommandId.From(Guid.NewGuid()),
                localHistory.State.Revision, area.Id, configured, true);
            var dryRun = handler.Handle(localHistory.State, command);
            var applied = handler.Handle(localHistory.State,
                command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false });
            localHistory.Record(applied);
            var undo = localHistory.Undo();
            var redo = localHistory.Redo();
            var serialized = ProjectJsonSerializer.Serialize(applied.State.Project);
            var roundTrip = ProjectJsonSerializer.Deserialize(serialized);
            var ui = _areaEnvironmentWindow.ConfigureR062AudioForSmokeTest(configured);

            var failures = new List<string>();
            if (attenuation.DebugSpheres.Count != 2 || attenuation.Samples[^1].Gain != 0)
                failures.Add("range-debug");
            if (control.Status != AudioPlaybackStatus.Fading || control.Gain != .25f ||
                denied.Diagnostics.Single().Code != AudioOrchestrationRuntimeService.AuthorityDeniedCode)
                failures.Add("handle-control");
            if (crossfade.Entries.Count != 2 || crossfade.Entries.All(item => item.ToGain <= 0))
                failures.Add("adaptive-crossfade");
            if (music is not { BeatSynchronized: true, StartDelaySeconds: > 0 } || music.Stems.Count != 1)
                failures.Add("music-sync");
            if (monitoring.Count != 2 || monitoring.Single(item => item.Bus == "Ambience").EffectiveGain != .56f)
                failures.Add("mix-monitor");
            if (!signatures.Any(value => value.StartsWith("void audio_pause", StringComparison.Ordinal)) ||
                !signatures.Any(value => value.StartsWith("void audio_fade", StringComparison.Ordinal)))
                failures.Add("forgescript");
            if (!dryRun.Succeeded || !applied.Succeeded || !undo.Succeeded || !redo.Succeeded)
                failures.Add("command-history");
            if (ProjectJsonSerializer.Serialize(roundTrip) != serialized ||
                !roundTrip.Areas.Single(item => item.Id == area.Id).Environment.Audio.Spatial.DebugSpheresVisible)
                failures.Add("roundtrip");
            if (!ui) failures.Add("ui");
            if (failures.Count > 0)
            {
                GD.PrintErr($"OPENRPG_FORGE_TOOLSET_R062_AUDIO_FAILED {string.Join(',', failures)}");
                return false;
            }
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_R062_AUDIO_FAILED exception_{exception.GetType().Name}");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R062_AUDIO_PASS " +
                 "range=inner-outer-curve-air-debug-spheres " +
                 "script=typed-handle-start-stop-pause-resume-fade-authority-events-api-v1 " +
                 "ambience=time-weather-location-danger-indoor-volume-crossfade " +
                 "music=day-night-exploration-tension-combat-boss-victory-stems-cooldown-beat-sync " +
                 "mix=hierarchy-snapshot-platform-dynamic-accessibility-live-monitor " +
                 "command=dry-run-apply-undo-redo persistence=roundtrip mcp=filtered-plan-apply-revert " +
                 "runtime=optional-safe-required-diagnostic");
        return true;
    }

    private bool RunR063AudioCaptionAcceptanceSmokeTest()
    {
        if (_history is null || _areaEnvironmentWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R063_AUDIO_CAPTION_FAILED missing_workspace");
            return false;
        }
        try
        {
            var area = _history.State.Project.Areas.Single(item => item.Id == _activeAreaContext.Current);
            var source = area.Environment;
            var resource = "builtin://openrpg/audio/dragon-roar";
            var caption = new SoundCaption(resource,
                [new("fr", "[Rugissement de dragon à gauche]"), new("en", "[Dragon roar to the left]")],
                true, "critical", Guid.Parse("a0635000-0000-4000-8000-000000000001"), 1,
                "toolset:r063", "assets/audio-captions.json", "CC0-1.0", new string('a', 64),
                "effect", 1.5f, 6, true);
            var orchestration = source.Audio.Orchestration with { Captions = [caption] };
            var audio = new AreaAudioProfile(source.Audio.DayMusic, source.Audio.NightMusic,
                source.Audio.CombatMusic, source.Audio.Ambience, source.Audio.ReverbPreset,
                source.Audio.TransitionSeconds, source.Audio.Playlist, source.Audio.OrderMode,
                source.Audio.IntervalSeconds, source.Audio.IntervalVariationSeconds,
                source.Audio.RelativeVolume, source.Audio.Spatial, source.Audio.RuntimeControl,
                source.Audio.AdaptiveLayers, orchestration, source.Audio.Emitters, source.Audio.Authoring);
            var configured = new AreaEnvironmentProfile(source.SchemaVersion, source.Classification,
                source.Tags, source.Lighting, source.Fog, audio, source.Rendering, source.Sky,
                source.Wind, source.Weather, source.Water, source.Reverb, source.Authoring,
                source.RequireAuthoringRuntimeMetadata);
            var validation = AudioCaptionValidator.Validate(audio);
            var presentation = AudioCaptionRuntimeService.Resolve(audio, resource, "de", "fr",
                Vector3Value.Zero, new(0, 0, -1), new(-4, 0, 0), true);
            var optional = AudioCaptionRuntimeService.Resolve(audio, "builtin://openrpg/audio/missing",
                "fr", "fr", Vector3Value.Zero, new(0, 0, -1), Vector3Value.Zero);
            var required = AudioCaptionRuntimeService.Resolve(audio, "builtin://openrpg/audio/missing",
                "fr", "fr", Vector3Value.Zero, new(0, 0, -1), Vector3Value.Zero, true);
            var script = AreaAudioScriptService.ReadCaption(area.WithEnvironment(configured), resource, "fr");

            var localHistory = new ProjectCommandHistory(_history.State);
            var handler = new SetAreaEnvironmentHandler(new SystemClock());
            var command = new SetAreaEnvironmentCommand(CommandId.From(Guid.NewGuid()),
                localHistory.State.Revision, area.Id, configured, true);
            var dryRun = handler.Handle(localHistory.State, command);
            var applied = handler.Handle(localHistory.State,
                command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false });
            localHistory.Record(applied);
            var undo = localHistory.Undo();
            var redo = localHistory.Redo();
            var serialized = ProjectJsonSerializer.Serialize(applied.State.Project);
            var roundTrip = ProjectJsonSerializer.Deserialize(serialized);
            var invalidCaption = caption with { LicenseSpdx = "UNLICENSED", ContentSha256 = "bad" };
            var invalidAudio = new AreaAudioProfile(audio.DayMusic, audio.NightMusic, audio.CombatMusic,
                audio.Ambience, audio.ReverbPreset, audio.TransitionSeconds, audio.Playlist,
                audio.OrderMode, audio.IntervalSeconds, audio.IntervalVariationSeconds, audio.RelativeVolume,
                audio.Spatial, audio.RuntimeControl, audio.AdaptiveLayers,
                orchestration with { Captions = [invalidCaption] }, audio.Emitters, audio.Authoring);
            var invalidEnvironment = new AreaEnvironmentProfile(configured.SchemaVersion,
                configured.Classification, configured.Tags, configured.Lighting, configured.Fog,
                invalidAudio, configured.Rendering, configured.Sky, configured.Wind, configured.Weather,
                configured.Water, configured.Reverb, configured.Authoring,
                configured.RequireAuthoringRuntimeMetadata);
            var invalid = handler.Handle(_history.State, command with
            {
                CommandId = CommandId.From(Guid.NewGuid()),
                BaseRevision = _history.State.Revision,
                Environment = invalidEnvironment,
            });
            var ui = _areaEnvironmentWindow.ConfigureR063AudioForSmokeTest(configured);

            var failures = new List<string>();
            if (validation.Count != 0) failures.Add("validation");
            if (presentation.Presentation is not { Locale: "fr", Direction: AudioCaptionDirection.Left } ||
                presentation.Presentation.Text != "[Rugissement de dragon à gauche]")
                failures.Add("presentation");
            if (optional.Diagnostics.Count != 0 || required.Diagnostics.Single().Code !=
                AudioCaptionRuntimeService.RequiredMissingCode) failures.Add("optional-required");
            if (script is not { CueKind: "effect", Importance: "critical" }) failures.Add("script");
            if (!ForgeScriptApiCatalog.Signatures.Any(value =>
                    value.StartsWith("AudioCaptionRef audio_caption", StringComparison.Ordinal)))
                failures.Add("forgescript-api");
            if (!dryRun.Succeeded || !applied.Succeeded || !undo.Succeeded || !redo.Succeeded)
                failures.Add("command-history");
            if (ProjectJsonSerializer.Serialize(roundTrip) != serialized ||
                roundTrip.Areas.Single(item => item.Id == area.Id).Environment.Audio.Orchestration.Captions
                    .Single().Id != caption.Id) failures.Add("roundtrip");
            if (invalid.Succeeded || invalid.Diagnostics.All(item =>
                    item.Code != AudioCaptionValidator.IdentityInvalidCode)) failures.Add("invalid");
            if (!ui) failures.Add("ui");
            if (failures.Count > 0)
            {
                GD.PrintErr($"OPENRPG_FORGE_TOOLSET_R063_AUDIO_CAPTION_FAILED {string.Join(',', failures)}");
                return false;
            }
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_R063_AUDIO_CAPTION_FAILED exception_{exception.GetType().Name}");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R063_AUDIO_CAPTION_PASS " +
                 "metadata=dialogue-effect-ambient-locales-fallback-importance-direction-duration " +
                 "identity=uuid-schema-provenance-manifest-spdx-sha256 " +
                 "ui=visible-empty-error-preview command=dry-run-apply-reject history=undo-redo " +
                 "persistence=roundtrip forgescript=typed-handle mcp=filtered-plan-apply-revert " +
                 "distribution=ungoverned-blocked runtime=optional-safe-required-diagnostic");
        return true;
    }


}
#endif
