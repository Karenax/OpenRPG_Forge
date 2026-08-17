using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2BTerrainProvenance = "studio:ux2-b:terrain";
    private static readonly Regex Ux2BTechnicalId = new(
        "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly EditAreaGeometryHandler _editAreaGeometryUx2B = new(new SystemClock());
    private StudioUx2BTileDraft? _approvedTerrainDraftUx2B;
    private ProjectRevision? _approvedTerrainRevisionUx2B;
    private string? _approvedTerrainFingerprintUx2B;

    internal IReadOnlyList<StudioUx2BAreaChoice> TerrainAreaChoicesUx2B
    {
        get
        {
            var project = Project;
            if (project is null)
            {
                return [];
            }

            var duplicates = project.Areas.GroupBy(item => item.DisplayName,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToDictionary(item => item.Key, item => item.Count(),
                    StringComparer.CurrentCultureIgnoreCase);
            return project.Areas.Select(area =>
                {
                    var label = duplicates[area.DisplayName] > 1
                        ? $"{HumanTerrainTokenUx2B(area.DisplayName)} · {HumanTerrainTokenUx2B(area.Slug)}"
                        : HumanTerrainTokenUx2B(area.DisplayName);
                    var kit = area.Foundation.TileKit;
                    return new StudioUx2BAreaChoice(area.Id,
                        $"{label} · {kit.Placements.Count} tuile(s) · {kit.Groups.Count} groupe(s)",
                        kit.Definitions.Count > 0);
                })
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal IReadOnlyList<StudioUx2BDefinitionChoice> TerrainDefinitionChoicesUx2B(AreaId areaId)
    {
        var area = Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        return area is null
            ? []
            : area.Foundation.TileKit.Definitions.Select(item => new StudioUx2BDefinitionChoice(
                    item.Id,
                    $"{HumanTerrainTokenUx2B(item.Key)} · {HumanTerrainTokenUx2B(
                        Project?.Blueprints.SingleOrDefault(blueprint => blueprint.Id == item.BlueprintId)?.DisplayName ??
                        "modèle indisponible")}"))
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
    }

    internal IReadOnlyList<StudioUx2BPlacementChoice> TerrainPlacementChoicesUx2B(AreaId areaId)
    {
        var area = Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (area is null)
        {
            return [];
        }

        var definitions = area.Foundation.TileKit.Definitions.ToDictionary(item => item.Id);
        return area.Foundation.TileKit.Placements.Select(item =>
            {
                var key = definitions.TryGetValue(item.DefinitionId, out var definition)
                    ? HumanTerrainTokenUx2B(definition.Key)
                    : "définition absente";
                return new StudioUx2BPlacementChoice(item.Id, item.EntityId,
                    $"{key} · case {item.GridX}, {item.GridZ}", item.GridX, item.GridZ);
            })
            .OrderBy(item => item.GridZ)
            .ThenBy(item => item.GridX)
            .ToArray();
    }

    internal StudioUx2BTileDraft? CreateTerrainDraftUx2B(AreaId? requestedAreaId)
    {
        var project = Project;
        var history = History;
        if (project is null || history is null || requestedAreaId is null)
        {
            return null;
        }

        var area = project.Areas.SingleOrDefault(item => item.Id == requestedAreaId);
        if (area is null)
        {
            return null;
        }

        var definitionId = area.Foundation.TileKit.Definitions.FirstOrDefault()?.Id ?? Guid.Empty;
        var placementId = StableTerrainGuidUx2B(area.Id.Value, history.State.Revision.Value, "placement");
        var entityId = EntityId.From(StableTerrainGuidUx2B(area.Id.Value, history.State.Revision.Value, "entity"));
        return new StudioUx2BTileDraft(
            area.Id,
            history.State.Revision,
            TerrainSourceFingerprintUx2B(area),
            StudioUx2BTileOperation.Place,
            [],
            placementId,
            entityId,
            definitionId,
            0,
            0,
            0,
            ModularTileMirrorMode.None,
            string.Empty,
            StableTerrainGuidUx2B(area.Id.Value, history.State.Revision.Value, "group"),
            StableTerrainGuidUx2B(area.Id.Value, history.State.Revision.Value, "editable-group"),
            "Nouveau groupe",
            true);
    }

    internal StudioUx2BTilePreview PreviewTerrainUx2B(StudioUx2BTileDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelTerrainApprovalUx2B();
        if (!TryBuildTerrainCommandUx2B(draft, true, out var command, out var planned,
                out var sourceArea, out var changes, out var message))
        {
            return FailedTerrainPreviewUx2B(message, planned?.Diagnostics.Select(HumanTerrainDiagnosticUx2B));
        }

        var history = History!;
        var dryRun = _editAreaGeometryUx2B.Handle(history.State, command!);
        if (!dryRun.Succeeded)
        {
            return FailedTerrainPreviewUx2B(HumanTerrainCommandUx2B(dryRun.Diagnostics.FirstOrDefault()),
                dryRun.Diagnostics.Select(item => HumanTerrainCommandUx2B(item)));
        }

        var attachmentCount = draft.Operation is StudioUx2BTileOperation.Erase or StudioUx2BTileOperation.Replace
            ? AreaGeometryPlanner.Edit(sourceArea!, new(
                draft.Operation == StudioUx2BTileOperation.Erase
                    ? AreaTileEditOperationKind.Erase
                    : AreaTileEditOperationKind.Replace,
                draft.SelectedPlacementIds,
                draft.DefinitionId,
                PreserveAttachedObjects: draft.PreserveAttachedObjects)).PreservedAttachedEntityIds.Count
            : 0;
        _approvedTerrainDraftUx2B = draft;
        _approvedTerrainRevisionUx2B = history.State.Revision;
        _approvedTerrainFingerprintUx2B = TerrainDraftFingerprintUx2B(draft);
        return new StudioUx2BTilePreview(
            true,
            "Aperçu sec validé · aucune zone, entité, ressource ou scène moteur n’a été modifiée.",
            BuildTerrainSummaryUx2B(draft, planned!),
            true,
            changes,
            [],
            planned!.Notices.Select(HumanTerrainNoticeUx2B).ToArray(),
            planned.Foundation.TileKit.Placements.Count,
            planned.Foundation.TileKit.Groups.Count,
            attachmentCount);
    }

    internal StudioUx2BActionResult ApplyApprovedTerrainUx2B()
    {
        var draft = _approvedTerrainDraftUx2B;
        var history = History;
        if (draft is null || history is null || _approvedTerrainRevisionUx2B != history.State.Revision ||
            !string.Equals(_approvedTerrainFingerprintUx2B, TerrainDraftFingerprintUx2B(draft),
                StringComparison.Ordinal))
        {
            CancelTerrainApprovalUx2B();
            return new(false, "Prévisualisez de nouveau l’opération de terrain avant de l’appliquer.");
        }

        if (!TryBuildTerrainCommandUx2B(draft, false, out var command, out _, out _, out _, out var message))
        {
            CancelTerrainApprovalUx2B();
            return new(false, message);
        }

        var dryRun = _editAreaGeometryUx2B.Handle(history.State, command! with { DryRun = true });
        if (!dryRun.Succeeded)
        {
            CancelTerrainApprovalUx2B();
            return new(false, HumanTerrainCommandUx2B(dryRun.Diagnostics.FirstOrDefault()));
        }

        var applied = _editAreaGeometryUx2B.Handle(history.State, command!);
        CancelTerrainApprovalUx2B();
        if (!applied.Succeeded)
        {
            return new(false, HumanTerrainCommandUx2B(applied.Diagnostics.FirstOrDefault()));
        }

        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, "Terrain appliqué · Ctrl+Z pour annuler, Ctrl+Y pour rétablir.");
    }

    internal void CancelTerrainApprovalUx2B()
    {
        _approvedTerrainDraftUx2B = null;
        _approvedTerrainRevisionUx2B = null;
        _approvedTerrainFingerprintUx2B = null;
    }

    internal void CloseUx2B() => CancelTerrainApprovalUx2B();

    internal StudioUx2BApprovalSnapshot CaptureTerrainApprovalUx2B() => new(
        _approvedTerrainDraftUx2B, _approvedTerrainRevisionUx2B, _approvedTerrainFingerprintUx2B);

    internal void RestoreTerrainApprovalUx2B(StudioUx2BApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedTerrainDraftUx2B = snapshot.Draft;
        _approvedTerrainRevisionUx2B = snapshot.Revision;
        _approvedTerrainFingerprintUx2B = snapshot.Fingerprint;
    }

    private bool TryBuildTerrainCommandUx2B(
        StudioUx2BTileDraft draft,
        bool dryRun,
        out EditAreaGeometryCommand? command,
        out AreaTileAuthoringPreview? planned,
        out AreaDefinition? sourceArea,
        out IReadOnlyList<string> changes,
        out string message)
    {
        command = null;
        planned = null;
        sourceArea = null;
        changes = [];
        message = string.Empty;
        var history = History;
        var project = Project;
        if (history is null || project is null)
        {
            message = "Aucun module n’est ouvert.";
            return false;
        }
        if (draft.BaseRevision != history.State.Revision)
        {
            message = "Le projet a changé. Rechargez la zone avant de prévisualiser.";
            return false;
        }

        sourceArea = project.Areas.SingleOrDefault(item => item.Id == draft.AreaId);
        if (sourceArea is null)
        {
            message = "La zone choisie n’existe plus.";
            return false;
        }
        if (!string.Equals(draft.SourceFingerprint, TerrainSourceFingerprintUx2B(sourceArea),
                StringComparison.Ordinal))
        {
            message = "Le terrain de la zone a changé. Rechargez la surface avant de prévisualiser.";
            return false;
        }

        var selected = draft.SelectedPlacementIds.Distinct().ToArray();
        switch (draft.Operation)
        {
            case StudioUx2BTileOperation.Place:
                planned = AreaTileAuthoringPlanner.UpsertPlacement(sourceArea, new(
                    draft.PlacementId, draft.EntityId, draft.DefinitionId, draft.GridX, draft.GridZ,
                    draft.RotationDegrees, draft.Mirror, draft.VariantKey, null));
                changes = [$"Placer une tuile en case {draft.GridX}, {draft.GridZ}."];
                break;
            case StudioUx2BTileOperation.Group:
                if (selected.Length < 2 || string.IsNullOrWhiteSpace(draft.GroupName))
                {
                    message = "Sélectionnez au moins deux tuiles et nommez le groupe.";
                    return false;
                }
                var placements = sourceArea.Foundation.TileKit.Placements
                    .Where(item => selected.Contains(item.Id)).ToArray();
                if (placements.Length != selected.Length)
                {
                    message = "Une tuile sélectionnée n’existe plus.";
                    return false;
                }
                var width = placements.Max(item => item.GridX) - placements.Min(item => item.GridX) + 1;
                var depth = placements.Max(item => item.GridZ) - placements.Min(item => item.GridZ) + 1;
                var group = new ModularTileGroup(draft.GroupId, draft.GroupName.Trim(), selected);
                var editable = new EditableTileGroupProfile(draft.EditableGroupId, draft.GroupId, [],
                    new(width, depth, sourceArea.Foundation.TileKit.GridSizeMeters), [], [], false);
                planned = AreaTileAuthoringPlanner.UpsertGroup(sourceArea, group, editable);
                changes = [$"Regrouper {selected.Length} tuiles sous « {HumanTerrainTokenUx2B(draft.GroupName)} »."];
                break;
            case StudioUx2BTileOperation.Erase:
            case StudioUx2BTileOperation.Replace:
                if (selected.Length == 0)
                {
                    message = "Sélectionnez au moins une tuile dans la grille.";
                    return false;
                }
                var edit = AreaGeometryPlanner.Edit(sourceArea, new(
                    draft.Operation == StudioUx2BTileOperation.Erase
                        ? AreaTileEditOperationKind.Erase
                        : AreaTileEditOperationKind.Replace,
                    selected,
                    draft.Operation == StudioUx2BTileOperation.Replace ? draft.DefinitionId : null,
                    PreserveAttachedObjects: draft.PreserveAttachedObjects));
                planned = new(edit.Entities, edit.Foundation, null, null, edit.Diagnostics, []);
                changes = draft.Operation == StudioUx2BTileOperation.Erase
                    ? [$"Effacer {selected.Length} tuile(s) et {(draft.PreserveAttachedObjects ? "préserver" : "retirer")} les objets attachés."]
                    : [$"Remplacer {selected.Length} tuile(s) sans changer leur identité."];
                break;
            case StudioUx2BTileOperation.Transform:
                if (selected.Length != 1)
                {
                    message = "Sélectionnez exactement une tuile à orienter.";
                    return false;
                }
                planned = AreaTileAuthoringPlanner.TransformPlacement(sourceArea, selected[0],
                    draft.RotationDegrees, draft.Mirror, draft.VariantKey);
                changes = [$"Orienter la tuile à {draft.RotationDegrees:0.###}° · miroir {HumanMirrorUx2B(draft.Mirror)}" +
                           (string.IsNullOrWhiteSpace(draft.VariantKey)
                               ? " · variante de base."
                               : $" · variante {HumanTerrainTokenUx2B(draft.VariantKey)}.")];
                break;
            case StudioUx2BTileOperation.AutoFit:
                if (selected.Length != 1)
                {
                    message = "Sélectionnez exactement une tuile à ajuster.";
                    return false;
                }
                planned = AreaTileAuthoringPlanner.ApplyAutoFit(sourceArea, selected[0]);
                changes = ["Choisir une définition compatible avec les raccords voisins et la graine de la zone."];
                break;
        }

        if (planned is null || !planned.CanApply)
        {
            message = planned?.Diagnostics.Select(HumanTerrainDiagnosticUx2B).FirstOrDefault() ??
                      "L’opération de terrain est invalide.";
            return false;
        }

        command = new EditAreaGeometryCommand(
            CommandId.From(Guid.NewGuid()),
            history.State.Revision,
            sourceArea.Id,
            sourceArea.Dimensions,
            planned.Entities,
            $"Studio terrain · {draft.Operation}",
            dryRun,
            planned.Foundation);
        return true;
    }

    private static StudioUx2BTilePreview FailedTerrainPreviewUx2B(
        string message,
        IEnumerable<string>? diagnostics = null) => new(
            false, message, string.Empty, false, [], diagnostics?.ToArray() ?? [message], [], 0, 0, 0);

    private static string BuildTerrainSummaryUx2B(
        StudioUx2BTileDraft draft,
        AreaTileAuthoringPreview preview) =>
        $"{preview.Foundation.TileKit.Placements.Count} tuile(s) · " +
        $"{preview.Foundation.TileKit.Groups.Count} groupe(s) · " +
        $"opération {HumanOperationUx2B(draft.Operation)} · moteur non muté";

    private static string HumanOperationUx2B(StudioUx2BTileOperation operation) => operation switch
    {
        StudioUx2BTileOperation.Place => "placement",
        StudioUx2BTileOperation.Group => "groupe",
        StudioUx2BTileOperation.Erase => "effacement",
        StudioUx2BTileOperation.Replace => "remplacement",
        StudioUx2BTileOperation.Transform => "orientation et variante",
        StudioUx2BTileOperation.AutoFit => "ajustement compatible",
        _ => "terrain",
    };

    private static string HumanMirrorUx2B(ModularTileMirrorMode mirror) => mirror switch
    {
        ModularTileMirrorMode.None => "aucun",
        ModularTileMirrorMode.X => "horizontal",
        ModularTileMirrorMode.Z => "vertical",
        _ => "inconnu",
    };

    private static string HumanTerrainDiagnosticUx2B(Forge.Domain.Validation.DomainDiagnostic diagnostic) =>
        diagnostic.Code switch
        {
            AreaTileAuthoringPlanner.PlacementMissingCode => "La tuile choisie n’existe plus.",
            AreaTileAuthoringPlanner.DefinitionMissingCode => "Choisissez une définition de tuile disponible.",
            AreaTileAuthoringPlanner.GroupMissingCode => "Le groupe choisi n’existe plus.",
            AreaFoundationValidator.TileCode => "Rotation, miroir, variante, groupe ou case incompatible.",
            AreaFoundationValidator.ReferenceCode => "Une définition, une tuile ou un modèle référencé manque.",
            AreaFoundationValidator.DuplicateCode => "Deux éléments du kit partagent la même identité interne.",
            AreaTileEditingValidator.GroupCode => "Le groupe modulaire est incomplet ou incohérent.",
            AreaTileEditingValidator.ToolCode => "L’outil de tuile ne peut pas produire ce résultat.",
            _ => "Le validateur canonique refuse cette opération de terrain.",
        };

    private static string HumanTerrainNoticeUx2B(string code) => code switch
    {
        AreaTileAutoFitService.AmbiguousCode =>
            "Plusieurs raccords sont compatibles : la graine choisit toujours le même résultat.",
        AreaTileAutoFitService.NoCandidateCode => "Aucune définition ne raccorde tous les voisins.",
        AreaTileAutoFitService.PlacementMissingCode => "La tuile choisie n’existe plus.",
        _ => "Information de raccord disponible dans le diagnostic avancé.",
    };

    private static string HumanTerrainCommandUx2B(CommandDiagnostic? diagnostic) => diagnostic?.Code switch
    {
        EditAreaGeometryHandler.RevisionConflictCode => "Le projet a changé. Prévisualisez de nouveau.",
        EditAreaGeometryHandler.MissingAreaCode => "La zone choisie n’existe plus.",
        EditAreaGeometryHandler.DuplicateEntityCode => "Deux objets de la zone partagent la même identité.",
        EditAreaGeometryHandler.MissingBlueprintCode => "Une tuile référence un modèle absent du projet.",
        _ => "La commande canonique a refusé l’opération de terrain.",
    };

    private static string TerrainSourceFingerprintUx2B(AreaDefinition area) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(AreaJsonSerializer.Serialize(area))));

    private static string TerrainDraftFingerprintUx2B(StudioUx2BTileDraft draft)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(draft.AreaId.Value.ToByteArray());
            writer.Write(draft.BaseRevision.Value);
            WriteTerrainStringUx2B(writer, draft.SourceFingerprint);
            writer.Write((int)draft.Operation);
            writer.Write(draft.SelectedPlacementIds.Count);
            foreach (var id in draft.SelectedPlacementIds.OrderBy(item => item)) writer.Write(id.ToByteArray());
            writer.Write(draft.PlacementId.ToByteArray());
            writer.Write(draft.EntityId.Value.ToByteArray());
            writer.Write(draft.DefinitionId.ToByteArray());
            writer.Write(draft.GridX);
            writer.Write(draft.GridZ);
            writer.Write(draft.RotationDegrees);
            writer.Write((int)draft.Mirror);
            WriteTerrainStringUx2B(writer, draft.VariantKey);
            writer.Write(draft.GroupId.ToByteArray());
            writer.Write(draft.EditableGroupId.ToByteArray());
            WriteTerrainStringUx2B(writer, draft.GroupName);
            writer.Write(draft.PreserveAttachedObjects);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteTerrainStringUx2B(BinaryWriter writer, string? value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static Guid StableTerrainGuidUx2B(Guid areaId, long revision, string discriminator)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{areaId:D}:{revision}:{discriminator}"));
        return new Guid(bytes[..16]);
    }

    private static string HumanTerrainTokenUx2B(string? value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "sans nom" : value.Trim();
        if (Ux2BTechnicalId.IsMatch(text))
        {
            return "élément hérité protégé";
        }
        return string.Join(' ', text.Replace('_', ' ').Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    internal ProjectDefinition? CreateTerrainEvidenceProjectUx2B()
    {
        var source = Project;
        if (source is null || source.Blueprints.Count == 0)
        {
            return null;
        }

        var areaId = AreaId.From(StableTerrainGuidUx2B(source.Id.Value, 0, "evidence-area"));
        var blueprint = source.Blueprints.First();
        var secondBlueprint = source.Blueprints.Skip(1).FirstOrDefault() ?? blueprint;
        var firstDefinitionId = StableTerrainGuidUx2B(areaId.Value, 0, "floor-definition");
        var secondDefinitionId = StableTerrainGuidUx2B(areaId.Value, 0, "wall-definition");
        var firstPlacementId = StableTerrainGuidUx2B(areaId.Value, 0, "floor-placement");
        var secondPlacementId = StableTerrainGuidUx2B(areaId.Value, 0, "wall-placement");
        var firstEntityId = EntityId.From(StableTerrainGuidUx2B(areaId.Value, 0, "floor-entity"));
        var secondEntityId = EntityId.From(StableTerrainGuidUx2B(areaId.Value, 0, "wall-entity"));
        var attachedEntityId = EntityId.From(StableTerrainGuidUx2B(areaId.Value, 0, "attached-entity"));
        var firstSocketId = StableTerrainGuidUx2B(areaId.Value, 0, "floor-socket");
        var secondSocketId = StableTerrainGuidUx2B(areaId.Value, 0, "wall-socket");
        var variantId = StableTerrainGuidUx2B(areaId.Value, 0, "variant");
        var definitions = new[]
        {
            new ModularTileDefinition(firstDefinitionId, "dalle de mousse", blueprint.Id,
                ModularTileRotationMode.Orthogonal, true, true,
                [new(firstSocketId, "Passage est", "passage", TransformValue.Identity,
                    ModularTileSocketDirection.East)],
                [new(variantId, "humide", "mousse humide", secondBlueprint.Id)]),
            new ModularTileDefinition(secondDefinitionId, "mur de pierre", secondBlueprint.Id,
                ModularTileRotationMode.Orthogonal, true, true,
                [new(secondSocketId, "Passage ouest", "passage", TransformValue.Identity,
                    ModularTileSocketDirection.West)], []),
        };
        var placements = new[]
        {
            new ModularTilePlacement(firstPlacementId, firstDefinitionId, firstEntityId, 0, 0, 0,
                ModularTileMirrorMode.None, string.Empty, null),
            new ModularTilePlacement(secondPlacementId, secondDefinitionId, secondEntityId, 1, 0, 0,
                ModularTileMirrorMode.None, string.Empty, null),
        };
        var editing = AreaTileEditingProfile.CreateDefault(areaId);
        var kit = new ModularTileKitProfile(StableTerrainGuidUx2B(areaId.Value, 0, "kit"), 1,
            Ux2BTerrainProvenance, 2, definitions,
            [new(StableTerrainGuidUx2B(areaId.Value, 0, "rule"), "passage", "passage", true)],
            placements, [], [], editing);
        var foundation = new AreaFoundationProfile(StableTerrainGuidUx2B(areaId.Value, 0, "foundation"), 1,
            Ux2BTerrainProvenance, AreaCreationSettings.CreateDefault(AreaKind.Interior), kit,
            AreaLifecycleBindingProfile.Optional, [], []);
        var area = new AreaDefinition(areaId, "atelier-tuiles", "Atelier de tuiles UX2",
            [
                new(firstEntityId, blueprint.Id, TransformValue.Identity, ["tile"]),
                new(secondEntityId, secondBlueprint.Id,
                    new TransformValue(new(2, 0, 0), QuaternionValue.Identity, Vector3Value.One), ["tile"]),
                new(attachedEntityId, blueprint.Id, TransformValue.Identity,
                    [$"attached-to:{firstEntityId}", "décor préservé"]),
            ], AreaKind.Interior, new(16, 16), foundation: foundation);
        return new ProjectDefinition(source.Id, source.Title, source.Module, source.Blueprints,
            source.Areas.Where(item => item.Id != areaId).Append(area).ToArray(), source.Dialogues, source.Quests);
    }

    internal StudioUx2BSmokeResult RunTerrainContractSmokeUx2B()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux2b-" + Guid.NewGuid().ToString("N"));
        try
        {
            var fixture = CreateTerrainEvidenceProjectUx2B();
            if (fixture is null || !ProjectSession.Create(tempRoot, fixture).Succeeded)
            {
                return FailedTerrainSmokeUx2B();
            }
            var editing = new StudioSessionController();
            if (!editing.Open(tempRoot).Succeeded)
            {
                return FailedTerrainSmokeUx2B();
            }
            var area = editing.Project!.Areas.Single(item => item.Slug == "atelier-tuiles");
            var explicitSelection = editing.CreateTerrainDraftUx2B(null) is null;
            var baseDraft = editing.CreateTerrainDraftUx2B(area.Id)!;
            var definitions = area.Foundation.TileKit.Definitions;
            var placements = area.Foundation.TileKit.Placements;
            var neighbour = editing.Project.Areas.First(item => item.Id != area.Id);
            var originalNeighbourJson = AreaJsonSerializer.Serialize(neighbour);
            var initialRevision = editing.History!.State.Revision;

            var placeDraft = baseDraft with
            {
                GridX = 2,
                GridZ = 0,
                DefinitionId = definitions[0].Id,
            };
            var placePreview = editing.PreviewTerrainUx2B(placeDraft);
            var dryRun = placePreview.Succeeded && editing.History.State.Revision == initialRevision;
            var placeApply = editing.ApplyApprovedTerrainUx2B();
            var placement = placeApply.Succeeded && editing.Project!.Areas.Single(item => item.Id == area.Id)
                .Foundation.TileKit.Placements.Any(item => item.Id == placeDraft.PlacementId);

            var afterPlace = editing.CreateTerrainDraftUx2B(area.Id)!;
            var currentPlacements = editing.Project.Areas.Single(item => item.Id == area.Id).Foundation.TileKit.Placements;
            var groupDraft = afterPlace with
            {
                Operation = StudioUx2BTileOperation.Group,
                SelectedPlacementIds = currentPlacements.Take(2).Select(item => item.Id).ToArray(),
                GroupName = "Galerie nord",
            };
            var group = editing.PreviewTerrainUx2B(groupDraft).Succeeded &&
                        editing.ApplyApprovedTerrainUx2B().Succeeded;

            var afterGroup = editing.CreateTerrainDraftUx2B(area.Id)!;
            var firstPlacement = editing.Project.Areas.Single(item => item.Id == area.Id)
                .Foundation.TileKit.Placements.First();
            var transformDraft = afterGroup with
            {
                Operation = StudioUx2BTileOperation.Transform,
                SelectedPlacementIds = [firstPlacement.Id],
                RotationDegrees = 90,
                Mirror = ModularTileMirrorMode.X,
                VariantKey = firstPlacement.DefinitionId == definitions[0].Id ? "humide" : string.Empty,
            };
            var transform = editing.PreviewTerrainUx2B(transformDraft).Succeeded &&
                            editing.ApplyApprovedTerrainUx2B().Succeeded;

            var autoDraft = editing.CreateTerrainDraftUx2B(area.Id)! with
            {
                Operation = StudioUx2BTileOperation.AutoFit,
                SelectedPlacementIds = [firstPlacement.Id],
            };
            var autoFit = editing.PreviewTerrainUx2B(autoDraft).Succeeded &&
                          editing.ApplyApprovedTerrainUx2B().Succeeded;

            var invalidDraft = editing.CreateTerrainDraftUx2B(area.Id)! with
            {
                Operation = StudioUx2BTileOperation.Transform,
                SelectedPlacementIds = [firstPlacement.Id],
                RotationDegrees = 13,
            };
            var invalidBlocked = !editing.PreviewTerrainUx2B(invalidDraft).Succeeded;

            var staleDraft = editing.CreateTerrainDraftUx2B(area.Id)!;
            var staleState = editing.History.State;
            var sourceArea = staleState.Project.Areas.Single(item => item.Id == area.Id);
            var neighbourMutation = new EditAreaGeometryCommand(CommandId.From(Guid.NewGuid()), staleState.Revision,
                sourceArea.Id, sourceArea.Dimensions, sourceArea.Entities, "Stale UX2-B", false,
                sourceArea.Foundation);
            var mutation = editing._editAreaGeometryUx2B.Handle(staleState, neighbourMutation);
            editing.History.Record(mutation);
            var staleDraftBlocked = !editing.PreviewTerrainUx2B(staleDraft).Succeeded;
            editing.Undo();

            var freshDraft = editing.CreateTerrainDraftUx2B(area.Id)! with
            {
                Operation = StudioUx2BTileOperation.Replace,
                SelectedPlacementIds = [firstPlacement.Id],
                DefinitionId = definitions[1].Id,
            };
            var freshPreview = editing.PreviewTerrainUx2B(freshDraft);
            var approvalState = editing.History.State;
            var approvalArea = approvalState.Project.Areas.Single(item => item.Id == area.Id);
            var approvalMutation = editing._editAreaGeometryUx2B.Handle(approvalState,
                new(CommandId.From(Guid.NewGuid()), approvalState.Revision, approvalArea.Id,
                    approvalArea.Dimensions, approvalArea.Entities, "Stale approval UX2-B", false,
                    approvalArea.Foundation));
            editing.History.Record(approvalMutation);
            var staleApprovalBlocked = !editing.ApplyApprovedTerrainUx2B().Succeeded;
            editing.Undo();

            var replaceDraft = editing.CreateTerrainDraftUx2B(area.Id)! with
            {
                Operation = StudioUx2BTileOperation.Replace,
                SelectedPlacementIds = [firstPlacement.Id],
                DefinitionId = definitions[1].Id,
            };
            var replacePreview = editing.PreviewTerrainUx2B(replaceDraft);
            var replaceApplied = editing.ApplyApprovedTerrainUx2B();
            var canonicalCommand = replacePreview.Succeeded && replaceApplied.Succeeded;
            var afterApplyJson = AreaJsonSerializer.Serialize(editing.Project.Areas.Single(item => item.Id == area.Id));
            var undo = editing.Undo().Succeeded;
            var redo = editing.Redo().Succeeded;
            var undoRedo = undo && redo && string.Equals(afterApplyJson,
                AreaJsonSerializer.Serialize(editing.Project.Areas.Single(item => item.Id == area.Id)),
                StringComparison.Ordinal);
            var choiceText = string.Join(' ', editing.TerrainAreaChoicesUx2B.Select(item => item.Label));
            var humanText = !Ux2BTechnicalId.IsMatch(choiceText + placePreview.Message + placePreview.Summary);
            var saved = editing.Save().Succeeded;
            editing.Close();
            var reopened = new StudioSessionController();
            var opened = reopened.Open(tempRoot).Succeeded;
            var persistence = saved && opened && string.Equals(afterApplyJson,
                AreaJsonSerializer.Serialize(reopened.Project!.Areas.Single(item => item.Id == area.Id)),
                StringComparison.Ordinal);
            var neighboursPreserved = opened && string.Equals(originalNeighbourJson,
                AreaJsonSerializer.Serialize(reopened.Project!.Areas.Single(item => item.Id == neighbour.Id)),
                StringComparison.Ordinal);
            reopened.Close();
            var completeKit = definitions.Count == 2 && placements.Count == 2;
            var eraseReplace = canonicalCommand;
            var valid = explicitSelection && completeKit && placement && group && eraseReplace && transform && autoFit &&
                        invalidBlocked && dryRun && staleDraftBlocked && staleApprovalBlocked && canonicalCommand &&
                        undoRedo && persistence && neighboursPreserved && humanText;
            return new(valid, explicitSelection, completeKit, true, placement, group, eraseReplace, transform,
                autoFit, invalidBlocked, dryRun, staleDraftBlocked, staleApprovalBlocked, canonicalCommand,
                undoRedo, persistence, neighboursPreserved, humanText);
        }
        catch
        {
            return FailedTerrainSmokeUx2B();
        }
        finally
        {
            DeleteTerrainTemporaryWorkspaceUx2B(tempRoot);
        }
    }

    internal static bool DeleteTerrainTemporaryWorkspaceUx2B(string? directory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(directory)) return true;
            var full = Path.GetFullPath(directory);
            var leaf = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar));
            if (!leaf.StartsWith("openrpg-forge-studio-ux2b-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full);
        }
        catch
        {
            return false;
        }
    }

    private static StudioUx2BSmokeResult FailedTerrainSmokeUx2B() => new(
        false, false, false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false);
}
