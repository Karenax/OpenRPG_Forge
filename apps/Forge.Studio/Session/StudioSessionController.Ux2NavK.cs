using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Forge.Application.Commands;
using Forge.Application.Navigation;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Core.Navigation;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2NavKProvenance = "studio:ux2-nav-k:cross-area-patrol";
    private const string Ux2NavKEvidenceProvenance = "studio:ux2-nav-k:evidence";
    private readonly SetAreaExperienceProfileHandler _setAreaExperienceUx2NavK = new(new SystemClock());
    private StudioUx2NavKDraft? _approvedCrossAreaPatrolUx2NavK;
    private ProjectRevision? _approvedCrossAreaPatrolRevisionUx2NavK;
    private string? _approvedCrossAreaPatrolFingerprintUx2NavK;

    internal IReadOnlyList<StudioUx2NavKAreaChoice> CrossAreaPatrolAreaChoicesUx2NavK
    {
        get
        {
            var project = Project;
            if (project is null) return [];
            var duplicateNames = project.Areas.GroupBy(item => HumanUx2NavK(item.DisplayName, "Zone"),
                    StringComparer.CurrentCultureIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(),
                    StringComparer.CurrentCultureIgnoreCase);
            return project.Areas.Select((area, index) =>
                {
                    var name = HumanUx2NavK(area.DisplayName, "Zone");
                    var label = duplicateNames[name] > 1
                        ? $"{name} · {HumanUx2NavK(area.Slug, $"zone {index + 1}")}" : name;
                    var routes = area.RuntimePolicies.Experience.PatrolRoutes;
                    return new StudioUx2NavKAreaChoice(area.Id, label, routes.Count > 0,
                        routes.Any(item => item.Provenance == Ux2NavKEvidenceProvenance));
                })
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.AreaId.Value)
                .ToArray();
        }
    }

    internal IReadOnlyList<StudioUx2NavKRouteChoice> CrossAreaPatrolRouteChoicesUx2NavK(AreaId? areaId)
    {
        var area = areaId is null ? null : Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (area is null) return [];
        var routes = area.RuntimePolicies.Experience.PatrolRoutes;
        var labels = routes.Select(item => HumanOwnerLabelUx2NavK(item.OwnerId)).ToArray();
        return routes.Select((route, index) =>
            {
                var label = labels.Count(candidate => string.Equals(candidate, labels[index],
                    StringComparison.CurrentCultureIgnoreCase)) > 1
                    ? $"{labels[index]} · trajet {index + 1}" : labels[index];
                return new StudioUx2NavKRouteChoice(route.Id, label,
                    ContainsTechnicalIdentifierUx2NavK(route.OwnerId),
                    route.Provenance == Ux2NavKEvidenceProvenance);
            }).ToArray();
    }

    internal IReadOnlyList<StudioUx2NavKPortalChoice> CrossAreaPatrolPortalChoicesUx2NavK(
        StudioUx2NavKSegmentDraft segment)
    {
        ArgumentNullException.ThrowIfNull(segment);
        var project = Project;
        if (project is null) return [];
        var choices = KnownPortalChoicesUx2NavK(project, segment.FromAreaId).ToList();
        if (choices.All(item => item.PortalId != segment.PortalId))
        {
            choices.Insert(0, new(segment.PortalId, segment.FromAreaId, segment.ToAreaId,
                "Passage existant protégé", true));
        }
        return choices;
    }

    internal IReadOnlyList<StudioUx2NavKWaypointChoice> CrossAreaPatrolWaypointChoicesUx2NavK(
        StudioUx2NavKSegmentDraft segment)
    {
        ArgumentNullException.ThrowIfNull(segment);
        var project = Project;
        if (project is null) return [];
        var choices = KnownWaypointChoicesUx2NavK(project, segment.ToAreaId).ToList();
        if (choices.All(item => item.WaypointId != segment.ResumeWaypointId))
        {
            choices.Insert(0, new(segment.ResumeWaypointId, segment.ToAreaId,
                "Point de reprise existant protégé", true));
        }
        return choices;
    }

    internal StudioUx2NavKDraft? CreateCrossAreaPatrolDraftUx2NavK(AreaId? areaId, Guid? routeId)
    {
        var history = History;
        var project = Project;
        var area = areaId is null ? null : project?.Areas.SingleOrDefault(item => item.Id == areaId);
        var route = routeId is null ? null : area?.RuntimePolicies.Experience.PatrolRoutes
            .SingleOrDefault(item => item.Id == routeId.Value);
        if (history is null || project is null || area is null || route is null) return null;
        var catalog = NavigationDestinationCatalog.Build(project);
        var label = CrossAreaPatrolAreaChoicesUx2NavK.Single(item => item.AreaId == area.Id).Label;
        return new(area.Id, route.Id, false, false, route, history.State.Revision,
            CrossAreaPatrolSourceFingerprintUx2NavK(project), label,
            ContainsTechnicalIdentifierUx2NavK(route.OwnerId), route.OwnerId, route.Loop,
            route.Segments.Select(item => SegmentDraftUx2NavK(item, catalog)).ToArray());
    }

    internal StudioUx2NavKDraft? CreateNewCrossAreaPatrolDraftUx2NavK(AreaId? areaId)
    {
        var history = History;
        var project = Project;
        var area = areaId is null ? null : project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (history is null || project is null || area is null) return null;
        var segment = CreateNewCrossAreaPatrolSegmentUx2NavK(area.Id, []);
        if (segment is null) return null;
        var label = CrossAreaPatrolAreaChoicesUx2NavK.Single(item => item.AreaId == area.Id).Label;
        return new(area.Id, Guid.NewGuid(), true, false, null, history.State.Revision,
            CrossAreaPatrolSourceFingerprintUx2NavK(project), label, false,
            $"patrouille {area.RuntimePolicies.Experience.PatrolRoutes.Count + 1}", false, [segment]);
    }

    internal StudioUx2NavKSegmentDraft? CreateNewCrossAreaPatrolSegmentUx2NavK(
        StudioUx2NavKDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Segments.Count >= 32) return null;
        var fromArea = draft.Segments.Count == 0 ? draft.OwnerAreaId : draft.Segments[^1].ToAreaId;
        return CreateNewCrossAreaPatrolSegmentUx2NavK(fromArea, draft.Segments);
    }

    private StudioUx2NavKSegmentDraft? CreateNewCrossAreaPatrolSegmentUx2NavK(AreaId fromArea,
        IReadOnlyList<StudioUx2NavKSegmentDraft> existing)
    {
        var project = Project;
        if (project is null) return null;
        var portal = KnownPortalChoicesUx2NavK(project, fromArea)
            .FirstOrDefault(item => KnownWaypointChoicesUx2NavK(project, item.ToAreaId).Count > 0);
        if (portal is null) return null;
        var waypoint = KnownWaypointChoicesUx2NavK(project, portal.ToAreaId)[0];
        var id = Guid.NewGuid();
        while (existing.Any(item => item.Id == id)) id = Guid.NewGuid();
        return new(id, true, null, portal.FromAreaId, portal.ToAreaId, portal.PortalId,
            waypoint.WaypointId, false, true, PatrolOwnershipAuthority.Server);
    }

    internal StudioUx2NavKSegmentDraft? ChangeCrossAreaPatrolPortalUx2NavK(
        StudioUx2NavKSegmentDraft segment, PortalId portalId)
    {
        ArgumentNullException.ThrowIfNull(segment);
        ArgumentNullException.ThrowIfNull(portalId);
        var project = Project;
        if (project is null) return null;
        var portal = KnownPortalChoicesUx2NavK(project, segment.FromAreaId)
            .SingleOrDefault(item => item.PortalId == portalId);
        if (portal is null) return segment.PortalId == portalId ? segment : null;
        var waypoint = KnownWaypointChoicesUx2NavK(project, portal.ToAreaId).FirstOrDefault();
        if (waypoint is null) return null;
        return segment with
        {
            ToAreaId = portal.ToAreaId,
            PortalId = portal.PortalId,
            ResumeWaypointId = waypoint.WaypointId,
            ResumeWaypointIsProtected = false,
        };
    }

    internal StudioUx2NavKPreview PreviewCrossAreaPatrolUx2NavK(StudioUx2NavKDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelCrossAreaPatrolApprovalUx2NavK();
        var history = History;
        var project = Project;
        if (history is null || project is null)
            return FailedCrossAreaPatrolPreviewUx2NavK("Aucun module n’est ouvert.");
        if (!TryBuildCrossAreaPatrolMutationUx2NavK(draft, true, out var command, out var sourceArea,
                out var candidateArea, out var candidateRoute, out var diagnostics, out var message))
            return FailedCrossAreaPatrolPreviewUx2NavK(message, diagnostics);
        var dryRun = _setAreaExperienceUx2NavK.Handle(history.State, command!);
        if (!dryRun.Succeeded)
        {
            var commandDiagnostics = dryRun.Diagnostics.Select(item => new StudioUx2NavKDiagnostic(
                item.Code, $"zone/{draft.AreaLabel}/patrouilles-inter-zones",
                HumanDiagnosticUx2NavK(item.Message))).ToArray();
            return FailedCrossAreaPatrolPreviewUx2NavK(
                commandDiagnostics.FirstOrDefault()?.Message ?? "L’aperçu de patrouille a été refusé.",
                commandDiagnostics);
        }
        var candidateProject = ReplaceAreaUx2NavK(project, candidateArea!);
        StudioUx2NavKRuntimePreview? runtime = null;
        var overlay = StudioNavigationOverlaySnapshot.Empty;
        if (candidateRoute is not null)
        {
            try
            {
                runtime = BuildCrossAreaPatrolRuntimePreviewUx2NavK(candidateProject, candidateRoute);
                overlay = BuildCrossAreaPatrolOverlayUx2NavK(candidateProject, draft.OwnerAreaId,
                    candidateRoute);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                               OverflowException)
            {
                return FailedCrossAreaPatrolPreviewUx2NavK(HumanDiagnosticUx2NavK(exception.Message),
                [
                    new("studio.cross_area_patrol.preview_invalid", "patrouilles-inter-zones/apercu",
                        HumanDiagnosticUx2NavK(exception.Message)),
                ]);
            }
        }
        var sourceRoute = sourceArea!.RuntimePolicies.Experience.PatrolRoutes
            .SingleOrDefault(item => item.Id == draft.RouteId);
        var diff = BuildCrossAreaPatrolDiffUx2NavK(project, sourceRoute, candidateRoute, draft.Remove);
        if (diff.Count > 0)
        {
            _approvedCrossAreaPatrolUx2NavK = draft;
            _approvedCrossAreaPatrolRevisionUx2NavK = history.State.Revision;
            _approvedCrossAreaPatrolFingerprintUx2NavK = CrossAreaPatrolDraftFingerprintUx2NavK(draft);
        }
        var summary = candidateRoute is null
            ? "Patrouille sélectionnée prête à être retirée."
            : $"{candidateRoute.Segments.Count} étape(s) inter-zone · " +
              $"{(candidateRoute.Loop ? "boucle" : "trajet terminé")} · " +
              $"{candidateRoute.Segments.Count(item => item.PrefetchDestination)} préchargement(s) demandé(s)";
        return new(true,
            diff.Count == 0
                ? "Aperçu validé · aucune différence sémantique à appliquer."
                : "Aperçu sec validé · aucun document, chargement moteur, lease ou transfert réseau n’a été exécuté.",
            summary, diff.Count > 0, diff, [], overlay, runtime);
    }

    internal StudioUx2NavKActionResult ApplyApprovedCrossAreaPatrolUx2NavK()
    {
        var draft = _approvedCrossAreaPatrolUx2NavK;
        var history = History;
        if (draft is null || history is null ||
            _approvedCrossAreaPatrolRevisionUx2NavK != history.State.Revision ||
            !string.Equals(_approvedCrossAreaPatrolFingerprintUx2NavK,
                CrossAreaPatrolDraftFingerprintUx2NavK(draft), StringComparison.Ordinal))
        {
            CancelCrossAreaPatrolApprovalUx2NavK();
            return new(false, "Prévisualisez de nouveau la patrouille avant de l’appliquer.");
        }
        if (!TryBuildCrossAreaPatrolMutationUx2NavK(draft, false, out var command, out _, out _, out _,
                out _, out var message))
        {
            CancelCrossAreaPatrolApprovalUx2NavK();
            return new(false, message);
        }
        var currentDryRun = _setAreaExperienceUx2NavK.Handle(history.State, command! with { DryRun = true });
        if (!currentDryRun.Succeeded)
        {
            CancelCrossAreaPatrolApprovalUx2NavK();
            return new(false, HumanDiagnosticUx2NavK(currentDryRun.Diagnostics.FirstOrDefault()?.Message));
        }
        var applied = _setAreaExperienceUx2NavK.Handle(history.State, command!);
        CancelCrossAreaPatrolApprovalUx2NavK();
        if (!applied.Succeeded)
            return new(false, HumanDiagnosticUx2NavK(applied.Diagnostics.FirstOrDefault()?.Message));
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, draft.Remove
            ? "Patrouille retirée · Ctrl+Z pour annuler"
            : "Patrouille appliquée · Ctrl+Z pour annuler, Ctrl+Y pour rétablir");
    }

    internal void CancelCrossAreaPatrolApprovalUx2NavK()
    {
        _approvedCrossAreaPatrolUx2NavK = null;
        _approvedCrossAreaPatrolRevisionUx2NavK = null;
        _approvedCrossAreaPatrolFingerprintUx2NavK = null;
    }

    internal void CloseUx2NavK() => CancelCrossAreaPatrolApprovalUx2NavK();

    internal StudioUx2NavKApprovalSnapshot CaptureCrossAreaPatrolApprovalUx2NavK() => new(
        _approvedCrossAreaPatrolUx2NavK, _approvedCrossAreaPatrolRevisionUx2NavK,
        _approvedCrossAreaPatrolFingerprintUx2NavK);

    internal void RestoreCrossAreaPatrolApprovalUx2NavK(StudioUx2NavKApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedCrossAreaPatrolUx2NavK = snapshot.Draft;
        _approvedCrossAreaPatrolRevisionUx2NavK = snapshot.Revision;
        _approvedCrossAreaPatrolFingerprintUx2NavK = snapshot.Fingerprint;
    }

    private bool TryBuildCrossAreaPatrolMutationUx2NavK(StudioUx2NavKDraft draft, bool dryRun,
        out SetAreaExperienceProfileCommand? command, out AreaDefinition? sourceArea,
        out AreaDefinition? candidateArea, out CrossAreaPatrolRoute? candidateRoute,
        out IReadOnlyList<StudioUx2NavKDiagnostic> diagnostics, out string message)
    {
        command = null;
        sourceArea = null;
        candidateArea = null;
        candidateRoute = null;
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
            message = "Le projet a changé depuis l’ouverture de cette fiche. Rechargez la patrouille.";
            return false;
        }
        sourceArea = project.Areas.SingleOrDefault(item => item.Id == draft.OwnerAreaId);
        if (sourceArea is null)
        {
            message = "La zone propriétaire choisie n’existe plus.";
            return false;
        }
        if (!string.Equals(draft.SourceFingerprint, CrossAreaPatrolSourceFingerprintUx2NavK(project),
                StringComparison.Ordinal))
        {
            message = "Le projet canonique a changé. Rechargez la fiche de patrouille.";
            return false;
        }
        var sourceExperience = sourceArea.RuntimePolicies.Experience;
        var currentRoute = sourceExperience.PatrolRoutes.SingleOrDefault(item => item.Id == draft.RouteId);
        if (draft.IsNew ? currentRoute is not null : currentRoute is null || draft.Original is null ||
            !CrossAreaPatrolRouteEquivalentUx2NavK(currentRoute, draft.Original))
        {
            message = "La patrouille source a été ajoutée, remplacée ou modifiée hors de cette fiche.";
            return false;
        }
        if (draft.Remove)
        {
            if (draft.IsNew || currentRoute is null)
            {
                message = "Une patrouille non appliquée ne peut pas être retirée du projet.";
                return false;
            }
        }
        else
        {
            diagnostics = ValidateCrossAreaPatrolDraftUx2NavK(project, draft, currentRoute);
            if (diagnostics.Count > 0)
            {
                message = diagnostics[0].Message;
                return false;
            }
        }
        try
        {
            var routes = sourceExperience.PatrolRoutes.ToList();
            if (draft.Remove)
            {
                routes.RemoveAll(item => item.Id == draft.RouteId);
            }
            else
            {
                var segments = draft.Segments.Select(item =>
                {
                    var current = currentRoute?.Segments.SingleOrDefault(source => source.Id == item.Id);
                    return current is not null && CrossAreaPatrolSegmentMatchesDraftUx2NavK(current, item)
                        ? current
                        : new CrossAreaPatrolSegment(item.Id, item.FromAreaId, item.ToAreaId, item.PortalId,
                            item.ResumeWaypointId, item.PrefetchDestination, item.Authority);
                }).ToArray();
                var built = currentRoute is not null &&
                            currentRoute.OwnerId == draft.OwnerId.Trim() && currentRoute.Loop == draft.Loop &&
                            currentRoute.Segments.SequenceEqual(segments)
                    ? currentRoute
                    : new CrossAreaPatrolRoute(draft.RouteId, draft.OwnerId.Trim(), segments, draft.Loop,
                        currentRoute?.Provenance ?? Ux2NavKProvenance);
                candidateRoute = built;
                if (draft.IsNew) routes.Add(built);
                else
                {
                    var index = routes.FindIndex(item => item.Id == draft.RouteId);
                    routes[index] = built;
                }
            }
            var candidateExperience = new AreaExperienceProfile(sourceExperience.Id,
                sourceExperience.SchemaVersion, sourceExperience.AudioCues, sourceExperience.ScatterEmitters,
                sourceExperience.Voices, routes, sourceExperience.Loading, sourceExperience.Provenance);
            var policies = sourceArea.RuntimePolicies;
            var candidatePolicies = new AreaRuntimePolicyProfile(policies.Id, policies.SchemaVersion,
                policies.Heartbeats, policies.Rest, policies.Provenance, policies.Cinematics, policies.Rules,
                candidateExperience);
            candidateArea = sourceArea.WithRuntimePolicies(candidatePolicies);
            command = new SetAreaExperienceProfileCommand(CommandId.From(Guid.NewGuid()), draft.BaseRevision,
                sourceArea.Id, candidateExperience, dryRun);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           OverflowException)
        {
            message = HumanDiagnosticUx2NavK(exception.Message);
            diagnostics = [new("studio.cross_area_patrol.profile_invalid", "patrouilles-inter-zones", message)];
            return false;
        }
    }

    private static IReadOnlyList<StudioUx2NavKDiagnostic> ValidateCrossAreaPatrolDraftUx2NavK(
        ProjectDefinition project, StudioUx2NavKDraft draft, CrossAreaPatrolRoute? currentRoute)
    {
        var found = new List<StudioUx2NavKDiagnostic>();
        void Add(string code, string path, string message) => found.Add(new(code, path, message));
        if (draft.OwnerIdIsProtected)
        {
            if (currentRoute is null || draft.OwnerId != currentRoute.OwnerId)
                Add("studio.cross_area_patrol.owner_protected", "patrouille/proprietaire",
                    "La clé propriétaire protégée doit rester inchangée.");
        }
        else if (!HumanOwnerKeyValidUx2NavK(draft.OwnerId))
        {
            Add("studio.cross_area_patrol.owner_invalid", "patrouille/proprietaire",
                "Le propriétaire doit être une clé humaine nommée de 2 à 80 caractères, sans UUID.");
        }
        if (draft.Segments.Count is < 1 or > 32)
            Add("studio.cross_area_patrol.segment_count", "patrouille/etapes",
                "Une patrouille inter-zone doit contenir entre 1 et 32 étapes.");
        if (draft.Segments.Any(item => item.Id == Guid.Empty) ||
            draft.Segments.Select(item => item.Id).Distinct().Count() != draft.Segments.Count)
            Add("studio.cross_area_patrol.segment_identity", "patrouille/etapes",
                "Chaque étape doit conserver une identité interne unique.");
        var currentSegmentIds = currentRoute?.Segments.Select(item => item.Id).ToHashSet() ?? [];
        if (draft.Segments.Any(item => item.IsNew == currentSegmentIds.Contains(item.Id)))
            Add("studio.cross_area_patrol.segment_source", "patrouille/etapes",
                "Une étape a été ajoutée ou remplacée hors de cette fiche.");
        if (draft.Segments.Count > 0 && draft.Segments[0].FromAreaId != draft.OwnerAreaId)
            Add("studio.cross_area_patrol.owner_area", "patrouille/etape-1/source",
                "La première étape doit partir de la zone propriétaire de la patrouille.");
        var catalog = NavigationDestinationCatalog.Build(project);
        for (var index = 0; index < draft.Segments.Count; index++)
        {
            var segment = draft.Segments[index];
            var path = $"patrouille/etape-{index + 1}";
            var source = project.Areas.SingleOrDefault(item => item.Id == segment.FromAreaId);
            var destination = project.Areas.SingleOrDefault(item => item.Id == segment.ToAreaId);
            var portal = source?.Portals.SingleOrDefault(item => item.Id == segment.PortalId);
            if (source is null || destination is null || portal is null ||
                portal.DestinationAreaId != segment.ToAreaId)
                Add("studio.cross_area_patrol.portal_invalid", $"{path}/passage",
                    "Le passage doit exister dans la zone source et mener à la zone cible affichée.");
            var resolved = catalog.Find(NavigationDestinationKind.Waypoint, segment.ResumeWaypointId);
            if (resolved?.AreaId != segment.ToAreaId)
            {
                var original = segment.Original;
                var protectedUnchanged = segment.ResumeWaypointIsProtected && original is not null &&
                                         original.ResumeWaypointId == segment.ResumeWaypointId &&
                                         original.PortalId == segment.PortalId &&
                                         original.FromAreaId == segment.FromAreaId &&
                                         original.ToAreaId == segment.ToAreaId;
                if (!protectedUnchanged)
                    Add("studio.cross_area_patrol.resume_invalid", $"{path}/reprise",
                        "Le point de reprise doit être un waypoint humain de la zone cible.");
            }
            if (index > 0 && draft.Segments[index - 1].ToAreaId != segment.FromAreaId)
                Add("studio.cross_area_patrol.chain_invalid", $"{path}/source",
                    "Les étapes doivent former une chaîne continue entre les zones.");
        }
        if (draft.Loop && draft.Segments.Count > 0 &&
            draft.Segments[^1].ToAreaId != draft.Segments[0].FromAreaId)
            Add("studio.cross_area_patrol.loop_invalid", "patrouille/boucle",
                "Une boucle doit revenir dans la zone de départ.");
        return found;
    }

    private static StudioUx2NavKRuntimePreview BuildCrossAreaPatrolRuntimePreviewUx2NavK(
        ProjectDefinition project, CrossAreaPatrolRoute route)
    {
        var cursor = new CrossAreaPatrolCursor(0, route.Segments[0].FromAreaId, Guid.Empty, false, false);
        var ownershipGuarded = false;
        try
        {
            _ = CrossAreaPatrolRuntimeService.Advance(route, cursor, false, true);
        }
        catch (InvalidOperationException exception)
        {
            ownershipGuarded = exception.Message == "PATROL_OWNERSHIP_REQUIRED";
        }
        var steps = new List<StudioUx2NavKRuntimeStep>();
        for (var index = 0; index < route.Segments.Count; index++)
        {
            var segment = route.Segments[index];
            var waiting = CrossAreaPatrolRuntimeService.Advance(route, cursor, true, false);
            var loaded = CrossAreaPatrolRuntimeService.Advance(route, waiting.Cursor, true, true);
            steps.Add(new(index + 1, AreaLabelUx2NavK(project, segment.FromAreaId),
                AreaLabelUx2NavK(project, segment.ToAreaId), PortalLabelUx2NavK(project, segment),
                WaypointLabelUx2NavK(project, segment), waiting.Cursor.AwaitingStreaming,
                !loaded.Cursor.AwaitingStreaming && loaded.Cursor.CurrentAreaId == segment.ToAreaId &&
                loaded.Cursor.ResumeWaypointId == segment.ResumeWaypointId,
                waiting.PrefetchDestination, AuthorityLabelUx2NavK(segment.Authority)));
            cursor = loaded.Cursor;
        }
        var completesOrLoops = route.Loop
            ? !cursor.Complete && cursor.SegmentIndex == 0 && cursor.CurrentAreaId == route.Segments[0].FromAreaId
            : cursor.Complete;
        return new(steps, ownershipGuarded, completesOrLoops,
            "Simulation canonique du curseur, de l’attente et de la reprise seulement · aucun chargement moteur, " +
            "lease, réplication, transport inter-processus, transfert d’acteur ou préchargement réel n’est exécuté.");
    }

    private static StudioNavigationOverlaySnapshot BuildCrossAreaPatrolOverlayUx2NavK(
        ProjectDefinition project, AreaId ownerAreaId, CrossAreaPatrolRoute route)
    {
        var markers = new List<StudioNavigationOverlayMarker>();
        foreach (var segment in route.Segments.Where(item => item.FromAreaId == ownerAreaId))
        {
            var area = project.Areas.Single(item => item.Id == segment.FromAreaId);
            var portal = area.Portals.SingleOrDefault(item => item.Id == segment.PortalId);
            var entity = portal is null ? null : area.Entities.SingleOrDefault(item => item.Id == portal.SourceEntityId);
            if (entity is null) continue;
            markers.Add(new($"ux2-nav-k:{route.Id:D}:{segment.Id:D}",
                $"Sortie · {HumanUx2NavK(portal!.DisplayName, "Passage protégé")}",
                StudioNavigationOverlaySemantic.Transition,
                new StudioNavigationPoint(entity.Transform.Position.X, entity.Transform.Position.Y,
                    entity.Transform.Position.Z), Selected: true));
        }
        return new(markers, [], []);
    }

    private static IReadOnlyList<string> BuildCrossAreaPatrolDiffUx2NavK(ProjectDefinition project,
        CrossAreaPatrolRoute? source, CrossAreaPatrolRoute? candidate, bool remove)
    {
        if (remove && source is not null)
            return [$"Retirer « {HumanOwnerLabelUx2NavK(source.OwnerId)} » et ses {source.Segments.Count} étape(s)."];
        if (candidate is null) return [];
        if (source is null)
            return [$"Créer « {HumanOwnerLabelUx2NavK(candidate.OwnerId)} » avec " +
                    $"{candidate.Segments.Count} étape(s) inter-zone."];
        if (CrossAreaPatrolRouteEquivalentUx2NavK(source, candidate)) return [];
        var diff = new List<string>();
        if (source.OwnerId != candidate.OwnerId)
            diff.Add($"Propriétaire : {HumanOwnerLabelUx2NavK(source.OwnerId)} → " +
                     $"{HumanOwnerLabelUx2NavK(candidate.OwnerId)}.");
        if (source.Loop != candidate.Loop)
            diff.Add(candidate.Loop ? "Boucle activée." : "Fin de trajet activée.");
        if (!source.Segments.Select(item => item.Id).SequenceEqual(candidate.Segments.Select(item => item.Id)))
            diff.Add("Ordre : " + string.Join(" → ", candidate.Segments.Select((item, index) =>
                $"{index + 1}. {AreaLabelUx2NavK(project, item.ToAreaId)}")) + ".");
        foreach (var segment in candidate.Segments)
        {
            var previous = source.Segments.SingleOrDefault(item => item.Id == segment.Id);
            if (previous is null)
                diff.Add($"Ajouter l’étape vers {AreaLabelUx2NavK(project, segment.ToAreaId)} via " +
                         $"{PortalLabelUx2NavK(project, segment)}.");
            else if (previous != segment)
                diff.Add($"Étape vers {AreaLabelUx2NavK(project, segment.ToAreaId)} : " +
                         $"{PortalLabelUx2NavK(project, segment)}, reprise {WaypointLabelUx2NavK(project, segment)}, " +
                         $"préchargement {(segment.PrefetchDestination ? "demandé" : "non demandé")}, " +
                         $"autorité {AuthorityLabelUx2NavK(segment.Authority)}.");
        }
        foreach (var removed in source.Segments.Where(item => candidate.Segments.All(value => value.Id != item.Id)))
            diff.Add($"Retirer l’étape vers {AreaLabelUx2NavK(project, removed.ToAreaId)}.");
        if (diff.Count == 0) diff.Add("Configuration de la patrouille mise à jour.");
        return diff;
    }

    private static IReadOnlyList<StudioUx2NavKPortalChoice> KnownPortalChoicesUx2NavK(
        ProjectDefinition project, AreaId fromAreaId)
    {
        var source = project.Areas.SingleOrDefault(item => item.Id == fromAreaId);
        if (source is null) return [];
        return source.Portals.Select(portal => new StudioUx2NavKPortalChoice(portal.Id,
                source.Id, portal.DestinationAreaId,
                $"{HumanUx2NavK(portal.DisplayName, "Passage protégé")} · " +
                $"{AreaLabelUx2NavK(project, source.Id)} → " +
                $"{AreaLabelUx2NavK(project, portal.DestinationAreaId)}"))
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.PortalId.Value)
            .ToArray();
    }

    private static IReadOnlyList<StudioUx2NavKWaypointChoice> KnownWaypointChoicesUx2NavK(
        ProjectDefinition project, AreaId areaId) => NavigationDestinationCatalog.Build(project).Entries
        .Where(item => item.Kind == NavigationDestinationKind.Waypoint && item.AreaId == areaId)
        .Select(item => new StudioUx2NavKWaypointChoice(item.Id, areaId, item.HumanLabel))
        .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
        .ThenBy(item => item.WaypointId)
        .ToArray();

    private static StudioUx2NavKSegmentDraft SegmentDraftUx2NavK(CrossAreaPatrolSegment segment,
        NavigationDestinationCatalog catalog) => new(segment.Id, false, segment, segment.FromAreaId,
        segment.ToAreaId, segment.PortalId, segment.ResumeWaypointId,
        catalog.Find(NavigationDestinationKind.Waypoint, segment.ResumeWaypointId)?.AreaId != segment.ToAreaId,
        segment.PrefetchDestination, segment.Authority);

    private static bool CrossAreaPatrolSegmentMatchesDraftUx2NavK(CrossAreaPatrolSegment segment,
        StudioUx2NavKSegmentDraft draft) => segment.Id == draft.Id && segment.FromAreaId == draft.FromAreaId &&
        segment.ToAreaId == draft.ToAreaId && segment.PortalId == draft.PortalId &&
        segment.ResumeWaypointId == draft.ResumeWaypointId &&
        segment.PrefetchDestination == draft.PrefetchDestination && segment.Authority == draft.Authority;

    private static bool CrossAreaPatrolRouteEquivalentUx2NavK(CrossAreaPatrolRoute left,
        CrossAreaPatrolRoute right) => left.Id == right.Id && left.OwnerId == right.OwnerId &&
        left.Loop == right.Loop && left.Provenance == right.Provenance &&
        left.Segments.SequenceEqual(right.Segments);

    private static bool HumanOwnerKeyValidUx2NavK(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length is >= 2 and <= 80 && !trimmed.Any(char.IsControl) &&
               !ContainsTechnicalIdentifierUx2NavK(trimmed);
    }

    private static string AreaLabelUx2NavK(ProjectDefinition project, AreaId areaId)
    {
        var area = project.Areas.SingleOrDefault(item => item.Id == areaId);
        return area is null ? "Zone protégée" : HumanUx2NavK(area.DisplayName, "Zone protégée");
    }

    private static string PortalLabelUx2NavK(ProjectDefinition project, CrossAreaPatrolSegment segment)
    {
        var portal = project.Areas.SingleOrDefault(item => item.Id == segment.FromAreaId)?.Portals
            .SingleOrDefault(item => item.Id == segment.PortalId);
        return portal is null ? "passage protégé" : HumanUx2NavK(portal.DisplayName, "passage protégé");
    }

    private static string WaypointLabelUx2NavK(ProjectDefinition project, CrossAreaPatrolSegment segment) =>
        NavigationDestinationCatalog.Build(project)
            .Find(NavigationDestinationKind.Waypoint, segment.ResumeWaypointId)?.HumanLabel ??
        "point de reprise protégé";

    private static string AuthorityLabelUx2NavK(PatrolOwnershipAuthority value) => value switch
    {
        PatrolOwnershipAuthority.Local => "locale",
        PatrolOwnershipAuthority.Host => "hôte",
        PatrolOwnershipAuthority.Server => "serveur",
        _ => "protégée",
    };

    private static string HumanOwnerLabelUx2NavK(string value)
    {
        if (ContainsTechnicalIdentifierUx2NavK(value)) return "Patrouille protégée";
        var label = Regex.Replace(value.Trim(), "[._-]+", " ");
        return string.IsNullOrWhiteSpace(label) ? "Patrouille" : $"Patrouille · {label}";
    }

    private static string HumanUx2NavK(string? value, string fallback)
    {
        var candidate = value?.Trim();
        return string.IsNullOrWhiteSpace(candidate) || ContainsTechnicalIdentifierUx2NavK(candidate)
            ? fallback : candidate;
    }

    private static bool ContainsTechnicalIdentifierUx2NavK(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (Guid.TryParse(value, out _) || Regex.IsMatch(value,
            "(?i)[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}"));

    private static string HumanDiagnosticUx2NavK(string? value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "Configuration refusée." : value.Trim();
        return Regex.Replace(text,
            "(?i)[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}",
            "identité interne");
    }

    private static ProjectDefinition ReplaceAreaUx2NavK(ProjectDefinition project, AreaDefinition area) => new(
        project.Id, project.Title, project.Module, project.Blueprints,
        project.Areas.Select(item => item.Id == area.Id ? area : item).ToArray(),
        project.Dialogues, project.Quests);

    private static StudioUx2NavKPreview FailedCrossAreaPatrolPreviewUx2NavK(string message,
        IReadOnlyList<StudioUx2NavKDiagnostic>? diagnostics = null) => new(false, message,
        "Aucun aperçu canonique disponible.", false, [], diagnostics ?? [],
        StudioNavigationOverlaySnapshot.Empty, null);

    private static string CrossAreaPatrolSourceFingerprintUx2NavK(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));

    private static string CrossAreaPatrolDraftFingerprintUx2NavK(StudioUx2NavKDraft draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(draft.OwnerAreaId.Value.ToByteArray());
        writer.Write(draft.RouteId.ToByteArray());
        writer.Write(draft.IsNew);
        writer.Write(draft.Remove);
        writer.Write(draft.BaseRevision.Value);
        WriteUx2NavK(writer, draft.SourceFingerprint);
        writer.Write(draft.OwnerIdIsProtected);
        WriteUx2NavK(writer, draft.OwnerId);
        writer.Write(draft.Loop);
        writer.Write(draft.Segments.Count);
        foreach (var item in draft.Segments)
        {
            writer.Write(item.Id.ToByteArray());
            writer.Write(item.IsNew);
            writer.Write(item.FromAreaId.Value.ToByteArray());
            writer.Write(item.ToAreaId.Value.ToByteArray());
            writer.Write(item.PortalId.Value.ToByteArray());
            writer.Write(item.ResumeWaypointId.ToByteArray());
            writer.Write(item.ResumeWaypointIsProtected);
            writer.Write(item.PrefetchDestination);
            writer.Write((int)item.Authority);
        }
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteUx2NavK(BinaryWriter writer, string? value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    internal ProjectDefinition? CreateUx2NavKEvidenceProject()
    {
        var source = Project;
        return source is null ? null : CreateUx2NavKEvidenceProject(source.Id.Value);
    }

    private static ProjectDefinition CreateUx2NavKEvidenceProject(Guid seed)
    {
        var project = CreateUx2NavJEvidenceProject(seed == Guid.Empty
            ? Guid.Parse("2a110000-0000-4000-8000-000000000001") : seed);
        var source = project.Areas[0];
        var target = project.Areas[1];
        var blueprint = project.Blueprints[0];
        var sourceEntity = new EntityInstance(EntityId.From(StableUx2NavK(seed, "entity:source")), blueprint.Id,
            new TransformValue(new Vector3Value(8, 0, 6), QuaternionValue.Identity, Vector3Value.One));
        var targetEntity = new EntityInstance(EntityId.From(StableUx2NavK(seed, "entity:target")), blueprint.Id,
            new TransformValue(new Vector3Value(-6, 0, -3), QuaternionValue.Identity, Vector3Value.One));
        source = source.WithEntities(source.Entities.Append(sourceEntity).ToArray());
        target = target.WithEntities(target.Entities.Append(targetEntity).ToArray());
        var outward = new PortalLink(PortalId.From(StableUx2NavK(seed, "portal:outward")),
            "Passage des braises", sourceEntity.Id, target.Id, targetEntity.Id, true, true,
            provenance: Ux2NavKEvidenceProvenance);
        var returning = new PortalLink(PortalId.From(StableUx2NavK(seed, "portal:return")),
            "Passage du retour", targetEntity.Id, source.Id, sourceEntity.Id, true, false,
            provenance: Ux2NavKEvidenceProvenance);
        source = source.WithPortals(source.Portals.Append(outward).ToArray());
        target = target.WithPortals(target.Portals.Append(returning).ToArray());
        var sourceWaypoint = source.Navigation.Waypoints.Waypoints[0];
        var targetProfile = target.Navigation.Waypoints;
        var targetWaypoint = new WaypointProfile(StableUx2NavK(seed, "waypoint:target"), 1,
            Ux2NavKEvidenceProvenance, "Halte de la lisière", new Vector3Value(-4, 0, 2),
            new WaypointEditorAppearance("flag", "#E8B568", "Patrouilles", true, 26), null,
            RequiredAtRuntime: true);
        var targetWaypoints = new WaypointAuthoringProfile(targetProfile.Id, targetProfile.SchemaVersion,
            targetProfile.Provenance, target.Id, targetProfile.RequiredAtRuntime,
            targetProfile.Waypoints.Append(targetWaypoint).ToArray(), targetProfile.PatrolRoutes,
            targetProfile.Locators, targetProfile.Collections, targetProfile.RouteVisualization);
        target = target.WithNavigation(target.Navigation.WithWaypoints(targetWaypoints));
        var first = new CrossAreaPatrolSegment(StableUx2NavK(seed, "segment:outward"), source.Id, target.Id,
            outward.Id, targetWaypoint.Id, true, PatrolOwnershipAuthority.Server);
        var second = new CrossAreaPatrolSegment(StableUx2NavK(seed, "segment:return"), target.Id, source.Id,
            returning.Id, sourceWaypoint.Id, false, PatrolOwnershipAuthority.Host);
        var route = new CrossAreaPatrolRoute(StableUx2NavK(seed, "route:main"), "garde.braises",
            [first, second], true, Ux2NavKEvidenceProvenance);
        var neighbor = new CrossAreaPatrolRoute(StableUx2NavK(seed, "route:neighbor"), "messager.lisiere",
            [first with { Id = StableUx2NavK(seed, "segment:neighbor") }], false, "fixture:neighbor");
        var experience = source.RuntimePolicies.Experience;
        var configuredExperience = new AreaExperienceProfile(experience.Id, experience.SchemaVersion,
            experience.AudioCues, experience.ScatterEmitters, experience.Voices, [route, neighbor],
            experience.Loading, Ux2NavKEvidenceProvenance);
        var policies = source.RuntimePolicies;
        source = source.WithRuntimePolicies(new AreaRuntimePolicyProfile(policies.Id, policies.SchemaVersion,
            policies.Heartbeats, policies.Rest, policies.Provenance, policies.Cinematics, policies.Rules,
            configuredExperience));
        return new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
            project.Areas.Select(item => item.Id == source.Id ? source : item.Id == target.Id ? target : item)
                .ToArray(), project.Dialogues, project.Quests);
    }

    private static Guid StableUx2NavK(Guid seed, string discriminator)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"ux2-nav-k:{seed:D}:{discriminator}"));
        Span<byte> id = stackalloc byte[16];
        bytes.AsSpan(0, id.Length).CopyTo(id);
        id[6] = (byte)((id[6] & 0x0f) | 0x40);
        id[8] = (byte)((id[8] & 0x3f) | 0x80);
        return new Guid(id);
    }

    internal StudioUx2NavKSmokeResult RunUx2NavKContractSmoke()
    {
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceRevision = sourceHistory?.State.Revision;
        var sourceJournal = sourceHistory?.Journal.ToArray() ?? [];
        var sourceDirty = IsDirty;
        var sourceUndo = sourceHistory?.CanUndo;
        var sourceRedo = sourceHistory?.CanRedo;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceApproval = CaptureCrossAreaPatrolApprovalUx2NavK();
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-nav-k-smoke-" + Guid.NewGuid().ToString("N"));
        StudioSessionController? editing = null;
        StudioSessionController? reopened = null;
        try
        {
            var fixture = CreateUx2NavKEvidenceProject(
                Guid.Parse("2a110000-0000-4000-8000-000000000099"));
            if (!ProjectSession.Create(tempRoot, fixture).Succeeded) return FailedUx2NavKSmoke();
            editing = new StudioSessionController();
            if (!editing.Open(tempRoot).Succeeded) return FailedUx2NavKSmoke();
            var areaChoice = editing.CrossAreaPatrolAreaChoicesUx2NavK.Single(item => item.IsEvidence);
            var routeChoice = editing.CrossAreaPatrolRouteChoicesUx2NavK(areaChoice.AreaId)
                .Single(item => item.IsEvidence);
            var explicitSelection = editing.CreateCrossAreaPatrolDraftUx2NavK(null, null) is null &&
                                    editing.CreateCrossAreaPatrolDraftUx2NavK(areaChoice.AreaId, null) is null &&
                                    editing.CreateCrossAreaPatrolDraftUx2NavK(areaChoice.AreaId,
                                        routeChoice.RouteId) is not null;
            var humanLabels = editing.CrossAreaPatrolAreaChoicesUx2NavK.All(item =>
                                  !ContainsTechnicalIdentifierUx2NavK(item.Label)) &&
                              editing.CrossAreaPatrolRouteChoicesUx2NavK(areaChoice.AreaId).All(item =>
                                  !ContainsTechnicalIdentifierUx2NavK(item.Label));

            var original = editing.CreateCrossAreaPatrolDraftUx2NavK(areaChoice.AreaId,
                routeChoice.RouteId)!;
            var invalidWaypoint = editing.PreviewCrossAreaPatrolUx2NavK(original with
            {
                Segments = original.Segments.Select((item, index) => index == 0
                    ? item with
                    {
                        ResumeWaypointId = Guid.Parse("2a110000-0000-4000-8000-0000000000ff"),
                        ResumeWaypointIsProtected = false,
                    }
                    : item).ToArray(),
            });
            var invalidOrder = editing.PreviewCrossAreaPatrolUx2NavK(original with
            {
                Segments = original.Segments.Reverse().ToArray(),
            });
            var staleSource = editing.PreviewCrossAreaPatrolUx2NavK(original with
            {
                SourceFingerprint = "stale",
            });

            var staleRevisionDraft = original;
            var neighborArea = editing.Project!.Areas.First(item => item.Id != areaChoice.AreaId);
            var neighborChanged = ApplyUx2NavKNeighborMutation(editing, neighborArea);
            var staleRevision = editing.PreviewCrossAreaPatrolUx2NavK(staleRevisionDraft);
            var staleRevisionBlocked = neighborChanged && !staleRevision.Succeeded && editing.Undo().Succeeded;

            var approvalDraft = editing.CreateCrossAreaPatrolDraftUx2NavK(areaChoice.AreaId,
                routeChoice.RouteId)!;
            approvalDraft = approvalDraft with
            {
                Segments = approvalDraft.Segments.Select((item, index) => index == 0
                    ? item with { PrefetchDestination = !item.PrefetchDestination } : item).ToArray(),
            };
            var approvalPreview = editing.PreviewCrossAreaPatrolUx2NavK(approvalDraft);
            neighborChanged = ApplyUx2NavKNeighborMutation(editing, neighborArea);
            var staleApply = editing.ApplyApprovedCrossAreaPatrolUx2NavK();
            var staleApprovalBlocked = approvalPreview.Succeeded && neighborChanged &&
                                       !staleApply.Succeeded && editing.Undo().Succeeded;

            var fingerprintDraft = editing.CreateCrossAreaPatrolDraftUx2NavK(areaChoice.AreaId,
                routeChoice.RouteId)! with { OwnerId = "garde empreinte" };
            var fingerprintPreview = editing.PreviewCrossAreaPatrolUx2NavK(fingerprintDraft);
            var fingerprintApproval = editing.CaptureCrossAreaPatrolApprovalUx2NavK();
            editing.RestoreCrossAreaPatrolApprovalUx2NavK(fingerprintApproval with
            {
                Draft = fingerprintApproval.Draft! with { OwnerId = "garde modifiée après aperçu" },
            });
            var fingerprintApply = editing.ApplyApprovedCrossAreaPatrolUx2NavK();
            var approvalFingerprintBlocked = fingerprintPreview.Succeeded && !fingerprintApply.Succeeded;

            var createdDraft = editing.CreateNewCrossAreaPatrolDraftUx2NavK(areaChoice.AreaId)! with
            {
                OwnerId = "patrouille du guet",
            };
            var createPreview = editing.PreviewCrossAreaPatrolUx2NavK(createdDraft);
            var createApply = editing.ApplyApprovedCrossAreaPatrolUx2NavK();
            var created = createPreview.Succeeded && createApply.Succeeded &&
                          editing.Project!.Areas.Single(item => item.Id == areaChoice.AreaId)
                              .RuntimePolicies.Experience.PatrolRoutes.Any(item => item.Id == createdDraft.RouteId);
            var removeDraft = editing.CreateCrossAreaPatrolDraftUx2NavK(areaChoice.AreaId,
                createdDraft.RouteId)! with { Remove = true };
            var removePreview = editing.PreviewCrossAreaPatrolUx2NavK(removeDraft);
            var removeApply = editing.ApplyApprovedCrossAreaPatrolUx2NavK();
            var removed = removePreview.Succeeded && removeApply.Succeeded &&
                          editing.Project!.Areas.Single(item => item.Id == areaChoice.AreaId)
                              .RuntimePolicies.Experience.PatrolRoutes.All(item => item.Id != createdDraft.RouteId);
            var restoreRemoved = editing.Undo().Succeeded &&
                                 editing.Project!.Areas.Single(item => item.Id == areaChoice.AreaId)
                                     .RuntimePolicies.Experience.PatrolRoutes.Any(item =>
                                         item.Id == createdDraft.RouteId);
            var restoreInitial = editing.Undo().Succeeded &&
                                 editing.Project!.Areas.Single(item => item.Id == areaChoice.AreaId)
                                     .RuntimePolicies.Experience.PatrolRoutes.All(item =>
                                         item.Id != createdDraft.RouteId);
            var replayCreated = editing.Redo().Succeeded;
            var replayRemoved = editing.Redo().Succeeded;
            var createEditRemove = created && removed && restoreRemoved && restoreInitial &&
                                   replayCreated && replayRemoved;

            var draft = editing.CreateCrossAreaPatrolDraftUx2NavK(areaChoice.AreaId,
                routeChoice.RouteId)!;
            var sourceTarget = editing.Project!.Areas.Single(item => item.Id == areaChoice.AreaId);
            var sourceExperience = sourceTarget.RuntimePolicies.Experience;
            var sourcePolicies = sourceTarget.RuntimePolicies;
            var neighborRoute = sourceExperience.PatrolRoutes.Single(item => item.Id != draft.RouteId);
            var configured = draft with
            {
                OwnerId = "garde des passages",
                Segments = draft.Segments.Select((item, index) => index == 0
                    ? item with
                    {
                        PrefetchDestination = false,
                        Authority = PatrolOwnershipAuthority.Host,
                    }
                    : item with
                    {
                        PrefetchDestination = true,
                        Authority = PatrolOwnershipAuthority.Server,
                    }).ToArray(),
            };
            var projectBeforeApply = editing.Project;
            var preview = editing.PreviewCrossAreaPatrolUx2NavK(configured);
            var dryRun = preview.Succeeded && preview.CanApply &&
                         ReferenceEquals(projectBeforeApply, editing.Project);
            var runtimePreview = preview.Runtime is { Steps.Count: 2, OwnershipGuarded: true,
                CompletesOrLoops: true } && preview.Runtime.Steps.All(item =>
                item.WaitsForStreaming && item.ResumesAfterLoading);
            var runtimeBoundary = preview.Runtime is { Boundary: var boundary } &&
                                  boundary.Contains("aucun chargement moteur",
                                      StringComparison.OrdinalIgnoreCase) &&
                                  boundary.Contains("transport inter-processus",
                                      StringComparison.OrdinalIgnoreCase);
            var apply = editing.ApplyApprovedCrossAreaPatrolUx2NavK();
            var appliedProject = editing.Project;
            var finalArea = appliedProject?.Areas.Single(item => item.Id == areaChoice.AreaId);
            var finalExperience = finalArea?.RuntimePolicies.Experience;
            var finalRoute = finalExperience?.PatrolRoutes.Single(item => item.Id == configured.RouteId);
            var segmentOrder = finalRoute is not null && finalRoute.Segments.Select(item => item.Id)
                .SequenceEqual(configured.Segments.Select(item => item.Id));
            var portalWaypointResolution = finalRoute is not null && finalRoute.Segments.All(segment =>
            {
                var portal = appliedProject!.Areas.Single(item => item.Id == segment.FromAreaId).Portals
                    .SingleOrDefault(item => item.Id == segment.PortalId);
                var waypoint = NavigationDestinationCatalog.Build(appliedProject!)
                    .Find(NavigationDestinationKind.Waypoint, segment.ResumeWaypointId);
                return portal?.DestinationAreaId == segment.ToAreaId && waypoint?.AreaId == segment.ToAreaId;
            });
            var experienceTails = finalExperience is not null && finalArea is not null &&
                                  finalExperience.Id == sourceExperience.Id &&
                                  finalExperience.SchemaVersion == sourceExperience.SchemaVersion &&
                                  finalExperience.AudioCues.SequenceEqual(sourceExperience.AudioCues) &&
                                  finalExperience.ScatterEmitters.SequenceEqual(sourceExperience.ScatterEmitters) &&
                                  finalExperience.Voices.SequenceEqual(sourceExperience.Voices) &&
                                  ReferenceEquals(finalExperience.Loading, sourceExperience.Loading) &&
                                  finalExperience.Provenance == sourceExperience.Provenance &&
                                  finalArea.RuntimePolicies.Heartbeats.SequenceEqual(sourcePolicies.Heartbeats) &&
                                  ReferenceEquals(finalArea.RuntimePolicies.Rest, sourcePolicies.Rest) &&
                                  ReferenceEquals(finalArea.RuntimePolicies.Cinematics, sourcePolicies.Cinematics) &&
                                  ReferenceEquals(finalArea.RuntimePolicies.Rules, sourcePolicies.Rules);
            var neighborRoutes = finalExperience is not null &&
                                 ReferenceEquals(finalExperience.PatrolRoutes.Single(item =>
                                     item.Id == neighborRoute.Id), neighborRoute);
            var navigationLots = finalArea is not null &&
                                 ReferenceEquals(finalArea.Navigation, sourceTarget.Navigation) &&
                                 ReferenceEquals(finalArea.Environment, sourceTarget.Environment) &&
                                 ReferenceEquals(finalArea.Authoring, sourceTarget.Authoring) &&
                                 ReferenceEquals(finalArea.World, sourceTarget.World) &&
                                 ReferenceEquals(finalArea.Foundation, sourceTarget.Foundation) &&
                                 ReferenceEquals(finalArea.Playtest, sourceTarget.Playtest);
            var canonicalCommand = editing.History?.Journal.LastOrDefault()?.CommandName ==
                                   "set_area_experience_profile";
            var undo = editing.Undo();
            var undone = undo.Succeeded && ReferenceEquals(projectBeforeApply, editing.Project);
            var redo = editing.Redo();
            var redone = redo.Succeeded && ReferenceEquals(appliedProject, editing.Project);
            var undoRedo = undone && redone;
            var save = editing.Save();
            reopened = new StudioSessionController();
            var reopenedOk = save.Succeeded && reopened.Open(tempRoot).Succeeded;
            var loadedRoute = reopened.Project?.Areas.Single(item => item.Id == areaChoice.AreaId)
                .RuntimePolicies.Experience.PatrolRoutes.Single(item => item.Id == configured.RouteId);
            var persistence = reopenedOk && finalRoute is not null && loadedRoute is not null &&
                              CrossAreaPatrolRouteEquivalentUx2NavK(loadedRoute, finalRoute);
            var projectNeighbors = finalArea is not null && appliedProject is not null &&
                                   appliedProject.Areas.Where(item => item.Id != areaChoice.AreaId)
                                       .SequenceEqual(projectBeforeApply!.Areas.Where(item =>
                                           item.Id != areaChoice.AreaId)) &&
                                   ReferenceEquals(appliedProject.Module, projectBeforeApply.Module) &&
                                   appliedProject.Blueprints.SequenceEqual(projectBeforeApply.Blueprints) &&
                                   appliedProject.Dialogues.SequenceEqual(projectBeforeApply.Dialogues) &&
                                   appliedProject.Quests.SequenceEqual(projectBeforeApply.Quests);
            var text = string.Join(' ', editing.CrossAreaPatrolAreaChoicesUx2NavK.Select(item => item.Label)) +
                       " " + string.Join(' ', editing.CrossAreaPatrolRouteChoicesUx2NavK(areaChoice.AreaId)
                           .Select(item => item.Label)) + " " + preview.Summary + " " +
                       string.Join(' ', preview.Diff) + " " +
                       string.Join(' ', preview.Runtime?.Steps.Select(item =>
                           $"{item.SourceArea} {item.DestinationArea} {item.Portal} {item.ResumeWaypoint}") ?? []);
            var humanText = !ContainsTechnicalIdentifierUx2NavK(text) &&
                            preview.Overlay.Markers.All(item =>
                                !ContainsTechnicalIdentifierUx2NavK(item.HumanLabel));
            var validation = !invalidWaypoint.Succeeded && !invalidOrder.Succeeded && !staleSource.Succeeded;
            var sourceUntouched = ReferenceEquals(sourceProject, Project) &&
                                  ReferenceEquals(sourceHistory, History) && History?.State.Revision == sourceRevision &&
                                  (History?.Journal ?? []).SequenceEqual(sourceJournal) && IsDirty == sourceDirty &&
                                  History?.CanUndo == sourceUndo && History?.CanRedo == sourceRedo &&
                                  ActiveAreaId == sourceArea && SelectedEntityIds.SequenceEqual(sourceSelection) &&
                                  SelectedEntityId == sourceSelected &&
                                  CaptureCrossAreaPatrolApprovalUx2NavK() == sourceApproval;
            var succeeded = explicitSelection && humanLabels && createEditRemove && segmentOrder &&
                            portalWaypointResolution && runtimePreview && runtimeBoundary && validation && dryRun &&
                            staleRevisionBlocked && staleApprovalBlocked && approvalFingerprintBlocked &&
                            canonicalCommand && undoRedo && persistence && experienceTails && neighborRoutes &&
                            navigationLots && projectNeighbors && sourceUntouched && humanText && apply.Succeeded;
            return new(succeeded, explicitSelection, humanLabels, createEditRemove, segmentOrder,
                portalWaypointResolution, runtimePreview, runtimeBoundary, validation, dryRun,
                staleRevisionBlocked, !staleSource.Succeeded, staleApprovalBlocked,
                approvalFingerprintBlocked, canonicalCommand, undoRedo, persistence, experienceTails,
                neighborRoutes, navigationLots, projectNeighbors, sourceUntouched, humanText);
        }
        catch
        {
            return FailedUx2NavKSmoke();
        }
        finally
        {
            reopened?.Close();
            editing?.Close();
            _ = DeleteUx2NavKTemporaryWorkspace(tempRoot);
        }
    }

    private static bool ApplyUx2NavKNeighborMutation(StudioSessionController controller,
        AreaDefinition area)
    {
        var history = controller.History!;
        var source = area.RuntimePolicies.Experience;
        var candidate = new AreaExperienceProfile(source.Id, source.SchemaVersion, source.AudioCues,
            source.ScatterEmitters, source.Voices, source.PatrolRoutes, source.Loading,
            $"{source.Provenance}:ux2-nav-k-neighbor");
        var result = controller._setAreaExperienceUx2NavK.Handle(history.State,
            new SetAreaExperienceProfileCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                area.Id, candidate, false));
        if (!result.Succeeded) return false;
        history.Record(result);
        return true;
    }

    internal static bool DeleteUx2NavKTemporaryWorkspace(string? directory)
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
                !leaf.StartsWith("openrpg-forge-studio-ux2-nav-k-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, true);
            return !Directory.Exists(full);
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static StudioUx2NavKSmokeResult FailedUx2NavKSmoke() => new(
        Succeeded: false,
        ExplicitSelection: false,
        HumanLabels: false,
        CreateEditRemove: false,
        SegmentOrder: false,
        PortalWaypointResolution: false,
        RuntimePreview: false,
        RuntimeBoundary: false,
        Validation: false,
        DryRun: false,
        StaleRevisionBlocked: false,
        StaleSourceBlocked: false,
        StaleApprovalBlocked: false,
        ApprovalFingerprintBlocked: false,
        CanonicalCommand: false,
        UndoRedo: false,
        Persistence: false,
        ExperienceTailsPreserved: false,
        NeighborRoutesPreserved: false,
        NavigationLotsPreserved: false,
        ProjectNeighborsPreserved: false,
        SourceUntouched: false,
        HumanText: false);
}
