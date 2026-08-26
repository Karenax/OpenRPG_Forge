using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Creator;
using Forge.Application.Entities;
using Forge.Application.Spatial;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SemanticClipboard _semanticClipboard = new();
    private readonly CutEntitiesHandler _cutEntities = new(new SystemClock());

    internal StudioClipboardState ClipboardState => _semanticClipboard.Payload is EntityClipboardPayload payload
        ? new StudioClipboardState(
            true,
            payload.Entities.Count,
            payload.IsCut,
            payload.IsCut
                ? $"{payload.Entities.Count} objet(s) coupé(s), prêts à être déplacés"
                : $"{payload.Entities.Count} objet(s) copié(s), prêts à être collés")
        : StudioClipboardState.Empty;

    internal void ClearClipboardForVerification() => _semanticClipboard.Clear();

    internal IReadOnlyList<string> AvailablePlacementSockets => ActiveArea?.Entities
        .SelectMany(entity => entity.Tags)
        .Where(tag => tag.StartsWith("socket:", StringComparison.OrdinalIgnoreCase))
        .Select(tag => tag["socket:".Length..].Trim())
        .Where(tag => tag.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Order(StringComparer.OrdinalIgnoreCase)
        .ToArray() ?? [];

    internal IReadOnlyList<BlueprintPaletteItem> BuildBlueprintPalette(
        BlueprintPaletteView view,
        string? query = null,
        EditorPaletteFilter? filter = null,
        Guid? namedCollectionId = null)
    {
        if (Project is null)
        {
            return [];
        }

        var catalog = BlueprintPaletteCatalog.Build(Project, WorkspaceProfile);
        var filtered = catalog.Filter(view, query, filter ?? WorkspaceProfile.Shell.PaletteFilter);
        if (namedCollectionId is null)
        {
            return filtered;
        }

        var collection = WorkspaceProfile.NamedPaletteCollections
            .FirstOrDefault(candidate => candidate.Id == namedCollectionId.Value);
        if (collection is null)
        {
            return [];
        }

        var byId = filtered.ToDictionary(item => item.BlueprintId);
        return collection.BlueprintIds
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .ToArray();
    }

    internal StudioActionResult ConfigurePaletteFilter(EditorPaletteFilter filter, bool persist = true)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var current = WorkspaceProfile;
        var shell = new EditorShellConfiguration(
            current.Shell.Id,
            EditorShellConfiguration.CurrentSchemaVersion,
            "studio:ux0-d-palette-filter",
            current.Shell.Layout,
            current.Shell.ExplorerGrouping,
            current.Shell.ViewportMode,
            filter,
            current.Shell.DirectManipulation);
        return ApplyWorkspaceProfileCandidate(
            EditorWorkspaceProfilePlanner.ConfigureShell(current, shell),
            "studio:ux0-d-palette-filter",
            persist);
    }

    internal StudioActionResult ToggleBlueprintFavorite(BlueprintId blueprintId, bool persist = true)
    {
        ArgumentNullException.ThrowIfNull(blueprintId);
        if (Project?.Blueprints.All(blueprint => blueprint.Id != blueprintId) != false)
        {
            return new StudioActionResult(false, "Cet élément n’existe plus dans la palette.");
        }

        var candidate = EditorWorkspaceProfilePlanner.TogglePersonalFavorite(WorkspaceProfile, blueprintId);
        var favorite = candidate.PersonalFavoriteBlueprintIds.Contains(blueprintId);
        var result = ApplyWorkspaceProfileCandidate(candidate, "studio:ux0-d-favorite", persist);
        return result.Succeeded
            ? new StudioActionResult(true, favorite ? "Ajouté aux favoris" : "Retiré des favoris")
            : result;
    }

    internal StudioActionResult SaveNamedPalette(
        string name,
        IReadOnlyList<BlueprintId> blueprintIds,
        bool taskScoped,
        bool persist = true)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new StudioActionResult(false, "Donnez un nom lisible à la palette.");
        }
        ArgumentNullException.ThrowIfNull(blueprintIds);
        var available = Project?.Blueprints.Select(blueprint => blueprint.Id).ToHashSet() ?? [];
        var ids = blueprintIds.Where(available.Contains).Distinct().Take(128).ToArray();
        if (ids.Length == 0)
        {
            return new StudioActionResult(false, "Ajoutez au moins un élément à cette palette.");
        }

        var existing = WorkspaceProfile.NamedPaletteCollections.FirstOrDefault(collection =>
            string.Equals(collection.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        var collection = new NamedPaletteCollection(
            existing?.Id ?? Guid.NewGuid(),
            1,
            name.Trim(),
            ids,
            taskScoped,
            "studio:ux0-d-named-palette");
        var candidate = EditorWorkspaceProfilePlanner.UpsertNamedPalette(WorkspaceProfile, collection);
        var result = ApplyWorkspaceProfileCandidate(candidate, "studio:ux0-d-named-palette", persist);
        return result.Succeeded
            ? new StudioActionResult(true, $"Palette « {collection.Name} » enregistrée · {ids.Length} élément(s)")
            : result;
    }

    internal StudioActionResult RemoveNamedPalette(Guid collectionId, bool persist = true)
    {
        var collection = WorkspaceProfile.NamedPaletteCollections.FirstOrDefault(candidate => candidate.Id == collectionId);
        if (collection is null)
        {
            return new StudioActionResult(false, "Cette palette personnalisée n’existe plus.");
        }

        var candidate = EditorWorkspaceProfilePlanner.RemoveNamedPalette(WorkspaceProfile, collectionId);
        var result = ApplyWorkspaceProfileCandidate(candidate, "studio:ux0-d-remove-palette", persist);
        return result.Succeeded
            ? new StudioActionResult(true, $"Palette « {collection.Name} » supprimée")
            : result;
    }

    internal StudioActionResult ConfigureRepeatedPlacement(
        EditorRepeatedPlacementProfile profile,
        bool persist = true)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var current = WorkspaceProfile;
        var candidate = EditorWorkspaceProfilePlanner.ConfigureProductivity(
            current,
            current.DisplayFilter,
            profile,
            current.MultiArea,
            current.Help,
            current.RolePolicy);
        return ApplyWorkspaceProfileCandidate(candidate, "studio:ux0-d-repeated-placement", persist);
    }

    internal StudioPlacementPreview PreviewRepeatedPlacement(StudioRepeatedPlacementRequest request) =>
        PrepareRepeatedPlacement(request).Preview;

    internal StudioEntityBatchActionResult PlaceRepeatedEntities(
        StudioRepeatedPlacementRequest request,
        bool persistPaletteUsage = true)
    {
        var prepared = PrepareRepeatedPlacement(request);
        if (!prepared.Preview.IsValid || prepared.Payload is null || prepared.EntityMap is null)
        {
            return new StudioEntityBatchActionResult(false, prepared.Preview.Message);
        }

        var commandId = CommandId.From(Guid.NewGuid());
        var result = ExecuteMutation(
            dryRun => _pasteEntities.Handle(
                History!.State,
                new PasteEntitiesCommand(
                    commandId,
                    History.State.Revision,
                    ActiveAreaId!,
                    prepared.Payload,
                    prepared.EntityMap,
                    Vector3Value.Zero,
                    dryRun,
                    RequiredSocket: request.RequiredSocket,
                    AvailableSockets: request.AvailableSockets ?? AvailablePlacementSockets,
                    AvailableBudgetBytes: request.AvailableBudgetBytes,
                    BlueprintEstimatedBytes: prepared.EstimatedBytes)),
            $"{prepared.EntityMap.Count} objet(s) placé(s) en une opération");
        if (!result.Succeeded)
        {
            return new StudioEntityBatchActionResult(false, result.Message);
        }

        var selected = prepared.EntityMap.Values.ToArray();
        SelectEntities(selected, selected.LastOrDefault());
        var usage = RecordBlueprintUsage(request.BlueprintId, persistPaletteUsage);
        var suffix = prepared.Preview.CorrectedCount > 0
            ? $" · {prepared.Preview.CorrectedCount} position(s) corrigée(s)"
            : string.Empty;
        if (!usage.Succeeded)
        {
            suffix += " · historique récent non mémorisé";
        }
        return new StudioEntityBatchActionResult(true, result.Message + suffix, selected.Length);
    }

    internal StudioActionResult CopySelectedEntities()
    {
        if (Project is null || ActiveAreaId is null || SelectedEntityIds.Count == 0)
        {
            return new StudioActionResult(false, "Sélectionnez au moins un objet à copier.");
        }

        try
        {
            var payload = _semanticClipboard.CopyEntities(
                Project,
                ActiveAreaId,
                SelectedEntityIds,
                "studio:ux0-d-copy");
            return new StudioActionResult(true,
                $"{payload.Entities.Count} objet(s) copié(s) · choisissez où les coller");
        }
        catch (ArgumentException)
        {
            return new StudioActionResult(false, "La sélection a changé ; sélectionnez de nouveau les objets à copier.");
        }
    }

    internal StudioActionResult CutSelectedEntities()
    {
        if (History is null || ActiveAreaId is null || SelectedEntityIds.Count == 0)
        {
            return new StudioActionResult(false, "Sélectionnez au moins un objet à couper.");
        }

        var command = new CutEntitiesCommand(
            CommandId.From(Guid.NewGuid()),
            CutEntitiesHandler.CurrentSchemaVersion,
            "studio:ux0-d-cut",
            History.State.Revision,
            ActiveAreaId,
            SelectedEntityIds,
            DryRun: false);
        var preview = _cutEntities.Handle(History.State, command with { DryRun = true });
        if (!preview.Succeeded)
        {
            return HumanizeCutFailure(preview);
        }

        var applied = _cutEntities.Handle(History.State, command);
        if (!applied.Succeeded || applied.Payload is null)
        {
            return HumanizeCutFailure(applied);
        }

        History.Record(applied.Command);
        _semanticClipboard.Set(applied.Payload);
        SelectEntities([], null);
        return new StudioActionResult(true,
            $"{applied.Payload.Entities.Count} objet(s) coupé(s) · Ctrl+Z pour annuler ou Ctrl+V pour déplacer");
    }

    internal StudioEntityBatchActionResult PasteClipboard(
        Vector3Value targetPosition,
        SemanticPasteMode mode,
        bool autoCorrect = true,
        string? requiredSocket = null,
        IReadOnlyList<string>? availableSockets = null,
        long? availableBudgetBytes = null,
        long? blueprintEstimatedBytes = null)
    {
        if (History is null || ActiveAreaId is null || _semanticClipboard.Payload is not EntityClipboardPayload payload)
        {
            return new StudioEntityBatchActionResult(false, "Le presse-papiers ne contient aucun objet à coller.");
        }

        var entityMap = payload.Entities.ToDictionary(entity => entity.Id, _ => EntityId.From(Guid.NewGuid()));
        var blueprintMap = mode == SemanticPasteMode.IndependentCopy
            ? payload.Entities.Select(entity => entity.BlueprintId).Distinct()
                .ToDictionary(id => id, _ => BlueprintId.From(Guid.NewGuid()))
            : null;
        var anchor = ComputeAnchor(payload.Entities);
        var requestedOffset = new Vector3Value(
            targetPosition.X - anchor.X,
            targetPosition.Y - anchor.Y,
            targetPosition.Z - anchor.Z);
        var command = new PasteEntitiesCommand(
            CommandId.From(Guid.NewGuid()),
            History.State.Revision,
            ActiveAreaId,
            payload,
            entityMap,
            requestedOffset,
            DryRun: false,
            mode,
            blueprintMap,
            requiredSocket,
            availableSockets ?? AvailablePlacementSockets,
            availableBudgetBytes,
            blueprintEstimatedBytes);
        var selectedCommand = FindValidPasteCommand(command, autoCorrect, out var preview, out var adjusted);
        if (selectedCommand is null)
        {
            return new StudioEntityBatchActionResult(false, HumanizePasteFailure(preview));
        }

        var result = ExecuteMutation(
            dryRun => _pasteEntities.Handle(History.State, selectedCommand with { DryRun = dryRun }),
            mode == SemanticPasteMode.IndependentCopy
                ? $"{entityMap.Count} objet(s) collé(s) avec copies indépendantes"
                : $"{entityMap.Count} objet(s) collé(s) comme instances");
        if (!result.Succeeded)
        {
            return new StudioEntityBatchActionResult(false, result.Message);
        }

        var selected = entityMap.Values.ToArray();
        SelectEntities(selected, selected.LastOrDefault());
        if (payload.IsCut)
        {
            _semanticClipboard.Clear();
        }
        return new StudioEntityBatchActionResult(
            true,
            result.Message + (adjusted ? " · emplacement libre ajusté" : string.Empty),
            selected.Length);
    }

    internal StudioEntityBatchActionResult DuplicateSelectedEntities()
    {
        if (History is null || Project is null || ActiveAreaId is null || SelectedEntityIds.Count == 0)
        {
            return new StudioEntityBatchActionResult(false, "Sélectionnez au moins un objet à dupliquer.");
        }

        EntityClipboardPayload payload;
        try
        {
            payload = new EntityClipboardPayload(
                ActiveAreaId,
                ActiveArea!.Entities.Where(entity => SelectedEntityIds.Contains(entity.Id)).ToArray(),
                provenance: "studio:ux0-d-duplicate");
        }
        catch (ArgumentException)
        {
            return new StudioEntityBatchActionResult(false, "La sélection a changé ; sélectionnez de nouveau les objets.");
        }

        var entityMap = payload.Entities.ToDictionary(entity => entity.Id, _ => EntityId.From(Guid.NewGuid()));
        var command = new PasteEntitiesCommand(
            CommandId.From(Guid.NewGuid()),
            History.State.Revision,
            ActiveAreaId,
            payload,
            entityMap,
            Vector3Value.Zero,
            DryRun: false);
        var selectedCommand = FindValidDuplicateCommand(command, out var preview);
        if (selectedCommand is null)
        {
            return new StudioEntityBatchActionResult(false, HumanizePasteFailure(preview));
        }

        var result = ExecuteMutation(
            dryRun => _pasteEntities.Handle(History.State, selectedCommand with { DryRun = dryRun }),
            $"{entityMap.Count} objet(s) dupliqué(s) à côté de la sélection");
        if (!result.Succeeded)
        {
            return new StudioEntityBatchActionResult(false, result.Message);
        }

        var selected = entityMap.Values.ToArray();
        SelectEntities(selected, selected.LastOrDefault());
        return new StudioEntityBatchActionResult(true, result.Message, selected.Length);
    }

    private PreparedRepeatedPlacement PrepareRepeatedPlacement(StudioRepeatedPlacementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (History is null || Project is null || ActiveAreaId is null)
        {
            return PreparedRepeatedPlacement.Invalid("Aucun module ou aucune zone active.");
        }
        var blueprint = Project.Blueprints.FirstOrDefault(candidate => candidate.Id == request.BlueprintId);
        if (blueprint is null)
        {
            return PreparedRepeatedPlacement.Invalid("L’élément choisi n’existe plus dans la palette.");
        }

        RepeatedPlacementPlan plan;
        var rotation = request.Rotation ?? QuaternionValue.Identity;
        var scale = request.Scale ?? Vector3Value.One;
        try
        {
            _ = new TransformValue(request.Origin, rotation, scale);
            PlacementSurfaceSample SampleSurface(Vector3Value position)
            {
                var y = request.HeightMode == StudioPlacementHeightMode.TerrainSurface &&
                        ActiveArea!.World.Terrain.Enabled
                    ? ActiveArea.World.Terrain.HeightAt(position.X, position.Z)
                    : position.Y;
                return new PlacementSurfaceSample(
                    new Vector3Value(position.X, y, position.Z),
                    new Vector3Value(0, 1, 0));
            }
            plan = request.Profile.BrushMode
                ? RepeatedPlacementPlanner.PlanBrushStroke(
                    request.Profile,
                    request.BrushSamples is { Count: > 0 } ? request.BrushSamples : [request.Origin],
                    SampleSurface)
                : RepeatedPlacementPlanner.Plan(
                    request.Profile,
                    request.Origin,
                    request.Count,
                    SampleSurface);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return PreparedRepeatedPlacement.Invalid($"Aperçu refusé · {exception.Message}");
        }

        var temporaryProject = Project;
        var sources = new List<EntityInstance>();
        var poses = new List<StudioPlacementPosePreview>();
        var correctedCount = 0;
        var availableSockets = request.AvailableSockets ?? AvailablePlacementSockets;
        var estimatedBytes = request.BlueprintEstimatedBytes ??
            (request.AvailableBudgetBytes is null ? null : blueprint.Authoring.PlatformByteBudget);
        var remainingBudget = request.AvailableBudgetBytes;
        var assetPlacement = ResolveAssetPlacement(blueprint);
        foreach (var pose in plan.Poses)
        {
            var projectedPosition = request.HeightMode == StudioPlacementHeightMode.TerrainSurface &&
                                    ActiveArea!.World.Terrain.Enabled
                ? new Vector3Value(
                    pose.Position.X,
                    ActiveArea.World.Terrain.HeightAt(pose.Position.X, pose.Position.Z) + request.Profile.SurfaceOffset,
                    pose.Position.Z)
                : pose.Position;
            var validationRequest = new PlacementValidationRequest(
                ActiveAreaId,
                blueprint.Id,
                projectedPosition,
                .25f,
                RequiredSocket: request.RequiredSocket,
                AvailableSockets: availableSockets,
                AvailableBudgetBytes: remainingBudget,
                BlueprintEstimatedBytes: estimatedBytes,
                AutoCorrect: true);
            var validation = _placementValidator.Validate(temporaryProject, validationRequest);
            var accepted = validation;
            if (!validation.IsValid && request.ApplyCorrections && validation.CorrectedPosition != pose.Position &&
                validation.Code is PlacementValidator.OccupiedCode or PlacementValidator.OutsideBoundsCode)
            {
                var correction = _placementValidator.Validate(
                    temporaryProject,
                    validationRequest with { Position = validation.CorrectedPosition, AutoCorrect = false });
                if (correction.IsValid)
                {
                    accepted = correction;
                    correctedCount++;
                }
            }

            if (!accepted.IsValid)
            {
                poses.Add(new StudioPlacementPosePreview(
                    validation.CorrectedPosition,
                    false,
                    HumanizePlacementValidation(validation, validationRequest),
                    validation.Corrections ?? [],
                    new TransformValue(validation.CorrectedPosition, rotation, scale)));
                continue;
            }

            var acceptedPosition = accepted.CorrectedPosition;
            if (request.HeightMode == StudioPlacementHeightMode.TerrainSurface &&
                ActiveArea!.World.Terrain.Enabled)
            {
                acceptedPosition = new Vector3Value(
                    acceptedPosition.X,
                    ActiveArea.World.Terrain.HeightAt(acceptedPosition.X, acceptedPosition.Z) +
                    request.Profile.SurfaceOffset,
                    acceptedPosition.Z);
            }
            var source = new EntityInstance(
                EntityId.From(Guid.NewGuid()),
                blueprint.Id,
                new TransformValue(acceptedPosition, rotation, scale),
                assetPlacement.Tags,
                assetPlacement.Overrides);
            sources.Add(source);
            temporaryProject = AddPreviewEntity(temporaryProject, ActiveAreaId, source);
            if (remainingBudget is not null && estimatedBytes is not null)
            {
                remainingBudget -= estimatedBytes.Value;
            }
            poses.Add(new StudioPlacementPosePreview(
                acceptedPosition,
                true,
                accepted.Message,
                accepted.Corrections ?? [],
                source.Transform));
        }

        var invalid = poses.FirstOrDefault(pose => !pose.IsValid);
        if (invalid is not null)
        {
            return new PreparedRepeatedPlacement(
                new StudioPlacementPreview(false,
                    $"Placement refusé · {invalid.Message}", poses, correctedCount),
                null,
                null,
                estimatedBytes);
        }

        var payload = new EntityClipboardPayload(
            ActiveAreaId,
            sources,
            provenance: "studio:ux0-d-repeated-placement");
        var entityMap = sources.ToDictionary(entity => entity.Id, _ => EntityId.From(Guid.NewGuid()));
        var commandPreview = _pasteEntities.Handle(
            History.State,
            new PasteEntitiesCommand(
                CommandId.From(Guid.NewGuid()),
                History.State.Revision,
                ActiveAreaId,
                payload,
                entityMap,
                Vector3Value.Zero,
                DryRun: true,
                RequiredSocket: request.RequiredSocket,
                AvailableSockets: availableSockets,
                AvailableBudgetBytes: request.AvailableBudgetBytes,
                BlueprintEstimatedBytes: estimatedBytes));
        if (!commandPreview.Succeeded)
        {
            return new PreparedRepeatedPlacement(
                new StudioPlacementPreview(false,
                    $"Placement refusé · {HumanizePasteFailure(commandPreview)}", poses, correctedCount),
                null,
                null,
                estimatedBytes);
        }

        return new PreparedRepeatedPlacement(
            new StudioPlacementPreview(
                true,
                $"Aperçu valide · {poses.Count} objet(s) · {PlacementDistributionLabel(request.Profile.Distribution)} · aucune donnée modifiée",
                poses,
                correctedCount),
            payload,
            entityMap,
            estimatedBytes);
    }

    private StudioActionResult RecordBlueprintUsage(BlueprintId blueprintId, bool persist)
    {
        var candidate = EditorWorkspaceProfilePlanner.RecordRecent(WorkspaceProfile, blueprintId);
        return ApplyWorkspaceProfileCandidate(candidate, "studio:ux0-d-recent", persist);
    }

    private PasteEntitiesCommand? FindValidPasteCommand(
        PasteEntitiesCommand command,
        bool autoCorrect,
        out CommandResult preview,
        out bool adjusted)
    {
        preview = _pasteEntities.Handle(History!.State, command with { DryRun = true });
        adjusted = false;
        if (preview.Succeeded || !autoCorrect ||
            preview.Diagnostics.FirstOrDefault()?.Code is not (PlacementValidator.OccupiedCode or PlacementValidator.OutsideBoundsCode))
        {
            return preview.Succeeded ? command : null;
        }

        foreach (var offset in PasteCorrectionOffsets())
        {
            var candidate = command with
            {
                PositionOffset = new Vector3Value(
                    command.PositionOffset.X + offset.X,
                    command.PositionOffset.Y + offset.Y,
                    command.PositionOffset.Z + offset.Z),
                DryRun = true,
            };
            var candidatePreview = _pasteEntities.Handle(History.State, candidate);
            if (!candidatePreview.Succeeded)
            {
                continue;
            }
            preview = candidatePreview;
            adjusted = true;
            return candidate with { DryRun = false };
        }
        return null;
    }

    private PasteEntitiesCommand? FindValidDuplicateCommand(PasteEntitiesCommand command, out CommandResult preview)
    {
        preview = _pasteEntities.Handle(History!.State, command with { DryRun = true });
        foreach (var offset in PasteCorrectionOffsets().Where(offset => offset != Vector3Value.Zero))
        {
            var candidate = command with { PositionOffset = offset, DryRun = true };
            var candidatePreview = _pasteEntities.Handle(History.State, candidate);
            if (!candidatePreview.Succeeded)
            {
                preview = candidatePreview;
                continue;
            }
            preview = candidatePreview;
            return candidate with { DryRun = false };
        }
        return null;
    }

    private static IReadOnlyList<Vector3Value> PasteCorrectionOffsets() =>
    [
        new(1, 0, 0), new(0, 0, 1), new(-1, 0, 0), new(0, 0, -1),
        new(1, 0, 1), new(-1, 0, 1), new(-1, 0, -1), new(1, 0, -1),
        new(2, 0, 0), new(0, 0, 2), new(-2, 0, 0), new(0, 0, -2),
        new(2, 0, 2), new(-2, 0, 2), new(-2, 0, -2), new(2, 0, -2),
    ];

    private static Vector3Value ComputeAnchor(IReadOnlyList<EntityInstance> entities) => new(
        entities.Average(entity => entity.Transform.Position.X),
        entities.Average(entity => entity.Transform.Position.Y),
        entities.Average(entity => entity.Transform.Position.Z));

    private static ProjectDefinition AddPreviewEntity(
        ProjectDefinition project,
        AreaId areaId,
        EntityInstance entity)
    {
        var area = project.Areas.Single(candidate => candidate.Id == areaId);
        var updatedArea = area.WithEntities([.. area.Entities, entity]);
        return new ProjectDefinition(
            project.Id,
            project.Title,
            project.Module,
            project.Blueprints,
            project.Areas.Select(candidate => candidate.Id == areaId ? updatedArea : candidate).ToArray(),
            project.Dialogues,
            project.Quests);
    }

    private static StudioActionResult HumanizeCutFailure(CutEntitiesResult result)
    {
        var diagnostic = result.Command.Diagnostics.FirstOrDefault();
        return diagnostic?.Code switch
        {
            CutEntitiesHandler.ReferenceCode => new StudioActionResult(false,
                "Coupe refusée : un autre contenu dépend encore de cette sélection."),
            CutEntitiesHandler.SelectionCode => new StudioActionResult(false,
                "La sélection a changé ; sélectionnez de nouveau les objets à couper."),
            _ => new StudioActionResult(false, diagnostic?.Message ?? "La coupe n’a pas pu être préparée."),
        };
    }

    private static string HumanizePasteFailure(CommandResult result)
    {
        var diagnostic = result.Diagnostics.FirstOrDefault();
        return diagnostic?.Code switch
        {
            PasteEntitiesHandler.InvalidMappingCode or PasteEntitiesHandler.DuplicateEntityCode or
                PasteEntitiesHandler.InvalidBlueprintMappingCode =>
                "Le contenu copié a changé ; copiez de nouveau la sélection.",
            PasteEntitiesHandler.MissingTargetAreaCode => "La zone cible n’existe plus.",
            PasteEntitiesHandler.RevisionConflictCode => "Le module a changé ; recommencez le collage.",
            _ => diagnostic?.Message ?? "Le collage n’a pas pu être préparé.",
        };
    }

    private static string HumanizePlacementValidation(
        PlacementValidationResult result,
        PlacementValidationRequest request) => result.Code switch
        {
            PlacementValidator.SocketMissingCode =>
                $"Raccord « {request.RequiredSocket} » indisponible. Choisissez un raccord compatible ou retirez la contrainte.",
            PlacementValidator.BudgetExceededCode =>
                $"Budget insuffisant : {FormatMegabytes(request.BlueprintEstimatedBytes)} requis pour " +
                $"{FormatMegabytes(request.AvailableBudgetBytes)} disponibles.",
            _ => result.Message,
        };

    private static string FormatMegabytes(long? bytes) => bytes is null
        ? "une taille inconnue"
        : $"{bytes.Value / 1024d / 1024d:0.###} Mo";

    private static string PlacementDistributionLabel(EditorPlacementDistribution distribution) => distribution switch
    {
        EditorPlacementDistribution.Line => "ligne",
        EditorPlacementDistribution.Grid => "grille",
        EditorPlacementDistribution.DeterministicScatter => "dispersion déterministe",
        _ => "placement répété",
    };

    private void CloseUx0D() => _semanticClipboard.Clear();

    private sealed record PreparedRepeatedPlacement(
        StudioPlacementPreview Preview,
        EntityClipboardPayload? Payload,
        IReadOnlyDictionary<EntityId, EntityId>? EntityMap,
        long? EstimatedBytes)
    {
        internal static PreparedRepeatedPlacement Invalid(string message) => new(
            new StudioPlacementPreview(false, message, [], 0),
            null,
            null,
            null);
    }
}
