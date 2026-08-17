using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Core.Navigation;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2NavLProvenance = "studio:ux2-nav-l:vehicle-route";
    private const string Ux2NavLEvidenceProvenance = "studio:ux2-nav-l:evidence";
    private const double Ux2NavLMaximumCoordinate = 1_000_000;
    private const double Ux2NavLMaximumScalar = 1_000_000;
    private static readonly string[] Ux2NavLDirections = ["forward", "reverse", "bidirectional"];
    private static readonly string[] Ux2NavLSyncModes = ["server-snapshots", "bounded-prediction", "local"];

    private readonly SetWorldFeatureCatalogHandler _setVehicleRouteCatalogUx2NavL = new(new SystemClock());
    private StudioUx2NavLDraft? _approvedVehicleRouteUx2NavL;
    private ProjectRevision? _approvedVehicleRouteRevisionUx2NavL;
    private string? _approvedVehicleRouteFingerprintUx2NavL;

    internal IReadOnlyList<StudioUx2NavLAreaChoice> VehicleRouteAreaChoicesUx2NavL
    {
        get
        {
            var project = Project;
            if (project is null) return [];
            var catalog = project.Module.WorldFeatures;
            var duplicateNames = project.Areas.GroupBy(item => HumanUx2NavL(item.DisplayName, "Zone"),
                    StringComparer.CurrentCultureIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(),
                    StringComparer.CurrentCultureIgnoreCase);
            return project.Areas.Select((area, index) =>
                {
                    var name = HumanUx2NavL(area.DisplayName, "Zone");
                    var label = duplicateNames[name] > 1
                        ? $"{name} · {HumanUx2NavL(area.Slug, $"zone {index + 1}")}" : name;
                    var routes = catalog.VehicleRoutes.Where(item => item.AreaId == area.Id).ToArray();
                    return new StudioUx2NavLAreaChoice(area.Id, label, routes.Length > 0,
                        routes.Any(item => item.Provenance == Ux2NavLEvidenceProvenance));
                })
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.AreaId.Value)
                .ToArray();
        }
    }

    internal IReadOnlyList<StudioUx2NavLRouteChoice> VehicleRouteChoicesUx2NavL(AreaId? areaId)
    {
        var project = Project;
        if (project is null || areaId is null) return [];
        var routes = project.Module.WorldFeatures.VehicleRoutes.Where(item => item.AreaId == areaId).ToArray();
        return routes.Select((route, index) =>
            {
                var editable = VehicleRouteEditableUx2NavL(route, out var reason);
                var label = $"Route de véhicules {index + 1} · {route.Points.Count} point(s) · " +
                            $"{route.Lanes.Count} voie(s)";
                if (!editable) label += " · lecture seule";
                return new StudioUx2NavLRouteChoice(route.Id, label, editable, reason,
                    route.Provenance == Ux2NavLEvidenceProvenance);
            }).ToArray();
    }

    internal StudioUx2NavLDraft? CreateVehicleRouteDraftUx2NavL(AreaId? areaId, Guid? routeId)
    {
        var history = History;
        var project = Project;
        if (history is null || project is null || areaId is null || routeId is null) return null;
        var area = project.Areas.SingleOrDefault(item => item.Id == areaId);
        var route = project.Module.WorldFeatures.VehicleRoutes.SingleOrDefault(item =>
            item.Id == routeId.Value && item.AreaId == areaId);
        if (area is null || route is null || !VehicleRouteEditableUx2NavL(route, out _)) return null;
        var label = VehicleRouteAreaChoicesUx2NavL.Single(item => item.AreaId == areaId).Label;
        return new(area.Id, route.Id, false, false, route, history.State.Revision,
            VehicleRouteSourceFingerprintUx2NavL(project), label,
            route.Points.Select(item => PointDraftUx2NavL(item)).ToArray(),
            route.Lanes.Select(item => LaneDraftUx2NavL(item)).ToArray(),
            route.Stops.Select(item => StopDraftUx2NavL(item)).ToArray(),
            route.DefaultSpeedLimit, route.AllowOvertaking, route.NetworkSyncMode);
    }

    internal StudioUx2NavLDraft? CreateNewVehicleRouteDraftUx2NavL(AreaId? areaId)
    {
        var history = History;
        var project = Project;
        if (history is null || project is null || areaId is null) return null;
        var area = project.Areas.SingleOrDefault(item => item.Id == areaId);
        if (area is null) return null;
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var extent = Math.Clamp(area.Dimensions.Width / 2d, 5, 20);
        var points = new StudioUx2NavLPointDraft[]
        {
            new(first, true, null, 0, 0, 0, 0, 3),
            new(second, true, null, extent, 0, 0, 0, 3),
        };
        var lanes = new StudioUx2NavLLaneDraft[]
        {
            new(true, null, false, "voie-1", 0, 8, false, "forward"),
        };
        var stops = new StudioUx2NavLStopDraft[]
        {
            new(Guid.NewGuid(), true, null, second, 5, false, "arret-1"),
        };
        var label = VehicleRouteAreaChoicesUx2NavL.Single(item => item.AreaId == areaId).Label;
        return new(area.Id, Guid.NewGuid(), true, false, null, history.State.Revision,
            VehicleRouteSourceFingerprintUx2NavL(project), label, points, lanes, stops,
            8, false, "server-snapshots");
    }

    internal StudioUx2NavLPointDraft? CreateNewVehiclePointUx2NavL(StudioUx2NavLDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Points.Count >= 64) return null;
        var last = draft.Points.LastOrDefault();
        var x = last is null ? 0 : last.PositionX + 5;
        if (Math.Abs(x) > Ux2NavLMaximumCoordinate) x = last is null ? 0 : last.PositionX - 5;
        var id = UniqueGuidUx2NavL(draft.Points.Select(item => item.Id)
            .Concat(draft.Stops.Select(item => item.Id)));
        return new(id, true, null, x, last?.PositionY ?? 0, last?.PositionZ ?? 0,
            last?.TangentDegrees ?? 0, last?.Width ?? 3);
    }

    internal StudioUx2NavLLaneDraft? CreateNewVehicleLaneUx2NavL(StudioUx2NavLDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Lanes.Count >= 16) return null;
        var index = draft.Lanes.Count + 1;
        var id = $"voie-{index}";
        while (draft.Lanes.Any(item => string.Equals(item.Id, id, StringComparison.Ordinal)))
            id = $"voie-{++index}";
        return new(true, null, false, id, draft.Lanes.Count * 3d, draft.DefaultSpeedLimit,
            false, "forward");
    }

    internal StudioUx2NavLStopDraft? CreateNewVehicleStopUx2NavL(StudioUx2NavLDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Stops.Count >= 64 || draft.Points.Count == 0) return null;
        var index = draft.Stops.Count + 1;
        var service = $"arret-{index}";
        while (draft.Stops.Any(item => string.Equals(item.ServiceId, service, StringComparison.Ordinal)))
            service = $"arret-{++index}";
        var id = UniqueGuidUx2NavL(draft.Points.Select(item => item.Id)
            .Concat(draft.Stops.Select(item => item.Id)));
        return new(id, true, null, draft.Points[^1].Id, 5, false, service);
    }

    internal StudioUx2NavLPreview PreviewVehicleRouteUx2NavL(StudioUx2NavLDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelVehicleRouteApprovalUx2NavL();
        var history = History;
        var project = Project;
        if (history is null || project is null)
            return FailedVehicleRoutePreviewUx2NavL("Aucun module n’est ouvert.");
        if (!TryBuildVehicleRouteMutationUx2NavL(draft, true, out var command, out _,
                out var candidateRoute, out var sourceRoute, out var diagnostics, out var message))
            return FailedVehicleRoutePreviewUx2NavL(message, diagnostics);
        var dryRun = _setVehicleRouteCatalogUx2NavL.Handle(history.State, command!);
        if (!dryRun.Succeeded)
        {
            var commandDiagnostics = dryRun.Diagnostics.Select(item => new StudioUx2NavLDiagnostic(
                item.Code, $"zone/{draft.AreaLabel}/routes-vehicules", HumanDiagnosticUx2NavL(item.Message)))
                .ToArray();
            return FailedVehicleRoutePreviewUx2NavL(
                commandDiagnostics.FirstOrDefault()?.Message ?? "L’aperçu de route a été refusé.",
                commandDiagnostics);
        }
        StudioUx2NavLRuntimePreview? runtime = null;
        var overlay = StudioNavigationOverlaySnapshot.Empty;
        if (candidateRoute is not null)
        {
            try
            {
                runtime = BuildVehicleRuntimePreviewUx2NavL(candidateRoute);
                overlay = BuildVehicleRouteOverlayUx2NavL(candidateRoute);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                               OverflowException)
            {
                return FailedVehicleRoutePreviewUx2NavL(HumanDiagnosticUx2NavL(exception.Message),
                [
                    new("studio.vehicle_route.preview_invalid", "route-vehicule/apercu",
                        HumanDiagnosticUx2NavL(exception.Message)),
                ]);
            }
        }
        var diff = BuildVehicleRouteDiffUx2NavL(sourceRoute, candidateRoute, draft.Remove);
        if (diff.Count > 0)
        {
            _approvedVehicleRouteUx2NavL = draft;
            _approvedVehicleRouteRevisionUx2NavL = history.State.Revision;
            _approvedVehicleRouteFingerprintUx2NavL = VehicleRouteDraftFingerprintUx2NavL(draft);
        }
        var summary = candidateRoute is null
            ? "Route sélectionnée prête à être retirée."
            : $"{candidateRoute.Points.Count} point(s) · {candidateRoute.Lanes.Count} voie(s) · " +
              $"{candidateRoute.Stops.Count} arrêt(s) · vitesse {candidateRoute.DefaultSpeedLimit:0.##}";
        return new(true, diff.Count == 0
                ? "Aperçu validé · aucune différence sémantique à appliquer."
                : "Aperçu sec validé · aucun véhicule, physique, dépassement ou trafic réseau n’a été exécuté.",
            summary, diff.Count > 0, diff, [], overlay, runtime);
    }

    internal StudioUx2NavLActionResult ApplyApprovedVehicleRouteUx2NavL()
    {
        var draft = _approvedVehicleRouteUx2NavL;
        var history = History;
        if (draft is null || history is null || _approvedVehicleRouteRevisionUx2NavL != history.State.Revision ||
            !string.Equals(_approvedVehicleRouteFingerprintUx2NavL,
                VehicleRouteDraftFingerprintUx2NavL(draft), StringComparison.Ordinal))
        {
            CancelVehicleRouteApprovalUx2NavL();
            return new(false, "Prévisualisez de nouveau la route avant de l’appliquer.");
        }
        if (!TryBuildVehicleRouteMutationUx2NavL(draft, false, out var command, out _, out _, out _, out _,
                out var message))
        {
            CancelVehicleRouteApprovalUx2NavL();
            return new(false, message);
        }
        var currentDryRun = _setVehicleRouteCatalogUx2NavL.Handle(history.State,
            command! with { DryRun = true });
        if (!currentDryRun.Succeeded)
        {
            CancelVehicleRouteApprovalUx2NavL();
            return new(false, HumanDiagnosticUx2NavL(currentDryRun.Diagnostics.FirstOrDefault()?.Message));
        }
        var applied = _setVehicleRouteCatalogUx2NavL.Handle(history.State, command!);
        CancelVehicleRouteApprovalUx2NavL();
        if (!applied.Succeeded)
            return new(false, HumanDiagnosticUx2NavL(applied.Diagnostics.FirstOrDefault()?.Message));
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, draft.Remove
            ? "Route retirée · Ctrl+Z pour annuler"
            : "Route appliquée · Ctrl+Z pour annuler, Ctrl+Y pour rétablir");
    }

    internal void CancelVehicleRouteApprovalUx2NavL()
    {
        _approvedVehicleRouteUx2NavL = null;
        _approvedVehicleRouteRevisionUx2NavL = null;
        _approvedVehicleRouteFingerprintUx2NavL = null;
    }

    internal void CloseUx2NavL() => CancelVehicleRouteApprovalUx2NavL();

    internal StudioUx2NavLApprovalSnapshot CaptureVehicleRouteApprovalUx2NavL() => new(
        _approvedVehicleRouteUx2NavL, _approvedVehicleRouteRevisionUx2NavL,
        _approvedVehicleRouteFingerprintUx2NavL);

    internal void RestoreVehicleRouteApprovalUx2NavL(StudioUx2NavLApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedVehicleRouteUx2NavL = snapshot.Draft;
        _approvedVehicleRouteRevisionUx2NavL = snapshot.Revision;
        _approvedVehicleRouteFingerprintUx2NavL = snapshot.Fingerprint;
    }

    private bool TryBuildVehicleRouteMutationUx2NavL(StudioUx2NavLDraft draft, bool dryRun,
        out SetWorldFeatureCatalogCommand? command, out ProjectDefinition? candidateProject,
        out VehicleRouteProfile? candidateRoute, out VehicleRouteProfile? sourceRoute,
        out IReadOnlyList<StudioUx2NavLDiagnostic> diagnostics, out string message)
    {
        command = null;
        candidateProject = null;
        candidateRoute = null;
        sourceRoute = null;
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
            message = "Le projet a changé depuis l’ouverture de cette fiche. Rechargez la route.";
            return false;
        }
        if (!string.Equals(draft.SourceFingerprint, VehicleRouteSourceFingerprintUx2NavL(project),
                StringComparison.Ordinal))
        {
            message = "Le projet canonique a changé. Rechargez la fiche de route.";
            return false;
        }
        if (!project.Areas.Any(item => item.Id == draft.AreaId))
        {
            message = "La zone choisie n’existe plus.";
            return false;
        }
        var catalog = project.Module.WorldFeatures;
        sourceRoute = catalog.VehicleRoutes.SingleOrDefault(item => item.Id == draft.RouteId);
        if (draft.IsNew ? sourceRoute is not null : sourceRoute is null || draft.Original is null ||
            !VehicleRouteEquivalentUx2NavL(sourceRoute, draft.Original))
        {
            message = "La route source a été ajoutée, remplacée ou modifiée hors de cette fiche.";
            return false;
        }
        if (draft.Remove)
        {
            if (draft.IsNew || sourceRoute is null)
            {
                message = "Une route non appliquée ne peut pas être retirée du projet.";
                return false;
            }
            if (catalog.Encounters.Any(item => item.Reinforcements?.RouteId == draft.RouteId) ||
                catalog.Merchants.Any(item => item.Restock.SupplierRouteId == draft.RouteId))
            {
                message = "Cette route est encore référencée par des renforts ou un réapprovisionnement marchand.";
                diagnostics =
                [
                    new("studio.vehicle_route.in_use", "route/references", message),
                ];
                return false;
            }
        }
        else
        {
            diagnostics = ValidateVehicleRouteDraftUx2NavL(draft, sourceRoute);
            if (diagnostics.Count > 0)
            {
                message = diagnostics[0].Message;
                return false;
            }
        }
        try
        {
            var routes = catalog.VehicleRoutes.ToList();
            if (draft.Remove)
            {
                routes.RemoveAll(item => item.Id == draft.RouteId);
            }
            else
            {
                var points = draft.Points.Select(item =>
                {
                    var current = item.Original;
                    return current is not null && VehiclePointMatchesUx2NavL(current, item)
                        ? current
                        : new VehicleRouteProfile.RoutePoint(item.Id,
                            new Vector3Value((float)item.PositionX, (float)item.PositionY, (float)item.PositionZ),
                            item.TangentDegrees, item.Width);
                }).ToArray();
                var lanes = draft.Lanes.Select(item =>
                {
                    var current = item.Original;
                    return current is not null && VehicleLaneMatchesUx2NavL(current, item)
                        ? current
                        : new VehicleRouteProfile.Lane(item.Id.Trim(), item.LateralOffset,
                            item.SpeedLimit, item.Direction);
                }).ToArray();
                var stops = draft.Stops.Select(item =>
                {
                    var current = item.Original;
                    return current is not null && VehicleStopMatchesUx2NavL(current, item)
                        ? current
                        : new VehicleRouteProfile.Stop(item.Id, item.RoutePointId, item.WaitSeconds,
                            item.ServiceId.Trim());
                }).ToArray();
                var built = sourceRoute is not null && sourceRoute.AreaId == draft.AreaId &&
                            sourceRoute.Points.SequenceEqual(points) && sourceRoute.Lanes.SequenceEqual(lanes) &&
                            sourceRoute.Stops.SequenceEqual(stops) &&
                            sourceRoute.DefaultSpeedLimit.Equals(draft.DefaultSpeedLimit) &&
                            sourceRoute.AllowOvertaking == draft.AllowOvertaking &&
                            sourceRoute.NetworkSyncMode == draft.NetworkSyncMode
                    ? sourceRoute
                    : new VehicleRouteProfile(draft.RouteId, WorldFeatureCatalog.CurrentSchemaVersion,
                        sourceRoute?.Provenance ?? Ux2NavLProvenance, draft.AreaId, points, lanes, stops,
                        draft.DefaultSpeedLimit, draft.AllowOvertaking, draft.NetworkSyncMode);
                candidateRoute = built;
                if (draft.IsNew) routes.Add(built);
                else
                {
                    var index = routes.FindIndex(item => item.Id == draft.RouteId);
                    routes[index] = built;
                }
            }
            var candidateCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion, catalog.Provenance,
                catalog.Placeables, catalog.Doors, catalog.Triggers, catalog.Encounters, routes,
                catalog.Merchants, catalog.Companions, catalog.Scripts);
            candidateProject = new ProjectDefinition(project.Id, project.Title,
                project.Module.WithWorldFeatures(candidateCatalog), project.Blueprints, project.Areas,
                project.Dialogues, project.Quests);
            command = new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), draft.BaseRevision,
                candidateCatalog, dryRun);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           OverflowException)
        {
            message = HumanDiagnosticUx2NavL(exception.Message);
            diagnostics = [new("studio.vehicle_route.profile_invalid", "routes-vehicules", message)];
            return false;
        }
    }

    private static IReadOnlyList<StudioUx2NavLDiagnostic> ValidateVehicleRouteDraftUx2NavL(
        StudioUx2NavLDraft draft, VehicleRouteProfile? currentRoute)
    {
        var found = new List<StudioUx2NavLDiagnostic>();
        void Add(string code, string path, string message) => found.Add(new(code, path, message));
        if (draft.Points.Count is < 2 or > 64)
            Add("studio.vehicle_route.point_count", "route/points",
                "Une route de véhicules doit contenir entre 2 et 64 points.");
        if (draft.Lanes.Count is < 1 or > 16)
            Add("studio.vehicle_route.lane_count", "route/voies",
                "Une route de véhicules doit contenir entre 1 et 16 voies.");
        if (draft.Stops.Count > 64)
            Add("studio.vehicle_route.stop_count", "route/arrets",
                "Une route de véhicules ne peut pas dépasser 64 arrêts.");
        var pointIds = draft.Points.Select(item => item.Id).ToArray();
        if (pointIds.Any(item => item == Guid.Empty) || pointIds.Distinct().Count() != pointIds.Length)
            Add("studio.vehicle_route.point_identity", "route/points",
                "Chaque point doit conserver une identité interne unique.");
        var currentPointIds = currentRoute?.Points.Select(item => item.Id).ToHashSet() ?? [];
        if (draft.Points.Any(item => item.IsNew == currentPointIds.Contains(item.Id)))
            Add("studio.vehicle_route.point_source", "route/points",
                "Un point a été ajouté ou remplacé hors de cette fiche.");
        for (var index = 0; index < draft.Points.Count; index++)
        {
            var item = draft.Points[index];
            if (!BoundedUx2NavL(item.PositionX, Ux2NavLMaximumCoordinate) ||
                !BoundedUx2NavL(item.PositionY, Ux2NavLMaximumCoordinate) ||
                !BoundedUx2NavL(item.PositionZ, Ux2NavLMaximumCoordinate) ||
                !BoundedUx2NavL(item.TangentDegrees, 360_000) ||
                !PositiveBoundedUx2NavL(item.Width))
                Add("studio.vehicle_route.point_bounds", $"route/point-{index + 1}",
                    "Position, tangente et largeur doivent rester finies et dans les limites de la fiche.");
        }
        var currentLanes = currentRoute?.Lanes.ToArray() ?? [];
        var laneIds = draft.Lanes.Select(item => item.Id).ToArray();
        if (laneIds.Distinct(StringComparer.Ordinal).Count() != laneIds.Length)
            Add("studio.vehicle_route.lane_identity", "route/voies",
                "Chaque voie doit conserver une clé unique.");
        for (var index = 0; index < draft.Lanes.Count; index++)
        {
            var item = draft.Lanes[index];
            var original = item.Original;
            if (item.IsNew ? original is not null : original is null || !currentLanes.Contains(original))
                Add("studio.vehicle_route.lane_source", $"route/voie-{index + 1}",
                    "Une voie a été ajoutée ou remplacée hors de cette fiche.");
            if (item.IdIsProtected ? original is null || item.Id != original.Id :
                !SafeHumanIdentifierUx2NavL(item.Id))
                Add("studio.vehicle_route.lane_id", $"route/voie-{index + 1}/cle",
                    "La clé de voie doit être humaine, sûre et ne pas contenir d’UUID.");
            if (!BoundedUx2NavL(item.LateralOffset, Ux2NavLMaximumScalar) ||
                !PositiveBoundedUx2NavL(item.SpeedLimit))
                Add("studio.vehicle_route.lane_bounds", $"route/voie-{index + 1}",
                    "Décalage et vitesse de voie doivent rester finis et dans les limites de la fiche.");
            if (item.DirectionIsProtected ? original is null || item.Direction != original.Direction :
                !Ux2NavLDirections.Contains(item.Direction, StringComparer.Ordinal))
                Add("studio.vehicle_route.lane_direction", $"route/voie-{index + 1}/sens",
                    "Choisissez un sens avant, arrière ou bidirectionnel.");
        }
        var currentStopIds = currentRoute?.Stops.Select(item => item.Id).ToHashSet() ?? [];
        var stopIds = draft.Stops.Select(item => item.Id).ToArray();
        if (stopIds.Any(item => item == Guid.Empty) || stopIds.Distinct().Count() != stopIds.Length)
            Add("studio.vehicle_route.stop_identity", "route/arrets",
                "Chaque arrêt doit conserver une identité interne unique.");
        for (var index = 0; index < draft.Stops.Count; index++)
        {
            var item = draft.Stops[index];
            var original = item.Original;
            if (item.IsNew == currentStopIds.Contains(item.Id))
                Add("studio.vehicle_route.stop_source", $"route/arret-{index + 1}",
                    "Un arrêt a été ajouté ou remplacé hors de cette fiche.");
            if (!pointIds.Contains(item.RoutePointId))
                Add("studio.vehicle_route.stop_point", $"route/arret-{index + 1}/point",
                    "Chaque arrêt doit viser un point de la route courante.");
            if (!BoundedUx2NavL(item.WaitSeconds, Ux2NavLMaximumScalar) || item.WaitSeconds < 0)
                Add("studio.vehicle_route.stop_wait", $"route/arret-{index + 1}/attente",
                    "L’attente doit être finie, positive ou nulle et dans les limites de la fiche.");
            if (item.ServiceIdIsProtected ? original is null || item.ServiceId != original.ServiceId :
                !SafeHumanIdentifierUx2NavL(item.ServiceId))
                Add("studio.vehicle_route.stop_service", $"route/arret-{index + 1}/service",
                    "Le service d’arrêt doit être une clé humaine sûre, sans UUID.");
        }
        if (!PositiveBoundedUx2NavL(draft.DefaultSpeedLimit))
            Add("studio.vehicle_route.default_speed", "route/vitesse",
                "La vitesse globale doit être finie, positive et dans les limites de la fiche.");
        if (!Ux2NavLSyncModes.Contains(draft.NetworkSyncMode, StringComparer.Ordinal))
            Add("studio.vehicle_route.sync", "route/synchronisation",
                "Choisissez snapshots serveur, prédiction bornée ou mode local.");
        return found;
    }

    private static StudioUx2NavLRuntimePreview BuildVehicleRuntimePreviewUx2NavL(VehicleRouteProfile route)
    {
        var samples = new[] { 0d, .25d, .5d, .75d, 1d }.Select(progress =>
        {
            var sample = WorldFeatureRuntimeResolver.SampleVehicleRoute(route, progress);
            return new StudioUx2NavLRuntimeSample((int)Math.Round(progress * 100), sample.Position.X,
                sample.Position.Y, sample.Position.Z, sample.SpeedLimit, sample.AtStop, sample.WaitSeconds);
        }).ToArray();
        return new(samples,
            "SampleVehicleRoute résout uniquement position, vitesse et arrêt. Tangentes, largeur, offsets, sens, " +
            "dépassement et synchronisation sont persistés mais non exécutés ; aucune physique, conduite, " +
            "réplication ou autorité réseau n’est simulée.");
    }

    private static StudioNavigationOverlaySnapshot BuildVehicleRouteOverlayUx2NavL(VehicleRouteProfile route)
    {
        var markers = route.Points.Select((item, index) => new StudioNavigationOverlayMarker(
            $"ux2-nav-l:{route.Id:D}:point:{item.Id:D}", $"Point {index + 1}",
            StudioNavigationOverlaySemantic.Route, PointUx2NavL(item.Position), Selected: true)).ToArray();
        var segments = route.Points.Zip(route.Points.Skip(1), (left, right) =>
            new StudioNavigationOverlaySegment($"ux2-nav-l:{route.Id:D}:segment:{left.Id:D}:{right.Id:D}",
                "Voie de véhicules", StudioNavigationOverlaySemantic.Route,
                PointUx2NavL(left.Position), PointUx2NavL(right.Position), Directed: true,
                Selected: true)).ToArray();
        return new(markers, segments, []);
    }

    private static IReadOnlyList<string> BuildVehicleRouteDiffUx2NavL(VehicleRouteProfile? source,
        VehicleRouteProfile? candidate, bool remove)
    {
        if (remove && source is not null)
            return [$"Retirer la route et ses {source.Points.Count} point(s), {source.Lanes.Count} voie(s) et " +
                    $"{source.Stops.Count} arrêt(s)."];
        if (candidate is null) return [];
        if (source is null)
            return [$"Créer une route avec {candidate.Points.Count} point(s), {candidate.Lanes.Count} voie(s) " +
                    $"et {candidate.Stops.Count} arrêt(s)."];
        if (VehicleRouteEquivalentUx2NavL(source, candidate)) return [];
        var diff = new List<string>();
        if (!source.Points.SequenceEqual(candidate.Points))
            diff.Add("Points ordonnés : " + string.Join(" → ", candidate.Points.Select((_, index) =>
                $"point {index + 1}")) + ".");
        if (!source.Lanes.SequenceEqual(candidate.Lanes))
            diff.Add($"Voies : {candidate.Lanes.Count} · " + string.Join(", ", candidate.Lanes.Select(item =>
                $"{HumanIdentifierUx2NavL(item.Id, "voie protégée")} " +
                $"{VehicleDirectionLabelUx2NavL(item.Direction)} " +
                $"à {item.SpeedLimit:0.##}")) + ".");
        if (!source.Stops.SequenceEqual(candidate.Stops))
            diff.Add($"Arrêts : {candidate.Stops.Count} · attentes " +
                     string.Join(", ", candidate.Stops.Select(item => $"{item.WaitSeconds:0.##} s")) + ".");
        if (!source.DefaultSpeedLimit.Equals(candidate.DefaultSpeedLimit))
            diff.Add($"Vitesse globale : {source.DefaultSpeedLimit:0.##} → {candidate.DefaultSpeedLimit:0.##}.");
        if (source.AllowOvertaking != candidate.AllowOvertaking)
            diff.Add(candidate.AllowOvertaking ? "Dépassement autorisé." : "Dépassement interdit.");
        if (source.NetworkSyncMode != candidate.NetworkSyncMode)
            diff.Add($"Synchronisation : {SyncLabelUx2NavL(candidate.NetworkSyncMode)}.");
        if (diff.Count == 0) diff.Add("Configuration de la route mise à jour.");
        return diff;
    }

    private static StudioUx2NavLPointDraft PointDraftUx2NavL(VehicleRouteProfile.RoutePoint item) => new(
        item.Id, false, item, item.Position.X, item.Position.Y, item.Position.Z,
        item.TangentDegrees, item.Width);

    private static StudioUx2NavLLaneDraft LaneDraftUx2NavL(VehicleRouteProfile.Lane item) => new(
        false, item, !SafeHumanIdentifierUx2NavL(item.Id), item.Id,
        item.LateralOffset, item.SpeedLimit,
        !Ux2NavLDirections.Contains(item.Direction, StringComparer.Ordinal), item.Direction);

    private static StudioUx2NavLStopDraft StopDraftUx2NavL(VehicleRouteProfile.Stop item) => new(
        item.Id, false, item, item.RoutePointId, item.WaitSeconds,
        !SafeHumanIdentifierUx2NavL(item.ServiceId), item.ServiceId);

    private static bool VehiclePointMatchesUx2NavL(VehicleRouteProfile.RoutePoint source,
        StudioUx2NavLPointDraft draft) => source.Id == draft.Id && source.Position.X == (float)draft.PositionX &&
        source.Position.Y == (float)draft.PositionY && source.Position.Z == (float)draft.PositionZ &&
        source.TangentDegrees.Equals(draft.TangentDegrees) && source.Width.Equals(draft.Width);

    private static bool VehicleLaneMatchesUx2NavL(VehicleRouteProfile.Lane source,
        StudioUx2NavLLaneDraft draft) => source.Id == draft.Id.Trim() &&
        source.LateralOffset.Equals(draft.LateralOffset) && source.SpeedLimit.Equals(draft.SpeedLimit) &&
        source.Direction == draft.Direction;

    private static bool VehicleStopMatchesUx2NavL(VehicleRouteProfile.Stop source,
        StudioUx2NavLStopDraft draft) => source.Id == draft.Id && source.RoutePointId == draft.RoutePointId &&
        source.WaitSeconds.Equals(draft.WaitSeconds) && source.ServiceId == draft.ServiceId.Trim();

    private static bool VehicleRouteEquivalentUx2NavL(VehicleRouteProfile left, VehicleRouteProfile right) =>
        left.Id == right.Id && left.SchemaVersion == right.SchemaVersion && left.Provenance == right.Provenance &&
        left.AreaId == right.AreaId && left.Points.SequenceEqual(right.Points) &&
        left.Lanes.SequenceEqual(right.Lanes) && left.Stops.SequenceEqual(right.Stops) &&
        left.DefaultSpeedLimit.Equals(right.DefaultSpeedLimit) &&
        left.AllowOvertaking == right.AllowOvertaking && left.NetworkSyncMode == right.NetworkSyncMode;

    private static bool VehicleRouteEditableUx2NavL(VehicleRouteProfile route, out string? reason)
    {
        reason = null;
        var pointIds = route.Points.Select(item => item.Id).ToArray();
        var laneIds = route.Lanes.Select(item => item.Id).ToArray();
        var stopIds = route.Stops.Select(item => item.Id).ToArray();
        if (route.Id == Guid.Empty || route.SchemaVersion != WorldFeatureCatalog.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(route.Provenance) || !ProjectAreaIdentityValidUx2NavL(route.AreaId))
            reason = "L’identité, le schéma ou la provenance du profil n’est pas éditable par cette fiche.";
        else if (route.Points.Count is < 2 or > 64 || route.Lanes.Count is < 1 or > 16 ||
                 route.Stops.Count > 64)
            reason = "Le profil dépasse les limites de 64 points, 16 voies ou 64 arrêts.";
        else if (pointIds.Any(item => item == Guid.Empty) || pointIds.Distinct().Count() != pointIds.Length ||
                 laneIds.Distinct(StringComparer.Ordinal).Count() != laneIds.Length ||
                 laneIds.Any(item => !SafeCanonicalIdentifierUx2NavL(item)) ||
                 route.Lanes.Any(item => !SafeCanonicalIdentifierUx2NavL(item.Direction)) ||
                 stopIds.Any(item => item == Guid.Empty) || stopIds.Distinct().Count() != stopIds.Length ||
                 route.Stops.Any(item => !pointIds.Contains(item.RoutePointId) ||
                                         !SafeCanonicalIdentifierUx2NavL(item.ServiceId)))
            reason = "Le profil contient une identité dupliquée, absente ou non sûre.";
        else if (route.Points.Any(item => !BoundedUx2NavL(item.Position.X, Ux2NavLMaximumCoordinate) ||
                                          !BoundedUx2NavL(item.Position.Y, Ux2NavLMaximumCoordinate) ||
                                          !BoundedUx2NavL(item.Position.Z, Ux2NavLMaximumCoordinate) ||
                                          !BoundedUx2NavL(item.TangentDegrees, 360_000) ||
                                          !PositiveBoundedUx2NavL(item.Width)) ||
                 route.Lanes.Any(item => !BoundedUx2NavL(item.LateralOffset, Ux2NavLMaximumScalar) ||
                                         !PositiveBoundedUx2NavL(item.SpeedLimit)) ||
                 route.Stops.Any(item => !BoundedUx2NavL(item.WaitSeconds, Ux2NavLMaximumScalar) ||
                                         item.WaitSeconds < 0) ||
                 !PositiveBoundedUx2NavL(route.DefaultSpeedLimit))
            reason = "Le profil contient une valeur non finie ou hors des bornes éditables ; aucun clamp n’est appliqué.";
        else if (!Ux2NavLSyncModes.Contains(route.NetworkSyncMode, StringComparer.Ordinal))
            reason = "Le mode de synchronisation existant n’est pas éditable par cette fiche.";
        return reason is null;
    }

    private static bool ProjectAreaIdentityValidUx2NavL(AreaId? value) => value is not null;

    private static bool SafeCanonicalIdentifierUx2NavL(string value) =>
        !string.IsNullOrWhiteSpace(value) && !value.Contains("..", StringComparison.Ordinal) &&
        value.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_' or ':' or '/');

    private static bool BoundedUx2NavL(double value, double maximum) =>
        double.IsFinite(value) && Math.Abs(value) <= maximum;

    private static bool PositiveBoundedUx2NavL(double value) =>
        double.IsFinite(value) && value > 0 && value <= Ux2NavLMaximumScalar;

    private static bool SafeHumanIdentifierUx2NavL(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length is >= 2 and <= 96 && !trimmed.Contains("..", StringComparison.Ordinal) &&
               trimmed.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_' or ':' or '/') &&
               !ContainsTechnicalIdentifierUx2NavL(trimmed);
    }

    private static string HumanIdentifierUx2NavL(string value, string fallback) =>
        ContainsTechnicalIdentifierUx2NavL(value) || string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static string HumanUx2NavL(string? value, string fallback)
    {
        var candidate = value?.Trim();
        return string.IsNullOrWhiteSpace(candidate) || ContainsTechnicalIdentifierUx2NavL(candidate)
            ? fallback : candidate;
    }

    private static bool ContainsTechnicalIdentifierUx2NavL(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (Guid.TryParse(value, out _) || Regex.IsMatch(value,
            "(?i)[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}"));

    private static string HumanDiagnosticUx2NavL(string? value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "Configuration refusée." : value.Trim();
        return Regex.Replace(text, "(?i)[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}",
            "identité interne");
    }

    internal static string VehicleDirectionLabelUx2NavL(string value) => value switch
    {
        "forward" => "Avant",
        "reverse" => "Arrière",
        "bidirectional" => "Bidirectionnel",
        _ => "Sens existant protégé",
    };

    internal static string SyncLabelUx2NavL(string value) => value switch
    {
        "server-snapshots" => "Snapshots serveur",
        "bounded-prediction" => "Prédiction bornée",
        "local" => "Locale",
        _ => "Mode existant protégé",
    };

    private static StudioNavigationPoint PointUx2NavL(Vector3Value value) => new(value.X, value.Y, value.Z);

    private static StudioUx2NavLPreview FailedVehicleRoutePreviewUx2NavL(string message,
        IReadOnlyList<StudioUx2NavLDiagnostic>? diagnostics = null) => new(false, message,
        "Aucun aperçu canonique disponible.", false, [], diagnostics ?? [],
        StudioNavigationOverlaySnapshot.Empty, null);

    private static string VehicleRouteSourceFingerprintUx2NavL(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));

    private static string VehicleRouteDraftFingerprintUx2NavL(StudioUx2NavLDraft draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(draft.AreaId.Value.ToByteArray());
        writer.Write(draft.RouteId.ToByteArray());
        writer.Write(draft.IsNew);
        writer.Write(draft.Remove);
        writer.Write(draft.BaseRevision.Value);
        WriteUx2NavL(writer, draft.SourceFingerprint);
        writer.Write(draft.Points.Count);
        foreach (var item in draft.Points)
        {
            writer.Write(item.Id.ToByteArray());
            writer.Write(item.IsNew);
            writer.Write(item.PositionX);
            writer.Write(item.PositionY);
            writer.Write(item.PositionZ);
            writer.Write(item.TangentDegrees);
            writer.Write(item.Width);
        }
        writer.Write(draft.Lanes.Count);
        foreach (var item in draft.Lanes)
        {
            writer.Write(item.IsNew);
            writer.Write(item.IdIsProtected);
            WriteUx2NavL(writer, item.Id);
            writer.Write(item.LateralOffset);
            writer.Write(item.SpeedLimit);
            writer.Write(item.DirectionIsProtected);
            WriteUx2NavL(writer, item.Direction);
        }
        writer.Write(draft.Stops.Count);
        foreach (var item in draft.Stops)
        {
            writer.Write(item.Id.ToByteArray());
            writer.Write(item.IsNew);
            writer.Write(item.RoutePointId.ToByteArray());
            writer.Write(item.WaitSeconds);
            writer.Write(item.ServiceIdIsProtected);
            WriteUx2NavL(writer, item.ServiceId);
        }
        writer.Write(draft.DefaultSpeedLimit);
        writer.Write(draft.AllowOvertaking);
        WriteUx2NavL(writer, draft.NetworkSyncMode);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteUx2NavL(BinaryWriter writer, string? value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static Guid UniqueGuidUx2NavL(IEnumerable<Guid> existing)
    {
        var values = existing.ToHashSet();
        var id = Guid.NewGuid();
        while (values.Contains(id)) id = Guid.NewGuid();
        return id;
    }

    internal ProjectDefinition? CreateUx2NavLEvidenceProject()
    {
        var source = Project;
        return source is null ? null : CreateUx2NavLEvidenceProject(source.Id.Value);
    }

    private static ProjectDefinition CreateUx2NavLEvidenceProject(Guid seed)
    {
        var project = CreateUx2NavJEvidenceProject(seed == Guid.Empty
            ? Guid.Parse("2e120000-0000-4000-8000-000000000001") : seed);
        var area = project.Areas[0];
        var routeId = StableUx2NavL(seed, "route:main");
        var first = new VehicleRouteProfile.RoutePoint(StableUx2NavL(seed, "point:first"),
            new Vector3Value(-10, 0, -4), 10, 3.5);
        var middle = new VehicleRouteProfile.RoutePoint(StableUx2NavL(seed, "point:middle"),
            new Vector3Value(0, 1, 3), 25, 4);
        var last = new VehicleRouteProfile.RoutePoint(StableUx2NavL(seed, "point:last"),
            new Vector3Value(12, 0, 8), -15, 3.5);
        var route = new VehicleRouteProfile(routeId, 1, Ux2NavLEvidenceProvenance, area.Id,
            [first, middle, last],
            [new("service", 0, 9, "forward"), new("retour", -2.5, 7, "reverse")],
            [new(StableUx2NavL(seed, "stop:middle"), middle.Id, 3, "halte-marche"),
             new(StableUx2NavL(seed, "stop:last"), last.Id, 6, "terminus-lisiere")],
            10, false, "server-snapshots");
        var neighborFirst = new VehicleRouteProfile.RoutePoint(StableUx2NavL(seed, "neighbor:first"),
            new Vector3Value(-6, 0, 10), 0, 3);
        var neighborLast = new VehicleRouteProfile.RoutePoint(StableUx2NavL(seed, "neighbor:last"),
            new Vector3Value(7, 0, 10), 0, 3);
        var neighbor = new VehicleRouteProfile(StableUx2NavL(seed, "route:neighbor"), 1,
            "fixture:ux2-nav-l:neighbor", area.Id, [neighborFirst, neighborLast],
            [new("navette", 0, 6, "bidirectional")], [], 6, true, "local");
        var outOfRange = new VehicleRouteProfile(StableUx2NavL(seed, "route:out-of-range"), 1,
            "fixture:ux2-nav-l:out-of-range", area.Id,
            [neighborFirst with { Id = StableUx2NavL(seed, "range:first") },
             neighborLast with
             {
                 Id = StableUx2NavL(seed, "range:last"),
                 Position = new Vector3Value((float)(Ux2NavLMaximumCoordinate + 1), 0, 0),
             }],
            [new("protegee", 0, 5, "forward")], [], 5, false, "server-snapshots");
        var source = project.Module.WorldFeatures;
        var catalog = new WorldFeatureCatalog(source.Id, source.SchemaVersion, source.Provenance,
            source.Placeables, source.Doors, source.Triggers, source.Encounters,
            source.VehicleRoutes.Concat([route, neighbor, outOfRange]).ToArray(), source.Merchants,
            source.Companions, source.Scripts);
        return new ProjectDefinition(project.Id, project.Title, project.Module.WithWorldFeatures(catalog),
            project.Blueprints, project.Areas, project.Dialogues, project.Quests);
    }

    private static Guid StableUx2NavL(Guid seed, string discriminator)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"ux2-nav-l:{seed:D}:{discriminator}"));
        Span<byte> id = stackalloc byte[16];
        bytes.AsSpan(0, id.Length).CopyTo(id);
        id[6] = (byte)((id[6] & 0x0f) | 0x40);
        id[8] = (byte)((id[8] & 0x3f) | 0x80);
        return new Guid(id);
    }

    internal StudioUx2NavLSmokeResult RunUx2NavLContractSmoke()
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
        var sourceApproval = CaptureVehicleRouteApprovalUx2NavL();
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-nav-l-smoke-" + Guid.NewGuid().ToString("N"));
        StudioSessionController? editing = null;
        StudioSessionController? reopened = null;
        try
        {
            var fixture = CreateUx2NavLEvidenceProject(
                Guid.Parse("2e120000-0000-4000-8000-000000000099"));
            if (!ProjectSession.Create(tempRoot, fixture).Succeeded) return FailedUx2NavLSmoke();
            editing = new StudioSessionController();
            if (!editing.Open(tempRoot).Succeeded) return FailedUx2NavLSmoke();
            var areaChoice = editing.VehicleRouteAreaChoicesUx2NavL.Single(item => item.IsEvidence);
            var routeChoice = editing.VehicleRouteChoicesUx2NavL(areaChoice.AreaId).Single(item => item.IsEvidence);
            var explicitSelection = editing.CreateVehicleRouteDraftUx2NavL(null, null) is null &&
                                    editing.CreateVehicleRouteDraftUx2NavL(areaChoice.AreaId, null) is null &&
                                    editing.CreateVehicleRouteDraftUx2NavL(null, routeChoice.RouteId) is null &&
                                    editing.CreateVehicleRouteDraftUx2NavL(areaChoice.AreaId,
                                        routeChoice.RouteId) is not null;
            var humanLabels = editing.VehicleRouteAreaChoicesUx2NavL.All(item =>
                                  !ContainsTechnicalIdentifierUx2NavL(item.Label)) &&
                              editing.VehicleRouteChoicesUx2NavL(areaChoice.AreaId).All(item =>
                                  !ContainsTechnicalIdentifierUx2NavL(item.Label));
            var readOnlyChoice = editing.VehicleRouteChoicesUx2NavL(areaChoice.AreaId)
                .Single(item => !item.IsEditable);
            var noSilentClamp = readOnlyChoice.ReadOnlyReason?.Contains("aucun clamp", StringComparison.Ordinal) ==
                                true && editing.CreateVehicleRouteDraftUx2NavL(areaChoice.AreaId,
                                    readOnlyChoice.RouteId) is null;
            var original = editing.CreateVehicleRouteDraftUx2NavL(areaChoice.AreaId, routeChoice.RouteId)!;
            var fullSurface = original.Points.Count == 3 && original.Lanes.Count == 2 && original.Stops.Count == 2 &&
                              editing.CreateNewVehiclePointUx2NavL(original) is not null &&
                              editing.CreateNewVehicleLaneUx2NavL(original) is not null &&
                              editing.CreateNewVehicleStopUx2NavL(original) is not null;

            var invalidPoint = editing.PreviewVehicleRouteUx2NavL(original with
            {
                Points = original.Points.Select((item, index) => index == 1
                    ? item with { Id = original.Points[0].Id } : item).ToArray(),
            });
            var invalidLane = editing.PreviewVehicleRouteUx2NavL(original with
            {
                Lanes = original.Lanes.Select((item, index) => index == 0
                    ? item with { SpeedLimit = double.NaN } : item).ToArray(),
            });
            var invalidStop = editing.PreviewVehicleRouteUx2NavL(original with
            {
                Stops = original.Stops.Select((item, index) => index == 0
                    ? item with { RoutePointId = Guid.NewGuid() } : item).ToArray(),
            });
            var invalidSync = editing.PreviewVehicleRouteUx2NavL(original with
            {
                NetworkSyncMode = "pair-a-pair",
            });
            var invalidBounds = editing.PreviewVehicleRouteUx2NavL(original with
            {
                Points = original.Points.Select((item, index) => index == 0
                    ? item with { PositionX = Ux2NavLMaximumCoordinate + 1 } : item).ToArray(),
            });
            var validation = !invalidPoint.Succeeded && !invalidLane.Succeeded && !invalidStop.Succeeded &&
                             !invalidSync.Succeeded && !invalidBounds.Succeeded;

            var staleSource = editing.PreviewVehicleRouteUx2NavL(original with { SourceFingerprint = "stale" });
            var staleRevisionDraft = original;
            var neighborChanged = ApplyUx2NavLNeighborMutation(editing);
            var staleRevision = editing.PreviewVehicleRouteUx2NavL(staleRevisionDraft);
            var staleRevisionBlocked = neighborChanged && !staleRevision.Succeeded && editing.Undo().Succeeded;

            var approvalDraft = editing.CreateVehicleRouteDraftUx2NavL(areaChoice.AreaId,
                routeChoice.RouteId)! with { AllowOvertaking = true };
            var approvalPreview = editing.PreviewVehicleRouteUx2NavL(approvalDraft);
            neighborChanged = ApplyUx2NavLNeighborMutation(editing);
            var staleApply = editing.ApplyApprovedVehicleRouteUx2NavL();
            var staleApprovalBlocked = approvalPreview.Succeeded && neighborChanged &&
                                       !staleApply.Succeeded && editing.Undo().Succeeded;

            var fingerprintDraft = editing.CreateVehicleRouteDraftUx2NavL(areaChoice.AreaId,
                routeChoice.RouteId)! with { DefaultSpeedLimit = 11 };
            var fingerprintPreview = editing.PreviewVehicleRouteUx2NavL(fingerprintDraft);
            var fingerprintApproval = editing.CaptureVehicleRouteApprovalUx2NavL();
            editing.RestoreVehicleRouteApprovalUx2NavL(fingerprintApproval with
            {
                Draft = fingerprintApproval.Draft! with { DefaultSpeedLimit = 12 },
            });
            var fingerprintApply = editing.ApplyApprovedVehicleRouteUx2NavL();
            var approvalFingerprintBlocked = fingerprintPreview.Succeeded && !fingerprintApply.Succeeded;

            var createdDraft = editing.CreateNewVehicleRouteDraftUx2NavL(areaChoice.AreaId)!;
            var createPreview = editing.PreviewVehicleRouteUx2NavL(createdDraft);
            var createApply = editing.ApplyApprovedVehicleRouteUx2NavL();
            var created = createPreview.Succeeded && createApply.Succeeded &&
                          editing.Project!.Module.WorldFeatures.VehicleRoutes.Any(item =>
                              item.Id == createdDraft.RouteId);
            var removeDraft = editing.CreateVehicleRouteDraftUx2NavL(areaChoice.AreaId,
                createdDraft.RouteId)! with { Remove = true };
            var removePreview = editing.PreviewVehicleRouteUx2NavL(removeDraft);
            var removeApply = editing.ApplyApprovedVehicleRouteUx2NavL();
            var removed = removePreview.Succeeded && removeApply.Succeeded &&
                          editing.Project!.Module.WorldFeatures.VehicleRoutes.All(item =>
                              item.Id != createdDraft.RouteId);
            var restoreRemoved = editing.Undo().Succeeded && editing.Project!.Module.WorldFeatures.VehicleRoutes
                .Any(item => item.Id == createdDraft.RouteId);
            var restoreInitial = editing.Undo().Succeeded && editing.Project!.Module.WorldFeatures.VehicleRoutes
                .All(item => item.Id != createdDraft.RouteId);
            var replayCreated = editing.Redo().Succeeded;
            var replayRemoved = editing.Redo().Succeeded;
            var createEditRemove = created && removed && restoreRemoved && restoreInitial &&
                                   replayCreated && replayRemoved;

            var draft = editing.CreateVehicleRouteDraftUx2NavL(areaChoice.AreaId, routeChoice.RouteId)!;
            var sourceCatalog = editing.Project!.Module.WorldFeatures;
            var sourceArea = editing.Project.Areas.Single(item => item.Id == areaChoice.AreaId);
            var neighborRoute = sourceCatalog.VehicleRoutes.Single(item =>
                item.Provenance == "fixture:ux2-nav-l:neighbor");
            var configured = draft with
            {
                Points =
                [
                    draft.Points[2] with
                    {
                        PositionY = 2,
                        TangentDegrees = 35,
                        Width = 4.5,
                    },
                    draft.Points[0] with { PositionX = -12, Width = 4 },
                    draft.Points[1] with { PositionZ = 5, TangentDegrees = -20 },
                ],
                Lanes =
                [
                    draft.Lanes[1] with
                    {
                        LateralOffset = -3,
                        SpeedLimit = 8,
                        Direction = "bidirectional",
                    },
                    draft.Lanes[0] with { LateralOffset = 2, SpeedLimit = 11 },
                ],
                Stops =
                [
                    draft.Stops[1] with
                    {
                        RoutePointId = draft.Points[2].Id,
                        WaitSeconds = 8,
                        ServiceId = "terminus-haut",
                    },
                    draft.Stops[0] with { WaitSeconds = 4, ServiceId = "halte-basse" },
                ],
                DefaultSpeedLimit = 12,
                AllowOvertaking = true,
                NetworkSyncMode = "bounded-prediction",
            };
            var projectBeforeApply = editing.Project;
            var preview = editing.PreviewVehicleRouteUx2NavL(configured);
            var dryRun = preview.Succeeded && preview.CanApply &&
                         ReferenceEquals(projectBeforeApply, editing.Project);
            var runtimeSample = preview.Runtime is { Samples.Count: 5 } &&
                                preview.Runtime.Samples.All(item => item.SpeedLimit > 0) &&
                                preview.Runtime.Samples.Any(item => item.AtStop);
            var runtimeBoundary = preview.Runtime is { Boundary: var boundary } &&
                                  boundary.Contains("position, vitesse et arrêt",
                                      StringComparison.OrdinalIgnoreCase) &&
                                  boundary.Contains("non exécutés", StringComparison.OrdinalIgnoreCase) &&
                                  boundary.Contains("réplication", StringComparison.OrdinalIgnoreCase);
            var apply = editing.ApplyApprovedVehicleRouteUx2NavL();
            var appliedProject = editing.Project;
            var finalCatalog = appliedProject?.Module.WorldFeatures;
            var finalRoute = finalCatalog?.VehicleRoutes.Single(item => item.Id == configured.RouteId);
            var orderedCollections = finalRoute is not null &&
                                     finalRoute.Points.Select(item => item.Id)
                                         .SequenceEqual(configured.Points.Select(item => item.Id)) &&
                                     finalRoute.Lanes.Select(item => item.Id)
                                         .SequenceEqual(configured.Lanes.Select(item => item.Id)) &&
                                     finalRoute.Stops.Select(item => item.Id)
                                         .SequenceEqual(configured.Stops.Select(item => item.Id));
            var canonicalCommand = editing.History?.Journal.LastOrDefault()?.CommandName ==
                                   "set_world_feature_catalog";
            var worldFeatureNeighbors = finalCatalog is not null &&
                                        SameReferencesUx2NavL(sourceCatalog.Placeables, finalCatalog.Placeables) &&
                                        SameReferencesUx2NavL(sourceCatalog.Doors, finalCatalog.Doors) &&
                                        SameReferencesUx2NavL(sourceCatalog.Triggers, finalCatalog.Triggers) &&
                                        SameReferencesUx2NavL(sourceCatalog.Encounters, finalCatalog.Encounters) &&
                                        SameReferencesUx2NavL(sourceCatalog.Merchants, finalCatalog.Merchants) &&
                                        SameReferencesUx2NavL(sourceCatalog.Companions, finalCatalog.Companions) &&
                                        ReferenceEquals(sourceCatalog.Scripts, finalCatalog.Scripts) &&
                                        sourceCatalog.Provenance == finalCatalog.Provenance;
            var vehicleNeighbor = finalCatalog is not null &&
                                  sourceCatalog.VehicleRoutes.Where(item => item.Id != configured.RouteId)
                                      .All(source => ReferenceEquals(finalCatalog.VehicleRoutes.Single(item =>
                                          item.Id == source.Id), source)) &&
                                  ReferenceEquals(finalCatalog.VehicleRoutes.Single(item =>
                                      item.Id == neighborRoute.Id), neighborRoute);
            var finalArea = appliedProject?.Areas.Single(item => item.Id == sourceArea.Id);
            var navigationLots = finalArea is not null && ReferenceEquals(finalArea.Navigation, sourceArea.Navigation) &&
                                 ReferenceEquals(finalArea.World, sourceArea.World) &&
                                 ReferenceEquals(finalArea.Environment, sourceArea.Environment) &&
                                 ReferenceEquals(finalArea.RuntimePolicies, sourceArea.RuntimePolicies) &&
                                 ReferenceEquals(finalArea.Authoring, sourceArea.Authoring);
            var undo = editing.Undo();
            var undone = undo.Succeeded && ReferenceEquals(projectBeforeApply, editing.Project);
            var redo = editing.Redo();
            var redone = redo.Succeeded && ReferenceEquals(appliedProject, editing.Project);
            var undoRedo = undone && redone;
            var save = editing.Save();
            reopened = new StudioSessionController();
            var reopenedOk = save.Succeeded && reopened.Open(tempRoot).Succeeded;
            var loadedRoute = reopened.Project?.Module.WorldFeatures.VehicleRoutes.Single(item =>
                item.Id == configured.RouteId);
            var persistence = reopenedOk && finalRoute is not null && loadedRoute is not null &&
                              VehicleRouteEquivalentUx2NavL(loadedRoute, finalRoute);
            var projectNeighbors = appliedProject is not null && projectBeforeApply is not null &&
                                   appliedProject.Areas.SequenceEqual(projectBeforeApply.Areas) &&
                                   appliedProject.Blueprints.SequenceEqual(projectBeforeApply.Blueprints) &&
                                   appliedProject.Dialogues.SequenceEqual(projectBeforeApply.Dialogues) &&
                                   appliedProject.Quests.SequenceEqual(projectBeforeApply.Quests);
            var text = string.Join(' ', editing.VehicleRouteAreaChoicesUx2NavL.Select(item => item.Label)) +
                       " " + string.Join(' ', editing.VehicleRouteChoicesUx2NavL(areaChoice.AreaId)
                           .Select(item => item.Label)) + " " + preview.Summary + " " +
                       string.Join(' ', preview.Diff);
            var humanText = !ContainsTechnicalIdentifierUx2NavL(text) &&
                            preview.Overlay.Markers.All(item =>
                                !ContainsTechnicalIdentifierUx2NavL(item.HumanLabel));
            var sourceUntouched = ReferenceEquals(sourceProject, Project) &&
                                  ReferenceEquals(sourceHistory, History) && History?.State.Revision == sourceRevision &&
                                  (History?.Journal ?? []).SequenceEqual(sourceJournal) && IsDirty == sourceDirty &&
                                  History?.CanUndo == sourceUndo && History?.CanRedo == sourceRedo &&
                                  ActiveAreaId == sourceAreaId && SelectedEntityIds.SequenceEqual(sourceSelection) &&
                                  SelectedEntityId == sourceSelected &&
                                  CaptureVehicleRouteApprovalUx2NavL() == sourceApproval;
            var succeeded = explicitSelection && humanLabels && fullSurface && createEditRemove &&
                            orderedCollections && validation && noSilentClamp && dryRun && staleRevisionBlocked &&
                            !staleSource.Succeeded && staleApprovalBlocked && approvalFingerprintBlocked &&
                            canonicalCommand && undoRedo && persistence && runtimeSample && runtimeBoundary &&
                            worldFeatureNeighbors && vehicleNeighbor && navigationLots && projectNeighbors &&
                            sourceUntouched && humanText && apply.Succeeded;
            return new(succeeded, explicitSelection, humanLabels, fullSurface, createEditRemove,
                orderedCollections, validation, noSilentClamp, dryRun, staleRevisionBlocked,
                !staleSource.Succeeded, staleApprovalBlocked, approvalFingerprintBlocked, canonicalCommand,
                undoRedo, persistence, runtimeSample, runtimeBoundary, worldFeatureNeighbors, vehicleNeighbor,
                navigationLots, projectNeighbors, sourceUntouched, humanText);
        }
        catch
        {
            return FailedUx2NavLSmoke();
        }
        finally
        {
            reopened?.Close();
            editing?.Close();
            _ = DeleteUx2NavLTemporaryWorkspace(tempRoot);
        }
    }

    private static bool ApplyUx2NavLNeighborMutation(StudioSessionController controller)
    {
        var history = controller.History!;
        var source = history.State.Project.Module.WorldFeatures;
        var candidate = new WorldFeatureCatalog(source.Id, source.SchemaVersion,
            source.Provenance + ":neighbor", source.Placeables, source.Doors, source.Triggers,
            source.Encounters, source.VehicleRoutes, source.Merchants, source.Companions, source.Scripts);
        var result = controller._setVehicleRouteCatalogUx2NavL.Handle(history.State,
            new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                candidate, false));
        if (!result.Succeeded) return false;
        history.Record(result);
        return true;
    }

    private static bool SameReferencesUx2NavL<T>(IReadOnlyList<T> left, IReadOnlyList<T> right)
        where T : class => left.Count == right.Count && left.Zip(right).All(pair =>
            ReferenceEquals(pair.First, pair.Second));

    internal static bool DeleteUx2NavLTemporaryWorkspace(string? directory)
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
                !leaf.StartsWith("openrpg-forge-studio-ux2-nav-l-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, true);
            return !Directory.Exists(full);
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static StudioUx2NavLSmokeResult FailedUx2NavLSmoke() => new(
        Succeeded: false,
        ExplicitSelection: false,
        HumanLabels: false,
        FullSurface: false,
        CreateEditRemove: false,
        OrderedCollections: false,
        Validation: false,
        NoSilentClamp: false,
        DryRun: false,
        StaleRevisionBlocked: false,
        StaleSourceBlocked: false,
        StaleApprovalBlocked: false,
        ApprovalFingerprintBlocked: false,
        CanonicalCommand: false,
        UndoRedo: false,
        Persistence: false,
        RuntimeSample: false,
        RuntimeBoundary: false,
        WorldFeatureNeighborsPreserved: false,
        VehicleNeighborPreserved: false,
        NavigationLotsPreserved: false,
        ProjectNeighborsPreserved: false,
        SourceUntouched: false,
        HumanText: false);
}
