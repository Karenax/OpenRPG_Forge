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
    private void RunWorkspaceSmokeTest()
    {
        EnsureForgeWindowTitle();
        var editorWindow = EditorInterface.Singleton.GetBaseControl().GetWindow();
        if (!string.Equals(editorWindow.Title, ForgeWindowTitle, StringComparison.Ordinal))
        {
            GD.PrintErr($"OPENRPG_FORGE_WINDOW_TITLE_FAILED actual={editorWindow.Title}");
            return;
        }

        GD.Print($"OPENRPG_FORGE_WINDOW_TITLE_PASS title={editorWindow.Title}");
        ReloadProject();
        if (_projectPreview?.EntityCount != 40 || _history is null || _newModuleButton is null ||
            _blueprintPalette is null || _positionX is null || _positionZ is null ||
            _spawnPicker is null || _spawnEntityIds.Count != 1)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_PREVIEW_REBUILD_FAILED");
            return;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_PREVIEW_REBUILD_PASS entities=40");
        if (!RunCreatorShellSmokeTest())
        {
            return;
        }

        if (!RunBlueprintCatalogSmokeTest())
        {
            return;
        }

        if (!RunDirectManipulationSmokeTest())
        {
            return;
        }

        if (!RunAreaWorkflowSmokeTest())
        {
            return;
        }

        if (!RunSemanticClipboardSmokeTest())
        {
            return;
        }

        if (!RunGlobalSearchAndRenameSmokeTest())
        {
            return;
        }

        if (!RunR034ContentOperationsSmokeTest())
        {
            return;
        }

        if (!RunModuleConfigurationSmokeTest())
        {
            return;
        }

        if (!RunPaletteProvenanceSmokeTest())
        {
            return;
        }

        if (!RunEditorCameraNavigationSmokeTest())
        {
            return;
        }

        if (!RunBuildPipelineSmokeTest())
        {
            return;
        }

        if (!RunR035BuildDiagnosticsSmokeTest())
        {
            return;
        }

        if (!RunR036EeAcceptanceSmokeTest())
        {
            return;
        }

        if (!RunDistributionProfilesSmokeTest())
        {
            return;
        }

        if (!RunContentDeliveryP2SmokeTest())
        {
            return;
        }

        if (!RunContentWorkflowSmokeTest())
        {
            return;
        }

        if (!RunContentPackageSmokeTest())
        {
            return;
        }

        if (!RunContentAuthoringSmokeTest())
        {
            return;
        }

        if (!RunCreatorProductivitySmokeTest())
        {
            return;
        }

        if (!RunR059ShellProductivitySmokeTest())
        {
            return;
        }

        if (!RunR060RecoverySmokeTest())
        {
            return;
        }

        if (!RunR061AudioAcceptanceSmokeTest())
        {
            return;
        }

        if (!RunR062AudioOrchestrationAcceptanceSmokeTest())
        {
            return;
        }

        if (!RunR063AudioCaptionAcceptanceSmokeTest())
        {
            return;
        }

        if (!RunScalableEditorExperienceSmokeTest())
        {
            return;
        }

        if (!RunPaletteProductionP2SmokeTest())
        {
            return;
        }

        if (!RunReferenceRenderingSmokeTest())
        {
            return;
        }

        if (!RunR114EeRenderingSmokeTest())
        {
            return;
        }

        if (!RunR115EePerformanceSmokeTest())
        {
            return;
        }

        if (!RunClimateWaterReverbSmokeTest())
        {
            return;
        }

        if (!RunTerrainWorldSmokeTest())
        {
            return;
        }

        if (!RunAreaProductionP2SmokeTest())
        {
            return;
        }

        if (!RunRuntimePolicySmokeTest())
        {
            return;
        }

        if (!RunCinematicSmokeTest())
        {
            return;
        }

        if (!RunGameplayControlsSmokeTest())
        {
            return;
        }

        if (!RunRulesProfilesSmokeTest())
        {
            return;
        }

        if (!RunForgeScriptFoundationSmokeTest())
        {
            return;
        }

        if (!RunForgeScriptWorldApiSmokeTest())
        {
            return;
        }

        if (!RunForgeScriptSafetyPolicySmokeTest())
        {
            return;
        }

        if (!RunForgeScriptModulePermissionsSmokeTest())
        {
            return;
        }

        if (!RunForgeScriptP1GameplaySmokeTest())
        {
            return;
        }

        if (!RunForgeScriptP1WorldApiSmokeTest())
        {
            return;
        }

        if (!RunForgeScriptP1RuntimeToolsSmokeTest())
        {
            return;
        }

        if (!RunForgeScriptP1QualitySmokeTest())
        {
            return;
        }

        if (!RunForgeScriptP1InteropSmokeTest())
        {
            return;
        }

        if (!RunNetworkFoundationP1SmokeTest())
        {
            return;
        }

        if (!RunNetworkOperationsP1SmokeTest())
        {
            return;
        }

        if (!RunNetworkAssuranceP1SmokeTest())
        {
            return;
        }

        if (!RunNetworkPlayerExperienceP2SmokeTest())
        {
            return;
        }

        if (!RunNetworkWorldAdministrationP2SmokeTest())
        {
            return;
        }

        if (!RunNetworkPersistenceObservabilityP2SmokeTest())
        {
            return;
        }

        if (!RunNetworkResilienceCapacityP2SmokeTest())
        {
            return;
        }

        if (!RunLiveDirectorAccessP1SmokeTest())
        {
            return;
        }

        if (!RunLiveDirectorWorkspaceP2SmokeTest())
        {
            return;
        }

        if (!RunLiveDirectorActionP2SmokeTest())
        {
            return;
        }

        if (!RunLiveDirectorAuthorityP2SmokeTest())
        {
            return;
        }

        if (!RunLiveDirectorGroupP2SmokeTest())
        {
            return;
        }
        if (!RunLiveDirectorOperationsP2SmokeTest()) return;
        if (!RunLiveDirectorNarrativeP2SmokeTest()) return;

        if (!RunBuildCollaborationP1SmokeTest())
        {
            return;
        }

        if (!RunContentAcceptanceP1SmokeTest())
        {
            return;
        }

        if (!RunContentRoundTripP1SmokeTest())
        {
            return;
        }

        if (!RunContentLifecycleP1SmokeTest())
        {
            return;
        }

        if (!RunAreaExperienceSmokeTest())
        {
            return;
        }

        if (!RunAudioEmitterSmokeTest())
        {
            return;
        }

        if (!RunRuntimeExtensionsSmokeTest())
        {
            return;
        }

        if (!RunQualityLabSmokeTest())
        {
            return;
        }

        if (!RunAreaFoundationSmokeTest())
        {
            return;
        }

        if (!RunAreaTileEditingR053SmokeTest())
        {
            return;
        }

        if (!RunAreaEnvironmentR054SmokeTest())
        {
            return;
        }

        if (!RunAreaExperienceR055SmokeTest())
        {
            return;
        }

        if (!RunAreaLayersR056SmokeTest())
        {
            return;
        }

        if (!RunPaletteR057SmokeTest())
        {
            return;
        }

        if (!RunPrefabR058SmokeTest())
        {
            return;
        }

        if (!RunAreaRuntimeValidationSmokeTest())
        {
            return;
        }

        if (!RunNavigationAndGeometrySmokeTest())
        {
            return;
        }

        if (!RunActionableDiagnosticsSmokeTest())
        {
            return;
        }

        if (!RunAssetContentBrowserSmokeTest())
        {
            return;
        }

        if (!RunGovernedAssetPreviewSmokeTest())
        {
            return;
        }

        if (!RunLocalizationWorkspaceSmokeTest())
        {
            return;
        }

        if (!RunAssistantFoundationSmokeTest())
        {
            return;
        }

        if (!RunLoreIndexSmokeTest())
        {
            return;
        }

        if (!RunAssistedAreaPlanSmokeTest())
        {
            return;
        }

        if (!RunAssistedAreaBuildSmokeTest())
        {
            return;
        }

        if (!RunAssistedDialogueSmokeTest())
        {
            return;
        }

        if (!RunAssistedQuestSmokeTest())
        {
            return;
        }

        if (!RunAssistedForgeScriptSmokeTest())
        {
            return;
        }

        if (!RunAssistedBossSmokeTest())
        {
            return;
        }

        if (!RunAssistedAbilitySmokeTest())
        {
            return;
        }

        if (!RunAssistedBlenderCreationSmokeTest())
        {
            return;
        }

        if (!RunAssistedModel3DGenerationSmokeTest())
        {
            return;
        }

        if (!RunExternalWorkerSecuritySmokeTest())
        {
            return;
        }

        if (!RunEditorKeymapSmokeTest())
        {
            return;
        }

        if (!RunCreatureFoundationSmokeTest())
        {
            return;
        }

        if (!RunCreatureAuthoringCompositionSmokeTest())
        {
            return;
        }

        if (!RunCreatureRulesCompositionSmokeTest())
        {
            return;
        }

        if (!RunCreatureInteractionCompositionSmokeTest())
        {
            return;
        }

        if (!RunCreatureEventAiCompositionSmokeTest())
        {
            return;
        }

        if (!RunCreaturePresentationSmokeTest())
        {
            return;
        }

        if (!RunCreatureProgressionSmokeTest())
        {
            return;
        }

        if (!RunCreatureLootSmokeTest())
        {
            return;
        }

        if (!RunCreatureRoleBehaviorSmokeTest())
        {
            return;
        }

        if (!RunCreatureEventSmokeTest())
        {
            return;
        }

        if (!RunAdvancedFactionSmokeTest())
        {
            return;
        }

        if (!RunDialoguePresentationSmokeTest())
        {
            return;
        }

        if (!RunQuestJournalSmokeTest())
        {
            return;
        }

        if (!RunQuestTransitionSmokeTest())
        {
            return;
        }

        if (!RunQuestAuthoringSmokeTest())
        {
            return;
        }

        if (!RunQuestExperienceSmokeTest())
        {
            return;
        }

        if (!RunQuestQualitySmokeTest())
        {
            return;
        }

        if (!RunQuestOperationsSmokeTest())
        {
            return;
        }

        if (!RunItemFoundationSmokeTest())
        {
            return;
        }

        if (!RunItemGameplaySmokeTest())
        {
            return;
        }

        if (!RunItemOperationsSmokeTest())
        {
            return;
        }

        if (!RunItemAdvancedSmokeTest())
        {
            return;
        }

        if (!RunWorldFeatureStudioSmokeTest())
        {
            return;
        }

        if (!RunPostLaunchStudioSmokeTest())
        {
            return;
        }

        if (!RunScaleAuthoringSmokeTest())
        {
            return;
        }

        if (!RunRpgKitAuthoringSmokeTest())
        {
            return;
        }

        if (!RunNewModuleSmokeTest())
        {
            return;
        }

        if (!RunProjectOperationsSmokeTest())
        {
            return;
        }

        var placeable = _history.State.Project.Blueprints.First(blueprint => blueprint.Kind == BlueprintKind.Placeable);
        _blueprintPalette.Selected = _paletteBlueprintIds.FindIndex(id => id == placeable.Id);
        _positionX.Value = 7;
        _positionZ.Value = 24;
        var placedEntityId = PlaceSelectedBlueprint();
        if (placedEntityId is null || _projectPreview.EntityCount != 41)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_T23_PLACE_FAILED");
            return;
        }

        MoveSelectedPositiveX();
        RotateSelected();
        var transformed = _history.State.Project.Areas.First().Entities.Single(entity => entity.Id == placedEntityId);
        if (transformed.Transform.Position.X != 8 || transformed.Transform.Rotation == QuaternionValue.Identity)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_T23_TRANSFORM_FAILED");
            return;
        }

        var smokeOverrides = new EntityPropertyOverrides(
            EntityPropertyOverrides.CurrentSchemaVersion,
            [
                new EntityPropertyOverride("displayName", EntityPropertyValueKind.String, "Pilier du smoke test"),
                new EntityPropertyOverride("locked", EntityPropertyValueKind.Boolean, "true"),
                new EntityPropertyOverride("hitPoints", EntityPropertyValueKind.Integer, "40"),
            ]);
        if (!ApplyEntityOverrides(smokeOverrides))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_T13_OVERRIDES_FAILED command");
            return;
        }

        var overridden = _history.State.Project.Areas.First().Entities.Single(entity => entity.Id == placedEntityId);
        if (overridden.Overrides.SchemaVersion != EntityPropertyOverrides.CurrentSchemaVersion ||
            overridden.Overrides.Properties.Count != 3 ||
            overridden.Overrides.Properties.Single(property => property.Key == "locked").Value != "true")
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_T13_OVERRIDES_FAILED invalid_state");
            return;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_T13_OVERRIDES_PASS schema=1 properties=3");

        Undo();
        Redo();
        if (!_history.CanUndo)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_T23_HISTORY_FAILED");
            return;
        }

        DeleteSelected();
        if (_projectPreview.EntityCount != 40)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_T23_DELETE_FAILED");
            return;
        }

        if (!RunDialogueSmokeTest())
        {
            return;
        }

        if (!RunQuestSmokeTest())
        {
            return;
        }

        SaveProject();
        GD.Print($"OPENRPG_FORGE_TOOLSET_T23_SMOKE_PASS entities={_projectPreview.EntityCount} revision={_history.State.Revision}");
        StartPlaytest(headless: true, workspaceSmokeTest: true);
    }

    private bool RunRpgKitAuthoringSmokeTest()
    {
        if (_history is null || _rpgKitAuthoringWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G8_AUTHOR_UI_FAILED missing_workspace");
            return false;
        }

        var blueprintId = BlueprintId.From(Guid.Parse("8a810000-0000-4000-8000-000000000001"));
        var revision = _history.State.Revision;
        var preview = PreviewOrApplyRpgBlueprint(blueprintId, "sentinelle-prismatique-smoke",
            "Sentinelle prismatique smoke", BlueprintKind.Creature, dryRun: true);
        if (!preview.Succeeded || preview.Audit.Applied || _history.State.Revision != revision ||
            _history.State.Project.Blueprints.Any(item => item.Id == blueprintId))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G8_AUTHOR_UI_FAILED preview");
            return false;
        }

        var applied = PreviewOrApplyRpgBlueprint(blueprintId, "sentinelle-prismatique-smoke",
            "Sentinelle prismatique smoke", BlueprintKind.Creature, dryRun: false);
        if (!applied.Succeeded || !applied.Audit.Applied ||
            !_history.State.Project.Blueprints.Any(item => item.Id == blueprintId))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G8_AUTHOR_UI_FAILED apply");
            return false;
        }

        Undo();
        if (_history.State.Project.Blueprints.Any(item => item.Id == blueprintId))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G8_AUTHOR_UI_FAILED undo");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_G8_AUTHOR_UI_PASS domains=9 json=none preview=dry-run apply=command undo=restored diagnostics=structured");
        return true;
    }

    private bool RunCreatorShellSmokeTest()
    {
        if (_creatorShell is null || _history is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G702_SHELL_FAILED missing_shell");
            return false;
        }

        var entity = _history.State.Project.Areas.First().Entities.First();
        if (_creatorShell.WorkspaceCount != 9 || _creatorShell.AdvancedToolCount != 3 ||
            _creatorShell.ExplorerNodeCount <= _history.State.Project.Areas.Sum(area => area.Entities.Count) ||
            !_creatorShell.SelectNodeForSmokeTest($"entity:{entity.Id}") ||
            _selectionContext.Current.Count != 1 ||
            _selectionContext.Current[0].Kind != CreatorSelectionKind.Entity ||
            _selectionContext.Current[0].Id != entity.Id.ToString())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G702_SHELL_FAILED explorer_selection");
            return false;
        }

        var target = _selectionContext.Current[0];
        var diagnostic = CreatorDiagnostic.Create(
            CreatorDiagnosticSeverity.Error,
            "creator.smoke.clickable",
            "Diagnostic synthétique du smoke test Creator.",
            target.DocumentPath,
            "smoke:g7-02",
            target);
        _creatorShell.SetDiagnostics([diagnostic]);
        if (_creatorShell.DiagnosticCount != 1 ||
            !_creatorShell.SelectDiagnosticForSmokeTest(0) ||
            _selectionContext.Current.Count != 1 ||
            _selectionContext.Current[0].Id != entity.Id.ToString())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G702_SHELL_FAILED diagnostic_navigation");
            return false;
        }

        RefreshCreatorShell();
        var originalProfile = LoadWorkspaceProfile();
        var projectSession = _projectSession!;
        var projectPreview = _projectPreview!;
        string? shellFailure = null;
        try
        {
            var project = _history.State.Project;
            var roots = Enum.GetValues<EditorExplorerGrouping>()
                .Select(grouping => CreatorProjectExplorer.Build(project, grouping))
                .ToArray();
            var blueprint = project.Blueprints.First();
            var paletteFilter = new EditorPaletteFilter(
                [blueprint.Kind], [blueprint.Authoring.Kit], blueprint.Authoring.Styles,
                [blueprint.Provenance.LicenseSpdx], blueprint.Authoring.Compatibility,
                blueprint.Authoring.Tags, favoritesOnly: true);
            var shell = new EditorShellConfiguration(
                Guid.Parse("32000000-0000-4000-8000-000000000001"), 1, "toolset:r032-smoke",
                new EditorShellLayout(Guid.Parse("32000000-0000-4000-8000-000000000002"), 1,
                    "toolset:r032-smoke", 272, 336, 176, assistantVisible: false),
                EditorExplorerGrouping.Tag, EditorViewportMode.Navigation, paletteFilter);
            var favoriteProfile = originalProfile.PersonalFavoriteBlueprintIds.Contains(blueprint.Id)
                ? originalProfile
                : EditorWorkspaceProfilePlanner.TogglePersonalFavorite(originalProfile, blueprint.Id);
            var candidate = EditorWorkspaceProfilePlanner.ConfigureShell(favoriteProfile, shell);
            var commands = new EditorWorkspaceProfileCommandHistory(originalProfile);
            var command = new UpdateEditorWorkspaceProfileCommand(
                Guid.Parse("32000000-0000-4000-8000-000000000003"), 1, "toolset:r032-smoke", candidate);
            var preview = commands.Execute(command with { DryRun = true });
            var applied = commands.Execute(command);
            var undo = commands.Undo();
            var redo = commands.Redo();
            SaveWorkspaceProfile(commands.Current);
            var persisted = EditorWorkspaceProfileStore.TryLoad(projectSession.RootDirectory);
            var paletteItems = BlueprintPaletteCatalog.Build(project, commands.Current)
                .Filter(BlueprintPaletteView.All, contextFilter: paletteFilter);
            foreach (var mode in Enum.GetValues<EditorViewportMode>())
            {
                projectPreview.SetEditorMode(mode);
                if (projectPreview.EditorMode != mode) shellFailure = $"viewport_{mode}";
            }

            if (roots.Length != 6 || roots.Any(root => root.Children.Count == 0) ||
                !preview.Succeeded || preview.Applied || !applied.Applied || !undo.Succeeded || !redo.Succeeded ||
                persisted?.Shell.ExplorerGrouping != EditorExplorerGrouping.Tag ||
                persisted.Shell.ViewportMode != EditorViewportMode.Navigation ||
                persisted.Shell.Layout.ExplorerWidth != 272 || paletteItems.Count != 1)
            {
                shellFailure ??= "configuration";
            }
        }
        catch (Exception exception)
        {
            shellFailure = $"exception_{exception.GetType().Name}";
        }
        finally
        {
            SaveWorkspaceProfile(originalProfile);
            RefreshCreatorShell();
        }
        if (shellFailure is not null)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_R032_SHELL_FOUNDATION_FAILED {shellFailure}");
            return false;
        }
        var beginnerProfile = EditorWorkspaceProfile.CreateDefault(EditorRole.Beginner,
            originalProfile.ActiveAreaId ?? _history.State.Project.Areas[0].Id);
        _creatorShell.ApplyWorkspaceProfile(beginnerProfile);
        var beginnerWorkspaceCount = _creatorShell.VisibleWorkspaceCount;
        _creatorShell.ApplyWorkspaceProfile(originalProfile);
        if (beginnerWorkspaceCount != 4)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_G702_SHELL_FAILED beginner_workspaces={beginnerWorkspaceCount}");
            return false;
        }
        if (_toolWindowManager is null || !_toolWindowManager.StartupWasClear ||
            !_toolWindowManager.HasSingleVisibleTool || _toolWindowManager.RegisteredCount < 10 ||
            !CreatorWindowLayoutPolicy.IsResponsive(_mainPanel!, out var responsiveWindowCount) ||
            _creatorShell.TechnicalDetailsVisible)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G702_SHELL_FAILED responsive_layout");
            return false;
        }
        GD.Print($"OPENRPG_FORGE_TOOLSET_G702_SHELL_PASS workspaces=9 beginner=4 advanced=3 explorer={_creatorShell.ExplorerNodeCount} selection=shared diagnostics=clickable responsive_windows={responsiveWindowCount} tool_windows=single-active startup=clear technical_details=collapsed");
        GD.Print("OPENRPG_FORGE_TOOLSET_G9_NETWORK_UI_PASS workspace=network navigation=advanced-tools");
        GD.Print("OPENRPG_FORGE_TOOLSET_G10_LIVE_DIRECTOR_UI_PASS workspace=live-director navigation=advanced-tools");
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_R032_SHELL_FOUNDATION_PASS " +
            "panels=explorer-viewport-inspector-palette-diagnostics-assistant layout=persisted " +
            "explorer=type-folder-area-tag-dependency-validation viewport=editing-simulation-lighting-navigation-collisions-performance " +
            "palette=type-kit-style-license-compatibility-tags-favorites origins=installed-project-generated-favorites-temporary " +
            "command=dry-run-apply history=undo-redo persistence=roundtrip");
        return true;
    }

    private bool RunBlueprintCatalogSmokeTest()
    {
        if (_blueprintCatalog is null || _history is null || _entityPicker is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G703_BLUEPRINT_FAILED missing_catalog");
            return false;
        }

        var initialBlueprintCount = _history.State.Project.Blueprints.Count;
        var entity = _history.State.Project.Areas.First().Entities.First();
        var entityIndex = _entityIds.FindIndex(id => id == entity.Id);
        if (entityIndex < 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G703_BLUEPRINT_FAILED missing_entity");
            return false;
        }

        _entityPicker.Selected = entityIndex;
        PublishSelectedEntity();
        var originalBlueprintId = entity.BlueprintId;
        var derivedBlueprintId = BlueprintId.From(Guid.Parse("f1000000-0000-4000-8000-000000000001"));
        if (!CreateBlueprintFromSelectedEntity(
                derivedBlueprintId,
                "smoke-derived-pillar",
                "Pilier dérivé smoke"))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G703_BLUEPRINT_FAILED command");
            return false;
        }

        var derived = _history.State.Project.Blueprints.Single(candidate => candidate.Id == derivedBlueprintId);
        var retargeted = _history.State.Project.Areas.First().Entities.Single(candidate => candidate.Id == entity.Id);
        var references = BlueprintReferenceIndex.Build(_history.State.Project).Find(derivedBlueprintId);
        if (_history.State.Project.Blueprints.Count != initialBlueprintCount + 1 ||
            retargeted.BlueprintId != derivedBlueprintId ||
            retargeted.Overrides != entity.Overrides ||
            retargeted.BlueprintVersionRequirement != $"={derived.Authoring.Version}" ||
            !derived.Authoring.ManifestPath.StartsWith("content/blueprints/", StringComparison.Ordinal) ||
            references.Count != 1 ||
            !_blueprintCatalog.SelectBlueprintForSmokeTest(derivedBlueprintId) ||
            _blueprintCatalog.VisibleBlueprintCount != initialBlueprintCount + 1 ||
            _blueprintCatalog.ReferenceCount != 1 ||
            _selectionContext.Current.Count != 1 ||
            _selectionContext.Current[0].Kind != CreatorSelectionKind.Blueprint ||
            _selectionContext.Current[0].Id != derivedBlueprintId.ToString())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G703_BLUEPRINT_FAILED invalid_state");
            return false;
        }

        var undone = _history.Undo();
        if (!undone.Succeeded ||
            _history.State.Project.Blueprints.Any(candidate => candidate.Id == derivedBlueprintId) ||
            _history.State.Project.Areas.First().Entities.Single(candidate => candidate.Id == entity.Id).BlueprintId != originalBlueprintId)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G703_BLUEPRINT_FAILED undo");
            return false;
        }

        var redone = _history.Redo();
        if (!redone.Succeeded ||
            _history.State.Project.Areas.First().Entities.Single(candidate => candidate.Id == entity.Id).BlueprintId != derivedBlueprintId)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G703_BLUEPRINT_FAILED redo");
            return false;
        }

        var restored = _history.Undo();
        if (!restored.Succeeded ||
            _history.State.Project.Blueprints.Count != initialBlueprintCount ||
            _history.State.Project.Areas.First().Entities.Single(candidate => candidate.Id == entity.Id).BlueprintId != originalBlueprintId)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G703_BLUEPRINT_FAILED cleanup");
            return false;
        }

        RebuildPreview();
        RefreshEditorControls(entity.Id);
        GD.Print($"OPENRPG_FORGE_TOOLSET_G703_BLUEPRINT_PASS catalog={initialBlueprintCount} derived=1 references=1 undo=restored");
        return true;
    }

    private bool RunDirectManipulationSmokeTest()
    {
        if (_projectPreview is null || _history is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G704_DIRECT_FAILED missing_preview");
            return false;
        }

        var project = _history.State.Project;
        var area = project.Areas.First();
        var placeable = project.Blueprints.First(blueprint => blueprint.Kind == BlueprintKind.Placeable);
        var initialEntityCount = area.Entities.Count;
        var initialRevision = _history.State.Revision;
        var initialJournalCount = _history.Journal.Count;
        var initialNativeRegistrations = _nativeUndoRedoRegistrationCount;
        var originalSelection = _selectionContext.Current.ToArray();
        var originalSelectionLock = _selectionContext.IsLocked;
        var occupiedPosition = area.Entities.First().Transform.Position;
        var rejected = _projectPreview.DropBlueprintForSmokeTest(placeable.Id, occupiedPosition);
        if (rejected is not null || _history.State.Revision != initialRevision ||
            !_projectPreview.GhostVisible || _projectPreview.LastPlacementWasValid)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G704_DIRECT_FAILED invalid_feedback");
            return false;
        }

        var freePosition = FindFreePlacement(placeable.Id);
        if (freePosition is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G704_DIRECT_FAILED no_free_cell");
            return false;
        }

        var placedEntityId = _projectPreview.DropBlueprintForSmokeTest(placeable.Id, freePosition.Value);
        if (placedEntityId is null || _history.State.Project.Areas.First().Entities.Count != initialEntityCount + 1 ||
            !_projectPreview.SelectEntityForSmokeTest(placedEntityId) || !_projectPreview.GizmoVisible)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G704_DIRECT_FAILED drag_drop");
            return false;
        }

        var movePosition = FindFreePlacement(placeable.Id, placedEntityId, freePosition.Value);
        if (movePosition is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G704_DIRECT_FAILED no_move_cell");
            return false;
        }

        var movePreview = _projectPreview.PreviewEntityMoveForSmokeTest(placedEntityId, movePosition.Value);
        if (!movePreview.IsValid || !_projectPreview.GhostVisible || !_projectPreview.LastPlacementWasValid ||
            !_projectPreview.CommitPreviewMoveForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G704_DIRECT_FAILED gizmo_move");
            return false;
        }

        var moved = _history.State.Project.Areas.First().Entities.Single(entity => entity.Id == placedEntityId);
        if (moved.Transform.Position != movePreview.CorrectedPosition)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G704_DIRECT_FAILED transform_state");
            return false;
        }

        _selectionContext.SetLocked(false);
        var anchor = area.Entities.First();
        var selections = new[]
        {
            new CreatorSelection(CreatorSelectionKind.Entity, anchor.Id.ToString(), "Ancre", $"areas/{area.Id}/entities/{anchor.Id}"),
            new CreatorSelection(CreatorSelectionKind.Entity, placedEntityId.ToString(), "Placement", $"areas/{area.Id}/entities/{placedEntityId}"),
        };
        _selectionContext.Replace(selections);
        var selectionInspection = CreatorSelectionInspector.Inspect(_history.State.Project, selections, "toolset:r033-smoke");
        if (!_selectionContext.Focus(selections[1], "toolset:r033-smoke") ||
            !_selectionContext.Frame("toolset:r033-smoke") || selectionInspection.CommonProperties.Count < 7)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R033_DIRECT_MANIPULATION_FAILED selection_inspector");
            return false;
        }

        var manipulation = new ManipulateEntitiesCommand(
            CommandId.From(Guid.NewGuid()), ManipulateEntitiesHandler.CurrentSchemaVersion, "toolset:r033-smoke",
            _history.State.Revision, area.Id, [anchor.Id, placedEntityId], EditorTransformTool.Rotate,
            new Vector3Value(0, 15, 0), EditorCoordinateSpace.World, EditorPivotMode.Individual,
            EditorTransformAlignment.None, 1, 15, .05f, false);
        if (!ApplyCommand(_manipulateEntitiesHandler.Handle(_history.State, manipulation),
                "Rotation multiple validée.", selectedEntityId: null))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R033_DIRECT_MANIPULATION_FAILED multi_gizmo");
            return false;
        }

        var validator = new PlacementValidator();
        var socketFailure = validator.Validate(_history.State.Project,
            new PlacementValidationRequest(area.Id, placeable.Id, freePosition.Value, 1,
                RequiredSocket: "wall", AvailableSockets: ["floor"]));
        var budgetFailure = validator.Validate(_history.State.Project,
            new PlacementValidationRequest(area.Id, placeable.Id, freePosition.Value, 1,
                AvailableBudgetBytes: 1024, BlueprintEstimatedBytes: 2048));
        var correction = validator.Validate(_history.State.Project,
            new PlacementValidationRequest(area.Id, placeable.Id, occupiedPosition, 1, AutoCorrect: true));
        if (socketFailure.Code != PlacementValidator.SocketMissingCode ||
            budgetFailure.Code != PlacementValidator.BudgetExceededCode || correction.Corrections?.Count < 1)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R033_DIRECT_MANIPULATION_FAILED placement_causes");
            return false;
        }

        var cameraHandler = new NavigateEditorCameraHandler(new SystemClock());
        var cameraState = new EditorCameraState(EditorCameraProfile.CreateDefault(
            EditorCameraProfileId.From(Guid.Parse("33000000-0000-4000-8000-000000000001"))), new EditorCameraRevision(0));
        var cameraInput = new EditorCameraInputConfiguration(
            Guid.Parse("33000000-0000-4000-8000-000000000002"), 1, "toolset:r033-smoke", EditorCameraPilotMode.Fly,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["forward"] = "I",
                ["back"] = "K",
                ["left"] = "J",
                ["right"] = "L",
                ["up"] = "U",
                ["down"] = "O",
                ["focus"] = "F",
            });
        var configuredCamera = cameraHandler.Handle(cameraState, new NavigateEditorCameraCommand(
            CommandId.From(Guid.NewGuid()), cameraState.Revision, EditorCameraOperation.SetInput, Input: cameraInput));
        var pilotedCamera = cameraHandler.Handle(configuredCamera.State, new NavigateEditorCameraCommand(
            CommandId.From(Guid.NewGuid()), configuredCamera.State.Revision, EditorCameraOperation.Pilot, 2, -1, Z: 3));
        if (!configuredCamera.Succeeded || !pilotedCamera.Succeeded ||
            pilotedCamera.State.Profile.Pose.Pivot == cameraState.Profile.Pose.Pivot)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R033_DIRECT_MANIPULATION_FAILED camera_pilot");
            return false;
        }

        var undoManipulation = _history.Undo();
        var undoTransform = _history.Undo();
        var undoPlacement = _history.Undo();
        if (!undoManipulation.Succeeded || !undoTransform.Succeeded || !undoPlacement.Succeeded ||
            _history.State.Project.Areas.First().Entities.Count != initialEntityCount)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G704_DIRECT_FAILED undo");
            return false;
        }

        var redoPlacement = _history.Redo();
        var redoTransform = _history.Redo();
        var redoManipulation = _history.Redo();
        if (!redoPlacement.Succeeded || !redoTransform.Succeeded || !redoManipulation.Succeeded ||
            _history.State.Project.Areas.First().Entities.Single(entity => entity.Id == placedEntityId).Transform.Position != movePreview.CorrectedPosition)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G704_DIRECT_FAILED redo");
            return false;
        }

        var cleanupManipulation = _history.Undo();
        var cleanupTransform = _history.Undo();
        var cleanupPlacement = _history.Undo();
        if (!cleanupManipulation.Succeeded || !cleanupTransform.Succeeded || !cleanupPlacement.Succeeded ||
            _history.State.Project.Areas.First().Entities.Count != initialEntityCount)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G704_DIRECT_FAILED cleanup");
            return false;
        }

        var journalRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "r033-journal", Guid.NewGuid().ToString("N"));
        try
        {
            EditorCommandJournalStore.Save(journalRoot, _history.Journal);
            var restoredJournal = EditorCommandJournalStore.Load(journalRoot);
            if (restoredJournal.Count != _history.Journal.Count ||
                _history.Journal.Count < initialJournalCount + 3 ||
                (_nativeUndoRedo is not null && _nativeUndoRedoRegistrationCount < initialNativeRegistrations + 3))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R033_DIRECT_MANIPULATION_FAILED command_journal_native_history");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(journalRoot)) Directory.Delete(journalRoot, true);
        }

        _selectionContext.Replace(originalSelection);
        _selectionContext.SetLocked(originalSelectionLock);
        RebuildPreview();
        RefreshEditorControls(area.Entities.First().Id);
        GD.Print("OPENRPG_FORGE_TOOLSET_G704_DIRECT_PASS drag=placed gizmo=moved invalid=blocked history=restored");
        GD.Print("OPENRPG_FORGE_TOOLSET_R033_DIRECT_MANIPULATION_PASS " +
            "selection=simple-multiple-focus-frame-lock-common-inspector camera=configurable-fly-orthographic-bookmarks " +
            "placement=ghost-collision-socket-grid-budget-correction gizmo=translate-rotate-scale-pivot-local-global-snap-numeric-align " +
            "command=uuid-schema-provenance-dry-run-atomic history=native-undo-redo-journal persistence=roundtrip");
        return true;
    }

    private Vector3Value? FindFreePlacement(
        BlueprintId blueprintId,
        EntityId? ignoredEntityId = null,
        Vector3Value? excludedPosition = null)
    {
        if (_history is null)
        {
            return null;
        }

        var area = _history.State.Project.Areas.First();
        var validator = new PlacementValidator();
        for (var x = -9; x <= 9; x++)
        {
            for (var z = -9; z <= 9; z++)
            {
                var candidate = new Vector3Value(x, 0, z);
                if (excludedPosition == candidate)
                {
                    continue;
                }

                var result = validator.Validate(
                    _history.State.Project,
                    new PlacementValidationRequest(area.Id, blueprintId, candidate, 1, ignoredEntityId));
                if (result.IsValid)
                {
                    return result.CorrectedPosition;
                }
            }
        }

        return null;
    }

    private bool RunAreaWorkflowSmokeTest()
    {
        if (_history is null || _areaWorkspace is null || _projectPreview is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G705_AREA_FAILED missing_workspace");
            return false;
        }

        var originalArea = GetActiveArea();
        var originalAreaCount = _history.State.Project.Areas.Count;
        var createdAreaId = CreateArea(
            "Cour des transitions G705",
            AreaKind.Exterior,
            new AreaDimensions(48, 36),
            AreaTemplateKind.Playable);
        if (createdAreaId is null ||
            _history.State.Project.Areas.Count != originalAreaCount + 1 ||
            _activeAreaContext.Current != createdAreaId ||
            !_areaWorkspace.SelectAreaForSmokeTest(createdAreaId) ||
            _projectPreview.EntityCount != 1)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G705_AREA_FAILED create_activate");
            return false;
        }

        var createdArea = _history.State.Project.Areas.Single(area => area.Id == createdAreaId);
        var sourceAnchor = createdArea.Entities.Single();
        var destinationAnchor = originalArea.Entities.First();
        var portal = new PortalLink(
            PortalId.From(Guid.Parse("f5000000-0000-4000-8000-000000000001")),
            "Retour vers la zone initiale",
            sourceAnchor.Id,
            originalArea.Id,
            destinationAnchor.Id,
            true,
            true);
        if (!UpsertPortal(createdArea.Id, portal) ||
            ProjectValidator.Validate(_history.State.Project)
                .Any(diagnostic => diagnostic.Code.StartsWith("area.portal.", StringComparison.Ordinal)))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G705_AREA_FAILED transition");
            return false;
        }

        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "area-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var creation = ProjectSession.Create(smokeRoot, _history.State.Project);
            var areaDirectory = Path.Combine(smokeRoot, "areas");
            var areaDocuments = Directory.Exists(areaDirectory)
                ? Directory.GetFiles(areaDirectory, "*.json", SearchOption.TopDirectoryOnly)
                : [];
            var reopened = ProjectSession.Open(smokeRoot);
            if (!creation.Succeeded || !reopened.Succeeded || reopened.Session is null ||
                areaDocuments.Length != originalAreaCount + 1 ||
                reopened.Session.Project.Areas.Single(area => area.Id == createdAreaId).Portals.Count != 1 ||
                !File.ReadAllText(Path.Combine(smokeRoot, ProjectSession.DefaultProjectFileName))
                    .Contains($"\"formatVersion\": \"{ProjectSession.CurrentProjectFormatVersion}\"", StringComparison.Ordinal))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G705_AREA_FAILED persistence");
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

        Undo();
        Undo();
        if (_history.State.Project.Areas.Count != originalAreaCount ||
            _activeAreaContext.Current != originalArea.Id)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G705_AREA_FAILED undo");
            return false;
        }

        Redo();
        Redo();
        if (_history.State.Project.Areas.Single(area => area.Id == createdAreaId).Portals.Count != 1)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G705_AREA_FAILED redo");
            return false;
        }

        Undo();
        Undo();
        ActivateArea(originalArea.Id);
        if (_history.State.Project.Areas.Count != originalAreaCount)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G705_AREA_FAILED cleanup");
            return false;
        }

        GD.Print(
            $"OPENRPG_FORGE_TOOLSET_G705_AREA_PASS areas={originalAreaCount + 1} " +
            $"documents={originalAreaCount + 1} transition=valid active=switched history=restored");
        return true;
    }

    private bool RunSemanticClipboardSmokeTest()
    {
        if (_history is null || _entityPicker is null || _projectPreview is null || _areaWorkspace is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_FAILED missing_workspace");
            return false;
        }

        var sourceArea = GetActiveArea();
        var candidate = FindEntityWithFreeDuplicateCell(sourceArea);
        if (candidate is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_FAILED no_free_duplicate_cell");
            return false;
        }

        var initialIds = sourceArea.Entities.Select(entity => entity.Id).ToHashSet();
        var initialEntityCount = sourceArea.Entities.Count;
        var entityIndex = _entityIds.FindIndex(id => id == candidate.Id);
        if (entityIndex < 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_FAILED selection");
            return false;
        }

        _entityPicker.Selected = entityIndex;
        _selectionContext.Replace([new CreatorSelection(CreatorSelectionKind.Entity, candidate.Id.ToString(),
            "clipboard-smoke", $"areas/{sourceArea.Id}/entities/{candidate.Id}")]);
        CopySelectedEntity();
        PasteSemanticClipboard();
        var afterPaste = GetActiveArea();
        var pasted = afterPaste.Entities.SingleOrDefault(entity => !initialIds.Contains(entity.Id));
        if (pasted is null || afterPaste.Entities.Count != initialEntityCount + 1 ||
            pasted.BlueprintId != candidate.BlueprintId || !pasted.Tags.SequenceEqual(candidate.Tags) ||
            pasted.Overrides != candidate.Overrides)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_FAILED entity_paste");
            return false;
        }

        Undo();
        Redo();
        if (GetActiveArea().Entities.Count != initialEntityCount + 1)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_FAILED entity_redo");
            return false;
        }
        Undo();

        var blueprintCount = _history.State.Project.Blueprints.Count;
        _independentCopy!.ButtonPressed = true;
        CopySelectedEntity();
        PasteSemanticClipboard();
        var independent = GetActiveArea().Entities.SingleOrDefault(entity => !initialIds.Contains(entity.Id));
        if (independent is null || independent.BlueprintId == candidate.BlueprintId ||
            _history.State.Project.Blueprints.Count != blueprintCount + 1)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_FAILED independent_copy");
            return false;
        }
        _selectionContext.Replace([new CreatorSelection(CreatorSelectionKind.Entity, independent.Id.ToString(),
            "clipboard-cut-smoke", $"areas/{sourceArea.Id}/entities/{independent.Id}")]);
        CutSelectedEntities();
        if (_semanticClipboard.Payload is not EntityClipboardPayload { IsCut: true } cutPayload ||
            cutPayload.Id == Guid.Empty || string.IsNullOrWhiteSpace(cutPayload.Provenance) ||
            GetActiveArea().Entities.Count != initialEntityCount)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_FAILED cut_payload");
            return false;
        }
        Undo();
        Undo();
        _independentCopy.ButtonPressed = false;
        _selectionContext.Replace([new CreatorSelection(CreatorSelectionKind.Entity, candidate.Id.ToString(),
            "clipboard-smoke", $"areas/{sourceArea.Id}/entities/{candidate.Id}")]);
        if (_history.State.Project.Blueprints.Count != blueprintCount ||
            GetActiveArea().Entities.Count != initialEntityCount)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_FAILED independent_cleanup");
            return false;
        }

        var clonedAreaId = DuplicateArea(
            sourceArea.Id,
            "Crypte clone G706",
            AreaCloneMode.Full,
            ExternalPortalPolicy.Drop);
        if (clonedAreaId is null || _activeAreaContext.Current != clonedAreaId ||
            _areaWorkspace.AreaCount != _history.State.Project.Areas.Count)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_FAILED area_clone");
            return false;
        }

        var clone = _history.State.Project.Areas.Single(area => area.Id == clonedAreaId);
        if (clone.Entities.Count != initialEntityCount ||
            clone.Entities.Select(entity => entity.Id).Intersect(initialIds).Any() ||
            !clone.Entities.Select(entity => entity.BlueprintId)
                .SequenceEqual(sourceArea.Entities.Select(entity => entity.BlueprintId)))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_FAILED area_remap");
            return false;
        }

        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "clipboard-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var creation = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            if (!creation.Succeeded || !reopened.Succeeded || reopened.Session is null ||
                reopened.Session.Project.Areas.Count != 2 ||
                reopened.Session.Project.Areas.Single(area => area.Id == clonedAreaId).Entities.Count != initialEntityCount)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_FAILED persistence");
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

        Undo();
        Redo();
        if (!_history.State.Project.Areas.Any(area => area.Id == clonedAreaId))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_FAILED area_redo");
            return false;
        }
        Undo();
        ActivateArea(sourceArea.Id);
        if (_history.State.Project.Areas.Any(area => area.Id == clonedAreaId) ||
            GetActiveArea().Entities.Count != initialEntityCount)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_FAILED cleanup");
            return false;
        }

        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_PASS entities=semantic-copy area=full-clone " +
            "uuids=remapped preview=available history=restored persistence=roundtrip");
        return true;
    }

    private bool RunGlobalSearchAndRenameSmokeTest()
    {
        if (_history is null || _globalSearch is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G707_SEARCH_FAILED missing_search");
            return false;
        }

        var project = _history.State.Project;
        var referenceIndex = CreatorReferenceIndex.Build(project);
        var blueprint = project.Blueprints
            .OrderByDescending(candidate => referenceIndex
                .Find(CreatorContentKind.Blueprint, candidate.Id.ToString()).Count)
            .ThenBy(candidate => candidate.DisplayName, StringComparer.Ordinal)
            .First();
        var initialName = blueprint.DisplayName;
        var initialSlug = blueprint.Slug;
        var referenceCount = referenceIndex
            .Find(CreatorContentKind.Blueprint, blueprint.Id.ToString()).Count;
        if (_globalSearch.SearchForSmokeTest(string.Empty, CreatorContentKind.Blueprint) != project.Blueprints.Count ||
            !_globalSearch.SelectDocumentForSmokeTest(CreatorContentKind.Blueprint, blueprint.Id.ToString()) ||
            _globalSearch.VisibleReferenceCount != referenceCount ||
            !_globalSearch.PreviewRenameForSmokeTest($"{initialName} G707", $"{initialSlug}-g707") ||
            !_globalSearch.ApplyRenameForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G707_SEARCH_FAILED ui_workflow");
            return false;
        }

        var renamed = _history.State.Project.Blueprints.Single(candidate => candidate.Id == blueprint.Id);
        if (renamed.DisplayName != $"{initialName} G707" || renamed.Slug != $"{initialSlug}-g707" ||
            CreatorReferenceIndex.Build(_history.State.Project)
                .Find(CreatorContentKind.Blueprint, blueprint.Id.ToString()).Count != referenceCount)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G707_SEARCH_FAILED stable_identity");
            return false;
        }

        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "search-rename-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var creation = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var persisted = reopened.Session?.Project.Blueprints
                .SingleOrDefault(candidate => candidate.Id == blueprint.Id);
            if (!creation.Succeeded || !reopened.Succeeded || persisted is null ||
                persisted.DisplayName != renamed.DisplayName || persisted.Slug != renamed.Slug)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G707_SEARCH_FAILED persistence");
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
        var restored = _history.State.Project.Blueprints.Single(candidate => candidate.Id == blueprint.Id);
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            restored.DisplayName != initialName || restored.Slug != initialSlug)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G707_SEARCH_FAILED history");
            return false;
        }

        RefreshEditorControls();
        GD.Print(
            $"OPENRPG_FORGE_TOOLSET_G707_SEARCH_PASS search=global references={referenceCount} " +
            "rename=stable-uuid history=restored persistence=roundtrip");
        return true;
    }

    private bool RunR034ContentOperationsSmokeTest()
    {
        if (_history is null || _globalSearch is null || _cutButton is null || _independentCopy is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R034_CONTENT_OPERATIONS_FAILED missing_ui");
            return false;
        }

        var root = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "r034-smoke", Guid.NewGuid().ToString("N"));
        try
        {
            var scripts = Path.Combine(root, "scripts");
            var assets = Path.Combine(root, "assets");
            Directory.CreateDirectory(scripts);
            Directory.CreateDirectory(assets);
            File.WriteAllText(Path.Combine(scripts, "guardian.forge"),
                "asset = \"assets/guardian.glb\"\n", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(assets, "guardian.glb"), "r034-asset", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(root, "project.json"),
                "{\"script\":\"scripts/guardian.forge\"}\n", new UTF8Encoding(false));

            var searchProfile = CreatorSearchProfile.CreateDefault(_history.State.Project.Id.Value) with
            {
                Provenance = "toolset:r034-smoke",
                ResultLimit = 37,
            };
            CreatorSearchProfileStore.Save(root, searchProfile);
            var loadedProfile = CreatorSearchProfileStore.Load(root, _history.State.Project.Id.Value);
            var script = CreatorWorkspaceResource.Create(CreatorContentKind.Script, "scripts/guardian.forge",
                File.ReadAllText(Path.Combine(scripts, "guardian.forge")), ["script"],
                id: WorkspaceResourceIdentityStore.Resolve(root, "scripts/guardian.forge"));
            var asset = CreatorWorkspaceResource.Create(CreatorContentKind.Asset, "assets/guardian.glb",
                "guardian 3d", ["asset", "3d"],
                id: WorkspaceResourceIdentityStore.Resolve(root, "assets/guardian.glb"));
            var catalog = CreatorWorkspaceResourceCatalog.Build(_history.State.Project, [script, asset]);
            var searchIndex = CreatorSearchIndex.Build(_history.State.Project,
                supplementalDocuments: catalog.Documents);
            var referenceIndex = CreatorReferenceIndex.Build(_history.State.Project, catalog.References);
            if (loadedProfile.ResultLimit != 37 ||
                searchIndex.Search(new CreatorSearchQuery("guardian", CreatorContentKind.Script)).Count != 1 ||
                searchIndex.Search(new CreatorSearchQuery("guardian 3d", CreatorContentKind.Asset)).Count != 1 ||
                referenceIndex.Find(CreatorContentKind.Asset, asset.Id.ToString()).Count != 1)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R034_CONTENT_OPERATIONS_FAILED search_references_profile");
                return false;
            }

            var rename = WorkspaceResourceRenameService.Preview(root, "scripts/guardian.forge",
                "scripts/guardian_runtime.forge", "toolset:r034-smoke");
            WorkspaceResourceRenameService.Apply(rename);
            var renamedValid = File.Exists(Path.Combine(scripts, "guardian_runtime.forge")) &&
                File.ReadAllText(Path.Combine(root, "project.json"))
                    .Contains("scripts/guardian_runtime.forge", StringComparison.Ordinal) &&
                WorkspaceResourceIdentityStore.Resolve(root, "scripts/guardian_runtime.forge") == rename.ResourceId;
            WorkspaceResourceRenameService.Undo(rename);
            if (!renamedValid || !File.Exists(Path.Combine(scripts, "guardian.forge")))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R034_CONTENT_OPERATIONS_FAILED resource_rename");
                return false;
            }

            var project = _history.State.Project;
            var playerIds = project.Blueprints.Where(item => item.Kind == BlueprintKind.PlayerSpawn)
                .Select(item => item.Id).ToHashSet();
            var start = project.Areas.SelectMany(area => area.Entities.Select(entity => (Area: area, Entity: entity)))
                .First(item => playerIds.Contains(item.Entity.BlueprintId));
            var targetKind = project.Quests.Count > 0 ? PlaytestTargetKind.SelectedQuest
                : project.Dialogues.Count > 0 ? PlaytestTargetKind.Dialogue : PlaytestTargetKind.World;
            Guid? targetId = targetKind == PlaytestTargetKind.SelectedQuest ? project.Quests[0].Id.Value
                : targetKind == PlaytestTargetKind.Dialogue ? project.Dialogues[0].Id.Value : null;
            var injections = targetKind == PlaytestTargetKind.SelectedQuest
                ? new[] { new PlaytestQuestInjection(Guid.NewGuid(), project.Quests[0].Id,
                    project.Quests[0].InitialStateId, new Dictionary<string, string>()) }
                : [];
            var defaults = AreaPlaytestProfile.CreateDefault(start.Area.Id);
            var profile = new AreaPlaytestProfile(defaults.Id, AreaPlaytestProfile.CurrentSchemaVersion,
                "toolset:r034-smoke", true, false, PlayFromHereStartMode.PlayerSpawn, start.Entity.Id,
                TransformValue.Identity, null, start.Entity.BlueprintId, "r034-targeted", injections,
                targetKind, targetId, headless: true);
            var before = ProjectJsonSerializer.Serialize(project);
            var snapshotStore = new PlaytestSnapshotStore(Path.Combine(root, "playtests"));
            var snapshot = snapshotStore.Create(project, start.Area.Id, profile);
            var manifest = PlaytestLaunchManifestStore.Load(snapshot.LaunchManifestFilePath!);
            var playtestValid = manifest.Headless && manifest.TargetKind == targetKind.ToString() &&
                manifest.TargetId == targetId?.ToString() && before == ProjectJsonSerializer.Serialize(project);
            snapshotStore.Delete(snapshot);
            if (!playtestValid)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R034_CONTENT_OPERATIONS_FAILED targeted_playtest");
                return false;
            }

            GD.Print("OPENRPG_FORGE_TOOLSET_R034_CONTENT_OPERATIONS_PASS " +
                "clipboard=multi-cut-instance-independent-remap search=scripts-assets-profile-persisted " +
                "references=localized-variable-resource-path rename=preview-atomic-uuid-rollback " +
                $"playtest=world-quest-dialogue-headless target={targetKind.ToString().ToLowerInvariant()}");
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_R034_CONTENT_OPERATIONS_FAILED exception={exception.Message}");
            return false;
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private bool RunModuleConfigurationSmokeTest()
    {
        if (_history is null || _moduleConfiguration is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G708_MODULE_FAILED missing_configuration");
            return false;
        }

        var originalModule = _history.State.Project.Module;
        var originalMetadata = originalModule.Metadata;
        var metadata = new ProjectMetadata(
            originalMetadata.Id,
            "fr",
            [
                new ProjectLocalizedMetadata(
                    "fr",
                    originalModule.DisplayName,
                    "Module de validation Creator G708",
                    originalModule.Description,
                    "Équipe OpenRPG Forge",
                    "Copiez le module puis lancez-le depuis le Creator.",
                    "Règles du module et variantes supportées.",
                    "ZQSD, souris et manette.",
                    "CC0-1.0 et notices incluses.",
                    "Contient des combats fantastiques.",
                    "OpenRPG Forge 0.1.0 ou supérieur."),
                new ProjectLocalizedMetadata(
                    "en",
                    $"{originalModule.DisplayName} EN",
                    "G708 Creator validation module",
                    originalModule.Description,
                    "OpenRPG Forge Team",
                    "Copy the module and launch it from Creator.",
                    "Module rules and supported variants.",
                    "WASD, mouse and controller.",
                    "CC0-1.0 and bundled notices.",
                    "Contains fantasy combat.",
                    "OpenRPG Forge 0.1.0 or newer."),
            ],
            "Atelier OpenRPG Forge",
            "0.8.0",
            "CC0-1.0",
            "assets/module-cover.png",
            ["creator", "smoke"],
            "Tout public",
            "creator-smoke");
        var variable = new ProjectVariableDefinition(
            ProjectVariableId.From(Guid.Parse("f8000000-0000-4000-8000-000000000001")),
            "smoke.g708.ready",
            ProjectVariableValueKind.Boolean,
            ProjectVariableScope.Save,
            "false",
            "Prouve le blackboard typé G708.",
            "creator-smoke", 1, ProjectVariableReplicationMode.Party, debugVisible: false,
            requiredAtRuntime: true);
        if (!_moduleConfiguration.ConfigureForSmokeTest(metadata, [variable]) ||
            !_moduleConfiguration.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G708_MODULE_FAILED ui_workflow");
            return false;
        }

        var configured = _history.State.Project.Module;
        var blackboard = new ProjectBlackboardScriptService(configured.Variables);
        var handle = blackboard.Query(variable.Key).Single();
        var mutation = blackboard.Set(handle, "true");
        var replication = blackboard.ReplicationDelta(BlackboardReplicationAudience.Party);
        var debug = blackboard.DebugSnapshot();
        var searchResults = CreatorSearchIndex.Build(_history.State.Project).Search(
            new CreatorSearchQuery("smoke.g708.ready", CreatorContentKind.ProjectVariable));
        if (configured.Metadata.Localizations.Count != 2 || configured.Metadata.Version != "0.8.0" ||
            configured.Variables.Count != 1 || searchResults.Count != 1 ||
            !mutation.Succeeded || replication.Values.GetValueOrDefault(variable.Key) != "true" ||
            debug.HiddenVariableCount != 1 || debug.Variables.Count != 0 ||
            blackboard.Set(handle, "not-a-boolean").Succeeded)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G708_MODULE_FAILED canonical_state");
            return false;
        }

        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "module-configuration-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = Path.Combine(smokeRoot, "workspace");
            var artifact = Path.Combine(smokeRoot, "configured.orpmod");
            var creation = ProjectSession.Create(workspace, _history.State.Project);
            var reopened = ProjectSession.Open(workspace);
            var exported = ModulePackager.Export(workspace, artifact);
            if (!creation.Succeeded || !reopened.Succeeded || reopened.Session is null ||
                reopened.Session.Project.Module.Metadata.Localizations.Count != 2 ||
                ProjectDocumentationValidator.Pages(
                    reopened.Session.Project.Module.Metadata.Localizations.Single(item => item.Locale == "fr")).Count != 8 ||
                reopened.Session.Project.Module.Metadata.Localizations.Single(item => item.Locale == "fr").Controls !=
                    "ZQSD, souris et manette." ||
                reopened.Session.Project.Module.Variables.Single().Kind != ProjectVariableValueKind.Boolean ||
                reopened.Session.Project.Module.Variables.Single().Replication != ProjectVariableReplicationMode.Party ||
                reopened.Session.Project.Module.Variables.Single().DebugVisible ||
                !reopened.Session.Project.Module.Variables.Single().RequiredAtRuntime ||
                exported.Manifest.ModuleVersion != "0.8.0" ||
                !File.ReadAllText(reopened.Session.ProjectFilePath)
                    .Contains($"\"formatVersion\": \"{ProjectSession.CurrentProjectFormatVersion}\"", StringComparison.Ordinal))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G708_MODULE_FAILED persistence");
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
            _history.State.Project.Module.Metadata != originalMetadata ||
            _history.State.Project.Module.Variables.Count != originalModule.Variables.Count)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G708_MODULE_FAILED history");
            return false;
        }

        RefreshEditorControls();
        GD.Print(
            $"OPENRPG_FORGE_TOOLSET_G708_MODULE_PASS metadata=localized variables=typed scopes=save " +
            $"format={ProjectSession.CurrentProjectFormatVersion} history=restored package=versioned");
        GD.Print("OPENRPG_FORGE_TOOLSET_R011_BLACKBOARD_PASS " +
            "schema=typed-scoped save=roundtrip replication=party debug=governed " +
            "forgescript=typed-handle-command ui=module-configuration history=undo-redo");
        GD.Print("OPENRPG_FORGE_TOOLSET_R052_PROJECT_DOCS_PASS pages=8-localized " +
            "synopsis-installation-rules-controls-credits-licenses-warnings-compatibility " +
            "ui=visible-empty-error-tabs command=dry-run-apply history=undo-redo " +
            "persistence=project-package-roundtrip mcp=filtered-plan-apply-revert build=preflight");
        return true;
    }

    private bool RunPaletteProvenanceSmokeTest()
    {
        if (_history is null || _blueprintCatalog is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G709_PALETTE_FAILED missing_catalog");
            return false;
        }

        var blueprint = _history.State.Project.Blueprints.First();
        var originalOrigin = blueprint.PaletteOrigin;
        var originalFavorite = blueprint.IsFavorite;
        var originalProvenance = blueprint.Provenance;
        var originalAuthoring = blueprint.Authoring;
        var installedProvenance = new BlueprintProvenance(
            BlueprintProvenance.CurrentSchemaVersion,
            $"pack://forge-core/1.0.0/{blueprint.Slug}",
            "OpenRPG Forge",
            "CC0-1.0",
            new string('a', 64),
            false,
            ["Validated installed pack content"],
            "spdx:CC0-1.0");
        if (!_blueprintCatalog.ConfigurePaletteForSmokeTest(
                blueprint.Id,
                BlueprintPaletteOrigin.InstalledPack,
                false,
                installedProvenance))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G709_PALETTE_FAILED installed_ui");
            return false;
        }

        var installedCatalog = BlueprintPaletteCatalog.Build(_history.State.Project);
        var installedBlueprint = _history.State.Project.Blueprints.Single(candidate => candidate.Id == blueprint.Id);
        var editInstalled = _upsertBlueprintHandler.Handle(
            _history.State,
            new UpsertBlueprintCommand(
                CommandId.From(Guid.NewGuid()),
                _history.State.Revision,
                blueprint.Id,
                blueprint.Slug,
                $"{blueprint.DisplayName} modifié",
                blueprint.Kind,
                DryRun: false));
        if (installedCatalog.Filter(BlueprintPaletteView.InstalledPacks).Count != 1 ||
            !_blueprintCatalog.SelectedIsReadOnly ||
            installedBlueprint.Authoring.Version != "1.0.0" ||
            installedBlueprint.Authoring.ManifestPath != "pack://forge-core/1.0.0/manifest.json" ||
            editInstalled.Diagnostics.Count != 1 ||
            editInstalled.Diagnostics[0].Code != UpsertBlueprintHandler.ReadOnlyCode)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G709_PALETTE_FAILED installed_readonly");
            return false;
        }

        if (!_history.Undo().Succeeded)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G709_PALETTE_FAILED installed_undo");
            return false;
        }
        RefreshEditorControls();

        var temporaryProvenance = new BlueprintProvenance(
            BlueprintProvenance.CurrentSchemaVersion,
            $"temporary://creator/{blueprint.Id}",
            "Creator session",
            "UNLICENSED",
            "unverified",
            false,
            ["Ephemeral authoring draft"]);
        if (!_blueprintCatalog.ConfigurePaletteForSmokeTest(
                blueprint.Id,
                BlueprintPaletteOrigin.Temporary,
                false,
                temporaryProvenance) ||
            BlueprintPaletteCatalog.Build(_history.State.Project)
                .Filter(BlueprintPaletteView.Temporary).Count != 1 ||
            !_history.Undo().Succeeded)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G709_PALETTE_FAILED temporary_scope");
            return false;
        }
        RefreshEditorControls();

        var generatedProvenance = new BlueprintProvenance(
            BlueprintProvenance.CurrentSchemaVersion,
            $"generator://creator/{blueprint.Slug}",
            "Forge Generator",
            "CC0-1.0",
            new string('b', 64),
            true,
            ["Generated for G7-09", "Reviewed by module author"],
            "project-author:generated-and-reviewed");
        if (!_blueprintCatalog.ConfigurePaletteForSmokeTest(
                blueprint.Id,
                BlueprintPaletteOrigin.Generated,
                true,
                generatedProvenance) ||
            !_blueprintCatalog.SelectViewForSmokeTest(BlueprintPaletteView.Generated) ||
            _blueprintCatalog.VisibleBlueprintCount != 1 ||
            !_blueprintCatalog.SelectViewForSmokeTest(BlueprintPaletteView.Favorites) ||
            _blueprintCatalog.VisibleBlueprintCount != 1)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G709_PALETTE_FAILED generated_favorite_views");
            return false;
        }

        var search = CreatorSearchIndex.Build(_history.State.Project).Search(
            new CreatorSearchQuery("Forge Generator", CreatorContentKind.Blueprint));
        if (search.Count != 1 || search[0].Document.Id != blueprint.Id.ToString() ||
            !search[0].Document.Tags.Contains("favorite", StringComparer.OrdinalIgnoreCase))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G709_PALETTE_FAILED provenance_search");
            return false;
        }

        var dropPlanner = new BlueprintDropPlanner();
        var activeArea = _history.State.Project.Areas.First();
        var freePlacement = FindFreePlacement(blueprint.Id);
        var itemBlueprint = _history.State.Project.Blueprints.FirstOrDefault(candidate => candidate.Kind == BlueprintKind.Item);
        var dropPlans = freePlacement is null || itemBlueprint is null
            ? []
            : new[]
            {
                dropPlanner.Plan(_history.State.Project, new BlueprintDropRequest(
                    Guid.NewGuid(), BlueprintDropPlanner.CurrentSchemaVersion, "toolset:r031-smoke",
                    blueprint.Id, BlueprintDropTargetKind.Viewport, $"areas/{activeArea.Id}/entities",
                    activeArea.Id, freePlacement)),
                dropPlanner.Plan(_history.State.Project, new BlueprintDropRequest(
                    Guid.NewGuid(), 1, "toolset:r031-smoke", blueprint.Id,
                    BlueprintDropTargetKind.Graph, "dialogues/smoke/nodes/speakerBlueprintId")),
                dropPlanner.Plan(_history.State.Project, new BlueprintDropRequest(
                    Guid.NewGuid(), 1, "toolset:r031-smoke", itemBlueprint.Id,
                    BlueprintDropTargetKind.Inventory, "areas/smoke/entities/player/inventory/items")),
                dropPlanner.Plan(_history.State.Project, new BlueprintDropRequest(
                    Guid.NewGuid(), 1, "toolset:r031-smoke", blueprint.Id,
                    BlueprintDropTargetKind.Property, "areas/smoke/entities/selection/properties/appearance")),
                dropPlanner.Plan(_history.State.Project, new BlueprintDropRequest(
                    Guid.NewGuid(), 1, "toolset:r031-smoke", blueprint.Id,
                    BlueprintDropTargetKind.Package, "module/content/packages/selection")),
            };
        if (dropPlans.Length != 5 || dropPlans.Any(plan => !plan.IsValid) ||
            BlueprintCreditCatalog.Aggregate(_history.State.Project.Blueprints).Count == 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R031_PALETTE_INTEGRATION_FAILED contextual_drop");
            return false;
        }

        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "palette-provenance-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = Path.Combine(smokeRoot, "workspace");
            var creation = ProjectSession.Create(workspace, _history.State.Project);
            var reopened = ProjectSession.Open(workspace);
            var restoredBlueprint = reopened.Session?.Project.Blueprints
                .Single(candidate => candidate.Id == blueprint.Id);
            if (!creation.Succeeded || !reopened.Succeeded || restoredBlueprint is null ||
                restoredBlueprint.PaletteOrigin != BlueprintPaletteOrigin.Generated ||
                !restoredBlueprint.IsFavorite ||
                !restoredBlueprint.Provenance.IsAiGenerated ||
                restoredBlueprint.Provenance.LicenseSpdx != "CC0-1.0" ||
                restoredBlueprint.Provenance.PermissionEvidence != "project-author:generated-and-reviewed")
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G709_PALETTE_FAILED persistence");
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
        var restored = _history.State.Project.Blueprints.Single(candidate => candidate.Id == blueprint.Id);
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            restored.PaletteOrigin != originalOrigin || restored.IsFavorite != originalFavorite ||
            restored.Provenance != originalProvenance || restored.Authoring != originalAuthoring)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G709_PALETTE_FAILED history");
            return false;
        }

        _blueprintCatalog.SelectViewForSmokeTest(BlueprintPaletteView.All);
        RefreshEditorControls();
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G709_PALETTE_PASS standard=readonly project=editable " +
            $"generated=provenance favorites=filtered temporary=isolated format={ProjectSession.CurrentProjectFormatVersion} " +
            "history=restored persistence=roundtrip");
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_R030_BLUEPRINT_PALETTES_PASS " +
            "definition=uuid-schema-version-package-license-hash-budget instance=version-range-overrides " +
            "create=parameters-dependencies-preview-package standard=readonly-versioned-replaceable " +
            "project=content-blueprints-namespace-git-friendly ui=visible-command history=undo-redo " +
            "persistence=roundtrip export=manifest-licenses-migration-preview");
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_R031_PALETTE_INTEGRATION_PASS " +
            "drop=viewport-graph-inventory-property-package validation=contextual-command " +
            "dependencies=local-transitive-packages-minimal governance=source-author-spdx-permission-hash-modifications-ai " +
            "credits=aggregated distribution=blocked-if-incomplete persistence=roundtrip history=undo-redo");
        return true;
    }


}
#endif
