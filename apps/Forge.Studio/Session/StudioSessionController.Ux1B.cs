using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Projects;
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
    private readonly EditAreaGeometryHandler _editAreaGeometry = new(new SystemClock());
    private readonly DuplicateAreaHandler _duplicateArea = new(new SystemClock());
    private StudioAreaResizeDraft? _approvedAreaResize;
    private StudioAreaCloneDraft? _approvedAreaClone;

    internal StudioAreaResizeDraft CreateAreaResizeDraft()
    {
        var area = ActiveArea ?? throw new InvalidOperationException("Aucune zone n’est active.");
        return new StudioAreaResizeDraft(
            area.Id,
            area.Dimensions.Width,
            area.Dimensions.Depth,
            AreaResizeAnchor.Center,
            StudioAreaResizeLossPolicy.RepositionInside);
    }

    internal StudioAreaCloneDraft CreateAreaCloneDraft()
    {
        var area = ActiveArea ?? throw new InvalidOperationException("Aucune zone n’est active.");
        return new StudioAreaCloneDraft(
            area.Id,
            AreaId.From(Guid.NewGuid()),
            NextAreaCopyName(area.DisplayName),
            AreaCloneMode.Full,
            ExternalPortalPolicy.Preserve,
            area.Entities.Select(item => new EntityIdRemap(item.Id, EntityId.From(Guid.NewGuid()))).ToArray(),
            area.Portals.Select(item => new PortalIdRemap(item.Id, PortalId.From(Guid.NewGuid()))).ToArray());
    }

    internal StudioAreaResizePreview PreviewAreaResize(StudioAreaResizeDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        _approvedAreaResize = null;
        if (!TryBuildAreaResize(draft, dryRun: true, out var command, out var plan, out var atRisk, out var message))
        {
            return new StudioAreaResizePreview(false, message, string.Empty, false);
        }

        var dryRun = _editAreaGeometry.Handle(History!.State, command!);
        if (!dryRun.Succeeded)
        {
            var diagnostic = dryRun.Diagnostics.FirstOrDefault();
            var human = HumanizeAreaResize(diagnostic?.Code);
            AddOperationalDiagnostic(
                diagnostic?.Code ?? "area.resize_refused",
                human,
                target: null,
                documentPath: "zones",
                Forge.Application.Creator.CreatorDiagnosticSeverity.Error,
                "area:resize");
            return new StudioAreaResizePreview(false, human, string.Empty, false);
        }

        var area = Project!.Areas.Single(item => item.Id == draft.SourceAreaId);
        _approvedAreaResize = draft;
        return new StudioAreaResizePreview(
            true,
            "Aperçu validé · aucune zone ni aucun fichier modifié.",
            BuildAreaResizeSummary(area, plan!, atRisk),
            true,
            plan!.Kept.Count,
            plan.Removed.Count,
            plan.Repositioned?.Count ?? 0,
            atRisk.Select(id => DescribeAreaEntity(area, id)).ToArray());
    }

    internal StudioAreaDocumentOperationResult ApplyApprovedAreaResize()
    {
        if (_approvedAreaResize is null || History is null)
        {
            return new StudioAreaDocumentOperationResult(false, "Prévisualisez d’abord le redimensionnement.");
        }

        var draft = _approvedAreaResize;
        if (!TryBuildAreaResize(draft, dryRun: false, out var command, out var plan, out _, out var message))
        {
            _approvedAreaResize = null;
            return new StudioAreaDocumentOperationResult(false, message);
        }

        var dryRun = _editAreaGeometry.Handle(History.State, command! with { DryRun = true });
        if (!dryRun.Succeeded)
        {
            _approvedAreaResize = null;
            return new StudioAreaDocumentOperationResult(
                false,
                HumanizeAreaResize(dryRun.Diagnostics.FirstOrDefault()?.Code));
        }

        var applied = _editAreaGeometry.Handle(History.State, command!);
        if (!applied.Succeeded)
        {
            _approvedAreaResize = null;
            return new StudioAreaDocumentOperationResult(
                false,
                HumanizeAreaResize(applied.Diagnostics.FirstOrDefault()?.Code));
        }

        History.Record(applied);
        InvalidateContentIndexes();
        var surviving = plan!.Kept.Select(item => item.Id).ToHashSet();
        var selected = SelectedEntityIds.Where(surviving.Contains).ToArray();
        SelectEntities(selected, selected.LastOrDefault());
        _approvedAreaResize = null;
        return new StudioAreaDocumentOperationResult(
            true,
            $"Zone redimensionnée en {draft.Width} × {draft.Depth} · " +
            $"{plan.Repositioned?.Count ?? 0} objet(s) replacé(s) · {plan.Removed.Count} retiré(s) · Ctrl+Z pour annuler",
            draft.SourceAreaId);
    }

    internal StudioAreaClonePreview PreviewAreaClone(StudioAreaCloneDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        _approvedAreaClone = null;
        if (!TryBuildAreaClone(draft, dryRun: true, out var command, out var source, out var message))
        {
            return new StudioAreaClonePreview(false, message, string.Empty, false);
        }

        var dryRun = _duplicateArea.Handle(History!.State, command!);
        if (!dryRun.Succeeded)
        {
            var diagnostic = dryRun.Diagnostics.FirstOrDefault();
            var human = HumanizeAreaClone(diagnostic?.Code);
            AddOperationalDiagnostic(
                diagnostic?.Code ?? "area.clone_refused",
                human,
                target: null,
                documentPath: "zones",
                Forge.Application.Creator.CreatorDiagnosticSeverity.Error,
                "area:clone");
            return new StudioAreaClonePreview(false, human, string.Empty, false);
        }

        var candidateResult = _duplicateArea.Handle(History.State, command! with { DryRun = false });
        var candidate = candidateResult.Succeeded
            ? candidateResult.State.Project.Areas.FirstOrDefault(item => item.Id == draft.DestinationAreaId)
            : null;
        if (candidate is null)
        {
            return new StudioAreaClonePreview(
                false,
                "L’aperçu de la copie n’a pas pu être construit.",
                string.Empty,
                false);
        }

        var preview = AreaDuplicationPreview.Create(source!, draft.Mode, draft.ExternalPortalPolicy);
        _approvedAreaClone = draft;
        return new StudioAreaClonePreview(
            true,
            "Aperçu validé · la copie n’existe pas encore dans le projet.",
            BuildAreaCloneSummary(preview),
            true,
            candidate);
    }

    internal StudioAreaDocumentOperationResult ApplyApprovedAreaClone()
    {
        if (_approvedAreaClone is null || History is null)
        {
            return new StudioAreaDocumentOperationResult(false, "Prévisualisez d’abord la copie de zone.");
        }

        var draft = _approvedAreaClone;
        if (!TryBuildAreaClone(draft, dryRun: false, out var command, out _, out var message))
        {
            _approvedAreaClone = null;
            return new StudioAreaDocumentOperationResult(false, message);
        }

        var dryRun = _duplicateArea.Handle(History.State, command! with { DryRun = true });
        if (!dryRun.Succeeded)
        {
            _approvedAreaClone = null;
            return new StudioAreaDocumentOperationResult(
                false,
                HumanizeAreaClone(dryRun.Diagnostics.FirstOrDefault()?.Code));
        }

        var previousAreaId = ActiveAreaId;
        var applied = _duplicateArea.Handle(History.State, command!);
        if (!applied.Succeeded)
        {
            _approvedAreaClone = null;
            return new StudioAreaDocumentOperationResult(
                false,
                HumanizeAreaClone(applied.Diagnostics.FirstOrDefault()?.Code));
        }

        History.Record(applied);
        InvalidateContentIndexes();
        var opened = OpenAreaTab(draft.DestinationAreaId, activate: true, persist: false);
        if (!opened.Succeeded)
        {
            History.Undo();
            ReconcileWorkspaceAreasAfterProjectMutation(previousAreaId);
            InvalidateContentIndexes();
            _approvedAreaClone = null;
            return new StudioAreaDocumentOperationResult(false, "La copie n’a pas pu être ouverte dans le Studio.");
        }

        _approvedAreaClone = null;
        return new StudioAreaDocumentOperationResult(
            true,
            $"Zone « {draft.DisplayName.Trim()} » copiée · nouvelles identités internes · Ctrl+Z pour annuler",
            draft.DestinationAreaId);
    }

    internal void CancelAreaDocumentOperation()
    {
        _approvedAreaResize = null;
        _approvedAreaClone = null;
    }

    internal StudioUx1BSmokeResult RunUx1BContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux1b-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var blueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1b10000-0000-4000-8000-000000000001")),
                "borne-de-pierre",
                "Borne de pierre",
                BlueprintKind.Placeable);
            var sourceId = AreaId.From(Guid.Parse("c1b10000-0000-4000-8000-000000000002"));
            var targetId = AreaId.From(Guid.Parse("c1b10000-0000-4000-8000-000000000003"));
            var first = new EntityInstance(
                EntityId.From(Guid.Parse("c1b10000-0000-4000-8000-000000000004")),
                blueprint.Id,
                new TransformValue(new(-2, 0, -2), QuaternionValue.Identity, Vector3Value.One));
            var second = new EntityInstance(
                EntityId.From(Guid.Parse("c1b10000-0000-4000-8000-000000000005")),
                blueprint.Id,
                new TransformValue(new(2, 0, -2), QuaternionValue.Identity, Vector3Value.One));
            var edge = new EntityInstance(
                EntityId.From(Guid.Parse("c1b10000-0000-4000-8000-000000000006")),
                blueprint.Id,
                new TransformValue(new(18, 0, 0), QuaternionValue.Identity, Vector3Value.One));
            var targetAnchor = new EntityInstance(
                EntityId.From(Guid.Parse("c1b10000-0000-4000-8000-000000000007")),
                blueprint.Id,
                TransformValue.Identity);
            var source = new AreaDefinition(
                sourceId,
                "atelier-source",
                "Atelier source",
                [first, second, edge],
                AreaKind.Exterior,
                new AreaDimensions(40, 40),
                [
                    new PortalLink(
                        PortalId.From(Guid.Parse("c1b10000-0000-4000-8000-000000000008")),
                        "Passage intérieur",
                        first.Id,
                        sourceId,
                        second.Id,
                        true,
                        false),
                    new PortalLink(
                        PortalId.From(Guid.Parse("c1b10000-0000-4000-8000-000000000009")),
                        "Vers la réserve",
                        second.Id,
                        targetId,
                        targetAnchor.Id,
                        false,
                        true),
                ]);
            var target = new AreaDefinition(
                targetId,
                "reserve",
                "Réserve",
                [targetAnchor],
                AreaKind.Interior,
                new AreaDimensions(24, 24));
            var seedProject = new ProjectDefinition(
                ProjectId.From(Guid.Parse("c1b10000-0000-4000-8000-000000000010")),
                "Studio UX1-B",
                [blueprint],
                [source, target]);
            var created = ProjectSession.Create(tempRoot, seedProject);
            var opened = created.Succeeded && editing.Open(tempRoot).Succeeded;
            if (!opened)
            {
                return FailedUx1BSmoke();
            }

            var originalProject = editing.Project!;
            var originalSource = originalProject.Areas.Single(item => item.Id == sourceId);
            var originalTarget = originalProject.Areas.Single(item => item.Id == targetId);
            var originalAreaDocumentCount = Directory.GetFiles(Path.Combine(tempRoot, "areas"), "*.json").Length;
            var resizeDraft = editing.CreateAreaResizeDraft() with
            {
                Width = 20,
                Depth = 20,
                Anchor = AreaResizeAnchor.Center,
            };
            var removal = editing.PreviewAreaResize(resizeDraft with
            {
                LossPolicy = StudioAreaResizeLossPolicy.Remove,
            });
            var removalPreview = removal.Succeeded && removal.RemovedCount == 1 &&
                                 removal.RepositionedCount == 0 && removal.AtRiskObjects?.Count == 1;
            var preview = editing.PreviewAreaResize(resizeDraft);
            var resizePreview = preview.Succeeded && preview.CanApply && preview.AtRiskObjects?.Count == 1;
            var repositionLosses = preview.RemovedCount == 0 && preview.RepositionedCount == 1 &&
                                   preview.KeptCount == originalSource.Entities.Count;
            var resizeDryRun = resizePreview && ReferenceEquals(originalProject, editing.Project) &&
                               editing.Project.Areas.Single(item => item.Id == sourceId).Dimensions ==
                               originalSource.Dimensions;
            var invalidResize = editing.PreviewAreaResize(resizeDraft with { Width = 3 });
            preview = editing.PreviewAreaResize(resizeDraft);
            var resizeApplied = editing.ApplyApprovedAreaResize();
            var resizedProject = editing.Project!;
            var resizedSource = resizedProject.Areas.Single(item => item.Id == sourceId);
            var resizedEdge = resizedSource.Entities.Single(item => item.Id == edge.Id);
            var resizePreserved = resizedSource.Dimensions == new AreaDimensions(20, 20) &&
                                  resizedSource.Entities.Count == originalSource.Entities.Count &&
                                  MathF.Abs(resizedEdge.Transform.Position.X) <= 9.5f &&
                                  ReferenceEquals(resizedSource.Environment, originalSource.Environment) &&
                                  resizedSource.Authoring.Map.Id == originalSource.Authoring.Map.Id &&
                                  resizedSource.Authoring.Experience.Id == originalSource.Authoring.Experience.Id &&
                                  resizedSource.Authoring.UserEvents.Count == originalSource.Authoring.UserEvents.Count &&
                                  resizedSource.Authoring.Layers.Count == originalSource.Authoring.Layers.Count &&
                                  ReferenceEquals(resizedSource.Foundation, originalSource.Foundation);
            var neighborPreserved = ReferenceEquals(
                originalTarget,
                resizedProject.Areas.Single(item => item.Id == targetId));
            var resizeUndo = editing.Undo();
            var resizeUndone = ReferenceEquals(originalProject, editing.Project);
            var resizeRedo = editing.Redo();
            var resizeRedone = ReferenceEquals(resizedProject, editing.Project);
            var resizeUndoRedo = resizeApplied.Succeeded && resizeUndo.Succeeded && resizeUndone &&
                                 resizeRedo.Succeeded && resizeRedone && resizePreserved;

            editing.ActivateAreaTab(sourceId, persist: false);
            var cloneDraft = editing.CreateAreaCloneDraft() with
            {
                DestinationAreaId = AreaId.From(Guid.Parse("c1b10000-0000-4000-8000-000000000020")),
                DisplayName = "Atelier source — Copie",
                Mode = AreaCloneMode.Full,
                ExternalPortalPolicy = ExternalPortalPolicy.Preserve,
            };
            var template = editing.PreviewAreaClone(cloneDraft with
            {
                DestinationAreaId = AreaId.From(Guid.Parse("c1b10000-0000-4000-8000-000000000021")),
                DisplayName = "Atelier source — Gabarit",
                Mode = AreaCloneMode.Template,
            });
            var templateClone = template.Succeeded && template.Candidate is
            {
                Entities.Count: 0,
                Portals.Count: 0,
            };
            var invalidClone = editing.PreviewAreaClone(cloneDraft with
            {
                DestinationAreaId = AreaId.From(Guid.Parse("c1b10000-0000-4000-8000-000000000022")),
                DisplayName = target.DisplayName,
            });
            var invalidBlocked = !invalidResize.Succeeded && !invalidClone.Succeeded;
            var humanDiagnostics = invalidBlocked && IsHumanAreaOperationMessage(invalidResize.Message) &&
                                   IsHumanAreaOperationMessage(invalidClone.Message);

            var clonePreviewResult = editing.PreviewAreaClone(cloneDraft);
            var clonePreview = clonePreviewResult.Succeeded && clonePreviewResult.CanApply &&
                               ReferenceEquals(resizedProject, editing.Project);
            var cloneApplied = editing.ApplyApprovedAreaClone();
            var clonedProject = editing.Project!;
            var clone = clonedProject.Areas.Single(item => item.Id == cloneDraft.DestinationAreaId);
            var clonedEntityIds = clone.Entities.Select(item => item.Id).ToHashSet();
            var sourceEntityIds = resizedSource.Entities.Select(item => item.Id).ToHashSet();
            var internalPortal = clone.Portals.Single(item => item.DestinationAreaId == clone.Id);
            var externalPortal = clone.Portals.Single(item => item.DestinationAreaId == targetId);
            var portalRemap = clone.Portals.Count == resizedSource.Portals.Count &&
                              clonedEntityIds.Contains(internalPortal.SourceEntityId) &&
                              clonedEntityIds.Contains(internalPortal.DestinationEntityId) &&
                              externalPortal.DestinationEntityId == targetAnchor.Id;
            var newIdentities = !clonedEntityIds.Overlaps(sourceEntityIds) &&
                                !clone.Portals.Select(item => item.Id)
                                    .Intersect(resizedSource.Portals.Select(item => item.Id)).Any() &&
                                clone.Navigation.Id != resizedSource.Navigation.Id &&
                                clone.Environment.Authoring.Id != resizedSource.Environment.Authoring.Id &&
                                clone.World.Id != resizedSource.World.Id &&
                                clone.World.Terrain.Id != resizedSource.World.Terrain.Id;
            var fullClone = cloneApplied.Succeeded && clone.Entities.Count == resizedSource.Entities.Count &&
                            clone.Dimensions == resizedSource.Dimensions &&
                            clone.Entities.Select(item => item.BlueprintId)
                                .SequenceEqual(resizedSource.Entities.Select(item => item.BlueprintId));
            neighborPreserved = neighborPreserved &&
                                 ReferenceEquals(resizedSource, clonedProject.Areas.Single(item => item.Id == sourceId)) &&
                                 ReferenceEquals(originalTarget, clonedProject.Areas.Single(item => item.Id == targetId));
            var cloneUndo = editing.Undo();
            var cloneUndone = ReferenceEquals(resizedProject, editing.Project);
            var cloneRedo = editing.Redo();
            var cloneRedone = ReferenceEquals(clonedProject, editing.Project);
            var cloneUndoRedo = cloneApplied.Succeeded && cloneUndo.Succeeded && cloneUndone &&
                                cloneRedo.Succeeded && cloneRedone;

            var save = editing.Save();
            var cloneFiles = Directory.GetFiles(
                Path.Combine(tempRoot, "areas"),
                $"{cloneDraft.DestinationAreaId}-*.json");
            var independentDocument = save.Succeeded && originalAreaDocumentCount == 2 && cloneFiles.Length == 1;
            editing.Close();
            var reopen = reopened.Open(tempRoot);
            var restoredSource = reopened.Project?.Areas.SingleOrDefault(item => item.Id == sourceId);
            var restoredClone = reopened.Project?.Areas.SingleOrDefault(item => item.Id == cloneDraft.DestinationAreaId);
            var resizePersistence = reopen.Succeeded && restoredSource?.Dimensions == new AreaDimensions(20, 20) &&
                                    restoredSource.Entities.Single(item => item.Id == edge.Id).Transform ==
                                    resizedEdge.Transform;
            var clonePersistence = restoredClone is not null && restoredClone.DisplayName == clone.DisplayName &&
                                   restoredClone.Entities.Select(item => item.Id).SequenceEqual(clone.Entities.Select(item => item.Id)) &&
                                   restoredClone.Portals.Select(item => item.Id).SequenceEqual(clone.Portals.Select(item => item.Id)) &&
                                   restoredClone.Navigation.Id == clone.Navigation.Id &&
                                   restoredClone.Environment.Authoring.Id == clone.Environment.Authoring.Id &&
                                   restoredClone.World.Id == clone.World.Id;

            var succeeded = resizePreview && repositionLosses && removalPreview && resizeDryRun && resizeUndoRedo &&
                            resizePersistence && neighborPreserved && clonePreview && fullClone && templateClone &&
                            portalRemap && newIdentities && cloneUndoRedo && independentDocument && clonePersistence &&
                            invalidBlocked && humanDiagnostics;
            return new StudioUx1BSmokeResult(
                succeeded,
                resizePreview,
                repositionLosses,
                removalPreview,
                resizeDryRun,
                resizeUndoRedo,
                resizePersistence,
                neighborPreserved,
                clonePreview,
                fullClone,
                templateClone,
                portalRemap,
                newIdentities,
                cloneUndoRedo,
                independentDocument,
                clonePersistence,
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

    private bool TryBuildAreaResize(
        StudioAreaResizeDraft draft,
        bool dryRun,
        out EditAreaGeometryCommand? command,
        out AreaResizePreview? plan,
        out IReadOnlyList<EntityId> atRisk,
        out string message)
    {
        command = null;
        plan = null;
        atRisk = [];
        message = string.Empty;
        if (History is null || Project is null)
        {
            message = "Aucun module n’est ouvert.";
            return false;
        }

        var area = Project.Areas.FirstOrDefault(item => item.Id == draft.SourceAreaId);
        if (area is null)
        {
            message = "La zone à redimensionner n’existe plus.";
            return false;
        }

        AreaDimensions dimensions;
        try
        {
            dimensions = new AreaDimensions(draft.Width, draft.Depth);
        }
        catch (ArgumentOutOfRangeException)
        {
            message = $"Largeur et profondeur doivent rester entre {AreaDimensions.MinimumSize} et {AreaDimensions.MaximumSize}.";
            return false;
        }

        var initial = AreaGeometryPlanner.Resize(area, dimensions, draft.Anchor);
        atRisk = initial.Removed;
        IReadOnlyDictionary<EntityId, Vector3Value>? repositioning = null;
        if (draft.LossPolicy == StudioAreaResizeLossPolicy.RepositionInside && initial.Removed.Count > 0)
        {
            repositioning = initial.Removed.ToDictionary(
                id => id,
                id => RepositionInside(area.Entities.Single(item => item.Id == id), initial.Translation, dimensions));
        }
        plan = AreaGeometryPlanner.Resize(area, dimensions, draft.Anchor, repositioning);
        command = new EditAreaGeometryCommand(
            CommandId.From(Guid.NewGuid()),
            History.State.Revision,
            area.Id,
            dimensions,
            plan.Kept,
            $"Resize area to {dimensions.Width}x{dimensions.Depth}; reposition {plan.Repositioned?.Count ?? 0}; remove {plan.Removed.Count}.",
            dryRun);
        return true;
    }

    private bool TryBuildAreaClone(
        StudioAreaCloneDraft draft,
        bool dryRun,
        out DuplicateAreaCommand? command,
        out AreaDefinition? source,
        out string message)
    {
        command = null;
        source = null;
        message = string.Empty;
        if (History is null || Project is null)
        {
            message = "Aucun module n’est ouvert.";
            return false;
        }
        source = Project.Areas.FirstOrDefault(item => item.Id == draft.SourceAreaId);
        if (source is null)
        {
            message = "La zone à copier n’existe plus.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(draft.DisplayName))
        {
            message = "Donnez un nom visible à la copie de zone.";
            return false;
        }

        command = new DuplicateAreaCommand(
            CommandId.From(Guid.NewGuid()),
            History.State.Revision,
            draft.SourceAreaId,
            draft.DestinationAreaId,
            draft.DisplayName.Trim(),
            draft.Mode,
            draft.ExternalPortalPolicy,
            draft.Mode == AreaCloneMode.Full ? draft.EntityIdMap : [],
            draft.Mode == AreaCloneMode.Full ? draft.PortalIdMap : [],
            dryRun);
        return true;
    }

    private Vector3Value RepositionInside(
        EntityInstance entity,
        Vector3Value translation,
        AreaDimensions dimensions)
    {
        var shiftedX = entity.Transform.Position.X + translation.X;
        var shiftedZ = entity.Transform.Position.Z + translation.Z;
        var xLimit = MathF.Max(0, dimensions.Width / 2f - MathF.Abs(entity.Transform.Scale.X) / 2f);
        var zLimit = MathF.Max(0, dimensions.Depth / 2f - MathF.Abs(entity.Transform.Scale.Z) / 2f);
        return new Vector3Value(
            Math.Clamp(shiftedX, -xLimit, xLimit),
            entity.Transform.Position.Y,
            Math.Clamp(shiftedZ, -zLimit, zLimit));
    }

    private string DescribeAreaEntity(AreaDefinition area, EntityId entityId)
    {
        var entity = area.Entities.First(item => item.Id == entityId);
        var blueprint = Project!.Blueprints.FirstOrDefault(item => item.Id == entity.BlueprintId);
        var ordinal = area.Entities.TakeWhile(item => item.Id != entityId)
            .Count(item => item.BlueprintId == entity.BlueprintId) + 1;
        return $"{blueprint?.DisplayName ?? "Objet"} {ordinal} · position {entity.Transform.Position.X:0.#}, {entity.Transform.Position.Z:0.#}";
    }

    private string NextAreaCopyName(string sourceName)
    {
        var stem = $"{sourceName.Trim()} — Copie";
        if (Project?.Areas.All(item => !string.Equals(item.DisplayName, stem, StringComparison.CurrentCultureIgnoreCase)) !=
            false)
        {
            return stem;
        }
        for (var index = 2; index < 10_000; index++)
        {
            var candidate = $"{stem} {index}";
            if (Project!.Areas.All(item =>
                    !string.Equals(item.DisplayName, candidate, StringComparison.CurrentCultureIgnoreCase)))
            {
                return candidate;
            }
        }
        return $"{stem} {DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
    }

    private static string BuildAreaResizeSummary(
        AreaDefinition source,
        AreaResizePreview preview,
        IReadOnlyList<EntityId> atRisk)
    {
        var movement = preview.Translation == Vector3Value.Zero
            ? "centre conservé"
            : $"contenu décalé de {preview.Translation.X:0.#} / {preview.Translation.Z:0.#}";
        var losses = atRisk.Count == 0
            ? "Aucun objet hors limites."
            : $"{atRisk.Count} objet(s) hors limites détecté(s) · " +
              $"{preview.Repositioned?.Count ?? 0} replacé(s) · {preview.Removed.Count} retiré(s).";
        return $"{source.Dimensions.Width} × {source.Dimensions.Depth} → " +
               $"{preview.Dimensions.Width} × {preview.Dimensions.Depth} · {movement}\n" +
               $"{preview.Kept.Count} objet(s) conservé(s). {losses}\n" +
               "Le terrain et les profils voisins restent rattachés au même document de zone.";
    }

    private static string BuildAreaCloneSummary(AreaDuplicationPreview preview)
    {
        var mode = preview.Mode == AreaCloneMode.Full ? "Copie complète" : "Gabarit vide";
        var portals = preview.ExternalPortalPolicy == ExternalPortalPolicy.Preserve
            ? "transitions externes conservées"
            : "transitions externes retirées";
        return $"{mode} · {preview.EntityCount} objet(s) · " +
               $"{preview.InternalPortalCount} transition(s) interne(s) · {preview.ExternalPortalCount} externe(s)\n" +
               $"Terrain, navigation et environnement copiés · {preview.SharedAssetReferences} référence(s) partagée(s)\n" +
               $"{portals} · nouvelles identités internes lors de l’application.";
    }

    private static string HumanizeAreaResize(string? code) => code switch
    {
        EditAreaGeometryHandler.RevisionConflictCode =>
            "Le module a changé depuis l’aperçu. Prévisualisez à nouveau.",
        EditAreaGeometryHandler.MissingAreaCode => "La zone à redimensionner n’existe plus.",
        EditAreaGeometryHandler.DuplicateEntityCode => "Deux objets possèdent la même identité interne.",
        EditAreaGeometryHandler.MissingBlueprintCode => "Un objet de la zone ne possède plus de modèle valide.",
        _ => "Le redimensionnement a été refusé. Vérifiez les objets hors limites et les transitions.",
    };

    private static string HumanizeAreaClone(string? code) => code switch
    {
        DuplicateAreaHandler.RevisionConflictCode =>
            "Le module a changé depuis l’aperçu. Prévisualisez à nouveau.",
        DuplicateAreaHandler.MissingSourceAreaCode => "La zone à copier n’existe plus.",
        DuplicateAreaHandler.NameRequiredCode => "Donnez un nom visible à la copie de zone.",
        DuplicateAreaHandler.DuplicateAreaCode => "Cette tentative de copie n’est plus actuelle. Rouvrez l’outil.",
        DuplicateAreaHandler.DuplicateSlugCode => "Une zone porte déjà ce nom. Choisissez un nom différent.",
        DuplicateAreaHandler.InvalidEntityMappingCode or DuplicateAreaHandler.InvalidPortalMappingCode =>
            "Le contenu de la zone a changé. Fermez puis rouvrez l’outil avant de copier.",
        _ => "La copie a été refusée. Consultez Messages et état pour la cible concernée.",
    };

    private static bool IsHumanAreaOperationMessage(string message) =>
        !message.Contains("area.", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains(".json", StringComparison.OrdinalIgnoreCase);

    private static StudioUx1BSmokeResult FailedUx1BSmoke() =>
        new(false, false, false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false);

    private void CloseUx1B()
    {
        _approvedAreaResize = null;
        _approvedAreaClone = null;
    }
}
