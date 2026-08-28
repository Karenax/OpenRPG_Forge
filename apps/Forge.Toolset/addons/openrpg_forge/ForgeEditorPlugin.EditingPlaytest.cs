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
    private EntityId? PlaceSelectedBlueprint()
    {
        if (_history is null || _blueprintPalette is null ||
            _positionX is null || _positionZ is null ||
            _blueprintPalette.Selected < 0 ||
            _blueprintPalette.Selected >= _paletteBlueprintIds.Count)
        {
            return null;
        }

        var area = GetActiveArea();
        var entityId = EntityId.From(Guid.NewGuid());
        var transform = new TransformValue(
            new Vector3Value(SnapToGrid(_positionX.Value), 0, SnapToGrid(_positionZ.Value)),
            QuaternionValue.Identity,
            Vector3Value.One);
        var blueprintId = _paletteBlueprintIds[_blueprintPalette.Selected];
        var assetPlacement = ResolveAssetPlacement(blueprintId);
        var command = new PlaceEntityCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            area.Id,
            entityId,
            blueprintId,
            transform,
            dryRun: false,
            assetPlacement.Tags,
            assetPlacement.Overrides);
        var result = _placeHandler.Handle(_history.State, command);
        return ApplyCommand(result, "Entité placée sur la grille.", entityId) ? entityId : null;
    }

    private EntityId? PlaceBlueprintFromViewport(BlueprintId blueprintId, Vector3Value position)
    {
        if (_history is null)
        {
            return null;
        }

        var area = GetActiveArea();
        var entityId = EntityId.From(Guid.NewGuid());
        var assetPlacement = ResolveAssetPlacement(blueprintId);
        var command = new PlaceEntityCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            area.Id,
            entityId,
            blueprintId,
            new TransformValue(position, QuaternionValue.Identity, Vector3Value.One),
            dryRun: false,
            assetPlacement.Tags,
            assetPlacement.Overrides);
        return ApplyCommand(
            _placeHandler.Handle(_history.State, command),
            "Blueprint déposé directement dans le Monde.",
            entityId)
                ? entityId
                : null;
    }

    private (IReadOnlyList<string> Tags, EntityPropertyOverrides Overrides) ResolveAssetPlacement(
        BlueprintId blueprintId)
    {
        var blueprint = _history?.State.Project.Blueprints.SingleOrDefault(candidate => candidate.Id == blueprintId);
        if (blueprint is null || !AssetBlueprintFactory.TryGetAssetId(blueprint, out var assetId) || assetId is null)
        {
            return ([], EntityPropertyOverrides.Empty);
        }

        var manifest = _assetPackageManifestHistory?.State.Find(assetId);
        if (manifest is null)
        {
            return ([], EntityPropertyOverrides.Empty);
        }

        var tags = new[] { "asset" }
            .Concat(manifest.ModelMetadata?.Styles ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var overrides = new EntityPropertyOverrides(
            EntityPropertyOverrides.CurrentSchemaVersion,
            [new EntityPropertyOverride("visual.asset", EntityPropertyValueKind.String, assetId.ToString())]);
        return (tags, overrides);
    }

    private bool TransformEntityFromViewport(EntityId entityId, TransformValue transform)
    {
        if (_history is null)
        {
            return false;
        }

        var current = _history.State.Project.Areas.SelectMany(candidate => candidate.Entities)
            .FirstOrDefault(candidate => candidate.Id == entityId);
        if (current is not null && current.Transform.Position == transform.Position &&
            current.Transform.Rotation == transform.Rotation && current.Transform.Scale != transform.Scale)
        {
            return ApplyScaleCommand([entityId], transform.Scale, multiply: false);
        }

        var area = _history.State.Project.Areas.FirstOrDefault(candidate =>
            candidate.Entities.Any(entity => entity.Id == entityId));
        if (area is null)
        {
            return false;
        }

        var command = new TransformEntityCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            area.Id,
            entityId,
            transform,
            DryRun: false);
        return ApplyCommand(
            _transformHandler.Handle(_history.State, command),
            "Entité déplacée avec le gizmo du Monde.",
            entityId);
    }

    private void SelectEntityFromViewport(EntityId entityId)
    {
        if (_entityPicker is null)
        {
            return;
        }

        var index = _entityIds.FindIndex(id => id == entityId);
        if (index < 0)
        {
            return;
        }

        _entityPicker.Selected = index;
        RefreshOverrideSummary();
        var settings = LoadWorkspaceProfile().Shell.DirectManipulation.Selection;
        var additive = settings.MultiSelectionEnabled && Input.IsKeyPressed(Key.Ctrl);
        PublishSelectedEntity(additive);
    }

    private void ShowViewportPlacementFeedback(string message, bool valid)
    {
        if (_statusLabel is null || _detailsLabel is null)
        {
            return;
        }

        _statusLabel.Text = valid ? "✓ Placement direct valide" : "✗ Placement direct refusé";
        _detailsLabel.Text = message;
        if (!valid)
        {
            var target = _selectionContext.Current.Count == 1 ? _selectionContext.Current[0] : null;
            var code = message.Split('·', 2, StringSplitOptions.TrimEntries)[0];
            _creatorShell?.SetDiagnostics([CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error,
                code,
                message,
                target?.DocumentPath ?? "areas/active/placement",
                "viewport:placement",
                target)]);
        }
    }

    private void PlaceSelectedBlueprintFromUi() => PlaceSelectedBlueprint();

    private void MoveSelectedNegativeX() => MoveSelected(-GridStep, 0);

    private void MoveSelectedPositiveX() => MoveSelected(GridStep, 0);

    private void MoveSelectedNegativeZ() => MoveSelected(0, -GridStep);

    private void MoveSelectedPositiveZ() => MoveSelected(0, GridStep);

    private void MoveSelected(float deltaX, float deltaZ)
    {
        var selected = GetSelectedEntityIdsForActiveArea();
        if (_history is null || selected.Count == 0)
        {
            return;
        }
        ApplyDirectManipulation(selected, EditorTransformTool.Translate,
            new Vector3Value(deltaX, 0, deltaZ), EditorTransformAlignment.None,
            selected.Count == 1 ? "Entité déplacée sur la grille." : $"{selected.Count} entités déplacées sur la grille.");
    }

    private void RotateSelected()
    {
        var selected = GetSelectedEntityIdsForActiveArea();
        if (_history is null || selected.Count == 0)
        {
            return;
        }
        ApplyDirectManipulation(selected, EditorTransformTool.Rotate,
            new Vector3Value(0, 90, 0), EditorTransformAlignment.None,
            selected.Count == 1 ? "Entité tournée de 90°." : $"{selected.Count} entités tournées de 90°.");
    }

    private IReadOnlyList<EntityId> GetSelectedEntityIdsForActiveArea()
    {
        var active = GetActiveArea().Entities.Select(entity => entity.Id).ToHashSet();
        var selected = _selectionContext.Current
            .Where(item => item.Kind == CreatorSelectionKind.Entity && Guid.TryParse(item.Id, out _))
            .Select(item => EntityId.From(Guid.Parse(item.Id)))
            .Where(active.Contains)
            .Distinct()
            .ToArray();
        if (selected.Length > 0) return selected;
        var fallback = GetSelectedEntity();
        return fallback is null ? [] : [fallback.Id];
    }

    private bool ApplyDirectManipulation(
        IReadOnlyList<EntityId> entityIds,
        EditorTransformTool tool,
        Vector3Value value,
        EditorTransformAlignment alignment,
        string successMessage)
    {
        if (_history is null || entityIds.Count == 0) return false;
        var configuration = LoadWorkspaceProfile().Shell.DirectManipulation.Transform;
        var command = new ManipulateEntitiesCommand(
            CommandId.From(Guid.NewGuid()), ManipulateEntitiesHandler.CurrentSchemaVersion,
            "creator-direct-manipulation", _history.State.Revision, GetActiveArea().Id, entityIds,
            tool, value, configuration.CoordinateSpace, configuration.PivotMode, alignment,
            configuration.TranslationSnap, configuration.RotationSnapDegrees, configuration.ScaleSnap,
            DryRun: false);
        var preview = _manipulateEntitiesHandler.Handle(_history.State, command with { DryRun = true });
        if (!preview.Succeeded) return ApplyCommand(preview, string.Empty, entityIds.Count == 1 ? entityIds[0] : null);
        return ApplyCommand(_manipulateEntitiesHandler.Handle(_history.State, command), successMessage,
            entityIds.Count == 1 ? entityIds[0] : null);
    }

    private void DeleteSelected()
    {
        var selected = GetSelectedEntity();
        if (_history is null || selected is null)
        {
            return;
        }

        var area = GetActiveArea();
        var command = new RemoveEntityCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            area.Id,
            selected.Id,
            DryRun: false);
        ApplyCommand(
            _removeHandler.Handle(_history.State, command),
            "Entité supprimée.",
            selectedEntityId: null);
    }

    private void Undo()
    {
        if (_history is null)
        {
            return;
        }

        var selectedEntityId = GetSelectedEntity()?.Id;
        var result = _history.Undo();
        if (!result.Succeeded)
        {
            return;
        }

        _activeAreaContext.EnsureValid(_history.State.Project);
        RebuildPreview();
        RefreshEditorControls(selectedEntityId);
        _creaturePresentationWindow?.Refresh();
        PersistProjectCommandJournal();
        ShowEditStatus("Modification annulée.");
    }

    private void Redo()
    {
        if (_history is null)
        {
            return;
        }

        var selectedEntityId = GetSelectedEntity()?.Id;
        var result = _history.Redo();
        if (!result.Succeeded)
        {
            return;
        }

        _activeAreaContext.EnsureValid(_history.State.Project);
        RebuildPreview();
        RefreshEditorControls(selectedEntityId);
        _creaturePresentationWindow?.Refresh();
        PersistProjectCommandJournal();
        ShowEditStatus("Modification rétablie.");
    }

    private bool ApplyCommand(
        CommandResult result,
        string successMessage,
        EntityId? selectedEntityId)
    {
        if (_history is null)
        {
            return false;
        }

        if (!result.Succeeded)
        {
            _statusLabel!.Text = "✗ Commande refusée";
            _detailsLabel!.Text = string.Join(
                "\n",
                result.Diagnostics.Select(diagnostic => $"{diagnostic.Code} — {diagnostic.Message}"));
            var currentTarget = _selectionContext.Current.Count == 1 ? _selectionContext.Current[0] : null;
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(diagnostic => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error,
                diagnostic.Code,
                diagnostic.Message,
                $"commands/{result.Audit.CommandId}",
                result.Audit.CommandId.ToString(),
                currentTarget)).ToArray());
            return false;
        }

        RecordProjectCommand(result);
        _activeAreaContext.EnsureValid(_history.State.Project);
        RebuildPreview();
        RefreshEditorControls(selectedEntityId);
        ShowEditStatus(successMessage);
        GD.Print($"OPENRPG_FORGE_TOOLSET_COMMAND_APPLIED operation={result.Patch?.Operation} revision={_history.State.Revision}");
        return true;
    }

    public void ApplyNativeProjectUndo()
    {
        if (_nativeUndoRedoCallback) return;
        _nativeUndoRedoCallback = true;
        try { Undo(); }
        finally { _nativeUndoRedoCallback = false; }
    }

    public void ApplyNativeProjectRedo()
    {
        if (_nativeUndoRedoCallback) return;
        _nativeUndoRedoCallback = true;
        try { Redo(); }
        finally { _nativeUndoRedoCallback = false; }
    }

    private void RecordProjectCommand(CommandResult result)
    {
        if (_history is null) throw new InvalidOperationException("No project command history is active.");
        _history.Record(result);
        PersistProjectCommandJournal();
        if (_nativeUndoRedo is null || _nativeUndoRedoCallback) return;
        var actionName = string.IsNullOrWhiteSpace(result.Patch?.Summary)
            ? $"OpenRPG Forge · {result.Audit.CommandName}"
            : $"OpenRPG Forge · {result.Patch.Summary}";
        _nativeUndoRedo.CreateAction(actionName, UndoRedo.MergeMode.Disable, this, false, true);
        _nativeUndoRedo.AddDoMethod(this, new StringName(nameof(ApplyNativeProjectRedo)));
        _nativeUndoRedo.AddUndoMethod(this, new StringName(nameof(ApplyNativeProjectUndo)));
        _nativeUndoRedo.CommitAction(false);
        _nativeUndoRedoRegistrationCount++;
    }

    private void PersistProjectCommandJournal()
    {
        if (_projectSession is null || _history is null) return;
        try
        {
            EditorCommandJournalStore.Save(_projectSession.RootDirectory, _history.Journal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            GD.PrintErr($"OPENRPG_FORGE_COMMAND_JOURNAL_SAVE_FAILED {exception.Message}");
        }
    }

    private void SaveProject()
    {
        if (_projectSession is null || _history is null || _statusLabel is null ||
            _detailsLabel is null || _saveButton is null)
        {
            return;
        }

        _saveButton.Disabled = true;
        var result = _projectSession.Save(_history.State.Project);
        _saveButton.Disabled = false;

        if (!result.Succeeded)
        {
            _statusLabel.Text = "✗ Échec de la sauvegarde";
            _detailsLabel.Text = FormatDiagnostics(result.Diagnostics);
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(MapSessionDiagnostic).ToArray());
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_PROJECT_SAVE_FAILED {result.Diagnostics.FirstOrDefault()?.Code}");
            return;
        }

        _statusLabel.Text = "✓ Projet sauvegardé de façon atomique";
        _detailsLabel.Text = $"{_history.State.Project.Title} — aucune écriture partielle";
        _lastSavedRevision = _history.State.Revision.Value;
        GD.Print("OPENRPG_FORGE_TOOLSET_PROJECT_SAVED");
    }

    private void RefreshEditorControls(EntityId? selectedEntityId = null)
    {
        if (_blueprintPalette is null || _entityPicker is null || _spawnPicker is null ||
            _placeButton is null || _revisionLabel is null ||
            _undoButton is null || _redoButton is null || _playButton is null ||
            _overridesButton is null || _overridesSummary is null || _dialoguesButton is null ||
            _areasButton is null || _searchButton is null || _moduleSettingsButton is null ||
            _copyButton is null || _cutButton is null || _pasteButton is null ||
            _duplicateButton is null || _independentCopy is null || _clipboardSummary is null)
        {
            return;
        }

        var selectedSpawnId = GetSelectedSpawnId();
        _paletteBlueprintIds.Clear();
        _blueprintPalette.Clear();
        _entityIds.Clear();
        _entityPicker.Clear();
        _spawnEntityIds.Clear();
        _spawnPicker.Clear();

        if (_history is null)
        {
            SetEditingEnabled(false);
            _revisionLabel.Text = "Révision —";
            _creatorShell?.SetProject(null);
            return;
        }

        var project = _history.State.Project;
        foreach (var blueprint in project.Blueprints
                     .OrderBy(blueprint => blueprint.Kind == BlueprintKind.Placeable ? 0 : 1)
                     .ThenBy(blueprint => blueprint.DisplayName, StringComparer.Ordinal))
        {
            _paletteBlueprintIds.Add(blueprint.Id);
            var favorite = blueprint.IsFavorite ? "★ " : string.Empty;
            _blueprintPalette.AddItem($"{favorite}{blueprint.DisplayName} · {FormatPaletteOrigin(blueprint.PaletteOrigin)}");
        }

        var blueprintNames = project.Blueprints.ToDictionary(blueprint => blueprint.Id, blueprint => blueprint.DisplayName);
        var activeArea = GetActiveArea();
        foreach (var entity in activeArea.Entities)
        {
            _entityIds.Add(entity.Id);
            var overrideBadge = entity.Overrides.Properties.Count > 0
                ? $" · {entity.Overrides.Properties.Count} prop."
                : string.Empty;
            _entityPicker.AddItem(
                $"{blueprintNames[entity.BlueprintId]} · {entity.Id.ToString()[..8]}{overrideBadge}");
        }

        var spawnBlueprintIds = project.Blueprints
            .Where(blueprint => blueprint.Kind == BlueprintKind.PlayerSpawn)
            .Select(blueprint => blueprint.Id)
            .ToHashSet();
        foreach (var area in project.Areas)
        {
            foreach (var entity in area.Entities.Where(entity => spawnBlueprintIds.Contains(entity.BlueprintId)))
            {
                _spawnEntityIds.Add(entity.Id);
                var position = entity.Transform.Position;
                _spawnPicker.AddItem($"{area.DisplayName} · X {position.X:0.##} / Z {position.Z:0.##}");
            }
        }

        if (selectedSpawnId is not null)
        {
            var spawnIndex = _spawnEntityIds.FindIndex(id => id == selectedSpawnId);
            if (spawnIndex >= 0)
            {
                _spawnPicker.Selected = spawnIndex;
            }
        }

        if (selectedEntityId is not null)
        {
            var selectedIndex = _entityIds.FindIndex(id => id == selectedEntityId);
            if (selectedIndex >= 0)
            {
                _entityPicker.Selected = selectedIndex;
            }
        }

        SetEditingEnabled(true);
        _revisionLabel.Text = $"Révision {_history.State.Revision}";
        _undoButton.Disabled = !_history.CanUndo;
        _redoButton.Disabled = !_history.CanRedo;
        _playButton.Disabled = _playtestRunning || _spawnEntityIds.Count == 0;
        RefreshOverrideSummary();
        RefreshScaleControls();
        RefreshCreatorShell();
        _blueprintCatalog?.Refresh();
        _areaWorkspace?.Refresh();
        _globalSearch?.Refresh();
        _moduleConfiguration?.Refresh();
        _distributionProfiles?.Refresh();
        _contentWorkflow?.Refresh();
        _contentPackageWindow?.Refresh();
        PublishSelectedEntity(frameSelection: false);
    }

    private void SetEditingEnabled(bool enabled)
    {
        var hasEntity = enabled && _entityIds.Count > 0;
        _blueprintPalette!.Disabled = !enabled || _paletteBlueprintIds.Count == 0;
        _placeButton!.Disabled = _blueprintPalette.Disabled;
        _entityPicker!.Disabled = !hasEntity;
        _moveXNegativeButton!.Disabled = !hasEntity;
        _moveXPositiveButton!.Disabled = !hasEntity;
        _moveZNegativeButton!.Disabled = !hasEntity;
        _moveZPositiveButton!.Disabled = !hasEntity;
        _rotateButton!.Disabled = !hasEntity;
        _deleteButton!.Disabled = !hasEntity;
        _scaleX!.Editable = hasEntity;
        _scaleY!.Editable = hasEntity;
        _scaleZ!.Editable = hasEntity;
        _uniformScale!.Disabled = !hasEntity;
        _scaleDownButton!.Disabled = !hasEntity;
        _scaleUpButton!.Disabled = !hasEntity;
        _applyScaleButton!.Disabled = !hasEntity;
        _overridesButton!.Disabled = !hasEntity;
        _copyButton!.Disabled = !hasEntity;
        _cutButton!.Disabled = !hasEntity;
        _duplicateButton!.Disabled = !hasEntity;
        _independentCopy!.Disabled = !enabled;
        _pasteButton!.Disabled = !enabled || _semanticClipboard.Payload is not EntityClipboardPayload;
        if (!hasEntity)
        {
            _overridesSummary!.Text = "Sélectionnez une entité";
        }
        _undoButton!.Disabled = true;
        _redoButton!.Disabled = true;
        _spawnPicker!.Disabled = !enabled || _spawnEntityIds.Count == 0;
        _playButton!.Disabled = !enabled || _spawnEntityIds.Count == 0 || _playtestRunning;
        _dialoguesButton!.Disabled = !enabled;
        _areasButton!.Disabled = !enabled;
        _workspaceButton!.Disabled = !enabled;
        _recoveryButton!.Disabled = !enabled;
        _qualityButton!.Disabled = !enabled;
        _searchButton!.Disabled = !enabled;
        _moduleSettingsButton!.Disabled = !enabled;
        _exportButton!.Disabled = !enabled;
    }

    private void RefreshOverrideSummary()
    {
        if (_overridesSummary is null)
        {
            return;
        }

        var selected = GetSelectedEntity();
        if (selected is null)
        {
            _overridesSummary.Text = "Sélectionnez une entité";
            return;
        }

        var count = selected.Overrides.Properties.Count;
        _overridesSummary.Text = count == 0
            ? "Hérite entièrement du blueprint · schéma v1"
            : $"{count} propriété(s) personnalisée(s) · schéma v{selected.Overrides.SchemaVersion}";
    }

    private void RefreshCreatorShell()
    {
        if (_creatorShell is null || _history is null || _projectSession is null || _projectPreview is null)
        {
            return;
        }

        var project = _history.State.Project;
        var profile = LoadWorkspaceProfile();
        _creatorShell.SetProject(CreatorProjectExplorer.Build(project, profile.Shell.ExplorerGrouping));
        _creatorShell.ApplyWorkspaceProfile(profile);
        _projectPreview?.SetEditorMode(profile.Shell.ViewportMode);
        _creatorShell.SetDiagnostics(CreatorDiagnostics.FromProject(
            project,
            $"revision:{_history.State.Revision}"));
    }

    private void PublishSelectedEntity(bool additive = false, bool frameSelection = true)
    {
        var entity = GetSelectedEntity();
        if (_history is null || entity is null)
        {
            return;
        }

        var project = _history.State.Project;
        var area = project.Areas.First(candidate => candidate.Entities.Any(item => item.Id == entity.Id));
        var blueprintName = project.Blueprints
            .FirstOrDefault(blueprint => blueprint.Id == entity.BlueprintId)
            ?.DisplayName ?? "Blueprint manquant";
        var selection = new CreatorSelection(
            CreatorSelectionKind.Entity,
            entity.Id.ToString(),
            blueprintName,
            $"areas/{area.Id}/entities/{entity.Id}");
        var settings = LoadWorkspaceProfile().Shell.DirectManipulation.Selection;
        var next = additive
            ? _selectionContext.Current.Concat([selection])
            : [selection];
        _selectionContext.Replace(next);
        if (settings.FocusOnSelection) _selectionContext.Focus(selection, "creator-viewport");
        if (settings.FrameSelection && frameSelection)
        {
            _selectionContext.Frame("creator-viewport");
            _projectPreview?.FocusSelectionFromShortcut();
        }
        _projectPreview?.ShowSelection(entity.Id);
    }

    private static CreatorDiagnostic MapSessionDiagnostic(ProjectSessionDiagnostic diagnostic) =>
        CreatorDiagnostic.Create(
            CreatorDiagnosticSeverity.Error,
            diagnostic.Code,
            diagnostic.Message,
            string.IsNullOrWhiteSpace(diagnostic.Path) ? "workspace" : diagnostic.Path);

    private EntityInstance? GetSelectedEntity()
    {
        if (_history is null || _entityPicker is null ||
            _entityPicker.Selected < 0 || _entityPicker.Selected >= _entityIds.Count)
        {
            return null;
        }

        var entityId = _entityIds[_entityPicker.Selected];
        return GetActiveArea().Entities.Single(entity => entity.Id == entityId);
    }

    private AreaDefinition GetActiveArea()
    {
        if (_history is null)
        {
            throw new InvalidOperationException("No Forge project is open.");
        }

        return _history.State.Project.Areas.FirstOrDefault(area => area.Id == _activeAreaContext.Current) ??
            _history.State.Project.Areas.First();
    }

    private static AreaId PreferredCreatorArea(ProjectDefinition project)
    {
        var entry = project.Areas.FirstOrDefault(area => area.Id == project.Module.EntryAreaId);
        if (entry?.World.Terrain.Enabled == true) return entry.Id;
        return project.Areas
                   .Where(area => area.World.Terrain.Enabled)
                   .OrderByDescending(area => (long)area.Dimensions.Width * area.Dimensions.Depth)
                   .ThenBy(area => area.DisplayName, StringComparer.Ordinal)
                   .FirstOrDefault()?.Id
               ?? entry?.Id
               ?? project.Areas.First().Id;
    }

    private EntityId? GetSelectedSpawnId()
    {
        if (_spawnPicker is null ||
            _spawnPicker.Selected < 0 || _spawnPicker.Selected >= _spawnEntityIds.Count)
        {
            return null;
        }

        return _spawnEntityIds[_spawnPicker.Selected];
    }

    private void StartPlaytestFromUi()
    {
        var configured = _history?.State.Project.Areas
            .FirstOrDefault(item => item.Id == _activeAreaContext.Current)?.Playtest;
        StartPlaytest(headless: configured?.Enabled == true && configured.Headless, workspaceSmokeTest: false);
    }

    private void StartPlaytest(bool headless, bool workspaceSmokeTest)
    {
        var spawnEntityId = GetSelectedSpawnId();
        if (_history is null || _playtestRunning)
        {
            return;
        }
        var activeArea = _history.State.Project.Areas.FirstOrDefault(item => item.Id == _activeAreaContext.Current);
        var configured = activeArea?.Playtest.Enabled == true ? activeArea.Playtest : null;
        if (configured is null && spawnEntityId is null) return;

        PlaytestSnapshot snapshot;
        try
        {
            snapshot = configured is null
                ? _playtestSnapshots.Create(_history.State.Project, spawnEntityId!, _projectSession?.RootDirectory)
                : _playtestSnapshots.Create(
                    _history.State.Project,
                    activeArea!.Id,
                    configured,
                    _projectSession?.RootDirectory);
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or UnauthorizedAccessException or
            NotSupportedException or System.Text.Json.JsonException or
            Forge.ModuleFormat.Serialization.ProjectValidationException)
        {
            _playtestStatus!.Text = "Échec du snapshot";
            _playtestLog!.Text = $"Le snapshot temporaire n’a pas pu être créé.\n{exception.Message}";
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_T32_SNAPSHOT_FAILED {exception.Message}");
            return;
        }

        _playtestRunning = true;
        _playButton!.Disabled = true;
        _spawnPicker!.Disabled = true;
        _playtestStatus!.Text = headless ? "Test automatisé…" : "Module en cours…";
        _playtestLog!.Text =
            $"Snapshot temporaire prêt.\nPoint de départ : {snapshot.SpawnEntityId}" +
            (configured is null ? "\n" : $" · mode {configured.StartMode} · profil {configured.TestProfile}\n") +
            (headless
                ? "Exécution du scénario de validation…"
                : "Le module est ouvert dans une fenêtre séparée. Fermez-la pour afficher le rapport complet.");
        _ = RunPlaytestAsync(snapshot, headless, workspaceSmokeTest);
    }

    private async Task RunPlaytestAsync(
        PlaytestSnapshot snapshot,
        bool headless,
        bool workspaceSmokeTest)
    {
        var result = await PlaytestProcessRunner.RunAsync(
            OS.GetExecutablePath(),
            GetRuntimeProjectDirectory(),
            snapshot,
            headless,
            runSmokeScenario: workspaceSmokeTest);
        string? cleanupError = null;
        try
        {
            _playtestSnapshots.Delete(snapshot);
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            cleanupError = exception.Message;
        }

        var snapshotDeleted = !Directory.Exists(snapshot.DirectoryPath);
        _mainThreadActions.Enqueue(() => CompletePlaytest(
            snapshot,
            result,
            cleanupError,
            snapshotDeleted,
            workspaceSmokeTest));
    }

    private void CompletePlaytest(
        PlaytestSnapshot snapshot,
        PlaytestProcessResult result,
        string? cleanupError,
        bool snapshotDeleted,
        bool workspaceSmokeTest)
    {
        _playtestRunning = false;
        var output = FilterRuntimeBoilerplate(result.StandardOutput);
        var error = FilterRuntimeBoilerplate(result.StandardError);
        var succeeded = result.Succeeded && cleanupError is null && snapshotDeleted;

        if (_playtestStatus is not null && _playtestLog is not null && _playButton is not null && _spawnPicker is not null)
        {
            _playtestStatus.Text = succeeded ? "✓ Test terminé" : "✗ Test interrompu";
            _playtestLog.Text = BuildPlaytestReport(snapshot, result, output, error, cleanupError, snapshotDeleted);
            _playButton.Disabled = _history is null || _spawnEntityIds.Count == 0;
            _spawnPicker.Disabled = _history is null || _spawnEntityIds.Count == 0;
        }

        if (!workspaceSmokeTest)
        {
            return;
        }

        var expectedMarkersPresent = output.Contains(
                "OPENRPG_FORGE_RUNTIME_SNAPSHOT_LOADED file=project.json",
                StringComparison.Ordinal) &&
            output.Contains(
                $"OPENRPG_FORGE_RUNTIME_SPAWN_SELECTED id={snapshot.SpawnEntityId}",
                StringComparison.Ordinal) &&
            output.Contains("OPENRPG_FORGE_RUNTIME_SMOKE_PASS", StringComparison.Ordinal);
        if (!succeeded || !expectedMarkersPresent)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_TOOLSET_T32_SMOKE_FAILED exit={result.ExitCode} snapshot_deleted={snapshotDeleted}\n{output}\n{error}");
            return;
        }

        GD.Print(
            "OPENRPG_FORGE_CREATOR_PRODUCT_JOURNEY_PASS " +
            "project=new-module areas=interior-exterior assets=governed-placement " +
            "rpg=creature-item-door-trigger-encounter-faction narrative=dialogue-quest-script " +
            "validation=quickfix-undo-redo playtest=snapshot-runtime export=orpmod-roundtrip assistant=governed");
        GD.Print(
            $"OPENRPG_FORGE_TOOLSET_T32_SMOKE_PASS spawn={snapshot.SpawnEntityId} snapshot=deleted logs=captured");
    }

    private static string BuildPlaytestReport(
        PlaytestSnapshot snapshot,
        PlaytestProcessResult result,
        string output,
        string error,
        string? cleanupError,
        bool snapshotDeleted)
    {
        var report = new List<string>
        {
            "OpenRPG Forge · Rapport de playtest",
            $"Point de départ : {snapshot.SpawnEntityId}",
            $"Code de sortie : {result.ExitCode}",
            $"Snapshot temporaire : {(snapshotDeleted ? "supprimé" : "à nettoyer")}",
        };

        if (result.LaunchError is not null)
        {
            report.Add($"Lancement : {result.LaunchError}");
        }

        if (cleanupError is not null)
        {
            report.Add($"Nettoyage : {cleanupError}");
        }

        if (!string.IsNullOrWhiteSpace(output))
        {
            report.Add(string.Empty);
            report.Add("Sortie du module");
            report.Add(output.Trim());
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            report.Add(string.Empty);
            report.Add("Diagnostics");
            report.Add(error.Trim());
        }

        return string.Join(System.Environment.NewLine, report);
    }

    private static string FilterRuntimeBoilerplate(string value) => string.Join(
        System.Environment.NewLine,
        value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line =>
                !line.StartsWith("Godot Engine", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("godotengine.org", StringComparison.OrdinalIgnoreCase)));

    private void RebuildPreview()
    {
        if (_history is not null)
        {
            _projectPreview?.Rebuild(_history.State.Project);
            if (_desktopCreatorShell)
            {
                _creatorShellNormalizationFrames = Math.Max(_creatorShellNormalizationFrames, 3);
            }
        }
    }

    private void ShowEditStatus(string message)
    {
        _statusLabel!.Text = $"✓ {message}";
        _detailsLabel!.Text = $"{_projectPreview?.EntityCount ?? 0} entités · révision {_history?.State.Revision}";
    }

    private float GridStep => Math.Max(0.25f, (float)(_gridSize?.Value ?? 1));

    private float SnapToGrid(double value) =>
        MathF.Round((float)value / GridStep) * GridStep;

    private static QuaternionValue MultiplyAndNormalize(QuaternionValue left, QuaternionValue right)
    {
        var x = (left.W * right.X) + (left.X * right.W) + (left.Y * right.Z) - (left.Z * right.Y);
        var y = (left.W * right.Y) - (left.X * right.Z) + (left.Y * right.W) + (left.Z * right.X);
        var z = (left.W * right.Z) + (left.X * right.Y) - (left.Y * right.X) + (left.Z * right.W);
        var w = (left.W * right.W) - (left.X * right.X) - (left.Y * right.Y) - (left.Z * right.Z);
        var length = MathF.Sqrt((x * x) + (y * y) + (z * z) + (w * w));
        return new QuaternionValue(x / length, y / length, z / length, w / length);
    }

    private static string FormatDiagnostics(IReadOnlyList<ProjectSessionDiagnostic> diagnostics) =>
        string.Join("\n", diagnostics.Select(diagnostic => $"{diagnostic.Code} — {diagnostic.Message}"));


}
#endif
