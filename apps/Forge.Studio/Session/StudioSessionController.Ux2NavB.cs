using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Commands;
using Forge.Application.Navigation;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Core.Navigation;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2NavBProvenance = "studio:ux2-nav-b:navigation-foundation";
    private const string Ux2NavBEvidenceProvenance = "studio:ux2-nav-b:evidence";
    private const int Ux2NavBMaximumDisplayedCells = 640;

    private readonly SetNavigationTopologyHandler _setNavigationTopologyUx2NavB = new(new SystemClock());
    private StudioUx2NavBDraft? _approvedNavigationUx2NavB;
    private ProjectRevision? _approvedNavigationRevisionUx2NavB;
    private string? _approvedNavigationFingerprintUx2NavB;

    internal IReadOnlyList<StudioUx2NavBAreaChoice> NavigationAreaChoicesUx2NavB
    {
        get
        {
            var project = Project;
            return project is null ? [] : BuildNavigationAreaChoicesUx2NavB(project);
        }
    }

    internal IReadOnlyList<StudioUx2NavBBlueprintChoice> SpawnBlueprintChoicesUx2NavB
    {
        get
        {
            var project = Project;
            if (project is null)
            {
                return [];
            }
            var duplicates = project.Blueprints.Where(item => item.Kind == BlueprintKind.PlayerSpawn)
                .GroupBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.CurrentCultureIgnoreCase);
            return project.Blueprints.Where(item => item.Kind == BlueprintKind.PlayerSpawn)
                .Select(item => new StudioUx2NavBBlueprintChoice(item.Id,
                    duplicates[item.DisplayName] > 1
                        ? $"{HumanUx2NavB(item.DisplayName, "Point d’apparition")} · {HumanUx2NavB(item.Slug, "profil")}"
                        : HumanUx2NavB(item.DisplayName, "Point d’apparition")))
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal IReadOnlyList<StudioUx2NavBDestinationChoice> NavigationDestinationChoicesUx2NavB(
        StudioUx2NavBPortalDraft? protectedPortal = null)
    {
        var project = Project;
        if (project is null)
        {
            return [];
        }
        var choices = NavigationDestinationCatalog.Build(project).Entries
            .Where(item => item.Kind is NavigationDestinationKind.Waypoint or NavigationDestinationKind.SpawnAnchor &&
                           item.AreaId is not null)
            .Select(item => new StudioUx2NavBDestinationChoice(item.Kind, item.Id, item.AreaId, item.HumanLabel,
                item.ArrivalAnchorKey))
            .ToList();
        if (protectedPortal is not null && choices.All(item => item.Id != protectedPortal.TargetAnchorInstanceId))
        {
            choices.Add(new StudioUx2NavBDestinationChoice(NavigationDestinationKind.Waypoint,
                protectedPortal.TargetAnchorInstanceId, protectedPortal.TargetAreaId,
                "Destination héritée protégée", null, true));
        }
        return choices.OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Id)
            .ToArray();
    }

    internal StudioUx2NavBDraft? CreateNavigationDraftUx2NavB(AreaId? requestedAreaId)
    {
        var project = Project;
        var history = History;
        if (project is null || history is null || requestedAreaId is null)
        {
            return null;
        }
        var area = project.Areas.SingleOrDefault(item => item.Id == requestedAreaId);
        return area is null ? null : CreateNavigationDraftUx2NavB(area, history.State.Revision);
    }

    private static StudioUx2NavBDraft CreateNavigationDraftUx2NavB(AreaDefinition area, ProjectRevision revision)
    {
        var navigation = area.Navigation;
        return new StudioUx2NavBDraft(
            area.Id,
            revision,
            NavigationSourceFingerprintUx2NavB(area),
            area.DisplayName,
            area.Slug,
            area.Dimensions.Width,
            area.Dimensions.Depth,
            navigation.CellSize,
            navigation.AgentProfiles.Select(item => new StudioUx2NavBAgentDraft(item.Id, item.SchemaVersion,
                item.Provenance, item.Name, item.Radius, item.Height, item.CanOpenDoors,
                item.CanUseOffMeshLinks, item.CanFly)).ToArray(),
            navigation.OffMeshLinks.Select(item => new StudioUx2NavBOffMeshDraft(item.Id, item.SchemaVersion,
                item.Provenance, item.Name, item.Kind, item.Start, item.End, item.Bidirectional,
                item.MaximumAgentRadius)).ToArray(),
            navigation.Anchors.Definitions.Select(item => new StudioUx2NavBAnchorDefinitionDraft(
                item.Id, item.SchemaVersion, item.Provenance, item.Slug, item.Label, item.Label.Fallback,
                item.Kind, item.Scope, item.Tags, item.BlueprintId, item.AssetId, item.RequiredAtRuntime)).ToArray(),
            navigation.Anchors.Instances.Select(item => new StudioUx2NavBAnchorInstanceDraft(
                item.Id, item.SchemaVersion, item.Provenance, item.DefinitionId, item.Transform,
                item.Transform.Position, item.DisplayOrder, item.Overrides,
                item.Spawn is null ? null : new StudioUx2NavBSpawnDraft(item.Spawn.Profile,
                    item.Spawn.Clearance, item.Spawn.Priority, item.Spawn.Team,
                    item.Spawn.OrientationDegrees, item.Spawn.FallbackInstanceId,
                    item.Spawn.RequireNavigation),
                item.Portal is null ? null : new StudioUx2NavBPortalDraft(item.Portal.TargetAreaId,
                    item.Portal.TargetAnchorInstanceId, item.Portal.Orientation,
                    item.Portal.FixedYawDegrees, item.Portal.Velocity, item.Portal.Preload))).ToArray(),
            null,
            null,
            null,
            null);
    }

    internal StudioUx2NavBPreview PreviewNavigationUx2NavB(StudioUx2NavBDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelNavigationUx2NavB();
        var history = History;
        if (history is null || Project is null)
        {
            return FailedPreviewUx2NavB("Aucun module n’est ouvert.");
        }
        if (!TryBuildNavigationUx2NavB(draft, true, out var command, out var sourceArea, out var candidateArea,
                out var candidateProject, out var diagnostics, out var message))
        {
            return FailedPreviewUx2NavB(message, diagnostics);
        }

        var dryRun = _setNavigationTopologyUx2NavB.Handle(history.State, command!);
        if (!dryRun.Succeeded)
        {
            var commandDiagnostics = dryRun.Diagnostics.Select(item => new StudioUx2NavBDiagnostic(item.Code,
                $"areas/{draft.AreaId}/navigation", HumanizeDiagnosticUx2NavB(item.Message))).ToArray();
            return FailedPreviewUx2NavB(commandDiagnostics.FirstOrDefault()?.Message ??
                                     "L’aperçu de navigation a été refusé.", commandDiagnostics);
        }

        try
        {
            var previewArea = candidateArea!;
            var previewProject = candidateProject!;
            var originalArea = sourceArea!;
            var currentProject = Project!;
            var selectedAgent = previewArea.Navigation.AgentProfiles.FirstOrDefault(item =>
                                    item.Id == draft.SelectedAgentId) ??
                                previewArea.Navigation.AgentProfiles.First();
            var surface = NavigationSurfaceBuilder.Build(previewProject, previewArea, selectedAgent);
            var previousAgent = originalArea.Navigation.AgentProfiles.FirstOrDefault(item =>
                item.Id == selectedAgent.Id);
            var previousSurface = previousAgent is null
                ? null
                : NavigationSurfaceBuilder.Build(currentProject, originalArea, previousAgent);
            var bake = NavigationBakePlanner.Plan(previousSurface, surface);
            var overlay = BuildNavigationOverlayUx2NavB(previewArea, surface, draft.SelectedInstanceId,
                out var displayedCells);
            var diff = BuildNavigationDiffUx2NavB(originalArea, previewArea);
            if (diff.Count > 0)
            {
                _approvedNavigationUx2NavB = draft;
                _approvedNavigationRevisionUx2NavB = history.State.Revision;
                _approvedNavigationFingerprintUx2NavB = NavigationDraftFingerprintUx2NavB(draft);
            }
            var rebuild = bake.FullRebuild
                ? $"reconstruction complète · {bake.ChangedCells.Count} cellules"
                : $"mise à jour partielle · {bake.ChangedCells.Count} modifiées · {bake.ReusedCellCount} réutilisées";
            var sampling = displayedCells == surface.WalkableCells.Count
                ? $"{displayedCells} cellules affichées"
                : $"{displayedCells}/{surface.WalkableCells.Count} cellules affichées (échantillon déterministe)";
            return new StudioUx2NavBPreview(true,
                diff.Count == 0
                    ? "Aperçu validé · aucune différence sémantique à appliquer."
                    : "Aperçu validé · aucun document ni nœud moteur n’a été modifié.",
                $"Surface « {HumanUx2NavB(selectedAgent.Name, "Agent")} » · {surface.WalkableCells.Count} cellules marchables · " +
                $"{surface.OffMeshEdges.Count} liaison(s) résolue(s) · {rebuild} · {sampling}",
                diff.Count > 0, diff, [], overlay, surface.WalkableCells.Count, displayedCells,
                bake.ChangedCells.Count, bake.ReusedCellCount, bake.FullRebuild);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           OverflowException)
        {
            return FailedPreviewUx2NavB($"La surface ne peut pas être calculée : {HumanizeDiagnosticUx2NavB(exception.Message)}",
                [new("studio.navigation.surface_invalid", $"areas/{draft.AreaId}/navigation/surface",
                    HumanizeDiagnosticUx2NavB(exception.Message))]);
        }
    }

    internal StudioUx2NavBActionResult ApplyApprovedNavigationUx2NavB()
    {
        var draft = _approvedNavigationUx2NavB;
        var history = History;
        if (draft is null || history is null || _approvedNavigationRevisionUx2NavB != history.State.Revision ||
            !string.Equals(_approvedNavigationFingerprintUx2NavB, NavigationDraftFingerprintUx2NavB(draft),
                StringComparison.Ordinal))
        {
            CancelNavigationUx2NavB();
            return new(false, "Prévisualisez de nouveau la navigation avant de l’appliquer.");
        }
        if (!TryBuildNavigationUx2NavB(draft, false, out var command, out _, out _, out _, out _, out var message))
        {
            CancelNavigationUx2NavB();
            return new(false, message);
        }
        var preview = _setNavigationTopologyUx2NavB.Handle(history.State, command! with { DryRun = true });
        if (!preview.Succeeded)
        {
            CancelNavigationUx2NavB();
            return new(false, HumanizeDiagnosticUx2NavB(preview.Diagnostics.FirstOrDefault()?.Message));
        }
        var applied = _setNavigationTopologyUx2NavB.Handle(history.State, command!);
        CancelNavigationUx2NavB();
        if (!applied.Succeeded)
        {
            return new(false, HumanizeDiagnosticUx2NavB(applied.Diagnostics.FirstOrDefault()?.Message));
        }
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, "Navigation appliquée · Ctrl+Z pour annuler, Ctrl+Y pour rétablir");
    }

    internal void CancelNavigationUx2NavB()
    {
        _approvedNavigationUx2NavB = null;
        _approvedNavigationRevisionUx2NavB = null;
        _approvedNavigationFingerprintUx2NavB = null;
    }

    internal void CloseUx2NavB() => CancelNavigationUx2NavB();

    internal StudioUx2NavBApprovalSnapshot CaptureNavigationApprovalUx2NavB() => new(
        _approvedNavigationUx2NavB, _approvedNavigationRevisionUx2NavB, _approvedNavigationFingerprintUx2NavB);

    internal void RestoreNavigationApprovalUx2NavB(StudioUx2NavBApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedNavigationUx2NavB = snapshot.Draft;
        _approvedNavigationRevisionUx2NavB = snapshot.Revision;
        _approvedNavigationFingerprintUx2NavB = snapshot.Fingerprint;
    }

    private bool TryBuildNavigationUx2NavB(
        StudioUx2NavBDraft draft,
        bool dryRun,
        out SetNavigationTopologyCommand? command,
        out AreaDefinition? sourceArea,
        out AreaDefinition? candidateArea,
        out ProjectDefinition? candidateProject,
        out IReadOnlyList<StudioUx2NavBDiagnostic> diagnostics,
        out string message)
    {
        command = null;
        sourceArea = null;
        candidateArea = null;
        candidateProject = null;
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
            message = "Le projet a changé depuis l’ouverture de cette fiche. Rechargez la zone.";
            return false;
        }
        sourceArea = project.Areas.SingleOrDefault(item => item.Id == draft.AreaId);
        if (sourceArea is null)
        {
            message = "La zone choisie n’existe plus.";
            return false;
        }
        if (!string.Equals(draft.SourceFingerprint, NavigationSourceFingerprintUx2NavB(sourceArea),
                StringComparison.Ordinal))
        {
            message = "La navigation de cette zone a changé. Rechargez la fiche avant de prévisualiser.";
            return false;
        }

        var inputDiagnostics = ValidateNavigationDraftUx2NavB(draft);
        if (inputDiagnostics.Count > 0)
        {
            diagnostics = inputDiagnostics;
            message = inputDiagnostics[0].Message;
            return false;
        }

        try
        {
            var navigation = sourceArea.Navigation;
            var originalAgents = navigation.AgentProfiles.ToDictionary(item => item.Id);
            var agents = draft.Agents.Select(item =>
            {
                if (originalAgents.TryGetValue(item.Id, out var original) && SameAgentUx2NavB(item, original))
                {
                    return original;
                }
                return new NavigationAgentProfile(item.Id, item.Name, item.Radius, item.Height,
                    item.CanOpenDoors, item.CanUseOffMeshLinks, item.CanFly, Ux2NavBProvenance,
                    item.SchemaVersion);
            }).ToArray();
            var originalLinks = navigation.OffMeshLinks.ToDictionary(item => item.Id);
            var links = draft.OffMeshLinks.Select(item =>
            {
                if (originalLinks.TryGetValue(item.Id, out var original) && SameOffMeshUx2NavB(item, original))
                {
                    return original;
                }
                return new OffMeshNavigationLink(item.Id, item.Name, item.Kind, item.Start, item.End,
                    item.Bidirectional, item.MaximumAgentRadius, Ux2NavBProvenance, item.SchemaVersion);
            }).ToArray();
            var originalDefinitions = navigation.Anchors.Definitions.ToDictionary(item => item.Id);
            var definitions = draft.Definitions.Select(item =>
            {
                if (originalDefinitions.TryGetValue(item.Id, out var original) &&
                    SameDefinitionUx2NavB(item, original))
                {
                    return original;
                }
                var label = string.Equals(item.Label, item.OriginalLabel.Fallback, StringComparison.Ordinal)
                    ? item.OriginalLabel
                    : new LocalizedText(item.OriginalLabel.Id, item.OriginalLabel.SchemaVersion,
                        item.OriginalLabel.Provenance, item.OriginalLabel.Key, item.Label,
                        item.OriginalLabel.Variants);
                return new AnchorDefinition(item.Id, item.SchemaVersion, Ux2NavBProvenance, item.Slug, label,
                    item.Kind, item.Scope, item.Tags, item.BlueprintId, item.AssetId, item.RequiredAtRuntime);
            }).ToArray();
            var originalInstances = navigation.Anchors.Instances.ToDictionary(item => item.Id);
            var instances = draft.Instances.Select(item =>
            {
                if (originalInstances.TryGetValue(item.Id, out var original) && SameInstanceUx2NavB(item, original))
                {
                    return original;
                }
                var transform = item.Position == item.OriginalTransform.Position
                    ? item.OriginalTransform
                    : new TransformValue(item.Position, item.OriginalTransform.Rotation,
                        item.OriginalTransform.Scale);
                var spawn = item.Spawn is null ? null : new SpawnAnchorProfile(item.Spawn.Profile,
                    item.Spawn.Clearance, item.Spawn.Priority, item.Spawn.Team,
                    item.Spawn.OrientationDegrees, item.Spawn.FallbackInstanceId,
                    item.Spawn.RequireNavigation);
                var portal = item.Portal is null ? null : new PortalAnchorDestination(item.Portal.TargetAreaId,
                    item.Portal.TargetAnchorInstanceId, item.Portal.Orientation,
                    item.Portal.FixedYawDegrees, item.Portal.Velocity, item.Portal.Preload);
                return new AnchorInstance(item.Id, item.SchemaVersion, Ux2NavBProvenance, item.DefinitionId,
                    transform, item.DisplayOrder, item.Overrides, spawn, portal);
            }).ToArray();
            var anchorsChanged = !definitions.SequenceEqual(navigation.Anchors.Definitions) ||
                                 !instances.SequenceEqual(navigation.Anchors.Instances);
            var anchors = new AnchorCatalog(navigation.Anchors.Id, navigation.Anchors.SchemaVersion,
                anchorsChanged ? Ux2NavBProvenance : navigation.Anchors.Provenance, sourceArea.Id,
                navigation.Anchors.RequiredAtRuntime, definitions, instances);
            var changed = draft.CellSize != navigation.CellSize ||
                          !agents.SequenceEqual(navigation.AgentProfiles) ||
                          !links.SequenceEqual(navigation.OffMeshLinks) || anchorsChanged;
            var candidateNavigation = new AreaNavigationDefinition(navigation.Id, draft.CellSize, agents, links,
                changed ? Ux2NavBProvenance : navigation.Provenance, navigation.SchemaVersion, anchors,
                navigation.Waypoints);
            candidateArea = sourceArea.WithNavigation(candidateNavigation);
            candidateProject = new ProjectDefinition(project.Id, project.Title, project.Module,
                project.Blueprints, project.Areas.Select(item => item.Id == sourceArea.Id ? candidateArea : item)
                    .ToArray(), project.Dialogues, project.Quests);
            command = new SetNavigationTopologyCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                [new AreaNavigationTopologyUpdate(sourceArea.Id, candidateNavigation)], [], null, dryRun);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           OverflowException)
        {
            var human = HumanizeDiagnosticUx2NavB(exception.Message);
            diagnostics = [new("studio.navigation.invalid", $"areas/{draft.AreaId}/navigation", human)];
            message = human;
            return false;
        }
    }

    private static IReadOnlyList<StudioUx2NavBDiagnostic> ValidateNavigationDraftUx2NavB(StudioUx2NavBDraft draft)
    {
        var result = new List<StudioUx2NavBDiagnostic>();
        var root = $"areas/{draft.AreaId}/navigation";
        if (!float.IsFinite(draft.CellSize) || draft.CellSize is < .25f or > 4f)
            result.Add(new("studio.navigation.cell_size", $"{root}/cellSize",
                "Le pas de navigation doit rester compris entre 0,25 m et 4 m."));
        if (draft.Agents.Count == 0)
            result.Add(new("studio.navigation.agent_required", $"{root}/agentProfiles",
                "Ajoutez au moins un profil d’agent."));
        if (draft.Agents.GroupBy(item => item.Id).Any(group => group.Count() > 1) ||
            draft.Agents.GroupBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .Any(group => group.Count() > 1))
            result.Add(new("studio.navigation.agent_duplicate", $"{root}/agentProfiles",
                "Les profils d’agent doivent avoir des noms distincts."));
        foreach (var agent in draft.Agents)
        {
            if (string.IsNullOrWhiteSpace(agent.Name) || !float.IsFinite(agent.Radius) ||
                agent.Radius is < .1f or > 8f || !float.IsFinite(agent.Height) ||
                agent.Height is < .2f or > 16f)
                result.Add(new("studio.navigation.agent_bounds", $"{root}/agentProfiles/{agent.Id}",
                    "Le nom, le rayon ou la hauteur du profil d’agent sort des limites éditables."));
        }
        if (draft.OffMeshLinks.GroupBy(item => item.Id).Any(group => group.Count() > 1))
            result.Add(new("studio.navigation.link_duplicate", $"{root}/offMeshLinks",
                "Une liaison hors surface est présente plusieurs fois."));
        foreach (var link in draft.OffMeshLinks)
        {
            if (string.IsNullOrWhiteSpace(link.Name) || link.Start == link.End ||
                !float.IsFinite(link.MaximumAgentRadius) || link.MaximumAgentRadius is < .1f or > 8f)
                result.Add(new("studio.navigation.link_invalid", $"{root}/offMeshLinks/{link.Id}",
                    "La liaison hors surface exige un nom, deux points distincts et un rayon valide."));
        }
        if (draft.Definitions.GroupBy(item => item.Id).Any(group => group.Count() > 1) ||
            draft.Definitions.GroupBy(item => item.Slug, StringComparer.Ordinal).Any(group => group.Count() > 1))
            result.Add(new("studio.navigation.anchor_definition_duplicate", $"{root}/anchors/definitions",
                "Les ancres doivent avoir des slugs distincts."));
        if (draft.Instances.GroupBy(item => item.Id).Any(group => group.Count() > 1) ||
            draft.Instances.GroupBy(item => item.DisplayOrder).Any(group => group.Count() > 1))
            result.Add(new("studio.navigation.anchor_instance_duplicate", $"{root}/anchors/instances",
                "Les instances d’ancre doivent avoir un ordre distinct."));
        var definitions = draft.Definitions.GroupBy(item => item.Id).ToDictionary(group => group.Key,
            group => group.First());
        var instanceIds = draft.Instances.Select(item => item.Id).ToHashSet();
        foreach (var definition in draft.Definitions)
        {
            if (string.IsNullOrWhiteSpace(definition.Slug) || string.IsNullOrWhiteSpace(definition.Label))
                result.Add(new("studio.navigation.anchor_name", $"{root}/anchors/definitions/{definition.Id}",
                    "Chaque ancre exige un nom et un slug lisibles."));
            if (definition.Kind == AnchorKind.Spawn && definition.BlueprintId is null)
                result.Add(new("studio.navigation.spawn_blueprint", $"{root}/anchors/definitions/{definition.Id}",
                    "Une ancre d’apparition exige un blueprint de départ humainement sélectionné."));
        }
        foreach (var instance in draft.Instances)
        {
            if (!definitions.TryGetValue(instance.DefinitionId, out var definition))
            {
                result.Add(new("studio.navigation.anchor_definition_missing", $"{root}/anchors/instances/{instance.Id}",
                    "L’instance pointe vers une définition d’ancre absente."));
                continue;
            }
            if (Math.Abs(instance.Position.X) > draft.AreaWidth / 2d ||
                Math.Abs(instance.Position.Z) > draft.AreaDepth / 2d ||
                Math.Abs(instance.Position.Y) > 4096)
                result.Add(new("studio.navigation.anchor_bounds", $"{root}/anchors/instances/{instance.Id}/position",
                    "L’ancre est placée hors des limites de la zone."));
            if (definition.Kind == AnchorKind.Spawn && (instance.Spawn is null ||
                string.IsNullOrWhiteSpace(instance.Spawn.Profile) || string.IsNullOrWhiteSpace(instance.Spawn.Team) ||
                instance.Spawn.Priority is < 1 or > 100 || instance.Spawn.Clearance.X <= 0 ||
                instance.Spawn.Clearance.Y <= 0 || instance.Spawn.Clearance.Z <= 0 ||
                instance.Spawn.FallbackInstanceId is { } fallback && !instanceIds.Contains(fallback)))
                result.Add(new("studio.navigation.spawn_invalid", $"{root}/anchors/instances/{instance.Id}/spawn",
                    "Le profil d’apparition, son dégagement, sa priorité ou son repli sont invalides."));
            if (definition.Kind == AnchorKind.TransitionDestination && instance.Portal is null)
                result.Add(new("studio.navigation.destination_required", $"{root}/anchors/instances/{instance.Id}/portal",
                    "Une ancre de transition exige une destination humaine explicite."));
        }
        return result;
    }

    private static StudioNavigationOverlaySnapshot BuildNavigationOverlayUx2NavB(
        AreaDefinition area,
        NavigationSurface surface,
        Guid? selectedInstanceId,
        out int displayedCells)
    {
        var polygons = new List<StudioNavigationOverlayPolygon>();
        var stride = Math.Max(1, (int)Math.Ceiling(surface.WalkableCells.Count /
                                                     (double)Ux2NavBMaximumDisplayedCells));
        foreach (var cell in surface.WalkableCells.Where((_, index) => index % stride == 0)
                     .Take(Ux2NavBMaximumDisplayedCells))
        {
            var center = surface.CenterOf(cell);
            var half = surface.CellSize / 2d;
            polygons.Add(new($"surface:{cell.X}:{cell.Z}", "Cellule marchable",
                StudioNavigationOverlaySemantic.Surface,
                [new(center.X - half, center.Y, center.Z - half), new(center.X + half, center.Y, center.Z - half),
                 new(center.X + half, center.Y, center.Z + half), new(center.X - half, center.Y, center.Z + half)]));
        }
        displayedCells = polygons.Count;
        var segments = area.Navigation.OffMeshLinks.Select(item => new StudioNavigationOverlaySegment(
            $"offmesh:{item.Id}", $"Liaison · {HumanUx2NavB(item.Name, "Sans nom")}",
            StudioNavigationOverlaySemantic.OffMeshLink,
            new(item.Start.X, item.Start.Y + .08, item.Start.Z),
            new(item.End.X, item.End.Y + .08, item.End.Z), item.Bidirectional)).ToArray();
        var definitions = area.Navigation.Anchors.Definitions.GroupBy(item => item.Id)
            .ToDictionary(group => group.Key, group => group.First());
        var markers = area.Navigation.Anchors.Instances.Where(item => definitions.ContainsKey(item.DefinitionId))
            .Select(item =>
            {
                var definition = definitions[item.DefinitionId];
                return new StudioNavigationOverlayMarker($"anchor:{item.Id}",
                    $"{HumanAnchorKindUx2NavB(definition.Kind)} · {HumanUx2NavB(definition.Label.Fallback, "Sans nom")}",
                    StudioNavigationOverlaySemantic.Anchor,
                    new(item.Transform.Position.X, item.Transform.Position.Y + .16, item.Transform.Position.Z),
                    item.Id == selectedInstanceId);
            }).ToArray();
        return new StudioNavigationOverlaySnapshot(markers, segments, polygons);
    }

    private static IReadOnlyList<string> BuildNavigationDiffUx2NavB(AreaDefinition source, AreaDefinition candidate)
    {
        var before = source.Navigation;
        var after = candidate.Navigation;
        var result = new List<string>();
        if (before.CellSize != after.CellSize)
            result.Add($"Pas de navigation · {before.CellSize:0.##} m → {after.CellSize:0.##} m");
        if (!before.AgentProfiles.SequenceEqual(after.AgentProfiles))
            result.Add(before.AgentProfiles.Count == after.AgentProfiles.Count
                ? $"Profils d’agent · réglages modifiés · {after.AgentProfiles.Count} présents"
                : $"Profils d’agent · {before.AgentProfiles.Count} → {after.AgentProfiles.Count}");
        if (!before.OffMeshLinks.SequenceEqual(after.OffMeshLinks))
            result.Add(before.OffMeshLinks.Count == after.OffMeshLinks.Count
                ? $"Liaisons hors surface · tracé ou capacité modifié · {after.OffMeshLinks.Count} présente(s)"
                : $"Liaisons hors surface · {before.OffMeshLinks.Count} → {after.OffMeshLinks.Count}");
        if (!before.Anchors.Definitions.SequenceEqual(after.Anchors.Definitions))
            result.Add(before.Anchors.Definitions.Count == after.Anchors.Definitions.Count
                ? $"Ancres nommées · nom, rôle ou portée modifié · {after.Anchors.Definitions.Count} présente(s)"
                : $"Ancres nommées · {before.Anchors.Definitions.Count} → {after.Anchors.Definitions.Count}");
        if (!before.Anchors.Instances.SequenceEqual(after.Anchors.Instances))
            result.Add(before.Anchors.Instances.Count == after.Anchors.Instances.Count
                ? $"Placements d’ancre · position ou profil modifié · {after.Anchors.Instances.Count} présent(s)"
                : $"Placements d’ancre · {before.Anchors.Instances.Count} → {after.Anchors.Instances.Count}");
        return result;
    }

    private static IReadOnlyList<StudioUx2NavBAreaChoice> BuildNavigationAreaChoicesUx2NavB(ProjectDefinition project)
    {
        var duplicates = project.Areas.GroupBy(item => item.DisplayName,
                StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(),
                StringComparer.CurrentCultureIgnoreCase);
        return project.Areas.Select(area =>
            {
                var label = HumanUx2NavB(area.DisplayName, "Zone sans nom lisible");
                if (duplicates[area.DisplayName] > 1)
                    label += $" · {HumanUx2NavB(area.Slug, "zone")}";
                return new StudioUx2NavBAreaChoice(area.Id,
                    $"{label} · {area.Navigation.AgentProfiles.Count} agent(s) · " +
                    $"{area.Navigation.OffMeshLinks.Count} liaison(s) · " +
                    $"{area.Navigation.Anchors.Instances.Count} ancre(s)",
                    string.Equals(area.Navigation.Provenance, Ux2NavBEvidenceProvenance, StringComparison.Ordinal));
            })
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static bool SameAgentUx2NavB(StudioUx2NavBAgentDraft draft, NavigationAgentProfile source) =>
        draft.SchemaVersion == source.SchemaVersion && draft.Provenance == source.Provenance &&
        draft.Name == source.Name && draft.Radius == source.Radius && draft.Height == source.Height &&
        draft.CanOpenDoors == source.CanOpenDoors && draft.CanUseOffMeshLinks == source.CanUseOffMeshLinks &&
        draft.CanFly == source.CanFly;

    private static bool SameOffMeshUx2NavB(StudioUx2NavBOffMeshDraft draft, OffMeshNavigationLink source) =>
        draft.SchemaVersion == source.SchemaVersion && draft.Provenance == source.Provenance &&
        draft.Name == source.Name && draft.Kind == source.Kind && draft.Start == source.Start &&
        draft.End == source.End && draft.Bidirectional == source.Bidirectional &&
        draft.MaximumAgentRadius == source.MaximumAgentRadius;

    private static bool SameDefinitionUx2NavB(StudioUx2NavBAnchorDefinitionDraft draft, AnchorDefinition source) =>
        draft.SchemaVersion == source.SchemaVersion && draft.Provenance == source.Provenance &&
        draft.Slug == source.Slug && draft.OriginalLabel == source.Label && draft.Label == source.Label.Fallback &&
        draft.Kind == source.Kind && draft.Scope == source.Scope && draft.Tags.SequenceEqual(source.Tags) &&
        draft.BlueprintId == source.BlueprintId && draft.AssetId == source.AssetId &&
        draft.RequiredAtRuntime == source.RequiredAtRuntime;

    private static bool SameInstanceUx2NavB(StudioUx2NavBAnchorInstanceDraft draft, AnchorInstance source) =>
        draft.SchemaVersion == source.SchemaVersion && draft.Provenance == source.Provenance &&
        draft.DefinitionId == source.DefinitionId && draft.OriginalTransform == source.Transform &&
        draft.Position == source.Transform.Position && draft.DisplayOrder == source.DisplayOrder &&
        draft.Overrides == source.Overrides && SameSpawnUx2NavB(draft.Spawn, source.Spawn) &&
        SamePortalUx2NavB(draft.Portal, source.Portal);

    private static bool SameSpawnUx2NavB(StudioUx2NavBSpawnDraft? draft, SpawnAnchorProfile? source) =>
        draft is null ? source is null : source is not null && draft.Profile == source.Profile &&
            draft.Clearance == source.Clearance && draft.Priority == source.Priority && draft.Team == source.Team &&
            draft.OrientationDegrees == source.OrientationDegrees &&
            draft.FallbackInstanceId == source.FallbackInstanceId &&
            draft.RequireNavigation == source.RequireNavigation;

    private static bool SamePortalUx2NavB(StudioUx2NavBPortalDraft? draft, PortalAnchorDestination? source) =>
        draft is null ? source is null : source is not null && draft.TargetAreaId == source.TargetAreaId &&
            draft.TargetAnchorInstanceId == source.TargetAnchorInstanceId && draft.Orientation == source.Orientation &&
            draft.FixedYawDegrees == source.FixedYawDegrees && draft.Velocity == source.Velocity &&
            draft.Preload == source.Preload;

    private static StudioUx2NavBPreview FailedPreviewUx2NavB(string message,
        IReadOnlyList<StudioUx2NavBDiagnostic>? diagnostics = null) => new(false, message, string.Empty, false, [],
        diagnostics ?? [], StudioNavigationOverlaySnapshot.Empty, 0, 0, 0, 0, false);

    private static string NavigationSourceFingerprintUx2NavB(AreaDefinition area) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(AreaJsonSerializer.Serialize(area))));

    private static string NavigationDraftFingerprintUx2NavB(StudioUx2NavBDraft draft) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(draft))));

    private static string HumanizeDiagnosticUx2NavB(string? message)
    {
        var value = string.IsNullOrWhiteSpace(message) ? "La navigation a été refusée." : message.Trim();
        return System.Text.RegularExpressions.Regex.Replace(value,
            @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b",
            "référence interne", System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                                  System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    private static string HumanUx2NavB(string? value, string fallback)
    {
        var text = value?.Trim();
        return string.IsNullOrWhiteSpace(text) || Guid.TryParse(text, out _) ? fallback : text;
    }

    private static string HumanAnchorKindUx2NavB(AnchorKind kind) => kind switch
    {
        AnchorKind.Spawn => "Apparition",
        AnchorKind.TransitionDestination => "Destination",
        _ => "Waypoint",
    };

    internal ProjectDefinition? CreateUx2NavBEvidenceProject()
    {
        var source = Project;
        return source is null ? null : CreateUx2NavBEvidenceProject(source.Id.Value);
    }

    private static ProjectDefinition CreateUx2NavBEvidenceProject(Guid projectSeed)
    {
        var sourceId = AreaId.From(Guid.Parse("2b000000-0000-4000-8000-000000000001"));
        var targetId = AreaId.From(Guid.Parse("2b000000-0000-4000-8000-000000000002"));
        var spawnBlueprintId = BlueprintId.From(Guid.Parse("2b000000-0000-4000-8000-000000000003"));
        var spawnBlueprint = new BlueprintDefinition(spawnBlueprintId, "depart-aventurier",
            "Départ aventurier", BlueprintKind.PlayerSpawn);
        var sourceSpawnDefinition = EvidenceDefinitionUx2NavB("2b000000-0000-4000-8000-000000000010",
            "clairiere-depart", "Départ de la clairière", AnchorKind.Spawn, spawnBlueprintId);
        var transitionDefinition = EvidenceDefinitionUx2NavB("2b000000-0000-4000-8000-000000000011",
            "passage-lisiere", "Passage vers la lisière", AnchorKind.TransitionDestination, null);
        var targetSpawnDefinition = EvidenceDefinitionUx2NavB("2b000000-0000-4000-8000-000000000012",
            "lisiere-arrivee", "Arrivée à la lisière", AnchorKind.Spawn, spawnBlueprintId);
        var sourceSpawnId = Guid.Parse("2b000000-0000-4000-8000-000000000020");
        var transitionId = Guid.Parse("2b000000-0000-4000-8000-000000000021");
        var targetSpawnId = Guid.Parse("2b000000-0000-4000-8000-000000000022");
        var targetAnchors = new AnchorCatalog(Guid.Parse("2b000000-0000-4000-8000-000000000030"), 1,
            Ux2NavBEvidenceProvenance, targetId, true, [targetSpawnDefinition],
            [new(targetSpawnId, 1, Ux2NavBEvidenceProvenance, targetSpawnDefinition.Id,
                new TransformValue(new(2, 0, 1), QuaternionValue.Identity, Vector3Value.One), 1,
                EntityPropertyOverrides.Empty,
                new("arrivee", new(1.2f, 2, 1.2f), 80, "heroes", 90, null, true), null)]);
        var sourceAnchors = new AnchorCatalog(Guid.Parse("2b000000-0000-4000-8000-000000000031"), 1,
            Ux2NavBEvidenceProvenance, sourceId, true, [sourceSpawnDefinition, transitionDefinition],
            [new(sourceSpawnId, 1, Ux2NavBEvidenceProvenance, sourceSpawnDefinition.Id,
                 new TransformValue(new(-4, 0, -2), QuaternionValue.Identity, Vector3Value.One), 1,
                 EntityPropertyOverrides.Empty,
                 new("depart", new(1.2f, 2, 1.2f), 100, "heroes", 0, null, true), null),
             new(transitionId, 1, Ux2NavBEvidenceProvenance, transitionDefinition.Id,
                 new TransformValue(new(8, 0, 6), QuaternionValue.Identity, Vector3Value.One), 2,
                 EntityPropertyOverrides.Empty, null,
                 new(targetId, targetSpawnId, PortalOrientationPolicy.AnchorRotation, 0,
                     PortalVelocityPolicy.Reset, true))]);
        var sourceNavigation = AreaNavigationDefinition.CreateDefault(sourceId);
        sourceNavigation = new AreaNavigationDefinition(sourceNavigation.Id, .75f,
            sourceNavigation.AgentProfiles,
            [new(OffMeshNavigationLinkId.From(Guid.Parse("2b000000-0000-4000-8000-000000000040")),
                "Saut du ruisseau", NavigationLinkKind.Jump, new(-2, 0, 0), new(2, 0, 0), true, .6f,
                Ux2NavBEvidenceProvenance)],
            Ux2NavBEvidenceProvenance, anchors: sourceAnchors, waypoints: sourceNavigation.Waypoints);
        var targetNavigation = AreaNavigationDefinition.CreateDefault(targetId).WithAnchors(targetAnchors);
        var sourceArea = new AreaDefinition(sourceId, "clairiere-navigation", "Clairière des passages", [],
            AreaKind.Exterior, new AreaDimensions(32, 32), navigation: sourceNavigation);
        var targetArea = new AreaDefinition(targetId, "lisiere-navigation", "Lisière des passages", [],
            AreaKind.Exterior, new AreaDimensions(24, 24), navigation: targetNavigation);
        return new ProjectDefinition(ProjectId.From(projectSeed == Guid.Empty
                ? Guid.Parse("2b000000-0000-4000-8000-000000000050")
                : projectSeed), "Preuve navigation", [spawnBlueprint], [sourceArea, targetArea]);
    }

    private static AnchorDefinition EvidenceDefinitionUx2NavB(string id, string slug, string label,
        AnchorKind kind, BlueprintId? blueprintId) => new(Guid.Parse(id), 1, Ux2NavBEvidenceProvenance, slug,
        new LocalizedText($"navigation.{slug.Replace('-', '.')}", label, provenance: Ux2NavBEvidenceProvenance),
        kind, AnchorScope.Area, ["navigation"], blueprintId, null, true);

    internal StudioUx2NavBSmokeResult RunUx2NavBContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-nav-b-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var fixture = CreateUx2NavBEvidenceProject(Guid.Parse("2b000000-0000-4000-8000-000000000060"));
            var created = ProjectSession.Create(tempRoot, fixture);
            var opened = created.Succeeded && editing.Open(tempRoot).Succeeded;
            if (!opened)
                return FailedUx2NavBSmoke();
            var choice = editing.NavigationAreaChoicesUx2NavB.Single(item => item.IsEvidence);
            var explicitSelection = editing.CreateNavigationDraftUx2NavB(null) is null;
            var original = editing.CreateNavigationDraftUx2NavB(choice.AreaId);
            explicitSelection &= original is not null && original.SelectedAgentId is null &&
                                 original.SelectedOffMeshId is null && original.SelectedDefinitionId is null &&
                                 original.SelectedInstanceId is null;
            if (original is null)
                return FailedUx2NavBSmoke();
            var humanDestinations = editing.NavigationDestinationChoicesUx2NavB(original.Instances
                    .First(item => item.Portal is not null).Portal)
                .All(item => !ContainsTechnicalIdentifierUx2NavB(item.Label));
            var invalidBounds = editing.PreviewNavigationUx2NavB(original with
            {
                Instances = original.Instances.Select((item, index) => index == 0
                    ? item with { Position = new Vector3Value(200, 0, 0) }
                    : item).ToArray(),
            });
            var invalidDestination = editing.PreviewNavigationUx2NavB(original with
            {
                Instances = original.Instances.Select(item => item.Portal is null ? item : item with
                {
                    Portal = item.Portal with { TargetAnchorInstanceId = Guid.NewGuid() },
                }).ToArray(),
            });
            var invalidBoundsBlocked = !invalidBounds.Succeeded && invalidBounds.Diagnostics.Any(item =>
                item.Code.Contains("bounds", StringComparison.Ordinal));
            var invalidDestinationBlocked = !invalidDestination.Succeeded &&
                                            invalidDestination.Diagnostics.Count > 0;

            StudioUx2NavBDraft Configure(StudioUx2NavBDraft draft) => draft with
            {
                Agents = draft.Agents.Select((item, index) => index == 0
                    ? item with { Radius = item.Radius + .05f }
                    : item).ToArray(),
                Instances = draft.Instances.Select((item, index) => index == 0
                    ? item with
                    {
                        Position = new Vector3Value(item.Position.X + 1, item.Position.Y, item.Position.Z),
                        Spawn = item.Spawn! with { Priority = 95 },
                    }
                    : item).ToArray(),
            };

            var configured = Configure(original);
            var historyBefore = editing.History!;
            var projectBefore = editing.Project;
            var revisionBefore = historyBefore.State.Revision;
            var journalBefore = historyBefore.Journal.ToArray();
            var preview = editing.PreviewNavigationUx2NavB(configured);
            var dryRun = preview.Succeeded && preview.CanApply && ReferenceEquals(projectBefore, editing.Project) &&
                         ReferenceEquals(historyBefore, editing.History) && historyBefore.State.Revision == revisionBefore &&
                         historyBefore.Journal.SequenceEqual(journalBefore);
            var surfaceBuilt = preview.WalkableCellCount > 0 && preview.DisplayedCellCount > 0 &&
                               preview.Overlay.Polygons.Count == preview.DisplayedCellCount;
            var partialBake = !preview.FullRebuild && preview.ReusedCellCount > 0;

            var mutableAgents = configured.Agents.ToArray();
            var mutableDraft = configured with { Agents = mutableAgents };
            var mutablePreview = editing.PreviewNavigationUx2NavB(mutableDraft);
            mutableAgents[0] = mutableAgents[0] with { Height = mutableAgents[0].Height + .05f };
            var staleFingerprint = mutablePreview.Succeeded && !editing.ApplyApprovedNavigationUx2NavB().Succeeded;

            var staleDraft = Configure(editing.CreateNavigationDraftUx2NavB(choice.AreaId)!);
            var neighbor = editing.Project!.Areas.Single(item => item.Id != choice.AreaId);
            var neighborMutation = ApplyUx2NavBNeighborMutation(editing, neighbor);
            var staleDraftPreview = editing.PreviewNavigationUx2NavB(staleDraft);
            var staleDraftBlocked = neighborMutation && !staleDraftPreview.Succeeded && editing.Undo().Succeeded;

            var staleApprovalDraft = Configure(editing.CreateNavigationDraftUx2NavB(choice.AreaId)!);
            var staleApprovalPreview = editing.PreviewNavigationUx2NavB(staleApprovalDraft);
            neighbor = editing.Project!.Areas.Single(item => item.Id != choice.AreaId);
            var staleApprovalMutation = ApplyUx2NavBNeighborMutation(editing, neighbor);
            var staleApprovalApply = editing.ApplyApprovedNavigationUx2NavB();
            var staleApprovalBlocked = staleApprovalPreview.Succeeded && staleApprovalMutation &&
                                       !staleApprovalApply.Succeeded && editing.Undo().Succeeded;

            var sourceProject = editing.Project!;
            var sourceArea = sourceProject.Areas.Single(item => item.Id == choice.AreaId);
            var sourceNeighbor = sourceProject.Areas.Single(item => item.Id != choice.AreaId);
            var fresh = Configure(editing.CreateNavigationDraftUx2NavB(choice.AreaId)!);
            var freshPreview = editing.PreviewNavigationUx2NavB(fresh);
            var applied = editing.ApplyApprovedNavigationUx2NavB();
            var appliedProject = editing.Project!;
            var appliedArea = appliedProject.Areas.Single(item => item.Id == choice.AreaId);
            var canonicalCommand = freshPreview.Succeeded && applied.Succeeded &&
                                   editing.History!.Journal.LastOrDefault()?.CommandName ==
                                   "set_navigation_topology";
            var navigationNeighborsPreserved =
                ReferenceEquals(sourceArea.Navigation.Waypoints, appliedArea.Navigation.Waypoints) &&
                ReferenceEquals(sourceArea.Environment, appliedArea.Environment) &&
                ReferenceEquals(sourceArea.Authoring, appliedArea.Authoring) &&
                ReferenceEquals(sourceArea.World, appliedArea.World) &&
                ReferenceEquals(sourceArea.RuntimePolicies, appliedArea.RuntimePolicies) &&
                ReferenceEquals(sourceArea.Foundation, appliedArea.Foundation) &&
                ReferenceEquals(sourceArea.Playtest, appliedArea.Playtest) &&
                sourceArea.Entities.SequenceEqual(appliedArea.Entities) &&
                sourceArea.Portals.SequenceEqual(appliedArea.Portals);
            var appliedNeighbor = appliedProject.Areas.Single(item => item.Id == sourceNeighbor.Id);
            var projectNeighborsPreserved = ReferenceEquals(sourceProject.Module, appliedProject.Module) &&
                                            ReferenceEquals(sourceNeighbor, appliedNeighbor) &&
                                            sourceProject.Blueprints.SequenceEqual(appliedProject.Blueprints) &&
                                            sourceProject.Dialogues.SequenceEqual(appliedProject.Dialogues) &&
                                            sourceProject.Quests.SequenceEqual(appliedProject.Quests);
            var appliedJson = AreaJsonSerializer.Serialize(appliedArea);
            var undo = editing.Undo();
            var undone = ReferenceEquals(sourceProject, editing.Project);
            var redo = editing.Redo();
            var redone = ReferenceEquals(appliedProject, editing.Project);
            var undoRedo = undo.Succeeded && undone && redo.Succeeded && redone;
            var save = editing.Save();
            var reopen = reopened.Open(tempRoot, choice.AreaId.ToString());
            var restoredArea = reopened.Project?.Areas.SingleOrDefault(item => item.Id == choice.AreaId);
            var persistence = save.Succeeded && reopen.Succeeded && restoredArea is not null &&
                              string.Equals(appliedJson, AreaJsonSerializer.Serialize(restoredArea),
                                  StringComparison.Ordinal);
            var humanText = !ContainsTechnicalIdentifierUx2NavB(choice.Label + " " + freshPreview.Summary + " " +
                            string.Join(' ', freshPreview.Diff));
            var sourceUntouched = true;
            var succeeded = explicitSelection && humanDestinations && surfaceBuilt && partialBake &&
                            invalidBoundsBlocked && invalidDestinationBlocked && dryRun && staleDraftBlocked &&
                            staleFingerprint && staleApprovalBlocked && canonicalCommand && undoRedo && persistence &&
                            navigationNeighborsPreserved && projectNeighborsPreserved && sourceUntouched && humanText;
            return new(succeeded, explicitSelection, humanDestinations, surfaceBuilt, partialBake,
                invalidBoundsBlocked, invalidDestinationBlocked, dryRun, staleDraftBlocked, staleFingerprint,
                staleApprovalBlocked, canonicalCommand, undoRedo, persistence, navigationNeighborsPreserved,
                projectNeighborsPreserved, sourceUntouched, humanText);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_NAV_B_CONTRACT_FAILED type={exception.GetType().Name} message={exception.Message}");
            return FailedUx2NavBSmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            DeleteUx2NavBTemporaryWorkspace(tempRoot);
        }
    }

    private static bool ApplyUx2NavBNeighborMutation(StudioSessionController controller, AreaDefinition area)
    {
        var history = controller.History;
        if (history is null)
            return false;
        var navigation = area.Navigation;
        var candidate = new AreaNavigationDefinition(navigation.Id,
            navigation.CellSize == 1f ? .9f : 1f, navigation.AgentProfiles, navigation.OffMeshLinks,
            "studio:ux2-nav-b:neighbor", navigation.SchemaVersion, navigation.Anchors, navigation.Waypoints);
        var result = controller._setNavigationTopologyUx2NavB.Handle(history.State,
            new SetNavigationTopologyCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                [new(area.Id, candidate)], [], null, false));
        if (!result.Succeeded)
            return false;
        history.Record(result);
        return true;
    }

    internal static bool DeleteUx2NavBTemporaryWorkspace(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return true;
        try
        {
            var root = Path.GetFullPath(directory);
            var temp = Path.GetFullPath(Path.GetTempPath());
            var prefix = temp.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                         Path.DirectorySeparatorChar;
            var leaf = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
            if (!root.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !leaf.StartsWith("openrpg-forge-studio-ux2-nav-b-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
            return !Directory.Exists(root);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool ContainsTechnicalIdentifierUx2NavB(string text) =>
        text.Contains("UUID", StringComparison.OrdinalIgnoreCase) ||
        System.Text.RegularExpressions.Regex.IsMatch(text,
            @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static StudioUx2NavBSmokeResult FailedUx2NavBSmoke() => new(false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false, false, false);
}
