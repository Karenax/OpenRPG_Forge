using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SetAreaAuthoringHandler _setAreaAuthoring = new(new SystemClock());
    private StudioAreaLayersDraft? _approvedAreaLayers;

    internal StudioAreaLayersDraft CreateAreaLayersDraft()
    {
        var area = ActiveArea ?? throw new InvalidOperationException("Aucune zone n’est active.");
        return DraftFromProfile(area.Id, area.Authoring);
    }

    internal StudioAreaLayerCatalog CreateAreaLayerCatalog()
    {
        var area = ActiveArea ?? throw new InvalidOperationException("Aucune zone n’est active.");
        return new(area.Entities
            .Select(entity => new StudioAreaLayerEntityOption(entity.Id, DescribeAreaEntity(area, entity.Id)))
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray());
    }

    internal StudioAreaLayerEditResult AddAreaLayer(StudioAreaLayersDraft draft, Guid? parentId)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (parentId is not null && draft.Layers.All(item => item.Id != parentId.Value))
        {
            return new(false, "Le groupe parent n’existe plus.", draft);
        }

        if (parentId is not null && TryBuildAreaLayers(draft, true, out _, out var profile, out _, out _) &&
            AreaLayerHierarchy.Resolve(profile!).Single(item => item.LayerId == parentId.Value).Locked)
        {
            return new(false, "Ce groupe est verrouillé. Déverrouillez-le avant d’y ajouter un calque.", draft);
        }

        var name = NextLayerName(draft.Layers, parentId);
        var layer = new StudioAreaLayerEntryDraft(
            Guid.NewGuid(),
            name,
            parentId,
            true,
            false,
            "Équipe du module",
            [],
            true,
            "studio:ux1-d-new-layer");
        var updated = draft with { Layers = [.. draft.Layers, layer] };
        return new(true, $"Calque « {name} » ajouté au brouillon.", updated, layer.Id);
    }

    internal StudioAreaLayerEditResult UpdateAreaLayer(
        StudioAreaLayersDraft draft,
        StudioAreaLayerEntryDraft replacement)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(replacement);
        if (draft.Layers.All(item => item.Id != replacement.Id))
        {
            return new(false, "Le calque à modifier n’existe plus.", draft);
        }

        var updated = draft with
        {
            Layers = draft.Layers.Select(item => item.Id == replacement.Id ? replacement : item).ToArray(),
        };
        return new(true, "Brouillon mis à jour · prévisualisez avant d’appliquer.", updated, replacement.Id);
    }

    internal StudioAreaLayerEditResult ReparentAreaLayer(
        StudioAreaLayersDraft draft,
        Guid layerId,
        Guid? parentId)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!TryBuildAreaLayers(draft, true, out _, out var profile, out _, out var message))
        {
            return new(false, message, draft, layerId);
        }

        var mutation = AreaLayerAuthoringService.Reparent(profile!, layerId, parentId);
        if (!mutation.Succeeded)
        {
            return new(false, HumanizeAreaLayer(mutation.Diagnostics.FirstOrDefault()), draft, layerId);
        }

        return new(
            true,
            parentId is null ? "Calque replacé à la racine du document." : "Calque déplacé dans le groupe choisi.",
            DraftFromProfile(draft.AreaId, mutation.Profile),
            layerId);
    }

    internal StudioAreaLayerEditResult AssignAreaLayerEntity(
        StudioAreaLayersDraft draft,
        Guid layerId,
        EntityId entityId)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(entityId);
        if (!TryBuildAreaLayers(draft, true, out _, out var profile, out var candidate, out var message))
        {
            return new(false, message, draft, layerId);
        }

        var mutation = AreaLayerAuthoringService.AssignEntity(candidate!, layerId, entityId);
        if (!mutation.Succeeded)
        {
            return new(false, HumanizeAreaLayer(mutation.Diagnostics.FirstOrDefault()), draft, layerId);
        }

        return new(
            true,
            "Objet affecté au calque · son ancienne affectation a été remplacée.",
            DraftFromProfile(draft.AreaId, mutation.Profile),
            layerId);
    }

    internal StudioAreaLayerEditResult RemoveAreaLayer(StudioAreaLayersDraft draft, Guid layerId)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var layer = draft.Layers.SingleOrDefault(item => item.Id == layerId);
        if (layer is null)
        {
            return new(false, "Le calque à retirer n’existe plus.", draft);
        }
        if (draft.Layers.Any(item => item.ParentId == layerId))
        {
            return new(false, "Déplacez d’abord les sous-calques hors de ce groupe.", draft, layerId);
        }
        if (layer.EntityIds.Count > 0)
        {
            return new(false, "Réaffectez d’abord les objets contenus dans ce calque.", draft, layerId);
        }

        var updated = draft with { Layers = draft.Layers.Where(item => item.Id != layerId).ToArray() };
        return new(true, $"Calque « {layer.Name} » retiré du brouillon.", updated,
            updated.Layers.FirstOrDefault()?.Id);
    }

    internal StudioAreaLayersPreview PreviewAreaLayers(
        StudioAreaLayersDraft draft,
        Guid? selectedLayerId,
        bool includeHidden)
    {
        ArgumentNullException.ThrowIfNull(draft);
        _approvedAreaLayers = null;
        if (!TryBuildAreaLayers(draft, true, out var command, out _, out var candidate, out var message))
        {
            RecordAreaLayerDiagnostic("area.layers.invalid", message);
            return new(false, message, string.Empty, false);
        }

        var dryRun = _setAreaAuthoring.Handle(History!.State, command!);
        if (!dryRun.Succeeded)
        {
            var diagnostic = dryRun.Diagnostics.FirstOrDefault();
            var human = HumanizeAreaLayer(diagnostic?.Code);
            RecordAreaLayerDiagnostic(diagnostic?.Code ?? "area.layers.refused", human);
            return new(false, human, string.Empty, false);
        }

        var area = candidate!;
        var hierarchy = AreaLayerHierarchy.Resolve(area.Authoring);
        var selected = selectedLayerId is not null && hierarchy.Any(item => item.LayerId == selectedLayerId.Value)
            ? selectedLayerId
            : hierarchy.FirstOrDefault()?.LayerId;
        var selection = selected is null
            ? null
            : AreaLayerAuthoringService.SelectGroup(area, selected.Value, includeDescendants: true, includeHidden);
        var export = selected is null
            ? null
            : AreaLayerAuthoringService.PreviewPartialExport(area, [selected.Value]);
        var selectedLabels = selection?.EntityIds.Select(id => DescribeAreaEntity(area, id)).ToArray() ?? [];
        var exportEntityLabels = export?.Entities.Select(entity => DescribeAreaEntity(area, entity.Id)).ToArray() ?? [];
        var exportBlueprintLabels = export?.BlueprintDependencies
            .Select(id => Project!.Blueprints.FirstOrDefault(item => item.Id == id)?.DisplayName ?? "Modèle requis")
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(item => item, StringComparer.CurrentCultureIgnoreCase)
            .ToArray() ?? [];
        _approvedAreaLayers = draft;
        return new(
            true,
            "Aperçu validé · aucun document ni objet modifié.",
            BuildAreaLayerSummary(draft, hierarchy, selection, export),
            true,
            selected,
            hierarchy,
            selection?.EntityIds ?? [],
            selectedLabels,
            export?.Layers.Select(item => item.Name).ToArray() ?? [],
            exportEntityLabels,
            exportBlueprintLabels);
    }

    internal StudioAreaLayersResult ApplyApprovedAreaLayers()
    {
        if (_approvedAreaLayers is null || History is null)
        {
            return new(false, "Prévisualisez d’abord les calques et leur export.");
        }

        var draft = _approvedAreaLayers;
        if (!TryBuildAreaLayers(draft, false, out var command, out _, out _, out var message))
        {
            _approvedAreaLayers = null;
            return new(false, message);
        }

        var dryRun = _setAreaAuthoring.Handle(History.State, command! with { DryRun = true });
        if (!dryRun.Succeeded)
        {
            _approvedAreaLayers = null;
            return new(false, HumanizeAreaLayer(dryRun.Diagnostics.FirstOrDefault()?.Code));
        }

        var applied = _setAreaAuthoring.Handle(History.State, command!);
        if (!applied.Succeeded)
        {
            _approvedAreaLayers = null;
            return new(false, HumanizeAreaLayer(applied.Diagnostics.FirstOrDefault()?.Code));
        }

        History.Record(applied);
        InvalidateContentIndexes();
        _approvedAreaLayers = null;
        return new(true, $"{draft.Layers.Count} calque(s) appliqué(s) à la zone · Ctrl+Z pour annuler");
    }

    internal void CancelAreaLayers() => _approvedAreaLayers = null;

    internal StudioUx1DSmokeResult RunUx1DContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux1d-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var blueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1d10000-0000-4000-8000-000000000001")),
                "pierre-runique",
                "Pierre runique",
                BlueprintKind.Placeable);
            var sourceId = AreaId.From(Guid.Parse("c1d10000-0000-4000-8000-000000000002"));
            var neighborId = AreaId.From(Guid.Parse("c1d10000-0000-4000-8000-000000000003"));
            var first = new EntityInstance(
                EntityId.From(Guid.Parse("c1d10000-0000-4000-8000-000000000004")),
                blueprint.Id,
                new TransformValue(new(-2, 0, 0), QuaternionValue.Identity, Vector3Value.One));
            var second = new EntityInstance(
                EntityId.From(Guid.Parse("c1d10000-0000-4000-8000-000000000005")),
                blueprint.Id,
                new TransformValue(new(2, 0, 0), QuaternionValue.Identity, Vector3Value.One));
            var secret = new EntityInstance(
                EntityId.From(Guid.Parse("c1d10000-0000-4000-8000-000000000006")),
                blueprint.Id,
                new TransformValue(new(0, 0, 3), QuaternionValue.Identity, Vector3Value.One));
            var source = new AreaDefinition(
                sourceId,
                "atelier-des-calques",
                "Atelier des calques",
                [first, second, secret],
                AreaKind.Exterior,
                new AreaDimensions(36, 36));
            var neighbor = new AreaDefinition(
                neighborId,
                "jardin-temoin",
                "Jardin témoin",
                [],
                AreaKind.Exterior,
                new AreaDimensions(24, 24));
            var project = new ProjectDefinition(
                ProjectId.From(Guid.Parse("c1d10000-0000-4000-8000-000000000007")),
                "Studio UX1-D",
                [blueprint],
                [source, neighbor]);
            var created = ProjectSession.Create(tempRoot, project);
            if (!created.Succeeded || !editing.Open(tempRoot).Succeeded)
            {
                return FailedUx1DSmoke();
            }

            var originalProject = editing.Project!;
            var originalArea = originalProject.Areas.Single(item => item.Id == sourceId);
            var originalNeighbor = originalProject.Areas.Single(item => item.Id == neighborId);
            var rootId = Guid.Parse("c1d10000-0000-4000-8000-000000000010");
            var stagingId = Guid.Parse("c1d10000-0000-4000-8000-000000000011");
            var actorsId = Guid.Parse("c1d10000-0000-4000-8000-000000000012");
            var privateId = Guid.Parse("c1d10000-0000-4000-8000-000000000013");
            var draft = editing.CreateAreaLayersDraft() with
            {
                RequireRuntimeMetadata = true,
                Layers =
                [
                    new(rootId, "Monde", null, true, false, "Équipe monde", [], true, "test:ux1-d"),
                    new(stagingId, "Préparation", rootId, true, false, "Équipe monde", [], true, "test:ux1-d"),
                    new(actorsId, "Rencontres", stagingId, true, false, "Équipe gameplay", [], true, "test:ux1-d"),
                    new(privateId, "Secrets", rootId, false, false, "Maître du jeu", [], false, "test:ux1-d"),
                ],
            };

            var reparented = editing.ReparentAreaLayer(draft, actorsId, rootId);
            draft = reparented.Draft;
            var assignedFirst = editing.AssignAreaLayerEntity(draft, actorsId, first.Id);
            draft = assignedFirst.Draft;
            var assignedSecond = editing.AssignAreaLayerEntity(draft, actorsId, second.Id);
            draft = assignedSecond.Draft;
            var assignedSecret = editing.AssignAreaLayerEntity(draft, privateId, secret.Id);
            draft = assignedSecret.Draft;
            var root = draft.Layers.Single(item => item.Id == rootId);
            draft = editing.UpdateAreaLayer(draft, root with { Locked = true }).Draft;
            var privateLayer = draft.Layers.Single(item => item.Id == privateId);
            draft = editing.UpdateAreaLayer(draft, privateLayer with { Locked = true }).Draft;

            var invalidDuplicate = new StudioAreaLayerEntryDraft(
                Guid.Parse("c1d10000-0000-4000-8000-000000000014"),
                "Monde",
                null,
                true,
                false,
                "Équipe bis",
                [first.Id],
                true,
                "test:ux1-d");
            var invalid = editing.PreviewAreaLayers(
                draft with { Layers = [.. draft.Layers, invalidDuplicate] },
                rootId,
                includeHidden: false);
            var invalidBlocked = !invalid.Succeeded;
            var humanDiagnostics = invalidBlocked && IsHumanAreaLayerMessage(invalid.Message);

            var preview = editing.PreviewAreaLayers(draft, rootId, includeHidden: false);
            var previewValid = preview.Succeeded && preview.CanApply && preview.Hierarchy?.Count == 4;
            var dryRun = previewValid && ReferenceEquals(originalProject, editing.Project);
            var childState = preview.Hierarchy?.Single(item => item.LayerId == actorsId);
            var privateState = preview.Hierarchy?.Single(item => item.LayerId == privateId);
            var nestedHierarchy = childState?.ParentId == rootId && childState.Depth == 1;
            var visibilityAndLocking = childState is { Visible: true, Locked: true } &&
                                       privateState is { Visible: false, Locked: true, ExportEnabled: false };
            var ownership = childState?.Owner == "Équipe gameplay" && privateState?.Owner == "Maître du jeu";
            var groupSelection = preview.SelectedEntityIds?.SequenceEqual([first.Id, second.Id]) == true;
            var partialExport = preview.ExportLayerLabels?.Count == 3 &&
                                preview.ExportEntityLabels?.Count == 2 &&
                                preview.ExportBlueprintLabels?.Single() == blueprint.DisplayName;
            var directReparent = reparented.Succeeded && nestedHierarchy;
            var entityAssignment = assignedFirst.Succeeded && assignedSecond.Succeeded && assignedSecret.Succeeded &&
                                   draft.Layers.Single(item => item.Id == actorsId).EntityIds.SequenceEqual(
                                       [first.Id, second.Id]) &&
                                   draft.Layers.Single(item => item.Id == privateId).EntityIds.Single() == secret.Id;

            var applied = editing.ApplyApprovedAreaLayers();
            var appliedProject = editing.Project!;
            var appliedArea = appliedProject.Areas.Single(item => item.Id == sourceId);
            var otherAuthoringPreserved = applied.Succeeded &&
                                          ReferenceEquals(appliedArea.Authoring.Map, originalArea.Authoring.Map) &&
                                          SameAreaExperience(
                                              appliedArea.Authoring.Experience,
                                              originalArea.Authoring.Experience) &&
                                          appliedArea.Authoring.UserEvents.SequenceEqual(originalArea.Authoring.UserEvents) &&
                                          ReferenceEquals(appliedArea.World, originalArea.World) &&
                                          ReferenceEquals(appliedArea.RuntimePolicies, originalArea.RuntimePolicies);
            var neighborPreserved = ReferenceEquals(
                originalNeighbor,
                appliedProject.Areas.Single(item => item.Id == neighborId));
            var runtime = AreaLayerRuntimeResolver.Resolve(appliedArea, required: true);
            var optional = AreaLayerRuntimeResolver.Resolve(originalArea, required: false);
            var required = AreaLayerRuntimeResolver.Resolve(originalArea, required: true);
            var runtimeSemantics = runtime.Handle is { SchemaVersion: 1, Layers.Count: 4 } &&
                                   runtime.Diagnostics.Count == 0 && optional.Diagnostics.Count == 0 &&
                                   required.Diagnostics.Single().Code == AreaLayerRuntimeResolver.RequiredMissingCode;

            var undo = editing.Undo();
            var undone = ReferenceEquals(originalProject, editing.Project);
            var redo = editing.Redo();
            var redone = ReferenceEquals(appliedProject, editing.Project);
            var undoRedo = undo.Succeeded && undone && redo.Succeeded && redone;

            var save = editing.Save();
            editing.Close();
            var reopen = reopened.Open(tempRoot);
            var restored = reopened.Project?.Areas.SingleOrDefault(item => item.Id == sourceId)?.Authoring;
            var persistence = save.Succeeded && reopen.Succeeded && restored is not null &&
                              restored.RequireLayerRuntimeMetadata && restored.Layers.Count == 4 &&
                              restored.Layers.Single(item => item.Id == actorsId).ParentId == rootId &&
                              restored.Layers.Single(item => item.Id == actorsId).EntityIds.SequenceEqual(
                                  [first.Id, second.Id]) &&
                              !restored.Layers.Single(item => item.Id == privateId).ExportEnabled &&
                              restored.Layers.All(item => item.SchemaVersion == AreaEditorLayer.CurrentSchemaVersion &&
                                                          !string.IsNullOrWhiteSpace(item.Provenance));

            var succeeded = previewValid && dryRun && nestedHierarchy && visibilityAndLocking && ownership &&
                            groupSelection && partialExport && directReparent && entityAssignment &&
                            otherAuthoringPreserved && neighborPreserved && runtimeSemantics && undoRedo &&
                            persistence && invalidBlocked && humanDiagnostics;
            return new(
                succeeded,
                previewValid,
                dryRun,
                nestedHierarchy,
                visibilityAndLocking,
                ownership,
                groupSelection,
                partialExport,
                directReparent,
                entityAssignment,
                otherAuthoringPreserved,
                neighborPreserved,
                runtimeSemantics,
                undoRedo,
                persistence,
                invalidBlocked,
                humanDiagnostics);
        }
        finally
        {
            editing.Close();
            reopened.Close();
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static bool SameAreaExperience(
        AreaExperienceAuthoringProfile candidate,
        AreaExperienceAuthoringProfile expected) =>
        candidate.Id == expected.Id &&
        candidate.SchemaVersion == expected.SchemaVersion &&
        string.Equals(candidate.Provenance, expected.Provenance, StringComparison.Ordinal) &&
        candidate.RequireRuntimeMetadata == expected.RequireRuntimeMetadata &&
        candidate.AudioSnapshots.SequenceEqual(expected.AudioSnapshots) &&
        ReferenceEquals(candidate.Visibility, expected.Visibility) &&
        ReferenceEquals(candidate.Lod, expected.Lod);

    private bool TryBuildAreaLayers(
        StudioAreaLayersDraft draft,
        bool dryRun,
        out SetAreaAuthoringCommand? command,
        out AreaAuthoringProfile? profile,
        out AreaDefinition? candidate,
        out string message)
    {
        command = null;
        profile = null;
        candidate = null;
        message = string.Empty;
        if (History is null || Project is null || Session is null)
        {
            message = "Aucun module n’est ouvert.";
            return false;
        }

        var area = Project.Areas.FirstOrDefault(item => item.Id == draft.AreaId);
        if (area is null)
        {
            message = "La zone à organiser n’existe plus.";
            return false;
        }
        if (draft.Layers.Any(item => string.IsNullOrWhiteSpace(item.Name)))
        {
            message = "Donnez un nom visible à chaque calque.";
            return false;
        }
        if (draft.Layers.Any(item => string.IsNullOrWhiteSpace(item.Owner)))
        {
            message = "Indiquez une équipe ou une personne responsable pour chaque calque.";
            return false;
        }

        try
        {
            var layers = draft.Layers.Select(item =>
            {
                var source = area.Authoring.Layers.SingleOrDefault(layer => layer.Id == item.Id);
                var unchanged = source is not null &&
                                source.Name == item.Name.Trim() &&
                                source.ParentId == item.ParentId &&
                                source.Visible == item.Visible &&
                                source.Locked == item.Locked &&
                                source.Owner == item.Owner.Trim() &&
                                source.EntityIds.SequenceEqual(item.EntityIds) &&
                                source.ExportEnabled == item.ExportEnabled;
                var provenance = unchanged
                    ? source!.Provenance
                    : "studio:ux1-d-area-layers";
                return new AreaEditorLayer(
                    item.Id,
                    item.Name,
                    item.ParentId,
                    item.Visible,
                    item.Locked,
                    item.Owner,
                    item.EntityIds,
                    AreaEditorLayer.CurrentSchemaVersion,
                    provenance,
                    item.ExportEnabled);
            }).ToArray();
            profile = new AreaAuthoringProfile(
                area.Authoring.SchemaVersion,
                area.Authoring.UserEvents,
                area.Authoring.Map,
                layers,
                area.Authoring.Experience,
                draft.RequireRuntimeMetadata);
            candidate = area.WithAuthoring(profile);
            command = new SetAreaAuthoringCommand(
                CommandId.From(Guid.NewGuid()),
                History.State.Revision,
                area.Id,
                profile,
                dryRun);
            return true;
        }
        catch (ArgumentException)
        {
            message = "L’organisation des calques est incomplète. Vérifiez les noms, groupes et objets affectés.";
            return false;
        }
    }

    private static StudioAreaLayersDraft DraftFromProfile(AreaId areaId, AreaAuthoringProfile profile) => new(
        areaId,
        profile.Layers.Select(item => new StudioAreaLayerEntryDraft(
            item.Id,
            item.Name,
            item.ParentId,
            item.Visible,
            item.Locked,
            item.Owner,
            item.EntityIds,
            item.ExportEnabled,
            item.Provenance)).ToArray(),
        profile.RequireLayerRuntimeMetadata);

    private static string NextLayerName(IReadOnlyList<StudioAreaLayerEntryDraft> layers, Guid? parentId)
    {
        const string stem = "Nouveau calque";
        var siblingNames = layers.Where(item => item.ParentId == parentId)
            .Select(item => item.Name)
            .ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        if (!siblingNames.Contains(stem))
        {
            return stem;
        }
        for (var index = 2; index < 10_000; index++)
        {
            var candidate = $"{stem} {index}";
            if (!siblingNames.Contains(candidate))
            {
                return candidate;
            }
        }
        return $"{stem} final";
    }

    private static string BuildAreaLayerSummary(
        StudioAreaLayersDraft draft,
        IReadOnlyList<AreaLayerEffectiveState> hierarchy,
        AreaLayerSelection? selection,
        AreaLayerPartialExportPlan? export)
    {
        if (hierarchy.Count == 0)
        {
            return draft.RequireRuntimeMetadata
                ? "Aucun calque · les métadonnées requises empêchent l’application."
                : "Aucun calque · la zone reste compatible et le runtime ignore cette extension optionnelle.";
        }

        var hidden = hierarchy.Count(item => !item.Visible);
        var locked = hierarchy.Count(item => item.Locked);
        return $"{hierarchy.Count} calque(s), dont {hidden} masqué(s) et {locked} verrouillé(s) après héritage.\n" +
               $"Groupe aperçu : {selection?.LayerIds.Count ?? 0} calque(s) et " +
               $"{selection?.EntityIds.Count ?? 0} objet(s) visibles dans la vue 3D.\n" +
               $"Export partiel : {export?.Layers.Count ?? 0} calque(s), {export?.Entities.Count ?? 0} objet(s) et " +
               $"{export?.BlueprintDependencies.Count ?? 0} modèle(s) requis.";
    }

    private static string HumanizeAreaLayer(string? code)
    {
        var stableCode = code?.Split(':', 2)[0];
        return stableCode switch
        {
            AreaLayerAuthoringValidator.IdentityCode => "Chaque calque doit avoir un nom et un responsable.",
            AreaLayerAuthoringValidator.SchemaCode => "Un calque utilise une version qui n’est plus prise en charge.",
            AreaLayerAuthoringValidator.ProvenanceCode => "L’origine d’une modification de calque est manquante.",
            AreaLayerAuthoringValidator.ParentCode => "Le groupe parent choisi n’existe plus dans cette zone.",
            AreaLayerAuthoringValidator.CycleCode => "Un calque ne peut pas contenir l’un de ses propres groupes parents.",
            AreaLayerAuthoringValidator.DuplicateNameCode => "Deux calques voisins portent le même nom.",
            AreaLayerAuthoringValidator.EntityReferenceCode => "Un objet affecté à un calque n’existe plus dans la zone.",
            AreaLayerAuthoringValidator.EntityOwnershipCode => "Un objet ne peut appartenir qu’à un seul calque.",
            AreaLayerAuthoringService.MissingLayerCode => "Le calque choisi n’existe plus.",
            AreaLayerAuthoringService.LockedLayerCode => "Ce calque est verrouillé, directement ou par son groupe parent.",
            AreaLayerRuntimeResolver.RequiredMissingCode => "Ajoutez au moins un calque ou désactivez leur exigence au runtime.",
            "command.revision_conflict" => "Le module a changé depuis l’aperçu. Prévisualisez à nouveau.",
            "area.authoring.area_missing" => "La zone à organiser n’existe plus.",
            _ => "L’organisation des calques a été refusée. Vérifiez les noms, groupes et affectations.",
        };
    }

    private void RecordAreaLayerDiagnostic(string code, string message) => AddOperationalDiagnostic(
        code,
        message,
        target: null,
        documentPath: "zones",
        Forge.Application.Creator.CreatorDiagnosticSeverity.Error,
        "area:layers");

    private static bool IsHumanAreaLayerMessage(string message) =>
        !message.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains(".json", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("area.layer", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("/", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("\\", StringComparison.OrdinalIgnoreCase);

    private static StudioUx1DSmokeResult FailedUx1DSmoke() => new(
        false, false, false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, false);

    private void CloseUx1D() => _approvedAreaLayers = null;
}
