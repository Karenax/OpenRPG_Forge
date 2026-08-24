using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Navigation;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.EngineAdapter.World;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux6VProvenance = "studio:ux6-v:advanced-pathfinding";
    private const string Ux6VEvidenceSlug = "studio-pathfinding-lab";

    private readonly SetNavigationTopologyHandler _setNavigationTopologyUx6V = new(new SystemClock());
    private StudioUx6VDraft? _approvedPathfindingUx6V;
    private ProjectRevision? _approvedPathfindingRevisionUx6V;
    private string? _approvedPathfindingFingerprintUx6V;

    internal IReadOnlyList<StudioUx6VAreaChoice> PathfindingAreaChoicesUx6V()
    {
        var project = Project;
        if (project is null) return [];
        var duplicateNames = project.Areas.GroupBy(item => item.DisplayName,
                StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(),
                StringComparer.CurrentCultureIgnoreCase);
        return project.Areas.Select(area => new StudioUx6VAreaChoice(area.Id,
                duplicateNames[area.DisplayName] > 1
                    ? $"{HumanTextUx6V(area.DisplayName, "Zone")} · {HumanTextUx6V(area.Slug, "zone")}"
                    : HumanTextUx6V(area.DisplayName, "Zone")))
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal IReadOnlyList<StudioUx6VAgentChoice> PathfindingAgentChoicesUx6V(AreaId? areaId)
    {
        var area = areaId is null ? null : Project?.Areas.FirstOrDefault(item => item.Id == areaId);
        return area is null ? [] : area.Navigation.AgentProfiles.Select(agent =>
                new StudioUx6VAgentChoice(agent.Id,
                    $"{HumanTextUx6V(agent.Name, "Agent")} · rayon {NumberUx6V(agent.Radius)} m · " +
                    $"hauteur {NumberUx6V(agent.Height)} m"))
            .ToArray();
    }

    internal StudioUx6VDraft? CreatePathfindingDraftUx6V(
        bool explicitlySelected,
        AreaId? areaId,
        NavigationAgentProfileId? agentId)
    {
        var project = Project;
        var history = History;
        if (!explicitlySelected || project is null || history is null || areaId is null || agentId is null)
            return null;
        var area = project.Areas.FirstOrDefault(item => item.Id == areaId);
        var agent = area?.Navigation.AgentProfiles.FirstOrDefault(item => item.Id == agentId);
        if (area is null || agent is null) return null;
        var width = (int)Math.Ceiling(area.Dimensions.Width / area.Navigation.CellSize);
        var depth = (int)Math.Ceiling(area.Dimensions.Depth / area.Navigation.CellSize);
        var start = new NavigationCell(Math.Clamp(1, 0, Math.Max(0, width - 1)),
            Math.Clamp(depth / 2, 0, Math.Max(0, depth - 1)));
        var goal = new NavigationCell(Math.Max(0, width - 2), start.Z);
        var middle = new NavigationCell(Math.Max(start.X + 1, width / 2), start.Z);
        var cost = new NavigationCell(Math.Max(start.X + 1, middle.X - 1), start.Z);
        return new StudioUx6VDraft(history.State.Revision, SourceFingerprintUx6V(project), area.Id, agent.Id,
            agent.Name, area.Navigation.CellSize, agent.Radius, agent.Height, agent.CanOpenDoors,
            agent.CanUseOffMeshLinks, agent.CanFly, NavigationDoorState.Current, start, goal,
            true, middle, true, cost, 12f, true, 4, 100_000);
    }

    internal StudioUx6VPreview PreviewPathfindingUx6V(StudioUx6VDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelPathfindingApprovalUx6V();
        if (!TryBuildPathfindingCandidateUx6V(draft, true, out var command, out var sourceArea,
                out var sourceAgent, out var candidateArea, out var candidateProject,
                out var candidateAgent, out var diagnostic))
            return FailedPathfindingPreviewUx6V(diagnostic!);

        var history = History!;
        var dryRun = _setNavigationTopologyUx6V.Handle(history.State, command!);
        if (!dryRun.Succeeded)
        {
            var failure = dryRun.Diagnostics.FirstOrDefault();
            return FailedPathfindingPreviewUx6V(new StudioUx6VDiagnostic(
                failure?.Code ?? "studio.pathfinding.command_refused",
                $"areas/{draft.AreaId}/navigation",
                HumanCommandFailureUx6V(failure?.Code)));
        }

        try
        {
            var previousSurface = NavigationSurfaceBuilder.Build(Project!, sourceArea!, sourceAgent!, draft.DoorState);
            var surface = NavigationSurfaceBuilder.Build(candidateProject!, candidateArea!, candidateAgent!,
                draft.DoorState);
            var cellDiagnostic = ValidatePathCellsUx6V(draft, surface);
            if (cellDiagnostic is not null) return FailedPathfindingPreviewUx6V(cellDiagnostic);

            var obstacles = draft.DynamicObstacleEnabled
                ? new HashSet<NavigationCell> { draft.DynamicObstacle }
                : new HashSet<NavigationCell>();
            var costs = draft.CostEnabled
                ? new Dictionary<NavigationCell, float> { [draft.CostCell] = draft.CostMultiplier }
                : new Dictionary<NavigationCell, float>();
            var query = new NavigationPathQuery(draft.Start, draft.Goal, obstacles, costs,
                draft.RecoverBlockedEndpoints, draft.RecoveryRadius, draft.MaximumVisitedCells);
            var path = NavigationPathfinder.FindPath(surface, query);

            var obstacleProbe = draft.DynamicObstacleEnabled
                ? NavigationPathfinder.FindPath(surface, new NavigationPathQuery(draft.Start, draft.Goal,
                    new HashSet<NavigationCell> { draft.DynamicObstacle }, maximumVisitedCells: 100_000))
                : path;
            var costProbe = draft.CostEnabled
                ? NavigationPathfinder.FindPath(surface, new NavigationPathQuery(draft.Start, draft.Goal,
                    costMultipliers: new Dictionary<NavigationCell, float>
                    {
                        [draft.CostCell] = draft.CostMultiplier,
                    }, maximumVisitedCells: 100_000))
                : path;
            var recoveryProbe = draft.RecoverBlockedEndpoints
                ? NavigationPathfinder.FindPath(surface, new NavigationPathQuery(draft.Start, draft.Goal,
                    new HashSet<NavigationCell> { draft.Start }, recoverBlockedEndpoints: true,
                    recoveryRadius: draft.RecoveryRadius, maximumVisitedCells: 100_000))
                : path;
            var budgetProbe = NavigationPathfinder.FindPath(surface,
                new NavigationPathQuery(draft.Start, draft.Goal, maximumVisitedCells: 1));
            var dynamicPlan = NavigationDynamicUpdatePlanner.Plan(
                surface,
                new HashSet<NavigationCell>(),
                obstacles,
                new Dictionary<NavigationCell, float>(),
                costs);
            var bake = NavigationBakePlanner.Plan(previousSurface, surface);
            var enginePlan = SurfaceNavigationPlanBuilder.Build(candidateArea!.World,
                candidateArea.Navigation, candidateAgent);

            var surfaceBuilt = surface.WalkableCells.Count > 0;
            var enginePlanBuilt = enginePlan.Enabled && enginePlan.Agent?.Id == candidateAgent!.Id &&
                                  enginePlan.Triangles.Count > 0;
            var obstacleAvoided = !draft.DynamicObstacleEnabled ||
                                  obstacleProbe.Succeeded &&
                                  !obstacleProbe.Cells.Contains(draft.DynamicObstacle);
            var costDetour = !draft.CostEnabled ||
                             costProbe.Succeeded && !costProbe.Cells.Contains(draft.CostCell);
            var recoveryVerified = !draft.RecoverBlockedEndpoints ||
                                   recoveryProbe.Succeeded && recoveryProbe.RecoveredStart;
            var budgetVerified = budgetProbe.Failure == NavigationPathFailure.SearchBudgetExceeded;
            var partialUpdate = dynamicPlan.ChangedCells.Count > 0 && !dynamicPlan.RequiresFullSearchReset;
            var diff = BuildPathfindingDiffUx6V(sourceArea!, sourceAgent!, candidateArea, candidateAgent!);
            if (diff.Count > 0)
            {
                _approvedPathfindingUx6V = draft;
                _approvedPathfindingRevisionUx6V = history.State.Revision;
                _approvedPathfindingFingerprintUx6V = CandidateFingerprintUx6V(Project!, draft);
            }

            var results = new[]
            {
                path.Succeeded
                    ? $"Chemin A* · {path.Cells.Count} cellules · coût {NumberUx6V(path.TotalCost)} · {path.VisitedCellCount} visitées"
                    : $"Chemin A* refusé · {HumanFailureUx6V(path.Failure)} · {path.VisitedCellCount} cellules visitées",
                $"Surface Core · {surface.WidthCells} × {surface.DepthCells} · {surface.WalkableCells.Count} marchables · {surface.OffMeshEdges.Count} liaison(s)",
                enginePlanBuilt
                    ? $"Plan Godot dérivé · {enginePlan.Triangles.Count} polygones · {enginePlan.BlockedTriangles} bloqué(s) · {enginePlan.Links.Count} lien(s)"
                    : $"Plan Godot non matérialisé · {HumanEngineDiagnosticUx6V(enginePlan.Diagnostics.FirstOrDefault()?.Code)}",
                $"Obstacle dynamique · {(obstacleAvoided ? "contourné" : "non contourné")} · coût local · {(costDetour ? "évité" : "traversé")}",
                $"Récupération · {(recoveryVerified ? "extrémité bloquée récupérée" : "non prouvée")} · budget · {(budgetVerified ? "dépassement localisé" : "non prouvé")}",
                $"Mise à jour dynamique · {dynamicPlan.ChangedCells.Count} cellule(s) · {dynamicPlan.ReusedCellCount} réutilisée(s) · {(partialUpdate ? "partielle" : "réinitialisation complète")}",
                bake.RequiresFullBake
                    ? $"Rebake auteur · complet · {bake.ChangedCells.Count} cellule(s)"
                    : $"Rebake auteur · partiel · {bake.ChangedCells.Count} modifiée(s) · {bake.ReusedCellCount} réutilisée(s)",
                "Aucun NavigationAgent3D, acteur, évitement local, mouvement, serveur ou transport réseau n’est exécuté · plans copiés seulement.",
            };
            var displayStride = Math.Max(1, (int)Math.Ceiling(surface.WalkableCells.Count / 8_000d));
            var displayedWalkable = surface.WalkableCells.Where((_, index) => index % displayStride == 0)
                .Take(8_000).ToArray();
            var grid = new StudioUx6VGridSnapshot(surface.WidthCells, surface.DepthCells,
                displayedWalkable, obstacles.Order().ToArray(), costs.Keys.Order().ToArray(),
                path.Cells, path.RequestedStart, path.RequestedGoal, path.ResolvedStart, path.ResolvedGoal);
            return new StudioUx6VPreview(true, diff.Count > 0, true,
                diff.Count > 0
                    ? "Aperçu sec canonique validé · la navigation source reste inchangée."
                    : "Aperçu sec validé · aucune configuration persistante ne change.",
                diff, [], results, grid, surfaceBuilt, enginePlanBuilt, obstacleAvoided, costDetour,
                recoveryVerified, budgetVerified, partialUpdate, true);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           OverflowException)
        {
            return FailedPathfindingPreviewUx6V(new StudioUx6VDiagnostic(
                "studio.pathfinding.preview_invalid", $"areas/{draft.AreaId}/navigation/pathfinding",
                HumanExceptionUx6V(exception.Message)));
        }
    }

    internal StudioUx6VActionResult ApplyApprovedPathfindingUx6V()
    {
        var draft = _approvedPathfindingUx6V;
        var project = Project;
        var history = History;
        if (draft is null || project is null || history is null ||
            _approvedPathfindingRevisionUx6V != history.State.Revision ||
            draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint, SourceFingerprintUx6V(project), StringComparison.Ordinal) ||
            !string.Equals(_approvedPathfindingFingerprintUx6V, CandidateFingerprintUx6V(project, draft),
                StringComparison.Ordinal) ||
            !TryBuildPathfindingCandidateUx6V(draft, false, out var command, out _, out _, out _, out _, out _,
                out _))
        {
            CancelPathfindingApprovalUx6V();
            return new(false, "Le projet ou le brouillon a changé · relancez l’aperçu sec canonique.");
        }

        var dryRun = _setNavigationTopologyUx6V.Handle(history.State, command! with { DryRun = true });
        if (!dryRun.Succeeded)
        {
            CancelPathfindingApprovalUx6V();
            return new(false, HumanCommandFailureUx6V(dryRun.Diagnostics.FirstOrDefault()?.Code));
        }
        var applied = _setNavigationTopologyUx6V.Handle(history.State, command!);
        CancelPathfindingApprovalUx6V();
        if (!applied.Succeeded)
            return new(false, HumanCommandFailureUx6V(applied.Diagnostics.FirstOrDefault()?.Code));
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, "Navigation appliquée · le laboratoire dynamique reste non persistant.");
    }

    internal StudioUx6VActionResult UndoPathfindingUx6V()
    {
        CancelPathfindingApprovalUx6V();
        var result = Undo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx6VActionResult RedoPathfindingUx6V()
    {
        CancelPathfindingApprovalUx6V();
        var result = Redo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx6VActionResult SavePathfindingUx6V()
    {
        var result = Save();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx6VApprovalSnapshot CapturePathfindingApprovalUx6V() =>
        new(_approvedPathfindingUx6V, _approvedPathfindingRevisionUx6V, _approvedPathfindingFingerprintUx6V);

    internal void RestorePathfindingApprovalUx6V(StudioUx6VApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedPathfindingUx6V = snapshot.Draft;
        _approvedPathfindingRevisionUx6V = snapshot.Revision;
        _approvedPathfindingFingerprintUx6V = snapshot.Fingerprint;
    }

    internal void CancelPathfindingApprovalUx6V()
    {
        _approvedPathfindingUx6V = null;
        _approvedPathfindingRevisionUx6V = null;
        _approvedPathfindingFingerprintUx6V = null;
    }

    internal void CloseUx6V() => CancelPathfindingApprovalUx6V();

    internal static StudioUx6VDraft ChangedPathfindingDraftUx6V(StudioUx6VDraft source) => source with
    {
        CellSize = source.CellSize == .75f ? .8f : .75f,
        AgentRadius = Math.Clamp(source.AgentRadius + .05f, .1f, 8f),
        AgentHeight = Math.Clamp(source.AgentHeight + .1f, .2f, 16f),
        CanOpenDoors = true,
        CanUseOffMeshLinks = true,
        CanFly = false,
        DoorState = NavigationDoorState.AllClosed,
        Start = new NavigationCell(1, 5),
        Goal = new NavigationCell(18, 5),
        DynamicObstacleEnabled = true,
        DynamicObstacle = new NavigationCell(9, 5),
        CostEnabled = true,
        CostCell = new NavigationCell(8, 5),
        CostMultiplier = 40f,
        RecoverBlockedEndpoints = true,
        RecoveryRadius = 4,
        MaximumVisitedCells = 100_000,
    };

    internal static ProjectDefinition CreateEvidenceProjectUx6V(ProjectDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var areaId = AreaId.From(StableIdUx6V(source.Id.Value, Ux6VEvidenceSlug));
        var navigation = AreaNavigationDefinition.CreateDefault(areaId);
        var area = new AreaDefinition(areaId, Ux6VEvidenceSlug, "Laboratoire de cheminement avancé", [],
            AreaKind.Exterior, new AreaDimensions(16, 12), navigation: navigation);
        var areas = source.Areas.Where(item => item.Id != areaId).Append(area).ToArray();
        return new ProjectDefinition(source.Id, source.Title, source.Module, source.Blueprints, areas,
            source.Dialogues, source.Quests);
    }

    internal StudioUx6VSmokeResult RunUx6VContractSmoke()
    {
        var real = Project;
        if (real is null) return FailedSmokeUx6V();
        var realReadable = CanReadRealNavigationUx6V(real);
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux6-v-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var cleanup = false;
        try
        {
            var evidence = CreateEvidenceProjectUx6V(real);
            var created = ProjectSession.Create(root, evidence);
            if (!created.Succeeded || !editing.Open(root).Succeeded) return FailedSmokeUx6V();
            var area = editing.Project!.Areas.Single(item => item.Slug == Ux6VEvidenceSlug);
            var agent = area.Navigation.AgentProfiles.First(item => !item.CanFly);
            var explicitSelection = editing.CreatePathfindingDraftUx6V(false, area.Id, agent.Id) is null &&
                                    editing.CreatePathfindingDraftUx6V(true, null, agent.Id) is null &&
                                    editing.CreatePathfindingDraftUx6V(true, area.Id, null) is null &&
                                    editing.CreatePathfindingDraftUx6V(true, area.Id, agent.Id) is not null;
            var baseline = editing.CreatePathfindingDraftUx6V(true, area.Id, agent.Id)!;
            var invalidConfiguration = !editing.PreviewPathfindingUx6V(baseline with { CellSize = .1f }).Succeeded &&
                                       !editing.PreviewPathfindingUx6V(baseline with
                                       {
                                           CostMultiplier = 0,
                                           MaximumVisitedCells = 0,
                                       }).Succeeded;

            var history = editing.History!;
            var beforeProject = editing.Project;
            var beforeRevision = history.State.Revision;
            var beforeJournal = history.Journal.ToArray();
            var changed = ChangedPathfindingDraftUx6V(baseline);
            var preview = editing.PreviewPathfindingUx6V(changed);
            var canonicalDryRun = preview is { Succeeded: true, CanApply: true, CanonicalDryRun: true } &&
                                  ReferenceEquals(beforeProject, editing.Project) &&
                                  history.State.Revision == beforeRevision &&
                                  history.Journal.SequenceEqual(beforeJournal) && !editing.IsDirty;

            var externalArea = editing.Project!.Areas.Single(item => item.Id == area.Id);
            var external = editing._setNavigationTopologyUx6V.Handle(history.State,
                new SetNavigationTopologyCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                    [new AreaNavigationTopologyUpdate(area.Id, externalArea.Navigation)], [], null, false));
            if (!external.Succeeded) return FailedSmokeUx6V();
            history.Record(external);
            var staleSource = editing.ApplyApprovedPathfindingUx6V();
            var staleSourceBlocked = !staleSource.Succeeded && editing.Undo().Succeeded;

            var refreshedArea = editing.Project!.Areas.Single(item => item.Id == area.Id);
            var refreshedAgent = refreshedArea.Navigation.AgentProfiles.Single(item => item.Id == agent.Id);
            var tamper = ChangedPathfindingDraftUx6V(
                editing.CreatePathfindingDraftUx6V(true, refreshedArea.Id, refreshedAgent.Id)!);
            var tamperPreview = editing.PreviewPathfindingUx6V(tamper);
            var approval = editing.CapturePathfindingApprovalUx6V();
            editing.RestorePathfindingApprovalUx6V(approval with
            {
                Draft = approval.Draft! with { MaximumVisitedCells = approval.Draft.MaximumVisitedCells + 1 },
            });
            var staleApproval = editing.ApplyApprovedPathfindingUx6V();
            var staleApprovalBlocked = tamperPreview.Succeeded && !staleApproval.Succeeded;

            var sourceProject = editing.Project!;
            var sourceArea = sourceProject.Areas.Single(item => item.Id == area.Id);
            var sourceAgent = sourceArea.Navigation.AgentProfiles.Single(item => item.Id == agent.Id);
            var fresh = ChangedPathfindingDraftUx6V(
                editing.CreatePathfindingDraftUx6V(true, sourceArea.Id, sourceAgent.Id)!);
            if (!editing.TryBuildPathfindingCandidateUx6V(fresh, false, out _, out _, out _, out _,
                    out var expectedProject, out _, out _)) return FailedSmokeUx6V();
            var freshPreview = editing.PreviewPathfindingUx6V(fresh);
            var applied = editing.ApplyApprovedPathfindingUx6V();
            var finalProject = editing.Project!;
            var finalArea = finalProject.Areas.Single(item => item.Id == area.Id);
            var canonicalCommand = freshPreview.Succeeded && applied.Succeeded &&
                                   history.Journal.Last().CommandName == "set_navigation_topology";
            var navigationNeighbors = ReferenceEquals(sourceArea.Navigation.Anchors, finalArea.Navigation.Anchors) &&
                                      ReferenceEquals(sourceArea.Navigation.Waypoints, finalArea.Navigation.Waypoints) &&
                                      sourceArea.Navigation.OffMeshLinks.Count == finalArea.Navigation.OffMeshLinks.Count &&
                                      sourceArea.Navigation.OffMeshLinks.Zip(finalArea.Navigation.OffMeshLinks)
                                          .All(pair => ReferenceEquals(pair.First, pair.Second)) &&
                                      sourceArea.Navigation.AgentProfiles.Where(item => item.Id != agent.Id)
                                          .All(item => finalArea.Navigation.AgentProfiles.Any(candidate =>
                                              candidate.Id == item.Id && ReferenceEquals(candidate, item)));
            var projectNeighbors = expectedProject is not null && string.Equals(
                ProjectJsonSerializer.Serialize(expectedProject), ProjectJsonSerializer.Serialize(finalProject),
                StringComparison.Ordinal);
            var finalJson = ProjectJsonSerializer.Serialize(finalProject);
            var undo = editing.Undo();
            var undone = undo.Succeeded && ReferenceEquals(sourceProject, editing.Project);
            var redo = editing.Redo();
            var redone = redo.Succeeded && ReferenceEquals(finalProject, editing.Project);
            var persistence = editing.Save().Succeeded && reopened.Open(root).Succeeded &&
                              reopened.Project is not null && string.Equals(finalJson,
                                  ProjectJsonSerializer.Serialize(reopened.Project), StringComparison.Ordinal);
            var human = !ContainsUuidUx6V(string.Join(' ', editing.PathfindingAreaChoicesUx6V()
                    .Select(item => item.Label)) + " " + string.Join(' ', freshPreview.Diff) + " " +
                string.Join(' ', freshPreview.Results));
            var bounded = freshPreview.RuntimeClaimsBounded && freshPreview.Results.Any(item =>
                item.Contains("Aucun NavigationAgent3D", StringComparison.Ordinal));

            editing.Close();
            reopened.Close();
            cleanup = DeleteTemporaryUx6V(root);
            var succeeded = explicitSelection && realReadable && invalidConfiguration && canonicalDryRun &&
                            freshPreview.SurfaceBuilt && freshPreview.EnginePlanBuilt &&
                            freshPreview.DynamicObstacleAvoided && freshPreview.CostDetour &&
                            freshPreview.RecoveryVerified && freshPreview.BudgetFailureVerified &&
                            freshPreview.PartialUpdatePlanned && staleSourceBlocked && staleApprovalBlocked &&
                            canonicalCommand && undone && redone && persistence && navigationNeighbors &&
                            projectNeighbors && human && bounded && cleanup;
            return new(succeeded, explicitSelection, realReadable, invalidConfiguration, canonicalDryRun,
                freshPreview.SurfaceBuilt, freshPreview.EnginePlanBuilt, freshPreview.DynamicObstacleAvoided,
                freshPreview.CostDetour, freshPreview.RecoveryVerified, freshPreview.BudgetFailureVerified,
                freshPreview.PartialUpdatePlanned, staleSourceBlocked, staleApprovalBlocked, canonicalCommand,
                undone && redone, persistence, navigationNeighbors, projectNeighbors, human, bounded, cleanup);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_V_CONTRACT_FAILED " +
                              $"type={exception.GetType().Name} message={exception.Message}");
            return FailedSmokeUx6V();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            cleanup = DeleteTemporaryUx6V(root);
            if (!cleanup) Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_V_CLEANUP_FAILED");
        }
    }

    private bool TryBuildPathfindingCandidateUx6V(
        StudioUx6VDraft draft,
        bool dryRun,
        out SetNavigationTopologyCommand? command,
        out AreaDefinition? sourceArea,
        out NavigationAgentProfile? sourceAgent,
        out AreaDefinition? candidateArea,
        out ProjectDefinition? candidateProject,
        out NavigationAgentProfile? candidateAgent,
        out StudioUx6VDiagnostic? diagnostic)
    {
        command = null;
        sourceArea = null;
        sourceAgent = null;
        candidateArea = null;
        candidateProject = null;
        candidateAgent = null;
        diagnostic = null;
        var project = Project;
        var history = History;
        if (project is null || history is null)
        {
            diagnostic = new("studio.pathfinding.project_required", "project",
                "Ouvrez un module avant de configurer le pathfinding.");
            return false;
        }
        if (draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint, SourceFingerprintUx6V(project), StringComparison.Ordinal))
        {
            diagnostic = new("studio.pathfinding.source_changed", "project",
                "Le projet a changé · rechargez la zone et le profil d’agent.");
            return false;
        }
        sourceArea = project.Areas.FirstOrDefault(item => item.Id == draft.AreaId);
        sourceAgent = sourceArea?.Navigation.AgentProfiles.FirstOrDefault(item => item.Id == draft.AgentId);
        if (sourceArea is null || sourceAgent is null)
        {
            diagnostic = new("studio.pathfinding.selection_missing", $"areas/{draft.AreaId}/navigation",
                "La zone ou le profil d’agent sélectionné n’existe plus.");
            return false;
        }
        diagnostic = ValidatePathfindingDraftUx6V(draft);
        if (diagnostic is not null) return false;
        try
        {
            var originalAgent = sourceAgent;
            var changedAgent = originalAgent.Radius != draft.AgentRadius ||
                               originalAgent.Height != draft.AgentHeight ||
                               originalAgent.CanOpenDoors != draft.CanOpenDoors ||
                               originalAgent.CanUseOffMeshLinks != draft.CanUseOffMeshLinks ||
                               originalAgent.CanFly != draft.CanFly;
            candidateAgent = changedAgent
                ? new NavigationAgentProfile(originalAgent.Id, originalAgent.Name, draft.AgentRadius, draft.AgentHeight,
                    draft.CanOpenDoors, draft.CanUseOffMeshLinks, draft.CanFly, Ux6VProvenance,
                    originalAgent.SchemaVersion)
                : originalAgent;
            var selected = candidateAgent;
            var agents = sourceArea.Navigation.AgentProfiles.Select(item =>
                item.Id == originalAgent.Id ? selected : item).ToArray();
            var changed = changedAgent || sourceArea.Navigation.CellSize != draft.CellSize;
            var navigation = new AreaNavigationDefinition(sourceArea.Navigation.Id, draft.CellSize, agents,
                sourceArea.Navigation.OffMeshLinks, changed ? Ux6VProvenance : sourceArea.Navigation.Provenance,
                sourceArea.Navigation.SchemaVersion, sourceArea.Navigation.Anchors, sourceArea.Navigation.Waypoints);
            candidateAgent = navigation.AgentProfiles.Single(item => item.Id == originalAgent.Id);
            candidateArea = sourceArea.WithNavigation(navigation);
            var areaId = sourceArea.Id;
            var replacement = candidateArea;
            candidateProject = new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
                project.Areas.Select(item => item.Id == areaId ? replacement : item).ToArray(), project.Dialogues,
                project.Quests);
            command = new SetNavigationTopologyCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                [new AreaNavigationTopologyUpdate(areaId, navigation)], [], null, dryRun);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           OverflowException)
        {
            diagnostic = new("studio.pathfinding.configuration_invalid",
                $"areas/{draft.AreaId}/navigation", HumanExceptionUx6V(exception.Message));
            return false;
        }
    }

    private static StudioUx6VDiagnostic? ValidatePathfindingDraftUx6V(StudioUx6VDraft draft)
    {
        var root = $"areas/{draft.AreaId}/navigation";
        if (!float.IsFinite(draft.CellSize) || draft.CellSize is < .25f or > 4f)
            return new("studio.pathfinding.cell_size", $"{root}/cellSize",
                "Le pas de cellule doit rester compris entre 0,25 m et 4 m.");
        if (!float.IsFinite(draft.AgentRadius) || draft.AgentRadius is < .1f or > 8f ||
            !float.IsFinite(draft.AgentHeight) || draft.AgentHeight is < .2f or > 16f)
            return new("studio.pathfinding.agent_bounds", $"{root}/agentProfiles/{draft.AgentId}",
                "Le rayon ou la hauteur de l’agent sort des limites canoniques.");
        if (!Enum.IsDefined(draft.DoorState))
            return new("studio.pathfinding.door_state", $"{root}/pathfinding/doorState",
                "La politique de portes choisie n’est pas reconnue.");
        if (draft.Start == draft.Goal)
            return new("studio.pathfinding.endpoints", $"{root}/pathfinding/endpoints",
                "Le départ et la destination doivent être distincts.");
        if (draft.DynamicObstacleEnabled &&
            (draft.DynamicObstacle == draft.Start || draft.DynamicObstacle == draft.Goal))
            return new("studio.pathfinding.obstacle_endpoint", $"{root}/pathfinding/dynamicObstacle",
                "Placez l’obstacle dynamique entre le départ et la destination.");
        if (draft.CostEnabled && (draft.CostCell == draft.Start || draft.CostCell == draft.Goal ||
                                  !float.IsFinite(draft.CostMultiplier) ||
                                  draft.CostMultiplier is <= 1f or > 1_000f))
            return new("studio.pathfinding.cost_invalid", $"{root}/pathfinding/costMultiplier",
                "Le coût local doit être supérieur à 1, inférieur ou égal à 1 000 et hors des extrémités.");
        if (draft.RecoveryRadius is < 0 or > 128 || draft.MaximumVisitedCells is < 1 or > 1_000_000)
            return new("studio.pathfinding.query_budget", $"{root}/pathfinding/queryBudget",
                "Le rayon de récupération doit rester entre 0 et 128 et le budget entre 1 et 1 000 000 cellules.");
        return null;
    }

    private static StudioUx6VDiagnostic? ValidatePathCellsUx6V(StudioUx6VDraft draft, NavigationSurface surface)
    {
        var root = $"areas/{draft.AreaId}/navigation/pathfinding";
        bool Inside(NavigationCell cell) => cell.X >= 0 && cell.Z >= 0 &&
                                            cell.X < surface.WidthCells && cell.Z < surface.DepthCells;
        if (!Inside(draft.Start) || !Inside(draft.Goal) || !surface.IsWalkable(draft.Start) ||
            !surface.IsWalkable(draft.Goal))
            return new("studio.pathfinding.endpoint_outside", $"{root}/endpoints",
                "Le départ et la destination doivent être des cellules marchables de la surface calculée.");
        if (draft.DynamicObstacleEnabled &&
            (!Inside(draft.DynamicObstacle) || !surface.IsWalkable(draft.DynamicObstacle)))
            return new("studio.pathfinding.obstacle_outside", $"{root}/dynamicObstacle",
                "L’obstacle dynamique doit cibler une cellule marchable de la surface.");
        if (draft.CostEnabled && (!Inside(draft.CostCell) || !surface.IsWalkable(draft.CostCell)))
            return new("studio.pathfinding.cost_outside", $"{root}/costCell",
                "Le coût local doit cibler une cellule marchable de la surface.");
        return null;
    }

    private static IReadOnlyList<string> BuildPathfindingDiffUx6V(
        AreaDefinition sourceArea,
        NavigationAgentProfile sourceAgent,
        AreaDefinition candidateArea,
        NavigationAgentProfile candidateAgent)
    {
        var result = new List<string>();
        if (sourceArea.Navigation.CellSize != candidateArea.Navigation.CellSize)
            result.Add($"Pas de cellule · {NumberUx6V(sourceArea.Navigation.CellSize)} m → " +
                       $"{NumberUx6V(candidateArea.Navigation.CellSize)} m");
        if (sourceAgent.Radius != candidateAgent.Radius)
            result.Add($"Rayon {HumanTextUx6V(sourceAgent.Name, "Agent")} · " +
                       $"{NumberUx6V(sourceAgent.Radius)} m → {NumberUx6V(candidateAgent.Radius)} m");
        if (sourceAgent.Height != candidateAgent.Height)
            result.Add($"Hauteur · {NumberUx6V(sourceAgent.Height)} m → {NumberUx6V(candidateAgent.Height)} m");
        if (sourceAgent.CanOpenDoors != candidateAgent.CanOpenDoors)
            result.Add($"Portes · {(candidateAgent.CanOpenDoors ? "ouvrables" : "bloquantes")}");
        if (sourceAgent.CanUseOffMeshLinks != candidateAgent.CanUseOffMeshLinks)
            result.Add($"Liaisons hors surface · {(candidateAgent.CanUseOffMeshLinks ? "autorisées" : "refusées")}");
        if (sourceAgent.CanFly != candidateAgent.CanFly)
            result.Add($"Vol · {(candidateAgent.CanFly ? "autorisé" : "surface uniquement")}");
        return result;
    }

    private static StudioUx6VPreview FailedPathfindingPreviewUx6V(StudioUx6VDiagnostic diagnostic) =>
        new(false, false, false, diagnostic.Message, [], [diagnostic], [], null,
            false, false, false, false, false, false, false, true);

    private static bool CanReadRealNavigationUx6V(ProjectDefinition project)
    {
        try
        {
            var area = project.Areas.FirstOrDefault(item => item.Id == project.Module.EntryAreaId) ??
                       project.Areas.First();
            var agent = area.Navigation.AgentProfiles.First();
            return NavigationSurfaceBuilder.Build(project, area, agent).WalkableCells.Count > 0;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           OverflowException)
        {
            return false;
        }
    }

    private static string SourceFingerprintUx6V(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));

    private static string CandidateFingerprintUx6V(ProjectDefinition project, StudioUx6VDraft draft)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            WriteTextUx6V(writer, "ux6-v-pathfinding-draft-v1");
            writer.Write(draft.BaseRevision.Value);
            WriteTextUx6V(writer, draft.SourceFingerprint);
            writer.Write(draft.AreaId.Value.ToByteArray());
            writer.Write(draft.AgentId.Value.ToByteArray());
            WriteTextUx6V(writer, draft.AgentName);
            writer.Write(draft.CellSize);
            writer.Write(draft.AgentRadius);
            writer.Write(draft.AgentHeight);
            writer.Write(draft.CanOpenDoors);
            writer.Write(draft.CanUseOffMeshLinks);
            writer.Write(draft.CanFly);
            writer.Write((int)draft.DoorState);
            WriteCellUx6V(writer, draft.Start);
            WriteCellUx6V(writer, draft.Goal);
            writer.Write(draft.DynamicObstacleEnabled);
            WriteCellUx6V(writer, draft.DynamicObstacle);
            writer.Write(draft.CostEnabled);
            WriteCellUx6V(writer, draft.CostCell);
            writer.Write(draft.CostMultiplier);
            writer.Write(draft.RecoverBlockedEndpoints);
            writer.Write(draft.RecoveryRadius);
            writer.Write(draft.MaximumVisitedCells);
            WriteTextUx6V(writer, ProjectJsonSerializer.Serialize(project));
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteCellUx6V(BinaryWriter writer, NavigationCell cell)
    {
        writer.Write(cell.X);
        writer.Write(cell.Z);
    }

    private static void WriteTextUx6V(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static Guid StableIdUx6V(Guid owner, string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{owner:D}|ux6-v|{key}"));
        Span<byte> value = stackalloc byte[16];
        bytes.AsSpan(0, value.Length).CopyTo(value);
        value[6] = (byte)((value[6] & 0x0f) | 0x40);
        value[8] = (byte)((value[8] & 0x3f) | 0x80);
        return new Guid(value);
    }

    private static string HumanCommandFailureUx6V(string? code) => code switch
    {
        SetNavigationTopologyHandler.RevisionConflictCode =>
            "Le projet a changé · rechargez la zone et le profil d’agent.",
        SetNavigationTopologyHandler.MissingAreaCode =>
            "La zone sélectionnée n’existe plus.",
        _ => "La commande canonique a refusé la configuration de navigation.",
    };

    private static string HumanFailureUx6V(NavigationPathFailure failure) => failure switch
    {
        NavigationPathFailure.None => "aucune erreur",
        NavigationPathFailure.StartBlocked => "départ bloqué",
        NavigationPathFailure.GoalBlocked => "destination bloquée",
        NavigationPathFailure.SearchBudgetExceeded => "budget de recherche dépassé",
        NavigationPathFailure.Unreachable => "destination inaccessible",
        _ => "échec non reconnu",
    };

    private static string HumanEngineDiagnosticUx6V(string? code) => code switch
    {
        SurfaceNavigationPlanBuilder.MissingGroundAgentCode => "profil d’agent de surface requis",
        SurfaceNavigationPlanBuilder.EmptySurfaceCode => "surface polygonale vide",
        null => "terrain polygonal non disponible pour cette zone",
        _ => "diagnostic de terrain disponible dans le plan",
    };

    private static string HumanExceptionUx6V(string? message) => string.IsNullOrWhiteSpace(message)
        ? "La configuration ne peut pas être évaluée."
        : message.Replace("Navigation", "La navigation", StringComparison.Ordinal)
            .Replace("navigation", "navigation", StringComparison.Ordinal);

    private static string HumanTextUx6V(string? value, string fallback)
    {
        var text = value?.Trim();
        return string.IsNullOrWhiteSpace(text) || Guid.TryParse(text, out _) ? fallback : text;
    }

    private static string NumberUx6V(float value) =>
        value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    private static bool ContainsUuidUx6V(string value) => System.Text.RegularExpressions.Regex.IsMatch(value,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b");

    internal static bool DeleteTemporaryUx6V(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) return true;
        try
        {
            var full = Path.GetFullPath(root);
            var temporary = Path.GetFullPath(Path.GetTempPath());
            if (!full.StartsWith(temporary, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux6-v-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static StudioUx6VSmokeResult FailedSmokeUx6V() => new(
        Succeeded: false,
        ExplicitSelection: false,
        RealModuleReadable: false,
        InvalidConfigurationBlocked: false,
        CanonicalDryRun: false,
        SurfaceBuilt: false,
        EnginePlanBuilt: false,
        DynamicObstacleAvoided: false,
        CostDetour: false,
        RecoveryVerified: false,
        BudgetFailureVerified: false,
        PartialUpdatePlanned: false,
        StaleSourceBlocked: false,
        StaleApprovalBlocked: false,
        CanonicalCommand: false,
        UndoRedo: false,
        Persistence: false,
        NavigationNeighborsPreserved: false,
        ProjectNeighborsPreserved: false,
        HumanText: false,
        RuntimeClaimsBounded: false,
        TemporaryWorkspaceReleased: false);
}
