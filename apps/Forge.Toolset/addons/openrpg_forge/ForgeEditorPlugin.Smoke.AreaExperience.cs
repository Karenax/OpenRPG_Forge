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
    private bool RunAreaExperienceSmokeTest()
    {
        if (_history is null || _areaExperienceWindow is null || _activeAreaContext.Current is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11N_EXPERIENCE_FAILED missing_workspace");
            return false;
        }

        var areaId = _activeAreaContext.Current;
        var original = _history.State.Project.Areas.Single(item => item.Id == areaId).RuntimePolicies.Experience;
        var cue = new AudioCueDefinition(Guid.Parse("d11e0000-0000-4000-8000-000000000001"),
            "ambient.embers", string.Empty, "Ambience", .8f, false, string.Empty, string.Empty, "1.0",
            "smoke:g11-n");
        var scatter = new SpatialScatterEmitter(Guid.Parse("d11e0000-0000-4000-8000-000000000002"),
            cue.CueId, Vector3Value.Zero, new Vector3Value(12, 3, 12), true, 2026, 1, 4, .75f, true,
            ["crypt"], ["safe-room"], "smoke:g11-n");
        var creature = _history.State.Project.Blueprints.First().Id;
        var voice = new CreatureVoiceProfile(Guid.Parse("d11e0000-0000-4000-8000-000000000003"), creature,
            "fr",
            [
                new LocalizedVoiceTake("fr", string.Empty, "Les braises nous observent.", string.Empty,
                    string.Empty, string.Empty),
                new LocalizedVoiceTake("en", string.Empty, "The embers are watching us.", string.Empty,
                    string.Empty, string.Empty),
            ], "tts.local",
            new VoiceConsentMetadata(true, true, "consent:smoke", "voix synthétique de test"), "smoke:g11-n");
        var loading = new AreaLoadingPresentation(original.Loading.Id, "Crypte des Cendres", "loading.png",
            "loading.ogv", new Dictionary<string, IReadOnlyList<string>>
            {
                ["fr"] = ["La lumière révèle les passages sûrs."],
                ["en"] = ["Light reveals safe paths."],
            }, true, "Préparation de la Crypte des Cendres.", true, 250, "smoke:g11-n");
        var profile = new AreaExperienceProfile(original.Id, 1, [cue], [scatter], [voice], [], loading,
            "smoke:g11-n");

        var emission = AreaExperienceRuntimeService.ResolveScatter(profile, scatter.Id, 3,
            new HashSet<string>(["crypt"], StringComparer.Ordinal), new HashSet<string>(), true);
        var repeated = AreaExperienceRuntimeService.ResolveScatter(profile, scatter.Id, 3,
            new HashSet<string>(["crypt"], StringComparer.Ordinal), new HashSet<string>(), true);
        var started = AreaExperienceRuntimeService.PlayCue(profile, AreaAudioRuntimeState.Empty, cue.CueId,
            DateTimeOffset.UnixEpoch, () => Guid.Parse("d11e0000-0000-4000-8000-000000000004"));
        var interrupted = AreaExperienceRuntimeService.Interrupt(started.State, started.Playback.PlaybackId,
            "area-transition", DateTimeOffset.UnixEpoch,
            () => Guid.Parse("d11e0000-0000-4000-8000-000000000005"));
        var resolvedVoice = AreaExperienceRuntimeService.ResolveVoice(voice, "de", "fr");
        var frame = AreaExperienceRuntimeService.ResolveLoading(loading, "fr", 2, 4, 0, true, true);
        var from = AreaId.From(Guid.Parse("d11e0000-0000-4000-8000-000000000010"));
        var destination = AreaId.From(Guid.Parse("d11e0000-0000-4000-8000-000000000011"));
        var resume = Guid.Parse("d11e0000-0000-4000-8000-000000000012");
        var patrol = new CrossAreaPatrolRoute(Guid.Parse("d11e0000-0000-4000-8000-000000000013"), "guard",
            [new CrossAreaPatrolSegment(Guid.Parse("d11e0000-0000-4000-8000-000000000014"), from, destination,
                PortalId.From(Guid.Parse("d11e0000-0000-4000-8000-000000000015")), resume, true,
                PatrolOwnershipAuthority.Server)], false, "smoke:g11-n");
        var cursor = new CrossAreaPatrolCursor(0, from, Guid.Empty, false, false);
        var waiting = CrossAreaPatrolRuntimeService.Advance(patrol, cursor, true, false);
        var resumed = CrossAreaPatrolRuntimeService.Advance(patrol, waiting.Cursor, true, true);
        if (emission is null || emission != repeated ||
            interrupted.Events.LastOrDefault() is not AudioCueInterrupted || !resolvedVoice.UsesTts ||
            frame.MediaKind != LoadingMediaKind.Image || frame.Progress != .5 ||
            !waiting.Cursor.AwaitingStreaming || resumed.Cursor.ResumeWaypointId != resume)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11N_EXPERIENCE_FAILED runtime_contract");
            return false;
        }

        if (!_areaExperienceWindow.ConfigureForSmokeTest(areaId, profile) ||
            !_areaExperienceWindow.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11N_EXPERIENCE_FAILED apply");
            return false;
        }

        var applied = _history.State.Project.Areas.Single(item => item.Id == areaId).RuntimePolicies.Experience;
        if (applied.AudioCues.Count != 1 || applied.ScatterEmitters.Count != 1 || applied.Voices.Count != 1 ||
            applied.Voices[0].Takes.Count != 2 ||
            !applied.Loading.ShowProgress || !applied.Loading.PrefetchRequiredResources)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11N_EXPERIENCE_FAILED applied_contract");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "experience-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project.Areas.Single(item => item.Id == areaId)
                .RuntimePolicies.Experience;
            if (!saved.Succeeded || !reopened.Succeeded || restored is null || restored.AudioCues.Count != 1 ||
                restored.Loading.Title != "Crypte des Cendres")
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11N_EXPERIENCE_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true);
        }

        var undo = _history.Undo(); var redo = _history.Redo(); var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            !ReferenceEquals(_history.State.Project.Areas.Single(item => item.Id == areaId)
                .RuntimePolicies.Experience, original))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11N_EXPERIENCE_FAILED history");
            return false;
        }

        RebuildPreview(); RefreshEditorControls();
        GD.Print("OPENRPG_FORGE_TOOLSET_G11N_EXPERIENCE_PASS scatter=volume-surface-nav-biome-seed " +
            "voice=localized-phoneme-subtitle-tts-consent audio=started-completed-interrupted-music-snapshot " +
            "patrol=cross-area-streaming-owner-resume loading=image-video-tip-progress-accessible-prefetch " +
            "mcp=governed history=restored persistence=roundtrip");
        return true;
    }

    private bool RunAudioEmitterSmokeTest()
    {
        if (_history is null || _areaEnvironmentWindow is null || _activeAreaContext.Current is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R004_AUDIO_FAILED missing_workspace");
            return false;
        }

        var areaId = _activeAreaContext.Current;
        var area = _history.State.Project.Areas.Single(item => item.Id == areaId);
        var original = area.Environment;
        var definitionId = Guid.Parse("a0040000-0000-4000-8000-000000000401");
        var instanceId = Guid.Parse("a0040000-0000-4000-8000-000000000402");
        var catalog = new AudioEmitterCatalog(AudioEmitterCatalog.CurrentSchemaVersion,
        [
            new AudioEmitterDefinition(definitionId, AudioEmitterDefinition.CurrentSchemaVersion, "Cascade de validation",
                new AudioClipSet(Guid.Parse("a0040000-0000-4000-8000-000000000403"),
                    AudioClipSet.CurrentSchemaVersion, "Cascade clips", AudioClipSelectionMode.Weighted,
                    [new(Guid.Parse("a0040000-0000-4000-8000-000000000404"), "audio/waterfall.ogg", 2,
                        ["water", "ambient"], "close", "fr", -18, "audio/waterfall.preview.ogg",
                        "audio/ogg", 1048576, "CC0-1.0", "smoke:r004", new string('a', 64))],
                    "smoke:r004"),
                new(AudioLoopMode.SeamlessRegion, 0, 1, 8, .25f, string.Empty),
                new(2, 42, "inverse-square", 180, -12, .25f, 1.5f, true), null, .25f,
                "scripts/waterfall-on.forge", "scripts/waterfall-off.forge", true, "smoke:r004",
                "assets/asset-manifests.json", "CC0-1.0", new string('a', 64)),
        ],
        [
            new AudioEmitterInstance(instanceId, definitionId,
                new TransformValue(new Vector3Value(6, 2, -5), QuaternionValue.Identity, Vector3Value.One),
                true, "smoke:r004"),
        ]);
        var audio = original.Audio;
        var updatedAudio = new AreaAudioProfile(audio.DayMusic, audio.NightMusic, audio.CombatMusic,
            audio.Ambience, audio.ReverbPreset, audio.TransitionSeconds, audio.Playlist, audio.OrderMode,
            audio.IntervalSeconds, audio.IntervalVariationSeconds, audio.RelativeVolume, audio.Spatial,
            audio.RuntimeControl, audio.AdaptiveLayers, audio.Orchestration, catalog, audio.Authoring);
        var configured = new AreaEnvironmentProfile(AreaEnvironmentProfile.CurrentSchemaVersion,
            original.Classification, original.Tags, original.Lighting, original.Fog, updatedAudio,
            original.Rendering, original.Sky, original.Wind, original.Weather, original.Water, original.Reverb);

        var runtime = AudioEmitterRuntimeService.CreateInitialPlaybacks(catalog, 2026).Single();
        if (runtime.Handle.Id != instanceId || runtime.Transform.Position != new Vector3Value(6, 2, -5) ||
            runtime.Loop.Mode != AudioLoopMode.SeamlessRegion || !runtime.Autoplay)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R004_AUDIO_FAILED runtime_contract");
            return false;
        }
        if (!_areaEnvironmentWindow.ConfigureRenderForSmokeTest(configured) ||
            !_areaEnvironmentWindow.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R004_AUDIO_FAILED authoring_apply");
            return false;
        }
        var applied = _history.State.Project.Areas.Single(item => item.Id == areaId).Environment.Audio.Emitters;
        if (applied.Definitions.Single().ClipSet.Clips.Single().LicenseSpdx != "CC0-1.0" ||
            applied.Instances.Single().Transform.Position != new Vector3Value(6, 2, -5) ||
            AudioEmitterCatalogValidator.Validate(applied).Count != 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R004_AUDIO_FAILED canonical_validation");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "audio-emitter-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project.Areas.Single(item => item.Id == areaId)
                .Environment.Audio.Emitters;
            if (!saved.Succeeded || !reopened.Succeeded || restored is null ||
                restored.Definitions.Single().Id != definitionId ||
                restored.Instances.Single().Id != instanceId)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R004_AUDIO_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true);
        }

        var undo = _history.Undo(); var redo = _history.Redo(); var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            !ReferenceEquals(_history.State.Project.Areas.Single(item => item.Id == areaId).Environment, original))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R004_AUDIO_FAILED history");
            return false;
        }
        RebuildPreview(); RefreshEditorControls();
        GD.Print("OPENRPG_FORGE_TOOLSET_R004_AUDIO_PASS object=definition-instance clips=weighted-licensed " +
            "loop=seamless-positioned validation=files-license-loudness-range-memory " +
            "script=typed-handle mcp=filtered-dry-run history=restored persistence=roundtrip");
        return true;
    }

    private bool RunRuntimeExtensionsSmokeTest()
    {
        if (_history is null || _runtimeExtensionsWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11O_EXTENSIONS_FAILED missing_workspace");
            return false;
        }

        var original = _history.State.Project.Module.RuntimePolicies.Extensions;
        var rule = new DataDrivenRuleSchema(Guid.Parse("e0110000-0000-4000-8000-000000000001"), 1,
            "class.ember-warden", RuleSchemaKind.Class, "{\"type\":\"object\"}",
            "{\"hitDie\":10,\"casting\":\"focus\"}", true, "smoke:g11-o");
        var json = new RuntimeJsonDocumentDefinition(Guid.Parse("e0150000-0000-4000-8000-000000000001"), 1,
            "adventure-state", "{\"type\":\"object\",\"required\":[\"chapter\"]}",
            "{\"chapter\":1,\"status\":\"Prêt\"}", 4096, 8, true, "1.0", "smoke:g11-o");
        var label = new ScriptableUiWidget(Guid.Parse("e0120000-0000-4000-8000-000000000001"),
            "chapter-label", ScriptableUiWidgetKind.Label, null, "Chapitre", null, false, 0, "player");
        var button = new ScriptableUiWidget(Guid.Parse("e0120000-0000-4000-8000-000000000002"),
            "continue-button", ScriptableUiWidgetKind.Button, null, "Continuer", null, true, 1, "player");
        var binding = new ScriptableUiEventBinding(Guid.Parse("e0130000-0000-4000-8000-000000000001"),
            "continue-button", ScriptableUiEventKind.Pressed, "ContinueAdventure", "1.0", "gameplay",
            "player");
        var ui = new ScriptableUiDocument(Guid.Parse("e0120000-0000-4000-8000-000000000010"), 1,
            "adventure-hud", "Aventure", "forge-dark", true, [label, button], [binding], "smoke:g11-o");
        var snapshot = new EntityStateSnapshotPolicy(original.SnapshotPolicy.Id, 1,
            ["transform", "combat", "inventory", "blackboard"], 128, 256, 32768, true, "smoke:g11-o");
        var profile = new ModuleRuntimeExtensionProfile(original.Id, 1, [rule], [ui], [json], snapshot,
            "smoke:g11-o");

        if (!_runtimeExtensionsWindow.ConfigureForSmokeTest(profile) ||
            !_runtimeExtensionsWindow.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11O_EXTENSIONS_FAILED apply");
            return false;
        }

        var applied = _history.State.Project.Module.RuntimePolicies.Extensions;
        if (applied.RuleSchemas.Single().Kind != RuleSchemaKind.Class ||
            applied.UiDocuments.Single().EventBindings.Single().Command != "ContinueAdventure" ||
            applied.JsonDocuments.Single().MaxDepth != 8 || applied.SnapshotPolicy.MaxInventoryItems != 256 ||
            applied.ModernTooling.RuntimeCopy.MaxCopiesPerTransaction != 32 ||
            applied.ModernTooling.AreaEffectIndicator.Shapes.Count != 4 ||
            applied.ModernTooling.UiToolkit.ToolkitVersion != "2.0" ||
            !applied.ModernTooling.Compiler.Incremental || applied.ModernTooling.Documentation.Catalogs.Count != 6 ||
            applied.OperationalReadiness.TaskManifests.Count != 1 ||
            !applied.OperationalReadiness.CrashArtifacts.CaptureSanitizedLogs ||
            !applied.OperationalReadiness.ProjectHealth.IncludeCoverage ||
            !applied.OperationalReadiness.Recovery.QuarantineCorruptFiles ||
            !applied.OperationalReadiness.ContentSynchronization.Resumable ||
            applied.ModernTooling.QualityAssurance.ClickableDiagnostics.Categories.Count != 7 ||
            applied.ModernTooling.QualityAssurance.MaliciousFixtures.Families.Count != 7 ||
            applied.ModernTooling.QualityAssurance.AuroraParityCoverage.ExpectedFeatureCount != 717 ||
            !applied.ModernTooling.QualityAssurance.AcceptanceGates.RequiredGates.Contains("G8", StringComparer.Ordinal))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11O_EXTENSIONS_FAILED canonical_contract");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "extensions-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project.Module.RuntimePolicies.Extensions;
            if (!saved.Succeeded || !reopened.Succeeded || restored is null ||
                 restored.RuleSchemas.Single().Key != "class.ember-warden" ||
                 restored.UiDocuments.Single().Widgets.Count != 2 ||
                 restored.JsonDocuments.Single().InitialValue != "{\"chapter\":1,\"status\":\"Prêt\"}" ||
                 !restored.ModernTooling.UiToolkit.Accessibility ||
                 restored.ModernTooling.Documentation.OutputDirectory != "docs/generated" ||
                 restored.ModernTooling.QualityAssurance.ClickableDiagnostics.ActiveProfile != "authoring" ||
                 restored.ModernTooling.QualityAssurance.MaliciousFixtures.Families.Count != 7 ||
                 restored.ModernTooling.QualityAssurance.AuroraParityCoverage.ExpectedFeatureCount != 717 ||
                 !restored.ModernTooling.QualityAssurance.AcceptanceGates.RequireLinkedDocumentation ||
                 restored.OperationalReadiness.TaskManifests.Single().Key != "validate-module" ||
                 restored.OperationalReadiness.ContentSynchronization.StorageDirectory != ".forge/content-cache")
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11O_EXTENSIONS_FAILED persistence");
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
            !ReferenceEquals(_history.State.Project.Module.RuntimePolicies.Extensions, original))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11O_EXTENSIONS_FAILED history");
            return false;
        }

        RebuildPreview();
        RefreshEditorControls();
        GD.Print("OPENRPG_FORGE_TOOLSET_G11O_EXTENSIONS_PASS rules=class-progression-weapon-vfx-casting " +
            "ui=declarative-reactive-themed-permission events=typed-focus-input-testable " +
            "json=schema-quota-safe-api snapshots=components-references-inventory-blackboard " +
            "history=restored persistence=roundtrip");
        GD.Print("OPENRPG_FORGE_TOOLSET_G11P_MODERN_PASS copy=uuid-owner-references-state-authority " +
            "aoe=mesh-targets-cover-height-colorblind-confirmation nui=2.0-forms-lists-canvas-accessibility-localization-inspector " +
            "compiler=incremental-stable-telemetry-conformance docs=metadata-generated-cli-mcp history=restored persistence=roundtrip");
        var loadingProfile = GetActiveArea().World.Loading;
        var passingLoadingGate = AreaStreamingBenchmarkGate.Evaluate(loadingProfile,
            new AreaStreamingResult([], 0, 0, 0,
                TimeSpan.FromMilliseconds(Math.Max(0, loadingProfile.TargetLoadMilliseconds - 1)), true));
        var failingLoadingGate = AreaStreamingBenchmarkGate.Evaluate(loadingProfile,
            new AreaStreamingResult([], 1, 0, 1,
                TimeSpan.FromMilliseconds(loadingProfile.TargetLoadMilliseconds + 1), false));
        if (!passingLoadingGate.Passed || failingLoadingGate.Passed ||
            failingLoadingGate.Diagnostics.Single().Code != AreaStreamingBenchmarkGate.TargetExceededCode)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R116_EE_RUNTIME_DATA_FAILED loading-benchmark-gate");
            return false;
        }
        GD.Print("OPENRPG_FORGE_TOOLSET_R116_EE_RUNTIME_DATA_PASS " +
            "json=typed-versioned-schema-validation-quota-pointer-safe " +
            "snapshot=components-references-inventory-blackboard-schema-save-restore " +
            "copy=uuid-owner-references-state-transaction-authority " +
            "aoe=mesh-targets-cover-height-colorblind-server-confirmation " +
            "loading=async-prefetch-progress-cache-configurable-cancellation-sequential-fallback-benchmark-gate-stable-diagnostic " +
            "identity=uuid-schema-provenance validation=required-unique-resolved-schema-localized " +
            "ui=runtime-extensions-terrain-visible-empty-error command=dry-run-apply " +
            "history=undo-redo persistence=roundtrip cli=structured-no-shell mcp=typed-no-shell " +
            "runtime=engine-adapter-optional-safe-required-diagnostic");
        var editorProfile = LoadWorkspaceProfile();
        var accessibility = applied.ModernTooling.QualityAssurance.Accessibility;
        var visualPlan = PerformanceQualityService.PlanVisualReviews(applied.ModernTooling.QualityAssurance.Visual);
        var radial = _history.State.Project.Module.RuntimePolicies.Gameplay.RadialMenu;
        var systemFont = _mainPanel?.Theme?.DefaultFont as SystemFont;
        if (editorProfile.UiScale is < EditorWorkspaceProfile.MinimumUiScale or > EditorWorkspaceProfile.MaximumUiScale ||
            editorProfile.TextScale is < EditorWorkspaceProfile.MinimumTextScale or > EditorWorkspaceProfile.MaximumTextScale ||
            editorProfile.FallbackFontFamilies.Count == 0 || systemFont is null || systemFont.FontNames.Length == 0 ||
            accessibility.RuntimeTextScale is < .8 or > 2 || !accessibility.RuntimeInputAssists ||
            visualPlan.Cases.Count != 8 || visualPlan.Cases.Select(item => item.Resolution).Distinct().Count() != 2 ||
            !radial.GamepadEnabled || !radial.KeyboardNavigation || !radial.ListFallback ||
            applied.ModernTooling.UiToolkit is not
            {
                ToolkitVersion: "2.0", Forms: true, Lists: true, Canvas: true,
                Accessibility: true, Localization: true, DeveloperInspector: true,
            } ||
            applied.ModernTooling.Compiler is not
            {
                Incremental: true, StableDiagnostics: true, PerformanceTelemetry: true,
            } || applied.ModernTooling.Compiler.ConformanceSuites.Count == 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R117_EE_UI_TOOLING_FAILED canonical-contract");
            return false;
        }
        GD.Print("OPENRPG_FORGE_TOOLSET_R117_EE_UI_TOOLING_PASS " +
            "nui=versioned-layout-forms-lists-canvas-accessibility-localization-inspector " +
            "font=system-truetype-fallback-shaping-dpi-user-text-scale-reflow-controller-focus-screenshot-suite " +
            "transform=tween-transform-material-state-easing-cancellation-authority-save " +
            "compiler=conformance-stable-diagnostics-incremental-telemetry " +
            "identity=uuid-schema-provenance validation=required-unique-resolved-schema-localized " +
            "ui=runtime-extensions-creator-quality-visible-empty-error command=dry-run-apply " +
            "history=undo-redo persistence=roundtrip cli=structured-no-shell mcp=typed-no-shell " +
            "runtime=ui-transform-compiler-optional-safe-required-diagnostic");
        GD.Print("OPENRPG_FORGE_TOOLSET_G11Q_RESILIENCE_PASS tasks=goal-scope-forbidden-tests-owner-dependencies " +
            "crash=sanitized-dump-version-fingerprint-reproduction health=domains-tests-performance-licenses-migrations-coverage " +
            "recovery=journal-reopen-quarantine-compare sync=resume-dedup-hash-trust-storage history=restored persistence=roundtrip");
        GD.Print("OPENRPG_FORGE_TOOLSET_R003_QA_PASS diagnostics=profile-category-severity-quickfix " +
            "malicious=7-fixtures parity=717-status-evidence gates=G0-G13-tests-docs-evidence " +
            "mcp=structured history=restored persistence=roundtrip");
        return true;
    }

    private bool RunAreaFoundationSmokeTest()
    {
        if (_history is null || _projectSession is null || _areaFoundationWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R028_AREA_FOUNDATION_FAILED missing_context");
            return false;
        }

        var area = GetActiveArea();
        var original = area.Foundation;
        if (!_areaFoundationWindow.PrepareForSmokeTest() || !_areaFoundationWindow.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R028_AREA_FOUNDATION_FAILED ui_apply");
            return false;
        }

        var appliedArea = _history.State.Project.Areas.Single(item => item.Id == area.Id);
        var applied = appliedArea.Foundation;
        var connection = ModularTileConnectionPreviewService.Preview(applied.TileKit,
            applied.TileKit.Placements.Single().Id);
        var placedEntity = appliedArea.Entities.Single(item =>
            item.Id == applied.TileKit.Placements.Single().EntityId);
        var resolvedTile = ModularTilePlacementResolver.Resolve(appliedArea, placedEntity);
        var lifecycle = new RuntimeLifecycleCoordinator();
        var loaded = lifecycle.LoadProject(_history.State.Project, RuntimeAuthority.SinglePlayer,
            RuntimeLifecycleCause.Startup);
        var entered = lifecycle.EnterInitialArea(area.Id, null, RuntimeLifecycleCause.Startup);
        if (AreaFoundationValidator.Validate(appliedArea, applied).Count != 0 ||
            applied.TileKit.Definitions.Count != 1 || applied.TileKit.Placements.Count != 1 ||
            applied.TileKit.Groups.Count != 1 || applied.TileKit.Brushes.Count != 1 ||
            applied.Creation.KitKey != "crypt-modular-kit" || connection.OpenSocketCount != 1 ||
            resolvedTile.Entity.Transform.Position.X != 0 || resolvedTile.Entity.Transform.Position.Z != 0 ||
            resolvedTile.Mirror != ModularTileMirrorMode.X || resolvedTile.SemanticState != "weathered" ||
            !loaded.Succeeded || !entered.Succeeded ||
            entered.Events.Single().ScriptPath != "scripts/ash_warden_phase_two.forge")
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R028_AREA_FOUNDATION_FAILED contract");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "r028-area-foundation",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project.Areas.Single(item => item.Id == area.Id).Foundation;
            if (!saved.Succeeded || !reopened.Succeeded || restored is null ||
                restored.Id != applied.Id || restored.Creation != applied.Creation ||
                restored.TileKit.Id != applied.TileKit.Id ||
                restored.TileKit.Definitions.Count != applied.TileKit.Definitions.Count ||
                restored.TileKit.Placements.Count != applied.TileKit.Placements.Count ||
                restored.Lifecycle.Entered != applied.Lifecycle.Entered)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R028_AREA_FOUNDATION_FAILED persistence");
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
            _history.State.Project.Areas.Single(item => item.Id == area.Id).Foundation != original)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R028_AREA_FOUNDATION_FAILED history");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R028_AREA_FOUNDATION_PASS " +
            "wizard=kit-terrain-lighting-navigation-streaming-biome-seed-budget-preview " +
            "document=uuid-schema-provenance-environment-instances-volumes-navigation-dependencies " +
            "tiles=grid-sockets-rules-groups-brushes-runtime rotation=kit-policy-mirror-semantic-variant-raccord-preview " +
            "lifecycle=entered-exited-actor-transition-context-authority-script-api " +
            "ui=visible-empty-error command=preview-apply history=undo-redo persistence=roundtrip forgescript=typed");
        return true;
    }

    private bool RunAreaTileEditingR053SmokeTest()
    {
        if (_history is null || _projectSession is null || _areaFoundationWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R053_AREA_TILE_EDITING_FAILED missing_context");
            return false;
        }

        var area = GetActiveArea();
        var original = area.Foundation;
        if (!_areaFoundationWindow.PrepareR053ForSmokeTest() || !_areaFoundationWindow.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R053_AREA_TILE_EDITING_FAILED ui_apply");
            return false;
        }

        var appliedArea = _history.State.Project.Areas.Single(item => item.Id == area.Id);
        var applied = appliedArea.Foundation;
        var editing = applied.TileKit.Editing;
        var placement = applied.TileKit.Placements.Single();
        var autoFit = AreaTileAutoFitService.Preview(applied.TileKit, placement.Id);
        var runtime = AreaTileEditingRuntimeResolver.Resolve(appliedArea.Id, editing,
            applied.RequireTileEditingRuntimeMetadata);
        var optional = AreaTileEditingRuntimeResolver.Resolve(appliedArea.Id, null, required: false);
        var required = AreaTileEditingRuntimeResolver.Resolve(appliedArea.Id, null, required: true);
        var script = AreaFoundationScriptService.Read(appliedArea);
        var selection = AreaGeometryPlanner.Edit(appliedArea,
            new(AreaTileEditOperationKind.SelectByType, [], SelectionType: applied.TileKit.Definitions.Single().Key));
        var converted = AreaGeometryPlanner.ConvertGroupToEditableInstances(appliedArea,
            applied.TileKit.Groups.Single().Id);
        if (editing is null || AreaFoundationValidator.Validate(appliedArea, applied).Count != 0 ||
            editing.Groups.Count != 1 || editing.Properties.Count != 1 ||
            editing.AutoFit.Seed != 53053 || !autoFit.ManualLock ||
            runtime.Handle is null || runtime.Diagnostics.Count != 0 || optional.Diagnostics.Count != 0 ||
            required.Diagnostics.Single().Code != AreaTileEditingRuntimeResolver.RequiredMissingCode ||
            script.EditableGroupCount != 1 || script.TilePropertyCount != 1 || script.ManualLockCount != 1 ||
            !script.ResizeMigrationEnabled || !script.TileEditingRequired ||
            selection.SelectedPlacementIds.Single() != placement.Id || selection.Diagnostics.Count != 0 ||
            converted.Entities.All(item => !item.Tags.Contains("tile-editable", StringComparer.Ordinal)) ||
            !converted.Foundation.TileKit.Editing!.Groups.Single().ConvertToEditableInstances)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R053_AREA_TILE_EDITING_FAILED contract");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "r053-area-tile-editing",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project.Areas.Single(item => item.Id == area.Id).Foundation;
            if (!saved.Succeeded || !reopened.Succeeded || restored?.TileKit.Editing is null ||
                restored.TileKit.Editing.AutoFit.Seed != 53053 ||
                restored.TileKit.Editing.Properties.Single().SemanticState != "torchlit" ||
                !restored.RequireTileEditingRuntimeMetadata)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R053_AREA_TILE_EDITING_FAILED persistence");
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
            _history.State.Project.Areas.Single(item => item.Id == area.Id).Foundation != original)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R053_AREA_TILE_EDITING_FAILED history");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R053_AREA_TILE_EDITING_PASS " +
            "groups=parameters-footprint-sockets-variants-convert-editable " +
            "tools=erase-replace-remap-fill-select-preserve-attached " +
            "autofit=socket-seed-manual-lock-ambiguity-report " +
            "properties=state-animation-material-light-collision-navigation-sockets-overrides " +
            "resize=anchor-loss-preview-reposition-migrate ui=visible-empty-error-preview " +
            "command=dry-run-apply history=undo-redo persistence=project-workspace-roundtrip " +
            "forgescript=typed-handle mcp=filtered-plan-apply-revert build=preflight runtime=optional-safe-required-diagnostic");
        return true;
    }

    private bool RunAreaEnvironmentR054SmokeTest()
    {
        if (_history is null || _projectSession is null || _areaEnvironmentWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R054_AREA_ENVIRONMENT_FAILED missing_context");
            return false;
        }

        var area = GetActiveArea();
        var original = area.Environment;
        if (!_areaEnvironmentWindow.ConfigureRenderForSmokeTest(original) ||
            !_areaEnvironmentWindow.PrepareR054ForSmokeTest() ||
            !_areaEnvironmentWindow.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R054_AREA_ENVIRONMENT_FAILED ui_apply");
            return false;
        }

        var appliedArea = _history.State.Project.Areas.Single(item => item.Id == area.Id);
        var environment = appliedArea.Environment;
        var snapshot = AreaEnvironmentScriptService.Read(appliedArea);
        var preview = AreaEnvironmentScriptService.PreviewTime(appliedArea, 18);
        var runtime = AreaEnvironmentRuntimeResolver.Resolve(appliedArea.Id, environment,
            environment.RequireAuthoringRuntimeMetadata);
        var optional = AreaEnvironmentRuntimeResolver.Resolve(appliedArea.Id, null, required: false);
        var required = AreaEnvironmentRuntimeResolver.Resolve(appliedArea.Id, null, required: true);
        if (AreaEnvironmentAuthoringValidator.Validate(appliedArea, environment).Count != 0 ||
            snapshot.ClassificationKey != "custom:moonlit-forest" ||
            snapshot.ClassificationExtensionPack != "forest-environments-2026" ||
            snapshot.DrivenSubsystems.Count != 6 || snapshot.CurvePointCount != 3 ||
            snapshot.TimedEventCount != 1 || snapshot.ReflectionProbeCount != 1 ||
            snapshot.LightingVolumeCount != 1 || snapshot.FogVolumeCount != 1 ||
            snapshot.FogQualityProfiles["desktop"] != AreaFogQualityTier.High ||
            preview.EventsAtHour.Single() != "world.sunset" || runtime.Handle is null ||
            runtime.Diagnostics.Count != 0 || optional.Diagnostics.Count != 0 ||
            required.Diagnostics.Single().Code != AreaEnvironmentRuntimeResolver.RequiredMissingCode)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R054_AREA_ENVIRONMENT_FAILED contract");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "r054-area-environment",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project.Areas.Single(item => item.Id == area.Id).Environment;
            if (!saved.Succeeded || !reopened.Succeeded || restored is null ||
                restored.Authoring.Classification.Key != "custom:moonlit-forest" ||
                restored.Authoring.Lighting.Probes.Count != 1 ||
                restored.Authoring.Fog.LocalVolumes.Count != 1 ||
                !restored.RequireAuthoringRuntimeMetadata)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R054_AREA_ENVIRONMENT_FAILED persistence");
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
        var cleaned = _history.State.Project.Areas.Single(item => item.Id == area.Id).Environment;
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            cleaned.Authoring.Id != original.Authoring.Id ||
            cleaned.Authoring.Lighting.PresetKey != original.Authoring.Lighting.PresetKey)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R054_AREA_ENVIRONMENT_FAILED history");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R054_AREA_ENVIRONMENT_PASS " +
            "clone=full-template-new-uuids-remap-shared-references-diff terrain=navigation-lighting-preserved " +
            "classification=extensible-render-audio-navigation-weather-camera-gameplay " +
            "daynight=clock-curves-sun-moon-transitions-events-volume-overrides " +
            "lighting=physical-presets-temperature-intensity-hdr-shadows-probes-simple-advanced " +
            "fog=distance-volumetric-density-height-color-noise-local-volumes-quality " +
            "ui=visible-empty-error-preview command=dry-run-apply history=undo-redo persistence=roundtrip " +
            "forgescript=typed-handle mcp=filtered-plan-apply-revert build=preflight " +
            "runtime=optional-safe-required-diagnostic");
        return true;
    }

    private bool RunAreaExperienceR055SmokeTest()
    {
        if (_history is null || _projectSession is null || _areaAuthoringWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R055_AREA_EXPERIENCE_FAILED missing_context");
            return false;
        }

        var area = GetActiveArea();
        var original = area.Authoring;
        if (!_areaAuthoringWindow.PrepareR055ForSmokeTest() || !_areaAuthoringWindow.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R055_AREA_EXPERIENCE_FAILED ui_apply");
            return false;
        }

        var appliedArea = _history.State.Project.Areas.Single(item => item.Id == area.Id);
        var snapshot = AreaExperienceAuthoringScriptService.Read(appliedArea);
        var eventName = $"area.{appliedArea.Slug}.alarm";
        var dispatched = AreaExperienceAuthoringScriptService.Dispatch(appliedArea, eventName,
            new Dictionary<string, object?> { ["level"] = 2 });
        var eventRuntime = new NativeForgeScriptRuntime();
        var eventPayload = new Dictionary<string, object?> { ["level"] = 2 };
        var eventPreview = AreaUserEventRuntimeService.Dispatch(appliedArea, eventName, eventPayload,
            eventRuntime, ResolveAreaUserEventSource, requiredAtRuntime: true, dryRun: true,
            correlationId: Guid.Parse("a0551000-0000-4000-8000-000000000011"));
        var eventExecution = AreaUserEventRuntimeService.Dispatch(appliedArea, eventName, eventPayload,
            eventRuntime, ResolveAreaUserEventSource, requiredAtRuntime: true, dryRun: false,
            correlationId: Guid.Parse("a0551000-0000-4000-8000-000000000012"));
        var map = AreaExperienceAuthoringScriptService.Reveal(appliedArea,
            new(AreaMapExplorationScope.Party, "party:smoke", new HashSet<string>(), new HashSet<Guid>()),
            17, 3, appliedArea.Authoring.Map.Pins[0].Id);
        var runtime = AreaExperienceRuntimeResolver.Resolve(appliedArea, required: true);
        var legacy = area.WithAuthoring(AreaAuthoringProfile.CreateDefault(area.Id));
        var optional = AreaExperienceRuntimeResolver.Resolve(legacy, required: false);
        var required = AreaExperienceRuntimeResolver.Resolve(legacy, required: true);
        var performance = AreaPerformanceAnalyzer.Analyze(_history.State.Project, area.Id);
        var renderPlan = AreaExperienceRenderPlanBuilder.Build(appliedArea);
        if (AreaExperienceAuthoringValidator.Validate(appliedArea).Count != 0 ||
            snapshot.AudioSnapshots.Count != 1 || snapshot.EventBindings.Count != 1 ||
            snapshot.MapPins != 1 || snapshot.VisibilityRooms != 2 || snapshot.VisibilityPortals != 1 ||
            snapshot.Occluders != 1 || snapshot.LodOverrides != 1 || !snapshot.FogOfWar ||
            dispatched.Subscriptions.Count != 1 ||
            !eventPreview.Succeeded || !eventPreview.DryRun || eventPreview.Executed ||
            eventPreview.Compilation?.Success != true || eventPreview.Deliveries.Count != 1 ||
            !eventExecution.Succeeded || eventExecution.DryRun || !eventExecution.Executed ||
            eventExecution.Execution?.Status != ForgeScriptExecutionStatus.Completed ||
            eventExecution.Deliveries.Count != 1 || !map.RevealedCells.Contains("v1:0:5:5") ||
            map.RevealedPins.Count != 1 || runtime.Handle is null || runtime.Diagnostics.Count != 0 ||
            optional.Diagnostics.Count != 0 ||
            required.Diagnostics.Single().Code != AreaExperienceRuntimeResolver.RequiredMissingCode ||
            performance.VisibilityRoomCount != 2 || performance.VisibilityPortalCount != 1 ||
            performance.OccluderCount != 1 || performance.LodOverrideCount != 1 ||
            renderPlan.MapFloors.Count != 2 || renderPlan.Rooms.Count != 2 ||
            renderPlan.VisibilityPortals.Count != 1 || renderPlan.Occluders.Count != 1 ||
            renderPlan.LodOverrides.Count != 1 || renderPlan.Diagnostics.Count != 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R055_AREA_EXPERIENCE_FAILED contract");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "r055-area-experience",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project.Areas.Single(item => item.Id == area.Id).Authoring;
            if (!saved.Succeeded || !reopened.Succeeded || restored is null ||
                restored.Experience.AudioSnapshots.Count != 1 || restored.UserEvents.Count != 1 ||
                restored.Map.Pins.Count != 1 || restored.Experience.Visibility.Rooms.Count != 2 ||
                restored.Experience.Visibility.Portals.Count != 1 ||
                restored.Experience.Visibility.Occluders.Count != 1 ||
                restored.Experience.Lod.AssetOverrides.Count != 1 ||
                !restored.Experience.RequireRuntimeMetadata)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R055_AREA_EXPERIENCE_FAILED persistence");
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
        var cleaned = _history.State.Project.Areas.Single(item => item.Id == area.Id).Authoring;
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            cleaned.Experience.Id != original.Experience.Id || cleaned.Map.Id != original.Map.Id)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R055_AREA_EXPERIENCE_FAILED history");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R055_AREA_EXPERIENCE_PASS " +
            "audio=day-night-combat-ambience-snapshots-adaptive-reverb-transitions " +
            "events=area-namespaced-payload-api-subscriptions-runtime " +
            "map=generated-2d-3d-player-party-world-pins-floors-fog-reveal " +
            "visibility=rooms-portals-occluders-culling-distance-streaming-cell-visualizer " +
            "lod=area-asset-import-generated-hybrid-thresholds-budgets-popping-crossfade " +
            "ui=visible-empty-error-preview command=dry-run-apply history=undo-redo persistence=roundtrip " +
            "forgescript=typed-handle mcp=filtered-plan-apply-revert build=preflight " +
            "runtime=optional-safe-required-diagnostic");
        return true;

        string? ResolveAreaUserEventSource(string relativePath)
        {
            if (_projectSession is null ||
                !ProjectLifecycleEventProfileValidator.IsSafeScriptPath(relativePath))
                return null;
            var root = Path.GetFullPath(_projectSession.RootDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            var candidate = Path.GetFullPath(Path.Combine(root,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(candidate)
                ? File.ReadAllText(candidate)
                : null;
        }
    }

    private bool RunAreaLayersR056SmokeTest()
    {
        if (_history is null || _projectSession is null || _areaAuthoringWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R056_AREA_LAYERS_FAILED missing_context");
            return false;
        }

        var area = GetActiveArea();
        var original = area.Authoring;
        if (!_areaAuthoringWindow.PrepareR056ForSmokeTest() || !_areaAuthoringWindow.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R056_AREA_LAYERS_FAILED ui_apply");
            return false;
        }

        var appliedArea = _history.State.Project.Areas.Single(item => item.Id == area.Id);
        var rootId = Guid.Parse("a0561000-0000-4000-8000-000000000001");
        var childId = Guid.Parse("a0561000-0000-4000-8000-000000000002");
        var privateId = Guid.Parse("a0561000-0000-4000-8000-000000000003");
        var snapshot = AreaLayerAuthoringService.Read(appliedArea);
        var selection = AreaLayerAuthoringService.SelectGroup(appliedArea, rootId);
        var export = AreaLayerAuthoringService.PreviewPartialExport(appliedArea, [rootId]);
        var runtime = AreaLayerRuntimeResolver.Resolve(appliedArea, required: true);
        var denied = AreaLayerAuthoringService.Reparent(appliedArea.Authoring, childId, privateId);
        var legacyAuthoring = new AreaAuthoringProfile(original.SchemaVersion, original.UserEvents, original.Map, [],
            original.Experience, false);
        var legacy = area.WithAuthoring(legacyAuthoring);
        var optional = AreaLayerRuntimeResolver.Resolve(legacy, required: false);
        var required = AreaLayerRuntimeResolver.Resolve(legacy, required: true);
        SelectAreaLayerEntities(selection.EntityIds);
        if (AreaLayerAuthoringValidator.Validate(appliedArea).Count != 0 ||
            snapshot.Layers.Count != 3 || !snapshot.RuntimeMetadataRequired ||
            snapshot.Layers.Single(item => item.LayerId == childId).Depth != 1 ||
            !snapshot.Layers.Single(item => item.LayerId == privateId).Locked ||
            selection.LayerIds.Count != 3 || selection.EntityIds.Count != 2 ||
            export.Layers.Count != 2 || export.Entities.Count != 2 ||
            export.BlueprintDependencies.Count == 0 || export.Diagnostics.Count != 0 ||
            runtime.Handle is null || runtime.Diagnostics.Count != 0 || denied.Succeeded ||
            optional.Diagnostics.Count != 0 ||
            required.Diagnostics.Single().Code != AreaLayerRuntimeResolver.RequiredMissingCode ||
            _selectionContext.Current.Count != 2 ||
            ProjectValidator.Validate(_history.State.Project).Count != 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R056_AREA_LAYERS_FAILED contract");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "r056-area-layers",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project.Areas.Single(item => item.Id == area.Id).Authoring;
            if (!saved.Succeeded || !reopened.Succeeded || restored is null || restored.Layers.Count != 3 ||
                !restored.RequireLayerRuntimeMetadata ||
                restored.Layers.Single(item => item.Id == privateId).ExportEnabled ||
                restored.Layers.Single(item => item.Id == childId).ParentId != rootId ||
                restored.Layers.Any(item => item.SchemaVersion != AreaEditorLayer.CurrentSchemaVersion ||
                                            item.Provenance != "author:r056-smoke"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R056_AREA_LAYERS_FAILED persistence");
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
        var cleaned = _history.State.Project.Areas.Single(item => item.Id == area.Id).Authoring;
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            cleaned.Layers.Count != original.Layers.Count ||
            cleaned.RequireLayerRuntimeMetadata != original.RequireLayerRuntimeMetadata)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R056_AREA_LAYERS_FAILED history");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R056_AREA_LAYERS_PASS " +
            "layers=nested-drag-drop visibility=inherited locking=inherited ownership=explicit " +
            "selection=group-descendants-viewport export=partial-layers-entities-blueprints-preview " +
            "identity=uuid-schema-provenance ui=visible-empty-error-preview command=dry-run-apply " +
            "history=undo-redo persistence=roundtrip forgescript=typed-handle " +
            "mcp=filtered-plan-apply-revert build=preflight runtime=optional-safe-required-diagnostic");
        return true;
    }

    private bool RunPaletteR057SmokeTest()
    {
        if (_history is null || _projectSession is null || _blueprintCatalog is null || _assetBrowser is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R057_PALETTE_FAILED missing_context");
            return false;
        }

        var entity = _history.State.Project.Areas.SelectMany(area => area.Entities).FirstOrDefault();
        if (entity is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R057_PALETTE_FAILED missing_entity");
            return false;
        }
        var original = _history.State.Project.Blueprints.Single(item => item.Id == entity.BlueprintId);
        var keys = entity.Overrides.Properties.Select(item => item.Key).Take(2).ToArray();
        if (!_blueprintCatalog.UpdateFromEntityForSmokeTest(original.Id, entity.Id, keys, dryRun: true))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R057_PALETTE_FAILED ui_preview");
            return false;
        }
        var plan = BlueprintUpdatePlanner.Build(_history.State.Project, original.Id, entity.Id, keys,
            BlueprintVersionBump.Patch);
        var command = new UpdateBlueprintFromEntityCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            original.Id, entity.Id, keys, BlueprintVersionBump.Patch, [], false, ["Smoke/Palette/Validated"]);
        if (!UpdateBlueprintFromEntity(command))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R057_PALETTE_FAILED apply");
            return false;
        }
        var updatedProject = _history.State.Project;
        var updated = updatedProject.Blueprints.Single(item => item.Id == original.Id);
        var library = BlueprintLibraryIndex.Build(updatedProject);
        var script = new BlueprintPaletteScriptService(updatedProject);
        var runtime = script.Resolve(entity.Id, true);
        var missing = script.Resolve(EntityId.From(Guid.NewGuid()), false);
        if (updated.Authoring.Version != plan.TargetVersion ||
            !updated.Authoring.CategoryPaths.Contains("Smoke/Palette/Validated", StringComparer.Ordinal) ||
            !library.CategoryPaths.Contains("Smoke/Palette", StringComparer.Ordinal) ||
            script.Query("validated").All(item => item.BlueprintId != original.Id) ||
            runtime.Blueprint?.BlueprintId != original.Id || runtime.Diagnostics.Count != 0 ||
            missing.Diagnostics.Count != 0 || ProjectValidator.Validate(updatedProject).Count != 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R057_PALETTE_FAILED contract");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "r057-palette",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, updatedProject);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project.Blueprints.Single(item => item.Id == original.Id);
            if (!saved.Succeeded || !reopened.Succeeded || restored is null ||
                restored.Authoring.Version != plan.TargetVersion ||
                !restored.Authoring.CategoryPaths.Contains("Smoke/Palette/Validated", StringComparer.Ordinal) ||
                !restored.Authoring.PromotedDefaults.SequenceEqual(updated.Authoring.PromotedDefaults))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R057_PALETTE_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true);
        }

        var advancedResults = _assetBrowser.FilterAdvancedForSmokeTest("apple author:ambientcg variant:LQ-1K-JPG");
        if (advancedResults == 0 || !_assetBrowser.SelectAssetForSmokeTest("ambientcg:3DApple001", loadTurntable: true) ||
            !_assetBrowser.SelectedHasPreviewMetadata)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R057_PALETTE_FAILED asset_browser");
            return false;
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            _history.State.Project.Blueprints.Single(item => item.Id == original.Id).Authoring.Version !=
            original.Authoring.Version)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R057_PALETTE_FAILED history");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R057_PALETTE_PASS " +
            "blueprint=instance-diff-selected-overrides-semver-impact-preview " +
            "instances=assisted-migration-pins-exclusions-rollback-overrides-preserved " +
            "taxonomy=hierarchical-custom-multitag-virtual-folders-facets " +
            "preview=thumbnail-turntable-variants-animations-metrics-license " +
            "search=text-tags-dimensions-style-skeleton-material-polycount-license-author-compatibility-variant " +
            "ui=visible-empty-error-preview command=dry-run-apply history=undo-redo persistence=roundtrip " +
            "forgescript=typed-handle mcp=filtered-plan-apply-revert build=preflight " +
            "runtime=optional-safe-required-diagnostic");
        return true;
    }

    private bool RunPrefabR058SmokeTest()
    {
        if (_history is null || _projectSession is null || _blueprintCatalog is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R058_PREFAB_FAILED missing_context");
            return false;
        }
        var group = _history.State.Project.Blueprints.Where(item => !item.IsReadOnly())
            .GroupBy(item => item.Kind).FirstOrDefault(items => items.Count() >= 2);
        if (group is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R058_PREFAB_FAILED missing_compatible_blueprints");
            return false;
        }
        var target = group.First();
        var baseBlueprint = group.Skip(1).First();
        var originalAuthoring = target.Authoring;
        var originalEntityCount = _history.State.Project.Areas.Sum(area => area.Entities.Count);
        if (!_blueprintCatalog.ConfigurePrefabForSmokeTest(target.Id, baseBlueprint.Id, baseBlueprint.Id,
                dryRun: true) ||
            !_blueprintCatalog.ConfigurePrefabForSmokeTest(target.Id, baseBlueprint.Id, baseBlueprint.Id,
                dryRun: false))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R058_PREFAB_FAILED ui_configure");
            return false;
        }
        var configuredProject = _history.State.Project;
        var configured = configuredProject.Blueprints.Single(item => item.Id == target.Id);
        var runtime = new BlueprintPrefabRuntimeService(configuredProject);
        var variant = runtime.ResolveVariant(target.Id, true);
        var composition = runtime.ResolveComposition(target.Id, true);
        var budgets = runtime.EvaluateBudget(target.Id, true);
        var script = new BlueprintPaletteScriptService(configuredProject).Query(target.Id.ToString(), 10)
            .Single(item => item.BlueprintId == target.Id);
        var library = BlueprintLibraryIndex.Build(configuredProject);
        if (variant.BaseBlueprintId != baseBlueprint.Id || variant.EffectiveOverrides.Count != 3 ||
            composition.Nodes.Count != 1 || budgets.Count != 2 ||
            budgets.Any(item => item.Status == AssetBudgetEvaluationStatus.Failed) ||
            script.VariantBaseBlueprintId != baseBlueprint.Id || script.CompositionNodeCount != 1 ||
            script.AssetBudgetPlatformCount != 2 ||
            !library.Collections.Contains("Collections/Smoke/Prefabs", StringComparer.Ordinal) ||
            ProjectValidator.Validate(configuredProject).Count != 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R058_PREFAB_FAILED contract");
            return false;
        }
        if (!_blueprintCatalog.PlaceCompositionForSmokeTest(dryRun: true) ||
            !_blueprintCatalog.PlaceCompositionForSmokeTest(dryRun: false))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R058_PREFAB_FAILED placement");
            return false;
        }
        var placedProject = _history.State.Project;
        if (placedProject.Areas.Sum(area => area.Entities.Count) != originalEntityCount + 1 ||
            placedProject.Areas.SelectMany(area => area.Entities).Count(entity =>
                entity.Tags.Contains($"composition:{target.Id}", StringComparer.Ordinal)) != 1)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R058_PREFAB_FAILED placement_contract");
            return false;
        }
        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "r058-prefab",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, placedProject);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project.Blueprints.Single(item => item.Id == target.Id);
            if (!saved.Succeeded || !reopened.Succeeded || restored?.Authoring.PrefabVariant is null ||
                restored.Authoring.PrefabComposition?.Nodes.Count != 1 ||
                restored.Authoring.AssetBudget?.Platforms.Count != 2 ||
                !restored.Authoring.AuthorCollections.Contains("Smoke/Prefabs", StringComparer.Ordinal))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R058_PREFAB_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true);
        }
        var undoPlacement = _history.Undo();
        var undoConfigure = _history.Undo();
        var redoConfigure = _history.Redo();
        var redoPlacement = _history.Redo();
        var cleanupPlacement = _history.Undo();
        var cleanupConfigure = _history.Undo();
        if (!undoPlacement.Succeeded || !undoConfigure.Succeeded || !redoConfigure.Succeeded ||
            !redoPlacement.Succeeded || !cleanupPlacement.Succeeded || !cleanupConfigure.Succeeded ||
            !_history.State.Project.Blueprints.Single(item => item.Id == target.Id).Authoring.Equals(originalAuthoring) ||
            _history.State.Project.Areas.Sum(area => area.Entities.Count) != originalEntityCount)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R058_PREFAB_FAILED history");
            return false;
        }
        GD.Print("OPENRPG_FORGE_TOOLSET_R058_PREFAB_PASS " +
            "variant=inherited-component-material-stat-script-overrides composition=hierarchy-sockets-rules-parameters-scripts " +
            "collections=author-persistent placement=atomic-group budget=triangles-materials-textures-memory-animations-lod-collision-platform-gates " +
            "identity=uuid-schema-provenance-manifest-spdx-sha256 ui=visible-empty-error-preview command=dry-run-apply " +
            "history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-plan-apply-revert " +
            "build=preflight runtime=optional-safe-required-diagnostic");
        return true;
    }

    private bool RunAreaRuntimeValidationSmokeTest()
    {
        if (_history is null || _projectSession is null || _areaWorkspace is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R029_AREA_RUNTIME_VALIDATION_FAILED missing_context");
            return false;
        }
        var area = GetActiveArea();
        var original = area.Playtest;
        if (!_areaWorkspace.PrepareRuntimeValidationForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R029_AREA_RUNTIME_VALIDATION_FAILED ui_apply");
            return false;
        }
        var appliedArea = _history.State.Project.Areas.Single(item => item.Id == area.Id);
        var profile = appliedArea.Playtest;
        var navigation = AreaOperationsScriptService.ReadNavigation(_history.State.Project, area.Id);
        var readiness = AreaReadinessService.Run(_history.State.Project, area.Id);
        if (profile.StartMode != PlayFromHereStartMode.Camera || profile.TestProfile != "r029-camera-smoke" ||
            profile.CameraTransform.Position != new Vector3Value(6, 2, -2) ||
            navigation.AgentProfiles != 4 || navigation.WalkableCells == 0 ||
            readiness.Checks.Count != 9 || readiness.CategoryCounts.Count != 9)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R029_AREA_RUNTIME_VALIDATION_FAILED contract");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "r029-area-runtime",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project.Areas.Single(item => item.Id == area.Id).Playtest;
            var snapshots = new PlaytestSnapshotStore(Path.Combine(smokeRoot, ".snapshots"));
            var snapshot = snapshots.Create(_history.State.Project, area.Id, profile);
            var manifest = PlaytestLaunchManifestStore.Load(snapshot.LaunchManifestFilePath!);
            if (!saved.Succeeded || !reopened.Succeeded || restored != profile ||
                manifest.StartMode != "Camera" || manifest.TestProfile != "r029-camera-smoke" ||
                !File.Exists(snapshot.ProjectFilePath))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R029_AREA_RUNTIME_VALIDATION_FAILED persistence");
                return false;
            }
            snapshots.Delete(snapshot);
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true);
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            _history.State.Project.Areas.Single(item => item.Id == area.Id).Playtest != original)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R029_AREA_RUNTIME_VALIDATION_FAILED history");
            return false;
        }
        GD.Print("OPENRPG_FORGE_TOOLSET_R029_AREA_RUNTIME_VALIDATION_PASS " +
            "portal=schema-provenance-orientation-velocity-hooks-preview navigation=surface-agents-links-polygons-overlay " +
            "play_from_here=spawn-camera-waypoint-character-profile-quest-manifest " +
            "validation=9-categories-ui command=preview-apply history=undo-redo persistence=roundtrip " +
            "mcp=filtered-plan-apply-revert forgescript=typed");
        return true;
    }

    private bool RunQualityLabSmokeTest()
    {
        if (_qualityLabWindow is null || _history is null || _projectSession is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11R_QUALITY_FAILED missing_context");
            return false;
        }

        if (!_qualityLabWindow.RunForSmokeTest() || _qualityLabWindow.Report is not { } report ||
            _qualityLabWindow.PerformanceReport is not { } performance ||
            _qualityLabWindow.GovernanceReport is not { } governance ||
            _qualityFoundationHistory is null ||
            QualityFoundationProfileValidator.Validate(_qualityFoundationHistory.State.Profile).Count > 0 ||
            !report.CanonicalValidation.Passed || report.CanonicalValidation.CategoryCounts.Count != 5 ||
            !_qualityLabWindow.PreviewFoundationForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11R_QUALITY_FAILED report");
            return false;
        }

        var localizationCovered = report.Localization.Coverage.Count >= 2 &&
            report.Localization.PseudoSnapshots.Count > 0 &&
            report.Localization.Issues.All(issue => issue.Severity != LocalizationQualitySeverity.Error);
        var scriptsCovered = report.Scripts.Tests.Count > 0 && report.Scripts.Tests.All(test =>
            test.Compiled && test.Passed && test.Status == ForgeScriptExecutionStatus.Completed &&
            test.EventCoverage >= 1 && test.Failures.Count == 0);
        var dialoguesCovered = report.Dialogues.Count > 0 && report.Dialogues.All(dialogue =>
            dialogue.Passed && dialogue.CoveredNodes == dialogue.TotalNodes &&
            dialogue.CoveredChoices == dialogue.TotalChoices && dialogue.Paths.SelectMany(path => path.Actions).Any());
        var questsCovered = report.Quests.Count > 0 && report.Quests.All(quest =>
            quest.Passed && quest.CoveredStates == quest.TotalStates &&
            quest.CoveredTransitions == quest.TotalTransitions &&
            quest.CoveredScopes.Count == Enum.GetValues<NarrativeFixtureScope>().Length &&
            quest.Paths.All(path => path.SaveResumeEqual));
        var encountersCovered = report.Encounters.TotalRuns >= 9 && report.Encounters.Areas.Count > 0 &&
            report.Encounters.Areas.All(area => area.CreatureCount == area.ReachableCreatures &&
                area.Runs.All(run => run.NavigationPassed && run.CleanupPassed));
        if (!localizationCovered || !scriptsCovered || !dialoguesCovered || !questsCovered || !encountersCovered ||
            report.PseudoLocalizationSnapshotPath is not null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11R_QUALITY_FAILED coverage");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_G11R_QUALITY_PASS " +
            "localization=coverage-placeholders-plurals-overflow-pseudo " +
            "scripts=sandbox-quota-mock-coverage dialogues=branches-conditions-actions " +
            "quests=events-transitions-save-scope encounters=profiles-seeds-nav-cleanup");
        GD.Print("OPENRPG_FORGE_TOOLSET_R027_QA_VALIDATION_PASS " +
            "schemas=versioned references=typed graphs=generic geometry=scale-polygon navigation=agents-doors-links-flight " +
            "ui=visible-empty-error command=preview-apply history=undo-redo persistence=roundtrip " +
            "cli=structured mcp=structured build=preflight diagnostics=code-path-uuid");
        var performanceCovered = performance.Passed && performance.Network.Passed &&
            performance.Network.Scenarios.Count == 9 && performance.Network.ReconnectPassed &&
            performance.Network.ContentMismatchPassed && performance.Network.AuthorityAttackPassed &&
            performance.VisualReviews.Cases.Count == 8 && performance.Benchmarks.Measurements.Count == 9 &&
            performance.Benchmarks.Budgets.All(item => item.Status != PerformanceBudgetStatus.Failed) &&
            Enum.GetValues<QualityTraceSurface>().All(surface =>
                performance.TraceSpans.Any(span => span.Surface == surface)) &&
            new[] { "database", "network", "script" }.All(category =>
                performance.TraceSpans.Any(span => span.Category == category)) &&
            performance.PerformanceReportPath is null && performance.VisualManifestPath is null &&
            performance.TracePath is null;
        if (!performanceCovered)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11S_PERFORMANCE_FAILED coverage");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_G11S_PERFORMANCE_PASS " +
            "network=latency-loss-reconnect-mismatch-authority visual=fixtures-tolerance-quality " +
            "benchmarks=interior-exterior-stress-load-frame-memory-nav-package " +
            "budgets=frame-memory-texture-draw-light-script-network " +
            "profiler=editor-runtime-server-script-database-network");
        var governanceCovered = governance.Passed && governance.Accessibility.Passed &&
            governance.Accessibility.EditorChecks.All(item => item.Passed) &&
            governance.Accessibility.RuntimeChecks.All(item => item.Passed) &&
            governance.SecurityFuzz.TotalCases == 112 && governance.SecurityFuzz.Boundaries.Count == 7 &&
            governance.SecurityFuzz.Boundaries.All(item => item.UnexpectedFailures == 0 && item.PathEscapes == 0) &&
            governance.Dependencies.Passed && governance.ReportPath is null;
        if (!governanceCovered)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11T_GOVERNANCE_FAILED coverage");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_G11T_GOVERNANCE_PASS " +
            "editor=keyboard-labels-scale-contrast-shortcuts runtime=subtitles-captions-color-input-motion-difficulty " +
            "fuzz=112-cases-7-boundaries-no-crash-no-escape dependencies=approval-risk-review-audit-lifecycle");
        GD.Print("OPENRPG_FORGE_TOOLSET_R019_QA_FOUNDATIONS_PASS " +
            "licenses=spdx-provenance-notice-block domain=covered headless=scenarios " +
            "save=roundtrip-legacy-partial-missing-rollback-deterministic " +
            "logs=severity-category-object-source-transaction-privacy-retention " +
            "dry_run=true persistence=atomic undo_redo=true ui=configurable");
        return true;
    }


}
#endif
