using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Commands;
using Forge.Application.Navigation;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Core.Navigation;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2NavEProvenance = "studio:ux2-nav-e:locator-collections";
    private const string Ux2NavEEvidenceProvenance = "studio:ux2-nav-e:evidence";

    private readonly SetNavigationTopologyHandler _setNavigationTopologyUx2NavE = new(new SystemClock());
    private StudioUx2NavEDraft? _approvedLocatorUx2NavE;
    private ProjectRevision? _approvedLocatorRevisionUx2NavE;
    private string? _approvedLocatorFingerprintUx2NavE;

    internal IReadOnlyList<StudioUx2NavEAreaChoice> LocatorAreaChoicesUx2NavE
    {
        get
        {
            var project = Project;
            if (project is null)
            {
                return [];
            }
            var duplicateNames = project.Areas.GroupBy(item => HumanUx2NavE(item.DisplayName, "Zone"),
                    StringComparer.CurrentCultureIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(),
                    StringComparer.CurrentCultureIgnoreCase);
            return project.Areas.Select(item =>
                {
                    var name = HumanUx2NavE(item.DisplayName, "Zone");
                    var label = duplicateNames[name] > 1
                        ? $"{name} · {HumanUx2NavE(item.Slug, "zone")}" : name;
                    var evidence = item.Navigation.Waypoints.Locators.Any(locator =>
                                           string.Equals(locator.Provenance, Ux2NavEEvidenceProvenance,
                                               StringComparison.Ordinal)) ||
                                   item.Navigation.Waypoints.Collections.Any(collection =>
                                       string.Equals(collection.Provenance, Ux2NavEEvidenceProvenance,
                                           StringComparison.Ordinal));
                    return new StudioUx2NavEAreaChoice(item.Id, label, evidence);
                })
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal IReadOnlyList<StudioUx2NavELocatorChoice> LocatorChoicesUx2NavE(AreaId? areaId)
    {
        var area = areaId is null ? null : Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (area is null)
        {
            return [];
        }
        var duplicates = area.Navigation.Waypoints.Locators
            .GroupBy(item => HumanUx2NavE(item.Name, "Ancre"),
                StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(),
                StringComparer.CurrentCultureIgnoreCase);
        return area.Navigation.Waypoints.Locators.Select(item =>
                {
                    var name = HumanUx2NavE(item.Name, "Ancre");
                    var purpose = HumanPurposeUx2NavE(item.Purpose);
                    var label = duplicates[name] > 1
                        ? $"{name} · {purpose} · position {item.Position.X:0.##}, {item.Position.Z:0.##}"
                        : $"{name} · {purpose}";
                    return new StudioUx2NavELocatorChoice(item.Id, label,
                        string.Equals(item.Provenance, Ux2NavEEvidenceProvenance,
                            StringComparison.Ordinal) && item.Arrival is not null);
                })
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.LocatorId)
            .ToArray();
    }

    internal IReadOnlyList<StudioUx2NavECollectionChoice> LocatorCollectionChoicesUx2NavE(
        AreaId? areaId)
    {
        var area = areaId is null ? null : Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (area is null)
        {
            return [];
        }
        var locators = area.Navigation.Waypoints.Locators;
        return area.Navigation.Waypoints.Collections.Select(item =>
                new StudioUx2NavECollectionChoice(item.Id,
                    $"{HumanUx2NavE(item.Name, "Groupe")} · {HumanSelectionUx2NavE(item.Selection)} · " +
                    $"{WaypointRouteService.ResolveCollection(item, locators).Count} résultat(s)",
                    string.Equals(item.Provenance, Ux2NavEEvidenceProvenance,
                        StringComparison.Ordinal)))
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.CollectionId)
            .ToArray();
    }

    internal IReadOnlyList<StudioUx2NavEBlueprintChoice> LocatorBlueprintChoicesUx2NavE(
        StudioUx2NavELocatorDraft? draft)
    {
        var project = Project;
        if (project is null)
        {
            return [];
        }
        var result = new List<StudioUx2NavEBlueprintChoice>
        {
            new(null, "Aucun blueprint · ressource directe"),
        };
        if (draft is { BlueprintIsProtected: true, BlueprintId: { } protectedId })
        {
            result.Add(new(protectedId, "Blueprint existant protégé · valeur conservée", true));
        }
        var duplicates = project.Blueprints
            .GroupBy(item => HumanUx2NavE(item.DisplayName, "Blueprint"),
                StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(),
                StringComparer.CurrentCultureIgnoreCase);
        foreach (var blueprint in project.Blueprints
                     .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                     .ThenBy(item => item.Slug, StringComparer.Ordinal))
        {
            if (result.Any(item => item.BlueprintId == blueprint.Id))
            {
                continue;
            }
            var name = HumanUx2NavE(blueprint.DisplayName, "Blueprint");
            var label = duplicates[name] > 1
                ? $"{name} · {HumanUx2NavE(blueprint.Slug, "profil")}" : name;
            result.Add(new(blueprint.Id, label));
        }
        return result;
    }

    internal StudioUx2NavELocatorDraft? CreateLocatorDraftUx2NavE(AreaId? areaId,
        Guid? locatorId)
    {
        var area = areaId is null ? null : Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        var locator = locatorId is null ? null : area?.Navigation.Waypoints.Locators
            .SingleOrDefault(item => item.Id == locatorId.Value);
        if (locator is null)
        {
            return null;
        }
        var protectedBlueprint = locator.BlueprintId is { } blueprintId &&
                                 Project?.Blueprints.All(item => item.Id != blueprintId) == true;
        return new StudioUx2NavELocatorDraft(locator.Id, false, locator, locator.Name,
            locator.Position, locator.Purpose, locator.Tags.ToArray(), locator.AssetId ?? string.Empty,
            locator.BlueprintId, protectedBlueprint,
            (locator.ExplicitOverrides ?? new Dictionary<string, string>())
            .Select(item => new StudioUx2NavEOverrideDraft(item.Key, item.Value,
                ContainsGuidValueUx2NavE(item.Key) || ContainsGuidValueUx2NavE(item.Value)))
            .ToArray(),
            locator.RequiredAtRuntime);
    }

    internal StudioUx2NavELocatorDraft? CreateNewLocatorDraftUx2NavE(AreaId? areaId)
    {
        var area = areaId is null ? null : Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        return area is null ? null : new StudioUx2NavELocatorDraft(Guid.NewGuid(), true, null,
            "Nouvelle ancre audio", Vector3Value.Zero, LocatorPurpose.Audio, ["audio"], string.Empty,
            null, false, [], false);
    }

    internal StudioUx2NavECollectionDraft? CreateLocatorCollectionDraftUx2NavE(AreaId? areaId,
        Guid? collectionId)
    {
        var area = areaId is null ? null : Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        var collection = collectionId is null ? null : area?.Navigation.Waypoints.Collections
            .SingleOrDefault(item => item.Id == collectionId.Value);
        if (collection is null)
        {
            return null;
        }
        return new StudioUx2NavECollectionDraft(collection.Id, false, collection,
            collection.Name, collection.Query, collection.LocatorIds.ToArray(), collection.Purpose,
            (collection.RequiredTags ?? []).ToArray(), collection.Selection,
            collection.Origin is not null, collection.Origin ?? Vector3Value.Zero, collection.Seed,
            collection.RequiredAtRuntime);
    }

    internal StudioUx2NavECollectionDraft? CreateNewLocatorCollectionDraftUx2NavE(AreaId? areaId)
    {
        var area = areaId is null ? null : Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (area is null)
        {
            return null;
        }
        var members = area.Navigation.Waypoints.Locators.Take(1).Select(item => item.Id).ToArray();
        return new StudioUx2NavECollectionDraft(Guid.NewGuid(), true, null, "Nouveau groupe",
            string.Empty, members, null, [], LocatorSelectionMode.Stable, false, Vector3Value.Zero,
            0, false);
    }

    internal StudioUx2NavEVisualizationDraft? CreateRouteVisualizationDraftUx2NavE(AreaId? areaId)
    {
        var area = areaId is null ? null : Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        var profile = area?.Navigation.Waypoints.RouteVisualization;
        return profile is null ? null : new StudioUx2NavEVisualizationDraft(profile,
            profile.ShowSegments, profile.ShowDirection, profile.ShowCosts, profile.ShowActions,
            profile.ShowBrokenLinks, profile.ShowNavigationProjection, profile.ShowTiming,
            profile.CostPerMeter, profile.RequiredAtRuntime);
    }

    internal StudioUx2NavEDraft? ComposeLocatorDraftUx2NavE(AreaId? areaId,
        StudioUx2NavELocatorDraft? locator, StudioUx2NavECollectionDraft? collection,
        StudioUx2NavEVisualizationDraft? visualization)
    {
        var history = History;
        var area = areaId is null ? null : Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (history is null || area is null || visualization is null)
        {
            return null;
        }
        if (locator is not null)
        {
            var current = area.Navigation.Waypoints.Locators.SingleOrDefault(item =>
                item.Id == locator.LocatorId);
            if (locator.IsNew && current is not null || !locator.IsNew &&
                (current is null || locator.Original is null || !ReferenceEquals(current, locator.Original)))
            {
                return null;
            }
        }
        if (collection is not null)
        {
            var current = area.Navigation.Waypoints.Collections.SingleOrDefault(item =>
                item.Id == collection.CollectionId);
            if (collection.IsNew && current is not null || !collection.IsNew &&
                (current is null || collection.Original is null || !ReferenceEquals(current, collection.Original)))
            {
                return null;
            }
        }
        if (!ReferenceEquals(area.Navigation.Waypoints.RouteVisualization, visualization.Original))
        {
            return null;
        }
        return new StudioUx2NavEDraft(area.Id, history.State.Revision,
            LocatorSourceFingerprintUx2NavE(area), HumanUx2NavE(area.DisplayName, "Zone"),
            area.Dimensions.Width, area.Dimensions.Depth, locator, collection, visualization);
    }

    internal StudioUx2NavEPreview PreviewLocatorsUx2NavE(StudioUx2NavEDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelLocatorsUx2NavE();
        var history = History;
        if (history is null || Project is null)
        {
            return FailedLocatorPreviewUx2NavE("Aucun module n’est ouvert.");
        }
        if (!TryBuildLocatorMutationUx2NavE(draft, true, out var command, out var sourceArea,
                out var candidateArea, out var candidateProject, out var diagnostics, out var message))
        {
            return FailedLocatorPreviewUx2NavE(message, diagnostics);
        }
        var dryRun = _setNavigationTopologyUx2NavE.Handle(history.State, command!);
        if (!dryRun.Succeeded)
        {
            var commandDiagnostics = dryRun.Diagnostics.Select(item => new StudioUx2NavEDiagnostic(
                item.Code, $"zones/{draft.AreaDisplayName}/navigation",
                HumanDiagnosticUx2NavE(item.Message))).ToArray();
            return FailedLocatorPreviewUx2NavE(commandDiagnostics.FirstOrDefault()?.Message ??
                                                "L’aperçu des ancres a été refusé.",
                commandDiagnostics);
        }

        var builtArea = candidateArea!;
        var builtProject = candidateProject!;
        var profile = builtArea.Navigation.Waypoints;
        var candidateLocator = draft.Locator is null ? null : profile.Locators.Single(item =>
            item.Id == draft.Locator.LocatorId);
        var candidateCollection = draft.Collection is null ? null : profile.Collections.Single(item =>
            item.Id == draft.Collection.CollectionId);
        var diff = BuildLocatorDiffUx2NavE(sourceArea!, candidateLocator, candidateCollection,
            profile.RouteVisualization, draft);
        var projected = BuildProjectedWaypointIdsUx2NavE(builtProject, builtArea,
            out var projectionAvailable);
        var runtime = BuildRouteDiagnosticsUx2NavE(profile, projected);
        var resolved = candidateCollection is null
            ? Array.Empty<GenericLocator>()
            : WaypointRouteService.ResolveCollection(candidateCollection, profile.Locators).ToArray();
        var overlay = BuildLocatorOverlayUx2NavE(profile, candidateLocator?.Id,
            candidateCollection?.LocatorIds ?? [], projected);
        if (diff.Count > 0)
        {
            _approvedLocatorUx2NavE = draft;
            _approvedLocatorRevisionUx2NavE = history.State.Revision;
            _approvedLocatorFingerprintUx2NavE = LocatorDraftFingerprintUx2NavE(draft);
        }
        var collectionSummary = candidateCollection is null
            ? "aucun groupe édité"
            : $"{HumanUx2NavE(candidateCollection.Name, "Groupe")} · " +
              $"{HumanSelectionUx2NavE(candidateCollection.Selection)} · {resolved.Length} résultat(s)";
        var projectionSummary = projectionAvailable
            ? $"projection canonique · {projected.Count} point(s) marchable(s)"
            : "projection indisponible · aucune approximation affichée";
        return new StudioUx2NavEPreview(true,
            diff.Count == 0
                ? "Aperçu validé · aucune différence sémantique à appliquer."
                : "Aperçu sec validé · aucun nœud moteur, document ou événement n’a été modifié.",
            $"{profile.Locators.Count} ancre(s) · {profile.Collections.Count} groupe(s) · " +
            $"{collectionSummary}\n{runtime.Count} segment(s) diagnostiqué(s) · {projectionSummary}",
            diff.Count > 0, diff, [], overlay,
            resolved.Select(item => new StudioUx2NavEResolvedLocator(
                HumanUx2NavE(item.Name, "Ancre"), item.Purpose, item.Position)).ToArray(),
            runtime, projectionAvailable, projected.Count);
    }

    internal StudioUx2NavEActionResult ApplyApprovedLocatorsUx2NavE()
    {
        var draft = _approvedLocatorUx2NavE;
        var history = History;
        if (draft is null || history is null || _approvedLocatorRevisionUx2NavE != history.State.Revision ||
            !string.Equals(_approvedLocatorFingerprintUx2NavE,
                LocatorDraftFingerprintUx2NavE(draft), StringComparison.Ordinal))
        {
            CancelLocatorsUx2NavE();
            return new(false, "Prévisualisez de nouveau les ancres avant de les appliquer.");
        }
        if (!TryBuildLocatorMutationUx2NavE(draft, false, out var command, out _, out _, out _, out _,
                out var message))
        {
            CancelLocatorsUx2NavE();
            return new(false, message);
        }
        var currentPreview = _setNavigationTopologyUx2NavE.Handle(history.State,
            command! with { DryRun = true });
        if (!currentPreview.Succeeded)
        {
            CancelLocatorsUx2NavE();
            return new(false, HumanDiagnosticUx2NavE(currentPreview.Diagnostics.FirstOrDefault()?.Message));
        }
        var applied = _setNavigationTopologyUx2NavE.Handle(history.State, command!);
        CancelLocatorsUx2NavE();
        if (!applied.Succeeded)
        {
            return new(false, HumanDiagnosticUx2NavE(applied.Diagnostics.FirstOrDefault()?.Message));
        }
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, "Ancres, groupes et visualisation appliqués · Ctrl+Z pour annuler");
    }

    internal void CancelLocatorsUx2NavE()
    {
        _approvedLocatorUx2NavE = null;
        _approvedLocatorRevisionUx2NavE = null;
        _approvedLocatorFingerprintUx2NavE = null;
    }

    internal void CloseUx2NavE() => CancelLocatorsUx2NavE();

    internal StudioUx2NavEApprovalSnapshot CaptureLocatorApprovalUx2NavE() => new(
        _approvedLocatorUx2NavE, _approvedLocatorRevisionUx2NavE,
        _approvedLocatorFingerprintUx2NavE);

    internal void RestoreLocatorApprovalUx2NavE(StudioUx2NavEApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedLocatorUx2NavE = snapshot.Draft;
        _approvedLocatorRevisionUx2NavE = snapshot.Revision;
        _approvedLocatorFingerprintUx2NavE = snapshot.Fingerprint;
    }

    private bool TryBuildLocatorMutationUx2NavE(StudioUx2NavEDraft draft, bool dryRun,
        out SetNavigationTopologyCommand? command, out AreaDefinition? sourceArea,
        out AreaDefinition? candidateArea, out ProjectDefinition? candidateProject,
        out IReadOnlyList<StudioUx2NavEDiagnostic> diagnostics, out string message)
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
            message = "Le projet a changé depuis l’ouverture de cette fiche. Rechargez les ancres.";
            return false;
        }
        sourceArea = project.Areas.SingleOrDefault(item => item.Id == draft.AreaId);
        if (sourceArea is null)
        {
            message = "La zone choisie n’existe plus.";
            return false;
        }
        if (!string.Equals(draft.SourceFingerprint, LocatorSourceFingerprintUx2NavE(sourceArea),
                StringComparison.Ordinal))
        {
            message = "Les ancres ou routes de cette zone ont changé. Rechargez la fiche.";
            return false;
        }
        var inputDiagnostics = ValidateLocatorDraftUx2NavE(draft, sourceArea, project);
        if (inputDiagnostics.Count > 0)
        {
            diagnostics = inputDiagnostics;
            message = inputDiagnostics[0].Message;
            return false;
        }

        try
        {
            var sourceProfile = sourceArea.Navigation.Waypoints;
            IReadOnlyList<GenericLocator> locators = sourceProfile.Locators;
            if (draft.Locator is { } locatorDraft)
            {
                var original = sourceProfile.Locators.SingleOrDefault(item =>
                    item.Id == locatorDraft.LocatorId);
                if (locatorDraft.IsNew && original is not null || !locatorDraft.IsNew && original is null)
                {
                    message = locatorDraft.IsNew
                        ? "Une ancre portant cette identité existe déjà. Recommencez l’ajout."
                        : "L’ancre choisie n’existe plus. Rechargez la liste.";
                    return false;
                }
                var candidate = BuildCandidateLocatorUx2NavE(locatorDraft, original);
                var updated = sourceProfile.Locators.ToList();
                if (original is null)
                {
                    updated.Add(candidate);
                }
                else
                {
                    updated[updated.FindIndex(item => item.Id == original.Id)] = candidate;
                }
                locators = updated;
            }

            IReadOnlyList<AnchorCollection> collections = sourceProfile.Collections;
            if (draft.Collection is { } collectionDraft)
            {
                var original = sourceProfile.Collections.SingleOrDefault(item =>
                    item.Id == collectionDraft.CollectionId);
                if (collectionDraft.IsNew && original is not null || !collectionDraft.IsNew && original is null)
                {
                    message = collectionDraft.IsNew
                        ? "Un groupe portant cette identité existe déjà. Recommencez l’ajout."
                        : "Le groupe choisi n’existe plus. Rechargez la liste.";
                    return false;
                }
                var candidate = BuildCandidateCollectionUx2NavE(collectionDraft, original);
                var updated = sourceProfile.Collections.ToList();
                if (original is null)
                {
                    updated.Add(candidate);
                }
                else
                {
                    updated[updated.FindIndex(item => item.Id == original.Id)] = candidate;
                }
                collections = updated;
            }

            var visualization = BuildCandidateVisualizationUx2NavE(draft.Visualization);
            var profile = new WaypointAuthoringProfile(sourceProfile.Id, sourceProfile.SchemaVersion,
                sourceProfile.Provenance, sourceProfile.AreaId, sourceProfile.RequiredAtRuntime,
                sourceProfile.Waypoints, sourceProfile.PatrolRoutes, locators, collections,
                visualization);
            var navigation = sourceArea.Navigation.WithWaypoints(profile);
            var builtArea = sourceArea.WithNavigation(navigation);
            candidateArea = builtArea;
            var sourceAreaId = sourceArea.Id;
            var areas = project.Areas.Select(item => item.Id == sourceAreaId ? builtArea : item)
                .ToArray();
            candidateProject = new ProjectDefinition(project.Id, project.Title, project.Module,
                project.Blueprints, areas, project.Dialogues, project.Quests);
            command = new SetNavigationTopologyCommand(CommandId.From(Guid.NewGuid()),
                draft.BaseRevision, [new(sourceAreaId, navigation)], [], null, dryRun);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           OverflowException)
        {
            message = HumanDiagnosticUx2NavE(exception.Message);
            diagnostics = [new("studio.navigation.locator_invalid",
                $"zones/{draft.AreaDisplayName}/navigation/ancres", message)];
            return false;
        }
    }

    private static GenericLocator BuildCandidateLocatorUx2NavE(StudioUx2NavELocatorDraft draft,
        GenericLocator? original)
    {
        var overrides = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in draft.ExplicitOverrides)
        {
            overrides.Add(item.IsProtected ? item.Key : item.Key.Trim(),
                item.IsProtected ? item.Value : item.Value.Trim());
        }
        var blueprintId = draft.BlueprintIsProtected ? original?.BlueprintId : draft.BlueprintId;
        var name = original is not null && ContainsGuidValueUx2NavE(original.Name)
            ? original.Name : draft.Name.Trim();
        var tags = original is not null && original.Tags.Any(ContainsGuidValueUx2NavE)
            ? original.Tags : draft.Tags.Select(item => item.Trim()).ToArray();
        var assetId = original is not null && ContainsGuidValueUx2NavE(original.AssetId)
            ? original.AssetId : EmptyToNullUx2NavE(draft.AssetId);
        return new GenericLocator(draft.LocatorId, original?.SchemaVersion ?? 1,
            original?.Provenance ?? Ux2NavEProvenance, name, draft.Position,
            draft.Purpose, tags, original?.Arrival, assetId, blueprintId,
            overrides.Count == 0 ? null : overrides,
            draft.RequiredAtRuntime);
    }

    private static AnchorCollection BuildCandidateCollectionUx2NavE(
        StudioUx2NavECollectionDraft draft, AnchorCollection? original)
    {
        var name = original is not null && ContainsGuidValueUx2NavE(original.Name)
            ? original.Name : draft.Name.Trim();
        var query = original is not null && ContainsGuidValueUx2NavE(original.Query)
            ? original.Query : draft.Query.Trim();
        var tags = original is not null &&
                   (original.RequiredTags ?? []).Any(ContainsGuidValueUx2NavE)
            ? original.RequiredTags : draft.RequiredTags.Select(item => item.Trim()).ToArray();
        return new AnchorCollection(draft.CollectionId, original?.SchemaVersion ?? 1,
            original?.Provenance ?? Ux2NavEProvenance, query, draft.LocatorIds.ToArray(), name,
            draft.Purpose, tags, draft.Selection, draft.HasOrigin ? draft.Origin : null, draft.Seed,
            draft.RequiredAtRuntime);
    }

    private static RouteVisualizationProfile BuildCandidateVisualizationUx2NavE(
        StudioUx2NavEVisualizationDraft draft) => new(draft.Original.Id,
        draft.Original.SchemaVersion, draft.Original.Provenance, draft.ShowSegments,
        draft.ShowDirection, draft.ShowCosts, draft.ShowActions, draft.ShowBrokenLinks,
        draft.ShowNavigationProjection, draft.ShowTiming, draft.CostPerMeter,
        draft.RequiredAtRuntime);

    private static IReadOnlyList<StudioUx2NavEDiagnostic> ValidateLocatorDraftUx2NavE(
        StudioUx2NavEDraft draft, AreaDefinition area, ProjectDefinition project)
    {
        var result = new List<StudioUx2NavEDiagnostic>();
        if (draft.Locator is { } locator)
        {
            var path = $"zones/{draft.AreaDisplayName}/ancres/{HumanUx2NavE(locator.Name, "Ancre")}";
            if (string.IsNullOrWhiteSpace(locator.Name))
                result.Add(new("studio.locator.name_required", path, "Nommez l’ancre."));
            if (!InsideAreaUx2NavE(area, locator.Position))
                result.Add(new("studio.locator.out_of_bounds", path,
                    "La position de l’ancre doit rester dans les limites de la zone."));
            if (locator.Tags.Any(string.IsNullOrWhiteSpace) ||
                locator.Tags.Distinct(StringComparer.OrdinalIgnoreCase).Count() != locator.Tags.Count)
                result.Add(new("studio.locator.tags_invalid", path,
                    "Les tags doivent être nommés et uniques."));
            if (locator.ExplicitOverrides.Any(item => string.IsNullOrWhiteSpace(item.Key)) ||
                locator.ExplicitOverrides.Select(item => item.Key.Trim())
                    .Distinct(StringComparer.Ordinal).Count() != locator.ExplicitOverrides.Count)
                result.Add(new("studio.locator.overrides_invalid", path,
                    "Les clés d’override doivent être nommées et uniques."));
            if (locator.ExplicitOverrides.Any(item => item.IsProtected &&
                    !ProtectedOverrideMatchesUx2NavE(locator.Original, item)))
                result.Add(new("studio.locator.override_protected", path,
                    "Les overrides techniques protégés doivent rester inchangés."));
            if ((locator.Original?.ExplicitOverrides ?? new Dictionary<string, string>())
                .Where(item => ContainsGuidValueUx2NavE(item.Key) ||
                               ContainsGuidValueUx2NavE(item.Value))
                .Any(item => !locator.ExplicitOverrides.Any(candidate => candidate.IsProtected &&
                    string.Equals(candidate.Key, item.Key, StringComparison.Ordinal) &&
                    string.Equals(candidate.Value, item.Value, StringComparison.Ordinal))))
                result.Add(new("studio.locator.override_protected", path,
                    "Les overrides techniques protégés doivent rester présents et inchangés."));
            if (locator.BlueprintIsProtected &&
                (locator.Original is null || locator.BlueprintId != locator.Original.BlueprintId))
                result.Add(new("studio.locator.blueprint_protected", path,
                    "Le blueprint protégé doit rester inchangé."));
            if (!locator.BlueprintIsProtected && locator.BlueprintId is { } blueprintId &&
                project.Blueprints.All(item => item.Id != blueprintId))
                result.Add(new("studio.locator.blueprint_missing", path,
                    "Choisissez un blueprint présent dans le projet."));
        }
        if (draft.Collection is { } collection)
        {
            var path = $"zones/{draft.AreaDisplayName}/groupes/{HumanUx2NavE(collection.Name, "Groupe")}";
            if (string.IsNullOrWhiteSpace(collection.Name))
                result.Add(new("studio.collection.name_required", path, "Nommez le groupe."));
            if (string.IsNullOrWhiteSpace(collection.Query) && collection.RequiredTags.Count == 0 &&
                collection.LocatorIds.Count == 0)
                result.Add(new("studio.collection.criteria_required", path,
                    "Ajoutez une requête, un tag requis ou au moins une ancre membre."));
            if (collection.LocatorIds.Distinct().Count() != collection.LocatorIds.Count)
                result.Add(new("studio.collection.members_duplicate", path,
                    "Une même ancre ne peut apparaître qu’une fois dans le groupe."));
            var knownIds = area.Navigation.Waypoints.Locators.Select(item => item.Id).ToHashSet();
            if (draft.Locator is { IsNew: true } newLocator)
                knownIds.Add(newLocator.LocatorId);
            if (collection.LocatorIds.Any(item => !knownIds.Contains(item)))
                result.Add(new("studio.collection.member_missing", path,
                    "Une ancre membre n’existe plus. Rechargez la liste."));
            if (collection.RequiredTags.Any(string.IsNullOrWhiteSpace) ||
                collection.RequiredTags.Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                collection.RequiredTags.Count)
                result.Add(new("studio.collection.tags_invalid", path,
                    "Les tags requis doivent être nommés et uniques."));
            if (collection.Selection == LocatorSelectionMode.Nearest && !collection.HasOrigin)
                result.Add(new("studio.collection.origin_required", path,
                    "Le mode le plus proche exige un point d’origine."));
            if (collection.HasOrigin && !InsideAreaUx2NavE(area, collection.Origin))
                result.Add(new("studio.collection.origin_out_of_bounds", path,
                    "Le point d’origine doit rester dans la zone."));
        }
        var view = draft.Visualization;
        if (!double.IsFinite(view.CostPerMeter) || view.CostPerMeter is <= 0 or > 10_000)
            result.Add(new("studio.route_visualization.cost_invalid",
                $"zones/{draft.AreaDisplayName}/visualisation",
                "Le coût par mètre doit être supérieur à zéro et au plus égal à 10 000."));
        if (!(view.ShowSegments || view.ShowDirection || view.ShowCosts || view.ShowActions ||
              view.ShowBrokenLinks || view.ShowNavigationProjection || view.ShowTiming))
            result.Add(new("studio.route_visualization.layers_required",
                $"zones/{draft.AreaDisplayName}/visualisation",
                "Conservez au moins une couche de visualisation active."));
        return result;
    }

    private static IReadOnlyList<string> BuildLocatorDiffUx2NavE(AreaDefinition sourceArea,
        GenericLocator? locator, AnchorCollection? collection,
        RouteVisualizationProfile visualization, StudioUx2NavEDraft draft)
    {
        var result = new List<string>();
        if (draft.Locator is { } locatorDraft && locator is not null)
        {
            var original = locatorDraft.Original;
            var label = HumanUx2NavE(locator.Name, "Ancre");
            if (original is null)
                result.Add($"Ancre · créer « {label} » · {HumanPurposeUx2NavE(locator.Purpose)}");
            else
            {
                if (!string.Equals(original.Name, locator.Name, StringComparison.Ordinal))
                    result.Add($"Ancre · nom « {HumanUx2NavE(original.Name, "Ancre")} » → « {label} »");
                if (original.Position != locator.Position)
                    result.Add($"{label} · position {PointTextUx2NavE(original.Position)} → {PointTextUx2NavE(locator.Position)}");
                if (original.Purpose != locator.Purpose)
                    result.Add($"{label} · rôle {HumanPurposeUx2NavE(original.Purpose)} → {HumanPurposeUx2NavE(locator.Purpose)}");
                if (!original.Tags.SequenceEqual(locator.Tags))
                    result.Add($"{label} · tags {original.Tags.Count} → {locator.Tags.Count}");
                if (!string.Equals(original.AssetId, locator.AssetId, StringComparison.Ordinal))
                    result.Add($"{label} · ressource {(locator.AssetId is null ? "retirée" : "mise à jour")}");
                if (original.BlueprintId != locator.BlueprintId)
                    result.Add($"{label} · blueprint {(locator.BlueprintId is null ? "dissocié" : "associé")}");
                if (!OverridesEqualUx2NavE(original.ExplicitOverrides, locator.ExplicitOverrides))
                    result.Add($"{label} · overrides {(original.ExplicitOverrides?.Count ?? 0)} → {(locator.ExplicitOverrides?.Count ?? 0)}");
                if (original.RequiredAtRuntime != locator.RequiredAtRuntime)
                    result.Add($"{label} · exigence runtime {(locator.RequiredAtRuntime ? "activée" : "désactivée")}");
            }
        }
        if (draft.Collection is { } collectionDraft && collection is not null)
        {
            var original = collectionDraft.Original;
            var label = HumanUx2NavE(collection.Name, "Groupe");
            if (original is null)
                result.Add($"Groupe · créer « {label} » · {collection.LocatorIds.Count} membre(s)");
            else
            {
                if (!string.Equals(original.Name, collection.Name, StringComparison.Ordinal))
                    result.Add($"Groupe · nom « {HumanUx2NavE(original.Name, "Groupe")} » → « {label} »");
                if (!string.Equals(original.Query, collection.Query, StringComparison.Ordinal) ||
                    original.Purpose != collection.Purpose ||
                    !(original.RequiredTags ?? []).SequenceEqual(collection.RequiredTags ?? []))
                    result.Add($"{label} · filtres de requête modifiés");
                if (!original.LocatorIds.SequenceEqual(collection.LocatorIds))
                    result.Add($"{label} · ordre des membres modifié · {collection.LocatorIds.Count} membre(s)");
                if (original.Selection != collection.Selection || original.Origin != collection.Origin ||
                    original.Seed != collection.Seed)
                    result.Add($"{label} · résolution {HumanSelectionUx2NavE(original.Selection)} → {HumanSelectionUx2NavE(collection.Selection)}");
                if (original.RequiredAtRuntime != collection.RequiredAtRuntime)
                    result.Add($"{label} · exigence runtime {(collection.RequiredAtRuntime ? "activée" : "désactivée")}");
            }
        }
        if (sourceArea.Navigation.Waypoints.RouteVisualization != visualization)
        {
            var before = sourceArea.Navigation.Waypoints.RouteVisualization;
            if (before.CostPerMeter != visualization.CostPerMeter)
                result.Add($"Visualisation · coût par mètre {before.CostPerMeter:0.##} → {visualization.CostPerMeter:0.##}");
            var beforeCount = EnabledLayerCountUx2NavE(before);
            var afterCount = EnabledLayerCountUx2NavE(visualization);
            if (beforeCount != afterCount || before != visualization)
                result.Add($"Visualisation · {beforeCount} → {afterCount} couche(s) active(s)");
        }
        return result;
    }

    private static IReadOnlySet<Guid> BuildProjectedWaypointIdsUx2NavE(ProjectDefinition project,
        AreaDefinition area, out bool available)
    {
        try
        {
            var agent = area.Navigation.AgentProfiles.First();
            var surface = NavigationSurfaceBuilder.Build(project, area, agent);
            available = true;
            return area.Navigation.Waypoints.PatrolRoutes.SelectMany(item => item.Points)
                .Where(item => surface.IsWalkable(surface.CellAt(item.Position)))
                .Select(item => item.Id).ToHashSet();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           OverflowException)
        {
            available = false;
            return new HashSet<Guid>();
        }
    }

    private static IReadOnlyList<StudioUx2NavERouteDiagnostic> BuildRouteDiagnosticsUx2NavE(
        WaypointAuthoringProfile profile, IReadOnlySet<Guid> projected)
    {
        var known = profile.PatrolRoutes.SelectMany(item => item.Points).Select(item => item.Id)
            .Concat(profile.Waypoints.Select(item => item.Id))
            .Concat(profile.Locators.Select(item => item.Id)).ToHashSet();
        var result = new List<StudioUx2NavERouteDiagnostic>();
        for (var routeIndex = 0; routeIndex < profile.PatrolRoutes.Count; routeIndex++)
        {
            var route = profile.PatrolRoutes[routeIndex];
            var diagnostics = WaypointRouteService.AnalyzeRoute(route, projected, known,
                profile.RouteVisualization.CostPerMeter);
            foreach (var item in diagnostics)
            {
                var from = route.Points.ToList().FindIndex(point => point.Id == item.FromId) + 1;
                var to = route.Points.ToList().FindIndex(point => point.Id == item.ToId) + 1;
                result.Add(new($"Patrouille {routeIndex + 1}", from, to, item.Direction < 0,
                    item.Distance, item.Cost, item.TravelSeconds, item.Action ?? string.Empty,
                    item.BrokenLink, item.NavProjected));
            }
        }
        return result;
    }

    private static StudioNavigationOverlaySnapshot BuildLocatorOverlayUx2NavE(
        WaypointAuthoringProfile profile, Guid? selectedLocatorId,
        IReadOnlyList<Guid> selectedCollectionMembers, IReadOnlySet<Guid> projected)
    {
        var selected = selectedCollectionMembers.ToHashSet();
        var markers = profile.Locators.Select(item => new StudioNavigationOverlayMarker(
            $"locator:{item.Id:N}", $"{HumanUx2NavE(item.Name, "Ancre")} · {HumanPurposeUx2NavE(item.Purpose)}",
            StudioNavigationOverlaySemantic.Anchor, PointUx2NavE(item.Position),
            item.Id == selectedLocatorId || selected.Contains(item.Id))).ToList();
        var segments = new List<StudioNavigationOverlaySegment>();
        var view = profile.RouteVisualization;
        if (view.ShowSegments)
        {
            var known = profile.PatrolRoutes.SelectMany(item => item.Points).Select(item => item.Id)
                .Concat(profile.Locators.Select(item => item.Id)).ToHashSet();
            foreach (var route in profile.PatrolRoutes)
            {
                var analyzed = WaypointRouteService.AnalyzeRoute(route, projected, known,
                    view.CostPerMeter);
                for (var index = 0; index < analyzed.Count; index++)
                {
                    var item = analyzed[index];
                    if (item.BrokenLink && !view.ShowBrokenLinks)
                    {
                        continue;
                    }
                    var from = route.Points.Single(point => point.Id == item.FromId);
                    var to = route.Points.Single(point => point.Id == item.ToId);
                    var details = new List<string>();
                    if (view.ShowCosts) details.Add($"coût {item.Cost:0.##}");
                    if (view.ShowTiming) details.Add($"{item.TravelSeconds:0.##} s");
                    if (view.ShowActions && !string.IsNullOrWhiteSpace(item.Action))
                        details.Add($"action {HumanUx2NavE(item.Action, "configurée")}");
                    if (view.ShowNavigationProjection)
                        details.Add(item.NavProjected ? "sur navigation" : "hors navigation");
                    var label = details.Count == 0 ? "Segment de patrouille" : string.Join(" · ", details);
                    segments.Add(new($"route:{route.Id:N}:segment:{index}", label,
                        StudioNavigationOverlaySemantic.Route, PointUx2NavE(from.Position),
                        PointUx2NavE(to.Position), view.ShowDirection,
                        item.NavProjected && view.ShowNavigationProjection, item.BrokenLink));
                }
            }
        }
        return new(markers, segments, []);
    }

    private static bool InsideAreaUx2NavE(AreaDefinition area, Vector3Value position) =>
        float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(position.Z) &&
        MathF.Abs(position.X) <= area.Dimensions.Width / 2f &&
        MathF.Abs(position.Z) <= area.Dimensions.Depth / 2f;

    private static int EnabledLayerCountUx2NavE(RouteVisualizationProfile profile) =>
        new[] { profile.ShowSegments, profile.ShowDirection, profile.ShowCosts, profile.ShowActions,
            profile.ShowBrokenLinks, profile.ShowNavigationProjection, profile.ShowTiming }
        .Count(item => item);

    private static bool OverridesEqualUx2NavE(IReadOnlyDictionary<string, string>? left,
        IReadOnlyDictionary<string, string>? right)
    {
        left ??= new Dictionary<string, string>();
        right ??= new Dictionary<string, string>();
        return left.Count == right.Count && left.All(item =>
            right.TryGetValue(item.Key, out var value) && string.Equals(item.Value, value,
                StringComparison.Ordinal));
    }

    private static bool ProtectedOverrideMatchesUx2NavE(GenericLocator? original,
        StudioUx2NavEOverrideDraft item) => original?.ExplicitOverrides is { } overrides &&
        overrides.TryGetValue(item.Key, out var value) &&
        string.Equals(value, item.Value, StringComparison.Ordinal);

    private static StudioUx2NavEPreview FailedLocatorPreviewUx2NavE(string message,
        IReadOnlyList<StudioUx2NavEDiagnostic>? diagnostics = null) => new(false, message,
        "Aucun aperçu disponible.", false, [], diagnostics ?? [],
        StudioNavigationOverlaySnapshot.Empty, [], [], false, 0);

    private static string LocatorSourceFingerprintUx2NavE(AreaDefinition area) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(AreaJsonSerializer.Serialize(area))));

    private static string LocatorDraftFingerprintUx2NavE(StudioUx2NavEDraft draft) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(draft))));

    private static StudioNavigationPoint PointUx2NavE(Vector3Value point) =>
        new(point.X, point.Y + .12, point.Z);

    private static string PointTextUx2NavE(Vector3Value point) =>
        $"{point.X.ToString("0.##", CultureInfo.CurrentCulture)}, " +
        $"{point.Y.ToString("0.##", CultureInfo.CurrentCulture)}, " +
        point.Z.ToString("0.##", CultureInfo.CurrentCulture);

    private static string? EmptyToNullUx2NavE(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string HumanDiagnosticUx2NavE(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "La modification a été refusée.";
        var separator = message.IndexOf(": ", StringComparison.Ordinal);
        var value = separator >= 0 ? message[(separator + 2)..] : message;
        value = System.Text.RegularExpressions.Regex.Replace(value,
            @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b",
            "élément protégé", System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                               System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return value.Replace("UUID", "identité", StringComparison.OrdinalIgnoreCase);
    }

    private static string HumanUx2NavE(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value) || ContainsGuidValueUx2NavE(value)) return fallback;
        return value.Trim();
    }

    private static string HumanPurposeUx2NavE(LocatorPurpose purpose) => purpose switch
    {
        LocatorPurpose.Audio => "audio",
        LocatorPurpose.Vfx => "effet visuel",
        LocatorPurpose.Spawn => "apparition",
        LocatorPurpose.Objective => "objectif",
        _ => "gameplay",
    };

    private static string HumanSelectionUx2NavE(LocatorSelectionMode selection) => selection switch
    {
        LocatorSelectionMode.Nearest => "plus proche",
        LocatorSelectionMode.RandomDeterministic => "aléatoire déterministe",
        _ => "ordre stable",
    };

    private static bool ContainsTechnicalIdentifierUx2NavE(string text) =>
        text.Contains("UUID", StringComparison.OrdinalIgnoreCase) ||
        ContainsGuidValueUx2NavE(text);

    private static bool ContainsGuidValueUx2NavE(string? text) =>
        !string.IsNullOrWhiteSpace(text) && System.Text.RegularExpressions.Regex.IsMatch(text,
            @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    internal ProjectDefinition? CreateUx2NavEEvidenceProject()
    {
        var project = CreateUx2NavDEvidenceProject();
        if (project is null)
        {
            return null;
        }
        var area = project.Areas.Single(item => item.Slug == "clairiere-navigation");
        var profile = area.Navigation.Waypoints;
        var blueprint = project.Blueprints.FirstOrDefault();
        var vfx = new GenericLocator(Guid.Parse("2e000000-0000-4000-8000-000000000011"), 1,
            Ux2NavEEvidenceProvenance, "Halo du sanctuaire", new(-3, 0, 2), LocatorPurpose.Vfx,
            ["rituel", "lumière"],
            new ArrivalBehavior(ArrivalFacingMode.Direction, 45, null, "pulse", .25,
                "observe", "locator.arrived"), "vfx:sanctuary-halo", blueprint?.Id,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["intensité"] = "douce",
                ["couleur"] = "ambre",
                ["liaison protégée"] = "2e000000-0000-4000-8000-000000000099",
            }, true);
        var audio = new GenericLocator(Guid.Parse("2e000000-0000-4000-8000-000000000012"), 1,
            Ux2NavEEvidenceProvenance, "Cloche du sanctuaire", new(2, 0, 3), LocatorPurpose.Audio,
            ["rituel", "son"], null, "audio:sanctuary-bell");
        var objective = new GenericLocator(Guid.Parse("2e000000-0000-4000-8000-000000000013"), 1,
            Ux2NavEEvidenceProvenance, "Objectif du sanctuaire", new(4, 0, -1),
            LocatorPurpose.Objective, ["rituel", "quête"], null);
        var locators = profile.Locators.Where(item =>
                item.Id != vfx.Id && item.Id != audio.Id && item.Id != objective.Id)
            .Concat([vfx, audio, objective]).ToArray();
        var collection = new AnchorCollection(
            Guid.Parse("2e000000-0000-4000-8000-000000000021"), 1,
            Ux2NavEEvidenceProvenance, "rituel", [vfx.Id, audio.Id, objective.Id],
            "Ancres du sanctuaire", null, ["rituel"], LocatorSelectionMode.Stable,
            Vector3Value.Zero, 2026, true);
        var collections = profile.Collections.Where(item => item.Id != collection.Id)
            .Append(collection).ToArray();
        var visualization = new RouteVisualizationProfile(
            Guid.Parse("2e000000-0000-4000-8000-000000000031"), 1,
            Ux2NavEEvidenceProvenance, true, true, true, true, true, true, true, 1.25, true);
        var authored = new WaypointAuthoringProfile(profile.Id, profile.SchemaVersion,
            profile.Provenance, area.Id, profile.RequiredAtRuntime, profile.Waypoints,
            profile.PatrolRoutes, locators, collections, visualization);
        var replacement = area.WithNavigation(area.Navigation.WithWaypoints(authored));
        var areas = project.Areas.Select(item => item.Id == area.Id ? replacement : item).ToArray();
        return new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
            areas, project.Dialogues, project.Quests);
    }

    internal StudioUx2NavESmokeResult RunUx2NavEContractSmoke()
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
        var sourceLocatorApproval = CaptureLocatorApprovalUx2NavE();
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-nav-e-contract-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var fixture = CreateUx2NavEEvidenceProject();
            if (fixture is null || !ProjectSession.Create(tempRoot, fixture).Succeeded ||
                !editing.Open(tempRoot).Succeeded)
            {
                return FailedUx2NavESmoke();
            }
            var areaChoice = editing.LocatorAreaChoicesUx2NavE.Single(item => item.IsEvidence);
            var locatorChoice = editing.LocatorChoicesUx2NavE(areaChoice.AreaId)
                .First(item => item.IsEvidence);
            var collectionChoice = editing.LocatorCollectionChoicesUx2NavE(areaChoice.AreaId)
                .Single(item => item.IsEvidence);
            var explicitSelection = areaChoice.AreaId.Value != Guid.Empty &&
                                    locatorChoice.LocatorId != Guid.Empty &&
                                    collectionChoice.CollectionId != Guid.Empty;
            var labels = areaChoice.Label + " " + locatorChoice.Label + " " + collectionChoice.Label;
            var humanLabels = !ContainsTechnicalIdentifierUx2NavE(labels);

            var newLocator = editing.CreateNewLocatorDraftUx2NavE(areaChoice.AreaId)! with
            {
                Name = "Souffle du jardin",
                Purpose = LocatorPurpose.Audio,
                Tags = ["jardin", "ambiance"],
                AssetId = "audio:garden-breath",
            };
            var visualDraft = editing.CreateRouteVisualizationDraftUx2NavE(areaChoice.AreaId)!;
            var newLocatorDraft = editing.ComposeLocatorDraftUx2NavE(areaChoice.AreaId,
                newLocator, null, visualDraft)!;
            var newLocatorPreview = editing.PreviewLocatorsUx2NavE(newLocatorDraft);
            var locatorCreation = newLocatorPreview.Succeeded && newLocatorPreview.CanApply;

            var locatorDraft = editing.CreateLocatorDraftUx2NavE(areaChoice.AreaId,
                locatorChoice.LocatorId)!;
            var invalidBounds = editing.PreviewLocatorsUx2NavE(editing.ComposeLocatorDraftUx2NavE(
                areaChoice.AreaId, locatorDraft with { Position = new(100_000, 0, 100_000) }, null,
                editing.CreateRouteVisualizationDraftUx2NavE(areaChoice.AreaId))!);
            var invalidOverrides = editing.PreviewLocatorsUx2NavE(editing.ComposeLocatorDraftUx2NavE(
                areaChoice.AreaId, locatorDraft with
                {
                    ExplicitOverrides = [new("teinte", "ambre"), new("teinte", "or")],
                }, null, editing.CreateRouteVisualizationDraftUx2NavE(areaChoice.AreaId))!);
            var locatorValidation = !invalidBounds.Succeeded && !invalidOverrides.Succeeded;

            var newCollection = editing.CreateNewLocatorCollectionDraftUx2NavE(areaChoice.AreaId)! with
            {
                Name = "Objectifs proches",
                Query = "objectif",
                Purpose = LocatorPurpose.Objective,
                Selection = LocatorSelectionMode.Nearest,
                HasOrigin = true,
                Origin = Vector3Value.Zero,
            };
            var newCollectionPreview = editing.PreviewLocatorsUx2NavE(
                editing.ComposeLocatorDraftUx2NavE(areaChoice.AreaId, null, newCollection,
                    editing.CreateRouteVisualizationDraftUx2NavE(areaChoice.AreaId))!);
            var collectionCreation = newCollectionPreview.Succeeded && newCollectionPreview.CanApply;
            var invalidCollection = editing.CreateNewLocatorCollectionDraftUx2NavE(areaChoice.AreaId)! with
            {
                Name = "Groupe invalide",
                Query = string.Empty,
                RequiredTags = [],
                LocatorIds = [],
                Selection = LocatorSelectionMode.Nearest,
                HasOrigin = false,
            };
            var invalidCollectionPreview = editing.PreviewLocatorsUx2NavE(
                editing.ComposeLocatorDraftUx2NavE(areaChoice.AreaId, null, invalidCollection,
                    editing.CreateRouteVisualizationDraftUx2NavE(areaChoice.AreaId))!);
            var collectionValidation = !invalidCollectionPreview.Succeeded;

            var canonicalSource = editing.Project!;
            var sourceArea = canonicalSource.Areas.Single(item => item.Id == areaChoice.AreaId);
            var sourceNeighbor = canonicalSource.Areas.Single(item => item.Id != areaChoice.AreaId);
            var sourceLocator = sourceArea.Navigation.Waypoints.Locators.Single(item =>
                item.Id == locatorChoice.LocatorId);
            var sourceCollection = sourceArea.Navigation.Waypoints.Collections.Single(item =>
                item.Id == collectionChoice.CollectionId);
            var sourceProfile = sourceArea.Navigation.Waypoints;
            var orderedMembers = sourceCollection.LocatorIds.Reverse().ToArray();
            var stable = sourceCollection with
            {
                Query = string.Empty,
                RequiredTags = [],
                Purpose = null,
                LocatorIds = orderedMembers,
                Selection = LocatorSelectionMode.Stable,
            };
            var stableResolved = WaypointRouteService.ResolveCollection(stable,
                sourceProfile.Locators);
            var nearest = WaypointRouteService.ResolveCollection(stable with
            {
                Selection = LocatorSelectionMode.Nearest,
                Origin = sourceProfile.Locators.Single(item => item.Id == orderedMembers[^1]).Position,
            }, sourceProfile.Locators);
            var randomA = WaypointRouteService.ResolveCollection(stable with
            {
                Selection = LocatorSelectionMode.RandomDeterministic,
                Seed = int.MinValue,
            }, sourceProfile.Locators);
            var randomB = WaypointRouteService.ResolveCollection(stable with
            {
                Selection = LocatorSelectionMode.RandomDeterministic,
                Seed = int.MinValue,
            }, sourceProfile.Locators);
            var stableMemberOrder = stableResolved.Select(item => item.Id).SequenceEqual(orderedMembers);
            var collectionModes = nearest.Count == 1 && nearest[0].Id == orderedMembers[^1] &&
                                  randomA.Count == 1 && randomA.Select(item => item.Id)
                                      .SequenceEqual(randomB.Select(item => item.Id));

            locatorDraft = editing.CreateLocatorDraftUx2NavE(areaChoice.AreaId,
                locatorChoice.LocatorId)!;
            var collectionDraft = editing.CreateLocatorCollectionDraftUx2NavE(areaChoice.AreaId,
                collectionChoice.CollectionId)!;
            visualDraft = editing.CreateRouteVisualizationDraftUx2NavE(areaChoice.AreaId)!;
            var configuredLocator = locatorDraft with
            {
                Name = "Halo du sanctuaire renforcé",
                Position = new(-2.5f, 0, 2.5f),
                Tags = ["rituel", "lumière", "validation"],
                AssetId = "vfx:sanctuary-halo-strong",
                ExplicitOverrides = locatorDraft.ExplicitOverrides
                    .Append(new("rythme", "lent")).ToArray(),
            };
            var configuredCollection = collectionDraft with
            {
                Query = string.Empty,
                RequiredTags = [],
                Purpose = null,
                LocatorIds = orderedMembers,
                Selection = LocatorSelectionMode.RandomDeterministic,
                HasOrigin = true,
                Origin = Vector3Value.Zero,
                Seed = 4242,
            };
            var configuredVisualization = visualDraft with
            {
                ShowCosts = !visualDraft.ShowCosts,
                ShowTiming = true,
                CostPerMeter = 2.5,
            };
            var configured = editing.ComposeLocatorDraftUx2NavE(areaChoice.AreaId,
                configuredLocator, configuredCollection, configuredVisualization)!;
            var historyBefore = editing.History!;
            var projectBefore = editing.Project;
            var revisionBefore = historyBefore.State.Revision;
            var journalBefore = historyBefore.Journal.ToArray();
            var preview = editing.PreviewLocatorsUx2NavE(configured);
            var dryRun = preview.Succeeded && preview.CanApply &&
                         ReferenceEquals(projectBefore, editing.Project) &&
                         ReferenceEquals(historyBefore, editing.History) &&
                         historyBefore.State.Revision == revisionBefore &&
                         historyBefore.Journal.SequenceEqual(journalBefore);
            var routeVisualization = preview.RouteDiagnostics.Count > 0 &&
                                     preview.Overlay.Markers.Count >= sourceProfile.Locators.Count &&
                                     preview.Overlay.Segments.Count > 0;
            var realProjection = preview.ProjectionAvailable && preview.ProjectedPointCount > 0 &&
                                 preview.RouteDiagnostics.Any(item => item.NavigationProjected);

            var staleSource = editing.PreviewLocatorsUx2NavE(configured with
            {
                SourceFingerprint = "stale",
            });
            var staleSourceBlocked = !staleSource.Succeeded;

            locatorDraft = editing.CreateLocatorDraftUx2NavE(areaChoice.AreaId,
                locatorChoice.LocatorId)!;
            collectionDraft = editing.CreateLocatorCollectionDraftUx2NavE(areaChoice.AreaId,
                collectionChoice.CollectionId)!;
            var staleDraft = editing.ComposeLocatorDraftUx2NavE(areaChoice.AreaId, locatorDraft,
                collectionDraft, editing.CreateRouteVisualizationDraftUx2NavE(areaChoice.AreaId))!;
            var neighborChanged = ApplyUx2NavENeighborMutation(editing, sourceNeighbor);
            var stalePreview = editing.PreviewLocatorsUx2NavE(staleDraft);
            var staleRevisionBlocked = neighborChanged && !stalePreview.Succeeded &&
                                       editing.Undo().Succeeded;

            locatorDraft = editing.CreateLocatorDraftUx2NavE(areaChoice.AreaId,
                locatorChoice.LocatorId)!;
            collectionDraft = editing.CreateLocatorCollectionDraftUx2NavE(areaChoice.AreaId,
                collectionChoice.CollectionId)!;
            configured = editing.ComposeLocatorDraftUx2NavE(areaChoice.AreaId,
                locatorDraft with { Name = "Halo prêt à appliquer" }, collectionDraft,
                editing.CreateRouteVisualizationDraftUx2NavE(areaChoice.AreaId))!;
            var approvalPreview = editing.PreviewLocatorsUx2NavE(configured);
            neighborChanged = ApplyUx2NavENeighborMutation(editing, sourceNeighbor);
            var staleApply = editing.ApplyApprovedLocatorsUx2NavE();
            var staleApprovalBlocked = approvalPreview.Succeeded && neighborChanged &&
                                       !staleApply.Succeeded && editing.Undo().Succeeded;

            canonicalSource = editing.Project!;
            sourceArea = canonicalSource.Areas.Single(item => item.Id == areaChoice.AreaId);
            sourceNeighbor = canonicalSource.Areas.Single(item => item.Id != areaChoice.AreaId);
            sourceLocator = sourceArea.Navigation.Waypoints.Locators.Single(item =>
                item.Id == locatorChoice.LocatorId);
            sourceCollection = sourceArea.Navigation.Waypoints.Collections.Single(item =>
                item.Id == collectionChoice.CollectionId);
            sourceProfile = sourceArea.Navigation.Waypoints;
            orderedMembers = sourceCollection.LocatorIds.Reverse().ToArray();
            locatorDraft = editing.CreateLocatorDraftUx2NavE(areaChoice.AreaId,
                locatorChoice.LocatorId)!;
            collectionDraft = editing.CreateLocatorCollectionDraftUx2NavE(areaChoice.AreaId,
                collectionChoice.CollectionId)!;
            visualDraft = editing.CreateRouteVisualizationDraftUx2NavE(areaChoice.AreaId)!;
            configuredLocator = locatorDraft with
            {
                Name = "Halo du sanctuaire renforcé",
                Position = new(-2.5f, 0, 2.5f),
                Tags = ["rituel", "lumière", "validation"],
                AssetId = "vfx:sanctuary-halo-strong",
                ExplicitOverrides = locatorDraft.ExplicitOverrides
                    .Append(new("rythme", "lent")).ToArray(),
            };
            configuredCollection = collectionDraft with
            {
                Query = string.Empty,
                RequiredTags = [],
                Purpose = null,
                LocatorIds = orderedMembers,
                Selection = LocatorSelectionMode.RandomDeterministic,
                HasOrigin = true,
                Origin = Vector3Value.Zero,
                Seed = 4242,
            };
            configuredVisualization = visualDraft with
            {
                ShowCosts = !visualDraft.ShowCosts,
                ShowTiming = true,
                CostPerMeter = 2.5,
            };
            configured = editing.ComposeLocatorDraftUx2NavE(areaChoice.AreaId,
                configuredLocator, configuredCollection, configuredVisualization)!;
            var freshPreview = editing.PreviewLocatorsUx2NavE(configured);
            var applied = editing.ApplyApprovedLocatorsUx2NavE();
            var appliedProject = editing.Project!;
            var appliedArea = appliedProject.Areas.Single(item => item.Id == areaChoice.AreaId);
            var appliedProfile = appliedArea.Navigation.Waypoints;
            var appliedLocator = appliedProfile.Locators.Single(item => item.Id == sourceLocator.Id);
            var appliedCollection = appliedProfile.Collections.Single(item => item.Id == sourceCollection.Id);
            stableMemberOrder &= appliedCollection.LocatorIds.SequenceEqual(orderedMembers);
            collectionModes &= appliedCollection.Selection == LocatorSelectionMode.RandomDeterministic &&
                               appliedCollection.Seed == 4242;
            var appliedOverrides = appliedLocator.ExplicitOverrides;
            var blueprintAndOverrides = appliedLocator.BlueprintId == sourceLocator.BlueprintId &&
                                        appliedOverrides is not null &&
                                        appliedOverrides.ContainsKey("rythme") &&
                                        appliedOverrides.TryGetValue("liaison protégée",
                                            out var protectedValue) &&
                                        string.Equals(protectedValue,
                                            "2e000000-0000-4000-8000-000000000099",
                                            StringComparison.Ordinal);
            var arrivalPreserved = appliedLocator.Arrival == sourceLocator.Arrival;
            var canonicalCommand = freshPreview.Succeeded && applied.Succeeded &&
                                   editing.History!.Journal.LastOrDefault()?.CommandName ==
                                   "set_navigation_topology";
            var waypointNeighborsPreserved = sourceProfile.Waypoints.SequenceEqual(appliedProfile.Waypoints) &&
                                              sourceProfile.PatrolRoutes.SequenceEqual(appliedProfile.PatrolRoutes);
            var locatorNeighborsPreserved = sourceProfile.Locators.Where(item => item.Id != sourceLocator.Id)
                .SequenceEqual(appliedProfile.Locators.Where(item => item.Id != sourceLocator.Id));
            var collectionNeighborsPreserved = sourceProfile.Collections
                .Where(item => item.Id != sourceCollection.Id)
                .SequenceEqual(appliedProfile.Collections.Where(item => item.Id != sourceCollection.Id));
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
                                            LocatorNeighborsMatchUx2NavE(sourceArea, appliedArea) &&
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
            var humanText = !ContainsTechnicalIdentifierUx2NavE(labels + " " +
                            freshPreview.Summary + " " + string.Join(' ', freshPreview.Diff));
            var sourceUntouched = ReferenceEquals(sourceProject, Project) &&
                                  ReferenceEquals(sourceHistory, History) &&
                                  History?.State.Revision == sourceRevision &&
                                  (History?.Journal ?? []).SequenceEqual(sourceJournal) &&
                                  IsDirty == sourceDirty && History?.CanUndo == sourceUndo &&
                                  History?.CanRedo == sourceRedo && ActiveAreaId == sourceAreaId &&
                                  SelectedEntityIds.SequenceEqual(sourceSelection) &&
                                  SelectedEntityId == sourceSelected &&
                                  CaptureNavigationApprovalUx2NavB() == sourceNavigationApproval &&
                                  CaptureWaypointApprovalUx2NavC() == sourceWaypointApproval &&
                                  CapturePatrolApprovalUx2NavD() == sourcePatrolApproval &&
                                  CaptureLocatorApprovalUx2NavE() == sourceLocatorApproval;
            var succeeded = explicitSelection && humanLabels && locatorCreation && locatorValidation &&
                            blueprintAndOverrides && arrivalPreserved && collectionCreation &&
                            stableMemberOrder && collectionModes && collectionValidation &&
                            routeVisualization && realProjection && dryRun && staleRevisionBlocked &&
                            staleSourceBlocked && staleApprovalBlocked && canonicalCommand && undoRedo &&
                            persistence && waypointNeighborsPreserved && locatorNeighborsPreserved &&
                            collectionNeighborsPreserved && navigationNeighborsPreserved &&
                            projectNeighborsPreserved && sourceUntouched && humanText;
            return new(succeeded, explicitSelection, humanLabels, locatorCreation,
                locatorValidation, blueprintAndOverrides, arrivalPreserved, collectionCreation,
                stableMemberOrder, collectionModes, collectionValidation, routeVisualization,
                realProjection, dryRun, staleRevisionBlocked, staleSourceBlocked,
                staleApprovalBlocked, canonicalCommand, undoRedo, persistence,
                waypointNeighborsPreserved, locatorNeighborsPreserved,
                collectionNeighborsPreserved, navigationNeighborsPreserved,
                projectNeighborsPreserved, sourceUntouched, humanText);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_NAV_E_CONTRACT_FAILED type={exception.GetType().Name} message={exception.Message}");
            return FailedUx2NavESmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            DeleteUx2NavETemporaryWorkspace(tempRoot);
        }
    }

    private static bool ApplyUx2NavENeighborMutation(StudioSessionController controller,
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
            navigation.OffMeshLinks, "studio:ux2-nav-e:neighbor", navigation.SchemaVersion,
            navigation.Anchors, navigation.Waypoints);
        var result = controller._setNavigationTopologyUx2NavE.Handle(history.State,
            new SetNavigationTopologyCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                [new(area.Id, candidate)], [], null, false));
        if (!result.Succeeded)
        {
            return false;
        }
        history.Record(result);
        return true;
    }

    internal static bool DeleteUx2NavETemporaryWorkspace(string? directory)
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
                !leaf.StartsWith("openrpg-forge-studio-ux2-nav-e-", StringComparison.Ordinal))
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

    internal static bool LocatorNeighborsMatchUx2NavE(AreaDefinition source,
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

    private static StudioUx2NavESmokeResult FailedUx2NavESmoke() => new(
        Succeeded: false,
        ExplicitSelection: false,
        HumanLabels: false,
        LocatorCreation: false,
        LocatorValidation: false,
        BlueprintAndOverrides: false,
        ArrivalPreserved: false,
        CollectionCreation: false,
        StableMemberOrder: false,
        CollectionModes: false,
        CollectionValidation: false,
        RouteVisualization: false,
        RealProjection: false,
        DryRun: false,
        StaleRevisionBlocked: false,
        StaleSourceBlocked: false,
        StaleApprovalBlocked: false,
        CanonicalCommand: false,
        UndoRedo: false,
        Persistence: false,
        WaypointNeighborsPreserved: false,
        LocatorNeighborsPreserved: false,
        CollectionNeighborsPreserved: false,
        NavigationNeighborsPreserved: false,
        ProjectNeighborsPreserved: false,
        SourceUntouched: false,
        HumanText: false);
}
