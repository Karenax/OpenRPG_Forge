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
    private bool RunScalableEditorExperienceSmokeTest()
    {
        if (_history is null || _projectSession is null || _mainPanel is null || _creatorShell is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11F_EXPERIENCE_FAILED missing_workspace");
            return false;
        }

        var originalProfile = LoadWorkspaceProfile();
        var originalArea = GetActiveArea();
        var originalAreaCount = _history.State.Project.Areas.Count;
        var originalStyle = _history.State.Project.Module.ArtisticStyle;
        var blueprintId = _history.State.Project.Blueprints.First().Id;
        AreaId? createdAreaId = null;
        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "editor-experience-smoke",
            Guid.NewGuid().ToString("N"));
        string? failure = null;
        try
        {
            createdAreaId = CreateArea(
                "Atelier comparatif G11F",
                AreaKind.Interior,
                new AreaDimensions(32, 32),
                AreaTemplateKind.Empty);
            if (createdAreaId is null)
            {
                failure = "area_creation";
            }
            else
            {
                var configuredProfile = new EditorWorkspaceProfile(
                    originalProfile.Id,
                    originalProfile.Name,
                    originalProfile.Role,
                    originalProfile.DisplayFilter,
                    originalProfile.RepeatPlacement,
                    [originalArea.Id, createdAreaId],
                    createdAreaId,
                    originalArea.Id,
                    originalProfile.ContextualHelpEnabled,
                    "toolset-g11f-smoke",
                    EditorWorkspaceProfile.CurrentSchemaVersion,
                    uiScale: 1.25,
                    textScale: 1.35,
                    primaryFontFamily: "Atkinson Hyperlegible",
                    fallbackFontFamilies: ["Noto Sans", "Segoe UI"],
                    personalFavoriteBlueprintIds: [blueprintId],
                    recentBlueprintIds: [blueprintId]);
                SaveWorkspaceProfile(configuredProfile);
                var persistedProfile = EditorWorkspaceProfileStore.TryLoad(_projectSession.RootDirectory);
                var fontNames = (_mainPanel.Theme?.DefaultFont as SystemFont)?.FontNames ?? [];
                var palette = BlueprintPaletteCatalog.Build(_history.State.Project, configuredProfile);
                if (persistedProfile is null || persistedProfile.SchemaVersion != EditorWorkspaceProfile.CurrentSchemaVersion ||
                    Math.Abs(persistedProfile.UiScale - 1.25) > 0.001 ||
                    Math.Abs(persistedProfile.TextScale - 1.35) > 0.001 ||
                    persistedProfile.ActiveAreaId != createdAreaId ||
                    persistedProfile.ComparisonAreaId != originalArea.Id ||
                    _creatorShell.AreaTabCount != 2 ||
                    Math.Abs((_mainPanel.Theme?.DefaultBaseScale ?? 0) - 1.25f) > 0.001f ||
                    _mainPanel.Theme?.DefaultFontSize != 19 ||
                    fontNames.Length < 3 || fontNames[0] != "Atkinson Hyperlegible" ||
                    palette.Filter(BlueprintPaletteView.Favorites).All(item => item.BlueprintId != blueprintId) ||
                    palette.Filter(BlueprintPaletteView.Recent).FirstOrDefault()?.BlueprintId != blueprintId)
                {
                    failure = "profile_ui_palette";
                }

                var style = new ArtisticStyleProfile(
                    originalStyle.Id,
                    metersPerUnit: 1,
                    targetTexelDensity: 512,
                    scaleTolerancePercent: 20,
                    texelTolerancePercent: 50,
                    shaderFamily: "pbr-metallic-roughness",
                    era: "original-fantasy",
                    ArtisticRealism.StylizedRealism,
                    ["earth", "ember", "stone"],
                    [new ArtisticAssetStyle(
                        blueprintId,
                        metersPerUnit: 1,
                        texelDensity: 512,
                        shaderFamily: "pbr-metallic-roughness",
                        era: "original-fantasy",
                        ArtisticRealism.StylizedRealism,
                        ["earth", "stone"],
                        "toolset-g11f-smoke")],
                    "toolset-g11f-smoke");
                if (failure is null &&
                    (!ApplyArtisticStyleProfile(style) ||
                     ArtisticCompatibilityValidator.Analyze(_history.State.Project).Count != 0))
                {
                    failure = "artistic_profile";
                }

                if (failure is null)
                {
                    var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
                    var reopened = ProjectSession.Open(smokeRoot);
                    if (!saved.Succeeded || !reopened.Succeeded || reopened.Session is null ||
                        reopened.Session.Project.Module.ArtisticStyle.Assets.Single().BlueprintId != blueprintId ||
                        !File.ReadAllText(Path.Combine(smokeRoot, ProjectSession.DefaultProjectFileName))
                            .Contains($"\"formatVersion\": \"{ProjectSession.CurrentProjectFormatVersion}\"", StringComparison.Ordinal))
                    {
                        failure = "persistence";
                    }
                }

                if (failure is null)
                {
                    var undoArt = _history.Undo();
                    var redoArt = _history.Redo();
                    var cleanupArt = _history.Undo();
                    if (!undoArt.Succeeded || !redoArt.Succeeded || !cleanupArt.Succeeded ||
                        !ReferenceEquals(_history.State.Project.Module.ArtisticStyle, originalStyle))
                    {
                        failure = "artistic_history";
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
            if (Directory.Exists(smokeRoot))
            {
                Directory.Delete(smokeRoot, recursive: true);
            }

            while (_history.CanUndo &&
                   ((createdAreaId is not null && _history.State.Project.Areas.Any(area => area.Id == createdAreaId)) ||
                    !ReferenceEquals(_history.State.Project.Module.ArtisticStyle, originalStyle)))
            {
                _history.Undo();
            }

            _activeAreaContext.EnsureValid(_history.State.Project);
            ActivateArea(originalArea.Id);
            SaveWorkspaceProfile(EditorWorkspaceProfilePlanner.ReconcileAreas(
                originalProfile,
                _history.State.Project.Areas.Select(area => area.Id).ToArray(),
                originalArea.Id));
            RebuildPreview();
            RefreshEditorControls();
        }

        if (failure is not null || _history.State.Project.Areas.Count != originalAreaCount ||
            !ReferenceEquals(_history.State.Project.Module.ArtisticStyle, originalStyle))
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_G11F_EXPERIENCE_FAILED {failure ?? "cleanup"}");
            return false;
        }

        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G11F_EXPERIENCE_PASS ui=scalable font=truetype-fallback " +
            "tabs=multi-area comparison=readonly favorites=project-personal recents=ordered " +
            "art=profiled-warnings history=restored persistence=roundtrip");
        return true;
    }

    private bool RunPaletteProductionP2SmokeTest()
    {
        if (_history is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R138_PALETTE_PRODUCTION_FAILED missing_workspace");
            return false;
        }

        try
        {
            var blueprintId = _history.State.Project.Blueprints.First().Id;
            var profile = EditorWorkspaceProfile.CreateDefault(EditorRole.Artist);
            var collection = new NamedPaletteCollection(
                Guid.Parse("a3800000-0000-4000-8000-000000000001"), 1, "Production crypte",
                [blueprintId], taskScoped: true, "toolset://r138/palette");
            profile = EditorWorkspaceProfilePlanner.UpsertNamedPalette(profile, collection);
            var item = BlueprintPaletteCatalog.Build(_history.State.Project, profile)
                .FilterNamedCollection(profile.NamedPaletteCollections.Single()).Single();
            var json = Encoding.UTF8.GetBytes(
                "{\"asset\":{\"version\":\"2.0\"},\"accessors\":[{\"min\":[-1,0,-0.5],\"max\":[1,3,0.5]}]," +
                "\"meshes\":[{\"primitives\":[{\"attributes\":{\"POSITION\":0}}]}]," +
                "\"materials\":[{\"pbrMetallicRoughness\":{}}]}");
            var metrics = ArtisticAssetMetricExtractor.ExtractDocument(
                blueprintId, json, [(1024, 1536)], "toolset://r138/metrics");
            if (item.BlueprintId != blueprintId || !collection.TaskScoped ||
                metrics.DimensionsMeters.Height != 3 || metrics.EstimatedTexelDensity != 512 ||
                metrics.ShaderFamily != "pbr-metallic-roughness" ||
                EditorWorkspaceProfilePlanner.RemoveNamedPalette(profile, collection.Id).NamedPaletteCollections.Count != 0)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R138_PALETTE_PRODUCTION_FAILED contract");
                return false;
            }
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_R138_PALETTE_PRODUCTION_FAILED exception_{exception.GetType().Name}");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R138_PALETTE_PRODUCTION_PASS " +
                 "collections=named-task-project-personal-ordered-bounded metrics=automatic-glb-bounds-png-texel-shader " +
                 "style=scale-density-shader-era-realism-palette-validation persistence=roundtrip");
        return true;
    }

    private bool RunReferenceRenderingSmokeTest()
    {
        if (_history is null || _areaEnvironmentWindow is null || _projectPreview is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11G_RENDER_FAILED missing_workspace");
            return false;
        }

        var area = GetActiveArea();
        var original = area.Environment;
        var rendering = new AreaRenderingProfile(
            AreaRenderingProfile.CurrentSchemaVersion,
            PbrMaterialWorkflow.MetallicRoughness,
            ToneMappingProfile.Aces,
            RenderQualityTier.Ultra,
            forwardPlus: true,
            perPixelLighting: true,
            materialInstances: true,
            hdrOutput: true,
            exposure: 1.15,
            whitePoint: 8,
            lutResource: "qa/rendering/r114-neutral-lut.tres",
            [
                new("desktop", 48, 12, 8192, 0.8),
                new("mobile", 8, 2, 2048, 0.75),
            ]);
        var sky = new SkyProfile(
            SkyProfile.CurrentSchemaVersion,
            SkyProfileMode.Procedural,
            string.Empty,
            "#25385f",
            "#d88f68",
            "#211a29",
            starIntensity: 0.35,
            cloudCoverage: 0.25,
            moonIntensity: 0.2,
            energyMultiplier: 1.1,
            transitionSeconds: 3);
        var configured = new AreaEnvironmentProfile(
            AreaEnvironmentProfile.CurrentSchemaVersion,
            original.Classification,
            original.Tags,
            new(DayNightMode.AlwaysNight, 45, .3f, 1.4f, 4200, true, true),
            original.Fog,
            original.Audio,
            rendering,
            sky);
        if (!_areaEnvironmentWindow.ConfigureRenderForSmokeTest(configured) ||
            !_areaEnvironmentWindow.ApplyForSmokeTest() ||
            _projectPreview.RenderProfileSummary != "MetallicRoughness:Aces:Procedural" ||
            _projectPreview.RenderDiagnosticCount != 0 ||
            !_projectPreview.ColorCorrectionEnabled || !_projectPreview.HdrOutputEnabled ||
            _history.State.Project.Areas.Single(candidate => candidate.Id == area.Id).Environment.Rendering.ToneMapping != ToneMappingProfile.Aces ||
            _history.State.Project.Areas.Single(candidate => candidate.Id == area.Id).Environment.Rendering.LightBudgets
                .Single(item => item.Platform == "desktop").MaximumDynamicLights != 48)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11G_RENDER_FAILED apply_preview");
            return false;
        }

        var applied = _history.State.Project.Areas.Single(candidate => candidate.Id == area.Id).Environment;
        var plan = AreaRenderPlanBuilder.Build(applied, "desktop", observedDynamicLights: 39, observedShadowCastingLights: 10);
        if (plan.LightBudget.MaximumDynamicLights != 48 ||
            !plan.Diagnostics.Any(item => item.Code == AreaRenderPlanBuilder.DynamicLightWarningCode) ||
            !plan.Diagnostics.Any(item => item.Code == AreaRenderPlanBuilder.ShadowLightWarningCode))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11G_RENDER_FAILED budgets");
            return false;
        }

        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "reference-rendering-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restoredEnvironment = reopened.Session?.Project.Areas.Single(candidate => candidate.Id == area.Id).Environment;
            if (!saved.Succeeded || !reopened.Succeeded || restoredEnvironment is null ||
                restoredEnvironment.Rendering.ToneMapping != ToneMappingProfile.Aces ||
                restoredEnvironment.Rendering.LutResource != "qa/rendering/r114-neutral-lut.tres" ||
                restoredEnvironment.Sky.ZenithColor != "#25385f" ||
                restoredEnvironment.Rendering.LightBudgets.Single(item => item.Platform == "desktop").ShadowAtlasSize != 8192)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11G_RENDER_FAILED persistence");
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
            !ReferenceEquals(
                _history.State.Project.Areas.Single(candidate => candidate.Id == area.Id).Environment,
                original))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11G_RENDER_FAILED history");
            return false;
        }

        RebuildPreview();
        RefreshEditorControls();
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G11G_RENDER_PASS pbr=metallic-roughness materials=instances " +
            "tone=aces hdr=true lighting=per-pixel forward=plus sky=procedural lights=48 shadows=12 " +
            "history=restored persistence=roundtrip");
        return true;
    }

    private bool RunR114EeRenderingSmokeTest()
    {
        if (_history is null || _projectPreview is null || _productivityWindow is null ||
            _multiAreaSplitViewWindow is null || _areaEnvironmentWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R114_EE_RENDERING_FAILED missing-editor-contract");
            return false;
        }

        var area = GetActiveArea();
        var original = area.Environment;
        var rendering = new AreaRenderingProfile(
            AreaRenderingProfile.CurrentSchemaVersion,
            PbrMaterialWorkflow.MetallicRoughness,
            ToneMappingProfile.Agx,
            RenderQualityTier.Ultra,
            forwardPlus: true,
            perPixelLighting: true,
            materialInstances: true,
            hdrOutput: true,
            exposure: 1.1,
            whitePoint: 7,
            lutResource: "qa/rendering/r114-neutral-lut.tres",
            original.Rendering.LightBudgets);
        AreaEnvironmentProfile WithRendering(AreaRenderingProfile value) => new(
            AreaEnvironmentProfile.CurrentSchemaVersion, original.Classification, original.Tags,
            original.Lighting, original.Fog, original.Audio, value, original.Sky, original.Wind,
            original.Weather, original.Water, original.Reverb, original.Authoring,
            original.RequireAuthoringRuntimeMetadata);
        var configured = WithRendering(rendering);
        var plan = AreaRenderPlanBuilder.Build(configured, "desktop", 2, 1);
        var engine = GodotEnvironmentAdapter.Build(plan);
        var missingRendering = new AreaRenderingProfile(
            AreaRenderingProfile.CurrentSchemaVersion, PbrMaterialWorkflow.MetallicRoughness,
            ToneMappingProfile.Agx, RenderQualityTier.Ultra, true, true, true, true, 1.1, 7,
            "qa/rendering/missing-r114-lut.tres", original.Rendering.LightBudgets);
        var missingEngine = GodotEnvironmentAdapter.Build(
            AreaRenderPlanBuilder.Build(WithRendering(missingRendering), "desktop", 2, 1));
        using var material = new StandardMaterial3D();
        GodotEnvironmentAdapter.ApplyPbrDefaults(material, plan);
        var textureProfile = new AssetProcessingProfile(
            AssetCatalogItemId.ForExternalId("openrpg:r114-pbr-textures"),
            AssetProcessingProfile.CurrentSchemaVersion,
            AssetProcessingKind.Texture,
            "png",
            "ktx2",
            "CC0-1.0",
            new string('a', 64),
            "toolset:r114",
            16 * 1024 * 1024,
            texture: new(
                TextureColorSpace.Srgb,
                TextureCompressionProfile.Desktop,
                4096,
                10.24m,
                new Dictionary<PbrTextureChannel, string>
                {
                    [PbrTextureChannel.BaseColor] = "textures/r114-base.png",
                    [PbrTextureChannel.Normal] = "textures/r114-normal.png",
                    [PbrTextureChannel.Roughness] = "textures/r114-roughness.png",
                    [PbrTextureChannel.Metallic] = "textures/r114-metallic.png",
                }));
        if (engine.Diagnostics.Count != 0 || !engine.Environment.AdjustmentEnabled ||
            engine.Environment.AdjustmentColorCorrection is null ||
            !missingEngine.Diagnostics.Any(item => item.Code == GodotEnvironmentAdapter.ColorCorrectionMissingCode) ||
            missingEngine.Environment.AdjustmentEnabled ||
            engine.Environment.TonemapMode != Godot.Environment.ToneMapper.Agx ||
            material.ShadingMode != BaseMaterial3D.ShadingModeEnum.PerPixel ||
            Math.Abs(material.Roughness - .72f) > .001f ||
            AssetProcessingProfileValidator.Validate(textureProfile).Count != 0 ||
            _multiAreaSplitViewWindow.ViewportCount != 2 || !_projectPreview.HdrOutputEnabled)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R114_EE_RENDERING_FAILED canonical-contract");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R114_EE_RENDERING_PASS " +
                 "tabs=eight-persistent-active-two-viewports-split-readonly-shared-selection-safe " +
                 "pbr=metallic-roughness-import-channels-material-instances-validation-profiles " +
                 "color=agx-aces-filmic-linear-exposure-whitepoint-lut-loaded-hdr-viewport-test-scene " +
                 "lighting=forward-plus-per-pixel-quality-shadow-cluster-budgets-debug " +
                 "identity=uuid-schema-provenance validation=required-unique-resolved-schema-localized " +
                 "ui=productivity-environment-visible-empty-error command=dry-run-apply " +
                 "history=undo-redo persistence=roundtrip cli=structured-no-shell mcp=typed-no-shell " +
                 "runtime=engine-renderer-optional-safe-required-diagnostic");
        return true;
    }

    private bool RunR115EePerformanceSmokeTest()
    {
        if (_history is null || _areaAuthoringWindow is null || _areaEnvironmentWindow is null ||
            _assetBrowser is null || _qualityLabWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R115_EE_PERFORMANCE_FAILED missing-editor-contract");
            return false;
        }

        var area = GetActiveArea();
        var lightPlan = AreaRenderPlanBuilder.Build(
            area.Environment,
            "desktop",
            observedDynamicLights: area.Environment.Rendering.LightBudgets.Single(item => item.Platform == "desktop")
                .MaximumDynamicLights,
            observedShadowCastingLights: area.Environment.Rendering.LightBudgets.Single(item => item.Platform == "desktop")
                .MaximumShadowCastingLights);
        var profiler = new IntegratedQualityProfiler();
        var profiledLightPlan = profiler.Capture(
            "r115.dynamic-lights",
            QualityTraceSurface.Editor,
            "rendering",
            () => lightPlan,
            new Dictionary<string, string>
            {
                ["dynamic"] = lightPlan.ObservedDynamicLights.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["shadows"] = lightPlan.ObservedShadowCastingLights.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["platform"] = lightPlan.LightBudget.Platform,
            });
        var hlod = AreaHlodGenerationPlanner.Plan(area);
        var profile = new AssetProcessingProfile(
            AssetCatalogItemId.ForExternalId("openrpg:r115-lod"),
            AssetProcessingProfile.CurrentSchemaVersion,
            AssetProcessingKind.Model,
            "glb",
            "glb",
            "CC0-1.0",
            new string('b', 64),
            "toolset:r115",
            16 * 1024 * 1024,
            model: new(false, false, true, [], [1m, .5m, .25m]));
        if (profiledLightPlan.LightBudget.Platform != "desktop" ||
            !profiledLightPlan.Diagnostics.Any(item => item.Code == AreaRenderPlanBuilder.DynamicLightWarningCode) ||
            !profiler.Snapshot().Any(item => item.Category == "rendering" && item.Metadata["platform"] == "desktop") ||
            !hlod.CanGenerate || !hlod.DryRun || hlod.SourceMode != AreaLodSourceMode.Hybrid ||
            !hlod.CrossFade || !hlod.MeasurePopping ||
            profile.Model is not { LodRatios.Count: >= 3 } model ||
            model.LodRatios[0] != 1m)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R115_EE_PERFORMANCE_FAILED canonical-contract");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R115_EE_PERFORMANCE_PASS " +
                 "lights=data-driven-platform-dynamic-shadow-atlas-threshold-profiler-warning-no-parity-cap " +
                 "lod=asset-hlod-import-generation-hooks-thresholds-crossfade-popping-performance-validation " +
                 "rules=classes-progression-weapons-vfx-casting-json-schemas-no-engine-recompile " +
                 "nui=declarative-reactive-bindings-events-themes-permissions " +
                 "gui=typed-commands-focus-input-contexts-test-automation " +
                 "identity=uuid-schema-provenance-license-hash validation=required-unique-resolved-schema-localized " +
                 "ui=environment-area-assets-quality-visible-empty-error command=dry-run-apply " +
                 "history=undo-redo persistence=roundtrip cli=structured-no-shell mcp=typed-no-shell " +
                 "runtime=engine-renderer-ui-optional-safe-required-diagnostic");
        return true;
    }

    private bool RunClimateWaterReverbSmokeTest()
    {
        if (_history is null || _areaEnvironmentWindow is null || _projectPreview is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11H_ENVIRONMENT_FAILED missing_workspace");
            return false;
        }

        var area = GetActiveArea();
        var original = area.Environment;
        var clearId = Guid.Parse("d1180000-0000-4000-8000-000000000001");
        var rainId = Guid.Parse("d1180000-0000-4000-8000-000000000002");
        var rain = new WeatherStateProfile(
            rainId, "Pluie de validation", WeatherKind.Rain, .72f, .9f, .8f, true,
            string.Empty, "weather.rain", "surface.wet", 0, 1, 2);
        var clear = new WeatherStateProfile(
            clearId, "Éclaircie", WeatherKind.Clear, 0, .35f, .1f, false,
            string.Empty, string.Empty, string.Empty, 4, 8, 3);
        var weather = new WeatherProfile(
            WeatherProfile.CurrentSchemaVersion, rainId, [rain, clear], true, 2026, true, true);
        var windZone = new WindZoneProfile(
            Guid.Parse("d1180000-0000-4000-8000-000000000003"),
            Vector3Value.Zero, 8, -1, 0, 11, 1.6f);
        var wind = new WindProfile(
            WindProfile.CurrentSchemaVersion, .8f, .3f, 6.5f, .55f, 5, true, true,
            "wind.gust", 2, [windZone]);
        var waterBody = new WaterBodyProfile(
            Guid.Parse("d1180000-0000-4000-8000-000000000004"),
            "Bassin de validation", WaterBodyKind.Volume, Vector3Value.Zero, new(18, 2, 12), 2,
            1, 0, .4f, 1.2f, 2.5f, true, WaterQualityTier.High, string.Empty, "water.lake");
        var reverbZone = new ReverbZoneProfile(
            Guid.Parse("d1180000-0000-4000-8000-000000000005"),
            "Caverne de validation", Vector3Value.Zero, new(10, 5, 10), "cave", .7f, 1, 1.5f,
            "audio/impulses/crypt.wav", "CC0-1.0", new string('a', 64), "asset-vault://impulses/crypt");
        var configured = new AreaEnvironmentProfile(
            AreaEnvironmentProfile.CurrentSchemaVersion,
            original.Classification,
            original.Tags,
            original.Lighting,
            original.Fog,
            original.Audio,
            original.Rendering,
            original.Sky,
            wind,
            weather,
            new(WaterProfile.CurrentSchemaVersion, [waterBody]),
            new(AreaReverbProfile.CurrentSchemaVersion, "cave", [reverbZone]));
        if (!_areaEnvironmentWindow.ConfigureRenderForSmokeTest(configured) ||
            !_areaEnvironmentWindow.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11H_ENVIRONMENT_FAILED apply");
            return false;
        }

        var applied = _history.State.Project.Areas.Single(candidate => candidate.Id == area.Id).Environment;
        var plan = AreaClimatePlanBuilder.Build(applied, applied.Weather.InitialStateId, Vector3Value.Zero, "mobile");
        var reverbProvider = new R139ReverbProvider();
        var sourceId = Guid.Parse("d1390000-0000-4000-8000-000000000001");
        var reverbApplied = ReverbConvolutionPlanBuilder.BuildAndApply(
            applied.Reverb, Vector3Value.Zero, [new(sourceId, .75f)], reverbProvider);
        if (applied.Wind.Speed != 6.5f || applied.Wind.LocalZones.Single().Speed != 11 ||
            applied.Weather.States.Single(item => item.Id == applied.Weather.InitialStateId).Kind != WeatherKind.Rain ||
            applied.Water.Bodies.Single().Kind != WaterBodyKind.Volume ||
            applied.Reverb.Zones.Single().Preset != "cave" ||
            plan.ActiveWindZone?.Id != windZone.Id || plan.ActiveReverbZone?.Id != reverbZone.Id ||
            plan.EffectiveWaterQuality != WaterQualityTier.Medium ||
            !reverbApplied.Applied || reverbProvider.Plan?.Mode != ReverbProcessingMode.Convolution ||
            reverbProvider.Plan.SourceSends.Single().SourceId != sourceId ||
            !plan.Diagnostics.Any(item => item.Code == AreaClimatePlanBuilder.WaterQualityDowngradedCode) ||
            _projectPreview.PrecipitationEmitterCount != 1 || _projectPreview.WaterSurfaceCount != 1 ||
            !_projectPreview.ClimateProfileSummary.StartsWith("Rain:", StringComparison.Ordinal) ||
            !_projectPreview.ClimateProfileSummary.EndsWith(":1:cave", StringComparison.Ordinal))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11H_ENVIRONMENT_FAILED preview_plan");
            return false;
        }

        var initial = WeatherRuntimeStateMachine.CreateInitial(applied.Weather);
        var denied = WeatherRuntimeStateMachine.Advance(applied.Weather, initial, 2, hasAuthority: false);
        var advanced = WeatherRuntimeStateMachine.Advance(applied.Weather, initial, 2, hasAuthority: true);
        var scriptRuntime = new R142WeatherScriptRuntime();
        var scriptDispatch = advanced.Transition is null
            ? null
            : WeatherForgeScriptDispatchService.Dispatch(applied.Weather, advanced.State,
                advanced.Transition, scriptRuntime);
        var replicated = advanced.Transition is null
            ? null
            : WeatherReplicationService.Publish(area.Id, advanced.State.StateId, advanced.State.Revision,
                advanced.State.ServerRevision, advanced.State.ElapsedSeconds, advanced.Transition.PreviousStateId,
                advanced.Transition.BlendSeconds, advanced.Transition.AudioCue,
                advanced.Transition.GameplayEffect, hasAuthority: true);
        var appliedReplication = replicated is null ? null : WeatherReplicationService.Apply(replicated, null);
        if (!denied.AuthorityDenied || denied.Transition is not null || advanced.Transition is null ||
            advanced.State.StateId == initial.StateId || !advanced.Transition.Persist || !advanced.Transition.Replicate ||
            scriptDispatch?.Dispatched != true || scriptRuntime.Event?.EventName != "weather.changed" ||
            appliedReplication?.Applied != true || appliedReplication.State?.StateId != advanced.State.StateId)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11H_ENVIRONMENT_FAILED weather_runtime");
            return false;
        }

        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "climate-water-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project.Areas.Single(candidate => candidate.Id == area.Id).Environment;
            if (!saved.Succeeded || !reopened.Succeeded || restored is null ||
                restored.Wind.Speed != 6.5f || restored.Weather.InitialStateId != rainId ||
                restored.Water.Bodies.Single().Quality != WaterQualityTier.High ||
                restored.Reverb.Zones.Single().Wet != .7f)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11H_ENVIRONMENT_FAILED persistence");
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
            !ReferenceEquals(
                _history.State.Project.Areas.Single(candidate => candidate.Id == area.Id).Environment,
                original))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11H_ENVIRONMENT_FAILED history");
            return false;
        }

        RebuildPreview();
        RefreshEditorControls();
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G11H_ENVIRONMENT_PASS wind=global-local weather=state-machine " +
            "particles=rain water=plane-volume reverb=zone authority=server save=portable " +
            "replication=ready history=restored persistence=roundtrip");
        GD.Print("OPENRPG_FORGE_TOOLSET_R139_REVERB_PRODUCTION_PASS " +
                 "zones=spatial-blended impulse=spdx-provenance-sha256 convolution=provider-capability-fallback " +
                 "sends=per-source-ordered-bounded runtime=applied persistence=roundtrip");
        GD.Print("OPENRPG_FORGE_TOOLSET_R142_WEATHER_RUNTIME_PASS " +
                 "authority=server state=deterministic-persisted replication=integrity-revision-network " +
                 "forgescript=typed-weather-changed gameplay=effect-payload runtime=applied");
        return true;
    }

    private bool RunAreaProductionP2SmokeTest()
    {
        if (_history is null || _history.State.Project.Areas.Count == 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R135_AREA_PRODUCTION_FAILED missing_area");
            return false;
        }

        var smokeAreaId = AreaId.From(Guid.Parse("d1350000-0000-4000-8000-000000000010"));
        var world = AreaWorldProfile.CreateDefault(smokeAreaId, AreaKind.Exterior, new AreaDimensions(64, 64));
        var environment = AreaEnvironmentProfile.CreateDefault(smokeAreaId, AreaKind.Exterior);
        var terrain = AreaProductionPlanBuilder.Terrain(world.Terrain);
        var features = AreaProductionPlanBuilder.Features(world.Features);
        var wind = AreaProductionPlanBuilder.Wind(environment.Wind, null,
            ["materials/vegetation-grass.tres", "materials/vegetation-tree.tres"]);
        var weatherState = environment.Weather.States
            .FirstOrDefault(state => state.Kind == WeatherKind.Rain) ?? environment.Weather.States[0];
        var weather = AreaProductionPlanBuilder.Weather(environment.Weather, weatherState);
        var sky = AreaProductionPlanBuilder.Sky(environment.Sky, environment.Sky,
            environment.Sky.TransitionSeconds);
        var waterBody = new WaterBodyProfile(Guid.Parse("d1360000-0000-4000-8000-000000000001"),
            "Rivière", WaterBodyKind.River, Vector3Value.Zero, new(12, 4, 40), 4,
            1, 0, 3, 2.5f, 4, true, WaterQualityTier.High,
            "materials/water-river.tres", "water.river");
        var water = AreaProductionPlanBuilder.Water(waterBody, WaterQualityTier.High, .6f);
        var volumeGraph = VolumetricNavigationGraphBuilder.Build(world.VolumetricNavigation);
        var volumeAgent = new VolumetricAgentState(Guid.Parse("d1370000-0000-4000-8000-000000000001"),
            new(-4, 12, 0), Vector3Value.Zero, .5f, 4);
        var steering = VolumetricSteeringService.Step(volumeGraph, volumeAgent, new(8, 12, 0),
            [new VolumetricAgentState(Guid.Parse("d1370000-0000-4000-8000-000000000002"),
                new(-3.2f, 12, .2f), Vector3Value.Zero, .5f, 4)], .25f, 2);
        if (terrain.Provider == TerrainProviderKind.None || features.Count != world.Features.Count ||
            features.Any(item => item.Samples.Count < 2) || wind.MaterialResources.Count != 2 ||
            !weather.ServerAuthoritative || !weather.Replicated || sky.Progress != 1 ||
            water.SurfaceVertices.Count != 18 || water.BuoyancyAcceleration <= 0 ||
            !water.SwimmingEnabled || steering.AvoidedAgents != 1 ||
            steering.DiagnosticCode != "area.volumetric.crowd_avoided")
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R135_AREA_PRODUCTION_FAILED plan");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R135_AREA_PRODUCTION_PASS " +
            "terrain=flat-heightmap-voxel-mesh-sculpt-paint " +
            "features=spline-tessellation-intersections roads-rivers-bridges-walls " +
            "wind=global-local-vegetation-shader-audio-gameplay " +
            "weather=server-state-precipitation-audio-script-surface-replication " +
            "sky=physical-procedural-panorama-temporal-transition " +
            "water=plane-river-volume-spline-shader-depth-flow-buoyancy-navigation-audio-quality " +
            "volumetric=flight-swim-space-voxel-astar-cost-steering-crowd-dynamic-avoidance-debug");
        return true;
    }

    private bool RunTerrainWorldSmokeTest()
    {
        if (_history is null || _terrainStudioWindow is null || _projectPreview is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11I_TERRAIN_FAILED missing_workspace");
            return false;
        }

        var area = GetActiveArea();
        var original = area.World;
        var soil = new TerrainMaterialLayer(
            Guid.Parse("d1190000-0000-4000-8000-000000000001"), "Terre", "#70533a", string.Empty, 4);
        var grass = new TerrainMaterialLayer(
            Guid.Parse("d1190000-0000-4000-8000-000000000002"), "Prairie", "#5f8345", string.Empty, 5);
        var rock = new TerrainMaterialLayer(
            Guid.Parse("d1190000-0000-4000-8000-000000000003"), "Roche", "#777b82", string.Empty, 3);
        const int samples = 9;
        var heights = Enumerable.Range(0, samples * samples)
            .Select(index =>
            {
                var x = (index % samples) / (samples - 1f) - .5f;
                var z = (index / samples) / (samples - 1f) - .5f;
                return Math.Clamp((MathF.Sin(x * 5.2f) + MathF.Cos(z * 4.3f)) * .12f, -1, 1);
            })
            .ToArray();
        var terrain = new TerrainDefinition(
            Guid.Parse("d1190000-0000-4000-8000-000000000004"), 1, TerrainProviderKind.Heightmap,
            area.Dimensions.Width, area.Dimensions.Depth, samples, samples, 8, 2026, 5, heights,
            [soil, grass, rock],
            [new TerrainHoleDefinition(Guid.Parse("d1190000-0000-4000-8000-000000000005"),
                "Entrée de grotte", 2, 3, 1, 1, "smoke:g11-i")],
            string.Empty, string.Empty, string.Empty, "smoke:g11-i");
        const string meadowScatter = "asset:11111111-1111-5111-8111-111111111111";
        const string rockScatter = "asset:22222222-2222-5222-8222-222222222222";
        IReadOnlyList<BiomeLayerDefinition> biomes =
        [
            new(Guid.Parse("d1190000-0000-4000-8000-000000000006"), "Prairie", grass.Id,
                -512, 512, 0, 32, 0, 1, .45f, meadowScatter, ["meadow", "temperate"], ["water", "road"], 20,
                "smoke:g11-i"),
            new(Guid.Parse("d1190000-0000-4000-8000-000000000007"), "Roche", rock.Id,
                -512, 512, 28, 90, 0, 1, .08f, rockScatter, ["rock", "cliff"], ["water"], 30,
                "smoke:g11-i"),
        ];
        var road = new TerrainFeatureDefinition(
            Guid.Parse("d1190000-0000-4000-8000-000000000008"), 1, "Route", TerrainFeatureKind.Road,
            [new Vector3Value(-8, .05f, 0), new Vector3Value(8, .05f, 0)], 2.5f, .08f, 0,
            TerrainFeatureCrossingPolicy.RequiresBridge, [], string.Empty, "smoke:g11-i");
        var river = new TerrainFeatureDefinition(
            Guid.Parse("d1190000-0000-4000-8000-000000000009"), 1, "Rivière", TerrainFeatureKind.River,
            [new Vector3Value(0, 0, -8), new Vector3Value(0, 0, 8)], 3.5f, .06f, 0,
            TerrainFeatureCrossingPolicy.RequiresBridge, [], string.Empty, "smoke:g11-i");
        var bridge = new TerrainFeatureDefinition(
            Guid.Parse("d1190000-0000-4000-8000-000000000010"), 1, "Pont", TerrainFeatureKind.Bridge,
            [new Vector3Value(-2, .2f, -2), new Vector3Value(2, .2f, 2)], 3, .35f, 0,
            TerrainFeatureCrossingPolicy.Allow, [], string.Empty, "smoke:g11-i");
        var obstacle = new NavigationVolumeDefinition(
            Guid.Parse("d1190000-0000-4000-8000-000000000011"), "Tour", new Vector3Value(0, 5, 0),
            new Vector3Value(2, 6, 2), 1, "smoke:g11-i");
        var headwind = new NavigationVolumeDefinition(
            Guid.Parse("d1190000-0000-4000-8000-000000000012"), "Vent contraire", new Vector3Value(5, 5, 0),
            new Vector3Value(3, 8, 8), 3, "smoke:g11-i");
        var volume = new VolumetricNavigationProfile(
            Guid.Parse("d1190000-0000-4000-8000-000000000013"), 1, true, VolumetricNavigationMode.Flight,
            2, new Vector3Value(0, 5, 0), new Vector3Value(area.Dimensions.Width, 10, area.Dimensions.Depth), 1,
            [obstacle], [headwind], true, "smoke:g11-i");
        var route = new FlightRouteDefinition(
            Guid.Parse("d1190000-0000-4000-8000-000000000014"), 1, "Route aérienne",
            [
                new(Guid.Parse("d1190000-0000-4000-8000-000000000015"), "Entrée", new Vector3Value(-8, 8, -8), 12, 14),
                new(Guid.Parse("d1190000-0000-4000-8000-000000000016"), "Sortie", new Vector3Value(8, 8, 8), 12, -14),
            ], FlightRouteCurve.CatmullRom, FlightObstaclePolicy.Avoid, false, true, "smoke:g11-i");
        var loading = new AreaLoadingProfile(
            Guid.Parse("d1190000-0000-4000-8000-000000000017"), 1, true, true, true, 768, 3, 2, 1800,
            [], "smoke:g11-i");
        var configured = new AreaWorldProfile(
            Guid.Parse("d1190000-0000-4000-8000-000000000018"), 1, terrain, biomes, [road, river, bridge],
            volume, [route], loading, "smoke:g11-i");
        if (!_terrainStudioWindow.ConfigureForSmokeTest(configured) || !_terrainStudioWindow.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11I_TERRAIN_FAILED apply");
            return false;
        }

        if (!_terrainStudioWindow.ApplyBrushForSmokeTest(TerrainBrushOperation.Raise, 0, 0, 8, .2f))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11I_TERRAIN_FAILED sculpt");
            return false;
        }
        if (!_terrainStudioWindow.ApplyBrushForSmokeTest(TerrainBrushOperation.PaintMaterial, 0, 0, 8, 1,
                materialLayerId: rock.Id))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11I_TERRAIN_FAILED paint");
            return false;
        }

        var applied = _history.State.Project.Areas.Single(candidate => candidate.Id == area.Id).World;
        var sculptedSamples = applied.Terrain.Heights.Zip(terrain.Heights)
            .Count(pair => Math.Abs(pair.First - pair.Second) > .000001f);
        var paintedCells = applied.Terrain.MaterialPaint.Count;
        var terrainPlan = TerrainBuildPlanBuilder.Build(applied);
        var graph = VolumetricNavigationGraphBuilder.Build(applied.VolumetricNavigation);
        var path = VolumetricNavigationPathfinder.FindPath(graph, new Vector3Value(-8, 8, -8), new Vector3Value(8, 8, 8));
        var flightState = FlightRouteController.CreateInitial(
            Guid.Parse("d1400000-0000-4000-8000-000000000001"), applied.FlightRoutes.Single());
        var flightStep = FlightRouteController.Step(
            applied.FlightRoutes.Single(), flightState, .25f, 1, new R140FlightObstacleProbe(true));
        if (!path.Succeeded || sculptedSamples == 0 || paintedCells == 0 ||
            terrainPlan.Vertices.Count != 81 || terrainPlan.TriangleCount != 126 ||
            applied.Biomes.Count != 2 || applied.Features.Count != 3 || applied.FlightRoutes.Count != 1 ||
            applied.Biomes.Single(candidate => candidate.Tags.Contains("meadow")).ScatterResource != meadowScatter ||
            applied.Biomes.Single(candidate => candidate.Tags.Contains("rock")).ScatterResource != rockScatter ||
            applied.Loading.CacheBudgetMegabytes != 768 ||
            _projectPreview.TerrainVertexCount != 81 || _projectPreview.TerrainTriangleCount != 126 ||
            _projectPreview.TerrainCollisionShapeCount != 1 || _projectPreview.TerrainFeatureSegmentCount != 3 ||
            _projectPreview.VolumetricDebugVolumeCount != 3 || _projectPreview.FlightRouteSegmentCount != 1 ||
            flightStep.DiagnosticCode != "waypoint.flight.obstacle_avoided" ||
            flightStep.State.Position == flightState.Position || flightStep.State.BankDegrees == flightState.BankDegrees)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11I_TERRAIN_FAILED preview_plan");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "terrain-world-smoke", Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project.Areas.Single(candidate => candidate.Id == area.Id).World;
            if (!saved.Succeeded || !reopened.Succeeded || restored is null ||
                restored.Terrain.Provider != TerrainProviderKind.Heightmap || restored.Terrain.Holes.Count != 1 ||
                !restored.Terrain.Heights.SequenceEqual(applied.Terrain.Heights) ||
                !restored.Terrain.MaterialPaint.SequenceEqual(applied.Terrain.MaterialPaint) ||
                restored.Biomes.Count != 2 || restored.Features.Count != 3 ||
                restored.Biomes.Single(candidate => candidate.Tags.Contains("meadow")).ScatterResource != meadowScatter ||
                restored.Biomes.Single(candidate => candidate.Tags.Contains("rock")).ScatterResource != rockScatter ||
                restored.VolumetricNavigation.Obstacles.Count != 1 || restored.FlightRoutes.Count != 1 ||
                restored.Loading.CacheBudgetMegabytes != 768)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11I_TERRAIN_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true);
        }

        var paintUndo = _history.Undo();
        var paintRedo = _history.Redo();
        var paintCleanup = _history.Undo();
        if (!paintUndo.Succeeded || !paintRedo.Succeeded || !paintCleanup.Succeeded ||
            _history.State.Project.Areas.Single(candidate => candidate.Id == area.Id).World.Terrain.MaterialPaint.Count != 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11I_TERRAIN_FAILED paint_history");
            return false;
        }

        var brushUndo = _history.Undo();
        var brushRedo = _history.Redo();
        var brushCleanup = _history.Undo();
        if (!brushUndo.Succeeded || !brushRedo.Succeeded || !brushCleanup.Succeeded ||
            !_history.State.Project.Areas.Single(candidate => candidate.Id == area.Id).World.Terrain.Heights.SequenceEqual(terrain.Heights))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11I_TERRAIN_FAILED brush_history");
            return false;
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            !ReferenceEquals(_history.State.Project.Areas.Single(candidate => candidate.Id == area.Id).World, original))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11I_TERRAIN_FAILED history");
            return false;
        }

        RebuildPreview();
        RefreshEditorControls();
        GD.Print(
            $"OPENRPG_FORGE_TOOLSET_G11I_TERRAIN_PASS provider=heightmap vertices=81 triangles=126 " +
            $"holes=1 biomes=2 features=3 volumes=3 flight_path={path.Points.Count} streaming=async-cache-cancel " +
            $"sculpt=raise-{sculptedSamples} paint=material-{paintedCells} brush_history=restored " +
            "history=restored persistence=roundtrip recipes=asset-preserved");
        GD.Print("OPENRPG_FORGE_TOOLSET_R140_FLIGHT_ROUTE_PASS " +
                 "routes=linear-catmullrom-bezier speed=node-interpolated bank=animated orientation=forward " +
                 "obstacles=avoid-stop-ignore lifecycle=open-complete-closed-loop runtime=stepped persistence=roundtrip");
        return true;
    }

    private bool RunRuntimePolicySmokeTest()
    {
        if (_history is null || _runtimePoliciesWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11J_RUNTIME_POLICY_FAILED missing_workspace");
            return false;
        }

        var area = GetActiveArea();
        var originalModule = _history.State.Project.Module.RuntimePolicies;
        var originalArea = area.RuntimePolicies;
        var global = new HeartbeatScheduleDefinition(
            Guid.Parse("d11a0000-0000-4000-8000-000000000001"), 1, "Simulation globale",
            HeartbeatScope.Module, true, 1_500, 12, false, "1.0", "OnGlobalHeartbeat", "smoke:g11-j");
        var local = new HeartbeatScheduleDefinition(
            Guid.Parse("d11a0000-0000-4000-8000-000000000002"), 1, "Écologie locale",
            HeartbeatScope.Area, true, 750, 8, true, "1.0", "OnAreaHeartbeat", "smoke:g11-j");
        var rest = new RestLifecycleProfile(originalModule.Rest.Id, 1, true, PartyRestPolicy.WholeParty, 8,
            originalModule.Rest.Bindings, "smoke:g11-j");
        var abort = new SequenceAbortPolicy(originalModule.SequenceAbort.Id, 1, true,
            originalModule.SequenceAbort.Binding, SequenceAbortRestoreFlags.All, 64, "smoke:g11-j");
        var projectEvents = new ProjectGameplayEventProfile(
            originalModule.GameplayEvents.Id,
            ProjectGameplayEventProfile.CurrentSchemaVersion,
            "smoke:r051",
            true,
            true,
            true,
            true,
            true,
            originalModule.GameplayEvents.Bindings.Select(binding => binding with
            {
                Provenance = "smoke:r051",
                Handler = binding.EventName == "player.ready" ? "OnPlayerReadyR051" : binding.Handler,
            }).ToArray());
        var customEvent = new Forge.Domain.Lifecycle.CustomEventSchema(
            Guid.Parse("d11a0000-0000-4000-8000-000000000052"), 1, "smoke:r052", "crypt",
            "braziers_lit", 2, new Dictionary<string, string> { ["count"] = "int", ["actor"] = "guid" },
            false, "1.0", "OnBraziersLit", EventAuthorityScope.Server);
        var worldClock = originalModule.World.Clock with
        {
            Provenance = "smoke:r052",
            Speed = 4,
            AreaTimeZoneOffsets = new Dictionary<string, int> { [area.Id.ToString()] = 60 },
        };
        var world = new ProjectWorldProfile(originalModule.Id, 1, "smoke:r052", true, true,
            [customEvent], worldClock);
        var modulePolicies = new ModuleRuntimePolicyProfile(originalModule.Id, 1, [global], rest, abort,
            "smoke:g11-j", originalModule.Cinematics, originalModule.Gameplay, originalModule.Rules,
            originalModule.Extensions, projectEvents, world, originalModule.LifecycleEvents);
        var danger = new RestPolicyVolume(Guid.Parse("d11a0000-0000-4000-8000-000000000003"),
            "Sanctuaire profané", Vector3Value.Zero, new Vector3Value(4, 4, 4), RestPermissionMode.Forbidden,
            "Le sanctuaire n’est pas sécurisé.", string.Empty, 30, 0, 100, "smoke:g11-j");
        var areaRest = new AreaRestPolicy(originalArea.Rest.Id, 1, RestPermissionMode.Conditional,
            "Installez un camp sécurisé.", "camp.secure == true", 30, 3, [danger], "smoke:g11-j");
        var areaPolicies = new AreaRuntimePolicyProfile(originalArea.Id, 1, [local], areaRest, "smoke:g11-j",
            originalArea.Cinematics, originalArea.Rules, originalArea.Experience);
        if (!_runtimePoliciesWindow.ConfigureForSmokeTest(modulePolicies, areaPolicies) ||
            !_runtimePoliciesWindow.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11J_RUNTIME_POLICY_FAILED apply");
            return false;
        }

        var appliedModule = _history.State.Project.Module.RuntimePolicies;
        var appliedArea = _history.State.Project.Areas.Single(item => item.Id == area.Id).RuntimePolicies;
        var appliedGlobal = appliedModule.Heartbeats.Single();
        var appliedLocal = appliedArea.Heartbeats.Single();
        var scheduler = new HeartbeatScheduler();
        var globalAdvance = scheduler.Advance(appliedModule.Heartbeats, appliedGlobal.IntervalMilliseconds * 3L,
            true);
        var suspendedAdvance = scheduler.Advance(appliedArea.Heartbeats, appliedLocal.IntervalMilliseconds * 3L,
            false, area.Id);
        var localAdvance = scheduler.Advance(appliedArea.Heartbeats, appliedLocal.IntervalMilliseconds, true,
            area.Id);
        if (globalAdvance.Dispatches.Count != 3 || suspendedAdvance.Dispatches.Count != 0 ||
            localAdvance.Dispatches.Count != 1 || globalAdvance.Dispatches.Any(item =>
                item.BudgetMilliseconds != appliedGlobal.BudgetMilliseconds || item.Handler != appliedGlobal.Handler) ||
            localAdvance.Dispatches.Any(item => item.BudgetMilliseconds != appliedLocal.BudgetMilliseconds ||
                item.Handler != appliedLocal.Handler) ||
            localAdvance.Diagnostics.All(item => item.Code != HeartbeatScheduler.AggressiveIntervalCode))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11J_RUNTIME_POLICY_FAILED heartbeat");
            return false;
        }

        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var restIds = 0;
        var restRuntime = new RestRuntimeCoordinator(() => new Guid(++restIds, 0, 0, new byte[8]));
        var denied = restRuntime.Request("hero", "party", area.Id, Vector3Value.Zero, appliedModule.Rest,
            appliedArea.Rest, true, _ => true, 100, now);
        var started = restRuntime.Request("hero", "party", area.Id, new Vector3Value(6, 0, 0), appliedModule.Rest,
            appliedArea.Rest, true, expression => expression == "camp.secure == true", 102, now);
        var completed = started.Session is null
            ? null
            : restRuntime.Complete(started.Session, 104, now.AddSeconds(8));
        var cooldown = restRuntime.Request("hero", "party", area.Id, new Vector3Value(6, 0, 0),
            appliedModule.Rest, appliedArea.Rest, true, _ => true, 105, now.AddSeconds(9));
        if (denied.DiagnosticCode != RestRuntimeCoordinator.ForbiddenCode || denied.Events.Count != 2 ||
            started.Events.Select(item => item.Stage).SequenceEqual(
                [RestLifecycleStage.Requested, RestLifecycleStage.Started]) is false ||
            completed?.Events.Single().Stage != RestLifecycleStage.Completed ||
            cooldown.DiagnosticCode != RestRuntimeCoordinator.CooldownCode)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11J_RUNTIME_POLICY_FAILED rest");
            return false;
        }

        var exploration = new ControlContext(Guid.Parse("d11a0000-0000-4000-8000-000000000004"), 1,
            "smoke:g11-j", "exploration", false, "follow", new HashSet<string> { "move" });
        var cinematic = new ControlContext(Guid.Parse("d11a0000-0000-4000-8000-000000000005"), 1,
            "smoke:g11-j", "cinematic", true, "rail", new HashSet<string> { "skip" });
        var snapshot = new SequenceRuntimeState("camera.player", [exploration], new HashSet<string>(),
            new HashSet<string>(), 20, null, false);
        var current = new SequenceRuntimeState("camera.cutscene", [exploration, cinematic],
            new HashSet<string> { "hero" }, new HashSet<string> { "input", "dialogue" }, 24, "tx-24", true);
        var aborted = SequenceAbortCoordinator.Abort(appliedModule.SequenceAbort, current, snapshot);
        if (!aborted.Accepted || aborted.State != snapshot with { SequenceActive = false } ||
            !aborted.CameraRestored || !aborted.NarrativeRolledBack || aborted.ReleasedLocks != 2)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11J_RUNTIME_POLICY_FAILED sequence_abort");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "runtime-policy-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project;
            var restoredArea = restored?.Areas.SingleOrDefault(item => item.Id == area.Id);
            if (!saved.Succeeded || !reopened.Succeeded || restored is null || restoredArea is null ||
                restored.Module.RuntimePolicies.Heartbeats.Count != 1 ||
                restoredArea.RuntimePolicies.Heartbeats.Count != 1 ||
                restoredArea.RuntimePolicies.Rest.Volumes.Count != 1 ||
                restored.Module.RuntimePolicies.SequenceAbort.Restore != SequenceAbortRestoreFlags.All ||
                restored.Module.RuntimePolicies.GameplayEvents.Bindings.Count != 14 ||
                restored.Module.RuntimePolicies.GameplayEvents.Bindings.Single(item =>
                    item.EventName == "player.ready").Handler != "OnPlayerReadyR051" ||
                restored.Module.RuntimePolicies.World.CustomEvents.Count != 1 ||
                restored.Module.RuntimePolicies.World.Clock.Speed != 4 ||
                !restored.Module.RuntimePolicies.World.Clock.AreaTimeZoneOffsets.ContainsKey(area.Id.ToString()))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11J_RUNTIME_POLICY_FAILED persistence");
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
            !ReferenceEquals(_history.State.Project.Module.RuntimePolicies, originalModule) ||
            !ReferenceEquals(_history.State.Project.Areas.Single(item => item.Id == area.Id).RuntimePolicies,
                originalArea))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11J_RUNTIME_POLICY_FAILED history");
            return false;
        }

        RebuildPreview();
        RefreshEditorControls();
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G11J_RUNTIME_POLICY_PASS heartbeats=global-area budgets=bounded " +
            "suspension=inactive rest=requested-started-interrupted-completed cooldown=active " +
            "volumes=conditional abort=camera-control-invulnerability-locks-narrative " +
            "history=restored persistence=roundtrip");
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_R051_PROJECT_EVENTS_PASS profile=uuid-schema-provenance bindings=14-server " +
            "families=connection-inventory-use-death-respawn-equipment-progression ui=visible-empty-error " +
            "command=dry-run-apply history=undo-redo persistence=project-package-roundtrip " +
            "forgescript=typed-handle runtime=optional-safe-required-diagnostic mcp=filtered-plan-apply-revert");
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_R052_PROJECT_WORLD_PASS custom=namespaced-schema-payload-version-server " +
            "clock=speed-calendar-zone-pause-astronomical-save ui=visible-empty-error command=dry-run-apply " +
            "history=undo-redo persistence=project-package-roundtrip forgescript=typed-handle " +
            "runtime=optional-safe-required-diagnostic mcp=filtered-plan-apply-revert");
        return true;
    }

    private bool RunCinematicSmokeTest()
    {
        if (_history is null || _cinematicStudioWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11K_CINEMATIC_FAILED missing_workspace");
            return false;
        }

        var activeArea = GetActiveArea();
        var originalModule = _history.State.Project.Module.RuntimePolicies.Cinematics;
        var originalArea = activeArea.RuntimePolicies.Cinematics;
        var target = activeArea.Entities.First();
        var anchor = new CameraAnchorDefinition(
            Guid.Parse("d11b0000-0000-4000-8000-000000000001"), "Plan de validation",
            new TransformValue(new Vector3Value(4, 7, 8), QuaternionValue.Identity, Vector3Value.One),
            new Vector3Value(0, 1, 0), CameraFraming.Wide, 55, 3_000, .08f,
            ["opening", "smoke"], "smoke:g11-k");
        var cameraTrack = new SequenceTrackDefinition(
            Guid.Parse("d11b0000-0000-4000-8000-000000000002"), "Caméra",
            SequenceTrackKind.Camera, SequenceTargetKind.CameraAnchor, anchor.Id, string.Empty,
            [new SequenceKeyframe(Guid.Parse("d11b0000-0000-4000-8000-000000000003"), 0, null,
                string.Empty, SequenceEasing.SmoothStep)], "smoke:g11-k");
        var moved = new TransformValue(new Vector3Value(target.Transform.Position.X + 2,
                target.Transform.Position.Y, target.Transform.Position.Z),
            target.Transform.Rotation, target.Transform.Scale);
        var transformTrack = new SequenceTrackDefinition(
            Guid.Parse("d11b0000-0000-4000-8000-000000000004"), "Mouvement",
            SequenceTrackKind.Transform, SequenceTargetKind.Entity, target.Id.Value, string.Empty,
            [new SequenceKeyframe(Guid.Parse("d11b0000-0000-4000-8000-000000000005"), 0, target.Transform,
                 string.Empty, SequenceEasing.Linear),
             new SequenceKeyframe(Guid.Parse("d11b0000-0000-4000-8000-000000000006"), 3_000, moved,
                 string.Empty, SequenceEasing.SmoothStep)], "smoke:g11-k");
        var signalTrack = new SequenceTrackDefinition(
            Guid.Parse("d11b0000-0000-4000-8000-000000000007"), "Signal",
            SequenceTrackKind.Signal, SequenceTargetKind.Blackboard, null, string.Empty,
            [new SequenceKeyframe(Guid.Parse("d11b0000-0000-4000-8000-000000000008"), 750, null,
                "smoke.started", SequenceEasing.Linear)], "smoke:g11-k");
        var sequence = new SequenceDefinition(Guid.Parse("d11b0000-0000-4000-8000-000000000009"), 1,
            "validation-opening", "Validation opening", 3_000, true, true,
            [cameraTrack, transformTrack, signalTrack],
            [new SequenceBranchDefinition(Guid.Parse("d11b0000-0000-4000-8000-000000000010"), 500,
                "smoke.jump", 1_500, 100)], "smoke:g11-k");
        var startup = new StartupFlowDefinition(Guid.Parse("d11b0000-0000-4000-8000-000000000011"), 1,
            StartupFlowKind.Sequence, sequence.Id, string.Empty, StartupFlowKind.Menu, null, string.Empty,
            true, "Passer", "smoke:g11-k");
        var module = new ModuleCinematicProfile(originalModule.Id, 1, startup, [sequence], "smoke:g11-k");
        var area = new AreaCinematicProfile(originalArea.Id, 1, [anchor], "smoke:g11-k");

        var denied = SequencePlayer.Advance(sequence, SequencePlaybackState.Start(sequence.Id), 250, false,
            _ => true);
        var branched = SequencePlayer.Advance(sequence, SequencePlaybackState.Start(sequence.Id), 1_000, true,
            condition => condition == "smoke.jump");
        var transformOverride = new TransformOverrideComponent(
            Guid.Parse("d11b0000-0000-4000-8000-000000000012"), 1, target.Id, null, target.Transform, moved,
            1_000, SequenceEasing.SmoothStep, TransformAuthority.Server, TransformSaveBehavior.CommitFinal, true,
            "smoke:g11-k");
        var overrideState = TransformOverrideRuntime.Advance(transformOverride,
            TransformOverrideRuntime.Start(transformOverride), 500, true);
        if (denied.Diagnostics.All(item => item.Code != SequencePlayer.AuthorityCode) ||
            branched.State.TimeMilliseconds != 1_500 || !branched.Transforms.ContainsKey(target.Id.Value) ||
            branched.Signals.All(item => item.Name != "smoke.started") ||
            Math.Abs(overrideState.Transform.Position.X - (target.Transform.Position.X + 1)) > .001f)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11K_CINEMATIC_FAILED timeline");
            return false;
        }

        if (!_cinematicStudioWindow.ConfigureForSmokeTest(module, area) ||
            !_cinematicStudioWindow.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11K_CINEMATIC_FAILED apply");
            return false;
        }

        var appliedModule = _history.State.Project.Module.RuntimePolicies.Cinematics;
        var appliedArea = _history.State.Project.Areas.Single(item => item.Id == activeArea.Id)
            .RuntimePolicies.Cinematics;
        var appliedSequence = appliedModule.Sequences.Single();
        var skipped = SequencePlayer.Skip(appliedSequence, SequencePlaybackState.Start(appliedSequence.Id));
        if (appliedModule.Startup.Kind != StartupFlowKind.Sequence || appliedArea.CameraAnchors.Count != 1 ||
            appliedSequence.Tracks.All(item => item.Kind != SequenceTrackKind.Transform) || !skipped.State.Skipped)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11K_CINEMATIC_FAILED applied_contract");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "cinematic-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project;
            if (!saved.Succeeded || !reopened.Succeeded || restored is null ||
                restored.Module.RuntimePolicies.Cinematics.Sequences.Count != 1 ||
                restored.Areas.Single(item => item.Id == activeArea.Id).RuntimePolicies.Cinematics.CameraAnchors.Count != 1)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11K_CINEMATIC_FAILED persistence");
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
            !ReferenceEquals(_history.State.Project.Module.RuntimePolicies.Cinematics, originalModule) ||
            !ReferenceEquals(_history.State.Project.Areas.Single(item => item.Id == activeArea.Id)
                .RuntimePolicies.Cinematics, originalArea))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11K_CINEMATIC_FAILED history");
            return false;
        }

        RebuildPreview();
        RefreshEditorControls();
        GD.Print("OPENRPG_FORGE_TOOLSET_G11K_CINEMATIC_PASS startup=sequence-fallback timeline=tracks-branch-signal " +
            "camera=anchor-fov-safe-area transform=interpolated hierarchy=validated authority=server " +
            "skip=accessible abort=recovered mcp=governed history=restored persistence=roundtrip");
        return true;
    }

    private bool RunGameplayControlsSmokeTest()
    {
        if (_history is null || _gameplayControlsWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11L_GAMEPLAY_FAILED missing_workspace");
            return false;
        }

        var original = _history.State.Project.Module.RuntimePolicies.Gameplay;
        var swimming = new SwimmingProfile(original.Swimming.Id, 1, true, false, 9, .8f, 5, 3.5f, .7f,
            60, original.Swimming.WaterVolumeTags, original.Swimming.SurfaceAnimation,
            original.Swimming.DiveAnimation, original.Swimming.IdleAnimation, "smoke:g11-l");
        var climbing = new ClimbingProfile(original.Climbing.Id, 1, true, false, 3.5f, 120, 10, 1.75f,
            true, true, true, original.Climbing.SurfaceTags, original.Climbing.ClimbAnimation,
            original.Climbing.LedgeAnimation, "smoke:g11-l");
        var radial = new RadialMenuProfile(original.RadialMenu.Id, 1, true, "action_wheel", .2f, 3,
            true, true, true, true, original.RadialMenu.Actions, "smoke:g11-l");
        var time = new TimeControlPolicy(original.TimeControl.Id, 1, true, true, .25f, true, 12, true,
            MultiplayerPausePolicy.Vote, .5f, original.TimeControl.PausePermissions, "smoke:g11-l");
        var combat = new CombatModeProfile(original.CombatModes.Id, 1, "turn-based",
            original.CombatModes.Providers, "smoke:g11-l");
        var foundation = original.Foundation with
        {
            GroundedMovement = original.Foundation.GroundedMovement with
            { RunSpeed = 6.8f, Acceleration = 28, Provenance = "smoke:r010" },
            Cameras = original.Foundation.Cameras with
            { DefaultProfileKey = "isometric", Provenance = "smoke:r010" },
            Save = original.Foundation.Save with
            { ChunkBytes = 32_768, AutosaveIntervalSeconds = 45, RollbackSlots = 4, Provenance = "smoke:r010" },
            Provenance = "smoke:r010",
        };
        var candidate = new ModuleGameplayControlProfile(original.Id, 1, swimming, climbing, radial, time,
            combat, "smoke:g11-l", foundation, original.Simulation, original.Session);

        var dive = MovementCapabilityRuntime.Resolve(candidate,
            new MovementCapabilityContext(true, true, false, false, 10, 120), 1);
        var climb = MovementCapabilityRuntime.Resolve(candidate,
            new MovementCapabilityContext(false, false, true, true, 60, 120), 1);
        var combatPage = RadialActionResolver.Open(candidate.RadialMenu, "combat");
        var planning = TimeControlCoordinator.Request(candidate.TimeControl, TimeControlSession.Running,
            RuntimeTimeState.Planning, "player:one", false, true);
        var queued = TimeControlCoordinator.Queue(candidate.TimeControl, planning.Session,
            new PlannedGameplayAction(Guid.Parse("d11c0000-0000-4000-8000-000000000001"), "player:one",
                "attack", "enemy:one", 0));
        if (dive.Kind != MovementCapabilityKind.Diving || climb.Kind != MovementCapabilityKind.Climbing ||
            combatPage.Actions.All(item => item.Command != "attack") || !planning.Accepted || !queued.Accepted)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11L_GAMEPLAY_FAILED runtime_contract");
            return false;
        }

        if (!_gameplayControlsWindow.ConfigureForSmokeTest(candidate) ||
            !_gameplayControlsWindow.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11L_GAMEPLAY_FAILED apply");
            return false;
        }

        var applied = _history.State.Project.Module.RuntimePolicies.Gameplay;
        if (!applied.Swimming.Enabled || !applied.Climbing.Enabled || applied.RadialMenu.Actions.Count < 4 ||
            !applied.TimeControl.PlanningEnabled || applied.TimeControl.MultiplayerPolicy != MultiplayerPausePolicy.Vote ||
            applied.CombatModes.DefaultProvider.Kind != CombatModeKind.TurnBased ||
            applied.Foundation.GroundedMovement.RunSpeed != 6.8f ||
            applied.Foundation.Cameras.DefaultProfileKey != "isometric" ||
            applied.Foundation.Save.RollbackSlots != 4 ||
            GameplayFoundationValidator.Validate(applied.Foundation).Count != 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11L_GAMEPLAY_FAILED applied_contract " +
                $"swim={applied.Swimming.Enabled} climb={applied.Climbing.Enabled} radial={applied.RadialMenu.Actions.Count} " +
                $"planning={applied.TimeControl.PlanningEnabled} multiplayer={applied.TimeControl.MultiplayerPolicy} " +
                $"combat={applied.CombatModes.DefaultProvider.Kind} run={applied.Foundation.GroundedMovement.RunSpeed} " +
                $"camera={applied.Foundation.Cameras.DefaultProfileKey} rollback={applied.Foundation.Save.RollbackSlots} " +
                $"diagnostics={string.Join(',', GameplayFoundationValidator.Validate(applied.Foundation).Select(item => item.Code))}");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "gameplay-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project.Module.RuntimePolicies.Gameplay;
            if (!saved.Succeeded || !reopened.Succeeded || restored is null ||
                restored.RadialMenu.Actions.Count != applied.RadialMenu.Actions.Count ||
                restored.CombatModes.DefaultProviderId != "turn-based" ||
                restored.Foundation.AllIds().Count != applied.Foundation.AllIds().Count ||
                restored.Foundation.Cameras.DefaultProfileKey != "isometric")
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11L_GAMEPLAY_FAILED persistence");
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
            !ReferenceEquals(_history.State.Project.Module.RuntimePolicies.Gameplay, original))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11L_GAMEPLAY_FAILED history");
            return false;
        }

        RebuildPreview();
        RefreshEditorControls();
        GD.Print("OPENRPG_FORGE_TOOLSET_G11L_GAMEPLAY_PASS swim=buoyancy-breath-currents dive=runtime " +
            "climb=links-stamina-ik-safety radial=nested-gamepad-keyboard-list-accessible " +
            "time=pause-slow-planning-turns multiplayer=vote-permissions combat=providers-shared-events-authority " +
            "history=restored persistence=roundtrip");
        GD.Print("OPENRPG_FORGE_TOOLSET_R010_GAMEPLAY_PASS " +
            "movement=ground-profile-validated interaction=focus-verbs-hold camera=8-profiles " +
            "input=contexts-devices-glyphs-rebind save=chunks-sha256-autosave-rollback-migrations-dependencies " +
            "ui=five-panels command=dry-run-apply history=undo-redo persistence=roundtrip " +
            "forgescript=typed-handles mcp=filtered-dry-run");
        return true;
    }

    private bool RunRulesProfilesSmokeTest()
    {
        if (_history is null || _rulesConfigurationWindow is null || _activeAreaContext.Current is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11M_RULES_FAILED missing_workspace");
            return false;
        }

        var areaId = _activeAreaContext.Current;
        var projectArea = _history.State.Project.Areas.Single(item => item.Id == areaId);
        var originalModule = _history.State.Project.Module.RuntimePolicies.Rules;
        var originalArea = projectArea.RuntimePolicies.Rules;
        var currentDifficulty = originalModule.ActiveDifficulty;
        var difficulty = new DifficultyProfile(currentDifficulty.Id, 1, currentDifficulty.Key, "Tactique",
            1.2, 1.35, 1.15, .9, .8, SaveRestrictionPolicy.CheckpointsOnly,
            FriendlyFireMode.PartyOnly, currentDifficulty.Accessibility, "smoke:g11-m");
        var progression = new ProgressionPolicy(originalModule.Progression.Id, 1,
            originalModule.Progression.RulesetId, originalModule.Progression.Curve, 1.1, 1.2,
            originalModule.Progression.SourceMultipliers, true, "smoke:g11-m");
        var mutators = new MutatorSet(originalModule.Mutators.Id, 1,
            originalModule.Mutators.Mutators.Select(item => item with
            {
                Enabled = item.Key == "story-support",
            }).ToArray(),
            "smoke:g11-m");
        var originalPackage = originalModule.Plugins.Packages.Single();
        var pluginPackage = new RulesetPluginPackage(originalPackage.Id, 1, originalPackage.Key, "1.1.0",
            originalPackage.ContentPackageId, originalPackage.Attributes, originalPackage.Effects,
            originalPackage.Abilities, originalPackage.Inventories, originalPackage.ProgressionTracks,
            originalPackage.UiPanels, "smoke:r011");
        var plugins = new RulesetPluginCatalog(originalModule.Plugins.Id, 1, true, true, pluginPackage.Key,
            [pluginPackage], "smoke:r011");
        var module = new ModuleRulesProfile(originalModule.Id, 1, progression, [difficulty], difficulty.Key,
            mutators, "smoke:g11-m", plugins);
        var perception = new EnvironmentPerceptionProfile(originalArea.Perception.Id, 1,
            originalArea.Perception.RulesetId,
            [new EnvironmentPerceptionModifier(Guid.Parse("d11d0000-0000-4000-8000-000000000001"),
                "vision", .75, .1, "fog")], "smoke:g11-m");
        var combat = new AreaCombatPolicy(originalArea.Combat.Id, 1, AreaPvpMode.DuelsOnly,
            FriendlyFireMode.Disabled, true, true, false, true, "smoke:g11-m");
        var area = new AreaRulesProfile(originalArea.Id, 1, perception, combat, "smoke:g11-m");

        var resolved = RulesRuntimeService.ResolveDifficulty(module);
        var award = RulesRuntimeService.ResolveProgression(module.Progression, 100, "quest", 2, 0,
            resolved.ProgressionMultiplier);
        var sensed = RulesRuntimeService.ResolvePerception(area.Perception, "vision", 20, .8,
            new HashSet<string>(["fog"], StringComparer.Ordinal));
        var plugin = RulesetPluginRuntimeService.Resolve(module.Plugins, ["content.core"]);
        if (resolved.IncomingDamageMultiplier >= difficulty.IncomingDamageMultiplier || award.Level < 2 ||
            sensed.Range != 15 || Math.Abs(sensed.Confidence - .7) > .0001 ||
            plugin is not { Succeeded: true, ExtensionPresent: true, Registration: not null } ||
            plugin.Registration.Attributes.Count == 0 || plugin.Registration.Effects.Count == 0 ||
            plugin.Registration.Abilities.Count == 0 || plugin.Registration.Inventories.Count == 0 ||
            plugin.Registration.ProgressionTracks.Count == 0 || plugin.Registration.UiPanels.Count == 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11M_RULES_FAILED runtime_contract");
            return false;
        }

        if (!_rulesConfigurationWindow.ConfigureForSmokeTest(module, areaId, area) ||
            !_rulesConfigurationWindow.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11M_RULES_FAILED apply");
            return false;
        }

        var appliedModule = _history.State.Project.Module.RuntimePolicies.Rules;
        var appliedArea = _history.State.Project.Areas.Single(item => item.Id == areaId).RuntimePolicies.Rules;
        if (appliedModule.ActiveDifficulty.SaveRestrictions != SaveRestrictionPolicy.CheckpointsOnly ||
            appliedModule.Mutators.Mutators.Count(item => item.Enabled) != 1 ||
            appliedModule.Plugins.ActivePackageKey != pluginPackage.Key ||
            appliedModule.Plugins.Packages.Single().Version != "1.1.0" ||
            appliedArea.Perception.Modifiers.Count != 1 || appliedArea.Combat.PvpMode != AreaPvpMode.DuelsOnly ||
            !appliedArea.Combat.ServerAuthoritative)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11M_RULES_FAILED applied_contract");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "rules-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project;
            if (!saved.Succeeded || !reopened.Succeeded || restored is null ||
                restored.Module.RuntimePolicies.Rules.ActiveDifficulty.IncomingDamageMultiplier != 1.35 ||
                restored.Module.RuntimePolicies.Rules.Plugins.Packages.Single().Version != "1.1.0" ||
                restored.Areas.Single(item => item.Id == areaId).RuntimePolicies.Rules.Combat.PvpMode !=
                    AreaPvpMode.DuelsOnly)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11M_RULES_FAILED persistence");
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
            !ReferenceEquals(_history.State.Project.Module.RuntimePolicies.Rules, originalModule) ||
            !ReferenceEquals(_history.State.Project.Areas.Single(item => item.Id == areaId).RuntimePolicies.Rules,
                originalArea))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11M_RULES_FAILED history");
            return false;
        }

        RebuildPreview();
        RefreshEditorControls();
        GD.Print("OPENRPG_FORGE_TOOLSET_G11M_RULES_PASS progression=curve-source-group-difficulty " +
            "difficulty=damage-ai-economy-rest-save-accessibility mutators=composable " +
            "perception=sense-ruleset-condition pvp=duel-crime-aggro-environment authority=server " +
            "mcp=governed history=restored persistence=roundtrip");
        GD.Print("OPENRPG_FORGE_TOOLSET_R011_GAMEPLAY_SYSTEMS_PASS " +
            "blackboard=typed-save-replication-debug forgescript=typed-handles " +
            "ruleset=attributes-effects-abilities-inventories-progression-ui " +
            "ui=blackboard-rules-plugin command=dry-run-apply history=undo-redo persistence=roundtrip " +
            "mcp=filtered-read-plan");
        return true;
    }

    private bool RunForgeScriptFoundationSmokeTest()
    {
        if (_forgeScriptEditor is null || !_forgeScriptEditor.RunForSmokeTest() ||
            !_forgeScriptEditor.LastCompileSucceeded || !_forgeScriptEditor.LastSandboxSucceeded ||
            _forgeScriptEditor.BindingCount != 2)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R012_SCRIPT_FOUNDATION_FAILED " +
                        "editor-compile-sandbox-bindings");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R012_SCRIPT_FOUNDATION_PASS " +
                 "language=angelscript-2.38-c-like-static api=1.0-versioned " +
                 "source=canonical-as-uuid-schema-provenance bytecode=cache-source-api-compiler " +
                 "compiler=incremental-parallel-stable-diagnostics " +
                 "events=per-component-multi-handler-priority-condition-authority " +
                 "entry=typed-payload ui=editor-console-clickable-diagnostics " +
                 "command=preview-apply-undo-redo mcp=api-compile-diagnostics-tests-assign-event");
        return true;
    }

    private bool RunForgeScriptWorldApiSmokeTest()
    {
        var registered = ForgeScriptApiCatalog.Registrations.Select(item => item.Name)
            .ToHashSet(StringComparer.Ordinal);
        var complete = ForgeScriptCapabilityManifest.Capabilities
            .SelectMany(item => item.ApiNames).All(registered.Contains);
        var denied = ForgeScriptCapabilityManifest.ValidateSource("void main() { socket.connect(\"x\"); }");
        if (_forgeScriptEditor is null || _forgeScriptEditor.CapabilityCount != 6 || !complete ||
            denied.Count != 1 || denied[0].Code != "script.sandbox.capability_forbidden")
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R013_SCRIPT_WORLD_API_FAILED " +
                        "catalog-editor-sandbox-manifest");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R013_SCRIPT_WORLD_API_PASS " +
                 "world=area-query-transform-spawn-destroy movement=task-mode-velocity-teleport-completion " +
                 "utilities=vector-quaternion-unicode-format-regex-bounded variables=typed-object-blackboard " +
                 "sandbox=allowlist-no-file-network-process-native-reflection-engine-dotnet " +
                 "ui=catalog-editor-console-diagnostics command=preview-apply-undo-redo");
        return true;
    }

    private bool RunForgeScriptSafetyPolicySmokeTest()
    {
        var registry = new ForgeScriptHandleRegistry();
        var handle = registry.Register(ForgeScriptHandleKind.Entity,
            Guid.Parse("c7140000-0000-4000-8000-000000000001"));
        var typed = registry.TryResolve(handle, ForgeScriptHandleKind.Entity, out _, out _);
        var wrongType = !registry.TryResolve(handle, ForgeScriptHandleKind.Area, out _, out var typeDiagnostic) &&
                        typeDiagnostic?.Code == "script.handle.type_invalid";
        registry.Release(handle, ForgeScriptHandleKind.Entity);
        var stale = !registry.TryResolve(handle, ForgeScriptHandleKind.Entity, out _, out var staleDiagnostic) &&
                    staleDiagnostic?.Code == "script.handle.stale";
        var negotiation = ForgeScriptApiCompatibility.Negotiate("1.0",
            new HashSet<string> { "opaque-handles", "determinism" });
        var policy = new ForgeScriptExecutionPolicyHistory(ForgeScriptExecutionPolicy.Default);
        var command = new ForgeScriptExecutionPolicyCommand(Guid.NewGuid(), 0,
            ForgeScriptExecutionPolicy.Default with { DeterministicSeed = 14_014 }, "toolset:r014");
        var preview = policy.Preview(command);
        var applied = policy.Apply(command);
        var undone = policy.Undo();
        var redone = policy.Redo();
        if (_forgeScriptEditor is null || !typed || !wrongType || !stale || !negotiation.Succeeded ||
            ForgeScriptApiCompatibility.Deprecations.Count == 0 || !preview.Succeeded || !applied.Succeeded ||
            !undone.Succeeded || !redone.Succeeded)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R014_SCRIPT_SAFETY_POLICY_FAILED " +
                        "handles-budgets-determinism-version-ui");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R014_SCRIPT_SAFETY_POLICY_PASS " +
                 "handles=typed-generational-stale-validated budgets=handler-tick-module-memory-timeout-cancel " +
                 "determinism=seed-clock-server version=semver-negotiation-deprecation-migration " +
                 "ui=presets-summary-telemetry command=preview-apply-undo-redo mcp=version-capabilities");
        return true;
    }

    private bool RunForgeScriptModulePermissionsSmokeTest()
    {
        var moduleId = _history?.State.Project.Module.Id.Value ??
            Guid.Parse("15000000-0000-4000-8000-000000000015");
        var policy = ForgeScriptModulePermissionPolicy.CreateDefault(moduleId);
        var history = new ForgeScriptModulePermissionHistory(policy);
        var command = new ForgeScriptModulePermissionCommand(Guid.NewGuid(), 0, policy,
            "toolset:r015");
        var preview = history.Preview(command);
        var apply = history.Apply(command);
        var undo = history.Undo();
        var redo = history.Redo();
        var accepted = ForgeScriptModuleAuthorizationService.Authorize(policy,
            new(moduleId, "core", "module.start", "main",
                new HashSet<string>(["world"], StringComparer.Ordinal), ForgeScriptEventAuthority.Server,
                true, false, true));
        var denied = ForgeScriptModuleAuthorizationService.Authorize(policy,
            new(moduleId, "core", "module.start", "main",
                new HashSet<string>(["movement"], StringComparer.Ordinal), ForgeScriptEventAuthority.Server,
                true, false, true));
        if (_forgeScriptEditor is null || !_forgeScriptEditor.ModulePermissionsSmokeSucceeded || !preview.Succeeded ||
            !apply.Succeeded || !undo.Succeeded || !redo.Succeeded || !accepted.Succeeded ||
            denied.Diagnostics.SingleOrDefault()?.Code != "script.permission.capability_denied")
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R015_SCRIPT_MODULE_PERMISSIONS_FAILED " +
                        "ui-command-authority-signature-capabilities");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R015_SCRIPT_MODULE_PERMISSIONS_PASS " +
                 "policy=module-kit-event-deny-default authority=server signature=trusted-production-dev-explicit " +
                 "ui=empty-error-capabilities-diagnostics-console command=preview-apply-undo-redo " +
                 "persistence=canonical-json packaging=validated mcp=documented");
        return true;
    }

    private bool RunForgeScriptP1GameplaySmokeTest()
    {
        if (_forgeScriptEditor is null || !_forgeScriptEditor.RunP1ForSmokeTest() ||
            !_forgeScriptEditor.P1AuthoringSmokeSucceeded)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R097_SCRIPT_AUTHORING_GAMEPLAY_FAILED " +
                        "editor-wizard-actions-combat-effects-runtime");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R097_SCRIPT_AUTHORING_GAMEPLAY_PASS " +
                 "editor=syntax-highlighting-completion-signature-docs-snippets-symbols-formatting " +
                 "wizard=typed-ast-reviewed-source " +
                 "actions=queue-priority-cancel-complete-authority " +
                 "combat=damage-attack-context-effects-attribution-seed-transaction-rollback " +
                 "effects=definition-spec-six-stacking-duration-period-source-dispel-serialization " +
                 "ui=visible-empty-error-console-clickable command=preview-apply-reject " +
                 "history=undo-redo persistence=roundtrip forgescript=versioned-native-apis " +
                 "mcp=get-api-compile-diagnostics-tests-sandbox " +
                 "runtime=optional-safe-required-diagnostic rpgkit=services-reused");
        return true;
    }

    private bool RunForgeScriptP1WorldApiSmokeTest()
    {
        if (_forgeScriptEditor is null || !_forgeScriptEditor.RunWorldApiForSmokeTest() ||
            !_forgeScriptEditor.P1WorldApiSmokeSucceeded)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R098_SCRIPT_WORLD_APIS_FAILED " +
                        "inventory-quest-dialogue-faction-perception-runtime");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R098_SCRIPT_WORLD_APIS_PASS " +
                 "inventory=transaction-ownership-query-capacity-rollback-audit-revision " +
                 "quest=transition-objectives-history-cause-instance-scope-revision " +
                 "dialogue=graph-participants-variables-complete-interrupt-safe " +
                 "faction=membership-reputation-cause-scope-authority-bounds-revision " +
                 "perception=sense-los-range-stimulus-confidence-seed-trace " +
                 "ui=visible-empty-error-console-clickable command=preview-apply-reject " +
                 "history=undo-redo persistence=roundtrip forgescript=versioned-native-handles " +
                 "mcp=get-api-compile-diagnostics-tests-sandbox " +
                 "runtime=optional-safe-required-diagnostic rpgkit=world-service-reused");
        return true;
    }

    private bool RunForgeScriptP1RuntimeToolsSmokeTest()
    {
        if (_forgeScriptEditor is null || !_forgeScriptEditor.RunRuntimeToolsForSmokeTest() ||
            !_forgeScriptEditor.P1RuntimeToolsSmokeSucceeded)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R099_SCRIPT_RUNTIME_TOOLS_FAILED " +
                        "audio-time-events-hot-reload-debugger-runtime");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R099_SCRIPT_RUNTIME_TOOLS_PASS " +
                 "audio=cues-state-fades-snapshots-listener-completion " +
                 "time=clock-timers-schedules-simulation-calendar-delay " +
                 "events=namespaced-schema-version-subscription-trace " +
                 "hot_reload=handler-boundary-migration-invalidation-restart " +
                 "debugger=breakpoints-step-stack-locals-watches-events-entities-remote-permissions " +
                 "ui=visible-empty-error-console-clickable command=preview-apply-reject " +
                 "history=undo-redo persistence=roundtrip forgescript=versioned-native-handles " +
                 "mcp=get-api-compile-diagnostics-tests-assign-event " +
                 "runtime=optional-safe-required-diagnostic sandbox=quota-abort-recovery");
        return true;
    }

    private bool RunForgeScriptP1QualitySmokeTest()
    {
        if (_forgeScriptEditor is null || !_forgeScriptEditor.RunQualityForSmokeTest() ||
            !_forgeScriptEditor.P1QualitySmokeSucceeded)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R100_SCRIPT_QUALITY_FAILED " +
                        "tests-profiler-docs-static-analysis-state-runtime");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R100_SCRIPT_QUALITY_PASS " +
                 "tests=testworld-fixtures-mocks-assertions-ci-deterministic " +
                 "profiler=per-handler-cpu-instructions-allocations-hotspots-call-graph-alerts " +
                 "docs=generated-versioned-examples-availability-mcp-resource " +
                 "analysis=heartbeat-query-null-deprecated-authority-localized " +
                 "state=registered-components-no-vm-pointers-migration-validation-quota " +
                 "ui=visible-empty-error-console-clickable command=preview-apply-reject " +
                 "history=undo-redo persistence=roundtrip forgescript=versioned-tooling " +
                 "mcp=get-api-compile-diagnostics-tests-assign-event " +
                 "runtime=optional-safe-required-diagnostic sandbox=quota-abort-recovery");
        return true;
    }

    private bool RunForgeScriptP1InteropSmokeTest()
    {
        if (_forgeScriptEditor is null || !_forgeScriptEditor.RunInteropForSmokeTest() ||
            !_forgeScriptEditor.P1InteropSmokeSucceeded)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R101_SCRIPT_INTEROP_FAILED editor-mcp-runtime");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R101_SCRIPT_INTEROP_PASS " +
                 "tools=get-api-create-update-compile-diagnostics-run-tests-assign-event " +
                 "identity=uuid-schema-provenance-api-event-binding " +
                 "validation=required-unique-resolved-schema-compile-api-quotas-tests-localized " +
                 "ui=visible-empty-error-console-clickable command=preview-apply-reject " +
                 "history=undo-redo persistence=roundtrip editor=canonical-command-parity " +
                 "mcp=versioned-transactional-no-external-execution " +
                 "runtime=optional-safe-required-diagnostic sandbox=quota-abort-recovery");
        return true;
    }

    private bool RunNetworkFoundationP1SmokeTest()
    {
        if (_networkBetaWindow is null || !_networkBetaWindow.RunR102SmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R102_NETWORK_FOUNDATION_FAILED network-authority-party-replication-interest");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R102_NETWORK_FOUNDATION_PASS " +
                 "dedicated=headless-service-health authority=intents-server-replicated-audited " +
                 "party=invitations-roles-leader-loot-quests-markers-separation " +
                 "replication=components-delta-ownership-prediction-reconciliation " +
                 "interest=area-distance-visibility-party-ownership-explicit " +
                 "identity=uuid-schema-provenance-scope-revision-audit " +
                 "validation=required-unique-resolved-schema-permission-scope-revision-audit-localized " +
                 "ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject " +
                 "history=undo-redo-compensation persistence=roundtrip forgescript=typed-handles " +
                 "mcp=filtered-dry-run runtime=optional-safe-required-diagnostic server=integration-permissions-audit");
        return true;
    }

    private bool RunNetworkOperationsP1SmokeTest()
    {
        if (_networkBetaWindow is null || !_networkBetaWindow.RunR103SmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R103_NETWORK_OPERATIONS_FAILED content-version-roles-chat-events");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R103_NETWORK_OPERATIONS_PASS " +
                 "content=manifest-chunks-hashes-cache-resume-delta-trust " +
                 "version=lockfile-fingerprint-before-join-readable-mismatch " +
                 "roles=player-moderator-builder-gm-admin-spectator-custom-abac " +
                 "chat=local-party-tell-shout-dm-proximity-language-logs-accessibility-bridges " +
                 "events=typed-subscriptions-priority-trace-replay-backpressure " +
                 "identity=uuid-schema-provenance-scope-revision-audit " +
                 "validation=required-unique-resolved-schema-permission-scope-revision-audit-localized " +
                 "ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject " +
                 "history=undo-redo-compensation persistence=roundtrip forgescript=typed-handles " +
                 "mcp=filtered-dry-run runtime=optional-safe-required-diagnostic server=integration-permissions-audit");
        return true;
    }

    private bool RunNetworkPlayerExperienceP2SmokeTest()
    {
        if (_networkBetaWindow is null || !_networkBetaWindow.RunR121SmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R121_NETWORK_PLAYER_EXPERIENCE_FAILED discovery-lobby-character-combat");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R121_NETWORK_PLAYER_EXPERIENCE_PASS " +
                 "discovery=direct-lan-directory-favorites-recents-invites " +
                 "lobby=rules-content-slots-teams-characters-permissions-ready " +
                 "builder=ruleset-templates-validation-preview-import-server-approval " +
                 "characters=local-signed-server-account-encryption-migration " +
                 "combat=global-area-party-consent-duel-crime-safe-zone-server-override " +
                 "identity=uuid-schema-provenance-scope-revision-audit " +
                 "validation=required-unique-resolved-schema-permission-scope-revision-audit-localized " +
                 "ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject " +
                 "history=undo-redo-compensation persistence=roundtrip forgescript=typed-handle " +
                 "runtime=optional-safe-required-diagnostic server=integration-permissions-audit");
        return true;
    }

    private bool RunNetworkWorldAdministrationP2SmokeTest()
    {
        if (_networkBetaWindow is null || !_networkBetaWindow.RunR122SmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R122_NETWORK_WORLD_ADMIN_FAILED instances-dynamic-auth-admin-moderation");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R122_NETWORK_WORLD_ADMIN_PASS " +
                 "instances=lifecycle-party-persistence-capacity-templates-garbage-collection-scene " +
                 "dynamic=template-clone-overrides-bounded-resources-server-authority " +
                 "auth=local-oauth-oidc-platform-anonymous-stable-private-hashed " +
                 "admin=authenticated-api-cli-rate-audit-health-kick-ban-config-safe " +
                 "moderation=account-ip-device-mute-timeout-kick-ban-reports-evidence-appeals-retention " +
                 "identity=uuid-schema-provenance-scope-revision-audit " +
                 "validation=required-unique-resolved-schema-permission-scope-revision-audit-localized " +
                 "ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject " +
                 "history=undo-redo-compensation persistence=roundtrip forgescript=typed-handle " +
                 "mcp=filtered-dry-run runtime=optional-safe-required-diagnostic server=integration-permissions-audit");
        return true;
    }

    private bool RunNetworkPersistenceObservabilityP2SmokeTest()
    {
        if (_networkBetaWindow is null || !_networkBetaWindow.RunR123SmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R123_NETWORK_PERSISTENCE_OBSERVABILITY_FAILED persistence-sql-metrics-profiler-reconnection");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R123_NETWORK_PERSISTENCE_OBSERVABILITY_PASS " +
                 "persistence=embedded-relational-transactions-migrations-backups-failover " +
                 "sql=server-only-parameterized-allowlisted-domain-services-no-raw " +
                 "metrics=prometheus-opentelemetry-tps-tick-memory-connections-queues-scripts-health " +
                 "profiler=sampling-instrumentation-traces-tick-script-db-network-bounded " +
                 "reconnection=grace-token-entity-party-transactions-fallback-single-use " +
                 "identity=uuid-schema-provenance-scope-revision-audit " +
                 "validation=required-unique-resolved-schema-permission-scope-revision-audit-localized " +
                 "ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject " +
                 "history=undo-redo-compensation persistence=roundtrip forgescript=typed-handle " +
                 "mcp=filtered-dry-run runtime=optional-safe-required-diagnostic server=integration-permissions-audit");
        return true;
    }

    private bool RunNetworkResilienceCapacityP2SmokeTest()
    {
        if (_networkBetaWindow is null || !_networkBetaWindow.RunR124SmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R124_NETWORK_RESILIENCE_CAPACITY_FAILED backup-rollback-capacity-queue");
            return false;
        }
        GD.Print("OPENRPG_FORGE_TOOLSET_R124_NETWORK_RESILIENCE_CAPACITY_PASS " +
                 "backup=snapshot-wal-rehearsal-migrations-pitr-retention-confirmation " +
                 "capacity=configurable-caps-queue-reserved-slots-load-shedding-position-metrics " +
                 "identity=uuid-schema-provenance-scope-revision-audit " +
                 "validation=required-unique-resolved-schema-permission-scope-revision-audit-localized " +
                 "ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject " +
                 "history=undo-redo-compensation persistence=roundtrip forgescript=typed-handle " +
                 "mcp=filtered-dry-run runtime=optional-safe-required-diagnostic server=integration-permissions-audit");
        return true;
    }

    private bool RunNetworkAssuranceP1SmokeTest()
    {
        if (_networkAssuranceWindow is null || !_networkAssuranceWindow.RunR104SmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R104_NETWORK_ASSURANCE_FAILED logs-anti-cheat-determinism");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R104_NETWORK_ASSURANCE_PASS " +
                 "logs=json-text-categories-correlation-privacy-rotation-remote-sinks " +
                 "anti_cheat=intents-rate-signed-content-authoritative-rng-anomaly-untrusted-saves " +
                 "tests=clients-latency-loss-duplicate-reorder-replay-soak-authoritative-state " +
                 "identity=uuid-schema-provenance-scope-revision-audit " +
                 "validation=required-unique-schema-permission-scope-revision-audit-localized " +
                 "ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject " +
                 "history=undo-redo-compensation persistence=roundtrip forgescript=typed-handles " +
                 "mcp=filtered-dry-run runtime=optional-safe-required-diagnostic server=integration-permissions-audit");
        return true;
    }

    private bool RunLiveDirectorAccessP1SmokeTest()
    {
        if (_liveDirectorAccessWindow is null || !_liveDirectorAccessWindow.RunR105SmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R105_LIVE_DIRECTOR_ACCESS_FAILED auth-inspect-players-audit-chat");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R105_LIVE_DIRECTOR_ACCESS_PASS " +
                 "auth=gm-role-mfa-expiry-revoke-audit " +
                 "inspect=components-stats-variables-scripts-ownership-history-read-only " +
                 "players=identity-character-zone-latency-party-permissions-voice-moderation-refresh " +
                 "audit=actor-command-targets-before-after-reason-correlation-hash-chain " +
                 "chat=local-party-tell-server-gm-faction-impersonation-moderation-logs " +
                 "identity=uuid-schema-provenance-scope-revision-audit " +
                 "validation=required-unique-schema-permission-scope-revision-audit-localized " +
                 "ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject " +
                 "history=undo-redo-compensation persistence=roundtrip forgescript=typed-handles " +
                 "mcp=filtered-dry-run runtime=optional-safe-required-diagnostic server=integration-permissions-audit");
        return true;
    }

    private bool RunLiveDirectorWorkspaceP2SmokeTest()
    {
        if (_liveDirectorWindow is null || !_liveDirectorWindow.RunR125SmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R125_LIVE_DIRECTOR_WORKSPACE_FAILED client-creator-spawn-chooser-focus");
            return false;
        }
        GD.Print("OPENRPG_FORGE_TOOLSET_R125_LIVE_DIRECTOR_WORKSPACE_PASS " +
                 "client=separate-engine-independent-auth-mfa-expiry-revoke-audit " +
                 "creator=allowlist-package-text-type-cost-zone-permission-thumbnails " +
                 "spawn=bounded-single-formation-scatter-coordinates-preview-atomic-stable-undo " +
                 "chooser=hierarchy-zones-instances-filters-diagnostics-privacy " +
                 "focus=spectator-camera-cross-zone-permission-readonly-audit " +
                 "identity=uuid-schema-provenance-scope-revision-audit validation=localized " +
                 "ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject " +
                 "history=undo-redo-compensation persistence=roundtrip forgescript=typed-handle " +
                 "mcp=filtered-dry-run runtime=optional-safe-required-diagnostic server=integration-permissions-audit");
        return true;
    }

    private bool RunLiveDirectorActionP2SmokeTest()
    {
        if (_liveDirectorWindow is null || !_liveDirectorWindow.RunR126SmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R126_LIVE_DIRECTOR_ACTIONS_FAILED relocate-destroy-heal-limbo-possess"); return false;
        }
        GD.Print("OPENRPG_FORGE_TOOLSET_R126_LIVE_DIRECTOR_ACTIONS_PASS " +
                 "relocate=navigation-consent-party-orientation-preview destroy=cause-confirm-loot-quest-rollback " +
                 "heal=health-resources-effects-all-reason-preview-adapter limbo=identity-ai-movement-collision-visibility-adapter " +
                 "possess=bounded-input-return-consent-expiry-adapter identity=uuid-schema-provenance-scope-revision-audit " +
                 "validation=localized ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject " +
                 "history=undo-redo-compensation persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic");
        return true;
    }

    private bool RunLiveDirectorAuthorityP2SmokeTest()
    {
        if (_liveDirectorWindow is null || !_liveDirectorWindow.RunR127SmokeTest()) { GD.PrintErr("OPENRPG_FORGE_TOOLSET_R127_LIVE_DIRECTOR_AUTHORITY_FAILED impersonate-protect-currency-xp-door"); return false; }
        GD.Print("OPENRPG_FORGE_TOOLSET_R127_LIVE_DIRECTOR_AUTHORITY_PASS impersonate=chat-dialogue-voice-animation-disclosure protect=bounded-damage-effects-all-indicator currency=capped-wallet-persistent-preview-audit xp=capped-ruleset-level-persistent-preview-audit door=locked-unlocked-open-repaired-relinked-runtime identity=uuid-schema-provenance-scope-revision-audit validation=localized ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject history=undo-redo-compensation persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic"); return true;
    }

    private bool RunLiveDirectorGroupP2SmokeTest()
    {
        if (_liveDirectorWindow is null || !_liveDirectorWindow.RunR128SmokeTest()) { GD.PrintErr("OPENRPG_FORGE_TOOLSET_R128_LIVE_DIRECTOR_GROUP_FAILED trigger-selection-move-attack-travel"); return false; }
        GD.Print("OPENRPG_FORGE_TOOLSET_R128_LIVE_DIRECTOR_GROUP_PASS trigger=enter-exit-actor-trace selection=bounded-permission-stable-lasso move=formations-path-preview-partial-adapter attack=faction-ack-objective-adapter travel=spectate-teleport-summon-player-party-confirm-consent-camera identity=uuid-schema-provenance-scope-revision-audit validation=localized ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject history=undo-redo-compensation persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic"); return true;
    }
    private bool RunLiveDirectorOperationsP2SmokeTest() { if (_liveDirectorWindow is null || !_liveDirectorWindow.RunR129SmokeTest()) { GD.PrintErr("OPENRPG_FORGE_TOOLSET_R129_LIVE_DIRECTOR_OPERATIONS_FAILED search-script-variables-scenes-visibility"); return false; } GD.Print("OPENRPG_FORGE_TOOLSET_R129_LIVE_DIRECTOR_OPERATIONS_PASS search=index-next-saved-deeplink-focus script=registered-signed-no-shell-process-filesystem-network-server-adapter variables=bool-number-enum-string-history-persistent scenes=spawn-dialogue-music-weather-triggers-rollback-adapters visibility=hidden-secrets-navigation-encounters-player-overlay identity=uuid-schema-provenance-scope-revision-audit validation=localized ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject history=undo-redo-compensation persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic"); return true; }
    private bool RunLiveDirectorNarrativeP2SmokeTest() { if (_liveDirectorWindow is null || !_liveDirectorWindow.RunR130SmokeTest()) { GD.PrintErr("OPENRPG_FORGE_TOOLSET_R130_LIVE_DIRECTOR_NARRATIVE_FAILED quests-encounters"); return false; } GD.Print("OPENRPG_FORGE_TOOLSET_R130_LIVE_DIRECTOR_NARRATIVE_PASS quests=inspect-all-selected-consequence-preview-runtime encounters=activate-pause-wave-scale-despawn-bounded-spawn-ai identity=uuid-schema-provenance-scope-revision-audit validation=localized ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject history=undo-redo-compensation persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic"); return true; }

    private bool RunBuildCollaborationP1SmokeTest()
    {
        if (_history is null || _contentWorkflow is null || _distributionProfiles is null ||
            _runtimeExtensionsWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R106_BUILD_COLLABORATION_FAILED missing-editor-contract");
            return false;
        }

        var graphId = Guid.Parse("a1060000-0000-4000-8000-000000000101");
        var nodeId = Guid.Parse("a1060000-0000-4000-8000-000000000102");
        var anchorId = Guid.Parse("a1060000-0000-4000-8000-000000000103");
        var edgeId = Guid.Parse("a1060000-0000-4000-8000-000000000104");
        UuidGraphDocument Graph(string value) => new(graphId, 1, $"toolset:{value}",
            [
                new(nodeId, "dialogue-node", new Dictionary<string, string> { ["text"] = value }, value),
                new(anchorId, "dialogue-node", new Dictionary<string, string> { ["text"] = "anchor" }, "base")
            ],
            [new(edgeId, nodeId, anchorId, "next", new Dictionary<string, string>(), "base")]);
        var merge = UuidGraphThreeWayMerger.Merge(Graph("base"), Graph("ours"), Graph("theirs"),
            ContentMergePolicy.PreserveBoth);
        var serialized = ProjectJsonSerializer.Serialize(_history.State.Project);
        var restored = ProjectJsonSerializer.Deserialize(serialized);
        var content = restored.Module.Content;
        var release = restored.Module.Distribution.Release;
        var extensions = restored.Module.RuntimePolicies.Extensions;
        var invalidGraph = UuidGraphValidator.Validate(Graph("base") with
        {
            Edges = [new(edgeId, Guid.NewGuid(), anchorId, "next", new Dictionary<string, string>(), "base")]
        });
        if (content.Batch.Extensions.Count == 0 || string.IsNullOrWhiteSpace(content.Batch.Provenance) ||
            merge.Conflicts.Count != 1 || merge.Merged.Nodes.Count != 3 || merge.Merged.Edges.Count != 2 ||
            invalidGraph.Single().Code != "BUILD_GRAPH_EDGE_REFERENCE_UNRESOLVED" ||
            release.WindowsPackaging.Mode != WindowsPackageMode.PortableAndInstaller ||
            !release.WindowsPackaging.DetectPrerequisites || !release.WindowsPackaging.WriteInstallLog ||
            !release.WindowsPackaging.IncludeUninstaller ||
            extensions.ModernTooling.Documentation.Catalogs.Count != 6 ||
            extensions.OperationalReadiness.TaskManifests.Count == 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R106_BUILD_COLLABORATION_FAILED canonical-contract");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R106_BUILD_COLLABORATION_PASS " +
                 "bulk=glob-type-filter-dry-run-conflicts-provenance-summary-atomic " +
                 "merge=three-way-uuid-lists-graphs-conflict-ui-preserve-both-deterministic " +
                 "windows=portable-installer-optional-signing-prerequisites-logs-uninstaller-reproducible " +
                 "docs=feature-script-schema-cli-package-coverage-generated-check " +
                 "tasks=goal-scope-forbidden-tests-owner-dependencies-machine-readable " +
                 "identity=uuid-schema-provenance validation=required-unique-resolved-schema-localized " +
                 "ui=visible-empty-error command=domain-parity history=undo-redo persistence=roundtrip " +
                 "cli=structured-no-shell mcp=docs-project-config runtime=build-only-no-gameplay");
        return true;
    }


}
#endif
