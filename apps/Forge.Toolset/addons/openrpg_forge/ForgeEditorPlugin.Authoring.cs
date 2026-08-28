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
    private void OpenNavigationConfiguration() =>
        OpenTool(_navigationConfiguration, () => _navigationConfiguration?.Open());

    private void OpenAreaEnvironment() => OpenTool(_areaEnvironmentWindow, () => _areaEnvironmentWindow?.Open());

    private void OpenTerrainStudio() => OpenTool(_terrainStudioWindow, () => _terrainStudioWindow?.Open());

    private void OpenRuntimePolicies() => OpenTool(_runtimePoliciesWindow, () => _runtimePoliciesWindow?.Open());

    private void OpenCinematicStudio() => OpenTool(_cinematicStudioWindow, () => _cinematicStudioWindow?.Open());

    private void OpenGameplayControls() => OpenTool(_gameplayControlsWindow, () => _gameplayControlsWindow?.Open());

    private void OpenRulesConfiguration() => OpenTool(_rulesConfigurationWindow, () => _rulesConfigurationWindow?.Open());

    private void OpenAreaExperience() => OpenTool(_areaExperienceWindow, () => _areaExperienceWindow?.Open());

    private void OpenAreaAuthoring() => OpenTool(_areaAuthoringWindow, () => _areaAuthoringWindow?.Open());

    private void SelectAreaLayerEntities(IReadOnlyList<EntityId> entityIds)
    {
        if (_history is null) return;
        var area = GetActiveArea();
        var selections = entityIds.Select(id =>
        {
            var entity = area.Entities.Single(item => item.Id == id);
            var displayName = _history.State.Project.Blueprints
                .FirstOrDefault(item => item.Id == entity.BlueprintId)?.DisplayName ?? id.ToString();
            return new CreatorSelection(CreatorSelectionKind.Entity, id.ToString(), displayName,
                $"areas/{area.Id}/entities/{id}");
        }).ToArray();
        _selectionContext.SetLocked(false);
        _selectionContext.Replace(selections);
    }

    private void OpenAreaFoundation() => OpenTool(_areaFoundationWindow, () => _areaFoundationWindow?.Open());

    private bool ApplyAreaFoundation(AreaId areaId, AreaFoundationProfile profile)
    {
        if (_history is null) return false;
        return ApplyCommand(_setAreaFoundationHandler.Handle(_history.State,
            new SetAreaFoundationCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision, areaId,
                profile, false)), "Fondation de zone mise à jour.", null);
    }

    private bool ApplyAreaTileEdit(AreaId areaId, AreaTileEditRequest request)
    {
        if (_history is null) return false;
        var area = _history.State.Project.Areas.SingleOrDefault(item => item.Id == areaId);
        if (area is null) return false;
        var preview = AreaGeometryPlanner.Edit(area, request);
        if (preview.Diagnostics.Count > 0)
        {
            foreach (var diagnostic in preview.Diagnostics)
                GD.PrintErr($"{diagnostic.Code} · {diagnostic.Path}: {diagnostic.Message}");
            return false;
        }

        var command = new EditAreaGeometryCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            area.Id, area.Dimensions, preview.Entities,
            $"Tile tool {request.Operation}; preserve attachments={request.PreserveAttachedObjects}.", false,
            preview.Foundation);
        var dryRun = _editAreaGeometryHandler.Handle(_history.State, command with { DryRun = true });
        if (!dryRun.Succeeded) return ApplyCommand(dryRun, string.Empty, null);
        return ApplyCommand(_editAreaGeometryHandler.Handle(_history.State, command),
            $"Outil de tuiles {request.Operation} appliqué · transaction atomique.", null);
    }

    private bool ApplyAreaPlaytest(AreaId areaId, AreaPlaytestProfile profile)
    {
        if (_history is null) return false;
        return ApplyCommand(_setAreaPlaytestHandler.Handle(_history.State,
            new SetAreaPlaytestCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision, areaId,
                profile, false)), "Play From Here configuré.", null);
    }

    private bool ApplyAreaAuthoring(AreaId areaId, AreaAuthoringProfile profile)
    {
        if (_history is null) return false;
        return ApplyCommand(_setAreaAuthoringHandler.Handle(_history.State,
            new SetAreaAuthoringCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision, areaId, profile, false)),
            "Carte, événements et calques mis à jour.", null);
    }

    private bool ApplyAreaEnvironment(AreaId areaId, AreaEnvironmentProfile profile)
    {
        if (_history is null) return false;
        var command = new SetAreaEnvironmentCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision, areaId, profile, false);
        return ApplyCommand(_setAreaEnvironmentHandler.Handle(_history.State, command), "Environnement de zone mis à jour.", null);
    }

    private bool ApplyAreaWorldProfile(AreaId areaId, AreaWorldProfile profile)
    {
        if (_history is null) return false;
        var command = new SetAreaWorldProfileCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision, areaId, profile, false);
        return ApplyCommand(_setAreaWorldProfileHandler.Handle(_history.State, command),
            "Terrain, biomes, features et volumes mis à jour.", null);
    }

    private bool ApplyTerrainBrush(AreaId areaId, TerrainBrushStroke stroke)
    {
        if (_history is null) return false;
        var command = new EditTerrainBrushCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision, areaId,
            stroke, false);
        return ApplyCommand(_editTerrainBrushHandler.Handle(_history.State, command),
            "Terrain sculpté et navigation reconstruite.", null);
    }

    private bool ApplyRuntimePolicies(ModuleRuntimePolicyProfile moduleProfile, AreaId areaId,
        AreaRuntimePolicyProfile areaProfile)
    {
        if (_history is null) return false;
        var preview = new SetRuntimePoliciesCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            moduleProfile, areaId, areaProfile, true);
        var previewResult = _setRuntimePoliciesHandler.Handle(_history.State, preview);
        if (!previewResult.Succeeded) return false;
        var command = preview with
        {
            CommandId = CommandId.From(Guid.NewGuid()),
            DryRun = false,
        };
        return ApplyCommand(_setRuntimePoliciesHandler.Handle(_history.State, command),
            "Rythme, repos et restauration des séquences mis à jour.", null);
    }

    private bool ApplyCinematicProfiles(Forge.Domain.Sequences.ModuleCinematicProfile moduleProfile, AreaId areaId,
        Forge.Domain.Sequences.AreaCinematicProfile areaProfile)
    {
        if (_history is null) return false;
        var preview = new SetCinematicProfilesCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            moduleProfile, areaId, areaProfile, true);
        var previewResult = _setCinematicProfilesHandler.Handle(_history.State, preview);
        if (!previewResult.Succeeded) return false;
        var command = preview with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
        return ApplyCommand(_setCinematicProfilesHandler.Handle(_history.State, command),
            "Séquences, caméras et mouvements mis à jour.", null);
    }

    private bool ApplyGameplayControlProfile(ModuleGameplayControlProfile profile)
    {
        if (_history is null) return false;
        var preview = new SetGameplayControlProfileCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            profile, true);
        if (!_setGameplayControlProfileHandler.Handle(_history.State, preview).Succeeded) return false;
        var command = preview with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
        return ApplyCommand(_setGameplayControlProfileHandler.Handle(_history.State, command),
            "Nage, escalade, roue d’actions, temps tactique et combat mis à jour.", null);
    }

    private bool ApplyRulesProfiles(ModuleRulesProfile moduleProfile, AreaId areaId, AreaRulesProfile areaProfile)
    {
        if (_history is null) return false;
        var preview = new SetRulesProfilesCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            moduleProfile, areaId, areaProfile, true);
        if (!_setRulesProfilesHandler.Handle(_history.State, preview).Succeeded) return false;
        var command = preview with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
        return ApplyCommand(_setRulesProfilesHandler.Handle(_history.State, command),
            "Progression, difficulté, perception et combat de zone mis à jour.", null);
    }

    private bool ApplyAreaExperienceProfile(AreaId areaId, AreaExperienceProfile profile)
    {
        if (_history is null) return false;
        var preview = new SetAreaExperienceProfileCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            areaId, profile, true);
        if (!_setAreaExperienceProfileHandler.Handle(_history.State, preview).Succeeded) return false;
        var command = preview with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
        return ApplyCommand(_setAreaExperienceProfileHandler.Handle(_history.State, command),
            "Audio spatial, voix, patrouilles et chargement mis à jour.", null);
    }

    private bool ApplyRuntimeExtensions(ModuleRuntimeExtensionProfile profile)
    {
        if (_history is null) return false;
        var preview = new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            profile, true);
        if (!_setRuntimeExtensionsHandler.Handle(_history.State, preview).Succeeded) return false;
        var command = preview with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
        return ApplyCommand(_setRuntimeExtensionsHandler.Handle(_history.State, command),
            "Règles extensibles, UI scriptable, JSON runtime et snapshots mis à jour.", null);
    }

    private void OpenGlobalSearch() => OpenTool(_globalSearch, () => _globalSearch?.Open());

    private void OpenModuleConfiguration() => OpenTool(_moduleConfiguration, () => _moduleConfiguration?.Open());

    private bool ActivateArea(AreaId areaId)
    {
        if (_history is null || !_activeAreaContext.Activate(_history.State.Project, areaId))
        {
            return _activeAreaContext.Current == areaId;
        }

        var area = _history.State.Project.Areas.Single(candidate => candidate.Id == areaId);
        _statusLabel!.Text = $"✓ Zone active · {area.DisplayName}";
        _detailsLabel!.Text =
            $"{area.Dimensions.Width} × {area.Dimensions.Depth} · {area.Entities.Count} entité(s) · " +
            $"{area.Portals.Count} transition(s)";
        if (_projectSession is not null)
        {
            SaveWorkspaceProfile(EditorWorkspaceProfilePlanner.OpenArea(LoadWorkspaceProfile(), areaId));
        }

        return true;
    }

    private AreaId? CreateArea(
        string displayName,
        AreaKind kind,
        AreaDimensions dimensions,
        AreaTemplateKind template) =>
        CreateArea(displayName, kind, dimensions, template, AreaCreationSettings.CreateDefault(kind));

    private AreaId? CreateArea(
        string displayName,
        AreaKind kind,
        AreaDimensions dimensions,
        AreaTemplateKind template,
        AreaCreationSettings creation)
    {
        if (_history is null)
        {
            return null;
        }

        var areaId = AreaId.From(Guid.NewGuid());
        var spawnBlueprintId = template == AreaTemplateKind.Playable
            ? _history.State.Project.Blueprints.FirstOrDefault(blueprint =>
                blueprint.Kind == BlueprintKind.PlayerSpawn)?.Id
            : null;
        var spawnEntityId = template == AreaTemplateKind.Playable
            ? EntityId.From(Guid.NewGuid())
            : null;
        var command = new CreateAreaCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            areaId,
            displayName,
            kind,
            dimensions,
            template,
            spawnBlueprintId,
            spawnEntityId,
            DryRun: false,
            Foundation: CreateAreaFoundation(areaId, kind, creation));
        if (!ApplyCommand(
                _createAreaHandler.Handle(_history.State, command),
                $"Zone « {displayName} » créée.",
                selectedEntityId: null))
        {
            return null;
        }

        ActivateArea(areaId);
        return areaId;
    }

    private static AreaFoundationProfile CreateAreaFoundation(
        AreaId areaId,
        AreaKind kind,
        AreaCreationSettings creation)
    {
        var defaults = AreaFoundationProfile.CreateDefault(areaId, kind);
        return new(defaults.Id, defaults.SchemaVersion, "author:area-wizard", creation,
            new(defaults.TileKit.Id, defaults.TileKit.SchemaVersion, "author:area-wizard",
                defaults.TileKit.GridSizeMeters, defaults.TileKit.Definitions, defaults.TileKit.ConnectionRules,
                defaults.TileKit.Placements, defaults.TileKit.Groups, defaults.TileKit.Brushes),
            defaults.Lifecycle, defaults.Volumes, defaults.Dependencies);
    }

    private bool UpsertPortal(AreaId sourceAreaId, PortalLink portal)
    {
        if (_history is null)
        {
            return false;
        }

        var command = new UpsertPortalLinkCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            sourceAreaId,
            portal,
            DryRun: false);
        return ApplyCommand(
            _upsertPortalHandler.Handle(_history.State, command),
            $"Transition « {portal.DisplayName} » enregistrée.",
            portal.SourceEntityId);
    }

    private void CopySelectedEntity()
    {
        var selected = GetSelectedEntityIdsForActiveArea();
        if (_history is null || selected.Count == 0)
        {
            return;
        }

        var area = GetActiveArea();
        _semanticClipboard.CopyEntities(
            _history.State.Project,
            area.Id,
            selected,
            provenance: "creator-selection-copy");
        _clipboardSummary!.Text =
            $"Aperçu · {selected.Count} entité(s) · UUID et références remappés au collage";
        _pasteButton!.Disabled = false;
        _statusLabel!.Text = $"✓ {selected.Count} entité(s) copiée(s) dans le clipboard sémantique";
        _detailsLabel!.Text = "Le collage sera prévalidé dans la zone active; choisissez instance ou copie indépendante.";
    }

    private void CutSelectedEntities()
    {
        var selected = GetSelectedEntityIdsForActiveArea();
        if (_history is null || selected.Count == 0)
        {
            return;
        }

        var command = new CutEntitiesCommand(
            CommandId.From(Guid.NewGuid()),
            CutEntitiesHandler.CurrentSchemaVersion,
            "creator-selection-cut",
            _history.State.Revision,
            GetActiveArea().Id,
            selected,
            DryRun: false);
        var preview = _cutEntitiesHandler.Handle(_history.State, command with { DryRun = true });
        if (!preview.Command.Succeeded)
        {
            ApplyCommand(preview.Command, string.Empty, selectedEntityId: null);
            return;
        }

        var applied = _cutEntitiesHandler.Handle(_history.State, command);
        if (applied.Payload is null || !ApplyCommand(
                applied.Command,
                $"{selected.Count} entité(s) coupée(s).",
                selectedEntityId: null))
        {
            return;
        }

        _semanticClipboard.Set(applied.Payload);
        _pasteButton!.Disabled = false;
        _clipboardSummary!.Text = $"{selected.Count} entité(s) coupée(s) · collage transactionnel disponible";
    }

    private void PasteSemanticClipboard()
    {
        if (_history is null || _semanticClipboard.Payload is not EntityClipboardPayload payload)
        {
            return;
        }

        var area = GetActiveArea();
        var mapping = payload.Entities.ToDictionary(
            entity => entity.Id,
            _ => EntityId.From(Guid.NewGuid()));
        var mode = _independentCopy!.ButtonPressed
            ? SemanticPasteMode.IndependentCopy
            : SemanticPasteMode.Instance;
        var blueprintMap = mode == SemanticPasteMode.IndependentCopy
            ? payload.Entities.Select(entity => entity.BlueprintId).Distinct()
                .ToDictionary(id => id, _ => BlueprintId.From(Guid.NewGuid()))
            : null;
        var command = new PasteEntitiesCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            area.Id,
            payload,
            mapping,
            new Vector3Value(GridStep, 0, GridStep),
            DryRun: false,
            Mode: mode,
            BlueprintIdMap: blueprintMap);
        var firstPastedId = mapping[payload.Entities[0].Id];
        if (ApplyCommand(
                _pasteEntitiesHandler.Handle(_history.State, command),
                $"{payload.Entities.Count} entité(s) collée(s) en mode {mode} avec de nouveaux UUID.",
                firstPastedId))
        {
            _clipboardSummary!.Text =
                $"{payload.Entities.Count} entité(s) disponible(s) · dernier collage {mode} appliqué";
        }
    }

    private void DuplicateSelectedEntity()
    {
        CopySelectedEntity();
        PasteSemanticClipboard();
    }

    private AreaId? DuplicateArea(
        AreaId sourceAreaId,
        string displayName,
        AreaCloneMode mode,
        ExternalPortalPolicy externalPortalPolicy)
    {
        if (_history is null)
        {
            return null;
        }

        var source = _history.State.Project.Areas.FirstOrDefault(area => area.Id == sourceAreaId);
        if (source is null)
        {
            return null;
        }

        _semanticClipboard.CopyArea(_history.State.Project, sourceAreaId);
        var destinationAreaId = AreaId.From(Guid.NewGuid());
        var entityMap = mode == AreaCloneMode.Full
            ? source.Entities.Select(entity =>
                new EntityIdRemap(entity.Id, EntityId.From(Guid.NewGuid()))).ToArray()
            : [];
        var portalMap = mode == AreaCloneMode.Full
            ? source.Portals.Select(portal =>
                new PortalIdRemap(portal.Id, PortalId.From(Guid.NewGuid()))).ToArray()
            : [];
        var command = new DuplicateAreaCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            sourceAreaId,
            destinationAreaId,
            displayName,
            mode,
            externalPortalPolicy,
            entityMap,
            portalMap,
            DryRun: false);
        var dryRun = _duplicateAreaHandler.Handle(_history.State, command with { DryRun = true });
        if (!dryRun.Succeeded)
        {
            ApplyCommand(dryRun, string.Empty, selectedEntityId: null);
            return null;
        }

        if (!ApplyCommand(
                _duplicateAreaHandler.Handle(_history.State, command),
                $"Zone « {displayName} » dupliquée avec remapping des UUID.",
                selectedEntityId: null))
        {
            return null;
        }

        _clipboardSummary!.Text = $"Zone « {source.DisplayName} » copiée · {entityMap.Length} UUID remappé(s)";
        ActivateArea(destinationAreaId);
        return destinationAreaId;
    }

    private bool ResizeArea(AreaId areaId, AreaDimensions dimensions, AreaResizeAnchor anchor)
    {
        if (_history is null) return false;
        var area = _history.State.Project.Areas.SingleOrDefault(candidate => candidate.Id == areaId);
        if (area is null) return false;
        var preview = AreaGeometryPlanner.Resize(area, dimensions, anchor);
        var command = new EditAreaGeometryCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            areaId, dimensions, preview.Kept,
            $"Resize area; remove {preview.Removed.Count} out-of-bounds entity(s).", DryRun: false);
        return ApplyCommand(_editAreaGeometryHandler.Handle(_history.State, command),
            $"Zone redimensionnée · {preview.Removed.Count} entité(s) hors limites retirée(s).", null);
    }

    private bool UpsertBlueprint(
        BlueprintId blueprintId,
        string slug,
        string displayName,
        BlueprintKind kind,
        BlueprintAuthoringMetadata authoring)
    {
        if (_history is null)
        {
            return false;
        }

        var command = new UpsertBlueprintCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            blueprintId,
            slug,
            displayName,
            kind,
            DryRun: false,
            Authoring: authoring);
        if (!ApplyCommand(
                _upsertBlueprintHandler.Handle(_history.State, command),
                $"Blueprint « {displayName} » enregistré.",
                GetSelectedEntity()?.Id))
        {
            return false;
        }

        PublishBlueprintSelection(blueprintId);
        _blueprintCatalog?.Refresh(blueprintId);
        return true;
    }

    private bool DeleteAuthoringElement(AuthoringElementKind kind, Guid elementId)
    {
        if (_history is null) return false;
        var preview = new DeleteAuthoringElementCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            kind, elementId, DryRun: true);
        var previewResult = _deleteAuthoringElementHandler.Handle(_history.State, preview);
        if (!previewResult.Succeeded)
            return ApplyCommand(previewResult, string.Empty, null);
        var command = preview with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
        var result = _deleteAuthoringElementHandler.Handle(_history.State, command);
        if (!ApplyCommand(result, $"{kind} supprimé du projet.", null)) return false;
        _blueprintCatalog?.Refresh();
        _dialogueEditor?.Refresh();
        _questJournalWindow?.Refresh();
        _areaWorkspace?.Refresh();
        return true;
    }

    private bool UpdateBlueprintFromEntity(UpdateBlueprintFromEntityCommand command)
    {
        if (_history is null) return false;
        var result = _updateBlueprintFromEntityHandler.Handle(_history.State, command);
        if (command.DryRun) return result.Succeeded;
        if (!ApplyCommand(result, "Blueprint et instances mis à jour depuis l’instance source.",
                command.SourceEntityId)) return false;
        PublishBlueprintSelection(command.BlueprintId);
        _blueprintCatalog?.Refresh(command.BlueprintId);
        return true;
    }

    private bool ConfigureBlueprintPrefab(ConfigureBlueprintPrefabCommand command)
    {
        if (_history is null) return false;
        var result = _configureBlueprintPrefabHandler.Handle(_history.State, command);
        if (command.DryRun) return result.Succeeded;
        if (!ApplyCommand(result, "Variante, composition, collections et budgets du blueprint enregistrés.",
                GetSelectedEntity()?.Id)) return false;
        PublishBlueprintSelection(command.BlueprintId);
        _blueprintCatalog?.Refresh(command.BlueprintId);
        return true;
    }

    private bool PlacePrefabComposition(PlacePrefabCompositionCommand command)
    {
        if (_history is null) return false;
        var result = _placePrefabCompositionHandler.Handle(_history.State, command);
        if (command.DryRun) return result.Succeeded;
        if (!ApplyCommand(result, "Composition placée atomiquement dans la zone.", null)) return false;
        _blueprintCatalog?.Refresh(command.BlueprintId);
        return true;
    }

    private CommandResult PreviewOrApplyRpgBlueprint(
        BlueprintId blueprintId,
        string slug,
        string displayName,
        BlueprintKind kind,
        bool dryRun)
    {
        if (_history is null)
        {
            throw new InvalidOperationException("Aucun projet Forge ouvert.");
        }

        var command = new UpsertBlueprintCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            blueprintId,
            slug,
            displayName,
            kind,
            dryRun);
        var result = _upsertBlueprintHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, $"Blueprint RPG « {displayName} » enregistré.", null);
            _blueprintCatalog?.Refresh(blueprintId);
        }

        return result;
    }

    private CommandResult PreviewOrApplyCreatureFoundation(
        BlueprintId blueprintId,
        CreatureFoundationProfile foundation,
        bool dryRun)
    {
        if (_history is null)
            throw new InvalidOperationException("Aucun projet Forge ouvert.");
        var command = new SetCreatureFoundationCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            blueprintId, foundation, dryRun);
        var result = _creatureFoundationHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, "Fondation de créature enregistrée dans le projet.", null);
            _creatureFoundationWindow?.Refresh(blueprintId);
            _creatureAuthoringCompositionWindow?.Refresh(blueprintId);
            _creatureRulesCompositionWindow?.Refresh(blueprintId);
            _creatureInteractionCompositionWindow?.Refresh(blueprintId);
            _creatureEventAiCompositionWindow?.Refresh(blueprintId);
        }
        return result;
    }

    private CommandResult PreviewOrApplyCreaturePresentation(
        BlueprintId blueprintId,
        CreaturePresentationProfile presentation,
        bool dryRun)
    {
        if (_history is null)
        {
            throw new InvalidOperationException("Aucun projet Forge ouvert.");
        }

        var command = new SetCreaturePresentationCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            blueprintId,
            presentation,
            dryRun);
        var result = _creaturePresentationHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, "Présentation de créature enregistrée dans le projet.", null);
            _creaturePresentationWindow?.Refresh();
        }

        return result;
    }

    private CommandResult PreviewOrApplyCreatureProgression(
        BlueprintId blueprintId,
        CreatureProgressionProfile progression,
        bool dryRun)
    {
        if (_history is null)
        {
            throw new InvalidOperationException("Aucun projet Forge ouvert.");
        }

        var command = new SetCreatureProgressionCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            blueprintId,
            progression,
            dryRun);
        var result = _creatureProgressionHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, "Progression de créature enregistrée dans le projet.", null);
            _creatureProgressionWindow?.Refresh();
        }

        return result;
    }

    private CommandResult PreviewOrApplyCreatureLoot(
        BlueprintId blueprintId,
        CreatureLootProfile loot,
        bool dryRun)
    {
        if (_history is null)
            throw new InvalidOperationException("Aucun projet Forge ouvert.");

        var command = new SetCreatureLootCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            blueprintId,
            loot,
            dryRun);
        var result = _creatureLootHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, "Politique de butin de créature enregistrée dans le projet.", null);
            _creatureLootWindow?.Refresh();
        }

        return result;
    }

    private CommandResult PreviewOrApplyCreatureRoleBehavior(
        BlueprintId blueprintId,
        CreatureRoleBehaviorProfile roleBehavior,
        bool dryRun)
    {
        if (_history is null)
            throw new InvalidOperationException("Aucun projet Forge ouvert.");

        var command = new SetCreatureRoleBehaviorCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            blueprintId,
            roleBehavior,
            dryRun);
        var result = _creatureRoleBehaviorHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, "Rôle, activité et auto-level de créature enregistrés dans le projet.", null);
            _creatureRoleBehaviorWindow?.Refresh();
        }

        return result;
    }

    private CommandResult PreviewOrApplyCreatureEvents(
        BlueprintId blueprintId,
        CreatureEventProfile events,
        bool dryRun)
    {
        if (_history is null)
            throw new InvalidOperationException("Aucun projet Forge ouvert.");

        var command = new SetCreatureEventsCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            blueprintId,
            events,
            dryRun);
        var result = _creatureEventsHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, "Événements avancés de créature enregistrés dans le projet.", null);
            _creatureEventWindow?.Refresh();
        }

        return result;
    }

    private CommandResult PreviewOrApplyQuestOperations(
        QuestId questId,
        QuestOperationsProfile? profile,
        bool dryRun)
    {
        if (_history is null)
            throw new InvalidOperationException("Aucun projet Forge ouvert.");
        var command = new SetQuestOperationsCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            questId, profile, dryRun);
        var result = _questOperationsHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, "Politiques avancées de quête enregistrées dans le projet.", null);
            _questOperationsWindow?.Refresh();
        }
        return result;
    }

    private CommandResult PreviewOrApplyQuestJournal(QuestDefinition quest, bool dryRun)
    {
        if (_history is null)
            throw new InvalidOperationException("Aucun projet Forge ouvert.");
        var command = new SetQuestCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision, quest, dryRun);
        var result = _setQuestHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, $"Quête « {quest.Title.Fallback} » enregistrée.", null);
            _questJournalWindow?.Refresh();
        }
        return result;
    }

    private CommandResult PreviewOrApplyItemAdvanced(
        BlueprintId blueprintId,
        ItemAdvancedProfile? profile,
        bool dryRun)
    {
        if (_history is null)
            throw new InvalidOperationException("Aucun projet Forge ouvert.");
        var command = new SetItemAdvancedProfileCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            blueprintId, profile, dryRun);
        var result = _itemAdvancedHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, "Économie, équipement, butin et craft de l’objet enregistrés.", null);
            _itemAdvancedWindow?.Refresh();
        }
        return result;
    }

    private CommandResult PreviewOrApplyItemFoundation(
        BlueprintId blueprintId,
        ItemFoundationProfile? profile,
        bool acceptTypeMigration,
        bool dryRun)
    {
        if (_history is null)
            throw new InvalidOperationException("Aucun projet Forge ouvert.");
        var command = new SetItemFoundationCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            blueprintId, profile, acceptTypeMigration, dryRun);
        var result = _itemFoundationHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, "Fondation canonique de l’objet enregistrée.", null);
            _itemFoundationWindow?.Refresh(blueprintId);
        }
        return result;
    }

    private CommandResult PreviewOrApplyWorldFeatures(WorldFeatureCatalog catalog, bool dryRun)
    {
        if (_history is null)
            throw new InvalidOperationException("Aucun projet Forge ouvert.");
        var command = new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            catalog, dryRun);
        var result = _worldFeatureHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, "Monde vivant et automatisations enregistrés dans le module.", null);
            _worldFeatureStudioWindow?.Refresh();
        }
        return result;
    }

    private CommandResult PreviewOrApplyPostLaunch(ModuleRuntimeExtensionProfile profile, bool dryRun)
    {
        if (_history is null)
            throw new InvalidOperationException("Aucun projet Forge ouvert.");
        var command = new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            profile, dryRun);
        var result = _setRuntimeExtensionsHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, "Production, plateforme et expérience enregistrées dans le module.", null);
            _postLaunchStudioWindow?.Refresh();
        }
        return result;
    }

    private CommandResult PreviewOrApplyFactions(FactionCatalog catalog, bool dryRun)
    {
        if (_history is null)
            throw new InvalidOperationException("Aucun projet Forge ouvert.");

        var command = new SetFactionCatalogCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            catalog,
            dryRun);
        var result = _factionHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, "Factions avancées enregistrées dans le projet.", null);
            _factionAdvancedWindow?.Refresh();
        }

        return result;
    }

    private CommandResult PreviewOrApplyDialoguePresentation(
        DialogueId dialogueId, DialoguePresentationProfile? presentation, bool dryRun)
    {
        if (_history is null)
            throw new InvalidOperationException("Aucun projet Forge ouvert.");
        var command = new SetDialoguePresentationCommand(CommandId.From(Guid.NewGuid()),
            _history.State.Revision, dialogueId, presentation, dryRun);
        var result = _dialoguePresentationHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, "Mise en scène du dialogue enregistrée dans le projet.", null);
            _dialoguePresentationWindow?.Refresh(dialogueId);
        }
        return result;
    }

    private bool ConfigureBlueprintPalette(
        BlueprintId blueprintId,
        BlueprintPaletteOrigin origin,
        bool isFavorite,
        BlueprintProvenance provenance)
    {
        if (_history is null)
        {
            return false;
        }

        var command = new ConfigureBlueprintPaletteCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            blueprintId,
            origin,
            isFavorite,
            provenance,
            DryRun: false);
        var preview = _configureBlueprintPaletteHandler.Handle(_history.State, command with { DryRun = true });
        if (!preview.Succeeded)
        {
            ApplyCommand(preview, string.Empty, GetSelectedEntity()?.Id);
            return false;
        }

        if (!ApplyCommand(
                _configureBlueprintPaletteHandler.Handle(_history.State, command),
                $"Palette « {FormatPaletteOrigin(origin)} » et provenance enregistrées.",
                GetSelectedEntity()?.Id))
        {
            return false;
        }

        PublishBlueprintSelection(blueprintId);
        _blueprintCatalog?.Refresh(blueprintId);
        return true;
    }

    private bool CreateBlueprintFromSelectedEntity(
        BlueprintId blueprintId,
        string slug,
        string displayName,
        BlueprintAuthoringMetadata? authoring = null)
    {
        var entity = GetSelectedEntity();
        if (_history is null || entity is null)
        {
            return false;
        }

        var area = _history.State.Project.Areas.First(candidate =>
            candidate.Entities.Any(item => item.Id == entity.Id));
        var command = new CreateBlueprintFromEntityCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            area.Id,
            entity.Id,
            blueprintId,
            slug,
            displayName,
            DryRun: false,
            Authoring: authoring);
        if (!ApplyCommand(
                _createBlueprintFromEntityHandler.Handle(_history.State, command),
                $"Blueprint « {displayName} » dérivé de l’instance.",
                entity.Id))
        {
            return false;
        }

        PublishBlueprintSelection(blueprintId);
        _blueprintCatalog?.Refresh(blueprintId);
        return true;
    }

    private void PublishBlueprintSelection(BlueprintId blueprintId)
    {
        var blueprint = _history?.State.Project.Blueprints.FirstOrDefault(candidate => candidate.Id == blueprintId);
        if (blueprint is null)
        {
            return;
        }

        _selectionContext.Replace(new CreatorSelection(
            CreatorSelectionKind.Blueprint,
            blueprint.Id.ToString(),
            blueprint.DisplayName,
            $"blueprints/{blueprint.Id}"));
    }

    private bool ApplySafeRename(SafeRenamePreview preview)
    {
        if (_history is null)
        {
            return false;
        }

        var command = new SafeRenameCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            preview.TargetKind,
            preview.TargetId,
            preview.NewDisplayName,
            preview.NewSlug,
            DryRun: false);
        var dryRun = _safeRenameHandler.Handle(_history.State, command with { DryRun = true });
        if (!dryRun.Succeeded)
        {
            return ApplyCommand(dryRun, string.Empty, selectedEntityId: null);
        }

        var selectedEntityId = GetSelectedEntity()?.Id;
        if (!ApplyCommand(
                _safeRenameHandler.Handle(_history.State, command),
                $"{preview.TargetKind} « {preview.NewDisplayName} » renommé sans modifier son UUID.",
                selectedEntityId))
        {
            return false;
        }

        if (_selectionContext.Current.Count == 1 &&
            _selectionContext.Current[0].Id == preview.TargetId)
        {
            var selection = _selectionContext.Current[0];
            _selectionContext.Replace(new CreatorSelection(
                selection.Kind,
                selection.Id,
                preview.NewDisplayName,
                selection.DocumentPath));
        }

        return true;
    }

    private bool ApplyModuleConfiguration(
        ProjectMetadata metadata,
        IReadOnlyList<ProjectVariableDefinition> variables)
    {
        if (_history is null)
        {
            return false;
        }

        var command = new SetModuleConfigurationCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            metadata,
            variables,
            DryRun: false);
        var dryRun = _setModuleConfigurationHandler.Handle(_history.State, command with { DryRun = true });
        if (!dryRun.Succeeded)
        {
            return ApplyCommand(dryRun, string.Empty, selectedEntityId: null);
        }

        return ApplyCommand(
            _setModuleConfigurationHandler.Handle(_history.State, command),
            $"Configuration du module enregistrée · {metadata.Localizations.Count} langue(s) · " +
            $"{variables.Count} variable(s).",
            GetSelectedEntity()?.Id);
    }

    private bool ApplyLocalizationCatalog(LocalizationCatalog catalog, bool dryRun)
    {
        if (_history is null)
        {
            return false;
        }

        var command = new SetLocalizationCatalogCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            catalog,
            DryRun: dryRun);
        var result = _setLocalizationCatalogHandler.Handle(_history.State, command);
        if (dryRun)
        {
            return result.Succeeded;
        }

        return ApplyCommand(
            result,
            $"Catalogue de localisation appliqué · {catalog.Messages.Count} message(s) · {catalog.Locales.Count} langue(s).",
            GetSelectedEntity()?.Id);
    }

    private bool ApplyModuleDistribution(ModuleDistributionConfiguration distribution)
    {
        if (_history is null)
        {
            return false;
        }

        var command = new SetModuleDistributionCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            distribution,
            DryRun: false);
        var dryRun = _setModuleDistributionHandler.Handle(_history.State, command with { DryRun = true });
        if (!dryRun.Succeeded)
        {
            return ApplyCommand(dryRun, string.Empty, selectedEntityId: null);
        }

        return ApplyCommand(
            _setModuleDistributionHandler.Handle(_history.State, command),
            $"Distribution enregistrée · {distribution.TargetProfiles.Count} cibles · " +
            $"{distribution.Dependencies.Count} dépendance(s).",
            GetSelectedEntity()?.Id);
    }

    private bool ApplyContentWorkspace(ContentWorkspaceConfiguration configuration)
    {
        if (_history is null)
        {
            return false;
        }

        var command = new SetContentWorkspaceCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            configuration,
            DryRun: false);
        var dryRun = _setContentWorkspaceHandler.Handle(_history.State, command with { DryRun = true });
        if (!dryRun.Succeeded)
        {
            return ApplyCommand(dryRun, string.Empty, selectedEntityId: null);
        }

        return ApplyCommand(
            _setContentWorkspaceHandler.Handle(_history.State, command),
            $"Contenu enregistré · {configuration.DataTables.Count} table(s) · " +
            $"fusion {configuration.MergePolicy}.",
            GetSelectedEntity()?.Id);
    }

    private bool ApplyArtisticStyleProfile(ArtisticStyleProfile profile)
    {
        if (_history is null)
        {
            return false;
        }

        var command = new SetArtisticStyleProfileCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            profile,
            DryRun: false);
        var dryRun = _setArtisticStyleProfileHandler.Handle(_history.State, command with { DryRun = true });
        if (!dryRun.Succeeded)
        {
            return ApplyCommand(dryRun, string.Empty, selectedEntityId: null);
        }

        return ApplyCommand(
            _setArtisticStyleProfileHandler.Handle(_history.State, command),
            $"Style artistique enregistré · {profile.Assets.Count} blueprint(s) profilé(s) · " +
            $"{ArtisticCompatibilityValidator.Analyze(profile).Count} avertissement(s).",
            GetSelectedEntity()?.Id);
    }

    private bool ApplyAreaNavigation(AreaId areaId, AreaNavigationDefinition navigation)
    {
        if (_history is null)
        {
            return false;
        }

        var command = new SetAreaNavigationCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            areaId,
            navigation,
            DryRun: false);
        var dryRun = _setAreaNavigationHandler.Handle(_history.State, command with { DryRun = true });
        if (!dryRun.Succeeded)
        {
            return ApplyCommand(dryRun, string.Empty, selectedEntityId: null);
        }

        return ApplyCommand(
            _setAreaNavigationHandler.Handle(_history.State, command),
            $"Navigation enregistrée · {navigation.AgentProfiles.Count} profil(s) · " +
            $"{navigation.OffMeshLinks.Count} lien(s) hors-maillage.",
            GetSelectedEntity()?.Id);
    }

    private bool ApplyDialogue(DialogueDefinition dialogue)
    {
        if (_history is null)
        {
            return false;
        }

        var command = new SetDialogueCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            dialogue,
            DryRun: false);
        return ApplyCommand(
            _setDialogueHandler.Handle(_history.State, command),
            $"Dialogue « {dialogue.DisplayName} » appliqué.",
            GetSelectedEntity()?.Id);
    }

    private bool ApplyQuest(QuestDefinition quest)
    {
        if (_history is null)
        {
            return false;
        }

        var command = new SetQuestCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            quest,
            DryRun: false);
        return ApplyCommand(
            _setQuestHandler.Handle(_history.State, command),
            $"Quête « {quest.DisplayName} » appliquée.",
            GetSelectedEntity()?.Id);
    }

    private void ShowNewModuleDialog()
    {
        if (_newModuleDialog is not null)
        {
            _newModuleDialog.Exclusive = true;
        }

        UpdateNewModuleSummary();
        _newModuleDialog?.PopupCentered(new Vector2I(980, 900));
        if (_newModuleDialog is not null)
        {
            ApplyForgeWindowIcon(_newModuleDialog);
        }
        _newModuleTitle?.GrabFocus();
        _newModuleTitle?.SelectAll();
    }

    private void HideNewModuleDialog()
    {
        _newModuleDialog?.Hide();
        if (_newModuleDialog is not null)
        {
            _newModuleDialog.Exclusive = false;
        }
    }

    private void ChooseNewModuleLocation()
    {
        if (_newModuleTitle is null || _newModuleAreaName is null ||
            string.IsNullOrWhiteSpace(_newModuleTitle.Text) ||
            (_newModuleCreateInitialArea?.ButtonPressed == true &&
             string.IsNullOrWhiteSpace(_newModuleAreaName.Text)))
        {
            _statusLabel!.Text = "✗ Informations incomplètes";
            _detailsLabel!.Text = "Le titre est obligatoire ; le nom de zone l’est lorsque sa création est activée.";
            return;
        }

        var preview = _newModuleHandler.Handle(CreateNewModuleCommand(dryRun: true));
        if (!preview.Succeeded)
        {
            _statusLabel!.Text = "✗ Fondation de projet invalide";
            _detailsLabel!.Text = string.Join("\n", preview.Diagnostics.Select(item => item.Message));
            return;
        }

        UpdateNewModuleSummary();
        HideNewModuleDialog();
        _newModuleLocationDialog!.CurrentDir = _projectSession is null
            ? System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments)
            : Directory.GetParent(_projectSession.RootDirectory)?.FullName ?? _projectSession.RootDirectory;
        _newModuleLocationDialog.PopupCenteredRatio(0.72f);
        ApplyForgeWindowIcon(_newModuleLocationDialog);
    }

    private void NewModuleAreaKindSelected(long index)
    {
        if (_newModuleAreaKind is null || _newModuleAreaWidth is null || _newModuleAreaDepth is null) return;
        var exterior = (AreaKind)_newModuleAreaKind.GetItemId((int)index) == AreaKind.Exterior;
        var suggested = exterior ? 128 : 32;
        _newModuleAreaWidth.Value = suggested;
        _newModuleAreaDepth.Value = suggested;
        UpdateNewModuleSummary();
    }

    private void NewModuleTemplateSelected(long index)
    {
        if (_newModuleTemplate is null || _newModuleCreateInitialArea is null) return;
        var template = (ProjectTemplateKind)_newModuleTemplate.GetItemId((int)index);
        _newModuleCreateInitialArea.Disabled = template is ProjectTemplateKind.Rpg or ProjectTemplateKind.CoreMenu;
        _newModuleCreateInitialArea.ButtonPressed = template == ProjectTemplateKind.Rpg;
        if (template == ProjectTemplateKind.CoreMenu) _newModuleKits!.Text = "forge-core";
        if (template == ProjectTemplateKind.Rpg) _newModuleKits!.Text = "forge-core, forge-rpg-kit";
        SetNewModuleAreaControls(_newModuleCreateInitialArea.ButtonPressed);
        UpdateNewModuleSummary();
    }

    private void NewModuleCreateInitialAreaToggled(bool enabled)
    {
        SetNewModuleAreaControls(enabled);
        UpdateNewModuleSummary();
    }

    private void SetNewModuleAreaControls(bool enabled)
    {
        if (_newModuleAreaName is not null) _newModuleAreaName.Editable = enabled;
        if (_newModuleAreaKind is not null) _newModuleAreaKind.Disabled = !enabled;
        if (_newModuleAreaWidth is not null) _newModuleAreaWidth.Editable = enabled;
        if (_newModuleAreaDepth is not null) _newModuleAreaDepth.Editable = enabled;
        if (_newModuleGameMode is not null) _newModuleGameMode.Editable = enabled;
        if (_newModuleEntryClass is not null) _newModuleEntryClass.Editable = enabled;
        if (_newModulePlayerRule is not null) _newModulePlayerRule.Editable = enabled;
    }

    private void UpdateNewModuleSummary()
    {
        if (_newModuleSummary is null || _newModuleTemplate is null || _newModulePlatform is null ||
            _newModuleRender is null || _newModuleNetwork is null) return;
        var area = _newModuleCreateInitialArea?.ButtonPressed == true
            ? $"zone {_newModuleAreaKind?.GetItemText(_newModuleAreaKind.Selected)} " +
              $"{(int)(_newModuleAreaWidth?.Value ?? 0)} × {(int)(_newModuleAreaDepth?.Value ?? 0)}"
            : "aucune zone initiale";
        var start = _newModuleCreateInitialArea?.ButtonPressed == true
            ? $" · départ {_newModuleGameMode?.Text}/{_newModuleEntryClass?.Text} ({_newModulePlayerRule?.Text})"
            : string.Empty;
        _newModuleSummary.Text =
            $"Résumé · {_newModuleTemplate.GetItemText(_newModuleTemplate.Selected)} · " +
            $"{_newModulePlatform.GetItemText(_newModulePlatform.Selected)} · " +
            $"rendu {_newModuleRender.GetItemText(_newModuleRender.Selected)} · " +
            $"réseau {_newModuleNetwork.GetItemText(_newModuleNetwork.Selected)} · {area}{start}";
    }

    private void CreateNewModuleIn(string parentDirectory)
    {
        var outcome = CreateNewModuleWorkspace(
            parentDirectory,
            _newModuleTitle!.Text,
            _newModuleDescription!.Text,
            _newModuleAreaName!.Text,
            (AreaKind)_newModuleAreaKind!.GetSelectedId(),
            new AreaDimensions((int)_newModuleAreaWidth!.Value, (int)_newModuleAreaDepth!.Value),
            (ProjectTemplateKind)_newModuleTemplate!.GetSelectedId(),
            ParseWizardKits(_newModuleKits!.Text),
            [(ModuleDistributionPlatform)_newModulePlatform!.GetSelectedId()],
            _newModuleRules!.Text.Trim(),
            (ProjectRenderPreset)_newModuleRender!.GetSelectedId(),
            (ProjectNetworkMode)_newModuleNetwork!.GetSelectedId(),
            _newModulePackageNamespace!.Text.Trim(),
            _newModuleGameMode!.Text.Trim(),
            _newModuleEntryClass!.Text.Trim(),
            _newModulePlayerRule!.Text.Trim(),
            _newModuleCreateInitialArea!.ButtonPressed);
        if (!outcome.Succeeded || outcome.WorkspaceDirectory is null)
        {
            _statusLabel!.Text = "✗ Création du module impossible";
            _detailsLabel!.Text = outcome.ErrorMessage ?? "Une erreur inconnue a interrompu la création.";
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_NEW_MODULE_FAILED {outcome.ErrorMessage}");
            return;
        }

        OpenProjectDirectory(outcome.WorkspaceDirectory);
        _statusLabel!.Text = "✓ Nouveau module prêt à construire";
        var createdArea = _history!.State.Project.Areas.SingleOrDefault();
        _detailsLabel!.Text = createdArea is null
            ? $"{_history.State.Project.Module.DisplayName} · projet Core prêt · aucune zone initiale"
            : $"{_history.State.Project.Module.DisplayName} · {createdArea.Kind} " +
              $"{createdArea.Dimensions.Width} × {createdArea.Dimensions.Depth} · point de départ créé";
        GD.Print($"OPENRPG_FORGE_TOOLSET_NEW_MODULE_CREATED path={outcome.WorkspaceDirectory}");
    }

    private NewModuleCreationOutcome CreateNewModuleWorkspace(
        string parentDirectory,
        string title,
        string description,
        string areaDisplayName,
        AreaKind areaKind = AreaKind.Interior,
        AreaDimensions? dimensions = null,
        ProjectTemplateKind template = ProjectTemplateKind.Rpg,
        IReadOnlyList<string>? kits = null,
        IReadOnlyList<ModuleDistributionPlatform>? targetPlatforms = null,
        string rulesProvider = "forge.generic",
        ProjectRenderPreset renderPreset = ProjectRenderPreset.Balanced,
        ProjectNetworkMode networkMode = ProjectNetworkMode.Offline,
        string packageNamespace = "",
        string gameMode = "campaign",
        string entryClass = "adventurer",
        string playerSelectionRule = "default",
        bool? createInitialArea = null)
    {
        var result = _newModuleHandler.Handle(new NewModuleCommand(
            CommandId.From(Guid.NewGuid()),
            new ProjectRevision(0),
            ProjectId.From(Guid.NewGuid()),
            Forge.Domain.Modules.ModuleId.From(Guid.NewGuid()),
            Forge.Domain.Areas.AreaId.From(Guid.NewGuid()),
            BlueprintId.From(Guid.NewGuid()),
            EntityId.From(Guid.NewGuid()),
            BlueprintId.From(Guid.NewGuid()),
            EntityId.From(Guid.NewGuid()),
            title,
            description,
            areaDisplayName,
            DryRun: false,
            AreaKind: areaKind,
            Dimensions: dimensions,
            Template: template,
            Kits: kits,
            TargetPlatforms: targetPlatforms,
            RulesProvider: rulesProvider,
            RenderPreset: renderPreset,
            NetworkMode: networkMode,
            PackageNamespace: packageNamespace,
            GameMode: gameMode,
            EntryClass: entryClass,
            PlayerSelectionRule: playerSelectionRule,
            CreateInitialArea: createInitialArea));
        if (!result.Succeeded || result.State is null)
        {
            return new NewModuleCreationOutcome(
                false,
                null,
                string.Join("\n", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }

        var workspaceDirectory = Path.Combine(
            Path.GetFullPath(parentDirectory),
            result.State.Project.Module.Slug);
        var creation = ProjectSession.Create(workspaceDirectory, result.State.Project);
        if (!creation.Succeeded)
        {
            return new NewModuleCreationOutcome(
                false,
                null,
                FormatDiagnostics(creation.Diagnostics));
        }

        return new NewModuleCreationOutcome(true, workspaceDirectory, null);
    }

    private NewModuleCommand CreateNewModuleCommand(bool dryRun) => new(
        CommandId.From(Guid.NewGuid()),
        new ProjectRevision(0),
        ProjectId.From(Guid.NewGuid()),
        Forge.Domain.Modules.ModuleId.From(Guid.NewGuid()),
        Forge.Domain.Areas.AreaId.From(Guid.NewGuid()),
        BlueprintId.From(Guid.NewGuid()),
        EntityId.From(Guid.NewGuid()),
        BlueprintId.From(Guid.NewGuid()),
        EntityId.From(Guid.NewGuid()),
        _newModuleTitle!.Text,
        _newModuleDescription!.Text,
        _newModuleAreaName!.Text,
        dryRun,
        (AreaKind)_newModuleAreaKind!.GetSelectedId(),
        new AreaDimensions((int)_newModuleAreaWidth!.Value, (int)_newModuleAreaDepth!.Value),
        (ProjectTemplateKind)_newModuleTemplate!.GetSelectedId(),
        ParseWizardKits(_newModuleKits!.Text),
        [(ModuleDistributionPlatform)_newModulePlatform!.GetSelectedId()],
        _newModuleRules!.Text.Trim(),
        (ProjectRenderPreset)_newModuleRender!.GetSelectedId(),
        (ProjectNetworkMode)_newModuleNetwork!.GetSelectedId(),
        _newModulePackageNamespace!.Text.Trim(),
        _newModuleGameMode!.Text.Trim(),
        _newModuleEntryClass!.Text.Trim(),
        _newModulePlayerRule!.Text.Trim(),
        _newModuleCreateInitialArea!.ButtonPressed);

    private static IReadOnlyList<string> ParseWizardKits(string value) => value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.Ordinal)
        .ToArray();


}
#endif
