using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Core.Navigation;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2NavMAuthoringProvenance = "studio:ux2-nav-m:authoring";
    private const string Ux2NavMEvidenceProvenance = "studio:ux2-nav-m:evidence";
    private readonly SetCinematicProfilesHandler _setCameraAnchorsUx2NavM = new(new SystemClock());
    private StudioUx2NavMDraft? _approvedCameraAnchorUx2NavM;
    private ProjectRevision? _approvedCameraAnchorRevisionUx2NavM;
    private string? _approvedCameraAnchorSourceFingerprintUx2NavM;
    private string? _approvedCameraAnchorDraftFingerprintUx2NavM;

    internal IReadOnlyList<StudioUx2NavMAreaChoice> CameraAnchorAreaChoicesUx2NavM
    {
        get
        {
            var project = Project;
            if (project is null) return [];
            var duplicates = project.Areas.GroupBy(item => item.DisplayName,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToDictionary(item => item.Key, item => item.Count(), StringComparer.CurrentCultureIgnoreCase);
            return project.Areas.Select(item => new StudioUx2NavMAreaChoice(item.Id,
                    duplicates[item.DisplayName] > 1
                        ? $"{item.DisplayName} · {HumanTokenUx2NavM(item.Slug)} · " +
                          $"{item.RuntimePolicies.Cinematics.CameraAnchors.Count} ancre(s) caméra"
                        : $"{item.DisplayName} · {item.RuntimePolicies.Cinematics.CameraAnchors.Count} ancre(s) caméra",
                    item.RuntimePolicies.Cinematics.CameraAnchors.Count,
                    item.RuntimePolicies.Cinematics.CameraAnchors.Any(anchor =>
                        string.Equals(anchor.Provenance, Ux2NavMEvidenceProvenance, StringComparison.Ordinal))))
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal IReadOnlyList<StudioUx2NavMAnchorChoice> CameraAnchorChoicesUx2NavM(AreaId? areaId)
    {
        var area = areaId is null ? null : Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        return area?.RuntimePolicies.Cinematics.CameraAnchors.Select(item => new StudioUx2NavMAnchorChoice(
                item.Id,
                $"{item.Name} · {HumanFramingUx2NavM(item.Framing)} · " +
                $"champ {item.FieldOfViewDegrees:0.##}°",
                string.Equals(item.Provenance, Ux2NavMEvidenceProvenance, StringComparison.Ordinal)))
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray() ?? [];
    }

    internal StudioUx2NavMDraft? CreateCameraAnchorDraftUx2NavM(AreaId? areaId, Guid? anchorId)
    {
        var project = Project;
        var history = History;
        if (project is null || history is null || areaId is null || anchorId is null) return null;
        var area = project.Areas.SingleOrDefault(item => item.Id == areaId);
        var anchor = area?.RuntimePolicies.Cinematics.CameraAnchors.SingleOrDefault(item => item.Id == anchorId);
        return area is null || anchor is null
            ? null
            : DraftFromAnchorUx2NavM(area, anchor, history.State.Revision, ProjectFingerprintUx2NavM(project));
    }

    internal StudioUx2NavMDraft? CreateNewCameraAnchorDraftUx2NavM(AreaId? areaId)
    {
        var project = Project;
        var history = History;
        if (project is null || history is null || areaId is null) return null;
        var area = project.Areas.SingleOrDefault(item => item.Id == areaId);
        if (area is null) return null;
        var index = area.RuntimePolicies.Cinematics.CameraAnchors.Count + 1;
        return new(area.Id, history.State.Revision, ProjectFingerprintUx2NavM(project), Guid.NewGuid(), true,
            Ux2NavMAuthoringProvenance, $"Plan caméra {index}",
            0, 6, 0, 0, 0, 0, 1, 1, 1, 1,
            0, 1.5, 0, CameraFraming.Medium, 55, 2_000, .08,
            ["camera", "authoring"], area.RuntimePolicies.Cinematics, null);
    }

    private static StudioUx2NavMDraft DraftFromAnchorUx2NavM(
        AreaDefinition area,
        CameraAnchorDefinition anchor,
        ProjectRevision revision,
        string fingerprint)
    {
        var transform = anchor.Transform;
        return new(area.Id, revision, fingerprint, anchor.Id, false, anchor.Provenance, anchor.Name,
            transform.Position.X, transform.Position.Y, transform.Position.Z,
            transform.Rotation.X, transform.Rotation.Y, transform.Rotation.Z, transform.Rotation.W,
            transform.Scale.X, transform.Scale.Y, transform.Scale.Z,
            anchor.LookAt.X, anchor.LookAt.Y, anchor.LookAt.Z, anchor.Framing,
            anchor.FieldOfViewDegrees, anchor.DefaultDurationMilliseconds, anchor.SafeAreaMargin,
            anchor.Tags.ToArray(), area.RuntimePolicies.Cinematics, anchor);
    }

    internal StudioUx2NavMPreview PreviewCameraAnchorUx2NavM(StudioUx2NavMDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelCameraAnchorPreviewUx2NavM();
        var history = History;
        var project = Project;
        if (history is null || project is null)
            return FailedCameraAnchorPreviewUx2NavM("Aucun module n’est ouvert.");
        if (draft.BaseRevision != history.State.Revision)
            return FailedCameraAnchorPreviewUx2NavM(
                "Le projet a changé depuis l’ouverture de la fiche. Rechargez l’ancre.");
        if (!string.Equals(draft.SourceFingerprint, ProjectFingerprintUx2NavM(project), StringComparison.Ordinal))
            return FailedCameraAnchorPreviewUx2NavM(
                "Le projet cinématique a changé. Rechargez l’ancre avant l’aperçu.");

        var sourceProject = project;
        var sourceRevision = history.State.Revision;
        var sourceJournalCount = history.Journal.Count;
        if (!TryBuildCameraAnchorCandidateUx2NavM(draft, true, out var command, out var candidate,
                out var candidateArea, out var diagnostics))
            return new(false, false,
                diagnostics.FirstOrDefault()?.Message ?? "L’ancre caméra contient une valeur invalide.",
                string.Empty, BuildCameraAnchorDiffUx2NavM(draft.OriginalAnchor, candidate), diagnostics,
                null, true);

        var result = _setCameraAnchorsUx2NavM.Handle(history.State, command!);
        if (!result.Succeeded)
        {
            var commandDiagnostics = result.Diagnostics.Select(item => new StudioUx2NavMDiagnostic(item.Code,
                $"areas/{draft.AreaId}/runtimePolicies/cinematics/cameraAnchors/{draft.AnchorId:D}",
                HumanCameraAnchorDiagnosticUx2NavM(item.Code, item.Message))).ToArray();
            return new(false, false,
                commandDiagnostics.FirstOrDefault()?.Message ?? "La commande canonique a refusé l’ancre.",
                result.Patch?.Summary ?? string.Empty,
                BuildCameraAnchorDiffUx2NavM(draft.OriginalAnchor, candidate), commandDiagnostics,
                null, true);
        }

        var diff = BuildCameraAnchorDiffUx2NavM(draft.OriginalAnchor, candidate);
        var canApply = diff.Any(item => item.Changed);
        var dryRunPure = ReferenceEquals(sourceProject, Project) && history.State.Revision == sourceRevision &&
                         history.Journal.Count == sourceJournalCount;
        if (canApply && dryRunPure)
        {
            _approvedCameraAnchorUx2NavM = draft;
            _approvedCameraAnchorRevisionUx2NavM = history.State.Revision;
            _approvedCameraAnchorSourceFingerprintUx2NavM = draft.SourceFingerprint;
            _approvedCameraAnchorDraftFingerprintUx2NavM = CameraAnchorDraftFingerprintUx2NavM(draft);
        }
        return new(true, canApply,
            canApply
                ? "Aperçu sec validé · ni caméra live ni document n’a été modifié."
                : "Aperçu sec validé · cette ancre est déjà identique au document canonique.",
            result.Patch?.Summary ?? string.Empty, diff, [],
            BuildFramingPreviewUx2NavM(candidate!, candidateArea!), dryRunPure);
    }

    internal StudioUx2NavMActionResult ApplyApprovedCameraAnchorUx2NavM()
    {
        var history = History;
        var project = Project;
        var draft = _approvedCameraAnchorUx2NavM;
        if (history is null || project is null || draft is null ||
            _approvedCameraAnchorRevisionUx2NavM != history.State.Revision ||
            !string.Equals(_approvedCameraAnchorSourceFingerprintUx2NavM,
                ProjectFingerprintUx2NavM(project), StringComparison.Ordinal) ||
            !string.Equals(_approvedCameraAnchorDraftFingerprintUx2NavM,
                CameraAnchorDraftFingerprintUx2NavM(draft), StringComparison.Ordinal))
        {
            CancelCameraAnchorPreviewUx2NavM();
            return new(false, "Prévisualisez de nouveau cette ancre avant de l’appliquer.");
        }
        if (!TryBuildCameraAnchorCandidateUx2NavM(draft, false, out var command, out _, out _,
                out var diagnostics))
        {
            CancelCameraAnchorPreviewUx2NavM();
            return new(false, diagnostics.FirstOrDefault()?.Message ?? "L’ancre caméra est invalide.");
        }
        var result = _setCameraAnchorsUx2NavM.Handle(history.State, command!);
        CancelCameraAnchorPreviewUx2NavM();
        if (!result.Succeeded)
            return new(false, HumanCameraAnchorDiagnosticUx2NavM(result.Diagnostics.FirstOrDefault()?.Code,
                result.Diagnostics.FirstOrDefault()?.Message));
        history.Record(result);
        InvalidateContentIndexes();
        return new(true, "Ancre caméra appliquée · Ctrl+Z pour annuler, Ctrl+Y pour rétablir.");
    }

    internal void CancelCameraAnchorPreviewUx2NavM()
    {
        _approvedCameraAnchorUx2NavM = null;
        _approvedCameraAnchorRevisionUx2NavM = null;
        _approvedCameraAnchorSourceFingerprintUx2NavM = null;
        _approvedCameraAnchorDraftFingerprintUx2NavM = null;
    }

    internal StudioUx2NavMApprovalSnapshot CaptureCameraAnchorApprovalUx2NavM() => new(
        _approvedCameraAnchorUx2NavM, _approvedCameraAnchorRevisionUx2NavM,
        _approvedCameraAnchorSourceFingerprintUx2NavM, _approvedCameraAnchorDraftFingerprintUx2NavM);

    internal void RestoreCameraAnchorApprovalUx2NavM(StudioUx2NavMApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedCameraAnchorUx2NavM = snapshot.Draft;
        _approvedCameraAnchorRevisionUx2NavM = snapshot.Revision;
        _approvedCameraAnchorSourceFingerprintUx2NavM = snapshot.SourceFingerprint;
        _approvedCameraAnchorDraftFingerprintUx2NavM = snapshot.DraftFingerprint;
    }

    internal void CloseUx2NavM() => CancelCameraAnchorPreviewUx2NavM();

    internal ProjectDefinition? CreateUx2NavMEvidenceProject()
    {
        var project = Project;
        if (project is null) return null;
        if (project.Areas.Any(item => item.RuntimePolicies.Cinematics.CameraAnchors.Count > 0)) return project;
        var area = ActiveArea ?? project.Areas.FirstOrDefault();
        if (area is null) return null;
        var anchor = new CameraAnchorDefinition(StableUx2NavM(area.Id.Value, "evidence-camera-anchor"),
            "Plan caméra isolé",
            new(new(0, 6, 0), QuaternionValue.Identity, Vector3Value.One),
            new(0, 1.5f, 0), CameraFraming.Medium, 55, 2_000, .08f,
            ["camera", "evidence"], Ux2NavMEvidenceProvenance);
        var current = area.RuntimePolicies.Cinematics;
        var cinematics = new AreaCinematicProfile(current.Id, current.SchemaVersion, [anchor], current.Provenance);
        var policies = area.RuntimePolicies;
        var updatedPolicies = new AreaRuntimePolicyProfile(policies.Id, policies.SchemaVersion,
            policies.Heartbeats, policies.Rest, policies.Provenance, cinematics, policies.Rules,
            policies.Experience);
        var updatedArea = area.WithRuntimePolicies(updatedPolicies);
        return new(project.Id, project.Title, project.Module, project.Blueprints,
            project.Areas.Select(item => item.Id == area.Id ? updatedArea : item).ToArray(),
            project.Dialogues, project.Quests);
    }

    private bool TryBuildCameraAnchorCandidateUx2NavM(
        StudioUx2NavMDraft draft,
        bool dryRun,
        out SetCinematicProfilesCommand? command,
        out CameraAnchorDefinition? candidate,
        out AreaDefinition? candidateArea,
        out IReadOnlyList<StudioUx2NavMDiagnostic> diagnostics)
    {
        command = null;
        candidate = null;
        candidateArea = null;
        diagnostics = [];
        var history = History;
        var project = Project;
        if (history is null || project is null || draft.BaseRevision != history.State.Revision)
        {
            diagnostics = [new("command.revision_conflict", "project/revision",
                "Le projet a changé. Rechargez l’ancre.")];
            return false;
        }
        if (!string.Equals(draft.SourceFingerprint, ProjectFingerprintUx2NavM(project), StringComparison.Ordinal))
        {
            diagnostics = [new("command.source_changed", "runtimePolicies/cinematics",
                "Les cinématiques ont changé. Rechargez l’ancre.")];
            return false;
        }
        var area = project.Areas.SingleOrDefault(item => item.Id == draft.AreaId);
        if (area is null)
        {
            diagnostics = [new("cinematic.area_missing", $"areas/{draft.AreaId}",
                "La zone sélectionnée n’existe plus.")];
            return false;
        }
        var current = area.RuntimePolicies.Cinematics.CameraAnchors.SingleOrDefault(item =>
            item.Id == draft.AnchorId);
        if (draft.IsNew ? current is not null : current is null)
        {
            diagnostics = [new("cinematic.camera_anchor_stale",
                $"areas/{area.Id}/runtimePolicies/cinematics/cameraAnchors/{draft.AnchorId:D}",
                "La liste des ancres a changé. Rechargez la zone.")];
            return false;
        }
        if (string.IsNullOrWhiteSpace(draft.Name) || string.IsNullOrWhiteSpace(draft.Provenance))
        {
            diagnostics = [new("cinematic.camera_anchor_identity_invalid",
                $"areas/{area.Id}/runtimePolicies/cinematics/cameraAnchors/{draft.AnchorId:D}",
                "Donnez un nom lisible à l’ancre caméra.")];
            return false;
        }
        if (area.RuntimePolicies.Cinematics.CameraAnchors.Any(item => item.Id != draft.AnchorId &&
            string.Equals(item.Name, draft.Name.Trim(), StringComparison.Ordinal)))
        {
            diagnostics = [new("cinematic.camera_anchor_name_duplicate",
                $"areas/{area.Id}/runtimePolicies/cinematics/cameraAnchors",
                "Une autre ancre de cette zone porte déjà ce nom.")];
            return false;
        }
        if (!TryFloatUx2NavM(draft.PositionX, out var positionX) ||
            !TryFloatUx2NavM(draft.PositionY, out var positionY) ||
            !TryFloatUx2NavM(draft.PositionZ, out var positionZ) ||
            !TryFloatUx2NavM(draft.RotationX, out var rotationX) ||
            !TryFloatUx2NavM(draft.RotationY, out var rotationY) ||
            !TryFloatUx2NavM(draft.RotationZ, out var rotationZ) ||
            !TryFloatUx2NavM(draft.RotationW, out var rotationW) ||
            !TryFloatUx2NavM(draft.ScaleX, out var scaleX) ||
            !TryFloatUx2NavM(draft.ScaleY, out var scaleY) ||
            !TryFloatUx2NavM(draft.ScaleZ, out var scaleZ) ||
            !TryFloatUx2NavM(draft.LookAtX, out var lookAtX) ||
            !TryFloatUx2NavM(draft.LookAtY, out var lookAtY) ||
            !TryFloatUx2NavM(draft.LookAtZ, out var lookAtZ) ||
            scaleX <= 0 || scaleY <= 0 || scaleZ <= 0 ||
            rotationX == 0 && rotationY == 0 && rotationZ == 0 && rotationW == 0)
        {
            diagnostics = [new("cinematic.camera_anchor_transform_invalid",
                $"areas/{area.Id}/runtimePolicies/cinematics/cameraAnchors/{draft.AnchorId:D}/transform",
                "Position, rotation, échelle et cible doivent être finies ; l’échelle reste positive et la rotation non nulle.")];
            return false;
        }
        if (!TryFloatUx2NavM(draft.FieldOfViewDegrees, out var fieldOfView) ||
            !TryFloatUx2NavM(draft.SafeAreaMargin, out var safeArea) ||
            fieldOfView is < 1 or > 179 || draft.DefaultDurationMilliseconds is < 1 or > 86_400_000 ||
            safeArea is < 0 or > .45f || !Enum.IsDefined(draft.Framing) ||
            draft.Tags.Any(string.IsNullOrWhiteSpace))
        {
            diagnostics = [new("cinematic.camera_anchor_parameters_invalid",
                $"areas/{area.Id}/runtimePolicies/cinematics/cameraAnchors/{draft.AnchorId:D}",
                "Le champ de vision, la durée, le cadre sûr et les tags dépassent les limites du contrat.")];
            return false;
        }

        try
        {
            var transform = new TransformValue(new(positionX, positionY, positionZ),
                new(rotationX, rotationY, rotationZ, rotationW), new(scaleX, scaleY, scaleZ));
            var anchor = new CameraAnchorDefinition(draft.AnchorId, draft.Name, transform,
                new(lookAtX, lookAtY, lookAtZ), draft.Framing, fieldOfView,
                draft.DefaultDurationMilliseconds, safeArea,
                draft.Tags.Select(item => item.Trim()).Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal).ToArray(), draft.Provenance);
            candidate = anchor;
            var sourceProfile = area.RuntimePolicies.Cinematics;
            var anchors = draft.IsNew
                ? sourceProfile.CameraAnchors.Append(anchor).ToArray()
                : sourceProfile.CameraAnchors.Select(item => item.Id == anchor.Id ? anchor : item).ToArray();
            var areaProfile = new AreaCinematicProfile(sourceProfile.Id, sourceProfile.SchemaVersion,
                anchors, sourceProfile.Provenance);
            var moduleProfile = project.Module.RuntimePolicies.Cinematics;
            var previewProject = ProjectWithCinematicsUx2NavM(project, area, moduleProfile, areaProfile);
            candidateArea = previewProject.Areas.Single(item => item.Id == area.Id);
            var issues = CinematicValidator.Validate(previewProject)
                .Where(item => item.Path.StartsWith($"areas/{area.Id}/runtimePolicies/cinematics",
                    StringComparison.Ordinal))
                .DistinctBy(item => (item.Code, item.Path))
                .Select(item => new StudioUx2NavMDiagnostic(item.Code, item.Path,
                    HumanCameraAnchorDiagnosticUx2NavM(item.Code, item.Message)))
                .ToArray();
            if (issues.Length > 0)
            {
                diagnostics = issues;
                return false;
            }
            command = new(CommandId.From(Guid.NewGuid()), history.State.Revision, moduleProfile,
                area.Id, areaProfile, dryRun);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            diagnostics = [new("cinematic.camera_anchor_invalid",
                $"areas/{area.Id}/runtimePolicies/cinematics/cameraAnchors/{draft.AnchorId:D}",
                HumanCameraAnchorDiagnosticUx2NavM("cinematic.camera_anchor_invalid", exception.Message))];
            return false;
        }
    }

    private static ProjectDefinition ProjectWithCinematicsUx2NavM(
        ProjectDefinition project,
        AreaDefinition area,
        ModuleCinematicProfile moduleProfile,
        AreaCinematicProfile areaProfile)
    {
        var previousModule = project.Module.RuntimePolicies;
        var modulePolicies = new ModuleRuntimePolicyProfile(previousModule.Id, previousModule.SchemaVersion,
            previousModule.Heartbeats, previousModule.Rest, previousModule.SequenceAbort,
            previousModule.Provenance, moduleProfile, previousModule.Gameplay, previousModule.Rules,
            previousModule.Extensions, previousModule.GameplayEvents, previousModule.World,
            previousModule.LifecycleEvents);
        var previousArea = area.RuntimePolicies;
        var areaPolicies = new AreaRuntimePolicyProfile(previousArea.Id, previousArea.SchemaVersion,
            previousArea.Heartbeats, previousArea.Rest, previousArea.Provenance, areaProfile,
            previousArea.Rules, previousArea.Experience);
        return new(project.Id, project.Title, project.Module.WithRuntimePolicies(modulePolicies),
            project.Blueprints,
            project.Areas.Select(item => item.Id == area.Id ? area.WithRuntimePolicies(areaPolicies) : item)
                .ToArray(), project.Dialogues, project.Quests);
    }

    private static IReadOnlyList<StudioUx2NavMDiff> BuildCameraAnchorDiffUx2NavM(
        CameraAnchorDefinition? before,
        CameraAnchorDefinition? after)
    {
        if (after is null) return [];
        return
        [
            DiffUx2NavM("Identité", before?.Name ?? "Nouvelle ancre", after.Name),
            DiffUx2NavM("Position", PositionSummaryUx2NavM(before), PositionSummaryUx2NavM(after)),
            DiffUx2NavM("Rotation et échelle", TransformSummaryUx2NavM(before), TransformSummaryUx2NavM(after)),
            DiffUx2NavM("Cible regardée", LookAtSummaryUx2NavM(before), LookAtSummaryUx2NavM(after)),
            DiffUx2NavM("Cadrage et lentille", FramingSummaryUx2NavM(before), FramingSummaryUx2NavM(after)),
            DiffUx2NavM("Durée et cadre sûr", TimingSummaryUx2NavM(before), TimingSummaryUx2NavM(after)),
            DiffUx2NavM("Tags", TagSummaryUx2NavM(before), TagSummaryUx2NavM(after)),
        ];
    }

    private static StudioUx2NavMDiff DiffUx2NavM(string section, string before, string after) =>
        new(section, before, after, !string.Equals(before, after, StringComparison.Ordinal));

    private static StudioUx2NavMFramingPreview BuildFramingPreviewUx2NavM(
        CameraAnchorDefinition anchor,
        AreaDefinition area)
    {
        var position = anchor.Transform.Position;
        var lookAt = anchor.LookAt;
        var dx = lookAt.X - position.X;
        var dy = lookAt.Y - position.Y;
        var dz = lookAt.Z - position.Z;
        var distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        var direction = distance <= double.Epsilon
            ? "cible confondue avec la caméra"
            : $"direction ({dx / distance:0.###}, {dy / distance:0.###}, {dz / distance:0.###})";
        var safeContent = (1 - anchor.SafeAreaMargin * 2) * 100;
        var stable = anchor.Id.ToString("N");
        var overlay = new StudioNavigationOverlaySnapshot(
            [
                new($"camera:{stable}", $"Caméra · {anchor.Name}",
                    StudioNavigationOverlaySemantic.Anchor, PointUx2NavM(position), true),
                new($"look-at:{stable}", $"Cible · {anchor.Name}",
                    StudioNavigationOverlaySemantic.Anchor, PointUx2NavM(lookAt), false),
            ],
            [new($"view:{stable}", $"Ligne de visée · {anchor.Name}",
                StudioNavigationOverlaySemantic.Route, PointUx2NavM(position), PointUx2NavM(lookAt),
                true, true)], []);
        return new(
            $"{anchor.Name} · position ({position.X:0.###}, {position.Y:0.###}, {position.Z:0.###}) · " +
            $"zone {area.DisplayName}",
            $"cible ({lookAt.X:0.###}, {lookAt.Y:0.###}, {lookAt.Z:0.###}) · " +
            $"distance {distance:0.###} · {direction}",
            HumanFramingUx2NavM(anchor.Framing),
            $"champ vertical {anchor.FieldOfViewDegrees:0.##}°",
            $"durée suggérée {anchor.DefaultDurationMilliseconds} ms",
            $"marge {anchor.SafeAreaMargin * 100:0.##}% · contenu sûr {safeContent:0.##}% × {safeContent:0.##}%",
            "Le runtime existant consomme position, champ de vision et look-at lorsqu’une séquence référence cette ancre. " +
            "Cadrage, durée, cadre sûr et tags restent des données canoniques d’auteur.",
            "Aperçu géométrique seulement : aucune caméra live, séquence, contrôle joueur, interpolation, rendu ou réseau n’est exécuté.",
            overlay);
    }

    private static StudioNavigationPoint PointUx2NavM(Vector3Value value) => new(value.X, value.Y, value.Z);

    private static StudioUx2NavMPreview FailedCameraAnchorPreviewUx2NavM(string message) =>
        new(false, false, message, string.Empty, [], [], null, true);

    private static string PositionSummaryUx2NavM(CameraAnchorDefinition? anchor) => anchor is null
        ? "Absente"
        : VectorSummaryUx2NavM(anchor.Transform.Position);

    private static string TransformSummaryUx2NavM(CameraAnchorDefinition? anchor) => anchor is null
        ? "Absente"
        : $"rotation {QuaternionSummaryUx2NavM(anchor.Transform.Rotation)} · " +
          $"échelle {VectorSummaryUx2NavM(anchor.Transform.Scale)}";

    private static string LookAtSummaryUx2NavM(CameraAnchorDefinition? anchor) => anchor is null
        ? "Absente"
        : VectorSummaryUx2NavM(anchor.LookAt);

    private static string FramingSummaryUx2NavM(CameraAnchorDefinition? anchor) => anchor is null
        ? "Absent"
        : $"{HumanFramingUx2NavM(anchor.Framing)} · champ {NumberUx2NavM(anchor.FieldOfViewDegrees)}°";

    private static string TimingSummaryUx2NavM(CameraAnchorDefinition? anchor) => anchor is null
        ? "Absent"
        : $"{anchor.DefaultDurationMilliseconds} ms · marge {NumberUx2NavM(anchor.SafeAreaMargin * 100)}%";

    private static string TagSummaryUx2NavM(CameraAnchorDefinition? anchor) => anchor is null
        ? "Aucun"
        : anchor.Tags.Count == 0 ? "Aucun" : string.Join(", ", anchor.Tags);

    private static string VectorSummaryUx2NavM(Vector3Value value) =>
        $"({NumberUx2NavM(value.X)}, {NumberUx2NavM(value.Y)}, {NumberUx2NavM(value.Z)})";

    private static string QuaternionSummaryUx2NavM(QuaternionValue value) =>
        $"({NumberUx2NavM(value.X)}, {NumberUx2NavM(value.Y)}, {NumberUx2NavM(value.Z)}, {NumberUx2NavM(value.W)})";

    private static string NumberUx2NavM(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    internal static string HumanCameraAnchorDiagnosticUx2NavM(string? code, string? message = null) => code switch
    {
        CinematicValidator.CameraAnchorBoundsCode =>
            "La position X/Z de l’ancre doit rester dans les limites de la zone.",
        CinematicValidator.AreaIdentityCode =>
            "Le profil caméra doit appartenir à la zone sélectionnée.",
        CinematicValidator.MissingCameraAnchorCode =>
            "Une séquence référence une ancre caméra qui n’existe plus.",
        "cinematic.camera_anchor_transform_invalid" =>
            "Position, rotation, échelle et cible doivent être finies ; l’échelle reste positive et la rotation non nulle.",
        "cinematic.camera_anchor_parameters_invalid" =>
            "Le champ de vision, la durée, le cadre sûr et les tags dépassent les limites du contrat.",
        "cinematic.camera_anchor_name_duplicate" =>
            "Une autre ancre de cette zone porte déjà ce nom.",
        "cinematic.camera_anchor_identity_invalid" => "Donnez un nom lisible à l’ancre caméra.",
        "cinematic.area_missing" => "La zone sélectionnée n’existe plus.",
        "command.revision_conflict" or "command.source_changed" =>
            "Le projet a changé. Rechargez l’ancre et prévisualisez à nouveau.",
        null or "" => "La commande cinématique canonique a refusé la modification.",
        _ when !string.IsNullOrWhiteSpace(message) && message.Contains("bounds", StringComparison.OrdinalIgnoreCase) =>
            "L’ancre doit rester dans les limites de la zone.",
        _ => "L’ancre ne respecte pas encore le contrat cinématique canonique.",
    };

    private static string ProjectFingerprintUx2NavM(ProjectDefinition project)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteFingerprintFieldUx2NavM(writer, "project-json");
        WriteFingerprintFieldUx2NavM(writer, ProjectJsonSerializer.Serialize(project));
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static string CameraAnchorDraftFingerprintUx2NavM(StudioUx2NavMDraft draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteFingerprintFieldUx2NavM(writer, "ux2-nav-m-draft-v1");
        WriteFingerprintFieldUx2NavM(writer, draft.AreaId.ToString());
        writer.Write(draft.BaseRevision.Value);
        WriteFingerprintFieldUx2NavM(writer, draft.SourceFingerprint);
        WriteFingerprintFieldUx2NavM(writer, draft.AnchorId.ToString("D"));
        writer.Write(draft.IsNew);
        WriteFingerprintFieldUx2NavM(writer, draft.Provenance);
        WriteFingerprintFieldUx2NavM(writer, draft.Name);
        writer.Write(draft.PositionX); writer.Write(draft.PositionY); writer.Write(draft.PositionZ);
        writer.Write(draft.RotationX); writer.Write(draft.RotationY); writer.Write(draft.RotationZ);
        writer.Write(draft.RotationW);
        writer.Write(draft.ScaleX); writer.Write(draft.ScaleY); writer.Write(draft.ScaleZ);
        writer.Write(draft.LookAtX); writer.Write(draft.LookAtY); writer.Write(draft.LookAtZ);
        writer.Write((int)draft.Framing);
        writer.Write(draft.FieldOfViewDegrees);
        writer.Write(draft.DefaultDurationMilliseconds);
        writer.Write(draft.SafeAreaMargin);
        foreach (var tag in draft.Tags)
        {
            writer.Write((byte)1);
            WriteFingerprintFieldUx2NavM(writer, tag);
        }
        writer.Write((byte)0);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteFingerprintFieldUx2NavM(BinaryWriter writer, string? value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static bool TryFloatUx2NavM(double value, out float result)
    {
        if (!double.IsFinite(value) || value is > float.MaxValue or < -float.MaxValue)
        {
            result = 0;
            return false;
        }
        result = (float)value;
        return float.IsFinite(result);
    }

    private static string HumanFramingUx2NavM(CameraFraming framing) => framing switch
    {
        CameraFraming.Wide => "Plan large",
        CameraFraming.Full => "Plan en pied",
        CameraFraming.Medium => "Plan moyen",
        CameraFraming.CloseUp => "Gros plan",
        CameraFraming.ExtremeCloseUp => "Très gros plan",
        _ => "Cadrage inconnu",
    };

    private static string HumanTokenUx2NavM(string? value) => string.IsNullOrWhiteSpace(value)
        ? "sans identifiant"
        : value.Replace('-', ' ').Replace('_', ' ').Replace('.', ' ').Replace(':', ' ').Trim();

    private static Guid StableUx2NavM(Guid seed, string purpose)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"ux2-nav-m:{seed:D}:{purpose}"));
        return new Guid(bytes[..16]);
    }

    internal StudioUx2NavMSmokeResult RunUx2NavMContractSmoke()
    {
        var fixture = CreateUx2NavMSmokeProject();
        var sourceJson = ProjectJsonSerializer.Serialize(fixture.Project);
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-nav-m-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(tempRoot, fixture.Project).Succeeded || !editing.Open(tempRoot).Succeeded ||
                !editing.SelectArea(fixture.AreaId))
                return FailedUx2NavMSmoke();
            var loadedProject = editing.Project!;
            var editingHistory = editing.History;
            if (editingHistory is null) return FailedUx2NavMSmoke();
            var loadedArea = loadedProject.Areas.Single(item => item.Id == fixture.AreaId);
            var loadedTarget = loadedArea.RuntimePolicies.Cinematics.CameraAnchors.Single(item =>
                item.Id == fixture.TargetId);
            var loadedNeighbor = loadedArea.RuntimePolicies.Cinematics.CameraAnchors.Single(item =>
                item.Id == fixture.NeighborId);
            var explicitSelection = editing.CreateCameraAnchorDraftUx2NavM(null, null) is null &&
                                    editing.CreateCameraAnchorDraftUx2NavM(fixture.AreaId, null) is null;
            var humanLabels = editing.CameraAnchorAreaChoicesUx2NavM.All(item => IsHumanUx2NavMText(item.Label)) &&
                              editing.CameraAnchorChoicesUx2NavM(fixture.AreaId)
                                  .All(item => IsHumanUx2NavMText(item.Label));
            var created = editing.CreateNewCameraAnchorDraftUx2NavM(fixture.AreaId);
            var creationPreview = created is null ? null : editing.PreviewCameraAnchorUx2NavM(created with
            {
                Name = "Plan caméra ajouté",
                PositionX = 1.25,
                PositionY = 7,
                PositionZ = -2,
                LookAtY = 1.75,
            });
            var creationSurface = created is not null && creationPreview is { Succeeded: true, CanApply: true } &&
                                  creationPreview.Framing?.Overlay.Markers.Count == 2 &&
                                  loadedArea.RuntimePolicies.Cinematics.CameraAnchors.Count == 2;
            var draft = editing.CreateCameraAnchorDraftUx2NavM(fixture.AreaId, fixture.TargetId);
            if (draft is null) return FailedUx2NavMSmoke();

            var invalidFinite = editing.PreviewCameraAnchorUx2NavM(draft with { PositionX = double.NaN });
            var invalidRotation = editing.PreviewCameraAnchorUx2NavM(draft with
            { RotationX = 0, RotationY = 0, RotationZ = 0, RotationW = 0 });
            var invalidScale = editing.PreviewCameraAnchorUx2NavM(draft with { ScaleX = 0 });
            var transformValidation = new[] { invalidFinite, invalidRotation, invalidScale }
                .All(item => !item.Succeeded && item.Diagnostics.Any(diagnostic =>
                    diagnostic.Code == "cinematic.camera_anchor_transform_invalid"));
            var invalidBounds = editing.PreviewCameraAnchorUx2NavM(draft with
            { PositionX = loadedArea.Dimensions.Width });
            var boundsValidation = !invalidBounds.Succeeded && invalidBounds.Diagnostics.Any(item =>
                item.Code == CinematicValidator.CameraAnchorBoundsCode);
            var invalidFov = editing.PreviewCameraAnchorUx2NavM(draft with { FieldOfViewDegrees = 180 });
            var invalidDuration = editing.PreviewCameraAnchorUx2NavM(draft with
            { DefaultDurationMilliseconds = 0 });
            var invalidSafe = editing.PreviewCameraAnchorUx2NavM(draft with { SafeAreaMargin = .5 });
            var invalidTags = editing.PreviewCameraAnchorUx2NavM(draft with { Tags = [""] });
            var invalidFraming = editing.PreviewCameraAnchorUx2NavM(draft with { Framing = (CameraFraming)999 });
            var lensTimingSafeAreaValidation = new[]
                { invalidFov, invalidDuration, invalidSafe, invalidTags, invalidFraming }
                .All(item => !item.Succeeded && item.Diagnostics.Any(diagnostic =>
                    diagnostic.Code == "cinematic.camera_anchor_parameters_invalid"));
            var duplicateName = editing.PreviewCameraAnchorUx2NavM(draft with { Name = loadedNeighbor.Name });
            var duplicateNameValidation = !duplicateName.Succeeded && duplicateName.Diagnostics.Any(item =>
                item.Code == "cinematic.camera_anchor_name_duplicate");

            var staleSource = draft;
            var changedBeforePreview = ApplyUx2NavMIntermediateMutation(editing, fixture.AreaId,
                fixture.NeighborId, 1);
            var staleDraft = editing.PreviewCameraAnchorUx2NavM(staleSource);
            var staleDraftBlocked = changedBeforePreview && !staleDraft.Succeeded && editing.Undo().Succeeded;

            var changed = editing.CreateCameraAnchorDraftUx2NavM(fixture.AreaId, fixture.TargetId)! with
            {
                Name = "Plan d’ouverture révisé",
                PositionX = 2.5,
                PositionY = 8.25,
                PositionZ = -3.75,
                LookAtX = .5,
                LookAtY = 1.75,
                LookAtZ = 1.25,
                Framing = CameraFraming.CloseUp,
                FieldOfViewDegrees = 47.5,
                DefaultDurationMilliseconds = 2_750,
                SafeAreaMargin = .11,
                Tags = ["camera", "opening", "reviewed"],
            };
            var approvalPreview = editing.PreviewCameraAnchorUx2NavM(changed);
            var changedAfterPreview = ApplyUx2NavMIntermediateMutation(editing, fixture.AreaId,
                fixture.NeighborId, 2);
            var staleApply = editing.ApplyApprovedCameraAnchorUx2NavM();
            var staleApprovalBlocked = approvalPreview.Succeeded && changedAfterPreview && !staleApply.Succeeded &&
                                       editing.Undo().Succeeded;

            changed = editing.CreateCameraAnchorDraftUx2NavM(fixture.AreaId, fixture.TargetId)! with
            {
                Name = "Plan d’ouverture révisé",
                PositionX = 2.5,
                PositionY = 8.25,
                PositionZ = -3.75,
                LookAtX = .5,
                LookAtY = 1.75,
                LookAtZ = 1.25,
                Framing = CameraFraming.CloseUp,
                FieldOfViewDegrees = 47.5,
                DefaultDurationMilliseconds = 2_750,
                SafeAreaMargin = .11,
                Tags = ["camera", "opening", "reviewed"],
            };
            var beforeDryRunJson = ProjectJsonSerializer.Serialize(editing.Project!);
            var beforeDryRunJournal = editingHistory.Journal.Count;
            var preview = editing.PreviewCameraAnchorUx2NavM(changed);
            var dryRunPure = preview.Succeeded && preview.CanApply && preview.DryRunPure &&
                             beforeDryRunJson == ProjectJsonSerializer.Serialize(editing.Project!) &&
                             editingHistory.Journal.Count == beforeDryRunJournal;
            var overlayPreview = preview.Framing?.Overlay is { Markers.Count: 2, Segments.Count: 1 };
            var runtimePreview = preview.Framing is { } framing &&
                                 framing.RuntimeConsumption.Contains("position", StringComparison.OrdinalIgnoreCase) &&
                                 framing.RuntimeConsumption.Contains("champ de vision", StringComparison.OrdinalIgnoreCase) &&
                                 framing.RuntimeConsumption.Contains("look-at", StringComparison.OrdinalIgnoreCase) &&
                                 framing.Boundary.Contains("aucune caméra live", StringComparison.OrdinalIgnoreCase) &&
                                 framing.Boundary.Contains("séquence", StringComparison.OrdinalIgnoreCase) &&
                                 framing.Boundary.Contains("réseau", StringComparison.OrdinalIgnoreCase);
            var applied = editing.ApplyApprovedCameraAnchorUx2NavM();
            if (!applied.Succeeded) return FailedUx2NavMSmoke();
            var appliedProject = editing.Project!;
            var appliedArea = appliedProject.Areas.Single(item => item.Id == fixture.AreaId);
            var appliedTarget = appliedArea.RuntimePolicies.Cinematics.CameraAnchors.Single(item =>
                item.Id == fixture.TargetId);
            var appliedNeighbor = appliedArea.RuntimePolicies.Cinematics.CameraAnchors.Single(item =>
                item.Id == fixture.NeighborId);
            var moduleCinematicsPreserved = ReferenceEquals(loadedProject.Module.RuntimePolicies.Cinematics,
                appliedProject.Module.RuntimePolicies.Cinematics) &&
                loadedProject.Module.RuntimePolicies.Heartbeats.SequenceEqual(
                    appliedProject.Module.RuntimePolicies.Heartbeats) &&
                ReferenceEquals(loadedProject.Module.RuntimePolicies.Rest,
                    appliedProject.Module.RuntimePolicies.Rest) &&
                ReferenceEquals(loadedProject.Module.RuntimePolicies.SequenceAbort,
                    appliedProject.Module.RuntimePolicies.SequenceAbort) &&
                ReferenceEquals(loadedProject.Module.RuntimePolicies.Gameplay,
                    appliedProject.Module.RuntimePolicies.Gameplay) &&
                ReferenceEquals(loadedProject.Module.RuntimePolicies.Rules,
                    appliedProject.Module.RuntimePolicies.Rules) &&
                ReferenceEquals(loadedProject.Module.RuntimePolicies.Extensions,
                    appliedProject.Module.RuntimePolicies.Extensions) &&
                ReferenceEquals(loadedProject.Module.RuntimePolicies.GameplayEvents,
                    appliedProject.Module.RuntimePolicies.GameplayEvents) &&
                ReferenceEquals(loadedProject.Module.RuntimePolicies.World,
                    appliedProject.Module.RuntimePolicies.World) &&
                ReferenceEquals(loadedProject.Module.RuntimePolicies.LifecycleEvents,
                    appliedProject.Module.RuntimePolicies.LifecycleEvents);
            var expectedAreaPolicies = new AreaRuntimePolicyProfile(loadedArea.RuntimePolicies.Id,
                loadedArea.RuntimePolicies.SchemaVersion, loadedArea.RuntimePolicies.Heartbeats,
                loadedArea.RuntimePolicies.Rest, "cinematic-command", appliedArea.RuntimePolicies.Cinematics,
                loadedArea.RuntimePolicies.Rules, loadedArea.RuntimePolicies.Experience);
            var expectedArea = loadedArea.WithRuntimePolicies(expectedAreaPolicies);
            var targetAreaNeighborsPreserved = string.Equals(AreaJsonSerializer.Serialize(expectedArea),
                AreaJsonSerializer.Serialize(appliedArea), StringComparison.Ordinal);
            var otherAnchorsPreserved = ReferenceEquals(loadedNeighbor, appliedNeighbor) &&
                appliedTarget.Id == loadedTarget.Id && appliedTarget.Provenance == loadedTarget.Provenance;
            var otherAreasPreserved = loadedProject.Areas.Count > 1 &&
                loadedProject.Areas.Where(item => item.Id != fixture.AreaId)
                .SequenceEqual(appliedProject.Areas.Where(item => item.Id != fixture.AreaId));
            var projectNeighborsPreserved = loadedProject.Blueprints.SequenceEqual(appliedProject.Blueprints) &&
                loadedProject.Dialogues.SequenceEqual(appliedProject.Dialogues) &&
                loadedProject.Quests.SequenceEqual(appliedProject.Quests) &&
                ReferenceEquals(loadedProject.Module.Factions, appliedProject.Module.Factions) &&
                ReferenceEquals(loadedProject.Module.WorldFeatures, appliedProject.Module.WorldFeatures);

            var undo = editing.Undo();
            var undoneProject = editing.Project!;
            var undone = undoneProject.Areas.Single(item => item.Id == fixture.AreaId)
                .RuntimePolicies.Cinematics.CameraAnchors.Single(item => item.Id == fixture.TargetId);
            var redo = editing.Redo();
            var redoneProject = editing.Project!;
            var redone = redoneProject.Areas.Single(item => item.Id == fixture.AreaId)
                .RuntimePolicies.Cinematics.CameraAnchors.Single(item => item.Id == fixture.TargetId);
            var undoRedo = undo.Succeeded && redo.Succeeded &&
                CameraAnchorJsonUx2NavM(undone, undoneProject, fixture.AreaId) ==
                CameraAnchorJsonUx2NavM(loadedTarget, loadedProject, fixture.AreaId) &&
                CameraAnchorJsonUx2NavM(redone, redoneProject, fixture.AreaId) ==
                CameraAnchorJsonUx2NavM(appliedTarget, appliedProject, fixture.AreaId);
            var saved = editing.Save();
            var opened = saved.Succeeded && reopened.Open(tempRoot).Succeeded;
            var persisted = opened ? reopened.Project!.Areas.Single(item => item.Id == fixture.AreaId)
                .RuntimePolicies.Cinematics.CameraAnchors.Single(item => item.Id == fixture.TargetId) : null;
            var persistence = persisted is not null &&
                CameraAnchorJsonUx2NavM(persisted, reopened.Project!, fixture.AreaId) ==
                CameraAnchorJsonUx2NavM(appliedTarget, appliedProject, fixture.AreaId) &&
                ProjectJsonSerializer.Serialize(editing.Project!) == ProjectJsonSerializer.Serialize(reopened.Project!);
            reopened.Close();
            var canonicalCommand = editingHistory.Journal.Count >= 3 &&
                                   editingHistory.Journal.All(item => item.CommandName == "set_cinematic_profiles");
            var structuredDiagnostics = new[]
                { invalidFinite, invalidRotation, invalidScale, invalidBounds, invalidFov,
                    invalidDuration, invalidSafe, invalidTags, invalidFraming, duplicateName }
                .SelectMany(item => item.Diagnostics)
                .All(item => !string.IsNullOrWhiteSpace(item.Code) && !string.IsNullOrWhiteSpace(item.Path));
            var sourceUntouched = sourceJson == ProjectJsonSerializer.Serialize(fixture.Project);
            var humanText = humanLabels && preview.Framing is { } humanFraming &&
                            IsHumanUx2NavMText(humanFraming.CameraPose) &&
                            IsHumanUx2NavMText(humanFraming.LookAt) &&
                            IsHumanUx2NavMText(humanFraming.Framing);
            var succeeded = explicitSelection && creationSurface && humanLabels && transformValidation &&
                            boundsValidation && lensTimingSafeAreaValidation && duplicateNameValidation &&
                            overlayPreview && runtimePreview && dryRunPure && staleDraftBlocked &&
                            staleApprovalBlocked && canonicalCommand && undoRedo && persistence &&
                            moduleCinematicsPreserved && targetAreaNeighborsPreserved && otherAnchorsPreserved &&
                            otherAreasPreserved && projectNeighborsPreserved && structuredDiagnostics &&
                            sourceUntouched && humanText;
            return new(succeeded, explicitSelection, creationSurface, humanLabels, transformValidation,
                boundsValidation, lensTimingSafeAreaValidation, duplicateNameValidation, overlayPreview,
                runtimePreview, dryRunPure, staleDraftBlocked, staleApprovalBlocked, canonicalCommand,
                undoRedo, persistence, moduleCinematicsPreserved, targetAreaNeighborsPreserved,
                otherAnchorsPreserved, otherAreasPreserved, projectNeighborsPreserved,
                structuredDiagnostics, sourceUntouched, humanText);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_NAV_M_CONTRACT_FAILED exception={exception.Message}");
            return FailedUx2NavMSmoke();
        }
        finally
        {
            reopened.Close();
            editing.Close();
            try
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_NAV_M_CLEANUP_FAILED " + exception.Message);
            }
        }
    }

    private static bool ApplyUx2NavMIntermediateMutation(
        StudioSessionController session,
        AreaId areaId,
        Guid anchorId,
        float delta)
    {
        var history = session.History;
        var project = session.Project;
        var area = project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (history is null || project is null || area is null) return false;
        var source = area.RuntimePolicies.Cinematics;
        var neighbor = source.CameraAnchors.Single(item => item.Id == anchorId);
        var updated = new CameraAnchorDefinition(neighbor.Id, neighbor.Name, neighbor.Transform, neighbor.LookAt,
            neighbor.Framing, neighbor.FieldOfViewDegrees + delta, neighbor.DefaultDurationMilliseconds,
            neighbor.SafeAreaMargin, neighbor.Tags, neighbor.Provenance);
        var profile = new AreaCinematicProfile(source.Id, source.SchemaVersion,
            source.CameraAnchors.Select(item => item.Id == anchorId ? updated : item).ToArray(), source.Provenance);
        var result = new SetCinematicProfilesHandler(new SystemClock()).Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision,
                project.Module.RuntimePolicies.Cinematics, area.Id, profile, false));
        if (!result.Succeeded) return false;
        history.Record(result);
        return true;
    }

    private static Ux2NavMSmokeFixture CreateUx2NavMSmokeProject()
    {
        var source = SampleModuleAuthoringService.Create().State.Project;
        var area = source.Areas.Single();
        var target = new CameraAnchorDefinition(StableUx2NavM(area.Id.Value, "smoke-target"),
            "Plan d’ouverture", new(new(-2, 7, -5), QuaternionValue.Identity, Vector3Value.One),
            new(0, 1.5f, 0), CameraFraming.Medium, 55, 2_000, .08f,
            ["camera", "opening"], "test:ux2-nav-m");
        var neighbor = new CameraAnchorDefinition(StableUx2NavM(area.Id.Value, "smoke-neighbor"),
            "Plan de sortie", new(new(3, 5, 4), QuaternionValue.Identity, Vector3Value.One),
            new(0, 1, 0), CameraFraming.Wide, 62, 1_500, .05f,
            ["camera", "exit"], "test:ux2-nav-m-neighbor");
        var sourcePolicies = area.RuntimePolicies;
        var profile = new AreaCinematicProfile(sourcePolicies.Cinematics.Id,
            sourcePolicies.Cinematics.SchemaVersion, [target, neighbor], sourcePolicies.Cinematics.Provenance);
        var policies = new AreaRuntimePolicyProfile(sourcePolicies.Id, sourcePolicies.SchemaVersion,
            sourcePolicies.Heartbeats, sourcePolicies.Rest, sourcePolicies.Provenance,
            profile, sourcePolicies.Rules, sourcePolicies.Experience);
        var authoredArea = area.WithRuntimePolicies(policies);
        var untouchedArea = new AreaDefinition(
            AreaId.From(StableUx2NavM(area.Id.Value, "smoke-untouched-area")),
            "zone-temoin-camera",
            "Zone témoin caméra",
            []);
        var project = new ProjectDefinition(source.Id, "Studio UX2-NAV-M", source.Module, source.Blueprints,
            [authoredArea, untouchedArea], source.Dialogues, source.Quests);
        return new(project, authoredArea.Id, target.Id, neighbor.Id);
    }

    private static string CameraAnchorJsonUx2NavM(
        CameraAnchorDefinition anchor,
        ProjectDefinition project,
        AreaId areaId)
    {
        var area = project.Areas.Single(item => item.Id == areaId);
        var source = area.RuntimePolicies;
        var profile = new AreaCinematicProfile(source.Cinematics.Id, source.Cinematics.SchemaVersion,
            [anchor], source.Cinematics.Provenance);
        var policies = new AreaRuntimePolicyProfile(source.Id, source.SchemaVersion, source.Heartbeats,
            source.Rest, source.Provenance, profile, source.Rules, source.Experience);
        var candidate = new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
            project.Areas.Select(item => item.Id == areaId ? item.WithRuntimePolicies(policies) : item).ToArray(),
            project.Dialogues, project.Quests);
        return ProjectJsonSerializer.Serialize(candidate);
    }

    private static bool IsHumanUx2NavMText(string value) =>
        !string.IsNullOrWhiteSpace(value) && !value.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !value.Contains("runtimePolicies/cinematics", StringComparison.Ordinal) &&
        !value.Split([' ', '·', '/', ':', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Any(token => Guid.TryParse(token, out _));

    private static StudioUx2NavMSmokeResult FailedUx2NavMSmoke() => new(
        false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false, false);

    private sealed record Ux2NavMSmokeFixture(
        ProjectDefinition Project,
        AreaId AreaId,
        Guid TargetId,
        Guid NeighborId);
}
