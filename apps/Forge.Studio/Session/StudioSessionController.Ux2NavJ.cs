using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.EngineAdapter.Rendering;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Core.Navigation;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2NavJProvenance = "studio:ux2-nav-j:visibility";
    private const string Ux2NavJEvidenceProvenance = "studio:ux2-nav-j:evidence";

    private readonly SetAreaAuthoringHandler _setAreaAuthoringUx2NavJ = new(new SystemClock());
    private StudioUx2NavJDraft? _approvedVisibilityUx2NavJ;
    private ProjectRevision? _approvedVisibilityRevisionUx2NavJ;
    private string? _approvedVisibilityFingerprintUx2NavJ;

    internal IReadOnlyList<StudioUx2NavJAreaChoice> VisibilityAreaChoicesUx2NavJ
    {
        get
        {
            var project = Project;
            if (project is null) return [];
            var duplicates = project.Areas.GroupBy(item => HumanUx2NavJ(item.DisplayName, "Zone"),
                    StringComparer.CurrentCultureIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(),
                    StringComparer.CurrentCultureIgnoreCase);
            return project.Areas.Select((area, index) =>
                {
                    var name = HumanUx2NavJ(area.DisplayName, "Zone");
                    var label = duplicates[name] > 1
                        ? $"{name} · {HumanUx2NavJ(area.Slug, $"zone {index + 1}")}" : name;
                    return new StudioUx2NavJAreaChoice(area.Id, label,
                        string.Equals(area.Authoring.Experience.Provenance,
                            Ux2NavJEvidenceProvenance, StringComparison.Ordinal));
                })
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.AreaId.Value)
                .ToArray();
        }
    }

    internal StudioUx2NavJDraft? CreateVisibilityDraftUx2NavJ(AreaId? areaId)
    {
        var history = History;
        var project = Project;
        var area = areaId is null ? null : project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (history is null || project is null || area is null) return null;
        var visibility = area.Authoring.Experience.Visibility;
        return new StudioUx2NavJDraft(
            area.Id,
            history.State.Revision,
            VisibilitySourceFingerprintUx2NavJ(project),
            HumanUx2NavJ(area.DisplayName, "Zone"),
            area.Authoring,
            visibility,
            visibility.OcclusionCulling,
            visibility.DistanceStreaming,
            visibility.CellVisualizer,
            visibility.CellSize,
            visibility.StreamingDistance,
            visibility.Rooms.Select(RoomDraftUx2NavJ).ToArray(),
            visibility.Portals.Select(PortalDraftUx2NavJ).ToArray(),
            visibility.Occluders.Select(OccluderDraftUx2NavJ).ToArray());
    }

    internal StudioUx2NavJRoomDraft CreateNewVisibilityRoomDraftUx2NavJ(StudioUx2NavJDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var extent = (float)Math.Clamp(draft.CellSize, 2, 64);
        return new(Guid.NewGuid(), true, null, false,
            $"Nouveau volume {draft.Rooms.Count + 1}",
            new TransformValue(Vector3Value.Zero, QuaternionValue.Identity,
                new Vector3Value(extent, Math.Max(2, extent * .5f), extent)),
            false, Math.Max(draft.StreamingDistance, draft.CellSize),
            new Dictionary<string, string>());
    }

    internal StudioUx2NavJPortalDraft? CreateNewVisibilityPortalDraftUx2NavJ(StudioUx2NavJDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Rooms.Count < 2) return null;
        var source = draft.Rooms[0];
        var destination = draft.Rooms[1];
        var midpoint = new Vector3Value(
            (source.Transform.Position.X + destination.Transform.Position.X) * .5f,
            (source.Transform.Position.Y + destination.Transform.Position.Y) * .5f,
            (source.Transform.Position.Z + destination.Transform.Position.Z) * .5f);
        return new(Guid.NewGuid(), true, null, false,
            $"Nouveau passage {draft.Portals.Count + 1}", source.Id, destination.Id,
            new TransformValue(midpoint, QuaternionValue.Identity, new Vector3Value(2, 3, .5f)),
            AreaVisibilityPortalMode.Open, false, string.Empty, true);
    }

    internal StudioUx2NavJOccluderDraft CreateNewOccluderDraftUx2NavJ(StudioUx2NavJDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return new(Guid.NewGuid(), true, null, false,
            $"Nouvel écran {draft.Occluders.Count + 1}",
            new TransformValue(Vector3Value.Zero, QuaternionValue.Identity, new Vector3Value(4, 3, .5f)),
            false, false, string.Empty, new Dictionary<string, string>());
    }

    private static StudioUx2NavJRoomDraft RoomDraftUx2NavJ(AreaVisibilityRoomProfile room) => new(
        room.Id, false, room, ContainsTechnicalIdentifierUx2NavJ(room.Name), room.Name,
        room.Transform, room.Exterior, room.StreamingDistance, room.Overrides);

    private static StudioUx2NavJPortalDraft PortalDraftUx2NavJ(AreaVisibilityPortalProfile portal) => new(
        portal.Id, false, portal, ContainsTechnicalIdentifierUx2NavJ(portal.Name), portal.Name,
        portal.SourceRoomId, portal.DestinationRoomId, portal.Transform, portal.Mode,
        ContainsTechnicalIdentifierUx2NavJ(portal.Condition), portal.Condition, portal.TwoWay);

    private static StudioUx2NavJOccluderDraft OccluderDraftUx2NavJ(AreaOccluderProfile item) => new(
        item.Id, false, item, ContainsTechnicalIdentifierUx2NavJ(item.Name), item.Name,
        item.Transform, item.Dynamic, ContainsTechnicalIdentifierUx2NavJ(item.MeshAssetId),
        item.MeshAssetId, item.Overrides);

    internal StudioUx2NavJPreview PreviewVisibilityUx2NavJ(StudioUx2NavJDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelVisibilityApprovalUx2NavJ();
        var history = History;
        var project = Project;
        if (history is null || project is null)
            return FailedVisibilityPreviewUx2NavJ("Aucun module n’est ouvert.");
        if (!TryBuildVisibilityMutationUx2NavJ(draft, true, out var command, out var sourceArea,
                out var candidateArea, out var diagnostics, out var message))
            return FailedVisibilityPreviewUx2NavJ(message, diagnostics);
        var dryRun = _setAreaAuthoringUx2NavJ.Handle(history.State, command!);
        if (!dryRun.Succeeded)
        {
            var commandDiagnostics = dryRun.Diagnostics.Select(item => new StudioUx2NavJDiagnostic(
                item.Code, $"zone/{draft.AreaDisplayName}/visibilite",
                HumanDiagnosticUx2NavJ(item.Message))).ToArray();
            return FailedVisibilityPreviewUx2NavJ(
                commandDiagnostics.FirstOrDefault()?.Message ?? "L’aperçu de visibilité a été refusé.",
                commandDiagnostics);
        }
        var candidate = candidateArea!;
        var candidateProject = ReplaceAreaUx2NavJ(project, candidate);
        StudioUx2NavJRuntimePreview runtime;
        StudioNavigationOverlaySnapshot overlay;
        try
        {
            runtime = BuildVisibilityRuntimePreviewUx2NavJ(candidateProject, candidate);
            overlay = BuildVisibilityOverlayUx2NavJ(candidate.Authoring.Experience.Visibility);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           OverflowException)
        {
            return FailedVisibilityPreviewUx2NavJ(HumanDiagnosticUx2NavJ(exception.Message),
            [
                new("studio.visibility.preview_invalid", "visibilite/apercu",
                    HumanDiagnosticUx2NavJ(exception.Message)),
            ]);
        }
        var diff = BuildVisibilityDiffUx2NavJ(sourceArea!.Authoring.Experience.Visibility,
            candidate.Authoring.Experience.Visibility);
        if (diff.Count > 0)
        {
            _approvedVisibilityUx2NavJ = draft;
            _approvedVisibilityRevisionUx2NavJ = history.State.Revision;
            _approvedVisibilityFingerprintUx2NavJ = VisibilityDraftFingerprintUx2NavJ(draft);
        }
        var visibility = candidate.Authoring.Experience.Visibility;
        return new StudioUx2NavJPreview(true,
            diff.Count == 0
                ? "Aperçu validé · aucune différence sémantique à appliquer."
                : "Aperçu sec validé · aucun nœud Godot, mesh, asset ou document n’a été modifié.",
            $"{visibility.Rooms.Count} volume(s) · {visibility.Portals.Count} passage(s) · " +
            $"{visibility.Occluders.Count} écran(s) · cellule {visibility.CellSize:0.##} m · " +
            $"streaming {visibility.StreamingDistance:0.##} m",
            diff.Count > 0, diff, [], overlay, runtime);
    }

    internal StudioUx2NavJActionResult ApplyApprovedVisibilityUx2NavJ()
    {
        var draft = _approvedVisibilityUx2NavJ;
        var history = History;
        if (draft is null || history is null ||
            _approvedVisibilityRevisionUx2NavJ != history.State.Revision ||
            !string.Equals(_approvedVisibilityFingerprintUx2NavJ,
                VisibilityDraftFingerprintUx2NavJ(draft), StringComparison.Ordinal))
        {
            CancelVisibilityApprovalUx2NavJ();
            return new(false, "Prévisualisez de nouveau la visibilité avant de l’appliquer.");
        }
        if (!TryBuildVisibilityMutationUx2NavJ(draft, false, out var command, out _, out _, out _,
                out var message))
        {
            CancelVisibilityApprovalUx2NavJ();
            return new(false, message);
        }
        var currentDryRun = _setAreaAuthoringUx2NavJ.Handle(history.State,
            command! with { DryRun = true });
        if (!currentDryRun.Succeeded)
        {
            CancelVisibilityApprovalUx2NavJ();
            return new(false, HumanDiagnosticUx2NavJ(currentDryRun.Diagnostics.FirstOrDefault()?.Message));
        }
        var applied = _setAreaAuthoringUx2NavJ.Handle(history.State, command!);
        CancelVisibilityApprovalUx2NavJ();
        if (!applied.Succeeded)
            return new(false, HumanDiagnosticUx2NavJ(applied.Diagnostics.FirstOrDefault()?.Message));
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, "Visibilité appliquée · Ctrl+Z pour annuler, Ctrl+Y pour rétablir");
    }

    internal void CancelVisibilityApprovalUx2NavJ()
    {
        _approvedVisibilityUx2NavJ = null;
        _approvedVisibilityRevisionUx2NavJ = null;
        _approvedVisibilityFingerprintUx2NavJ = null;
    }

    internal void CloseUx2NavJ() => CancelVisibilityApprovalUx2NavJ();

    internal StudioUx2NavJApprovalSnapshot CaptureVisibilityApprovalUx2NavJ() => new(
        _approvedVisibilityUx2NavJ, _approvedVisibilityRevisionUx2NavJ,
        _approvedVisibilityFingerprintUx2NavJ);

    internal void RestoreVisibilityApprovalUx2NavJ(StudioUx2NavJApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedVisibilityUx2NavJ = snapshot.Draft;
        _approvedVisibilityRevisionUx2NavJ = snapshot.Revision;
        _approvedVisibilityFingerprintUx2NavJ = snapshot.Fingerprint;
    }

    private bool TryBuildVisibilityMutationUx2NavJ(StudioUx2NavJDraft draft, bool dryRun,
        out SetAreaAuthoringCommand? command, out AreaDefinition? sourceArea,
        out AreaDefinition? candidateArea, out IReadOnlyList<StudioUx2NavJDiagnostic> diagnostics,
        out string message)
    {
        command = null;
        sourceArea = null;
        candidateArea = null;
        diagnostics = [];
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
            message = "Le projet a changé depuis l’ouverture de cette fiche. Rechargez la zone.";
            return false;
        }
        sourceArea = project.Areas.SingleOrDefault(item => item.Id == draft.AreaId);
        if (sourceArea is null)
        {
            message = "La zone choisie n’existe plus.";
            return false;
        }
        if (!string.Equals(draft.SourceFingerprint, VisibilitySourceFingerprintUx2NavJ(project),
                StringComparison.Ordinal))
        {
            message = "Le projet canonique a changé. Rechargez la fiche de visibilité.";
            return false;
        }
        if (!ReferenceEquals(draft.OriginalVisibility, sourceArea.Authoring.Experience.Visibility) &&
            !VisibilityEquivalentUx2NavJ(draft.OriginalVisibility,
                sourceArea.Authoring.Experience.Visibility))
        {
            message = "Le profil de visibilité source n’est plus celui qui a été sélectionné.";
            return false;
        }
        var inputDiagnostics = ValidateVisibilityDraftUx2NavJ(draft);
        if (inputDiagnostics.Count > 0)
        {
            diagnostics = inputDiagnostics;
            message = inputDiagnostics[0].Message;
            return false;
        }
        try
        {
            var current = sourceArea.Authoring.Experience.Visibility;
            if (!DraftIdentitiesMatchUx2NavJ(draft, current, out message)) return false;
            var rooms = draft.Rooms.Select(item => BuildRoomUx2NavJ(item,
                current.Rooms.SingleOrDefault(source => source.Id == item.Id))).ToArray();
            var portals = draft.Portals.Select(item => BuildPortalUx2NavJ(item,
                current.Portals.SingleOrDefault(source => source.Id == item.Id))).ToArray();
            var occluders = draft.Occluders.Select(item => BuildOccluderUx2NavJ(item,
                current.Occluders.SingleOrDefault(source => source.Id == item.Id))).ToArray();
            var visibility = new AreaVisibilityAuthoringProfile(rooms, portals, occluders,
                draft.OcclusionCulling, draft.DistanceStreaming, draft.CellVisualizer,
                (float)draft.CellSize, (float)draft.StreamingDistance);
            var sourceAuthoring = sourceArea.Authoring;
            var sourceExperience = sourceAuthoring.Experience;
            var experience = new AreaExperienceAuthoringProfile(sourceExperience.Id,
                sourceExperience.SchemaVersion, Ux2NavJProvenance, sourceExperience.AudioSnapshots,
                visibility, sourceExperience.Lod, sourceExperience.RequireRuntimeMetadata);
            var authoring = new AreaAuthoringProfile(sourceAuthoring.SchemaVersion,
                sourceAuthoring.UserEvents, sourceAuthoring.Map, sourceAuthoring.Layers, experience,
                sourceAuthoring.RequireLayerRuntimeMetadata);
            candidateArea = sourceArea.WithAuthoring(authoring);
            var domainDiagnostics = AreaExperienceAuthoringValidator.Validate(candidateArea)
                .Where(item => item.Code is AreaExperienceAuthoringValidator.VisibilityCode or
                    AreaExperienceAuthoringValidator.IdentityCode or AreaExperienceAuthoringValidator.SchemaCode)
                .Select(item => new StudioUx2NavJDiagnostic(item.Code,
                    $"zone/{draft.AreaDisplayName}/visibilite", HumanDiagnosticUx2NavJ(item.Message))).ToArray();
            if (domainDiagnostics.Length > 0)
            {
                diagnostics = domainDiagnostics;
                message = diagnostics[0].Message;
                return false;
            }
            command = new SetAreaAuthoringCommand(CommandId.From(Guid.NewGuid()),
                draft.BaseRevision, sourceArea.Id, authoring, dryRun);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           OverflowException)
        {
            message = HumanDiagnosticUx2NavJ(exception.Message);
            diagnostics = [new("studio.visibility.profile_invalid", "visibilite", message)];
            return false;
        }
    }

    private static bool DraftIdentitiesMatchUx2NavJ(StudioUx2NavJDraft draft,
        AreaVisibilityAuthoringProfile current, out string message)
    {
        message = string.Empty;
        bool Matches<T>(IReadOnlyList<T> source, IEnumerable<(Guid Id, bool IsNew)> items,
            Func<T, Guid> id)
        {
            var sourceIds = source.Select(id).ToHashSet();
            return items.All(item => item.IsNew ? !sourceIds.Contains(item.Id) : sourceIds.Contains(item.Id));
        }
        if (!Matches(current.Rooms, draft.Rooms.Select(item => (item.Id, item.IsNew)), item => item.Id) ||
            !Matches(current.Portals, draft.Portals.Select(item => (item.Id, item.IsNew)), item => item.Id) ||
            !Matches(current.Occluders, draft.Occluders.Select(item => (item.Id, item.IsNew)), item => item.Id))
        {
            message = "Un élément de visibilité a été ajouté ou remplacé hors de cette fiche. Rechargez la zone.";
            return false;
        }
        foreach (var room in draft.Rooms.Where(item => !item.IsNew))
        {
            var source = current.Rooms.Single(item => item.Id == room.Id);
            if (room.Transform.Rotation != source.Transform.Rotation ||
                !DictionaryEquivalentUx2NavJ(room.Overrides, source.Overrides))
            {
                message = "L’orientation ou les propriétés protégées d’un volume ont changé hors surface.";
                return false;
            }
        }
        foreach (var portal in draft.Portals.Where(item => !item.IsNew))
        {
            var source = current.Portals.Single(item => item.Id == portal.Id);
            if (portal.Transform.Rotation != source.Transform.Rotation)
            {
                message = "L’orientation protégée d’un passage a changé hors surface.";
                return false;
            }
        }
        foreach (var item in draft.Occluders.Where(item => !item.IsNew))
        {
            var source = current.Occluders.Single(source => source.Id == item.Id);
            if (item.Transform.Rotation != source.Transform.Rotation ||
                !DictionaryEquivalentUx2NavJ(item.Overrides, source.Overrides))
            {
                message = "L’orientation ou les propriétés protégées d’un écran ont changé hors surface.";
                return false;
            }
        }
        return true;
    }

    private static IReadOnlyList<StudioUx2NavJDiagnostic> ValidateVisibilityDraftUx2NavJ(
        StudioUx2NavJDraft draft)
    {
        var found = new List<StudioUx2NavJDiagnostic>();
        void Add(string code, string path, string message) => found.Add(new(code, path, message));
        if (!double.IsFinite(draft.CellSize) || draft.CellSize is < 1 or > 512)
            Add("studio.visibility.cell_size_invalid", "visibilite/cellules/taille",
                "La taille de cellule doit rester comprise entre 1 et 512 mètres.");
        if (!double.IsFinite(draft.StreamingDistance) || draft.StreamingDistance < draft.CellSize)
            Add("studio.visibility.streaming_invalid", "visibilite/streaming/distance",
                "La distance de streaming doit être supérieure ou égale à la taille de cellule.");
        var ids = draft.Rooms.Select(item => item.Id).Concat(draft.Portals.Select(item => item.Id))
            .Concat(draft.Occluders.Select(item => item.Id)).ToArray();
        if (ids.Any(item => item == Guid.Empty) || ids.Distinct().Count() != ids.Length)
            Add("studio.visibility.identity_invalid", "visibilite/elements",
                "Chaque volume, passage et écran doit conserver une identité interne unique.");
        foreach (var room in draft.Rooms)
        {
            if (!HumanNameValidUx2NavJ(room.Name) || !FiniteUx2NavJ(room.Transform) ||
                !double.IsFinite(room.StreamingDistance) || room.StreamingDistance < 0 ||
                room.IsNew && room.Overrides.Count > 0)
                Add("studio.visibility.room_invalid", $"visibilite/volumes/{HumanUx2NavJ(room.Name, "Volume")}",
                    "Le volume exige un nom humain, une position finie et une distance positive.");
        }
        var roomIds = draft.Rooms.Select(item => item.Id).ToHashSet();
        foreach (var portal in draft.Portals)
        {
            if (!HumanNameValidUx2NavJ(portal.Name) || !FiniteUx2NavJ(portal.Transform) ||
                portal.SourceRoomId == portal.DestinationRoomId || !roomIds.Contains(portal.SourceRoomId) ||
                !roomIds.Contains(portal.DestinationRoomId) ||
                portal.Mode == AreaVisibilityPortalMode.Conditional &&
                string.IsNullOrWhiteSpace(portal.Condition))
                Add("studio.visibility.portal_invalid", $"visibilite/passages/{HumanUx2NavJ(portal.Name, "Passage")}",
                    "Le passage exige deux volumes distincts et une condition nommée lorsqu’il est conditionnel.");
        }
        foreach (var item in draft.Occluders)
        {
            if (!HumanNameValidUx2NavJ(item.Name) || !FiniteUx2NavJ(item.Transform) ||
                item.Transform.Scale.X <= 0 || item.Transform.Scale.Y <= 0 || item.Transform.Scale.Z <= 0 ||
                item.IsNew && item.Overrides.Count > 0)
                Add("studio.visibility.occluder_invalid", $"visibilite/ecrans/{HumanUx2NavJ(item.Name, "Écran")}",
                    "L’écran d’occlusion exige un nom humain et des dimensions strictement positives.");
        }
        return found;
    }

    private static AreaVisibilityRoomProfile BuildRoomUx2NavJ(StudioUx2NavJRoomDraft draft,
        AreaVisibilityRoomProfile? current)
    {
        if (current is not null && RoomMatchesDraftUx2NavJ(current, draft)) return current;
        return new(draft.Id, draft.Name.Trim(), draft.Transform, draft.Exterior,
            (float)draft.StreamingDistance, draft.Overrides);
    }

    private static AreaVisibilityPortalProfile BuildPortalUx2NavJ(StudioUx2NavJPortalDraft draft,
        AreaVisibilityPortalProfile? current)
    {
        if (current is not null && PortalMatchesDraftUx2NavJ(current, draft)) return current;
        return new(draft.Id, draft.Name.Trim(), draft.SourceRoomId, draft.DestinationRoomId,
            draft.Transform, draft.Mode, draft.Condition.Trim(), draft.TwoWay);
    }

    private static AreaOccluderProfile BuildOccluderUx2NavJ(StudioUx2NavJOccluderDraft draft,
        AreaOccluderProfile? current)
    {
        if (current is not null && OccluderMatchesDraftUx2NavJ(current, draft)) return current;
        return new(draft.Id, draft.Name.Trim(), draft.Transform, draft.Dynamic,
            draft.MeshAssetId.Trim(), draft.Overrides);
    }

    private static bool RoomMatchesDraftUx2NavJ(AreaVisibilityRoomProfile item,
        StudioUx2NavJRoomDraft draft) => item.Id == draft.Id && item.Name == draft.Name &&
        item.Transform == draft.Transform && item.Exterior == draft.Exterior &&
        item.StreamingDistance.Equals((float)draft.StreamingDistance) &&
        DictionaryEquivalentUx2NavJ(item.Overrides, draft.Overrides);

    private static bool PortalMatchesDraftUx2NavJ(AreaVisibilityPortalProfile item,
        StudioUx2NavJPortalDraft draft) => item.Id == draft.Id && item.Name == draft.Name &&
        item.SourceRoomId == draft.SourceRoomId && item.DestinationRoomId == draft.DestinationRoomId &&
        item.Transform == draft.Transform && item.Mode == draft.Mode && item.Condition == draft.Condition &&
        item.TwoWay == draft.TwoWay;

    private static bool OccluderMatchesDraftUx2NavJ(AreaOccluderProfile item,
        StudioUx2NavJOccluderDraft draft) => item.Id == draft.Id && item.Name == draft.Name &&
        item.Transform == draft.Transform && item.Dynamic == draft.Dynamic &&
        item.MeshAssetId == draft.MeshAssetId && DictionaryEquivalentUx2NavJ(item.Overrides, draft.Overrides);

    private static StudioUx2NavJRuntimePreview BuildVisibilityRuntimePreviewUx2NavJ(
        ProjectDefinition project, AreaDefinition area)
    {
        var render = AreaExperienceRenderPlanBuilder.Build(area);
        var typed = AreaExperienceAuthoringScriptService.Read(area);
        var runtime = AreaExperienceRuntimeResolver.Resolve(area,
            area.Authoring.Experience.RequireRuntimeMetadata);
        var performance = AreaPerformanceAnalyzer.Analyze(project, area.Id);
        var portals = render.VisibilityPortals;
        return new(render.Rooms.Count,
            portals.Count(item => item.Mode == AreaVisibilityPortalMode.Open),
            portals.Count(item => item.Mode == AreaVisibilityPortalMode.Closed),
            portals.Count(item => item.Mode == AreaVisibilityPortalMode.Conditional),
            render.Occluders.Count, render.Occluders.Count(item => item.Dynamic),
            runtime.Handle is not null && typed.VisibilityRooms == render.Rooms.Count,
            performance.IsWithinBudget, performance.Diagnostics,
            "Plan canonique et budget CPU seulement · aucun culling GPU, mesh HLOD, streaming réel, réseau ou préchargement n’est exécuté.");
    }

    private static StudioNavigationOverlaySnapshot BuildVisibilityOverlayUx2NavJ(
        AreaVisibilityAuthoringProfile visibility)
    {
        var markers = new List<StudioNavigationOverlayMarker>();
        var segments = new List<StudioNavigationOverlaySegment>();
        foreach (var room in visibility.Rooms)
            markers.Add(new($"ux2-nav-j-room:{room.Id:D}", HumanUx2NavJ(room.Name, "Volume"),
                StudioNavigationOverlaySemantic.Visibility, PointUx2NavJ(room.Transform.Position)));
        foreach (var item in visibility.Occluders)
            markers.Add(new($"ux2-nav-j-occluder:{item.Id:D}", HumanUx2NavJ(item.Name, "Écran"),
                StudioNavigationOverlaySemantic.Visibility, PointUx2NavJ(item.Transform.Position)));
        var rooms = visibility.Rooms.ToDictionary(item => item.Id);
        foreach (var portal in visibility.Portals)
        {
            if (!rooms.TryGetValue(portal.SourceRoomId, out var source) ||
                !rooms.TryGetValue(portal.DestinationRoomId, out var destination)) continue;
            segments.Add(new($"ux2-nav-j-portal:{portal.Id:D}", HumanUx2NavJ(portal.Name, "Passage"),
                StudioNavigationOverlaySemantic.Visibility, PointUx2NavJ(source.Transform.Position),
                PointUx2NavJ(destination.Transform.Position), Directed: !portal.TwoWay,
                Broken: portal.Mode == AreaVisibilityPortalMode.Closed));
        }
        return new(markers, segments, []);
    }

    private static IReadOnlyList<string> BuildVisibilityDiffUx2NavJ(
        AreaVisibilityAuthoringProfile source, AreaVisibilityAuthoringProfile candidate)
    {
        if (VisibilityEquivalentUx2NavJ(source, candidate)) return [];
        var diff = new List<string>();
        if (source.OcclusionCulling != candidate.OcclusionCulling ||
            source.DistanceStreaming != candidate.DistanceStreaming ||
            source.CellVisualizer != candidate.CellVisualizer)
            diff.Add($"Options : occlusion {(candidate.OcclusionCulling ? "active" : "inactive")}, " +
                     $"streaming {(candidate.DistanceStreaming ? "actif" : "inactif")}, " +
                     $"cellules {(candidate.CellVisualizer ? "visibles" : "masquées")}.");
        if (!source.CellSize.Equals(candidate.CellSize) ||
            !source.StreamingDistance.Equals(candidate.StreamingDistance))
            diff.Add($"Grille : cellule {source.CellSize:0.##} → {candidate.CellSize:0.##} m · " +
                     $"distance {source.StreamingDistance:0.##} → {candidate.StreamingDistance:0.##} m.");
        AddCollectionDiffUx2NavJ(diff, "Volumes", source.Rooms.Select(item => (item.Id, item.Name)).ToArray(),
            candidate.Rooms.Select(item => (item.Id, item.Name)).ToArray());
        AddCollectionDiffUx2NavJ(diff, "Passages", source.Portals.Select(item => (item.Id, item.Name)).ToArray(),
            candidate.Portals.Select(item => (item.Id, item.Name)).ToArray());
        AddCollectionDiffUx2NavJ(diff, "Écrans", source.Occluders.Select(item => (item.Id, item.Name)).ToArray(),
            candidate.Occluders.Select(item => (item.Id, item.Name)).ToArray());
        if (diff.Count == 0) diff.Add("Transformations ou politiques des éléments sélectionnés mises à jour.");
        return diff;
    }

    private static void AddCollectionDiffUx2NavJ(ICollection<string> diff, string title,
        IReadOnlyList<(Guid Id, string Name)> source, IReadOnlyList<(Guid Id, string Name)> candidate)
    {
        if (source.Count == candidate.Count && source.Select(item => item.Id)
                .SequenceEqual(candidate.Select(item => item.Id)) && source.Select(item => item.Name)
                .SequenceEqual(candidate.Select(item => item.Name))) return;
        diff.Add($"{title} : {source.Count} → {candidate.Count} · ordre " +
                 string.Join(" → ", candidate.Select(item => HumanUx2NavJ(item.Name, title.TrimEnd('s')))) + ".");
    }

    private static bool VisibilityEquivalentUx2NavJ(AreaVisibilityAuthoringProfile left,
        AreaVisibilityAuthoringProfile right) => left.OcclusionCulling == right.OcclusionCulling &&
        left.DistanceStreaming == right.DistanceStreaming && left.CellVisualizer == right.CellVisualizer &&
        left.CellSize.Equals(right.CellSize) && left.StreamingDistance.Equals(right.StreamingDistance) &&
        left.Rooms.Count == right.Rooms.Count && left.Rooms.Zip(right.Rooms)
            .All(pair => RoomEquivalentUx2NavJ(pair.First, pair.Second)) &&
        left.Portals.Count == right.Portals.Count && left.Portals.Zip(right.Portals)
            .All(pair => PortalEquivalentUx2NavJ(pair.First, pair.Second)) &&
        left.Occluders.Count == right.Occluders.Count && left.Occluders.Zip(right.Occluders)
            .All(pair => OccluderEquivalentUx2NavJ(pair.First, pair.Second));

    private static bool RoomEquivalentUx2NavJ(AreaVisibilityRoomProfile left,
        AreaVisibilityRoomProfile right) => left.Id == right.Id && left.Name == right.Name &&
        left.Transform == right.Transform && left.Exterior == right.Exterior &&
        left.StreamingDistance.Equals(right.StreamingDistance) &&
        DictionaryEquivalentUx2NavJ(left.Overrides, right.Overrides);

    private static bool PortalEquivalentUx2NavJ(AreaVisibilityPortalProfile left,
        AreaVisibilityPortalProfile right) => left.Id == right.Id && left.Name == right.Name &&
        left.SourceRoomId == right.SourceRoomId && left.DestinationRoomId == right.DestinationRoomId &&
        left.Transform == right.Transform && left.Mode == right.Mode && left.Condition == right.Condition &&
        left.TwoWay == right.TwoWay;

    private static bool OccluderEquivalentUx2NavJ(AreaOccluderProfile left,
        AreaOccluderProfile right) => left.Id == right.Id && left.Name == right.Name &&
        left.Transform == right.Transform && left.Dynamic == right.Dynamic &&
        left.MeshAssetId == right.MeshAssetId && DictionaryEquivalentUx2NavJ(left.Overrides, right.Overrides);

    private static bool DictionaryEquivalentUx2NavJ(IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right) => left.Count == right.Count &&
        left.All(item => right.TryGetValue(item.Key, out var value) && value == item.Value);

    private static ProjectDefinition ReplaceAreaUx2NavJ(ProjectDefinition project, AreaDefinition area) => new(
        project.Id, project.Title, project.Module, project.Blueprints,
        project.Areas.Select(item => item.Id == area.Id ? area : item).ToArray(),
        project.Dialogues, project.Quests);

    private static StudioUx2NavJPreview FailedVisibilityPreviewUx2NavJ(string message,
        IReadOnlyList<StudioUx2NavJDiagnostic>? diagnostics = null) => new(false, message,
        "Aucun aperçu canonique disponible.", false, [], diagnostics ?? [],
        StudioNavigationOverlaySnapshot.Empty, null);

    private static string VisibilitySourceFingerprintUx2NavJ(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));

    private static string VisibilityDraftFingerprintUx2NavJ(StudioUx2NavJDraft draft)
    {
        var value = new StringBuilder();
        value.Append(draft.AreaId).Append('|').Append(draft.BaseRevision).Append('|')
            .Append(draft.SourceFingerprint).Append('|').Append(draft.OcclusionCulling).Append('|')
            .Append(draft.DistanceStreaming).Append('|').Append(draft.CellVisualizer).Append('|')
            .Append(draft.CellSize.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|')
            .Append(draft.StreamingDistance.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        foreach (var room in draft.Rooms)
        {
            AppendDraftItemUx2NavJ(value, room.Id, room.IsNew, room.Name, room.Transform,
                $"{room.NameIsProtected}|{room.Exterior}|{room.StreamingDistance:R}");
            AppendDictionaryUx2NavJ(value, room.Overrides);
        }
        foreach (var portal in draft.Portals)
            AppendDraftItemUx2NavJ(value, portal.Id, portal.IsNew, portal.Name, portal.Transform,
                $"{portal.NameIsProtected}|{portal.SourceRoomId:D}|{portal.DestinationRoomId:D}|{portal.Mode}|{portal.ConditionIsProtected}|{portal.Condition}|{portal.TwoWay}");
        foreach (var item in draft.Occluders)
        {
            AppendDraftItemUx2NavJ(value, item.Id, item.IsNew, item.Name, item.Transform,
                $"{item.NameIsProtected}|{item.Dynamic}|{item.MeshAssetIsProtected}|{item.MeshAssetId}");
            AppendDictionaryUx2NavJ(value, item.Overrides);
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.ToString())));
    }

    private static void AppendDraftItemUx2NavJ(StringBuilder value, Guid id, bool isNew, string name,
        TransformValue transform, string tail)
    {
        value.Append('|').Append(id.ToString("D")).Append('|').Append(isNew).Append('|').Append(name)
            .Append('|').Append(transform.Position.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
            .Append('|').Append(transform.Position.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
            .Append('|').Append(transform.Position.Z.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
            .Append('|').Append(transform.Rotation.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
            .Append('|').Append(transform.Rotation.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
            .Append('|').Append(transform.Rotation.Z.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
            .Append('|').Append(transform.Rotation.W.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
            .Append('|').Append(transform.Scale.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
            .Append('|').Append(transform.Scale.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
            .Append('|').Append(transform.Scale.Z.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
            .Append('|').Append(tail);
    }

    private static void AppendDictionaryUx2NavJ(StringBuilder value,
        IReadOnlyDictionary<string, string> items)
    {
        foreach (var item in items.OrderBy(item => item.Key, StringComparer.Ordinal))
            value.Append('|').Append(item.Key).Append('=').Append(item.Value);
    }

    private static bool FiniteUx2NavJ(TransformValue value) =>
        float.IsFinite(value.Position.X) && float.IsFinite(value.Position.Y) && float.IsFinite(value.Position.Z) &&
        float.IsFinite(value.Rotation.X) && float.IsFinite(value.Rotation.Y) &&
        float.IsFinite(value.Rotation.Z) && float.IsFinite(value.Rotation.W) &&
        float.IsFinite(value.Scale.X) && float.IsFinite(value.Scale.Y) && float.IsFinite(value.Scale.Z);

    private static bool HumanNameValidUx2NavJ(string? value) => !string.IsNullOrWhiteSpace(value) &&
        value.Trim().Length <= 160;

    private static string HumanUx2NavJ(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) || ContainsTechnicalIdentifierUx2NavJ(value) ? fallback : value.Trim();

    private static bool ContainsTechnicalIdentifierUx2NavJ(string? value) =>
        !string.IsNullOrWhiteSpace(value) && (Guid.TryParse(value.Trim(), out _) ||
            Regex.IsMatch(value, "(?i)(?<![0-9a-f])[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}(?![0-9a-f])"));

    private static string HumanDiagnosticUx2NavJ(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "La configuration a été refusée.";
        var human = value.Replace("UUID", "identité interne", StringComparison.OrdinalIgnoreCase);
        return Regex.Replace(human,
            "(?i)(?<![0-9a-f])[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}(?![0-9a-f])",
            "identité interne");
    }

    private static StudioNavigationPoint PointUx2NavJ(Vector3Value value) => new(value.X, value.Y, value.Z);

    internal ProjectDefinition? CreateUx2NavJEvidenceProject()
    {
        var source = Project;
        return source is null ? null : CreateUx2NavJEvidenceProject(source.Id.Value);
    }

    private static ProjectDefinition CreateUx2NavJEvidenceProject(Guid seed)
    {
        var project = CreateUx2NavIEvidenceProject(seed == Guid.Empty
            ? Guid.Parse("c233c234-0000-4000-8000-000000000001") : seed);
        var area = project.Areas.First();
        var roomA = new AreaVisibilityRoomProfile(StableUx2NavJ(seed, "room:a"), "Galerie nord",
            new TransformValue(new Vector3Value(-8, 1, 0), QuaternionValue.Identity,
                new Vector3Value(12, 5, 10)), false, 128,
            new Dictionary<string, string> { ["lighting"] = "preserve:north" });
        var roomB = new AreaVisibilityRoomProfile(StableUx2NavJ(seed, "room:b"),
            StableUx2NavJ(seed, "protected-room-name").ToString("D"),
            new TransformValue(new Vector3Value(10, 1, 2), QuaternionValue.Identity,
                new Vector3Value(10, 6, 12)), false, 144,
            new Dictionary<string, string> { ["custom:neighbor"] = "preserved" });
        var portal = new AreaVisibilityPortalProfile(StableUx2NavJ(seed, "portal:a"),
            "Arche du cristal", roomA.Id, roomB.Id,
            new TransformValue(new Vector3Value(1, 2, 1), QuaternionValue.Identity,
                new Vector3Value(2, 4, .5f)), AreaVisibilityPortalMode.Conditional,
            "quest.crystal.open", true);
        var occluderA = new AreaOccluderProfile(StableUx2NavJ(seed, "occluder:a"), "Mur nord",
            new TransformValue(new Vector3Value(-3, 2, -2), QuaternionValue.Identity,
                new Vector3Value(8, 4, .5f)), false, "architecture/mur-nord",
            new Dictionary<string, string> { ["material"] = "stone" });
        var occluderB = new AreaOccluderProfile(StableUx2NavJ(seed, "occluder:neighbor"),
            "Écran voisin protégé",
            new TransformValue(new Vector3Value(12, 2, 5), QuaternionValue.Identity,
                new Vector3Value(5, 4, .5f)), true,
            StableUx2NavJ(seed, "protected-mesh-asset").ToString("D"),
            new Dictionary<string, string> { ["custom:unknown"] = "keep-exactly" });
        var visibility = new AreaVisibilityAuthoringProfile([roomA, roomB], [portal],
            [occluderA, occluderB], true, true, true, 16, 160);
        var source = area.Authoring;
        var experience = source.Experience;
        var configuredExperience = new AreaExperienceAuthoringProfile(experience.Id,
            experience.SchemaVersion, Ux2NavJEvidenceProvenance, experience.AudioSnapshots,
            visibility, experience.Lod, experience.RequireRuntimeMetadata);
        var authoring = new AreaAuthoringProfile(source.SchemaVersion, source.UserEvents, source.Map,
            source.Layers, configuredExperience, source.RequireLayerRuntimeMetadata);
        return ReplaceAreaUx2NavJ(project, area.WithAuthoring(authoring));
    }

    private static Guid StableUx2NavJ(Guid seed, string discriminator)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"ux2-nav-j:{seed:D}:{discriminator}"));
        Span<byte> id = stackalloc byte[16];
        bytes.AsSpan(0, id.Length).CopyTo(id);
        id[6] = (byte)((id[6] & 0x0f) | 0x40);
        id[8] = (byte)((id[8] & 0x3f) | 0x80);
        return new Guid(id);
    }

    internal StudioUx2NavJSmokeResult RunUx2NavJContractSmoke()
    {
        var sourceProject = Project;
        var sourceHistory = History;
        if (sourceProject is null || sourceHistory is null) return FailedUx2NavJSmoke();
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceUndo = sourceHistory.CanUndo;
        var sourceRedo = sourceHistory.CanRedo;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceApproval = CaptureVisibilityApprovalUx2NavJ();
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-nav-j-smoke-" + Guid.NewGuid().ToString("N"));
        StudioSessionController? editing = null;
        StudioSessionController? reopened = null;
        try
        {
            var fixture = CreateUx2NavJEvidenceProject(Guid.Parse("c233c234-0000-4000-8000-000000000099"));
            if (!ProjectSession.Create(tempRoot, fixture).Succeeded) return FailedUx2NavJSmoke();
            editing = new StudioSessionController();
            if (!editing.Open(tempRoot).Succeeded) return FailedUx2NavJSmoke();
            var choice = editing.VisibilityAreaChoicesUx2NavJ.Single(item => item.IsEvidence);
            var explicitSelection = editing.CreateVisibilityDraftUx2NavJ(null) is null &&
                                    editing.CreateVisibilityDraftUx2NavJ(choice.AreaId) is not null;
            var humanLabels = editing.VisibilityAreaChoicesUx2NavJ.All(item =>
                !ContainsTechnicalIdentifierUx2NavJ(item.Label));
            var baseDraft = editing.CreateVisibilityDraftUx2NavJ(choice.AreaId)!;
            var invalidCell = editing.PreviewVisibilityUx2NavJ(baseDraft with { CellSize = 0 });
            var invalidPortal = editing.PreviewVisibilityUx2NavJ(baseDraft with
            {
                Portals = baseDraft.Portals.Select((item, index) => index == 0
                    ? item with { DestinationRoomId = item.SourceRoomId } : item).ToArray(),
            });
            var staleSource = editing.PreviewVisibilityUx2NavJ(baseDraft with
            {
                SourceFingerprint = "stale",
            });
            var staleRevisionDraft = baseDraft;
            var otherArea = editing.Project!.Areas.First(item => item.Id != choice.AreaId);
            var neighborChanged = ApplyUx2NavJNeighborMutation(editing, otherArea);
            var staleRevision = editing.PreviewVisibilityUx2NavJ(staleRevisionDraft);
            var staleRevisionBlocked = neighborChanged && !staleRevision.Succeeded && editing.Undo().Succeeded;
            var approvalDraft = editing.CreateVisibilityDraftUx2NavJ(choice.AreaId)! with
            {
                CellVisualizer = !baseDraft.CellVisualizer,
            };
            var approvalPreview = editing.PreviewVisibilityUx2NavJ(approvalDraft);
            neighborChanged = ApplyUx2NavJNeighborMutation(editing, otherArea);
            var staleApply = editing.ApplyApprovedVisibilityUx2NavJ();
            var staleApprovalBlocked = approvalPreview.Succeeded && neighborChanged &&
                                       !staleApply.Succeeded && editing.Undo().Succeeded;

            var draft = editing.CreateVisibilityDraftUx2NavJ(choice.AreaId)!;
            var sourceTarget = editing.Project!.Areas.Single(item => item.Id == choice.AreaId);
            var sourceAuthoring = sourceTarget.Authoring;
            var protectedNeighbor = sourceAuthoring.Experience.Visibility.Occluders[1];
            var editedRoom = draft.Rooms[0] with
            {
                Name = "Galerie nord restaurée",
                Transform = new TransformValue(new Vector3Value(-6, 1, 1),
                    draft.Rooms[0].Transform.Rotation, draft.Rooms[0].Transform.Scale),
                StreamingDistance = 176,
            };
            var editedPortal = draft.Portals[0] with
            {
                Mode = AreaVisibilityPortalMode.Open,
                ConditionIsProtected = false,
                Condition = string.Empty,
                TwoWay = false,
            };
            var editedOccluder = draft.Occluders[0] with
            {
                Dynamic = true,
                MeshAssetIsProtected = false,
                MeshAssetId = "architecture/mur-nord-mobile",
            };
            var withRoom = draft with
            {
                Rooms = draft.Rooms.Select((item, index) => index == 0 ? editedRoom : item).ToArray(),
                Portals = draft.Portals.Select((item, index) => index == 0 ? editedPortal : item).ToArray(),
                Occluders = draft.Occluders.Select((item, index) => index == 0 ? editedOccluder : item).ToArray(),
                CellSize = 20,
                StreamingDistance = 200,
                CellVisualizer = !draft.CellVisualizer,
            };
            var newRoom = editing.CreateNewVisibilityRoomDraftUx2NavJ(withRoom) with
            {
                Name = "Belvédère",
                Transform = new TransformValue(new Vector3Value(24, 3, 4), QuaternionValue.Identity,
                    new Vector3Value(8, 4, 8)),
            };
            withRoom = withRoom with { Rooms = withRoom.Rooms.Concat([newRoom]).ToArray() };
            var newPortal = editing.CreateNewVisibilityPortalDraftUx2NavJ(withRoom)! with
            {
                Name = "Passage du belvédère",
                SourceRoomId = withRoom.Rooms[1].Id,
                DestinationRoomId = newRoom.Id,
            };
            var newOccluder = editing.CreateNewOccluderDraftUx2NavJ(withRoom) with
            {
                Name = "Écran du belvédère",
                MeshAssetIsProtected = false,
                MeshAssetId = "architecture/belvedere-screen",
            };
            var configured = withRoom with
            {
                Portals = withRoom.Portals.Concat([newPortal]).ToArray(),
                Occluders = withRoom.Occluders.Concat([newOccluder]).ToArray(),
            };
            var projectBeforeApply = editing.Project;
            var preview = editing.PreviewVisibilityUx2NavJ(configured);
            var dryRun = preview.Succeeded && preview.CanApply && ReferenceEquals(projectBeforeApply, editing.Project);
            var runtimePreview = preview.Runtime is { Rooms: 3, OpenPortals: 2, Occluders: 3,
                TypedRuntimeHandle: true } && preview.Overlay.Markers.Count >= 6 &&
                preview.Overlay.Segments.Count == 2;
            var applied = editing.ApplyApprovedVisibilityUx2NavJ();
            var appliedProject = editing.Project;
            var finalArea = appliedProject?.Areas.Single(item => item.Id == choice.AreaId);
            var finalAuthoring = finalArea?.Authoring;
            var finalVisibility = finalAuthoring?.Experience.Visibility;
            var visibilityAuthoring = applied.Succeeded && finalVisibility is not null &&
                                      finalVisibility.Rooms.Count == 3 && finalVisibility.Portals.Count == 2 &&
                                      finalVisibility.Occluders.Count == 3 && finalVisibility.CellSize == 20 &&
                                      finalVisibility.StreamingDistance == 200 &&
                                      finalVisibility.CellVisualizer == configured.CellVisualizer &&
                                      finalVisibility.Rooms[0].Name == "Galerie nord restaurée" &&
                                      finalVisibility.Rooms[0].Transform.Position == new Vector3Value(-6, 1, 1) &&
                                      finalVisibility.Rooms[0].StreamingDistance == 176 &&
                                      finalVisibility.Portals[0].Mode == AreaVisibilityPortalMode.Open &&
                                      string.IsNullOrEmpty(finalVisibility.Portals[0].Condition) &&
                                      !finalVisibility.Portals[0].TwoWay && finalVisibility.Occluders[0].Dynamic &&
                                      finalVisibility.Occluders[0].MeshAssetId ==
                                      "architecture/mur-nord-mobile";
            var stableOrder = finalVisibility is not null &&
                              finalVisibility.Rooms.Take(2).Select(item => item.Id)
                                  .SequenceEqual(sourceAuthoring.Experience.Visibility.Rooms.Select(item => item.Id)) &&
                              finalVisibility.Portals[0].Id == sourceAuthoring.Experience.Visibility.Portals[0].Id &&
                              finalVisibility.Occluders.Take(2).Select(item => item.Id)
                                  .SequenceEqual(sourceAuthoring.Experience.Visibility.Occluders.Select(item => item.Id));
            var unknownNeighbors = finalVisibility is not null &&
                                   ReferenceEquals(finalVisibility.Occluders[1], protectedNeighbor) &&
                                   finalVisibility.Occluders[1].MeshAssetId == protectedNeighbor.MeshAssetId &&
                                   DictionaryEquivalentUx2NavJ(finalVisibility.Occluders[1].Overrides,
                                       protectedNeighbor.Overrides);
            var lodPreserved = finalAuthoring is not null &&
                               ReferenceEquals(finalAuthoring.Experience.Lod,
                                   sourceAuthoring.Experience.Lod);
            var otherAuthoring = finalAuthoring is not null &&
                                 ReferenceEquals(finalAuthoring.Map, sourceAuthoring.Map) &&
                                 finalAuthoring.UserEvents.SequenceEqual(sourceAuthoring.UserEvents) &&
                                 finalAuthoring.Layers.SequenceEqual(sourceAuthoring.Layers) &&
                                 finalAuthoring.Experience.AudioSnapshots.SequenceEqual(
                                     sourceAuthoring.Experience.AudioSnapshots) &&
                                 finalAuthoring.RequireLayerRuntimeMetadata ==
                                 sourceAuthoring.RequireLayerRuntimeMetadata;
            var canonicalCommand = editing.History?.Journal.LastOrDefault()?.CommandName == "set_area_authoring";
            var undo = editing.Undo();
            var undone = undo.Succeeded && ReferenceEquals(projectBeforeApply, editing.Project);
            var redo = editing.Redo();
            var redone = redo.Succeeded && ReferenceEquals(appliedProject, editing.Project);
            var undoRedo = undone && redone;
            var save = editing.Save();
            reopened = new StudioSessionController();
            var reopenedOk = save.Succeeded && reopened.Open(tempRoot).Succeeded;
            var loadedArea = reopened.Project?.Areas.Single(item => item.Id == choice.AreaId);
            var persistence = reopenedOk && loadedArea is not null && finalVisibility is not null &&
                              VisibilityEquivalentUx2NavJ(loadedArea.Authoring.Experience.Visibility,
                                  finalVisibility) && LodEquivalentUx2NavJ(loadedArea.Authoring.Experience.Lod,
                                  sourceAuthoring.Experience.Lod);
            var projectNeighbors = finalArea is not null && appliedProject is not null &&
                                   appliedProject.Areas.Where(item => item.Id != choice.AreaId)
                                       .SequenceEqual(projectBeforeApply!.Areas.Where(item => item.Id != choice.AreaId)) &&
                                   ReferenceEquals(appliedProject.Module, projectBeforeApply.Module) &&
                                   appliedProject.Blueprints.SequenceEqual(projectBeforeApply.Blueprints) &&
                                   appliedProject.Dialogues.SequenceEqual(projectBeforeApply.Dialogues) &&
                                   appliedProject.Quests.SequenceEqual(projectBeforeApply.Quests);
            var text = string.Join(' ', editing.VisibilityAreaChoicesUx2NavJ.Select(item => item.Label)) + " " +
                       preview.Summary + " " + string.Join(' ', preview.Diff) + " " +
                       string.Join(' ', preview.Diagnostics.Select(item => item.Message));
            var humanText = !text.Contains(choice.AreaId.ToString(), StringComparison.OrdinalIgnoreCase) &&
                            !ContainsTechnicalIdentifierUx2NavJ(text) &&
                            preview.Overlay.Markers.All(item =>
                                !ContainsTechnicalIdentifierUx2NavJ(item.HumanLabel)) &&
                            preview.Overlay.Segments.All(item =>
                                !ContainsTechnicalIdentifierUx2NavJ(item.HumanLabel));
            var validation = !invalidCell.Succeeded && !invalidPortal.Succeeded && !staleSource.Succeeded;
            var sourceUntouched = ReferenceEquals(sourceProject, Project) &&
                                  ReferenceEquals(sourceHistory, History) && History?.State.Revision == sourceRevision &&
                                  (History?.Journal ?? []).SequenceEqual(sourceJournal) && IsDirty == sourceDirty &&
                                  History?.CanUndo == sourceUndo && History?.CanRedo == sourceRedo &&
                                  ActiveAreaId == sourceArea && SelectedEntityIds.SequenceEqual(sourceSelection) &&
                                  SelectedEntityId == sourceSelected &&
                                  CaptureVisibilityApprovalUx2NavJ() == sourceApproval;
            var succeeded = explicitSelection && humanLabels && visibilityAuthoring && stableOrder &&
                            unknownNeighbors && lodPreserved && otherAuthoring && validation && runtimePreview &&
                            dryRun && staleRevisionBlocked && staleApprovalBlocked && canonicalCommand && undoRedo &&
                            persistence && projectNeighbors && sourceUntouched && humanText;
            return new(succeeded, explicitSelection, humanLabels, visibilityAuthoring, stableOrder,
                unknownNeighbors, lodPreserved, otherAuthoring, validation, runtimePreview, dryRun,
                staleRevisionBlocked, !staleSource.Succeeded, staleApprovalBlocked, canonicalCommand,
                undoRedo, persistence, projectNeighbors, sourceUntouched, humanText);
        }
        catch
        {
            return FailedUx2NavJSmoke();
        }
        finally
        {
            reopened?.Close();
            editing?.Close();
            _ = DeleteUx2NavJTemporaryWorkspace(tempRoot);
        }
    }

    private static bool ApplyUx2NavJNeighborMutation(StudioSessionController controller,
        AreaDefinition area)
    {
        var history = controller.History!;
        var source = area.Authoring;
        var experience = source.Experience;
        var changedExperience = new AreaExperienceAuthoringProfile(experience.Id,
            experience.SchemaVersion, "studio:ux2-nav-j:neighbor", experience.AudioSnapshots,
            experience.Visibility, experience.Lod, experience.RequireRuntimeMetadata);
        var authoring = new AreaAuthoringProfile(source.SchemaVersion, source.UserEvents, source.Map,
            source.Layers, changedExperience, source.RequireLayerRuntimeMetadata);
        var result = controller._setAreaAuthoringUx2NavJ.Handle(history.State,
            new SetAreaAuthoringCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                area.Id, authoring, false));
        if (!result.Succeeded) return false;
        history.Record(result);
        return true;
    }

    private static bool LodEquivalentUx2NavJ(AreaLodAuthoringProfile left,
        AreaLodAuthoringProfile right) => left.DefaultSourceMode == right.DefaultSourceMode &&
        left.DistanceThresholds.SequenceEqual(right.DistanceThresholds) &&
        left.MaximumVisibleTriangles == right.MaximumVisibleTriangles &&
        left.MaximumHlodClusters == right.MaximumHlodClusters &&
        left.HlodClusterSize.Equals(right.HlodClusterSize) &&
        left.PoppingTolerance.Equals(right.PoppingTolerance) && left.CrossFade == right.CrossFade &&
        left.MeasurePopping == right.MeasurePopping &&
        left.AssetOverrides.Count == right.AssetOverrides.Count && left.AssetOverrides.Zip(right.AssetOverrides)
            .All(pair => LodOverrideEquivalentUx2NavJ(pair.First, pair.Second));

    private static bool LodOverrideEquivalentUx2NavJ(AreaLodAssetOverride left,
        AreaLodAssetOverride right) => left.Id == right.Id && left.AssetId == right.AssetId &&
        left.SourceMode == right.SourceMode && left.ScreenRatios.SequenceEqual(right.ScreenRatios) &&
        left.TriangleBudgets.SequenceEqual(right.TriangleBudgets) &&
        left.CrossFadeSeconds.Equals(right.CrossFadeSeconds) && left.Provenance == right.Provenance;

    internal static bool DeleteUx2NavJTemporaryWorkspace(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return true;
        try
        {
            var full = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var leaf = Path.GetFileName(full);
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !leaf.StartsWith("openrpg-forge-studio-ux2-nav-j-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, true);
            return !Directory.Exists(full);
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static StudioUx2NavJSmokeResult FailedUx2NavJSmoke() => new(
        Succeeded: false,
        ExplicitSelection: false,
        HumanLabels: false,
        VisibilityAuthoring: false,
        StableOrder: false,
        UnknownNeighborsPreserved: false,
        LodPreservedByReference: false,
        OtherAuthoringPreserved: false,
        Validation: false,
        RuntimePreview: false,
        DryRun: false,
        StaleRevisionBlocked: false,
        StaleSourceBlocked: false,
        StaleApprovalBlocked: false,
        CanonicalCommand: false,
        UndoRedo: false,
        Persistence: false,
        ProjectNeighborsPreserved: false,
        SourceUntouched: false,
        HumanText: false);
}
