using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Navigation;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Core.Navigation;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2NavDProvenance = "studio:ux2-nav-d:patrol-arrival";
    private const string Ux2NavDEvidenceProvenance = "studio:ux2-nav-d:evidence";

    private readonly SetNavigationTopologyHandler _setNavigationTopologyUx2NavD = new(new SystemClock());
    private StudioUx2NavDDraft? _approvedPatrolUx2NavD;
    private ProjectRevision? _approvedPatrolRevisionUx2NavD;
    private string? _approvedPatrolFingerprintUx2NavD;

    internal IReadOnlyList<StudioUx2NavDAreaChoice> PatrolAreaChoicesUx2NavD
    {
        get
        {
            var project = Project;
            if (project is null)
            {
                return [];
            }
            var duplicateNames = project.Areas.GroupBy(item => HumanUx2NavD(item.DisplayName, "Zone"),
                    StringComparer.CurrentCultureIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(),
                    StringComparer.CurrentCultureIgnoreCase);
            return project.Areas.Select(item =>
                {
                    var name = HumanUx2NavD(item.DisplayName, "Zone");
                    var label = duplicateNames[name] > 1
                        ? $"{name} · {HumanUx2NavD(item.Slug, "zone")}" : name;
                    var evidence = item.Navigation.Waypoints.PatrolRoutes.Any(route =>
                                           string.Equals(route.Provenance, Ux2NavDEvidenceProvenance,
                                               StringComparison.Ordinal)) ||
                                       item.Navigation.Waypoints.Waypoints.Any(waypoint =>
                                           string.Equals(waypoint.Provenance, Ux2NavDEvidenceProvenance,
                                               StringComparison.Ordinal));
                    return new StudioUx2NavDAreaChoice(item.Id, label, evidence);
                })
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal IReadOnlyList<StudioUx2NavDRouteChoice> PatrolRouteChoicesUx2NavD(AreaId? areaId)
    {
        var area = areaId is null ? null : Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (area is null)
        {
            return [];
        }
        return area.Navigation.Waypoints.PatrolRoutes.Select((route, index) =>
                new StudioUx2NavDRouteChoice(route.Id,
                    $"Patrouille {index + 1} · {HumanTraversalUx2NavD(route.Traversal)} · " +
                    $"{route.Points.Count} points",
                    string.Equals(route.Provenance, Ux2NavDEvidenceProvenance,
                        StringComparison.Ordinal)))
            .ToArray();
    }

    internal IReadOnlyList<StudioUx2NavDWaypointChoice> ArrivalWaypointChoicesUx2NavD(AreaId? areaId)
    {
        var area = areaId is null ? null : Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (area is null)
        {
            return [];
        }
        return area.Navigation.Waypoints.Waypoints.Select(item =>
                new StudioUx2NavDWaypointChoice(item.Id,
                    $"{HumanUx2NavD(item.Name, "Repère")} · " +
                    (item.Arrival is null ? "sans arrivée" : "arrivée configurée"),
                    item.Arrival is not null,
                    string.Equals(item.Provenance, Ux2NavDEvidenceProvenance,
                        StringComparison.Ordinal)))
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.WaypointId)
            .ToArray();
    }

    internal IReadOnlyList<StudioUx2NavDArrivalTargetChoice> ArrivalTargetChoicesUx2NavD(
        AreaId? areaId, StudioUx2NavDArrivalDraft? arrival)
    {
        var area = areaId is null ? null : Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (area is null)
        {
            return [];
        }
        var result = new List<StudioUx2NavDArrivalTargetChoice>();
        if (arrival is { TargetIsProtected: true } && !string.IsNullOrWhiteSpace(arrival.TargetId))
        {
            result.Add(new(arrival.TargetId, "Cible existante protégée", true));
        }
        else if (arrival is { TargetId.Length: > 0 } &&
                 area.Navigation.Waypoints.Waypoints.All(item =>
                     !string.Equals(item.Name, arrival.TargetId, StringComparison.Ordinal)))
        {
            result.Add(new(arrival.TargetId, HumanUx2NavD(arrival.TargetId, "Cible existante"), false));
        }
        foreach (var waypoint in area.Navigation.Waypoints.Waypoints
                     .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                     .ThenBy(item => item.Id))
        {
            if (result.Any(item => string.Equals(item.Value, waypoint.Name, StringComparison.Ordinal)))
            {
                continue;
            }
            result.Add(new(waypoint.Name, HumanUx2NavD(waypoint.Name, "Repère"), false));
        }
        return result;
    }

    internal StudioUx2NavDRouteDraft? CreatePatrolRouteDraftUx2NavD(AreaId? areaId, Guid? routeId)
    {
        var area = areaId is null ? null : Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        var route = routeId is null
            ? null
            : area?.Navigation.Waypoints.PatrolRoutes.SingleOrDefault(item => item.Id == routeId.Value);
        return route is null ? null : RouteDraftFromUx2NavD(route);
    }

    internal StudioUx2NavDRouteDraft? CreateNewPatrolRouteDraftUx2NavD(AreaId? areaId)
    {
        var area = areaId is null ? null : Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (area is null)
        {
            return null;
        }
        var halfX = Math.Max(.5f, Math.Min(5f, area.Dimensions.Width / 4f));
        var halfZ = Math.Max(.5f, Math.Min(5f, area.Dimensions.Depth / 4f));
        return new StudioUx2NavDRouteDraft(Guid.NewGuid(), true, null, PatrolTraversalPolicy.Loop,
        [
            new(Guid.NewGuid(), new Vector3Value(-halfX, 0, -halfZ), 0, 0, 2,
                string.Empty, string.Empty, 1),
            new(Guid.NewGuid(), new Vector3Value(halfX, 0, halfZ), 0, 180, 2,
                string.Empty, string.Empty, 1),
        ], string.Empty);
    }

    internal StudioUx2NavDArrivalDraft? CreateArrivalDraftUx2NavD(AreaId? areaId, Guid? waypointId)
    {
        var area = areaId is null ? null : Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        var waypoint = waypointId is null
            ? null
            : area?.Navigation.Waypoints.Waypoints.SingleOrDefault(item => item.Id == waypointId.Value);
        if (area is null || waypoint is null)
        {
            return null;
        }
        var arrival = waypoint.Arrival;
        var target = arrival?.TargetId ?? string.Empty;
        var protectedTarget = !string.IsNullOrWhiteSpace(target) &&
                              area.Navigation.Waypoints.Waypoints.All(item =>
                                  !string.Equals(item.Name, target, StringComparison.Ordinal));
        return new StudioUx2NavDArrivalDraft(waypoint.Id, waypoint, arrival, arrival is not null,
            arrival?.Facing ?? ArrivalFacingMode.Preserve, arrival?.DirectionDegrees ?? 0, target,
            protectedTarget, arrival?.Animation ?? string.Empty,
            arrival?.WaitSeconds ?? 0, arrival?.Interaction ?? string.Empty,
            arrival?.Callback ?? string.Empty);
    }

    internal StudioUx2NavDDraft? ComposePatrolDraftUx2NavD(AreaId? areaId,
        StudioUx2NavDRouteDraft? route, StudioUx2NavDArrivalDraft? arrival)
    {
        var history = History;
        var area = areaId is null ? null : Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (history is null || area is null || route is null && arrival is null)
        {
            return null;
        }
        if (route is not null)
        {
            var currentRoute = area.Navigation.Waypoints.PatrolRoutes.SingleOrDefault(item =>
                item.Id == route.RouteId);
            if (route.IsNew && currentRoute is not null || !route.IsNew &&
                (currentRoute is null || route.Original is null ||
                 !RoutesEquivalentUx2NavD(currentRoute, route.Original)))
            {
                return null;
            }
        }
        if (arrival is not null)
        {
            var currentWaypoint = area.Navigation.Waypoints.Waypoints.SingleOrDefault(item =>
                item.Id == arrival.WaypointId);
            if (currentWaypoint is null || currentWaypoint != arrival.OriginalWaypoint ||
                currentWaypoint.Arrival != arrival.Original)
            {
                return null;
            }
        }
        return new StudioUx2NavDDraft(area.Id, history.State.Revision,
            PatrolSourceFingerprintUx2NavD(area), HumanUx2NavD(area.DisplayName, "Zone"),
            area.Dimensions.Width, area.Dimensions.Depth, route, arrival);
    }

    private static StudioUx2NavDRouteDraft RouteDraftFromUx2NavD(PatrolRoute route) => new(
        route.Id, false, route, route.Traversal,
        route.Points.Select(item => new StudioUx2NavDPointDraft(item.Id, item.Position,
            item.WaitSeconds, item.FacingDegrees, item.Speed, item.Action ?? string.Empty,
            item.BranchCondition ?? string.Empty, item.Weight)).ToArray(), route.ScriptHandler);

    internal StudioUx2NavDPreview PreviewPatrolUx2NavD(StudioUx2NavDDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelPatrolUx2NavD();
        var history = History;
        if (history is null || Project is null)
        {
            return FailedPatrolPreviewUx2NavD("Aucun module n’est ouvert.");
        }
        if (!TryBuildPatrolMutationUx2NavD(draft, true, out var command, out var sourceArea,
                out var candidateArea, out var diagnostics, out var message))
        {
            return FailedPatrolPreviewUx2NavD(message, diagnostics);
        }
        var dryRun = _setNavigationTopologyUx2NavD.Handle(history.State, command!);
        if (!dryRun.Succeeded)
        {
            var commandDiagnostics = dryRun.Diagnostics.Select(item => new StudioUx2NavDDiagnostic(
                item.Code, HumanPathUx2NavD($"navigation/patrouilles/{draft.AreaId}"),
                HumanDiagnosticUx2NavD(item.Message))).ToArray();
            return FailedPatrolPreviewUx2NavD(commandDiagnostics.FirstOrDefault()?.Message ??
                                              "L’aperçu de patrouille a été refusé.",
                commandDiagnostics);
        }

        var builtArea = candidateArea!;
        var candidateRoute = draft.Route is null
            ? null
            : builtArea.Navigation.Waypoints.PatrolRoutes.Single(item =>
                item.Id == draft.Route.RouteId);
        var candidateWaypoint = draft.Arrival is null
            ? null
            : builtArea.Navigation.Waypoints.Waypoints.Single(item =>
                item.Id == draft.Arrival.WaypointId);
        var diff = BuildPatrolDiffUx2NavD(sourceArea!, candidateRoute, candidateWaypoint, draft);
        var overlay = BuildPatrolOverlayUx2NavD(builtArea, candidateRoute, candidateWaypoint);
        var runtimeSteps = BuildRuntimeStepsUx2NavD(candidateRoute,
            builtArea.Navigation.Waypoints.RouteVisualization.CostPerMeter);
        var arrival = candidateWaypoint?.Arrival;
        var authorized = arrival is not null &&
                         WaypointRouteService.ResolveArrival(arrival).AuthorityGranted;
        var denied = arrival is not null &&
                     WaypointRouteService.ResolveArrival(arrival, false) is
                     {
                         AuthorityGranted: false,
                         Animation: null,
                         WaitSeconds: 0,
                         Interaction: null,
                         Callback: null,
                     };
        if (diff.Count > 0)
        {
            _approvedPatrolUx2NavD = draft;
            _approvedPatrolRevisionUx2NavD = history.State.Revision;
            _approvedPatrolFingerprintUx2NavD = PatrolDraftFingerprintUx2NavD(draft);
        }
        var routeSummary = candidateRoute is null
            ? "aucune route éditée"
            : $"{HumanTraversalUx2NavD(candidateRoute.Traversal)} · {candidateRoute.Points.Count} points · " +
              $"{runtimeSteps.Count} étapes simulées · {TraversalOutcomeUx2NavD(candidateRoute)}";
        var arrivalSummary = candidateWaypoint is null
            ? "aucune arrivée éditée"
            : candidateWaypoint.Arrival is null
                ? $"{HumanUx2NavD(candidateWaypoint.Name, "Repère")} · arrivée désactivée"
                : $"{HumanUx2NavD(candidateWaypoint.Name, "Repère")} · " +
                  $"{HumanFacingUx2NavD(candidateWaypoint.Arrival.Facing)} · " +
                  $"attente {candidateWaypoint.Arrival.WaitSeconds:0.##} s";
        return new StudioUx2NavDPreview(true,
            diff.Count == 0
                ? "Aperçu validé · aucune différence sémantique à appliquer."
                : "Aperçu sec validé · aucun acteur, événement runtime ou document n’a été modifié.",
            $"Route : {routeSummary}\nArrivée : {arrivalSummary}", diff.Count > 0, diff, [],
            overlay, runtimeSteps, authorized, denied);
    }

    internal StudioUx2NavDActionResult ApplyApprovedPatrolUx2NavD()
    {
        var draft = _approvedPatrolUx2NavD;
        var history = History;
        if (draft is null || history is null ||
            _approvedPatrolRevisionUx2NavD != history.State.Revision ||
            !string.Equals(_approvedPatrolFingerprintUx2NavD,
                PatrolDraftFingerprintUx2NavD(draft), StringComparison.Ordinal))
        {
            CancelPatrolUx2NavD();
            return new(false, "Prévisualisez de nouveau la patrouille avant de l’appliquer.");
        }
        if (!TryBuildPatrolMutationUx2NavD(draft, false, out var command, out _, out _, out _,
                out var message))
        {
            CancelPatrolUx2NavD();
            return new(false, message);
        }
        var currentPreview = _setNavigationTopologyUx2NavD.Handle(history.State,
            command! with { DryRun = true });
        if (!currentPreview.Succeeded)
        {
            CancelPatrolUx2NavD();
            return new(false, HumanDiagnosticUx2NavD(currentPreview.Diagnostics.FirstOrDefault()?.Message));
        }
        var applied = _setNavigationTopologyUx2NavD.Handle(history.State, command!);
        CancelPatrolUx2NavD();
        if (!applied.Succeeded)
        {
            return new(false, HumanDiagnosticUx2NavD(applied.Diagnostics.FirstOrDefault()?.Message));
        }
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, "Patrouille et arrivée appliquées · Ctrl+Z pour annuler, Ctrl+Y pour rétablir");
    }

    internal void CancelPatrolUx2NavD()
    {
        _approvedPatrolUx2NavD = null;
        _approvedPatrolRevisionUx2NavD = null;
        _approvedPatrolFingerprintUx2NavD = null;
    }

    internal void CloseUx2NavD() => CancelPatrolUx2NavD();

    internal StudioUx2NavDApprovalSnapshot CapturePatrolApprovalUx2NavD() => new(
        _approvedPatrolUx2NavD, _approvedPatrolRevisionUx2NavD,
        _approvedPatrolFingerprintUx2NavD);

    internal void RestorePatrolApprovalUx2NavD(StudioUx2NavDApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedPatrolUx2NavD = snapshot.Draft;
        _approvedPatrolRevisionUx2NavD = snapshot.Revision;
        _approvedPatrolFingerprintUx2NavD = snapshot.Fingerprint;
    }

    private bool TryBuildPatrolMutationUx2NavD(StudioUx2NavDDraft draft, bool dryRun,
        out SetNavigationTopologyCommand? command, out AreaDefinition? sourceArea,
        out AreaDefinition? candidateArea, out IReadOnlyList<StudioUx2NavDDiagnostic> diagnostics,
        out string message)
    {
        command = null;
        sourceArea = null;
        candidateArea = null;
        diagnostics = [];
        message = string.Empty;
        var project = Project;
        var history = History;
        if (project is null || history is null)
        {
            message = "Aucun module n’est ouvert.";
            return false;
        }
        if (draft.BaseRevision != history.State.Revision)
        {
            message = "Le projet a changé depuis l’ouverture de cette fiche. Rechargez la patrouille.";
            return false;
        }
        sourceArea = project.Areas.SingleOrDefault(item => item.Id == draft.AreaId);
        if (sourceArea is null)
        {
            message = "La zone choisie n’existe plus.";
            return false;
        }
        if (!string.Equals(draft.SourceFingerprint, PatrolSourceFingerprintUx2NavD(sourceArea),
                StringComparison.Ordinal))
        {
            message = "Les routes ou arrivées de cette zone ont changé. Rechargez la fiche.";
            return false;
        }
        var inputDiagnostics = ValidatePatrolDraftUx2NavD(draft, sourceArea);
        if (inputDiagnostics.Count > 0)
        {
            diagnostics = inputDiagnostics;
            message = inputDiagnostics[0].Message;
            return false;
        }

        try
        {
            var sourceProfile = sourceArea.Navigation.Waypoints;
            IReadOnlyList<PatrolRoute> routes = sourceProfile.PatrolRoutes;
            if (draft.Route is { } routeDraft)
            {
                var original = sourceProfile.PatrolRoutes.SingleOrDefault(item =>
                    item.Id == routeDraft.RouteId);
                if (routeDraft.IsNew && original is not null || !routeDraft.IsNew && original is null)
                {
                    message = routeDraft.IsNew
                        ? "Une route portant cette identité existe déjà. Recommencez l’ajout."
                        : "La route choisie n’existe plus. Rechargez la liste.";
                    return false;
                }
                var candidate = BuildCandidateRouteUx2NavD(routeDraft, original);
                var updated = sourceProfile.PatrolRoutes.ToList();
                if (original is null)
                {
                    updated.Add(candidate);
                }
                else
                {
                    updated[updated.FindIndex(item => item.Id == original.Id)] = candidate;
                }
                routes = updated;
            }

            IReadOnlyList<WaypointProfile> waypoints = sourceProfile.Waypoints;
            if (draft.Arrival is { } arrivalDraft)
            {
                var originalWaypoint = sourceProfile.Waypoints.SingleOrDefault(item =>
                    item.Id == arrivalDraft.WaypointId);
                if (originalWaypoint is null)
                {
                    message = "Le repère d’arrivée choisi n’existe plus. Rechargez la liste.";
                    return false;
                }
                var arrival = BuildCandidateArrivalUx2NavD(arrivalDraft, originalWaypoint.Arrival);
                var candidateWaypoint = originalWaypoint with { Arrival = arrival };
                var updated = sourceProfile.Waypoints.ToList();
                updated[updated.FindIndex(item => item.Id == originalWaypoint.Id)] = candidateWaypoint;
                waypoints = updated;
            }

            var candidateProfile = new WaypointAuthoringProfile(sourceProfile.Id,
                sourceProfile.SchemaVersion, sourceProfile.Provenance, sourceProfile.AreaId,
                sourceProfile.RequiredAtRuntime, waypoints, routes, sourceProfile.Locators,
                sourceProfile.Collections, sourceProfile.RouteVisualization);
            var navigation = sourceArea.Navigation.WithWaypoints(candidateProfile);
            candidateArea = sourceArea.WithNavigation(navigation);
            command = new SetNavigationTopologyCommand(CommandId.From(Guid.NewGuid()),
                draft.BaseRevision, [new(draft.AreaId, navigation)], [], null, dryRun);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           OverflowException)
        {
            message = HumanDiagnosticUx2NavD(exception.Message);
            diagnostics = [new("studio.patrol.build_invalid", "navigation/patrouilles",
                HumanDiagnosticUx2NavD(exception.Message))];
            return false;
        }
    }

    private static PatrolRoute BuildCandidateRouteUx2NavD(StudioUx2NavDRouteDraft draft,
        PatrolRoute? original)
    {
        if (original is not null && RouteMatchesDraftUx2NavD(original, draft))
        {
            return original;
        }
        var points = draft.Points.Select(item => new PatrolPoint(item.Id, item.Position,
            item.WaitSeconds, item.FacingDegrees, item.Speed, EmptyToNullUx2NavD(item.Action),
            EmptyToNullUx2NavD(item.BranchCondition), item.Weight)).ToArray();
        var schemaVersion = original?.SchemaVersion ?? 1;
        var provenance = original?.Provenance ?? Ux2NavDProvenance;
        var requiredAtRuntime = original?.RequiredAtRuntime ?? false;
        var canonical = new PatrolRoute(draft.RouteId, schemaVersion, provenance,
            draft.Traversal, points, null, draft.ScriptHandler, requiredAtRuntime);
        var edges = canonical.Edges.Select(edge =>
        {
            var preserved = original?.Edges.SingleOrDefault(item =>
                item.FromPointId == edge.FromPointId && item.ToPointId == edge.ToPointId);
            var previousPoint = original?.Points.SingleOrDefault(item =>
                item.Id == edge.FromPointId);
            var currentPoint = points.Single(item => item.Id == edge.FromPointId);
            if (preserved is null || previousPoint is null)
            {
                return edge;
            }
            var branchChanged = !string.Equals(previousPoint.BranchCondition,
                currentPoint.BranchCondition, StringComparison.Ordinal);
            var weightChanged = !previousPoint.Weight.Equals(currentPoint.Weight);
            return edge with
            {
                Id = preserved.Id,
                BranchCondition = branchChanged ? edge.BranchCondition : preserved.BranchCondition,
                Weight = weightChanged ? edge.Weight : preserved.Weight,
            };
        }).ToArray();
        return new PatrolRoute(draft.RouteId, schemaVersion, provenance, draft.Traversal, points,
            edges, draft.ScriptHandler, requiredAtRuntime);
    }

    private static ArrivalBehavior? BuildCandidateArrivalUx2NavD(StudioUx2NavDArrivalDraft draft,
        ArrivalBehavior? original)
    {
        if (ArrivalMatchesDraftUx2NavD(original, draft))
        {
            return original;
        }
        if (!draft.HasArrival)
        {
            return null;
        }
        return new ArrivalBehavior(draft.Facing, draft.DirectionDegrees,
            ResolvedArrivalTargetUx2NavD(draft, original),
            EmptyToNullUx2NavD(draft.Animation), draft.WaitSeconds,
            EmptyToNullUx2NavD(draft.Interaction), EmptyToNullUx2NavD(draft.Callback));
    }

    private static IReadOnlyList<StudioUx2NavDDiagnostic> ValidatePatrolDraftUx2NavD(
        StudioUx2NavDDraft draft, AreaDefinition area)
    {
        var result = new List<StudioUx2NavDDiagnostic>();
        if (draft.Route is null && draft.Arrival is null)
        {
            result.Add(new("studio.patrol.selection_required", "navigation/patrouilles",
                "Choisissez explicitement une route ou un repère d’arrivée."));
            return result;
        }
        if (draft.Route is { } route)
        {
            var root = "navigation/patrouilles/route-selectionnee";
            if (route.RouteId == Guid.Empty || route.Points.Count < 2 ||
                route.Points.Select(item => item.Id).Distinct().Count() != route.Points.Count ||
                route.Points.Any(item => item.Id == Guid.Empty))
            {
                result.Add(new("waypoint.route.invalid", root,
                    "La route requiert au moins deux points ordonnés dotés d’identités stables."));
            }
            var sourceCount = area.Navigation.Waypoints.PatrolRoutes.Sum(item => item.Points.Count) -
                              (route.Original?.Points.Count ?? 0);
            if (route.IsNew && area.Navigation.Waypoints.PatrolRoutes.Count >= 128 ||
                sourceCount + route.Points.Count > 2048)
            {
                result.Add(new("waypoint.authoring.budget_exceeded", root,
                    "La zone ne peut pas dépasser 128 routes ou 2 048 points de patrouille."));
            }
            for (var index = 0; index < route.Points.Count; index++)
            {
                var point = route.Points[index];
                var path = $"{root}/point-{index + 1}";
                if (!InsideAreaUx2NavD(area, point.Position))
                {
                    result.Add(new("waypoint.route.position_invalid", $"{path}/position",
                        $"Le point {index + 1} doit rester dans les limites de la zone."));
                }
                if (!double.IsFinite(point.WaitSeconds) || point.WaitSeconds is < 0 or > 3600 ||
                    !double.IsFinite(point.FacingDegrees) || point.FacingDegrees is < -360 or > 360 ||
                    !double.IsFinite(point.Speed) || point.Speed is <= 0 or > 100 ||
                    !double.IsFinite(point.Weight) || point.Weight < 0)
                {
                    result.Add(new("waypoint.route.point_invalid", path,
                        $"Le point {index + 1} requiert attente 0–3 600 s, orientation -360–360°, " +
                        "vitesse > 0–100 et poids positif ou nul."));
                }
                if (!ValidActionUx2NavD(point.Action) || !ValidBranchUx2NavD(point.BranchCondition))
                {
                    result.Add(new("waypoint.route.action_invalid", path,
                        $"L’action du point {index + 1} doit être un nom sûr et sa branche un texte borné."));
                }
            }
            var totalWeight = route.Points.Sum(item => item.Weight);
            if (route.Traversal == PatrolTraversalPolicy.Weighted &&
                (!double.IsFinite(totalWeight) || totalWeight <= 0))
            {
                result.Add(new("waypoint.route.weight_invalid", $"{root}/poids",
                    "Une patrouille pondérée requiert un poids total strictement positif."));
            }
            if (route.Traversal == PatrolTraversalPolicy.Scripted &&
                !ValidActionUx2NavD(route.ScriptHandler, required: true))
            {
                result.Add(new("waypoint.route.script_invalid", $"{root}/script",
                    "Une patrouille scriptée requiert un gestionnaire ForgeScript nommé et sûr."));
            }
        }
        if (draft.Arrival is { } arrival)
        {
            const string root = "navigation/arrivee/repere-selectionne";
            if (arrival.WaypointId == Guid.Empty)
            {
                result.Add(new("waypoint.arrival.selection_invalid", root,
                    "Le repère d’arrivée n’existe plus."));
            }
            if (arrival.HasArrival && (!double.IsFinite(arrival.DirectionDegrees) ||
                    arrival.DirectionDegrees is < -360 or > 360 ||
                    !double.IsFinite(arrival.WaitSeconds) || arrival.WaitSeconds is < 0 or > 3600))
            {
                result.Add(new("waypoint.arrival.bounds_invalid", root,
                    "L’arrivée requiert une orientation -360–360° et une attente 0–3 600 s."));
            }
            if (arrival.HasArrival && arrival.Facing == ArrivalFacingMode.Target &&
                string.IsNullOrWhiteSpace(arrival.TargetId))
            {
                result.Add(new("waypoint.arrival.target_required", $"{root}/cible",
                    "Le mode « Faire face à une cible » requiert une cible humaine sélectionnée."));
            }
            if (arrival.TargetIsProtected &&
                !string.Equals(arrival.TargetId, arrival.Original?.TargetId, StringComparison.Ordinal))
            {
                result.Add(new("waypoint.arrival.protected_target_changed", $"{root}/cible",
                    "La cible existante protégée doit être conservée ou remplacée par une cible humaine."));
            }
            if (arrival.HasArrival && (!ValidActionUx2NavD(arrival.Animation) ||
                    !ValidActionUx2NavD(arrival.Interaction) ||
                    !ValidActionUx2NavD(arrival.Callback)))
            {
                result.Add(new("waypoint.arrival.action_invalid", $"{root}/actions",
                    "Animation, interaction et callback doivent être des noms sûrs de 128 caractères maximum."));
            }
        }
        return result;
    }

    private static IReadOnlyList<string> BuildPatrolDiffUx2NavD(AreaDefinition sourceArea,
        PatrolRoute? candidateRoute, WaypointProfile? candidateWaypoint, StudioUx2NavDDraft draft)
    {
        var result = new List<string>();
        if (candidateRoute is not null && draft.Route is { } routeDraft)
        {
            var original = sourceArea.Navigation.Waypoints.PatrolRoutes.SingleOrDefault(item =>
                item.Id == candidateRoute.Id);
            if (original is null)
            {
                result.Add($"Ajouter une patrouille {HumanTraversalUx2NavD(candidateRoute.Traversal)} " +
                           $"de {candidateRoute.Points.Count} points.");
            }
            else
            {
                if (original.Traversal != candidateRoute.Traversal ||
                    !string.Equals(original.ScriptHandler, candidateRoute.ScriptHandler,
                        StringComparison.Ordinal))
                {
                    result.Add($"Parcours : {HumanTraversalUx2NavD(original.Traversal)} → " +
                               $"{HumanTraversalUx2NavD(candidateRoute.Traversal)}.");
                }
                if (!original.Points.Select(item => item.Id).SequenceEqual(
                        candidateRoute.Points.Select(item => item.Id)))
                {
                    result.Add("Réordonner les points selon la liste affichée et reconstruire les étapes canoniques.");
                }
                var changedPoints = candidateRoute.Points.Count(point =>
                {
                    var previous = original.Points.SingleOrDefault(item => item.Id == point.Id);
                    return previous is null || previous != point;
                });
                if (changedPoints > 0 || original.Points.Count != candidateRoute.Points.Count)
                {
                    result.Add($"Points : {original.Points.Count} → {candidateRoute.Points.Count} · " +
                               $"{changedPoints} point(s) ajouté(s) ou modifié(s).");
                }
            }
            if (routeDraft.Traversal == PatrolTraversalPolicy.Weighted)
            {
                result.Add($"Poids total de sélection : {candidateRoute.Points.Sum(item => item.Weight):0.##}.");
            }
        }
        if (candidateWaypoint is not null && draft.Arrival is { } arrivalDraft)
        {
            var original = sourceArea.Navigation.Waypoints.Waypoints.Single(item =>
                item.Id == candidateWaypoint.Id);
            if (original.Arrival != candidateWaypoint.Arrival)
            {
                result.Add(candidateWaypoint.Arrival is null
                    ? $"Désactiver l’arrivée de « {HumanUx2NavD(original.Name, "Repère")} »."
                    : original.Arrival is null
                        ? $"Configurer l’arrivée de « {HumanUx2NavD(original.Name, "Repère")} »."
                        : $"Mettre à jour l’orientation, l’attente ou les actions d’arrivée de « " +
                          $"{HumanUx2NavD(original.Name, "Repère")} ».");
            }
            if (arrivalDraft.TargetIsProtected && candidateWaypoint.Arrival?.Facing == ArrivalFacingMode.Target)
            {
                result.Add("Conserver la cible existante protégée sans afficher son identifiant interne.");
            }
        }
        return result;
    }

    private static StudioNavigationOverlaySnapshot BuildPatrolOverlayUx2NavD(AreaDefinition area,
        PatrolRoute? route, WaypointProfile? arrivalWaypoint)
    {
        var markers = new List<StudioNavigationOverlayMarker>();
        var segments = new List<StudioNavigationOverlaySegment>();
        if (route is not null)
        {
            for (var index = 0; index < route.Points.Count; index++)
            {
                var point = route.Points[index];
                markers.Add(new($"patrol:{route.Id:N}:point:{index}", $"Point {index + 1}",
                    StudioNavigationOverlaySemantic.Route,
                    PointUx2NavD(point.Position), index == 0));
            }
            var runtime = WaypointRouteService.AnalyzeRoute(route,
                knownPointIds: route.Points.Select(item => item.Id).ToHashSet(),
                costPerMeter: area.Navigation.Waypoints.RouteVisualization.CostPerMeter);
            for (var index = 0; index < runtime.Count; index++)
            {
                var item = runtime[index];
                var fromIndex = route.Points.ToList().FindIndex(point => point.Id == item.FromId);
                var toIndex = route.Points.ToList().FindIndex(point => point.Id == item.ToId);
                if (fromIndex < 0 || toIndex < 0)
                {
                    continue;
                }
                segments.Add(new($"patrol:{route.Id:N}:segment:{index}",
                    $"Étape {fromIndex + 1} vers {toIndex + 1}",
                    StudioNavigationOverlaySemantic.Route,
                    PointUx2NavD(route.Points[fromIndex].Position),
                    PointUx2NavD(route.Points[toIndex].Position), true, false, item.BrokenLink));
            }
        }
        if (arrivalWaypoint is not null)
        {
            markers.Add(new($"arrival:{arrivalWaypoint.Id:N}",
                $"Arrivée · {HumanUx2NavD(arrivalWaypoint.Name, "Repère")}",
                StudioNavigationOverlaySemantic.Transition,
                PointUx2NavD(arrivalWaypoint.Position), route is null));
        }
        return new(markers, segments, []);
    }

    private static IReadOnlyList<StudioUx2NavDRuntimeStep> BuildRuntimeStepsUx2NavD(
        PatrolRoute? route, double costPerMeter)
    {
        if (route is null)
        {
            return [];
        }
        var points = route.Points.ToList();
        return WaypointRouteService.AnalyzeRoute(route,
                knownPointIds: route.Points.Select(item => item.Id).ToHashSet(),
                costPerMeter: costPerMeter)
            .Select(item =>
            {
                var from = points.FindIndex(point => point.Id == item.FromId);
                var to = points.FindIndex(point => point.Id == item.ToId);
                var source = from < 0 ? null : points[from];
                return new StudioUx2NavDRuntimeStep(from + 1, to + 1, item.Direction,
                    item.Distance, item.TravelSeconds, HumanUx2NavD(item.Action, "aucune"),
                    HumanUx2NavD(source?.BranchCondition, "aucune"));
            }).ToArray();
    }

    private static bool RouteMatchesDraftUx2NavD(PatrolRoute original,
        StudioUx2NavDRouteDraft draft) => original.Traversal == draft.Traversal &&
        string.Equals(original.ScriptHandler, draft.ScriptHandler.Trim(), StringComparison.Ordinal) &&
        original.Points.Count == draft.Points.Count && original.Points.Select((item, index) =>
            item.Id == draft.Points[index].Id && item.Position == draft.Points[index].Position &&
            item.WaitSeconds.Equals(draft.Points[index].WaitSeconds) &&
            item.FacingDegrees.Equals(draft.Points[index].FacingDegrees) &&
            item.Speed.Equals(draft.Points[index].Speed) &&
            string.Equals(item.Action ?? string.Empty, draft.Points[index].Action.Trim(),
                StringComparison.Ordinal) &&
            string.Equals(item.BranchCondition ?? string.Empty,
                draft.Points[index].BranchCondition.Trim(), StringComparison.Ordinal) &&
            item.Weight.Equals(draft.Points[index].Weight)).All(item => item);

    private static bool RoutesEquivalentUx2NavD(PatrolRoute left, PatrolRoute right) =>
        left.Id == right.Id && left.SchemaVersion == right.SchemaVersion &&
        string.Equals(left.Provenance, right.Provenance, StringComparison.Ordinal) &&
        left.Traversal == right.Traversal && left.Points.SequenceEqual(right.Points) &&
        left.Edges.SequenceEqual(right.Edges) &&
        string.Equals(left.ScriptHandler, right.ScriptHandler, StringComparison.Ordinal) &&
        left.RequiredAtRuntime == right.RequiredAtRuntime;

    private static bool ArrivalMatchesDraftUx2NavD(ArrivalBehavior? original,
        StudioUx2NavDArrivalDraft draft)
    {
        if (!draft.HasArrival)
        {
            return original is null;
        }
        return original is not null && original.Facing == draft.Facing &&
               original.DirectionDegrees.Equals(draft.DirectionDegrees) &&
               string.Equals(original.TargetId ?? string.Empty,
                   ResolvedArrivalTargetUx2NavD(draft, original) ?? string.Empty,
                   StringComparison.Ordinal) &&
               string.Equals(original.Animation ?? string.Empty, draft.Animation.Trim(),
                   StringComparison.Ordinal) && original.WaitSeconds.Equals(draft.WaitSeconds) &&
               string.Equals(original.Interaction ?? string.Empty, draft.Interaction.Trim(),
                   StringComparison.Ordinal) &&
               string.Equals(original.Callback ?? string.Empty, draft.Callback.Trim(),
                   StringComparison.Ordinal);
    }

    private static string? ResolvedArrivalTargetUx2NavD(StudioUx2NavDArrivalDraft draft,
        ArrivalBehavior? original)
    {
        if (draft.Facing == ArrivalFacingMode.Target)
        {
            return draft.TargetId.Trim();
        }
        return original is not null && draft.Original is not null &&
               original.Facing == draft.Facing && draft.Original.Facing == draft.Facing &&
               string.Equals(original.TargetId, draft.Original.TargetId, StringComparison.Ordinal)
            ? original.TargetId
            : null;
    }

    private static StudioUx2NavDPreview FailedPatrolPreviewUx2NavD(string message,
        IReadOnlyList<StudioUx2NavDDiagnostic>? diagnostics = null) => new(false, message,
        "Aucun aperçu disponible.", false, [], diagnostics ?? [],
        StudioNavigationOverlaySnapshot.Empty, [], false, false);

    private static bool InsideAreaUx2NavD(AreaDefinition area, Vector3Value position) =>
        float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(position.Z) &&
        Math.Abs(position.X) <= area.Dimensions.Width / 2f &&
        Math.Abs(position.Z) <= area.Dimensions.Depth / 2f && Math.Abs(position.Y) <= 4096;

    private static bool ValidActionUx2NavD(string? value, bool required = false)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return !required;
        }
        return text.Length <= 128 && text.All(character => char.IsLetterOrDigit(character) ||
            character is '.' or '_' or '-' or ':');
    }

    private static bool ValidBranchUx2NavD(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        return text.Length <= 256 && text.All(character => !char.IsControl(character));
    }

    private static string PatrolSourceFingerprintUx2NavD(AreaDefinition area)
    {
        var json = AreaJsonSerializer.Serialize(area);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    private static string PatrolDraftFingerprintUx2NavD(StudioUx2NavDDraft draft)
    {
        var values = new List<string>
        {
            draft.AreaId.ToString(), draft.BaseRevision.ToString(), draft.SourceFingerprint,
            draft.Route is null ? "route:none" : "route:present",
            draft.Arrival is null ? "arrival:none" : "arrival:present",
        };
        if (draft.Route is { } route)
        {
            values.AddRange([route.RouteId.ToString("D"), route.IsNew.ToString(),
                route.Traversal.ToString(), route.ScriptHandler]);
            foreach (var point in route.Points)
            {
                values.AddRange([point.Id.ToString("D"), F(point.Position.X), F(point.Position.Y),
                    F(point.Position.Z), F(point.WaitSeconds), F(point.FacingDegrees), F(point.Speed),
                    point.Action, point.BranchCondition, F(point.Weight)]);
            }
        }
        if (draft.Arrival is { } arrival)
        {
            values.AddRange([arrival.WaypointId.ToString("D"), arrival.HasArrival.ToString(),
                arrival.Facing.ToString(), F(arrival.DirectionDegrees), arrival.TargetId,
                arrival.TargetIsProtected.ToString(), arrival.Animation, F(arrival.WaitSeconds),
                arrival.Interaction, arrival.Callback]);
        }
        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join('\u001f', values))));

        static string F(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static StudioNavigationPoint PointUx2NavD(Vector3Value point) =>
        new(point.X, point.Y, point.Z);

    private static string? EmptyToNullUx2NavD(string? value)
    {
        var text = value?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string HumanDiagnosticUx2NavD(string? message)
    {
        var text = string.IsNullOrWhiteSpace(message) ? "La commande a été refusée." : message.Trim();
        return System.Text.RegularExpressions.Regex.Replace(text,
            @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b",
            "référence interne", System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                                  System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    private static string HumanPathUx2NavD(string path) =>
        System.Text.RegularExpressions.Regex.Replace(path, @"[0-9a-f]{8}-[0-9a-f-]{27,}",
            "zone-selectionnee", System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static string HumanUx2NavD(string? value, string fallback)
    {
        var text = value?.Trim();
        return string.IsNullOrWhiteSpace(text) || Guid.TryParse(text, out _) ? fallback : text;
    }

    private static string HumanTraversalUx2NavD(PatrolTraversalPolicy policy) => policy switch
    {
        PatrolTraversalPolicy.Loop => "boucle",
        PatrolTraversalPolicy.PingPong => "aller-retour",
        PatrolTraversalPolicy.Once => "une fois",
        PatrolTraversalPolicy.Random => "aléatoire",
        PatrolTraversalPolicy.Weighted => "pondérée",
        PatrolTraversalPolicy.Scripted => "scriptée",
        _ => "héritée",
    };

    private static string HumanFacingUx2NavD(ArrivalFacingMode mode) => mode switch
    {
        ArrivalFacingMode.Preserve => "orientation conservée",
        ArrivalFacingMode.Direction => "direction imposée",
        ArrivalFacingMode.Target => "face à une cible",
        _ => "orientation héritée",
    };

    private static string TraversalOutcomeUx2NavD(PatrolRoute route)
    {
        var next = WaypointRouteService.Next(route, new PatrolCursor(0, 1, false), 73);
        return route.Traversal == PatrolTraversalPolicy.Scripted
            ? "prochain point délégué au gestionnaire scripté"
            : next.Complete
                ? "parcours terminé"
                : $"prochain point simulé : {next.Index + 1}";
    }

    private static bool ContainsTechnicalIdentifierUx2NavD(string text) =>
        text.Contains("UUID", StringComparison.OrdinalIgnoreCase) ||
        System.Text.RegularExpressions.Regex.IsMatch(text,
            @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    internal ProjectDefinition? CreateUx2NavDEvidenceProject()
    {
        var source = Project;
        return source is null ? null : CreateUx2NavDEvidenceProject(source.Id.Value);
    }

    private static ProjectDefinition CreateUx2NavDEvidenceProject(Guid seed)
    {
        var project = CreateUx2NavCEvidenceProject(seed == Guid.Empty
            ? Guid.Parse("2d000000-0000-4000-8000-000000000050") : seed);
        var area = project.Areas.Single(item => item.Slug == "clairiere-navigation");
        var profile = area.Navigation.Waypoints;
        var selectedWaypoint = profile.Waypoints.Single(item =>
            item.Name == "Autel des braises") with
        {
            Provenance = Ux2NavDEvidenceProvenance,
            Arrival = new ArrivalBehavior(ArrivalFacingMode.Target, 90,
                "2d000000-0000-4000-8000-000000000090", "look", .5,
                "arrival.inspect", "waypoint.arrived"),
        };
        var waypoints = profile.Waypoints.Select(item =>
            item.Id == selectedWaypoint.Id ? selectedWaypoint : item).ToArray();
        var route = new PatrolRoute(Guid.Parse("2d000000-0000-4000-8000-000000000010"), 1,
            Ux2NavDEvidenceProvenance, PatrolTraversalPolicy.PingPong,
        [
            new PatrolPoint(Guid.Parse("2d000000-0000-4000-8000-000000000011"),
                new Vector3Value(-5, 0, -4), 1, 0, 2, "observe", "alert.low", 1),
            new PatrolPoint(Guid.Parse("2d000000-0000-4000-8000-000000000012"),
                new Vector3Value(0, 0, 4), 2, 90, 3, "listen", "alert.medium", 2),
            new PatrolPoint(Guid.Parse("2d000000-0000-4000-8000-000000000013"),
                new Vector3Value(5, 0, -4), .5, 180, 2.5, "return", null, 1),
        ]);
        var neighborRoute = new PatrolRoute(Guid.Parse("2d000000-0000-4000-8000-000000000020"),
            1, "fixture:neighbor", PatrolTraversalPolicy.Once,
        [
            new PatrolPoint(Guid.Parse("2d000000-0000-4000-8000-000000000021"),
                new Vector3Value(-3, 0, 2), 0, 45, 1.5, null, null, 1),
            new PatrolPoint(Guid.Parse("2d000000-0000-4000-8000-000000000022"),
                new Vector3Value(3, 0, 2), 1, -45, 1.5, "guard", null, 1),
        ]);
        var authored = new WaypointAuthoringProfile(profile.Id, profile.SchemaVersion,
            profile.Provenance, area.Id, profile.RequiredAtRuntime, waypoints, [route, neighborRoute],
            profile.Locators, profile.Collections, profile.RouteVisualization);
        var replacement = area.WithNavigation(area.Navigation.WithWaypoints(authored));
        var areas = project.Areas.Select(item => item.Id == area.Id ? replacement : item).ToArray();
        return new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
            areas, project.Dialogues, project.Quests);
    }

    internal StudioUx2NavDSmokeResult RunUx2NavDContractSmoke()
    {
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceRevision = sourceHistory?.State.Revision;
        var sourceJournal = sourceHistory?.Journal.ToArray() ?? [];
        var sourceDirty = IsDirty;
        var sourceUndo = sourceHistory?.CanUndo;
        var sourceRedo = sourceHistory?.CanRedo;
        var sourceAreaId = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceNavigationApproval = CaptureNavigationApprovalUx2NavB();
        var sourceWaypointApproval = CaptureWaypointApprovalUx2NavC();
        var sourcePatrolApproval = CapturePatrolApprovalUx2NavD();
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-nav-d-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var fixture = CreateUx2NavDEvidenceProject(
                Guid.Parse("2d000000-0000-4000-8000-000000000060"));
            var created = ProjectSession.Create(tempRoot, fixture);
            if (!created.Succeeded || !editing.Open(tempRoot).Succeeded)
            {
                return FailedUx2NavDSmoke();
            }
            var areaChoice = editing.PatrolAreaChoicesUx2NavD.Single(item => item.IsEvidence);
            var routeChoice = editing.PatrolRouteChoicesUx2NavD(areaChoice.AreaId)
                .Single(item => item.IsEvidence);
            var waypointChoice = editing.ArrivalWaypointChoicesUx2NavD(areaChoice.AreaId)
                .Single(item => item.IsEvidence);
            var explicitSelection = editing.ComposePatrolDraftUx2NavD(null, null, null) is null &&
                                    editing.ComposePatrolDraftUx2NavD(areaChoice.AreaId, null, null) is null &&
                                    editing.CreatePatrolRouteDraftUx2NavD(areaChoice.AreaId, null) is null &&
                                    editing.CreateArrivalDraftUx2NavD(areaChoice.AreaId, null) is null;
            var routeDraft = editing.CreatePatrolRouteDraftUx2NavD(areaChoice.AreaId,
                routeChoice.RouteId);
            var arrivalDraft = editing.CreateArrivalDraftUx2NavD(areaChoice.AreaId,
                waypointChoice.WaypointId);
            var original = editing.ComposePatrolDraftUx2NavD(areaChoice.AreaId, routeDraft,
                arrivalDraft);
            if (routeDraft is null || arrivalDraft is null || original is null)
            {
                return FailedUx2NavDSmoke();
            }
            var humanLabels = editing.PatrolAreaChoicesUx2NavD.All(item =>
                                  !ContainsTechnicalIdentifierUx2NavD(item.Label)) &&
                              editing.PatrolRouteChoicesUx2NavD(areaChoice.AreaId).All(item =>
                                  !ContainsTechnicalIdentifierUx2NavD(item.Label)) &&
                              editing.ArrivalWaypointChoicesUx2NavD(areaChoice.AreaId).All(item =>
                                  !ContainsTechnicalIdentifierUx2NavD(item.Label)) &&
                              editing.ArrivalTargetChoicesUx2NavD(areaChoice.AreaId, arrivalDraft).All(item =>
                                  !ContainsTechnicalIdentifierUx2NavD(item.Label));

            var newRoute = editing.CreateNewPatrolRouteDraftUx2NavD(areaChoice.AreaId);
            var newDraft = editing.ComposePatrolDraftUx2NavD(areaChoice.AreaId, newRoute, null);
            var newPreview = newDraft is null
                ? FailedPatrolPreviewUx2NavD("Brouillon absent.")
                : editing.PreviewPatrolUx2NavD(newDraft);
            var routeCreation = newRoute is { IsNew: true, Original: null, Points.Count: 2 } &&
                                newPreview.Succeeded && newPreview.CanApply;

            var invalidFew = editing.PreviewPatrolUx2NavD(original with
            {
                Route = routeDraft with { Points = [routeDraft.Points[0]] },
            });
            var invalidBounds = editing.PreviewPatrolUx2NavD(original with
            {
                Route = routeDraft with
                {
                    Points = routeDraft.Points.Select((item, index) => index == 0
                        ? item with { WaitSeconds = 3601, Speed = 0, Weight = -1 }
                        : item).ToArray(),
                },
            });
            var invalidWeighted = editing.PreviewPatrolUx2NavD(original with
            {
                Route = routeDraft with
                {
                    Traversal = PatrolTraversalPolicy.Weighted,
                    Points = routeDraft.Points.Select(item => item with { Weight = 0 }).ToArray(),
                },
            });
            var invalidScript = editing.PreviewPatrolUx2NavD(original with
            {
                Route = routeDraft with
                {
                    Traversal = PatrolTraversalPolicy.Scripted,
                    ScriptHandler = string.Empty,
                },
            });
            var routeValidation = !invalidFew.Succeeded && !invalidBounds.Succeeded &&
                                  !invalidWeighted.Succeeded && !invalidScript.Succeeded &&
                                  invalidFew.Diagnostics.Any(item => item.Code == "waypoint.route.invalid") &&
                                  invalidBounds.Diagnostics.Any(item =>
                                      item.Code == "waypoint.route.point_invalid") &&
                                  invalidWeighted.Diagnostics.Any(item =>
                                      item.Code == "waypoint.route.weight_invalid") &&
                                  invalidScript.Diagnostics.Any(item =>
                                      item.Code == "waypoint.route.script_invalid");

            var invalidTarget = editing.PreviewPatrolUx2NavD(original with
            {
                Route = null,
                Arrival = arrivalDraft with
                {
                    HasArrival = true,
                    Facing = ArrivalFacingMode.Target,
                    TargetId = string.Empty,
                    TargetIsProtected = false,
                },
            });
            var invalidArrivalBounds = editing.PreviewPatrolUx2NavD(original with
            {
                Route = null,
                Arrival = arrivalDraft with { HasArrival = true, WaitSeconds = 3601 },
            });
            var invalidArrivalAction = editing.PreviewPatrolUx2NavD(original with
            {
                Route = null,
                Arrival = arrivalDraft with { HasArrival = true, Callback = "callback invalide!" },
            });
            var arrivalValidation = !invalidTarget.Succeeded && !invalidArrivalBounds.Succeeded &&
                                    !invalidArrivalAction.Succeeded &&
                                    invalidTarget.Diagnostics.Any(item =>
                                        item.Code == "waypoint.arrival.target_required") &&
                                    invalidArrivalBounds.Diagnostics.Any(item =>
                                        item.Code == "waypoint.arrival.bounds_invalid") &&
                                    invalidArrivalAction.Diagnostics.Any(item =>
                                        item.Code == "waypoint.arrival.action_invalid");

            var protectedTarget = arrivalDraft.TargetId;
            var protectedDraft = editing.ComposePatrolDraftUx2NavD(areaChoice.AreaId, null,
                arrivalDraft with { WaitSeconds = .75 })!;
            var protectedPreview = editing.PreviewPatrolUx2NavD(protectedDraft);
            var protectedApplied = editing.ApplyApprovedPatrolUx2NavD();
            var protectedWaypoint = editing.Project!.Areas.Single(item =>
                    item.Id == areaChoice.AreaId).Navigation.Waypoints.Waypoints.Single(item =>
                    item.Id == waypointChoice.WaypointId);
            var protectedUndo = editing.Undo();
            arrivalValidation &= arrivalDraft.TargetIsProtected && protectedPreview.Succeeded &&
                                 protectedApplied.Succeeded &&
                                 protectedWaypoint.Arrival?.TargetId == protectedTarget &&
                                 !ContainsTechnicalIdentifierUx2NavD(
                                     string.Join(' ', protectedPreview.Diff)) &&
                                 protectedUndo.Succeeded;

            var traversalPreview = Enum.GetValues<PatrolTraversalPolicy>().All(policy =>
            {
                var route = new PatrolRoute(Guid.NewGuid(), 1, "smoke:runtime", policy,
                    routeDraft.Points.Select(item => new PatrolPoint(item.Id, item.Position,
                        item.WaitSeconds, item.FacingDegrees, item.Speed,
                        EmptyToNullUx2NavD(item.Action), EmptyToNullUx2NavD(item.BranchCondition),
                        Math.Max(1, item.Weight))).ToArray(), null,
                    policy == PatrolTraversalPolicy.Scripted ? "patrol.choose" : null);
                var segments = WaypointRouteService.AnalyzeRoute(route,
                    knownPointIds: route.Points.Select(item => item.Id).ToHashSet());
                var next = WaypointRouteService.Next(route, new PatrolCursor(0, 1, false), 73);
                return segments.Count >= route.Points.Count - 1 && next.Index >= 0 &&
                       next.Index < route.Points.Count;
            });

            var reorderedPoints = new[]
            {
                routeDraft.Points[2] with
                {
                    WaitSeconds = 3,
                    Speed = 4,
                    Action = "signal.ready",
                    BranchCondition = "alert.high",
                    Weight = 3,
                },
                routeDraft.Points[0] with { Weight = 1.5 },
                routeDraft.Points[1] with { Weight = .5 },
            };
            var target = editing.Project!.Areas.Single(item => item.Id == areaChoice.AreaId)
                .Navigation.Waypoints.Waypoints.Single(item => item.Id != waypointChoice.WaypointId);
            var configuredRoute = routeDraft with
            {
                Traversal = PatrolTraversalPolicy.Weighted,
                Points = reorderedPoints,
                ScriptHandler = string.Empty,
            };
            var configuredArrival = arrivalDraft with
            {
                HasArrival = true,
                Facing = ArrivalFacingMode.Target,
                DirectionDegrees = 135,
                TargetId = target.Name,
                TargetIsProtected = false,
                Animation = "arrival.salute",
                WaitSeconds = 1.25,
                Interaction = "arrival.inspect",
                Callback = "waypoint.arrived",
            };
            var configured = editing.ComposePatrolDraftUx2NavD(areaChoice.AreaId,
                configuredRoute, configuredArrival)!;
            var historyBefore = editing.History!;
            var projectBefore = editing.Project;
            var revisionBefore = historyBefore.State.Revision;
            var journalBefore = historyBefore.Journal.ToArray();
            var preview = editing.PreviewPatrolUx2NavD(configured);
            var dryRun = preview.Succeeded && preview.CanApply &&
                         ReferenceEquals(projectBefore, editing.Project) &&
                         ReferenceEquals(historyBefore, editing.History) &&
                         historyBefore.State.Revision == revisionBefore &&
                         historyBefore.Journal.SequenceEqual(journalBefore);
            var arrivalAuthority = preview.ArrivalAuthorized &&
                                   preview.ArrivalDeniedWithoutActions;
            traversalPreview &= preview.RuntimeSteps.Count >= reorderedPoints.Length - 1 &&
                                preview.Overlay.Segments.Count == preview.RuntimeSteps.Count;

            var staleSource = editing.PreviewPatrolUx2NavD(configured with
            {
                SourceFingerprint = "stale",
            });
            var staleSourceBlocked = !staleSource.Succeeded;

            routeDraft = editing.CreatePatrolRouteDraftUx2NavD(areaChoice.AreaId,
                routeChoice.RouteId)!;
            arrivalDraft = editing.CreateArrivalDraftUx2NavD(areaChoice.AreaId,
                waypointChoice.WaypointId)!;
            var staleDraft = editing.ComposePatrolDraftUx2NavD(areaChoice.AreaId, routeDraft,
                arrivalDraft)!;
            var otherArea = editing.Project!.Areas.Single(item => item.Id != areaChoice.AreaId);
            var neighborChanged = ApplyUx2NavDNeighborMutation(editing, otherArea);
            var stalePreview = editing.PreviewPatrolUx2NavD(staleDraft);
            var staleRevisionBlocked = neighborChanged && !stalePreview.Succeeded &&
                                       editing.Undo().Succeeded;

            routeDraft = editing.CreatePatrolRouteDraftUx2NavD(areaChoice.AreaId,
                routeChoice.RouteId)!;
            arrivalDraft = editing.CreateArrivalDraftUx2NavD(areaChoice.AreaId,
                waypointChoice.WaypointId)!;
            configured = editing.ComposePatrolDraftUx2NavD(areaChoice.AreaId,
                routeDraft with { Traversal = PatrolTraversalPolicy.Loop }, arrivalDraft)!;
            var approvalPreview = editing.PreviewPatrolUx2NavD(configured);
            otherArea = editing.Project!.Areas.Single(item => item.Id != areaChoice.AreaId);
            neighborChanged = ApplyUx2NavDNeighborMutation(editing, otherArea);
            var staleApply = editing.ApplyApprovedPatrolUx2NavD();
            var staleApprovalBlocked = approvalPreview.Succeeded && neighborChanged &&
                                       !staleApply.Succeeded && editing.Undo().Succeeded;

            var canonicalSource = editing.Project!;
            var sourceArea = canonicalSource.Areas.Single(item => item.Id == areaChoice.AreaId);
            var sourceNeighbor = canonicalSource.Areas.Single(item => item.Id != areaChoice.AreaId);
            var sourceRoute = sourceArea.Navigation.Waypoints.PatrolRoutes.Single(item =>
                item.Id == routeChoice.RouteId);
            var sourceWaypoint = sourceArea.Navigation.Waypoints.Waypoints.Single(item =>
                item.Id == waypointChoice.WaypointId);
            routeDraft = editing.CreatePatrolRouteDraftUx2NavD(areaChoice.AreaId,
                routeChoice.RouteId)!;
            arrivalDraft = editing.CreateArrivalDraftUx2NavD(areaChoice.AreaId,
                waypointChoice.WaypointId)!;
            reorderedPoints =
            [
                routeDraft.Points[2] with
                {
                    WaitSeconds = 3,
                    Speed = 4,
                    Action = "signal.ready",
                    BranchCondition = "alert.high",
                    Weight = 3,
                },
                routeDraft.Points[0] with { Weight = 1.5 },
                routeDraft.Points[1] with { Weight = .5 },
            ];
            target = sourceArea.Navigation.Waypoints.Waypoints.Single(item =>
                item.Id != waypointChoice.WaypointId);
            configuredRoute = routeDraft with
            {
                Traversal = PatrolTraversalPolicy.Weighted,
                Points = reorderedPoints,
            };
            configuredArrival = arrivalDraft with
            {
                HasArrival = true,
                Facing = ArrivalFacingMode.Target,
                DirectionDegrees = 135,
                TargetId = target.Name,
                TargetIsProtected = false,
                Animation = "arrival.salute",
                WaitSeconds = 1.25,
                Interaction = "arrival.inspect",
                Callback = "waypoint.arrived",
            };
            configured = editing.ComposePatrolDraftUx2NavD(areaChoice.AreaId, configuredRoute,
                configuredArrival)!;
            var freshPreview = editing.PreviewPatrolUx2NavD(configured);
            var applied = editing.ApplyApprovedPatrolUx2NavD();
            var appliedProject = editing.Project!;
            var appliedArea = appliedProject.Areas.Single(item => item.Id == areaChoice.AreaId);
            var appliedRoute = appliedArea.Navigation.Waypoints.PatrolRoutes.Single(item =>
                item.Id == routeChoice.RouteId);
            var appliedWaypoint = appliedArea.Navigation.Waypoints.Waypoints.Single(item =>
                item.Id == waypointChoice.WaypointId);
            var stablePointOrder = appliedRoute.Points.Select(item => item.Id)
                                       .SequenceEqual(reorderedPoints.Select(item => item.Id)) &&
                                   appliedRoute.Edges.Select(item => item.DisplayOrder)
                                       .SequenceEqual(Enumerable.Range(0,
                                           appliedRoute.Points.Count - 1)) &&
                                   appliedRoute.Edges.Select((edge, index) =>
                                           edge.FromPointId == appliedRoute.Points[index].Id &&
                                           edge.ToPointId == appliedRoute.Points[index + 1].Id &&
                                           edge.BranchCondition == appliedRoute.Points[index].BranchCondition &&
                                           edge.Weight.Equals(appliedRoute.Points[index].Weight))
                                       .All(item => item) &&
                                   sourceRoute.Edges.Where(edge => appliedRoute.Edges.Any(candidate =>
                                           candidate.FromPointId == edge.FromPointId &&
                                           candidate.ToPointId == edge.ToPointId))
                                       .All(edge => appliedRoute.Edges.Single(candidate =>
                                           candidate.FromPointId == edge.FromPointId &&
                                           candidate.ToPointId == edge.ToPointId).Id == edge.Id);
            var canonicalCommand = freshPreview.Succeeded && applied.Succeeded &&
                                   editing.History!.Journal.LastOrDefault()?.CommandName ==
                                   "set_navigation_topology";
            var routeNeighborsPreserved = sourceArea.Navigation.Waypoints.PatrolRoutes
                    .Where(item => item.Id != sourceRoute.Id)
                    .SequenceEqual(appliedArea.Navigation.Waypoints.PatrolRoutes
                        .Where(item => item.Id != sourceRoute.Id)) &&
                sourceRoute.Id == appliedRoute.Id && sourceRoute.SchemaVersion == appliedRoute.SchemaVersion &&
                sourceRoute.Provenance == appliedRoute.Provenance &&
                sourceRoute.RequiredAtRuntime == appliedRoute.RequiredAtRuntime;
            var waypointNeighborsPreserved = sourceArea.Navigation.Waypoints.Waypoints
                    .Where(item => item.Id != sourceWaypoint.Id)
                    .SequenceEqual(appliedArea.Navigation.Waypoints.Waypoints
                        .Where(item => item.Id != sourceWaypoint.Id));
            var waypointTailsPreserved = appliedWaypoint.Id == sourceWaypoint.Id &&
                                         appliedWaypoint.SchemaVersion == sourceWaypoint.SchemaVersion &&
                                         appliedWaypoint.Provenance == sourceWaypoint.Provenance &&
                                         appliedWaypoint.Name == sourceWaypoint.Name &&
                                         appliedWaypoint.Position == sourceWaypoint.Position &&
                                         appliedWaypoint.Appearance == sourceWaypoint.Appearance &&
                                         appliedWaypoint.Marker == sourceWaypoint.Marker &&
                                         appliedWaypoint.AnchorInstanceId == sourceWaypoint.AnchorInstanceId &&
                                         appliedWaypoint.RequiredAtRuntime == sourceWaypoint.RequiredAtRuntime &&
                                         appliedWaypoint.ForgeScriptApiVersion ==
                                         sourceWaypoint.ForgeScriptApiVersion &&
                                         sourceArea.Navigation.Waypoints.Id ==
                                         appliedArea.Navigation.Waypoints.Id &&
                                         sourceArea.Navigation.Waypoints.SchemaVersion ==
                                         appliedArea.Navigation.Waypoints.SchemaVersion &&
                                         sourceArea.Navigation.Waypoints.Provenance ==
                                         appliedArea.Navigation.Waypoints.Provenance &&
                                         sourceArea.Navigation.Waypoints.AreaId ==
                                         appliedArea.Navigation.Waypoints.AreaId &&
                                         sourceArea.Navigation.Waypoints.RequiredAtRuntime ==
                                         appliedArea.Navigation.Waypoints.RequiredAtRuntime &&
                                         sourceArea.Navigation.Waypoints.Locators.SequenceEqual(
                                             appliedArea.Navigation.Waypoints.Locators) &&
                                         sourceArea.Navigation.Waypoints.Collections.SequenceEqual(
                                             appliedArea.Navigation.Waypoints.Collections) &&
                                         sourceArea.Navigation.Waypoints.RouteVisualization ==
                                         appliedArea.Navigation.Waypoints.RouteVisualization;
            var navigationNeighborsPreserved = sourceArea.Navigation.AgentProfiles.SequenceEqual(
                                                   appliedArea.Navigation.AgentProfiles) &&
                                               sourceArea.Navigation.OffMeshLinks.SequenceEqual(
                                                   appliedArea.Navigation.OffMeshLinks) &&
                                               ReferenceEquals(sourceArea.Navigation.Anchors,
                                                   appliedArea.Navigation.Anchors) &&
                                               sourceArea.Portals.SequenceEqual(appliedArea.Portals);
            var appliedNeighbor = appliedProject.Areas.Single(item => item.Id == sourceNeighbor.Id);
            var projectNeighborsPreserved = ReferenceEquals(canonicalSource.Module,
                                                appliedProject.Module) &&
                                            ReferenceEquals(sourceNeighbor, appliedNeighbor) &&
                                            PatrolNeighborsMatchUx2NavD(sourceArea, appliedArea) &&
                                            canonicalSource.Blueprints.SequenceEqual(
                                                appliedProject.Blueprints) &&
                                            canonicalSource.Dialogues.SequenceEqual(
                                                appliedProject.Dialogues) &&
                                            canonicalSource.Quests.SequenceEqual(appliedProject.Quests);
            var appliedJson = AreaJsonSerializer.Serialize(appliedArea);
            var undo = editing.Undo();
            var undone = ReferenceEquals(canonicalSource, editing.Project);
            var redo = editing.Redo();
            var redone = ReferenceEquals(appliedProject, editing.Project);
            var undoRedo = undo.Succeeded && undone && redo.Succeeded && redone;
            var save = editing.Save();
            var reopen = reopened.Open(tempRoot, areaChoice.AreaId.ToString());
            var restoredArea = reopened.Project?.Areas.SingleOrDefault(item =>
                item.Id == areaChoice.AreaId);
            var persistence = save.Succeeded && reopen.Succeeded && restoredArea is not null &&
                              string.Equals(appliedJson, AreaJsonSerializer.Serialize(restoredArea),
                                  StringComparison.Ordinal);
            var humanText = !ContainsTechnicalIdentifierUx2NavD(areaChoice.Label + " " +
                            routeChoice.Label + " " + waypointChoice.Label + " " +
                            freshPreview.Summary + " " + string.Join(' ', freshPreview.Diff));
            var sourceUntouched = ReferenceEquals(sourceProject, Project) &&
                                  ReferenceEquals(sourceHistory, History) &&
                                  History?.State.Revision == sourceRevision &&
                                  (History?.Journal ?? []).SequenceEqual(sourceJournal) &&
                                  IsDirty == sourceDirty && History?.CanUndo == sourceUndo &&
                                  History?.CanRedo == sourceRedo &&
                                  ActiveAreaId == sourceAreaId &&
                                  SelectedEntityIds.SequenceEqual(sourceSelection) &&
                                  SelectedEntityId == sourceSelected &&
                                  CaptureNavigationApprovalUx2NavB() == sourceNavigationApproval &&
                                  CaptureWaypointApprovalUx2NavC() == sourceWaypointApproval &&
                                  CapturePatrolApprovalUx2NavD() == sourcePatrolApproval;
            var succeeded = explicitSelection && humanLabels && routeCreation && stablePointOrder &&
                            routeValidation && arrivalValidation && traversalPreview && arrivalAuthority &&
                            dryRun && staleRevisionBlocked && staleSourceBlocked &&
                            staleApprovalBlocked && canonicalCommand && undoRedo && persistence &&
                            routeNeighborsPreserved && waypointNeighborsPreserved &&
                            waypointTailsPreserved && navigationNeighborsPreserved &&
                            projectNeighborsPreserved && sourceUntouched && humanText;
            return new(succeeded, explicitSelection, humanLabels, routeCreation, stablePointOrder,
                routeValidation, arrivalValidation, traversalPreview, arrivalAuthority, dryRun,
                staleRevisionBlocked, staleSourceBlocked, staleApprovalBlocked, canonicalCommand,
                undoRedo, persistence, routeNeighborsPreserved, waypointNeighborsPreserved,
                waypointTailsPreserved, navigationNeighborsPreserved, projectNeighborsPreserved,
                sourceUntouched, humanText);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_NAV_D_CONTRACT_FAILED type={exception.GetType().Name} message={exception.Message}");
            return FailedUx2NavDSmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            DeleteUx2NavDTemporaryWorkspace(tempRoot);
        }
    }

    private static bool ApplyUx2NavDNeighborMutation(StudioSessionController controller,
        AreaDefinition area)
    {
        var history = controller.History;
        if (history is null)
        {
            return false;
        }
        var navigation = area.Navigation;
        var candidate = new AreaNavigationDefinition(navigation.Id,
            navigation.CellSize == 1f ? .9f : 1f, navigation.AgentProfiles,
            navigation.OffMeshLinks, "studio:ux2-nav-d:neighbor", navigation.SchemaVersion,
            navigation.Anchors, navigation.Waypoints);
        var result = controller._setNavigationTopologyUx2NavD.Handle(history.State,
            new SetNavigationTopologyCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                [new(area.Id, candidate)], [], null, false));
        if (!result.Succeeded)
        {
            return false;
        }
        history.Record(result);
        return true;
    }

    internal static bool DeleteUx2NavDTemporaryWorkspace(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return true;
        }
        try
        {
            var root = Path.GetFullPath(directory);
            var temp = Path.GetFullPath(Path.GetTempPath());
            var prefix = temp.TrimEnd(Path.DirectorySeparatorChar,
                             Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var leaf = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
            if (!root.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !leaf.StartsWith("openrpg-forge-studio-ux2-nav-d-", StringComparison.Ordinal))
            {
                return false;
            }
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
            return !Directory.Exists(root);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    internal static bool PatrolNeighborsMatchUx2NavD(AreaDefinition source,
        AreaDefinition candidate)
    {
        var navigation = source.Navigation;
        var expectedNavigation = new AreaNavigationDefinition(navigation.Id, navigation.CellSize,
            navigation.AgentProfiles, navigation.OffMeshLinks, navigation.Provenance,
            navigation.SchemaVersion, navigation.Anchors, candidate.Navigation.Waypoints);
        var expected = source.WithNavigation(expectedNavigation);
        return string.Equals(AreaJsonSerializer.Serialize(expected),
            AreaJsonSerializer.Serialize(candidate), StringComparison.Ordinal);
    }

    private static StudioUx2NavDSmokeResult FailedUx2NavDSmoke() => new(
        Succeeded: false,
        ExplicitSelection: false,
        HumanLabels: false,
        RouteCreation: false,
        StablePointOrder: false,
        RouteValidation: false,
        ArrivalValidation: false,
        TraversalPreview: false,
        ArrivalAuthority: false,
        DryRun: false,
        StaleRevisionBlocked: false,
        StaleSourceBlocked: false,
        StaleApprovalBlocked: false,
        CanonicalCommand: false,
        UndoRedo: false,
        Persistence: false,
        RouteNeighborsPreserved: false,
        WaypointNeighborsPreserved: false,
        WaypointTailsPreserved: false,
        NavigationNeighborsPreserved: false,
        ProjectNeighborsPreserved: false,
        SourceUntouched: false,
        HumanText: false);
}
