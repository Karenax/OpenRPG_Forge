using Forge.Application.Blueprints;
using Forge.Application.Creator;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    internal bool PrepareUx0DEvidence(string mode)
    {
        SwitchPage("zones", notify: false);
        var source = _session.ActiveArea!.Entities.FirstOrDefault();
        if (source is null)
        {
            return false;
        }
        var blueprintId = source.BlueprintId;
        var current = _session.WorkspaceProfile.RepeatedPlacementProfile;
        var profile = new EditorRepeatedPlacementProfile(
            current.Id,
            EditorRepeatedPlacementProfile.CurrentSchemaVersion,
            "studio:ux0-d-evidence",
            true,
            false,
            false,
            EditorPlacementDistribution.Grid,
            EditorPlacementAlignment.GridPlane,
            1.5f,
            3,
            8152026,
            0);
        var request = FindValidPlacement(blueprintId, profile, count: 6);
        if (request is null)
        {
            return false;
        }

        if (string.Equals(mode, "ux0-d-invalid", StringComparison.OrdinalIgnoreCase))
        {
            BeginAddEntity(source.Transform.Position);
            var invalidRequest = request with
            {
                Origin = source.Transform.Position,
                Count = 1,
                ApplyCorrections = false,
            };
            var invalid = _session.PreviewRepeatedPlacement(invalidRequest);
            _inspectorContent.GetChildren().OfType<StudioAddEntityInspector>().SingleOrDefault()
                ?.ConfigureEvidencePlacement(invalidRequest);
            WorldViewport.ShowPlacementPreview(invalid);
            WorldViewport.FramePlacementPreview(invalid);
            SetStatus(invalid.Message, error: true);
            return !invalid.IsValid && WorldViewport.PlacementGhostCount == 1;
        }

        if (string.Equals(mode, "ux0-d-clipboard", StringComparison.OrdinalIgnoreCase))
        {
            var selected = _session.ActiveArea.Entities.Take(3).Select(entity => entity.Id).ToArray();
            if (!_session.SelectEntities(selected, selected.LastOrDefault()) ||
                !_session.CopySelectedEntities().Succeeded)
            {
                return false;
            }
            BeginAddEntity(request.Origin);
            var inspector = _inspectorContent.GetChildren().OfType<StudioAddEntityInspector>().SingleOrDefault();
            SetStatus("Presse-papiers sémantique · choisissez instances ou copies indépendantes");
            return inspector is not null && _session.ClipboardState.EntityCount == selected.Length;
        }

        if (string.Equals(mode, "ux0-d-brush", StringComparison.OrdinalIgnoreCase))
        {
            var basePreview = _session.PreviewRepeatedPlacement(request);
            var brushProfile = new EditorRepeatedPlacementProfile(
                profile.Id,
                EditorRepeatedPlacementProfile.CurrentSchemaVersion,
                "studio:ux0-d-evidence-brush",
                true,
                true,
                true,
                profile.Distribution,
                profile.Alignment,
                profile.Spacing,
                profile.Columns,
                profile.Seed,
                profile.SurfaceOffset);
            var brush = request with
            {
                Profile = brushProfile,
                BrushSamples = basePreview.Poses.Select(pose => pose.Position).ToArray(),
            };
            var preview = _session.PreviewRepeatedPlacement(brush);
            BeginAddEntity(request.Origin);
            _inspectorContent.GetChildren().OfType<StudioAddEntityInspector>().SingleOrDefault()
                ?.ConfigureEvidencePlacement(brush);
            ScrollInspectorToPlacementControls();
            WorldViewport.ArmPlacementTool(brush);
            WorldViewport.ShowPlacementPreview(preview);
            WorldViewport.FramePlacementPreview(preview);
            return preview.IsValid && preview.Poses.Count == 6 && WorldViewport.IsPlacementToolArmed;
        }

        BeginAddEntity(request.Origin);
        if (string.Equals(mode, "ux0-d-placement", StringComparison.OrdinalIgnoreCase))
        {
            var preview = _session.PreviewRepeatedPlacement(request);
            _inspectorContent.GetChildren().OfType<StudioAddEntityInspector>().SingleOrDefault()
                ?.ConfigureEvidencePlacement(request);
            WorldViewport.ShowPlacementPreview(preview);
            WorldViewport.FramePlacementPreview(preview);
            SetStatus("Aperçu groupé · six objets · une seule annulation");
            return preview.IsValid && preview.Poses.Count == 6 && WorldViewport.PlacementGhostCount == 6;
        }

        var palette = _inspectorContent.GetChildren().OfType<StudioAddEntityInspector>().SingleOrDefault();
        return palette is not null && palette.PaletteViewCount >= 7 && palette.FilterDimensionCount == 6 &&
               palette.PlacementControlsReady;
    }

    private void ScrollInspectorToPlacementControls()
    {
        if (_inspectorContent.GetParent() is not ScrollContainer scroll)
        {
            return;
        }
        scroll.ScrollVertical = 100_000;
        scroll.SetDeferred("scroll_vertical", 100_000);
    }

    internal bool RunUx0DSmoke()
    {
        var originalProject = _session.Project!;
        var originalWorkspace = _session.WorkspaceProfile;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var originalEntityIds = _session.ActiveArea!.Entities.Select(entity => entity.Id).ToHashSet();
        var originalEntityCount = originalEntityIds.Count;
        var originalBlueprintCount = originalProject.Blueprints.Count;
        _session.ClearClipboardForVerification();

        var views = Enum.GetValues<BlueprintPaletteView>();
        var viewCounts = views.ToDictionary(view => view, view => _session.BuildBlueprintPalette(view).Count);
        var allItems = _session.BuildBlueprintPalette(BlueprintPaletteView.All);
        var paletteItem = allItems.FirstOrDefault(item =>
            _session.ActiveArea.Entities.Any(entity => entity.BlueprintId == item.BlueprintId)) ??
            allItems.FirstOrDefault();
        if (paletteItem is null)
        {
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX0D_FAILED reason=palette_empty");
            return false;
        }

        var preciseFilter = new EditorPaletteFilter(
            [paletteItem.Kind],
            [paletteItem.Kit],
            paletteItem.Styles.Take(1).ToArray(),
            [paletteItem.Provenance.LicenseSpdx],
            paletteItem.Compatibility.Take(1).ToArray(),
            paletteItem.Tags.Take(1).ToArray());
        var filterApply = _session.ConfigurePaletteFilter(preciseFilter, persist: false);
        var filterMatched = _session.BuildBlueprintPalette(
            BlueprintPaletteView.All,
            paletteItem.DisplayName,
            preciseFilter).Any(item => item.BlueprintId == paletteItem.BlueprintId);
        var favorite = _session.ToggleBlueprintFavorite(paletteItem.BlueprintId, persist: false);
        var favoriteVisible = _session.BuildBlueprintPalette(BlueprintPaletteView.Favorites)
            .Any(item => item.BlueprintId == paletteItem.BlueprintId);
        var named = _session.SaveNamedPalette(
            "Sélection UX0-D",
            allItems.Take(Math.Min(3, allItems.Count)).Select(item => item.BlueprintId).ToArray(),
            taskScoped: false,
            persist: false);
        var namedCollection = _session.WorkspaceProfile.NamedPaletteCollections
            .FirstOrDefault(collection => collection.Name == "Sélection UX0-D");
        var namedVisible = namedCollection is not null &&
            _session.BuildBlueprintPalette(
                BlueprintPaletteView.All,
                filter: EditorPaletteFilter.ShowAll,
                namedCollectionId: namedCollection.Id).Count == namedCollection.BlueprintIds.Count;

        var baseProfile = _session.WorkspaceProfile.RepeatedPlacementProfile;
        var repeatedProfile = new EditorRepeatedPlacementProfile(
            baseProfile.Id,
            EditorRepeatedPlacementProfile.CurrentSchemaVersion,
            "studio:ux0-d-smoke",
            enabled: true,
            brushMode: false,
            continuousStroke: false,
            EditorPlacementDistribution.Line,
            EditorPlacementAlignment.GridPlane,
            spacing: 1.5f,
            columns: 3,
            seed: 8152026,
            surfaceOffset: 0);
        var repeatedConfigured = _session.ConfigureRepeatedPlacement(repeatedProfile, persist: false);
        var profileRoundTrip = VerifyUx0DProfileRoundTrip(_session.WorkspaceProfile, paletteItem.BlueprintId);

        var exactRequest = FindValidPlacement(paletteItem.BlueprintId, repeatedProfile, count: 3);
        if (exactRequest is null)
        {
            RestoreUx0DState(originalProject, originalWorkspace, originalSelection, originalActive);
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX0D_FAILED reason=no_free_series");
            return false;
        }
        var preview = _session.PreviewRepeatedPlacement(exactRequest);
        var previewDidNotMutate = ReferenceEquals(_session.Project, originalProject);

        var occupied = _session.ActiveArea.Entities.First();
        var occupiedRequest = exactRequest with
        {
            Origin = occupied.Transform.Position,
            Count = 1,
            ApplyCorrections = false,
        };
        var collisionBlocked = !_session.PreviewRepeatedPlacement(occupiedRequest).IsValid;
        var collisionMessage = _session.PreviewRepeatedPlacement(occupiedRequest).Message;
        var humanCollision = !collisionMessage.Contains(occupied.Id.ToString()[..8], StringComparison.OrdinalIgnoreCase);
        var correction = _session.PreviewRepeatedPlacement(occupiedRequest with { ApplyCorrections = true });
        var correctionValid = correction.IsValid && correction.CorrectedCount == 1;
        var socketBlocked = !_session.PreviewRepeatedPlacement(exactRequest with
        {
            Count = 1,
            RequiredSocket = "raccord-absent",
            AvailableSockets = [],
        }).IsValid;
        var budgetBlocked = !_session.PreviewRepeatedPlacement(exactRequest with
        {
            Count = 2,
            AvailableBudgetBytes = 3_000,
            BlueprintEstimatedBytes = 2_000,
        }).IsValid;

        var distributions = 0;
        foreach (var distribution in Enum.GetValues<EditorPlacementDistribution>())
        {
            var profile = new EditorRepeatedPlacementProfile(
                repeatedProfile.Id,
                EditorRepeatedPlacementProfile.CurrentSchemaVersion,
                "studio:ux0-d-smoke-distribution",
                true,
                false,
                false,
                distribution,
                repeatedProfile.Alignment,
                repeatedProfile.Spacing,
                repeatedProfile.Columns,
                repeatedProfile.Seed,
                repeatedProfile.SurfaceOffset);
            var candidate = FindValidPlacement(paletteItem.BlueprintId, profile, count: 3);
            if (candidate is not null && _session.PreviewRepeatedPlacement(candidate).IsValid)
            {
                distributions++;
            }
        }

        var alignments = 0;
        foreach (var alignment in Enum.GetValues<EditorPlacementAlignment>())
        {
            var profile = new EditorRepeatedPlacementProfile(
                repeatedProfile.Id,
                EditorRepeatedPlacementProfile.CurrentSchemaVersion,
                "studio:ux0-d-smoke-alignment",
                true,
                false,
                false,
                repeatedProfile.Distribution,
                alignment,
                repeatedProfile.Spacing,
                repeatedProfile.Columns,
                repeatedProfile.Seed,
                repeatedProfile.SurfaceOffset);
            var candidate = FindValidPlacement(paletteItem.BlueprintId, profile, count: 2);
            if (candidate is not null && _session.PreviewRepeatedPlacement(candidate).IsValid)
            {
                alignments++;
            }
        }

        var brushProfile = new EditorRepeatedPlacementProfile(
            repeatedProfile.Id,
            EditorRepeatedPlacementProfile.CurrentSchemaVersion,
            "studio:ux0-d-smoke-brush",
            true,
            true,
            true,
            repeatedProfile.Distribution,
            repeatedProfile.Alignment,
            repeatedProfile.Spacing,
            repeatedProfile.Columns,
            repeatedProfile.Seed,
            repeatedProfile.SurfaceOffset);
        var brushRequest = exactRequest with
        {
            Profile = brushProfile,
            BrushSamples = preview.Poses.Select(pose => pose.Position).ToArray(),
        };
        var brushPreview = _session.PreviewRepeatedPlacement(brushRequest);

        var placed = _session.PlaceRepeatedEntities(exactRequest, persistPaletteUsage: false);
        var placedEntities = _session.ActiveArea.Entities
            .Where(entity => !originalEntityIds.Contains(entity.Id))
            .ToArray();
        var placementApplied = placed.Succeeded && placedEntities.Length == 3;
        var groupedUndo = placementApplied && _session.Undo().Succeeded &&
            _session.ActiveArea.Entities.Count == originalEntityCount;
        var groupedRedo = groupedUndo && _session.Redo().Succeeded &&
            _session.ActiveArea.Entities.Count == originalEntityCount + 3;

        var copied = groupedRedo && _session.SelectEntities(
            placedEntities.Select(entity => entity.Id).ToArray(),
            placedEntities[^1].Id) && _session.CopySelectedEntities().Succeeded;
        var copyState = _session.ClipboardState;
        var pasteTargetRequest = FindValidPlacement(paletteItem.BlueprintId, repeatedProfile, count: 3);
        var pasteTargetPreview = pasteTargetRequest is null
            ? null
            : _session.PreviewRepeatedPlacement(pasteTargetRequest);
        var pasteTarget = pasteTargetPreview is { IsValid: true }
            ? AveragePosition(pasteTargetPreview.Poses)
            : Vector3Value.Zero;
        var pasted = pasteTargetPreview is { IsValid: true }
            ? _session.PasteClipboard(pasteTarget, SemanticPasteMode.Instance, autoCorrect: true)
            : new StudioEntityBatchActionResult(false, "Aucune cible libre.");
        var instanceCopies = _session.ActiveArea.Entities
            .Where(entity => !originalEntityIds.Contains(entity.Id) &&
                             placedEntities.All(source => source.Id != entity.Id))
            .ToArray();
        var mappingChanged = pasted.Succeeded && instanceCopies.Length == 3 &&
            instanceCopies.All(copy => placedEntities.All(source => source.Id != copy.Id));
        var pasteUndo = pasted.Succeeded && _session.Undo().Succeeded &&
            _session.ActiveArea.Entities.Count == originalEntityCount + 3;

        var independent = pasteUndo
            ? _session.PasteClipboard(pasteTarget, SemanticPasteMode.IndependentCopy, autoCorrect: true)
            : new StudioEntityBatchActionResult(false, "Collage précédent non annulé.");
        var independentBlueprints = _session.Project!.Blueprints.Count - originalBlueprintCount;
        var independentValid = independent.Succeeded && independentBlueprints == 1;
        var independentUndo = independent.Succeeded && _session.Undo().Succeeded &&
            _session.Project!.Blueprints.Count == originalBlueprintCount;

        var duplicateSelection = independentUndo && _session.SelectEntities(
            placedEntities.Select(entity => entity.Id).ToArray(),
            placedEntities[^1].Id);
        var duplicated = duplicateSelection
            ? _session.DuplicateSelectedEntities()
            : new StudioEntityBatchActionResult(false, "Sélection indisponible.");
        var duplicateValid = duplicated.Succeeded &&
            _session.ActiveArea.Entities.Count == originalEntityCount + 6;
        var duplicateUndo = duplicated.Succeeded && _session.Undo().Succeeded &&
            _session.ActiveArea.Entities.Count == originalEntityCount + 3;

        var cutSelection = duplicateUndo && _session.SelectEntities(
            placedEntities.Select(entity => entity.Id).ToArray(),
            placedEntities[^1].Id);
        var cut = cutSelection
            ? _session.CutSelectedEntities()
            : new StudioActionResult(false, "Sélection indisponible.");
        var cutValid = cut.Succeeded && _session.ClipboardState.IsCut &&
            _session.ActiveArea.Entities.Count == originalEntityCount;
        var cutUndo = cut.Succeeded && _session.Undo().Succeeded &&
            _session.ActiveArea.Entities.Count == originalEntityCount + 3;
        _session.ClearClipboardForVerification();

        while (!ReferenceEquals(_session.Project, originalProject) && _session.CanUndo)
        {
            _session.Undo();
        }
        var projectRestored = ReferenceEquals(_session.Project, originalProject) && !_session.IsDirty;

        BeginAddEntity(exactRequest.Origin);
        var inspector = _inspectorContent.GetChildren().OfType<StudioAddEntityInspector>().SingleOrDefault();
        var uiVisible = inspector is not null && inspector.PaletteViewCount >= 8 &&
                        inspector.VisibleResultCount > 0 && inspector.FilterDimensionCount == 6 &&
                        inspector.PlacementControlsReady && WorldViewport.PlacementGhostCount > 0 &&
                        WorldViewport.PlacementFeedbackVisible;
        _placementPosition = null;
        WorldViewport.CancelPlacementTool(notify: false);
        WorldViewport.HidePlacementPreview();

        var workspaceRestored = _session.ApplyWorkspaceProfile(originalWorkspace).Succeeded;
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.Rebuild(_session, frameArea: false);
        RefreshInspector();
        UpdateCommandState();

        var standardItems = _session.BuildBlueprintPalette(BlueprintPaletteView.InstalledPacks);
        var customItems = _session.BuildBlueprintPalette(BlueprintPaletteView.ProjectContent);
        var standardCustom = standardItems.All(item => item.Origin == BlueprintPaletteOrigin.InstalledPack) &&
                             customItems.All(item => item.Origin == BlueprintPaletteOrigin.ProjectContent) &&
                             standardItems.Select(item => item.BlueprintId)
                                 .Intersect(customItems.Select(item => item.BlueprintId)).Any() == false;
        var valid = views.Length == 7 && viewCounts[BlueprintPaletteView.All] == originalBlueprintCount &&
                    standardCustom && filterApply.Succeeded && filterMatched && favorite.Succeeded &&
                    favoriteVisible && named.Succeeded && namedVisible && repeatedConfigured.Succeeded &&
                    profileRoundTrip && preview.IsValid && preview.Poses.Count == 3 && previewDidNotMutate &&
                    collisionBlocked && humanCollision && correctionValid && socketBlocked && budgetBlocked &&
                    distributions == 3 && alignments == 3 && brushPreview.IsValid && brushPreview.Poses.Count == 3 &&
                    placementApplied && groupedUndo && groupedRedo && copied && copyState.EntityCount == 3 &&
                    mappingChanged && pasteUndo && independentValid && independentUndo && duplicateValid &&
                    duplicateUndo && cutValid && cutUndo && projectRestored && uiVisible && workspaceRestored &&
                    !_session.WorkspaceProfileHasUnsavedChanges;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX0D_{(valid ? "PASS" : "FAILED")} " +
            $"palette_views={views.Length} standard_custom={standardCustom.ToString().ToLowerInvariant()} " +
            $"filters=6 favorite={favoriteVisible.ToString().ToLowerInvariant()} named={namedVisible.ToString().ToLowerInvariant()} " +
            $"distributions={distributions} alignments={alignments} brush={(brushPreview.IsValid && brushPreview.Poses.Count == 3).ToString().ToLowerInvariant()} " +
            $"preview_no_mutation={previewDidNotMutate.ToString().ToLowerInvariant()} correction={correctionValid.ToString().ToLowerInvariant()} " +
            $"invalid_collision_socket_budget={(collisionBlocked && socketBlocked && budgetBlocked).ToString().ToLowerInvariant()} " +
            $"grouped_undo={(groupedUndo && groupedRedo).ToString().ToLowerInvariant()} " +
            $"clipboard_copy_cut_paste={(copied && cutValid && mappingChanged).ToString().ToLowerInvariant()} " +
            $"independent_copy={independentValid.ToString().ToLowerInvariant()} duplicate_group={duplicateValid.ToString().ToLowerInvariant()} " +
            $"ui_ghost={uiVisible.ToString().ToLowerInvariant()} human_no_uuid={humanCollision.ToString().ToLowerInvariant()} " +
            "windows=1 persisted=false");
        return valid;
    }

    private StudioRepeatedPlacementRequest? FindValidPlacement(
        BlueprintId blueprintId,
        EditorRepeatedPlacementProfile profile,
        int count)
    {
        var area = _session.ActiveArea!;
        var xExtent = area.Dimensions.Width / 2f;
        var zExtent = area.Dimensions.Depth / 2f;
        for (var z = -zExtent + 2; z <= zExtent - 2; z += 2)
        {
            for (var x = -xExtent + 2; x <= xExtent - 2; x += 2)
            {
                var request = new StudioRepeatedPlacementRequest(
                    blueprintId,
                    new Vector3Value(x, 0, z),
                    profile,
                    count,
                    ApplyCorrections: false);
                if (_session.PreviewRepeatedPlacement(request).IsValid)
                {
                    return request;
                }
            }
        }
        return null;
    }

    private void RestoreUx0DState(
        Forge.Domain.Projects.ProjectDefinition originalProject,
        EditorWorkspaceProfile originalWorkspace,
        IReadOnlyList<EntityId> originalSelection,
        EntityId? originalActive)
    {
        while (!ReferenceEquals(_session.Project, originalProject) && _session.CanUndo)
        {
            _session.Undo();
        }
        _session.ClearClipboardForVerification();
        _session.ApplyWorkspaceProfile(originalWorkspace);
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.Rebuild(_session, frameArea: false);
        RefreshInspector();
        UpdateCommandState();
    }

    private static Vector3Value AveragePosition(IReadOnlyList<StudioPlacementPosePreview> poses) => new(
        poses.Average(pose => pose.Position.X),
        poses.Average(pose => pose.Position.Y),
        poses.Average(pose => pose.Position.Z));

    private static bool VerifyUx0DProfileRoundTrip(
        EditorWorkspaceProfile profile,
        BlueprintId favoriteBlueprintId)
    {
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux0d-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            EditorWorkspaceProfileStore.Save(root, profile);
            var restored = EditorWorkspaceProfileStore.TryLoad(root);
            return restored is not null &&
                   restored.PersonalFavoriteBlueprintIds.Contains(favoriteBlueprintId) &&
                   restored.NamedPaletteCollections.Any(collection => collection.Name == "Sélection UX0-D") &&
                   restored.RepeatedPlacementProfile.Enabled &&
                   restored.Shell.PaletteFilter.Kinds.SequenceEqual(profile.Shell.PaletteFilter.Kinds);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
