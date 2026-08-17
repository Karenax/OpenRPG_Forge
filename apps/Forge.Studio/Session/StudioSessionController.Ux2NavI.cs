using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Areas;
using Forge.Application.Commands;
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
    private const string Ux2NavIProvenance = "studio:ux2-nav-i:aerial-navigation";
    private const string Ux2NavIEvidenceProvenance = "studio:ux2-nav-i:evidence";

    private readonly SetAreaWorldProfileHandler _setAreaWorldUx2NavI = new(new SystemClock());
    private StudioUx2NavIDraft? _approvedAerialUx2NavI;
    private ProjectRevision? _approvedAerialRevisionUx2NavI;
    private string? _approvedAerialFingerprintUx2NavI;

    internal IReadOnlyList<StudioUx2NavIAreaChoice> AerialAreaChoicesUx2NavI
    {
        get
        {
            var project = Project;
            if (project is null)
            {
                return [];
            }
            var duplicates = project.Areas.GroupBy(item => HumanUx2NavI(item.DisplayName, "Zone"),
                    StringComparer.CurrentCultureIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(),
                    StringComparer.CurrentCultureIgnoreCase);
            return project.Areas.Select(area =>
                {
                    var name = HumanUx2NavI(area.DisplayName, "Zone");
                    var label = duplicates[name] > 1
                        ? $"{name} · {HumanUx2NavI(area.Slug, "zone")}" : name;
                    var evidence = string.Equals(area.World.Provenance, Ux2NavIEvidenceProvenance,
                                       StringComparison.Ordinal) ||
                                   area.World.FlightRoutes.Any(route => string.Equals(route.Provenance,
                                       Ux2NavIEvidenceProvenance, StringComparison.Ordinal));
                    return new StudioUx2NavIAreaChoice(area.Id, label, evidence);
                })
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.AreaId.Value)
                .ToArray();
        }
    }

    internal IReadOnlyList<StudioUx2NavIRouteChoice> AerialRouteChoicesUx2NavI(AreaId? areaId)
    {
        var area = areaId is null ? null : Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (area is null)
        {
            return [];
        }
        var duplicateNames = area.World.FlightRoutes.GroupBy(
                route => HumanUx2NavI(route.Name, "Route aérienne"),
                StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(),
                StringComparer.CurrentCultureIgnoreCase);
        return area.World.FlightRoutes.Select((route, index) =>
            {
                var name = HumanUx2NavI(route.Name, "Route aérienne");
                var label = duplicateNames[name] > 1
                    ? $"{name} · route {index + 1}"
                    : $"{name} · {route.Nodes.Count} étapes";
                return new StudioUx2NavIRouteChoice(route.Id, label,
                    string.Equals(route.Provenance, Ux2NavIEvidenceProvenance,
                        StringComparison.Ordinal));
            }).ToArray();
    }

    internal StudioUx2NavIDraft? CreateAerialDraftUx2NavI(AreaId? areaId)
    {
        var history = History;
        var area = areaId is null ? null : Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (history is null || area is null)
        {
            return null;
        }
        var profile = area.World.VolumetricNavigation;
        var volumes = profile.Obstacles.Select(item => VolumeDraftFromUx2NavI(
                item, StudioUx2NavIVolumeKind.Obstacle))
            .Concat(profile.CostVolumes.Select(item => VolumeDraftFromUx2NavI(
                item, StudioUx2NavIVolumeKind.Cost)))
            .ToArray();
        return new StudioUx2NavIDraft(area.Id, history.State.Revision,
            AerialSourceFingerprintUx2NavI(area), HumanUx2NavI(area.DisplayName, "Zone"),
            area.World, profile.Enabled, profile.Modes, profile.CellSize, profile.Center,
            profile.Size, profile.AvoidanceRadius, profile.DebugVisible, volumes, null);
    }

    internal StudioUx2NavIRouteDraft? CreateAerialRouteDraftUx2NavI(AreaId? areaId, Guid? routeId)
    {
        var route = areaId is null || routeId is null
            ? null
            : Project?.Areas.SingleOrDefault(item => item.Id == areaId)?.World.FlightRoutes
                .SingleOrDefault(item => item.Id == routeId.Value);
        return route is null ? null : RouteDraftFromUx2NavI(route);
    }

    internal StudioUx2NavIRouteDraft? CreateNewAerialRouteDraftUx2NavI(AreaId? areaId)
    {
        var area = areaId is null ? null : Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (area is null)
        {
            return null;
        }
        var profile = area.World.VolumetricNavigation;
        var halfX = Math.Max(profile.Size.X * .1f, Math.Min(profile.Size.X * .28f, 12));
        var halfZ = Math.Max(profile.Size.Z * .1f, Math.Min(profile.Size.Z * .28f, 12));
        var y = profile.Center.Y;
        return new StudioUx2NavIRouteDraft(Guid.NewGuid(), true, null, false, "Nouvelle route aérienne",
            FlightRouteCurve.CatmullRom, FlightObstaclePolicy.Avoid, false, true,
            [
                new(Guid.NewGuid(), true, null, false, "Départ", new(profile.Center.X - halfX, y,
                    profile.Center.Z - halfZ), 8, -12),
                new(Guid.NewGuid(), true, null, false, "Arrivée", new(profile.Center.X + halfX, y,
                    profile.Center.Z + halfZ), 8, 12),
            ]);
    }

    internal StudioUx2NavIVolumeDraft CreateNewNavigationVolumeDraftUx2NavI(
        StudioUx2NavIDraft draft, StudioUx2NavIVolumeKind kind)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var extent = (float)Math.Clamp(draft.CellSize * 2, 1, 128);
        var size = new Vector3Value(extent, extent, extent);
        return new StudioUx2NavIVolumeDraft(Guid.NewGuid(), true, kind, null, false,
            kind == StudioUx2NavIVolumeKind.Obstacle ? "Nouvel obstacle" : "Nouvelle zone de coût",
            draft.Center, size, kind == StudioUx2NavIVolumeKind.Obstacle ? 1 : 2);
    }

    private static StudioUx2NavIVolumeDraft VolumeDraftFromUx2NavI(
        NavigationVolumeDefinition volume, StudioUx2NavIVolumeKind kind) => new(
        volume.Id, false, kind, volume, ContainsTechnicalIdentifierUx2NavI(volume.Name), volume.Name,
        volume.Center, volume.Size,
        volume.CostMultiplier);

    private static StudioUx2NavIRouteDraft RouteDraftFromUx2NavI(FlightRouteDefinition route) => new(
        route.Id, false, route, ContainsTechnicalIdentifierUx2NavI(route.Name), route.Name, route.Curve,
        route.ObstaclePolicy, route.Closed,
        route.DebugVisible, route.Nodes.Select(node => new StudioUx2NavINodeDraft(node.Id, false,
            node, ContainsTechnicalIdentifierUx2NavI(node.Name), node.Name, node.Position, node.Speed,
            node.BankDegrees)).ToArray());

    internal StudioUx2NavIPreview PreviewAerialNavigationUx2NavI(StudioUx2NavIDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelAerialNavigationUx2NavI();
        var history = History;
        if (history is null || Project is null)
        {
            return FailedAerialPreviewUx2NavI("Aucun module n’est ouvert.");
        }
        if (!TryBuildAerialMutationUx2NavI(draft, true, out var command, out var sourceArea,
                out var candidateArea, out var diagnostics, out var message))
        {
            return FailedAerialPreviewUx2NavI(message, diagnostics);
        }
        var dryRun = _setAreaWorldUx2NavI.Handle(history.State, command!);
        if (!dryRun.Succeeded)
        {
            var commandDiagnostics = dryRun.Diagnostics.Select(item => new StudioUx2NavIDiagnostic(
                item.Code, HumanPathUx2NavI($"zone/{draft.AreaDisplayName}/monde"),
                HumanDiagnosticUx2NavI(item.Message))).ToArray();
            return FailedAerialPreviewUx2NavI(commandDiagnostics.FirstOrDefault()?.Message ??
                                              "L’aperçu aérien a été refusé.", commandDiagnostics);
        }

        var builtArea = candidateArea!;
        var route = draft.Route is null
            ? null
            : builtArea.World.FlightRoutes.Single(item => item.Id == draft.Route.RouteId);
        var diff = BuildAerialDiffUx2NavI(sourceArea!, builtArea, route, draft);
        var overlay = BuildAerialOverlayUx2NavI(builtArea.World.VolumetricNavigation, route);
        StudioUx2NavIRuntimePreview? runtime;
        try
        {
            runtime = BuildAerialRuntimePreviewUx2NavI(builtArea.World.VolumetricNavigation, route);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           OverflowException)
        {
            return FailedAerialPreviewUx2NavI(HumanDiagnosticUx2NavI(exception.Message),
            [
                new("studio.aerial.runtime_preview_invalid", "navigation-aerienne/apercu-runtime",
                    HumanDiagnosticUx2NavI(exception.Message)),
            ]);
        }
        if (diff.Count > 0)
        {
            _approvedAerialUx2NavI = draft;
            _approvedAerialRevisionUx2NavI = history.State.Revision;
            _approvedAerialFingerprintUx2NavI = AerialDraftFingerprintUx2NavI(draft);
        }
        var modeSummary = draft.Enabled
            ? HumanModesUx2NavI(draft.Modes)
            : "désactivée";
        var routeSummary = route is null
            ? "aucune route éditée"
            : $"{HumanUx2NavI(route.Name, "Route aérienne")} · {route.Nodes.Count} étapes · " +
              $"{HumanCurveUx2NavI(route.Curve)} · {HumanObstaclePolicyUx2NavI(route.ObstaclePolicy)}";
        return new StudioUx2NavIPreview(true,
            diff.Count == 0
                ? "Aperçu validé · aucune différence sémantique à appliquer."
                : "Aperçu sec validé · aucun acteur, nœud Godot, obstacle physique ou document n’a été modifié.",
            $"Volume : {modeSummary} · grille {runtime.GridCells:N0} cellules · " +
            $"{draft.Volumes.Count} volume(s) nommé(s)\nRoute : {routeSummary}",
            diff.Count > 0, diff, [], overlay, runtime);
    }

    internal StudioUx2NavIActionResult ApplyApprovedAerialNavigationUx2NavI()
    {
        var draft = _approvedAerialUx2NavI;
        var history = History;
        if (draft is null || history is null ||
            _approvedAerialRevisionUx2NavI != history.State.Revision ||
            !string.Equals(_approvedAerialFingerprintUx2NavI,
                AerialDraftFingerprintUx2NavI(draft), StringComparison.Ordinal))
        {
            CancelAerialNavigationUx2NavI();
            return new(false, "Prévisualisez de nouveau la navigation aérienne avant de l’appliquer.");
        }
        if (!TryBuildAerialMutationUx2NavI(draft, false, out var command, out _, out _, out _,
                out var message))
        {
            CancelAerialNavigationUx2NavI();
            return new(false, message);
        }
        var currentDryRun = _setAreaWorldUx2NavI.Handle(history.State,
            command! with { DryRun = true });
        if (!currentDryRun.Succeeded)
        {
            CancelAerialNavigationUx2NavI();
            return new(false, HumanDiagnosticUx2NavI(currentDryRun.Diagnostics.FirstOrDefault()?.Message));
        }
        var applied = _setAreaWorldUx2NavI.Handle(history.State, command!);
        CancelAerialNavigationUx2NavI();
        if (!applied.Succeeded)
        {
            return new(false, HumanDiagnosticUx2NavI(applied.Diagnostics.FirstOrDefault()?.Message));
        }
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true,
            "Navigation volumétrique et route aérienne appliquées · Ctrl+Z pour annuler, Ctrl+Y pour rétablir");
    }

    internal void CancelAerialNavigationUx2NavI()
    {
        _approvedAerialUx2NavI = null;
        _approvedAerialRevisionUx2NavI = null;
        _approvedAerialFingerprintUx2NavI = null;
    }

    internal void CloseUx2NavI() => CancelAerialNavigationUx2NavI();

    internal StudioUx2NavIApprovalSnapshot CaptureAerialApprovalUx2NavI() => new(
        _approvedAerialUx2NavI, _approvedAerialRevisionUx2NavI,
        _approvedAerialFingerprintUx2NavI);

    internal void RestoreAerialApprovalUx2NavI(StudioUx2NavIApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedAerialUx2NavI = snapshot.Draft;
        _approvedAerialRevisionUx2NavI = snapshot.Revision;
        _approvedAerialFingerprintUx2NavI = snapshot.Fingerprint;
    }

    private bool TryBuildAerialMutationUx2NavI(StudioUx2NavIDraft draft, bool dryRun,
        out SetAreaWorldProfileCommand? command, out AreaDefinition? sourceArea,
        out AreaDefinition? candidateArea, out IReadOnlyList<StudioUx2NavIDiagnostic> diagnostics,
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
        if (!string.Equals(draft.SourceFingerprint, AerialSourceFingerprintUx2NavI(sourceArea),
                StringComparison.Ordinal))
        {
            message = "Le profil de monde de cette zone a changé. Rechargez la fiche.";
            return false;
        }
        if (!ReferenceEquals(draft.OriginalWorld, sourceArea.World) &&
            !string.Equals(AreaWorldFingerprintUx2NavI(draft.OriginalWorld),
                AreaWorldFingerprintUx2NavI(sourceArea.World), StringComparison.Ordinal))
        {
            message = "Le profil source n’est plus celui qui a été sélectionné.";
            return false;
        }
        var inputDiagnostics = ValidateAerialDraftUx2NavI(draft);
        if (inputDiagnostics.Count > 0)
        {
            diagnostics = inputDiagnostics;
            message = inputDiagnostics[0].Message;
            return false;
        }

        try
        {
            var sourceWorld = sourceArea.World;
            var obstacles = draft.Volumes.Where(item => item.Kind == StudioUx2NavIVolumeKind.Obstacle)
                .Select(BuildNavigationVolumeUx2NavI).ToArray();
            var costs = draft.Volumes.Where(item => item.Kind == StudioUx2NavIVolumeKind.Cost)
                .Select(BuildNavigationVolumeUx2NavI).ToArray();
            var volume = BuildVolumetricProfileUx2NavI(sourceWorld.VolumetricNavigation,
                draft, obstacles, costs);
            IReadOnlyList<FlightRouteDefinition> routes = sourceWorld.FlightRoutes;
            if (draft.Route is { } routeDraft)
            {
                var current = sourceWorld.FlightRoutes.SingleOrDefault(item =>
                    item.Id == routeDraft.RouteId);
                if (routeDraft.IsNew && current is not null || !routeDraft.IsNew && current is null)
                {
                    message = routeDraft.IsNew
                        ? "Une route portant cette identité existe déjà. Recommencez l’ajout."
                        : "La route aérienne choisie n’existe plus. Rechargez la liste.";
                    return false;
                }
                var candidate = BuildFlightRouteUx2NavI(routeDraft, current);
                var updated = sourceWorld.FlightRoutes.ToList();
                if (current is null)
                {
                    updated.Add(candidate);
                }
                else
                {
                    updated[updated.FindIndex(item => item.Id == current.Id)] = candidate;
                }
                routes = updated;
            }
            if (routes.Count > 0 && (!volume.Enabled ||
                    !volume.Modes.HasFlag(VolumetricNavigationMode.Flight)))
            {
                diagnostics =
                [
                    new("area.world.flight_mode_required", "navigation-aerienne/modes",
                        "Les routes aériennes exigent une navigation volumétrique active en mode Vol."),
                ];
                message = diagnostics[0].Message;
                return false;
            }
            var candidateWorld = new AreaWorldProfile(sourceWorld.Id, sourceWorld.SchemaVersion,
                sourceWorld.Terrain, sourceWorld.Biomes, sourceWorld.Features, volume, routes,
                sourceWorld.Loading, sourceWorld.Provenance);
            candidateArea = sourceArea.WithWorld(candidateWorld);
            command = new SetAreaWorldProfileCommand(CommandId.From(Guid.NewGuid()),
                draft.BaseRevision, sourceArea.Id, candidateWorld, dryRun);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           OverflowException)
        {
            message = HumanDiagnosticUx2NavI(exception.Message);
            diagnostics =
            [
                new("studio.aerial.build_invalid", "navigation-aerienne",
                    HumanDiagnosticUx2NavI(exception.Message)),
            ];
            return false;
        }
    }

    private static IReadOnlyList<StudioUx2NavIDiagnostic> ValidateAerialDraftUx2NavI(
        StudioUx2NavIDraft draft)
    {
        var result = new List<StudioUx2NavIDiagnostic>();
        const VolumetricNavigationMode allModes = VolumetricNavigationMode.Flight |
                                                   VolumetricNavigationMode.Swim |
                                                   VolumetricNavigationMode.Space;
        if ((draft.Modes & ~allModes) != 0 || draft.Enabled && draft.Modes == VolumetricNavigationMode.None)
        {
            result.Add(new("area.world.volumetric_mode_invalid", "navigation-aerienne/modes",
                "Une navigation active requiert au moins un mode Vol, Nage ou Espace."));
        }
        if (!double.IsFinite(draft.CellSize) || draft.CellSize is < .5 or > 64 ||
            !double.IsFinite(draft.AvoidanceRadius) || draft.AvoidanceRadius is < 0 or > 64 ||
            !FiniteUx2NavI(draft.Center) || !FiniteUx2NavI(draft.Size) ||
            draft.Size.X <= 0 || draft.Size.Y <= 0 || draft.Size.Z <= 0)
        {
            result.Add(new("area.world.volumetric_bounds_invalid", "navigation-aerienne/volume-principal",
                "Le volume requiert une cellule de 0,5 à 64, un évitement de 0 à 64 et trois tailles positives."));
        }
        if (draft.Volumes.Select(item => item.Id).Distinct().Count() != draft.Volumes.Count ||
            draft.Volumes.Any(item => item.Id == Guid.Empty))
        {
            result.Add(new("area.world.volume_identity_invalid", "navigation-aerienne/volumes-nommes",
                "Chaque obstacle ou zone de coût doit conserver une identité stable distincte."));
        }
        for (var index = 0; index < draft.Volumes.Count; index++)
        {
            var item = draft.Volumes[index];
            if (item.NameIsProtected && (item.Original is null ||
                    !string.Equals(item.Name, item.Original.Name, StringComparison.Ordinal) ||
                    !ContainsTechnicalIdentifierUx2NavI(item.Name)) ||
                !item.NameIsProtected && !HumanNameValidUx2NavI(item.Name) ||
                !FiniteUx2NavI(item.Center) ||
                !FiniteUx2NavI(item.Size) || item.Size.X <= 0 || item.Size.Y <= 0 || item.Size.Z <= 0 ||
                !double.IsFinite(item.CostMultiplier) || item.CostMultiplier is < 0 or > 100)
            {
                result.Add(new("area.world.navigation_volume_invalid",
                    $"navigation-aerienne/volumes-nommes/volume-{index + 1}",
                    $"Le volume nommé {index + 1} requiert un nom lisible, trois tailles positives et un coût de 0 à 100."));
            }
        }
        if (draft.Route is { } route)
        {
            if (route.RouteId == Guid.Empty || route.NameIsProtected &&
                (route.Original is null ||
                 !string.Equals(route.Name, route.Original.Name, StringComparison.Ordinal) ||
                 !ContainsTechnicalIdentifierUx2NavI(route.Name)) || !route.NameIsProtected &&
                !HumanNameValidUx2NavI(route.Name) ||
                route.Nodes.Count < 2 || route.Nodes.Select(item => item.Id).Distinct().Count() != route.Nodes.Count ||
                route.Nodes.Any(item => item.Id == Guid.Empty))
            {
                result.Add(new("waypoint.flight.route_invalid", "navigation-aerienne/route-selectionnee",
                    "La route requiert un nom lisible et au moins deux étapes ordonnées aux identités stables."));
            }
            if (!draft.Enabled || !draft.Modes.HasFlag(VolumetricNavigationMode.Flight))
            {
                result.Add(new("waypoint.flight.mode_required", "navigation-aerienne/modes",
                    "Activez le mode Vol avant de configurer une route aérienne."));
            }
            for (var index = 0; index < route.Nodes.Count; index++)
            {
                var node = route.Nodes[index];
                var path = $"navigation-aerienne/route-selectionnee/etape-{index + 1}";
                if (node.NameIsProtected && (node.Original is null ||
                        !string.Equals(node.Name, node.Original.Name, StringComparison.Ordinal) ||
                        !ContainsTechnicalIdentifierUx2NavI(node.Name)) ||
                    !node.NameIsProtected && !HumanNameValidUx2NavI(node.Name) ||
                    !FiniteUx2NavI(node.Position) ||
                    !double.IsFinite(node.Speed) || node.Speed is <= 0 or > 500 ||
                    !double.IsFinite(node.BankDegrees) || node.BankDegrees is < -89 or > 89)
                {
                    result.Add(new("waypoint.flight.node_invalid", path,
                        $"L’étape {index + 1} requiert un nom, une position finie, une vitesse > 0–500 et une inclinaison -89–89°."));
                }
                if (!ContainsUx2NavI(draft.Center, draft.Size, node.Position))
                {
                    result.Add(new("area.world.flight_route_out_of_bounds", $"{path}/position",
                        $"L’étape {index + 1} doit rester dans le volume principal."));
                }
                if (draft.Volumes.Where(item => item.Kind == StudioUx2NavIVolumeKind.Obstacle)
                    .Any(item => ContainsUx2NavI(item.Center, item.Size, node.Position,
                        (float)draft.AvoidanceRadius)))
                {
                    result.Add(new("waypoint.flight.node_blocked", $"{path}/position",
                        $"L’étape {index + 1} se trouve dans la marge d’un obstacle nommé."));
                }
                if (index > 0 && route.Nodes[index - 1].Position == node.Position)
                {
                    result.Add(new("waypoint.flight.segment_degenerate", $"{path}/position",
                        $"L’étape {index + 1} doit être distincte de l’étape précédente."));
                }
            }
        }
        return result;
    }

    private static NavigationVolumeDefinition BuildNavigationVolumeUx2NavI(
        StudioUx2NavIVolumeDraft draft)
    {
        if (draft.Original is not null && VolumeMatchesDraftUx2NavI(draft.Original, draft))
        {
            return draft.Original;
        }
        return new NavigationVolumeDefinition(draft.Id, draft.Name.Trim(), draft.Center, draft.Size,
            (float)draft.CostMultiplier, draft.Original?.Provenance ?? Ux2NavIProvenance);
    }

    private static VolumetricNavigationProfile BuildVolumetricProfileUx2NavI(
        VolumetricNavigationProfile original, StudioUx2NavIDraft draft,
        IReadOnlyList<NavigationVolumeDefinition> obstacles,
        IReadOnlyList<NavigationVolumeDefinition> costs)
    {
        if (original.Enabled == draft.Enabled && original.Modes == draft.Modes &&
            original.CellSize.Equals((float)draft.CellSize) && original.Center == draft.Center &&
            original.Size == draft.Size && original.AvoidanceRadius.Equals((float)draft.AvoidanceRadius) &&
            original.DebugVisible == draft.DebugVisible && original.Obstacles.SequenceEqual(obstacles) &&
            original.CostVolumes.SequenceEqual(costs))
        {
            return original;
        }
        return new VolumetricNavigationProfile(original.Id, original.SchemaVersion, draft.Enabled,
            draft.Modes, (float)draft.CellSize, draft.Center, draft.Size,
            (float)draft.AvoidanceRadius, obstacles, costs, draft.DebugVisible, original.Provenance);
    }

    private static FlightRouteDefinition BuildFlightRouteUx2NavI(StudioUx2NavIRouteDraft draft,
        FlightRouteDefinition? original)
    {
        if (original is not null && RouteMatchesDraftUx2NavI(original, draft))
        {
            return original;
        }
        var nodes = draft.Nodes.Select(item =>
        {
            if (item.Original is not null && NodeMatchesDraftUx2NavI(item.Original, item))
            {
                return item.Original;
            }
            return new FlightRouteNode(item.Id, item.Name.Trim(), item.Position,
                (float)item.Speed, (float)item.BankDegrees);
        }).ToArray();
        return new FlightRouteDefinition(draft.RouteId,
            original?.SchemaVersion ?? FlightRouteDefinition.CurrentSchemaVersion,
            draft.Name.Trim(), nodes, draft.Curve, draft.ObstaclePolicy, draft.Closed,
            draft.DebugVisible, original?.Provenance ?? Ux2NavIProvenance);
    }

    private static IReadOnlyList<string> BuildAerialDiffUx2NavI(AreaDefinition sourceArea,
        AreaDefinition candidateArea, FlightRouteDefinition? route, StudioUx2NavIDraft draft)
    {
        var result = new List<string>();
        var source = sourceArea.World.VolumetricNavigation;
        var candidate = candidateArea.World.VolumetricNavigation;
        if (source.Enabled != candidate.Enabled || source.Modes != candidate.Modes)
        {
            result.Add($"Navigation volumétrique : {HumanModesUx2NavI(source.Enabled ? source.Modes : VolumetricNavigationMode.None)} → " +
                       $"{HumanModesUx2NavI(candidate.Enabled ? candidate.Modes : VolumetricNavigationMode.None)}.");
        }
        if (!source.CellSize.Equals(candidate.CellSize) || source.Center != candidate.Center ||
            source.Size != candidate.Size || !source.AvoidanceRadius.Equals(candidate.AvoidanceRadius))
        {
            result.Add($"Grille : cellule {source.CellSize:0.##} → {candidate.CellSize:0.##} · " +
                       $"volume {candidate.Size.X:0.##} × {candidate.Size.Y:0.##} × {candidate.Size.Z:0.##} · " +
                       $"évitement {candidate.AvoidanceRadius:0.##}.");
        }
        if (!source.Obstacles.SequenceEqual(candidate.Obstacles) ||
            !source.CostVolumes.SequenceEqual(candidate.CostVolumes))
        {
            result.Add($"Volumes nommés : {source.Obstacles.Count} obstacle(s) / {source.CostVolumes.Count} coût(s) → " +
                       $"{candidate.Obstacles.Count} obstacle(s) / {candidate.CostVolumes.Count} coût(s).");
        }
        if (source.DebugVisible != candidate.DebugVisible)
        {
            result.Add(candidate.DebugVisible
                ? "Afficher le volume et ses zones nommées dans l’overlay de diagnostic."
                : "Masquer le volume dans l’overlay de diagnostic.");
        }
        if (route is not null && draft.Route is { } routeDraft)
        {
            var original = sourceArea.World.FlightRoutes.SingleOrDefault(item => item.Id == route.Id);
            if (original is null)
            {
                result.Add($"Ajouter « {HumanUx2NavI(route.Name, "Route aérienne")} » avec {route.Nodes.Count} étapes ordonnées.");
            }
            else
            {
                if (!string.Equals(original.Name, route.Name, StringComparison.Ordinal) ||
                    original.Curve != route.Curve || original.ObstaclePolicy != route.ObstaclePolicy ||
                    original.Closed != route.Closed || original.DebugVisible != route.DebugVisible)
                {
                    result.Add($"Route : {HumanCurveUx2NavI(original.Curve)} / " +
                               $"{HumanObstaclePolicyUx2NavI(original.ObstaclePolicy)} → " +
                               $"{HumanCurveUx2NavI(route.Curve)} / {HumanObstaclePolicyUx2NavI(route.ObstaclePolicy)}.");
                }
                if (!original.Nodes.Select(item => item.Id).SequenceEqual(route.Nodes.Select(item => item.Id)))
                {
                    result.Add("Réordonner les étapes aériennes selon la liste affichée, sans changer leur identité.");
                }
                var changed = route.Nodes.Count(node =>
                    original.Nodes.SingleOrDefault(item => item.Id == node.Id) is not { } old || old != node);
                if (changed > 0 || original.Nodes.Count != route.Nodes.Count)
                {
                    result.Add($"Étapes : {original.Nodes.Count} → {route.Nodes.Count} · {changed} ajoutée(s) ou modifiée(s).");
                }
            }
            if (routeDraft.Nodes.Any(item => Math.Abs(item.Position.Y) > .001))
            {
                result.Add("Conserver les altitudes XYZ, vitesses et inclinaisons de chaque étape dans le document canonique.");
            }
        }
        return result;
    }

    private static StudioNavigationOverlaySnapshot BuildAerialOverlayUx2NavI(
        VolumetricNavigationProfile profile, FlightRouteDefinition? route)
    {
        var markers = new List<StudioNavigationOverlayMarker>();
        var segments = new List<StudioNavigationOverlaySegment>();
        AddBoxUx2NavI(segments, "volume-principal", "Limites de navigation volumétrique",
            profile.Center, profile.Size, selected: route is null, broken: false);
        foreach (var obstacle in profile.Obstacles)
        {
            AddBoxUx2NavI(segments, $"obstacle:{obstacle.Id:N}",
                $"Obstacle · {HumanUx2NavI(obstacle.Name, "Volume nommé")}", obstacle.Center,
                obstacle.Size, false, true);
        }
        foreach (var cost in profile.CostVolumes)
        {
            AddBoxUx2NavI(segments, $"cout:{cost.Id:N}",
                $"Coût · {HumanUx2NavI(cost.Name, "Zone nommée")}", cost.Center,
                cost.Size, false, false);
        }
        if (route is not null)
        {
            for (var index = 0; index < route.Nodes.Count; index++)
            {
                var node = route.Nodes[index];
                markers.Add(new($"route:{route.Id:N}:node:{index}",
                    $"Étape {index + 1} · {HumanUx2NavI(node.Name, "Étape")}",
                    StudioNavigationOverlaySemantic.Route, PointUx2NavI(node.Position), index == 0));
                if (index > 0)
                {
                    segments.Add(new($"route:{route.Id:N}:segment:{index - 1}",
                        $"Trajet {index} vers {index + 1}", StudioNavigationOverlaySemantic.Route,
                        PointUx2NavI(route.Nodes[index - 1].Position), PointUx2NavI(node.Position), true,
                        false, false));
                }
            }
        }
        return new(markers, segments, []);
    }

    private static void AddBoxUx2NavI(List<StudioNavigationOverlaySegment> segments,
        string key, string label, Vector3Value center, Vector3Value size, bool selected, bool broken)
    {
        var x = size.X / 2;
        var y = size.Y / 2;
        var z = size.Z / 2;
        var points = new[]
        {
            new Vector3Value(center.X - x, center.Y - y, center.Z - z),
            new Vector3Value(center.X + x, center.Y - y, center.Z - z),
            new Vector3Value(center.X + x, center.Y - y, center.Z + z),
            new Vector3Value(center.X - x, center.Y - y, center.Z + z),
            new Vector3Value(center.X - x, center.Y + y, center.Z - z),
            new Vector3Value(center.X + x, center.Y + y, center.Z - z),
            new Vector3Value(center.X + x, center.Y + y, center.Z + z),
            new Vector3Value(center.X - x, center.Y + y, center.Z + z),
        };
        var edges = new (int From, int To)[]
        {
            (0, 1), (1, 2), (2, 3), (3, 0), (4, 5), (5, 6), (6, 7), (7, 4),
            (0, 4), (1, 5), (2, 6), (3, 7),
        };
        for (var index = 0; index < edges.Length; index++)
        {
            var edge = edges[index];
            segments.Add(new($"{key}:edge:{index}", label, StudioNavigationOverlaySemantic.Volume,
                PointUx2NavI(points[edge.From]), PointUx2NavI(points[edge.To]), false, selected, broken));
        }
    }

    private static StudioUx2NavIRuntimePreview BuildAerialRuntimePreviewUx2NavI(
        VolumetricNavigationProfile profile, FlightRouteDefinition? route)
    {
        if (!profile.Enabled)
        {
            return new(0, 0, 0, 0, "area.volumetric.disabled", "waypoint.flight.not_selected",
                route?.Nodes[0].Position ?? profile.Center, Vector3Value.Zero, 0, 0,
                "Aperçu pur : navigation désactivée ; aucun graphe ni acteur moteur n’est créé.");
        }
        var graph = VolumetricNavigationGraphBuilder.Build(profile);
        var cellCount = checked(graph.WidthCells * graph.HeightCells * graph.DepthCells);
        var start = route?.Nodes[0].Position ?? new Vector3Value(
            profile.Center.X - Math.Min(profile.Size.X / 4, profile.CellSize), profile.Center.Y,
            profile.Center.Z);
        var destination = route?.Nodes[1].Position ?? new Vector3Value(
            profile.Center.X + Math.Min(profile.Size.X / 4, profile.CellSize), profile.Center.Y,
            profile.Center.Z);
        var path = VolumetricNavigationPathfinder.FindPath(graph, start, destination);
        var agent = new VolumetricAgentState(Guid.Parse("2d000000-0000-4000-8000-0000000000a1"),
            start, Vector3Value.Zero, Math.Max(.1f, Math.Min(1, profile.CellSize / 4)),
            Math.Max(1, Math.Min(12, profile.CellSize * 2)));
        var neighbor = new VolumetricAgentState(
            Guid.Parse("2d000000-0000-4000-8000-0000000000a3"),
            new Vector3Value(start.X + Math.Min(.2f, profile.CellSize / 10), start.Y, start.Z),
            Vector3Value.Zero, agent.Radius, agent.MaximumSpeed);
        var steeringTarget = path.Points.Count > 1 ? path.Points[1] : destination;
        var steering = VolumetricSteeringService.Step(graph, agent,
            steeringTarget, [neighbor], .25f,
            Math.Max(agent.Radius, profile.AvoidanceRadius));
        var flightDiagnostic = "waypoint.flight.not_selected";
        var flightPosition = start;
        var flightForward = Vector3Value.Zero;
        var flightSpeed = 0f;
        var bank = 0f;
        if (route is not null)
        {
            var state = FlightRouteController.CreateInitial(
                Guid.Parse("2d000000-0000-4000-8000-0000000000a2"), route);
            var step = FlightRouteController.Step(route, state, .25f,
                Math.Max(.1f, profile.AvoidanceRadius), new VolumeObstacleProbeUx2NavI(profile.Obstacles));
            flightDiagnostic = step.DiagnosticCode;
            flightPosition = step.State.Position;
            flightForward = step.State.Forward;
            flightSpeed = step.Speed;
            bank = step.State.BankDegrees;
        }
        return new(cellCount, graph.Blocked.Count, graph.Costs.Count, path.Points.Count,
            steering.DiagnosticCode, flightDiagnostic, flightPosition, flightForward, flightSpeed, bank,
            "Aperçu déterministe Domain : grille, A* et contrôleur de route réellement calculés ; " +
            "aucun acteur, collision Godot, IA autonome, réseau ou préchargement n’est exécuté.");
    }

    private sealed class VolumeObstacleProbeUx2NavI(
        IReadOnlyList<NavigationVolumeDefinition> obstacles) : IFlightObstacleProbe
    {
        public bool IsBlocked(Vector3Value position, float clearanceRadius) => obstacles.Any(item =>
            ContainsUx2NavI(item.Center, item.Size, position, clearanceRadius));
    }

    private sealed class FixedObstacleProbeUx2NavI(bool blocked) : IFlightObstacleProbe
    {
        public bool IsBlocked(Vector3Value position, float clearanceRadius) => blocked;
    }

    private static StudioUx2NavIPreview FailedAerialPreviewUx2NavI(string message,
        IReadOnlyList<StudioUx2NavIDiagnostic>? diagnostics = null) => new(false, message,
        "Aucun aperçu disponible.", false, [], diagnostics ?? [],
        StudioNavigationOverlaySnapshot.Empty, null);

    private static bool VolumeMatchesDraftUx2NavI(NavigationVolumeDefinition original,
        StudioUx2NavIVolumeDraft draft) => original.Id == draft.Id &&
        string.Equals(original.Name, draft.Name.Trim(), StringComparison.Ordinal) &&
        original.Center == draft.Center && original.Size == draft.Size &&
        original.CostMultiplier.Equals((float)draft.CostMultiplier);

    private static bool NodeMatchesDraftUx2NavI(FlightRouteNode original,
        StudioUx2NavINodeDraft draft) => original.Id == draft.Id &&
        string.Equals(original.Name, draft.Name.Trim(), StringComparison.Ordinal) &&
        original.Position == draft.Position && original.Speed.Equals((float)draft.Speed) &&
        original.BankDegrees.Equals((float)draft.BankDegrees);

    private static bool RouteMatchesDraftUx2NavI(FlightRouteDefinition original,
        StudioUx2NavIRouteDraft draft) => original.Id == draft.RouteId &&
        string.Equals(original.Name, draft.Name.Trim(), StringComparison.Ordinal) &&
        original.Curve == draft.Curve && original.ObstaclePolicy == draft.ObstaclePolicy &&
        original.Closed == draft.Closed && original.DebugVisible == draft.DebugVisible &&
        original.Nodes.Count == draft.Nodes.Count && original.Nodes.Select((item, index) =>
            NodeMatchesDraftUx2NavI(item, draft.Nodes[index])).All(item => item);

    private static bool FiniteUx2NavI(Vector3Value value) => float.IsFinite(value.X) &&
        float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static bool ContainsUx2NavI(Vector3Value center, Vector3Value size,
        Vector3Value point, float padding = 0) =>
        MathF.Abs(point.X - center.X) <= size.X / 2 + padding &&
        MathF.Abs(point.Y - center.Y) <= size.Y / 2 + padding &&
        MathF.Abs(point.Z - center.Z) <= size.Z / 2 + padding;

    private static bool HumanNameValidUx2NavI(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        return text.Length is > 0 and <= 128 && !ContainsTechnicalIdentifierUx2NavI(text) &&
               text.All(character => !char.IsControl(character));
    }

    private static string AerialSourceFingerprintUx2NavI(AreaDefinition area) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(AreaJsonSerializer.Serialize(area))));

    private static string AreaWorldFingerprintUx2NavI(AreaWorldProfile world)
    {
        var values = new List<string>
        {
            world.Id.ToString("D"), world.SchemaVersion.ToString(CultureInfo.InvariantCulture),
            world.Provenance, world.VolumetricNavigation.Id.ToString("D"),
            world.VolumetricNavigation.SchemaVersion.ToString(CultureInfo.InvariantCulture),
            world.VolumetricNavigation.Enabled.ToString(), world.VolumetricNavigation.Modes.ToString(),
            F(world.VolumetricNavigation.CellSize), P(world.VolumetricNavigation.Center),
            P(world.VolumetricNavigation.Size), F(world.VolumetricNavigation.AvoidanceRadius),
            world.VolumetricNavigation.DebugVisible.ToString(),
        };
        foreach (var volume in world.VolumetricNavigation.Obstacles.Concat(
                     world.VolumetricNavigation.CostVolumes))
        {
            values.AddRange([volume.Id.ToString("D"), volume.Name, P(volume.Center), P(volume.Size),
                F(volume.CostMultiplier), volume.Provenance]);
        }
        foreach (var route in world.FlightRoutes)
        {
            values.AddRange([route.Id.ToString("D"), route.SchemaVersion.ToString(CultureInfo.InvariantCulture),
                route.Name, route.Curve.ToString(), route.ObstaclePolicy.ToString(), route.Closed.ToString(),
                route.DebugVisible.ToString(), route.Provenance]);
            foreach (var node in route.Nodes)
            {
                values.AddRange([node.Id.ToString("D"), node.Name, P(node.Position), F(node.Speed),
                    F(node.BankDegrees)]);
            }
        }
        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join('\u001f', values))));

        static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        static string P(Vector3Value value) => $"{F(value.X)},{F(value.Y)},{F(value.Z)}";
    }

    private static string AerialDraftFingerprintUx2NavI(StudioUx2NavIDraft draft)
    {
        var values = new List<string>
        {
            draft.AreaId.ToString(), draft.BaseRevision.ToString(), draft.SourceFingerprint,
            draft.Enabled.ToString(), draft.Modes.ToString(), F(draft.CellSize), P(draft.Center),
            P(draft.Size), F(draft.AvoidanceRadius), draft.DebugVisible.ToString(),
            draft.Route is null ? "route:none" : "route:present",
        };
        foreach (var volume in draft.Volumes)
        {
            values.AddRange([volume.Id.ToString("D"), volume.IsNew.ToString(), volume.Kind.ToString(),
                volume.NameIsProtected.ToString(), volume.Name, P(volume.Center), P(volume.Size),
                F(volume.CostMultiplier)]);
        }
        if (draft.Route is { } route)
        {
            values.AddRange([route.RouteId.ToString("D"), route.IsNew.ToString(),
                route.NameIsProtected.ToString(), route.Name,
                route.Curve.ToString(), route.ObstaclePolicy.ToString(), route.Closed.ToString(),
                route.DebugVisible.ToString()]);
            foreach (var node in route.Nodes)
            {
                values.AddRange([node.Id.ToString("D"), node.IsNew.ToString(),
                    node.NameIsProtected.ToString(), node.Name,
                    P(node.Position), F(node.Speed), F(node.BankDegrees)]);
            }
        }
        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join('\u001f', values))));

        static string F(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        static string P(Vector3Value value) => $"{F(value.X)},{F(value.Y)},{F(value.Z)}";
    }

    private static StudioNavigationPoint PointUx2NavI(Vector3Value point) =>
        new(point.X, point.Y, point.Z);

    private static string HumanDiagnosticUx2NavI(string? message)
    {
        var text = string.IsNullOrWhiteSpace(message) ? "La commande a été refusée." : message.Trim();
        return System.Text.RegularExpressions.Regex.Replace(text,
            @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b",
            "référence interne", System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                                  System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    private static string HumanPathUx2NavI(string path) =>
        System.Text.RegularExpressions.Regex.Replace(path, @"[0-9a-f]{8}-[0-9a-f-]{27,}",
            "élément-sélectionné", System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static string HumanUx2NavI(string? value, string fallback)
    {
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }
        return ContainsTechnicalIdentifierUx2NavI(text) ? fallback : text;
    }

    private static string HumanModesUx2NavI(VolumetricNavigationMode modes)
    {
        if (modes == VolumetricNavigationMode.None)
        {
            return "désactivée";
        }
        var labels = new List<string>();
        if (modes.HasFlag(VolumetricNavigationMode.Flight)) labels.Add("Vol");
        if (modes.HasFlag(VolumetricNavigationMode.Swim)) labels.Add("Nage");
        if (modes.HasFlag(VolumetricNavigationMode.Space)) labels.Add("Espace");
        return string.Join(" + ", labels);
    }

    private static string HumanCurveUx2NavI(FlightRouteCurve curve) => curve switch
    {
        FlightRouteCurve.Linear => "linéaire",
        FlightRouteCurve.CatmullRom => "courbe douce",
        FlightRouteCurve.Bezier => "transition lissée",
        _ => "héritée",
    };

    private static string HumanObstaclePolicyUx2NavI(FlightObstaclePolicy policy) => policy switch
    {
        FlightObstaclePolicy.Avoid => "contourner",
        FlightObstaclePolicy.Stop => "s’arrêter",
        FlightObstaclePolicy.Ignore => "ignorer",
        _ => "héritée",
    };

    private static bool ContainsTechnicalIdentifierUx2NavI(string text) =>
        text.Contains("UUID", StringComparison.OrdinalIgnoreCase) ||
        System.Text.RegularExpressions.Regex.IsMatch(text,
            @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    internal ProjectDefinition? CreateUx2NavIEvidenceProject()
    {
        var source = Project;
        return source is null ? null : CreateUx2NavIEvidenceProject(source.Id.Value);
    }

    private static ProjectDefinition CreateUx2NavIEvidenceProject(Guid seed)
    {
        var project = CreateUx2NavDEvidenceProject(seed == Guid.Empty
            ? Guid.Parse("2d000000-0000-4000-8000-0000000000b0") : seed);
        var area = project.Areas.Single(item => item.Slug == "clairiere-navigation");
        var source = area.World;
        var volume = new VolumetricNavigationProfile(source.VolumetricNavigation.Id,
            source.VolumetricNavigation.SchemaVersion, true,
            VolumetricNavigationMode.Flight | VolumetricNavigationMode.Swim, 4,
            new Vector3Value(0, 12, 0),
            new Vector3Value(area.Dimensions.Width, 28, area.Dimensions.Depth), 1,
            [
                new NavigationVolumeDefinition(
                    Guid.Parse("2d000000-0000-4000-8000-0000000000b1"),
                    "2d000000-0000-4000-8000-0000000001b1",
                    new Vector3Value(18, 10, 16), new Vector3Value(5, 16, 5), 1,
                    "fixture:ux2-nav-i:neighbor"),
            ],
            [
                new NavigationVolumeDefinition(
                    Guid.Parse("2d000000-0000-4000-8000-0000000000b2"), "Courant ascendant",
                    new Vector3Value(-7, 10, 4), new Vector3Value(6, 10, 8), 2.5f,
                    Ux2NavIEvidenceProvenance),
            ], true, Ux2NavIEvidenceProvenance);
        var selectedRoute = new FlightRouteDefinition(
            Guid.Parse("2d000000-0000-4000-8000-0000000000c0"), 1,
            "Ronde des hautes branches",
            [
                new FlightRouteNode(Guid.Parse("2d000000-0000-4000-8000-0000000000c1"),
                    "Lisière", new Vector3Value(-10, 7, -8), 8, -15),
                new FlightRouteNode(Guid.Parse("2d000000-0000-4000-8000-0000000000c2"),
                    "Canopée", new Vector3Value(0, 12, 0), 11, 20),
                new FlightRouteNode(Guid.Parse("2d000000-0000-4000-8000-0000000000c3"),
                    "Belvédère", new Vector3Value(11, 9, 8), 9, 5),
            ], FlightRouteCurve.CatmullRom, FlightObstaclePolicy.Avoid, true, true,
            Ux2NavIEvidenceProvenance);
        var neighborRoute = new FlightRouteDefinition(
            Guid.Parse("2d000000-0000-4000-8000-0000000000d0"), 1,
            "2d000000-0000-4000-8000-0000000001d0",
            [
                new FlightRouteNode(Guid.Parse("2d000000-0000-4000-8000-0000000000d1"),
                    "2d000000-0000-4000-8000-0000000001d1",
                    new Vector3Value(-16, 6, 10), 6, 0),
                new FlightRouteNode(Guid.Parse("2d000000-0000-4000-8000-0000000000d2"),
                    "Est", new Vector3Value(15, 8, 10), 7, 8),
            ], FlightRouteCurve.Linear, FlightObstaclePolicy.Stop, false, false,
            "fixture:ux2-nav-i:neighbor");
        var world = new AreaWorldProfile(source.Id, source.SchemaVersion, source.Terrain,
            source.Biomes, source.Features, volume, [selectedRoute, neighborRoute], source.Loading,
            Ux2NavIEvidenceProvenance);
        var replacement = area.WithWorld(world);
        return new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
            project.Areas.Select(item => item.Id == area.Id ? replacement : item).ToArray(),
            project.Dialogues, project.Quests);
    }

    internal StudioUx2NavISmokeResult RunUx2NavIContractSmoke()
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
        var sourceApproval = CaptureAerialApprovalUx2NavI();
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-nav-i-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var fixture = CreateUx2NavIEvidenceProject(
                Guid.Parse("2d000000-0000-4000-8000-0000000000e0"));
            var created = ProjectSession.Create(tempRoot, fixture);
            if (!created.Succeeded || !editing.Open(tempRoot).Succeeded)
            {
                return FailedUx2NavISmoke();
            }
            var areaChoice = editing.AerialAreaChoicesUx2NavI.Single(item => item.IsEvidence);
            var routeChoice = editing.AerialRouteChoicesUx2NavI(areaChoice.AreaId)
                .Single(item => item.IsEvidence);
            var explicitSelection = editing.CreateAerialDraftUx2NavI(null) is null &&
                                    editing.CreateAerialRouteDraftUx2NavI(areaChoice.AreaId, null) is null &&
                                    editing.CreateAerialRouteDraftUx2NavI(null, routeChoice.RouteId) is null;
            var draft = editing.CreateAerialDraftUx2NavI(areaChoice.AreaId);
            var route = editing.CreateAerialRouteDraftUx2NavI(areaChoice.AreaId, routeChoice.RouteId);
            if (draft is null || route is null)
            {
                return FailedUx2NavISmoke();
            }
            draft = draft with { Route = route };
            var humanLabels = editing.AerialAreaChoicesUx2NavI.All(item =>
                                  !ContainsTechnicalIdentifierUx2NavI(item.Label)) &&
                              editing.AerialRouteChoicesUx2NavI(areaChoice.AreaId).All(item =>
                                  !ContainsTechnicalIdentifierUx2NavI(item.Label)) &&
                              draft.Volumes.Any(item => item.NameIsProtected) &&
                              editing.AerialRouteChoicesUx2NavI(areaChoice.AreaId).Any(item =>
                                  !item.IsEvidence && item.Label.Contains("Route aérienne",
                                      StringComparison.Ordinal));

            var newRoute = editing.CreateNewAerialRouteDraftUx2NavI(areaChoice.AreaId);
            var newPreview = newRoute is null
                ? FailedAerialPreviewUx2NavI("Brouillon absent.")
                : editing.PreviewAerialNavigationUx2NavI(draft with { Route = newRoute });
            var flightRouteAuthoring = newRoute is { IsNew: true, Original: null, Nodes.Count: 2 } &&
                                       newPreview.Succeeded && newPreview.CanApply;

            var addedObstacle = editing.CreateNewNavigationVolumeDraftUx2NavI(draft,
                StudioUx2NavIVolumeKind.Obstacle) with
            {
                Name = "Tronc creux",
                Center = new Vector3Value(22, 8, -18),
                Size = new Vector3Value(3, 12, 3),
            };
            var addedCost = editing.CreateNewNavigationVolumeDraftUx2NavI(draft,
                StudioUx2NavIVolumeKind.Cost) with
            {
                Name = "Vent du nord",
                Center = new Vector3Value(4, 14, -12),
                Size = new Vector3Value(12, 8, 8),
                CostMultiplier = 3.25,
            };
            var configuredVolumes = draft.Volumes.Concat([addedObstacle, addedCost]).ToArray();
            var configuredRoute = route with
            {
                Name = "Ronde haute révisée",
                Curve = FlightRouteCurve.Bezier,
                ObstaclePolicy = FlightObstaclePolicy.Stop,
                Closed = false,
                DebugVisible = true,
                Nodes =
                [
                    route.Nodes[2] with
                    {
                        Name = "Belvédère haut",
                        Position = new Vector3Value(11, 10, 8),
                        Speed = 12,
                        BankDegrees = 18,
                    },
                    route.Nodes[0] with { Speed = 9, BankDegrees = -20 },
                    route.Nodes[1] with { Position = new Vector3Value(0, 13, 0), Speed = 13 },
                ],
            };
            var configured = draft with
            {
                Modes = VolumetricNavigationMode.Flight | VolumetricNavigationMode.Swim |
                        VolumetricNavigationMode.Space,
                CellSize = 3,
                AvoidanceRadius = .75,
                DebugVisible = true,
                Volumes = configuredVolumes,
                Route = configuredRoute,
            };
            var historyBefore = editing.History!;
            var projectBefore = editing.Project;
            var revisionBefore = historyBefore.State.Revision;
            var journalBefore = historyBefore.Journal.ToArray();
            var preview = editing.PreviewAerialNavigationUx2NavI(configured);
            var dryRun = preview.Succeeded && preview.CanApply &&
                         ReferenceEquals(projectBefore, editing.Project) &&
                         ReferenceEquals(historyBefore, editing.History) &&
                         historyBefore.State.Revision == revisionBefore &&
                         historyBefore.Journal.SequenceEqual(journalBefore);
            var volumetricAuthoring = preview.Succeeded && preview.Overlay.Segments.Count >= 12 &&
                                      preview.Diff.Any(item => item.Contains("Volumes nommés",
                                          StringComparison.Ordinal));
            var runtimePreview = preview.Runtime is
            {
                GridCells: > 0,
                BlockedCells: > 0,
                CostCells: > 0,
                PathPoints: > 0,
                FlightSpeed: > 0,
            } runtime && runtime.FlightDiagnostic.StartsWith("waypoint.flight.",
                StringComparison.Ordinal) && runtime.SteeringDiagnostic ==
                "area.volumetric.crowd_avoided" && runtime.Boundary.Contains("aucun acteur",
                StringComparison.OrdinalIgnoreCase);
            runtimePreview &= Enum.GetValues<FlightObstaclePolicy>().All(policy =>
            {
                var policyRoute = new FlightRouteDefinition(Guid.NewGuid(), 1, "Validation obstacle",
                    route.Nodes.Select(item => new FlightRouteNode(item.Id, item.Name, item.Position,
                        item.Speed, item.BankDegrees)).ToArray(), FlightRouteCurve.Linear, policy,
                    false, false, "smoke:ux2-nav-i:policy");
                var initial = FlightRouteController.CreateInitial(Guid.NewGuid(), policyRoute);
                var step = FlightRouteController.Step(policyRoute, initial, .25f, .5f,
                    new FixedObstacleProbeUx2NavI(true));
                return policy switch
                {
                    FlightObstaclePolicy.Avoid =>
                        step.DiagnosticCode == "waypoint.flight.obstacle_avoided",
                    FlightObstaclePolicy.Stop =>
                        step.DiagnosticCode == "waypoint.flight.obstacle_stopped" && step.Speed == 0,
                    FlightObstaclePolicy.Ignore =>
                        step.DiagnosticCode == "waypoint.flight.obstacle_ignored",
                    _ => false,
                };
            });

            var invalidVolume = editing.PreviewAerialNavigationUx2NavI(configured with
            {
                Size = new Vector3Value(0, configured.Size.Y, configured.Size.Z),
            });
            var invalidMode = editing.PreviewAerialNavigationUx2NavI(configured with
            {
                Modes = VolumetricNavigationMode.Swim,
            });
            var invalidNode = editing.PreviewAerialNavigationUx2NavI(configured with
            {
                Route = configuredRoute with
                {
                    Nodes = configuredRoute.Nodes.Select((item, index) => index == 0
                        ? item with { Speed = 0, BankDegrees = 90 }
                        : item).ToArray(),
                },
            });
            var validation = !invalidVolume.Succeeded && !invalidMode.Succeeded &&
                             !invalidNode.Succeeded && invalidVolume.Diagnostics.Any(item =>
                                 item.Code == "area.world.volumetric_bounds_invalid") &&
                             invalidMode.Diagnostics.Any(item =>
                                 item.Code == "waypoint.flight.mode_required") &&
                             invalidNode.Diagnostics.Any(item =>
                                 item.Code == "waypoint.flight.node_invalid");

            var staleSource = editing.PreviewAerialNavigationUx2NavI(configured with
            {
                SourceFingerprint = "stale",
            });
            var staleSourceBlocked = !staleSource.Succeeded;

            draft = editing.CreateAerialDraftUx2NavI(areaChoice.AreaId)!;
            route = editing.CreateAerialRouteDraftUx2NavI(areaChoice.AreaId, routeChoice.RouteId)!;
            var staleRevisionDraft = draft with { Route = route };
            var otherArea = editing.Project!.Areas.Single(item => item.Id != areaChoice.AreaId);
            var neighborChanged = ApplyUx2NavINeighborMutation(editing, otherArea);
            var staleRevision = editing.PreviewAerialNavigationUx2NavI(staleRevisionDraft);
            var staleRevisionBlocked = neighborChanged && !staleRevision.Succeeded &&
                                       editing.Undo().Succeeded;

            draft = editing.CreateAerialDraftUx2NavI(areaChoice.AreaId)!;
            route = editing.CreateAerialRouteDraftUx2NavI(areaChoice.AreaId, routeChoice.RouteId)!;
            var approvalDraft = draft with
            {
                Route = route with { Curve = FlightRouteCurve.Linear },
            };
            var approvalPreview = editing.PreviewAerialNavigationUx2NavI(approvalDraft);
            otherArea = editing.Project!.Areas.Single(item => item.Id != areaChoice.AreaId);
            neighborChanged = ApplyUx2NavINeighborMutation(editing, otherArea);
            var staleApply = editing.ApplyApprovedAerialNavigationUx2NavI();
            var staleApprovalBlocked = approvalPreview.Succeeded && neighborChanged &&
                                       !staleApply.Succeeded && editing.Undo().Succeeded;

            var canonicalSource = editing.Project!;
            var sourceArea = canonicalSource.Areas.Single(item => item.Id == areaChoice.AreaId);
            var sourceNeighbor = canonicalSource.Areas.Single(item => item.Id != areaChoice.AreaId);
            var sourceWorld = sourceArea.World;
            var sourceRoute = sourceWorld.FlightRoutes.Single(item => item.Id == routeChoice.RouteId);
            var neighborRoute = sourceWorld.FlightRoutes.Single(item => item.Id != routeChoice.RouteId);
            var neighborObstacle = sourceWorld.VolumetricNavigation.Obstacles.Single();
            var neighborCost = sourceWorld.VolumetricNavigation.CostVolumes.Single();
            draft = editing.CreateAerialDraftUx2NavI(areaChoice.AreaId)!;
            route = editing.CreateAerialRouteDraftUx2NavI(areaChoice.AreaId, routeChoice.RouteId)!;
            addedObstacle = editing.CreateNewNavigationVolumeDraftUx2NavI(draft,
                StudioUx2NavIVolumeKind.Obstacle) with
            {
                Name = "Tronc creux",
                Center = new Vector3Value(22, 8, -18),
                Size = new Vector3Value(3, 12, 3),
            };
            addedCost = editing.CreateNewNavigationVolumeDraftUx2NavI(draft,
                StudioUx2NavIVolumeKind.Cost) with
            {
                Name = "Vent du nord",
                Center = new Vector3Value(4, 14, -12),
                Size = new Vector3Value(12, 8, 8),
                CostMultiplier = 3.25,
            };
            configuredRoute = route with
            {
                Name = "Ronde haute révisée",
                Curve = FlightRouteCurve.Bezier,
                ObstaclePolicy = FlightObstaclePolicy.Stop,
                Closed = false,
                DebugVisible = true,
                Nodes =
                [
                    route.Nodes[2] with
                    {
                        Name = "Belvédère haut", Position = new Vector3Value(11, 10, 8),
                        Speed = 12, BankDegrees = 18,
                    },
                    route.Nodes[0] with { Speed = 9, BankDegrees = -20 },
                    route.Nodes[1] with { Position = new Vector3Value(0, 13, 0), Speed = 13 },
                ],
            };
            configured = draft with
            {
                Modes = VolumetricNavigationMode.Flight | VolumetricNavigationMode.Swim |
                        VolumetricNavigationMode.Space,
                CellSize = 3,
                AvoidanceRadius = .75,
                DebugVisible = true,
                Volumes = draft.Volumes.Concat([addedObstacle, addedCost]).ToArray(),
                Route = configuredRoute,
            };
            var freshPreview = editing.PreviewAerialNavigationUx2NavI(configured);
            var applied = editing.ApplyApprovedAerialNavigationUx2NavI();
            var appliedProject = editing.Project!;
            var appliedArea = appliedProject.Areas.Single(item => item.Id == areaChoice.AreaId);
            var appliedWorld = appliedArea.World;
            var appliedRoute = appliedWorld.FlightRoutes.Single(item => item.Id == routeChoice.RouteId);
            var stableOrder = appliedRoute.Nodes.Select(item => item.Id)
                                  .SequenceEqual(configuredRoute.Nodes.Select(item => item.Id)) &&
                              appliedRoute.Nodes[0].Name == "Belvédère haut";
            var canonicalCommand = freshPreview.Succeeded && applied.Succeeded &&
                                   editing.History!.Journal.LastOrDefault()?.CommandName ==
                                   "set_area_world_profile";
            var worldNeighborsPreserved = ReferenceEquals(sourceWorld.Terrain, appliedWorld.Terrain) &&
                                          sourceWorld.Biomes.SequenceEqual(appliedWorld.Biomes) &&
                                          sourceWorld.Features.SequenceEqual(appliedWorld.Features) &&
                                          ReferenceEquals(sourceWorld.Loading, appliedWorld.Loading) &&
                                          sourceWorld.Id == appliedWorld.Id &&
                                          sourceWorld.SchemaVersion == appliedWorld.SchemaVersion &&
                                          sourceWorld.Provenance == appliedWorld.Provenance &&
                                          sourceWorld.VolumetricNavigation.Id ==
                                          appliedWorld.VolumetricNavigation.Id &&
                                          sourceWorld.VolumetricNavigation.SchemaVersion ==
                                          appliedWorld.VolumetricNavigation.SchemaVersion &&
                                          sourceWorld.VolumetricNavigation.Provenance ==
                                          appliedWorld.VolumetricNavigation.Provenance &&
                                          sourceWorld.VolumetricNavigation.Obstacles.SequenceEqual(
                                              appliedWorld.VolumetricNavigation.Obstacles.Where(item =>
                                                  sourceWorld.VolumetricNavigation.Obstacles.Any(sourceItem =>
                                                      sourceItem.Id == item.Id))) &&
                                          sourceWorld.VolumetricNavigation.CostVolumes.SequenceEqual(
                                              appliedWorld.VolumetricNavigation.CostVolumes.Where(item =>
                                                  sourceWorld.VolumetricNavigation.CostVolumes.Any(sourceItem =>
                                                      sourceItem.Id == item.Id))) &&
                                          appliedWorld.VolumetricNavigation.Obstacles.Contains(neighborObstacle) &&
                                          appliedWorld.VolumetricNavigation.CostVolumes.Contains(neighborCost);
            var routeNeighborsPreserved = sourceWorld.FlightRoutes
                                              .Where(item => item.Id != sourceRoute.Id)
                                              .SequenceEqual(appliedWorld.FlightRoutes.Where(item =>
                                                  item.Id != sourceRoute.Id)) &&
                                          appliedWorld.FlightRoutes.Contains(neighborRoute) &&
                                          appliedRoute.Id == sourceRoute.Id &&
                                          appliedRoute.SchemaVersion == sourceRoute.SchemaVersion &&
                                          appliedRoute.Provenance == sourceRoute.Provenance;
            var appliedNeighbor = appliedProject.Areas.Single(item => item.Id == sourceNeighbor.Id);
            var projectNeighborsPreserved = ReferenceEquals(canonicalSource.Module,
                                                appliedProject.Module) &&
                                            ReferenceEquals(sourceNeighbor, appliedNeighbor) &&
                                            ReferenceEquals(sourceArea.Navigation, appliedArea.Navigation) &&
                                            sourceArea.Portals.SequenceEqual(appliedArea.Portals) &&
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
            var restoredArea = reopened.Project?.Areas.SingleOrDefault(item => item.Id == areaChoice.AreaId);
            var persistence = save.Succeeded && reopen.Succeeded && restoredArea is not null &&
                              string.Equals(appliedJson, AreaJsonSerializer.Serialize(restoredArea),
                                  StringComparison.Ordinal);
            var humanText = !ContainsTechnicalIdentifierUx2NavI(areaChoice.Label + " " +
                            routeChoice.Label + " " + freshPreview.Summary + " " +
                            string.Join(' ', freshPreview.Diff) + " " +
                            string.Join(' ', freshPreview.Diagnostics.Select(item => item.Message)));
            var sourceUntouched = ReferenceEquals(sourceProject, Project) &&
                                  ReferenceEquals(sourceHistory, History) &&
                                  History?.State.Revision == sourceRevision &&
                                  (History?.Journal ?? []).SequenceEqual(sourceJournal) &&
                                  IsDirty == sourceDirty && History?.CanUndo == sourceUndo &&
                                  History?.CanRedo == sourceRedo && ActiveAreaId == sourceAreaId &&
                                  SelectedEntityIds.SequenceEqual(sourceSelection) &&
                                  SelectedEntityId == sourceSelected &&
                                  CaptureAerialApprovalUx2NavI() == sourceApproval;
            var succeeded = explicitSelection && humanLabels && volumetricAuthoring &&
                            flightRouteAuthoring && stableOrder && validation && runtimePreview && dryRun &&
                            staleRevisionBlocked && staleSourceBlocked && staleApprovalBlocked &&
                            canonicalCommand && undoRedo && persistence && worldNeighborsPreserved &&
                            routeNeighborsPreserved && projectNeighborsPreserved && sourceUntouched &&
                            humanText;
            return new(succeeded, explicitSelection, humanLabels, volumetricAuthoring,
                flightRouteAuthoring, stableOrder, validation, runtimePreview, dryRun,
                staleRevisionBlocked, staleSourceBlocked, staleApprovalBlocked, canonicalCommand,
                undoRedo, persistence, worldNeighborsPreserved, routeNeighborsPreserved,
                projectNeighborsPreserved, sourceUntouched, humanText);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_NAV_I_CONTRACT_FAILED type={exception.GetType().Name} message={exception.Message}");
            return FailedUx2NavISmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            DeleteUx2NavITemporaryWorkspace(tempRoot);
        }
    }

    private static bool ApplyUx2NavINeighborMutation(StudioSessionController controller,
        AreaDefinition area)
    {
        var history = controller.History;
        if (history is null)
        {
            return false;
        }
        var source = area.World;
        var candidate = new AreaWorldProfile(source.Id, source.SchemaVersion, source.Terrain,
            source.Biomes, source.Features, source.VolumetricNavigation, source.FlightRoutes,
            source.Loading, source.Provenance + ":neighbor-check");
        var result = controller._setAreaWorldUx2NavI.Handle(history.State,
            new SetAreaWorldProfileCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                area.Id, candidate, false));
        if (!result.Succeeded)
        {
            return false;
        }
        history.Record(result);
        return true;
    }

    internal static bool DeleteUx2NavITemporaryWorkspace(string? directory)
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
                !leaf.StartsWith("openrpg-forge-studio-ux2-nav-i-", StringComparison.Ordinal))
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

    private static StudioUx2NavISmokeResult FailedUx2NavISmoke() => new(
        Succeeded: false,
        ExplicitSelection: false,
        HumanLabels: false,
        VolumetricAuthoring: false,
        FlightRouteAuthoring: false,
        StableOrder: false,
        Validation: false,
        RuntimePreview: false,
        DryRun: false,
        StaleRevisionBlocked: false,
        StaleSourceBlocked: false,
        StaleApprovalBlocked: false,
        CanonicalCommand: false,
        UndoRedo: false,
        Persistence: false,
        WorldNeighborsPreserved: false,
        RouteNeighborsPreserved: false,
        ProjectNeighborsPreserved: false,
        SourceUntouched: false,
        HumanText: false);
}
