using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Commands;
using Forge.Application.Navigation;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Doors;
using Forge.RpgKit.Domain.Doors;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2NavGProvenance = "studio:ux2-nav-g:door-portals";
    private readonly SetNavigationTopologyHandler _setNavigationTopologyUx2NavG = new(new SystemClock());
    private StudioUx2NavGDraft? _approvedDoorNavigationUx2NavG;
    private ProjectRevision? _approvedDoorNavigationRevisionUx2NavG;
    private string? _approvedDoorNavigationFingerprintUx2NavG;

    internal IReadOnlyList<StudioUx2NavGDoorChoice> DoorNavigationChoicesUx2NavG
    {
        get
        {
            var project = Project;
            if (project is null) return [];
            var labels = NavigationDestinationCatalog.Build(project).Entries
                .Where(item => item.Kind == NavigationDestinationKind.Door)
                .ToDictionary(item => item.Id, item => item.HumanLabel);
            return project.Module.WorldFeatures.Doors
                .Select(profile =>
                {
                    var availability = DoorNavigationAvailabilityUx2NavG(profile);
                    return new StudioUx2NavGDoorChoice(profile.Id,
                        labels.GetValueOrDefault(profile.Id, "Porte protégée"),
                        availability is null, availability ?? "Prête à configurer");
                })
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal IReadOnlyList<StudioUx2NavGDestinationChoice> DoorDestinationChoicesUx2NavG(Guid? sourceDoorId)
    {
        var project = Project;
        if (project is null) return [];
        return NavigationDestinationCatalog.Build(project).Entries
            .Where(item => item.Kind is NavigationDestinationKind.Door or NavigationDestinationKind.Trigger or
                           NavigationDestinationKind.Waypoint or NavigationDestinationKind.SpawnAnchor)
            .Where(item => item.Kind != NavigationDestinationKind.Door || item.Id != sourceDoorId)
            .Select(item => new StudioUx2NavGDestinationChoice(item.Kind, item.Id, item.HumanLabel))
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal IReadOnlyList<StudioUx2NavGArrivalChoice> DoorArrivalChoicesUx2NavG()
    {
        var project = Project;
        if (project is null) return [];
        return NavigationDestinationCatalog.Build(project).Entries
            .Where(item => item.Kind == NavigationDestinationKind.SpawnAnchor &&
                           !string.IsNullOrWhiteSpace(item.ArrivalAnchorKey))
            .Select(item => new StudioUx2NavGArrivalChoice(item.Id, item.ArrivalAnchorKey!, item.HumanLabel))
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal StudioUx2NavGDraft? CreateDoorNavigationDraftUx2NavG(Guid? doorId) =>
        TryCreateDoorNavigationDraftUx2NavG(doorId, out var draft, out _) ? draft : null;

    internal bool TryCreateDoorNavigationDraftUx2NavG(Guid? doorId, out StudioUx2NavGDraft? draft,
        out string message)
    {
        draft = null;
        message = string.Empty;
        var project = Project;
        var history = History;
        if (project is null || history is null)
        {
            message = "Ouvrez un module avant de configurer une porte.";
            return false;
        }
        if (doorId is null)
        {
            message = "Choisissez explicitement une porte.";
            return false;
        }
        var profile = project.Module.WorldFeatures.Doors.SingleOrDefault(item => item.Id == doorId.Value);
        if (profile is null)
        {
            message = "La porte sélectionnée n’existe plus.";
            return false;
        }
        var availability = DoorNavigationAvailabilityUx2NavG(profile);
        if (availability is not null)
        {
            message = availability;
            return false;
        }

        var foundation = profile.Foundation!;
        var portal = foundation.Portal;
        var navigation = foundation.Navigation!;
        var sourceLinks = portal?.DestinationKind == DoorWorldProfile.DestinationKind.Door
            ? foundation.Links.Where(item => item.TargetId == portal.DestinationId).ToArray()
            : [];
        if (sourceLinks.Length > 1)
        {
            message = "Plusieurs liens visent la destination actuelle · corrigez cette ambiguïté avant l’édition.";
            return false;
        }
        var sourceLink = sourceLinks.SingleOrDefault();
        DoorWorldProfile.LinkPolicy? reverseLink = null;
        if (sourceLink is { OneWay: false })
        {
            var target = project.Module.WorldFeatures.Doors.SingleOrDefault(item => item.Id == sourceLink.TargetId);
            var reverse = target?.Foundation?.Links.Where(item => item.TargetId == profile.Id && !item.OneWay)
                .ToArray() ?? [];
            if (reverse.Length > 1)
            {
                message = "Plusieurs retours visent cette porte · corrigez cette ambiguïté avant l’édition.";
                return false;
            }
            reverseLink = reverse.SingleOrDefault();
        }

        draft = new StudioUx2NavGDraft(
            profile.Id,
            history.State.Revision,
            DoorNavigationSourceFingerprintUx2NavG(project),
            profile,
            portal is null ? NavigationDestinationKind.AreaEntity : MapDestinationUx2NavG(portal.DestinationKind),
            portal?.DestinationId ?? Guid.Empty,
            portal?.SpawnAnchorId ?? string.Empty,
            portal?.ExitYawDegrees ?? 0,
            portal?.Preload ?? true,
            portal?.Permissions.ToArray() ?? [],
            sourceLink is { OneWay: false },
            sourceLink?.ReturnNavigationLabel ?? "Retour par la porte liée",
            sourceLink?.Id ?? Guid.NewGuid(),
            reverseLink?.Id ?? Guid.NewGuid(),
            sourceLink?.TargetId,
            sourceLink?.Id,
            reverseLink?.Id,
            profile.AccessEvents!.PortalUse.ForgeScriptApiVersion,
            navigation.ObstacleId,
            navigation.LinkId,
            navigation.BlockingStateIds.ToArray(),
            navigation.TraversableStateIds.ToArray(),
            navigation.OpeningPassableAt,
            navigation.ClosingBlockedAt,
            navigation.VisualizeInEditor);
        return true;
    }

    internal StudioUx2NavGPreview PreviewDoorNavigationUx2NavG(StudioUx2NavGDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelDoorNavigationApprovalUx2NavG();
        var history = History;
        if (history is null || Project is null)
            return FailedDoorNavigationPreviewUx2NavG("Aucun module n’est ouvert.");
        if (!TryBuildDoorNavigationUx2NavG(draft, true, out var command, out var candidate,
                out var updated, out var diagnostics, out var message))
            return FailedDoorNavigationPreviewUx2NavG(message, diagnostics);

        var projectBefore = history.State.Project;
        var revisionBefore = history.State.Revision;
        var dryRun = _setNavigationTopologyUx2NavG.Handle(history.State, command!);
        if (!dryRun.Succeeded)
        {
            var items = dryRun.Diagnostics.Select(item => new StudioUx2NavGDiagnostic(item.Code,
                "navigation/portes", HumanDoorNavigationDiagnosticUx2NavG(item.Message))).ToArray();
            return FailedDoorNavigationPreviewUx2NavG(items.FirstOrDefault()?.Message ??
                                                      "La configuration a été refusée.", items);
        }
        if (!ReferenceEquals(projectBefore, history.State.Project) || history.State.Revision != revisionBefore ||
            dryRun.NewRevision is not null)
            return FailedDoorNavigationPreviewUx2NavG("L’aperçu n’a pas garanti l’absence d’écriture.");

        if (!TryPreviewDoorRuntimeUx2NavG(candidate!, updated!, draft, out var destinationResolved,
                out var portalAccepted, out var portalVetoed, out var directionValidated,
                out var navigationValidated, out var runtimeMessage))
            return FailedDoorNavigationPreviewUx2NavG(runtimeMessage,
                [new("door.runtime.preview_failed", "navigation/portes/aperçu", runtimeMessage)]);

        var diff = BuildDoorNavigationDiffUx2NavG(Project!, draft, updated!);
        _approvedDoorNavigationUx2NavG = draft;
        _approvedDoorNavigationRevisionUx2NavG = history.State.Revision;
        _approvedDoorNavigationFingerprintUx2NavG = DoorNavigationDraftFingerprintUx2NavG(draft);
        var summary = BuildDoorNavigationSummaryUx2NavG(candidate!, updated!, draft);
        return new(true, diff.Count > 0,
            "Aperçu sec validé · aucun acteur, moteur, document ou fichier n’a été modifié.",
            summary, diff, [], destinationResolved, portalAccepted, portalVetoed, directionValidated,
            navigationValidated, true);
    }

    internal StudioUx2NavGActionResult ApplyApprovedDoorNavigationUx2NavG()
    {
        var draft = _approvedDoorNavigationUx2NavG;
        var history = History;
        if (draft is null || history is null || _approvedDoorNavigationRevisionUx2NavG != history.State.Revision ||
            !string.Equals(_approvedDoorNavigationFingerprintUx2NavG,
                DoorNavigationDraftFingerprintUx2NavG(draft), StringComparison.Ordinal))
        {
            CancelDoorNavigationApprovalUx2NavG();
            return new(false, "Le projet ou l’aperçu a changé · prévisualisez de nouveau.", true);
        }
        if (!TryBuildDoorNavigationUx2NavG(draft, false, out var command, out _, out _, out _, out var message))
        {
            CancelDoorNavigationApprovalUx2NavG();
            return new(false, message, true);
        }
        var confirmation = _setNavigationTopologyUx2NavG.Handle(history.State, command! with { DryRun = true });
        if (!confirmation.Succeeded)
        {
            CancelDoorNavigationApprovalUx2NavG();
            return new(false, HumanDoorNavigationDiagnosticUx2NavG(
                confirmation.Diagnostics.FirstOrDefault()?.Message), true);
        }
        var applied = _setNavigationTopologyUx2NavG.Handle(history.State, command!);
        CancelDoorNavigationApprovalUx2NavG();
        if (!applied.Succeeded)
            return new(false, HumanDoorNavigationDiagnosticUx2NavG(applied.Diagnostics.FirstOrDefault()?.Message));
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, "Portail de porte appliqué · Ctrl+Z pour annuler, Ctrl+Y pour rétablir.");
    }

    internal void CancelDoorNavigationApprovalUx2NavG()
    {
        _approvedDoorNavigationUx2NavG = null;
        _approvedDoorNavigationRevisionUx2NavG = null;
        _approvedDoorNavigationFingerprintUx2NavG = null;
    }

    internal void CloseUx2NavG() => CancelDoorNavigationApprovalUx2NavG();

    internal StudioUx2NavGApprovalSnapshot CaptureDoorNavigationApprovalUx2NavG() => new(
        _approvedDoorNavigationUx2NavG, _approvedDoorNavigationRevisionUx2NavG,
        _approvedDoorNavigationFingerprintUx2NavG);

    internal void RestoreDoorNavigationApprovalUx2NavG(StudioUx2NavGApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedDoorNavigationUx2NavG = snapshot.Draft;
        _approvedDoorNavigationRevisionUx2NavG = snapshot.Revision;
        _approvedDoorNavigationFingerprintUx2NavG = snapshot.Fingerprint;
    }

    private bool TryBuildDoorNavigationUx2NavG(StudioUx2NavGDraft draft, bool dryRun,
        out SetNavigationTopologyCommand? command, out ProjectDefinition? candidate,
        out DoorWorldProfile? updatedSource, out IReadOnlyList<StudioUx2NavGDiagnostic> diagnostics,
        out string message)
    {
        command = null;
        candidate = null;
        updatedSource = null;
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
            message = "Le projet a changé · rechargez la porte.";
            return false;
        }
        if (!string.Equals(draft.SourceFingerprint, DoorNavigationSourceFingerprintUx2NavG(project),
                StringComparison.Ordinal))
        {
            message = "La topologie ou les portes ont changé · rechargez la porte.";
            return false;
        }
        var source = project.Module.WorldFeatures.Doors.SingleOrDefault(item => item.Id == draft.DoorId);
        if (source is null || source != draft.OriginalProfile)
        {
            message = "La porte sélectionnée n’est plus dans l’état attendu.";
            return false;
        }
        var local = ValidateDoorNavigationDraftUx2NavG(project, source, draft);
        if (local.Count > 0)
        {
            diagnostics = local;
            message = local[0].Message;
            return false;
        }

        try
        {
            var foundation = source.Foundation!;
            var portal = new DoorWorldProfile.PortalPolicy(
                MapDestinationUx2NavG(draft.DestinationKind), draft.DestinationId,
                draft.SpawnAnchorKey.Trim(), draft.ExitYawDegrees, draft.Preload,
                NormalizeDoorNavigationListUx2NavG(draft.Permissions));
            var navigation = new DoorWorldProfile.NavigationPolicy(
                draft.ObstacleId.Trim(), draft.NavigationLinkId.Trim(),
                NormalizeDoorNavigationListUx2NavG(draft.BlockingStateIds),
                NormalizeDoorNavigationListUx2NavG(draft.TraversableStateIds),
                draft.OpeningPassableAt, draft.ClosingBlockedAt, draft.VisualizeInEditor);
            var access = source.AccessEvents!;
            var portalUse = access.PortalUse with
            {
                ForgeScriptApiVersion = draft.PortalUseForgeScriptApiVersion.Trim(),
            };
            var sourceWithPolicies = source with
            {
                Foundation = foundation with { Portal = portal, Navigation = navigation },
                AccessEvents = access with { PortalUse = portalUse },
            };
            if (!TryMergeDoorLinksUx2NavG(project, sourceWithPolicies, draft, out var replacements,
                    out var linkMessage))
            {
                message = linkMessage;
                return false;
            }
            updatedSource = replacements[source.Id];
            var catalog = project.Module.WorldFeatures;
            var doors = catalog.Doors.Select(item => replacements.GetValueOrDefault(item.Id, item)).ToArray();
            var world = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion, catalog.Provenance,
                catalog.Placeables, doors, catalog.Triggers, catalog.Encounters, catalog.VehicleRoutes,
                catalog.Merchants, catalog.Companions, catalog.Scripts);
            candidate = new ProjectDefinition(project.Id, project.Title,
                project.Module.WithWorldFeatures(world), project.Blueprints, project.Areas,
                project.Dialogues, project.Quests);
            command = new SetNavigationTopologyCommand(CommandId.From(Guid.NewGuid()),
                history.State.Revision, [], [], world, dryRun);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            message = HumanDoorNavigationDiagnosticUx2NavG(exception.Message);
            return false;
        }
    }

    private static IReadOnlyList<StudioUx2NavGDiagnostic> ValidateDoorNavigationDraftUx2NavG(
        ProjectDefinition project, DoorWorldProfile source, StudioUx2NavGDraft draft)
    {
        var result = new List<StudioUx2NavGDiagnostic>();
        var catalog = NavigationDestinationCatalog.Build(project);
        if (draft.DestinationKind is not (NavigationDestinationKind.Door or NavigationDestinationKind.Trigger or
                NavigationDestinationKind.Waypoint or NavigationDestinationKind.SpawnAnchor) ||
            draft.DestinationId == Guid.Empty || catalog.Find(draft.DestinationKind, draft.DestinationId) is null)
            result.Add(new("navigation.destination.missing", "navigation/portes/destination",
                "Choisissez une destination canonique existante."));
        if (draft.DestinationKind == NavigationDestinationKind.Door && draft.DestinationId == source.Id)
            result.Add(new("world.door.transition_loop", "navigation/portes/destination",
                "Une porte ne peut pas se cibler elle-même."));
        if (catalog.FindArrivalAnchor(draft.SpawnAnchorKey) is null)
            result.Add(new("navigation.arrival_anchor.missing", "navigation/portes/arrivée",
                "Choisissez une ancre d’arrivée canonique."));
        if (!double.IsFinite(draft.ExitYawDegrees) || draft.ExitYawDegrees is < -360 or > 360)
            result.Add(new("world.door.portal_yaw_invalid", "navigation/portes/orientation",
                "L’orientation doit rester entre -360° et 360°."));
        var permissions = NormalizeDoorNavigationListUx2NavG(draft.Permissions);
        if (permissions.Count != draft.Permissions.Count || draft.Permissions.Any(item =>
                !string.Equals(item, item.Trim(), StringComparison.Ordinal)))
            result.Add(new("world.door.portal_permissions_invalid", "navigation/portes/permissions",
                "Les permissions doivent être renseignées sans doublon."));
        if (draft.DestinationKind == NavigationDestinationKind.Door && draft.Bidirectional &&
            string.IsNullOrWhiteSpace(draft.ReturnNavigationLabel))
            result.Add(new("world.door.return_navigation_required", "navigation/portes/retour",
                "Un aller-retour exige un libellé humain de retour."));
        if (string.IsNullOrWhiteSpace(draft.PortalUseForgeScriptApiVersion) ||
            draft.PortalUseForgeScriptApiVersion.Trim().Length > 40)
            result.Add(new("world.door.portal_events_invalid", "navigation/portes/événement",
                "La version d’API ForgeScript de PortalUse est requise et limitée à 40 caractères."));
        if (string.IsNullOrWhiteSpace(draft.ObstacleId) || string.IsNullOrWhiteSpace(draft.NavigationLinkId))
            result.Add(new("world.door.navigation_invalid", "navigation/portes/navigation",
                "Les identifiants humains d’obstacle et de liaison sont requis."));
        if (!double.IsFinite(draft.OpeningPassableAt) || !double.IsFinite(draft.ClosingBlockedAt) ||
            draft.OpeningPassableAt is < 0 or > 1 || draft.ClosingBlockedAt is < 0 or > 1 ||
            draft.OpeningPassableAt < draft.ClosingBlockedAt)
            result.Add(new("world.door.navigation_threshold_invalid", "navigation/portes/seuils",
                "Les seuils doivent être compris entre 0 et 1, avec ouverture supérieure ou égale à fermeture."));
        var foundation = source.Foundation!;
        if (foundation.States.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() !=
            foundation.States.Count)
        {
            result.Add(new("world.door.state_machine_invalid", "navigation/portes/états",
                "Les états de porte sont ambigus · corrigez la fondation avant l’édition."));
            return result;
        }
        var states = foundation.States.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var blocking = NormalizeDoorNavigationListUx2NavG(draft.BlockingStateIds);
        var traversable = NormalizeDoorNavigationListUx2NavG(draft.TraversableStateIds);
        if (blocking.Count != draft.BlockingStateIds.Count || traversable.Count != draft.TraversableStateIds.Count ||
            draft.BlockingStateIds.Any(item => !string.Equals(item, item.Trim(), StringComparison.Ordinal)) ||
            draft.TraversableStateIds.Any(item => !string.Equals(item, item.Trim(), StringComparison.Ordinal)) ||
            blocking.Count == 0 || traversable.Count == 0 ||
            blocking.Intersect(traversable, StringComparer.Ordinal).Any() ||
            blocking.Concat(traversable).Any(item => !states.ContainsKey(item)) ||
            blocking.Any(item => !states[item].CollisionEnabled) ||
            traversable.Any(item => states[item].CollisionEnabled) ||
            foundation.States.All(item => item.Kind != DoorWorldProfile.StateKind.Opening) ||
            foundation.States.All(item => item.Kind != DoorWorldProfile.StateKind.Closing))
            result.Add(new("world.door.navigation_state_mismatch", "navigation/portes/états",
                "Les états bloquants et traversables doivent être distincts, connus et cohérents avec la collision."));
        var accessDiagnostics = DoorAccessEventProfileValidator.Validate(source.AccessEvents!,
            $"module/worldFeatures/doors/{source.Id}/accessEvents");
        if (accessDiagnostics.Count > 0)
            result.Add(new(accessDiagnostics[0].Code, "navigation/portes/portal-use",
                "La fiche d’accès UX1 doit être valide avant d’adopter PortalUse."));
        return result;
    }

    private static bool TryMergeDoorLinksUx2NavG(ProjectDefinition project,
        DoorWorldProfile sourceWithPolicies, StudioUx2NavGDraft draft,
        out IReadOnlyDictionary<Guid, DoorWorldProfile> replacements, out string message)
    {
        message = string.Empty;
        var profiles = project.Module.WorldFeatures.Doors.ToDictionary(item => item.Id);
        var linkLists = project.Module.WorldFeatures.Doors.ToDictionary(item => item.Id,
            item => item.Foundation?.Links.ToList() ?? []);
        var sourceLinks = linkLists[sourceWithPolicies.Id];
        var sourceIndex = draft.OriginalLinkPolicyId is { } sourcePolicyId
            ? sourceLinks.FindIndex(item => item.Id == sourcePolicyId)
            : -1;
        if (draft.OriginalLinkPolicyId is not null && sourceIndex < 0)
        {
            replacements = new Dictionary<Guid, DoorWorldProfile>();
            message = "Le lien source a changé · rechargez la porte.";
            return false;
        }
        if (sourceIndex >= 0) sourceLinks.RemoveAt(sourceIndex);

        var reverseIndex = -1;
        List<DoorWorldProfile.LinkPolicy>? originalTargetLinks = null;
        if (draft.OriginalTargetDoorId is { } originalTargetId &&
            draft.OriginalReverseLinkPolicyId is { } reversePolicyId)
        {
            if (!linkLists.TryGetValue(originalTargetId, out originalTargetLinks))
            {
                replacements = new Dictionary<Guid, DoorWorldProfile>();
                message = "La porte de retour n’existe plus.";
                return false;
            }
            reverseIndex = originalTargetLinks.FindIndex(item => item.Id == reversePolicyId);
            if (reverseIndex < 0)
            {
                replacements = new Dictionary<Guid, DoorWorldProfile>();
                message = "Le lien de retour a changé · rechargez la porte.";
                return false;
            }
            originalTargetLinks.RemoveAt(reverseIndex);
        }

        if (draft.DestinationKind == NavigationDestinationKind.Door)
        {
            if (!profiles.TryGetValue(draft.DestinationId, out var target) || target.Foundation is null)
            {
                replacements = new Dictionary<Guid, DoorWorldProfile>();
                message = "La porte de destination ne possède pas de fondation éditable.";
                return false;
            }
            var allLinks = linkLists.SelectMany(pair => pair.Value.Select(link =>
                new DoorLink(pair.Key, link.TargetId, link.OneWay, link.ReturnNavigationLabel, link.Id))).ToArray();
            try
            {
                _ = DoorLinkAuthoringService.Link(allLinks, profiles.Keys.ToHashSet(), sourceWithPolicies.Id,
                    target.Id, !draft.Bidirectional,
                    draft.Bidirectional ? draft.ReturnNavigationLabel.Trim() : string.Empty);
            }
            catch (InvalidOperationException exception)
            {
                replacements = new Dictionary<Guid, DoorWorldProfile>();
                message = HumanDoorNavigationDiagnosticUx2NavG(exception.Message);
                return false;
            }

            var sourcePolicy = new DoorWorldProfile.LinkPolicy(draft.LinkPolicyId, target.Id,
                !draft.Bidirectional, draft.Bidirectional ? draft.ReturnNavigationLabel.Trim() : string.Empty);
            sourceLinks.Insert(sourceIndex >= 0 ? Math.Min(sourceIndex, sourceLinks.Count) : sourceLinks.Count,
                sourcePolicy);
            if (draft.Bidirectional)
            {
                var targetLinks = linkLists[target.Id];
                if (targetLinks.Any(item => item.TargetId == sourceWithPolicies.Id))
                {
                    replacements = new Dictionary<Guid, DoorWorldProfile>();
                    message = "Un retour distinct existe déjà sur la porte de destination · sélectionnez-le avant de créer une paire.";
                    return false;
                }
                var targetIndex = draft.OriginalTargetDoorId == target.Id && reverseIndex >= 0
                    ? Math.Min(reverseIndex, targetLinks.Count)
                    : targetLinks.Count;
                targetLinks.Insert(targetIndex, new DoorWorldProfile.LinkPolicy(draft.ReverseLinkPolicyId,
                    sourceWithPolicies.Id, false, draft.ReturnNavigationLabel.Trim()));
            }
        }

        var allPolicyIds = linkLists.SelectMany(item => item.Value).Select(item => item.Id).ToArray();
        if (allPolicyIds.Any(item => item == Guid.Empty) || allPolicyIds.Distinct().Count() != allPolicyIds.Length)
        {
            replacements = new Dictionary<Guid, DoorWorldProfile>();
            message = "Les identités internes des liens se chevauchent · rechargez la porte.";
            return false;
        }

        var changed = new Dictionary<Guid, DoorWorldProfile>();
        foreach (var profile in project.Module.WorldFeatures.Doors)
        {
            var baseProfile = profile.Id == sourceWithPolicies.Id ? sourceWithPolicies : profile;
            if (baseProfile.Foundation is null) continue;
            var links = linkLists[profile.Id].ToArray();
            if (profile.Id == sourceWithPolicies.Id || !profile.Foundation.Links.SequenceEqual(links))
                changed[profile.Id] = baseProfile with
                {
                    Foundation = baseProfile.Foundation with { Links = links },
                };
        }
        replacements = changed;
        return true;
    }

    private static bool TryPreviewDoorRuntimeUx2NavG(ProjectDefinition candidate, DoorWorldProfile profile,
        StudioUx2NavGDraft draft, out bool destinationResolved, out bool portalAccepted,
        out bool portalVetoed, out bool directionValidated, out bool navigationValidated, out string message)
    {
        destinationResolved = false;
        portalAccepted = false;
        portalVetoed = false;
        directionValidated = false;
        navigationValidated = false;
        message = string.Empty;
        try
        {
            var catalog = NavigationDestinationCatalog.Build(candidate);
            destinationResolved = catalog.Find(draft.DestinationKind, draft.DestinationId) is not null &&
                                  catalog.FindArrivalAnchor(draft.SpawnAnchorKey) is not null;
            var accepted = DoorAccessEventFoundationService.RequestPortalUse(profile,
                "auteur-studio", "aperçu-auteur", new HashSet<string>(["preview"], StringComparer.Ordinal));
            var vetoed = DoorAccessEventFoundationService.RequestPortalUse(profile,
                "auteur-studio", "aperçu-auteur", new HashSet<string>(["preview"], StringComparer.Ordinal),
                _ => "refus simulé");
            portalAccepted = !accepted.Request.Vetoed && accepted.Completed is not null &&
                             accepted.Request.DestinationId == draft.DestinationId &&
                             accepted.Completed.SpawnAnchorId == draft.SpawnAnchorKey;
            portalVetoed = vetoed.Request.Vetoed && vetoed.Completed is null &&
                           string.Equals(vetoed.Request.VetoReason, "refus simulé", StringComparison.Ordinal);

            var links = candidate.Module.WorldFeatures.Doors.SelectMany(item =>
                item.Foundation?.Links.Select(link => new DoorLink(item.Id, link.TargetId, link.OneWay,
                    link.ReturnNavigationLabel, link.Id)) ?? []).ToArray();
            var linkDiagnostics = DoorLinkAuthoringService.Validate(links,
                candidate.Module.WorldFeatures.Doors.Select(item => item.Id).ToHashSet());
            if (draft.DestinationKind == NavigationDestinationKind.Door)
            {
                var forward = links.SingleOrDefault(item => item.SourceDoorId == profile.Id &&
                                                           item.TargetDoorId == draft.DestinationId);
                var reverse = links.SingleOrDefault(item => item.SourceDoorId == draft.DestinationId &&
                                                           item.TargetDoorId == profile.Id);
                directionValidated = forward is not null && forward.OneWay == !draft.Bidirectional &&
                                     (!draft.Bidirectional || reverse is { OneWay: false }) &&
                                     linkDiagnostics.Count == 0;
            }
            else
            {
                directionValidated = linkDiagnostics.Count == 0;
            }

            var foundation = profile.Foundation!;
            var blockingId = NormalizeDoorNavigationListUx2NavG(draft.BlockingStateIds).First();
            var traversableId = NormalizeDoorNavigationListUx2NavG(draft.TraversableStateIds).First();
            var openingId = foundation.States.Single(item => item.Kind == DoorWorldProfile.StateKind.Opening).Id;
            var closingId = foundation.States.Single(item => item.Kind == DoorWorldProfile.StateKind.Closing).Id;
            var belowOpen = Math.Max(0, draft.OpeningPassableAt - .01);
            var atOpen = draft.OpeningPassableAt;
            var belowClose = Math.Max(0, draft.ClosingBlockedAt - .01);
            var atClose = draft.ClosingBlockedAt;
            var blocking = WorldFeatureRuntimeResolver.SynchronizeDoorNavigation(profile, blockingId, 1);
            var traversable = WorldFeatureRuntimeResolver.SynchronizeDoorNavigation(profile, traversableId, 1);
            var openingEarly = WorldFeatureRuntimeResolver.SynchronizeDoorNavigation(profile, openingId, belowOpen);
            var openingReady = WorldFeatureRuntimeResolver.SynchronizeDoorNavigation(profile, openingId, atOpen);
            var closingEarly = WorldFeatureRuntimeResolver.SynchronizeDoorNavigation(profile, closingId, belowClose);
            var closingBlocked = WorldFeatureRuntimeResolver.SynchronizeDoorNavigation(profile, closingId, atClose);
            var openingThresholdValidated = draft.OpeningPassableAt == 0
                ? openingReady.LinkEnabled
                : openingEarly.ObstacleEnabled && openingReady.LinkEnabled;
            var closingThresholdValidated = draft.ClosingBlockedAt == 0
                ? closingBlocked.ObstacleEnabled
                : closingEarly.LinkEnabled && closingBlocked.ObstacleEnabled;
            navigationValidated = blocking.ObstacleEnabled && !blocking.LinkEnabled &&
                                  !traversable.ObstacleEnabled && traversable.LinkEnabled &&
                                  openingThresholdValidated && closingThresholdValidated &&
                                  (!draft.VisualizeInEditor ||
                                   blocking.Visualization.Contains(draft.ObstacleId, StringComparison.Ordinal));
            if (destinationResolved && portalAccepted && portalVetoed && directionValidated && navigationValidated)
                return true;
            message = "La simulation canonique de destination, PortalUse, direction ou navigation n’a pas convergé.";
            return false;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            message = HumanDoorNavigationDiagnosticUx2NavG(exception.Message);
            return false;
        }
    }

    private static IReadOnlyList<string> BuildDoorNavigationDiffUx2NavG(ProjectDefinition project,
        StudioUx2NavGDraft draft, DoorWorldProfile updated)
    {
        var source = draft.OriginalProfile;
        var before = source.Foundation!;
        var after = updated.Foundation!;
        var result = new List<string>();
        if (!DoorPortalEquivalentUx2NavG(before.Portal, after.Portal))
        {
            var destination = NavigationDestinationCatalog.Build(project)
                .Find(draft.DestinationKind, draft.DestinationId)?.HumanLabel ?? "Destination protégée";
            var arrival = NavigationDestinationCatalog.Build(project)
                .FindArrivalAnchor(draft.SpawnAnchorKey)?.HumanLabel ?? "Ancre protégée";
            result.Add($"Destination · {destination} · arrivée {arrival} · orientation {draft.ExitYawDegrees:0.##}°.");
            result.Add($"Préchargement {(draft.Preload ? "activé" : "désactivé")} · " +
                       $"{draft.Permissions.Count} permission(s).");
        }
        if (!before.Links.SequenceEqual(after.Links))
            result.Add(draft.DestinationKind == NavigationDestinationKind.Door
                ? draft.Bidirectional
                    ? $"Créer ou mettre à jour la paire aller-retour « {draft.ReturnNavigationLabel.Trim()} »."
                    : "Créer ou mettre à jour un lien explicitement à sens unique."
                : "Retirer uniquement l’ancien lien associé à la destination remplacée.");
        if (source.AccessEvents!.PortalUse != updated.AccessEvents!.PortalUse)
            result.Add($"Adopter PortalUse existant avec l’API ForgeScript {draft.PortalUseForgeScriptApiVersion.Trim()}.");
        if (!DoorNavigationEquivalentUx2NavG(before.Navigation, after.Navigation))
            result.Add($"Navigation dynamique · passage à {draft.OpeningPassableAt:0.##} à l’ouverture · " +
                       $"blocage à {draft.ClosingBlockedAt:0.##} à la fermeture.");
        return result;
    }

    private static bool DoorPortalEquivalentUx2NavG(DoorWorldProfile.PortalPolicy? left,
        DoorWorldProfile.PortalPolicy? right) => left is null && right is null ||
        left is not null && right is not null && left.DestinationKind == right.DestinationKind &&
        left.DestinationId == right.DestinationId &&
        string.Equals(left.SpawnAnchorId, right.SpawnAnchorId, StringComparison.Ordinal) &&
        left.ExitYawDegrees.Equals(right.ExitYawDegrees) && left.Preload == right.Preload &&
        left.Permissions.SequenceEqual(right.Permissions, StringComparer.Ordinal);

    private static bool DoorNavigationEquivalentUx2NavG(DoorWorldProfile.NavigationPolicy? left,
        DoorWorldProfile.NavigationPolicy? right) => left is null && right is null ||
        left is not null && right is not null &&
        string.Equals(left.ObstacleId, right.ObstacleId, StringComparison.Ordinal) &&
        string.Equals(left.LinkId, right.LinkId, StringComparison.Ordinal) &&
        left.BlockingStateIds.SequenceEqual(right.BlockingStateIds, StringComparer.Ordinal) &&
        left.TraversableStateIds.SequenceEqual(right.TraversableStateIds, StringComparer.Ordinal) &&
        left.OpeningPassableAt.Equals(right.OpeningPassableAt) &&
        left.ClosingBlockedAt.Equals(right.ClosingBlockedAt) &&
        left.VisualizeInEditor == right.VisualizeInEditor;

    private static string BuildDoorNavigationSummaryUx2NavG(ProjectDefinition project,
        DoorWorldProfile profile, StudioUx2NavGDraft draft)
    {
        var destination = NavigationDestinationCatalog.Build(project)
            .Find(draft.DestinationKind, draft.DestinationId)?.HumanLabel ?? "Destination protégée";
        var direction = draft.DestinationKind != NavigationDestinationKind.Door
            ? "destination typée"
            : draft.Bidirectional ? "paire aller-retour atomique" : "sens unique explicite";
        var visualization = profile.Foundation!.Navigation!.VisualizeInEditor
            ? "overlay obstacle/liaison visible" : "overlay obstacle/liaison masqué";
        return $"{destination} · {direction} · PortalUseRequested/veto/Completed simulés · {visualization}.";
    }

    private static StudioUx2NavGPreview FailedDoorNavigationPreviewUx2NavG(string message,
        IReadOnlyList<StudioUx2NavGDiagnostic>? diagnostics = null) => new(false, false, message,
        "Aucune simulation exécutée.", [], diagnostics ?? [], false, false, false, false, false, true);

    private static string? DoorNavigationAvailabilityUx2NavG(DoorWorldProfile profile)
    {
        var foundation = profile.Foundation;
        if (foundation is null) return "Fondation UX1 absente · aucune donnée n’est reconstruite.";
        var navigation = foundation.Navigation;
        if (navigation is null)
            return "Profil de navigation interne absent · complétez d’abord la fondation UX1.";
        var access = profile.AccessEvents;
        if (access is null)
            return "Fiche d’accès UX1 absente · PortalUse ne peut pas être inventé.";
        var diagnostic = DoorAccessEventProfileValidator.Validate(access,
            $"module/worldFeatures/doors/{profile.Id}/accessEvents").FirstOrDefault();
        if (diagnostic is not null)
            return "Fiche d’accès UX1 invalide · corrigez-la avant la navigation.";
        if (foundation.Portal is { } portal &&
            (!double.IsFinite(portal.ExitYawDegrees) || portal.ExitYawDegrees is < -360 or > 360))
            return "Orientation existante hors limites · corrigez la fiche avant de l’ouvrir ici.";
        if (foundation.Portal is { } existingPortal &&
            (existingPortal.Permissions.Any(item => string.IsNullOrWhiteSpace(item) ||
                !string.Equals(item, item.Trim(), StringComparison.Ordinal)) ||
             existingPortal.Permissions.Distinct(StringComparer.Ordinal).Count() !=
             existingPortal.Permissions.Count))
            return "Permissions existantes ambiguës · aucune normalisation silencieuse ne sera appliquée.";
        if (!double.IsFinite(navigation.OpeningPassableAt) || !double.IsFinite(navigation.ClosingBlockedAt) ||
            navigation.OpeningPassableAt is < 0 or > 1 || navigation.ClosingBlockedAt is < 0 or > 1 ||
            navigation.OpeningPassableAt < navigation.ClosingBlockedAt)
            return "Seuils de navigation existants hors limites · aucune valeur ne sera corrigée silencieusement.";
        if (foundation.States.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() !=
            foundation.States.Count)
            return "États de porte ambigus · corrigez la fondation avant l’édition.";
        var states = foundation.States.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var blocking = navigation.BlockingStateIds.ToHashSet(StringComparer.Ordinal);
        var traversable = navigation.TraversableStateIds.ToHashSet(StringComparer.Ordinal);
        if (blocking.Count != navigation.BlockingStateIds.Count ||
            traversable.Count != navigation.TraversableStateIds.Count ||
            navigation.BlockingStateIds.Any(item => !string.Equals(item, item.Trim(), StringComparison.Ordinal)) ||
            navigation.TraversableStateIds.Any(item => !string.Equals(item, item.Trim(), StringComparison.Ordinal)) ||
            blocking.Count == 0 || traversable.Count == 0 || blocking.Overlaps(traversable) ||
            blocking.Concat(traversable).Any(item => !states.ContainsKey(item)) ||
            blocking.Any(item => !states[item].CollisionEnabled) ||
            traversable.Any(item => states[item].CollisionEnabled))
            return "États de navigation existants incohérents · corrigez la fiche avant l’édition.";
        var visibleValues = foundation.States.Select(item => item.Id)
            .Concat(foundation.Portal?.Permissions ?? [])
            .Concat([navigation.ObstacleId, navigation.LinkId, access.PortalUse.ForgeScriptApiVersion])
            .Concat(foundation.Links.Select(item => item.ReturnNavigationLabel));
        return visibleValues.Any(ContainsGuidTokenUx2NavG)
            ? "Cette fiche contient une identité technique visible · qualifiez-la dans la page de validation avant l’édition."
            : null;
    }

    private static bool ContainsGuidTokenUx2NavG(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        foreach (var token in value.Split([' ', '\t', '\r', '\n', ':', '/', '\\', ',', ';', '(', ')', '[', ']'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (Guid.TryParse(token.TrimEnd('.', '!', '?'), out _)) return true;
        return Guid.TryParse(value, out _);
    }

    private static IReadOnlyList<string> NormalizeDoorNavigationListUx2NavG(IEnumerable<string> values) =>
        values.Select(item => item.Trim()).Where(item => item.Length > 0)
            .Distinct(StringComparer.Ordinal).ToArray();

    private static NavigationDestinationKind MapDestinationUx2NavG(DoorWorldProfile.DestinationKind kind) =>
        kind switch
        {
            DoorWorldProfile.DestinationKind.Door => NavigationDestinationKind.Door,
            DoorWorldProfile.DestinationKind.Trigger => NavigationDestinationKind.Trigger,
            DoorWorldProfile.DestinationKind.Waypoint => NavigationDestinationKind.Waypoint,
            DoorWorldProfile.DestinationKind.SpawnAnchor => NavigationDestinationKind.SpawnAnchor,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    private static DoorWorldProfile.DestinationKind MapDestinationUx2NavG(NavigationDestinationKind kind) =>
        kind switch
        {
            NavigationDestinationKind.Door => DoorWorldProfile.DestinationKind.Door,
            NavigationDestinationKind.Trigger => DoorWorldProfile.DestinationKind.Trigger,
            NavigationDestinationKind.Waypoint => DoorWorldProfile.DestinationKind.Waypoint,
            NavigationDestinationKind.SpawnAnchor => DoorWorldProfile.DestinationKind.SpawnAnchor,
            _ => throw new InvalidOperationException("navigation.destination.kind_invalid"),
        };

    private static string DoorNavigationSourceFingerprintUx2NavG(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));

    private static string DoorNavigationDraftFingerprintUx2NavG(StudioUx2NavGDraft draft) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(draft))));

    private static string HumanDoorNavigationDiagnosticUx2NavG(string? value) => value switch
    {
        null or "" => "La configuration a été refusée par la validation canonique.",
        "RPG_DOOR_LINK_TARGET_INVALID" => "Choisissez une autre porte existante comme destination.",
        "RPG_DOOR_LINK_DUPLICATE" => "Un lien vers cette porte existe déjà et n’est pas celui en cours d’édition.",
        "RPG_DOOR_RETURN_NAVIGATION_REQUIRED" => "Un aller-retour exige un libellé humain de retour.",
        "WORLD_DOOR_FOUNDATION_MISSING" => "La fondation UX1 de la porte est absente.",
        "WORLD_DOOR_NAVIGATION_MISSING" => "La navigation interne UX1 de la porte est absente.",
        _ when value.Contains(':', StringComparison.Ordinal) => value[(value.IndexOf(':') + 1)..].Trim(),
        _ => value,
    };

    internal ProjectDefinition? CreateUx2NavGEvidenceProject()
    {
        var seed = Project?.Id.Value ?? Guid.Parse("2e700000-0000-4000-8000-000000000050");
        return CreateUx2NavGEvidenceProject(seed);
    }

    private static ProjectDefinition CreateUx2NavGEvidenceProject(Guid seed)
    {
        var project = CreateUx2NavBEvidenceProject(seed);
        var sourceBlueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("2e700000-0000-4000-8000-000000000001")),
            "porte-des-brumes", "Porte des brumes", BlueprintKind.Door);
        var targetBlueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("2e700000-0000-4000-8000-000000000002")),
            "porte-du-belvedere", "Porte du belvédère", BlueprintKind.Door);
        var neighborBlueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("2e700000-0000-4000-8000-000000000003")),
            "porte-de-service", "Porte de service", BlueprintKind.Door);
        var sourceId = Guid.Parse("2e700000-0000-4000-8000-000000000010");
        var targetId = Guid.Parse("2e700000-0000-4000-8000-000000000011");
        var neighborId = Guid.Parse("2e700000-0000-4000-8000-000000000012");
        const string arrival = "lisiere-arrivee";
        var spawnId = Guid.Parse("2b000000-0000-4000-8000-000000000022");
        var source = CreateCompleteDoorProfileUx1X(sourceBlueprint.Id, sourceId, Guid.NewGuid()) with
        {
            DialogueId = null,
            FactionId = null,
            Validation = null,
            ValidationRequiredAtRuntime = false,
        };
        var target = CreateCompleteDoorProfileUx1X(targetBlueprint.Id, targetId, Guid.NewGuid()) with
        {
            DialogueId = null,
            FactionId = null,
            Validation = null,
            ValidationRequiredAtRuntime = false,
        };
        var neighbor = CreateCompleteDoorProfileUx1X(neighborBlueprint.Id, neighborId, Guid.NewGuid()) with
        {
            DialogueId = null,
            FactionId = null,
            Validation = null,
            ValidationRequiredAtRuntime = false,
        };
        var managedLink = new DoorWorldProfile.LinkPolicy(
            Guid.Parse("2e700000-0000-4000-8000-000000000020"), targetId, true, string.Empty);
        var preservedLink = new DoorWorldProfile.LinkPolicy(
            Guid.Parse("2e700000-0000-4000-8000-000000000021"), neighborId, true, string.Empty);
        source = source with
        {
            Foundation = source.Foundation! with
            {
                Portal = new(DoorWorldProfile.DestinationKind.Door, targetId, arrival, 90, true,
                    ["portal.travel"]),
                Links = [managedLink, preservedLink],
            },
        };
        target = target with
        {
            Foundation = target.Foundation! with
            {
                Portal = new(DoorWorldProfile.DestinationKind.Door, sourceId, arrival, -90, true,
                    ["portal.travel"]),
                Links = [],
            },
        };
        neighbor = neighbor with
        {
            Foundation = neighbor.Foundation! with
            {
                Portal = new(DoorWorldProfile.DestinationKind.SpawnAnchor, spawnId, arrival, 0, false, []),
                Links = [],
            },
        };
        var catalog = project.Module.WorldFeatures;
        var world = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion, Ux2NavGProvenance,
            catalog.Placeables, [source, target, neighbor], catalog.Triggers, catalog.Encounters,
            catalog.VehicleRoutes, catalog.Merchants, catalog.Companions, catalog.Scripts);
        return new ProjectDefinition(project.Id, project.Title, project.Module.WithWorldFeatures(world),
            project.Blueprints.Append(sourceBlueprint).Append(targetBlueprint).Append(neighborBlueprint).ToArray(),
            project.Areas, project.Dialogues, project.Quests);
    }

    internal StudioUx2NavGSmokeResult RunUx2NavGContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-nav-g-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var cleanup = false;
        try
        {
            var fixture = CreateUx2NavGEvidenceProject(Guid.Parse("2e700000-0000-4000-8000-000000000060"));
            var created = ProjectSession.Create(tempRoot, fixture);
            if (!created.Succeeded || !editing.Open(tempRoot).Succeeded || editing.Project is null ||
                editing.History is null)
                return FailedUx2NavGSmoke();

            var project = editing.Project;
            var history = editing.History;
            var sourceId = Guid.Parse("2e700000-0000-4000-8000-000000000010");
            var targetId = Guid.Parse("2e700000-0000-4000-8000-000000000011");
            var neighborId = Guid.Parse("2e700000-0000-4000-8000-000000000012");
            var original = project.Module.WorldFeatures.Doors.Single(item => item.Id == sourceId);
            var originalTarget = project.Module.WorldFeatures.Doors.Single(item => item.Id == targetId);
            var originalNeighbor = project.Module.WorldFeatures.Doors.Single(item => item.Id == neighborId);
            var preservedLink = original.Foundation!.Links[1];
            var explicitSelection = editing.CreateDoorNavigationDraftUx2NavG(null) is null;
            var choices = editing.DoorNavigationChoicesUx2NavG;
            var humanLabels = choices.Count == 3 && choices.All(item => !Guid.TryParse(item.Label, out _) &&
                !item.Label.Contains(item.DoorId.ToString("D"), StringComparison.OrdinalIgnoreCase));
            var draft = editing.CreateDoorNavigationDraftUx2NavG(sourceId);
            var destination = editing.DoorDestinationChoicesUx2NavG(sourceId)
                .Single(item => item.Kind == NavigationDestinationKind.Door && item.Id == targetId);
            var arrival = editing.DoorArrivalChoicesUx2NavG().Single(item => item.AnchorKey == "lisiere-arrivee");
            if (draft is null) return FailedUx2NavGSmoke();

            StudioUx2NavGDraft Configure(StudioUx2NavGDraft value) => value with
            {
                DestinationKind = destination.Kind,
                DestinationId = destination.Id,
                SpawnAnchorKey = arrival.AnchorKey,
                ExitYawDegrees = 145,
                Preload = false,
                Permissions = ["portal.travel", "party.enter"],
                Bidirectional = true,
                ReturnNavigationLabel = "Retour vers la porte des brumes",
                PortalUseForgeScriptApiVersion = "1.1.0",
                ObstacleId = "nav-obstacle:porte-brumes",
                NavigationLinkId = "nav-link:porte-brumes",
                OpeningPassableAt = .8,
                ClosingBlockedAt = .2,
                VisualizeInEditor = true,
            };

            var configured = Configure(draft);
            var oneWayBuilt = editing.TryBuildDoorNavigationUx2NavG(configured with { Bidirectional = false },
                true, out _, out var oneWayCandidate, out _, out _, out _);
            var oneWayLinks = oneWayCandidate?.Module.WorldFeatures.Doors.SelectMany(item =>
                item.Foundation?.Links.Select(link => new DoorLink(item.Id, link.TargetId, link.OneWay,
                    link.ReturnNavigationLabel, link.Id)) ?? []).ToArray() ?? [];
            var oneWay = oneWayBuilt && oneWayLinks.Count(item => item.SourceDoorId == sourceId &&
                item.TargetDoorId == targetId && item.OneWay) == 1 &&
                oneWayLinks.All(item => item.SourceDoorId != targetId || item.TargetDoorId != sourceId);

            var projectBeforePreview = history.State.Project;
            var revisionBeforePreview = history.State.Revision;
            var journalBeforePreview = history.Journal.ToArray();
            var preview = editing.PreviewDoorNavigationUx2NavG(configured);
            var dryRun = preview.Succeeded && ReferenceEquals(projectBeforePreview, history.State.Project) &&
                         history.State.Revision == revisionBeforePreview &&
                         history.Journal.SequenceEqual(journalBeforePreview);
            var typedDestination = preview.DestinationResolved && destination.Label.Contains("Porte", StringComparison.Ordinal) &&
                                   !destination.Label.Contains(targetId.ToString("D"), StringComparison.OrdinalIgnoreCase) &&
                                   !arrival.Label.Contains(arrival.Id.ToString("D"), StringComparison.OrdinalIgnoreCase);
            var atomicDirection = oneWay && preview.DirectionValidated;
            var portalRuntime = preview.PortalUseAccepted && preview.PortalUseVetoed;
            var dynamicRuntime = preview.DynamicNavigationValidated;

            var selfTarget = editing.PreviewDoorNavigationUx2NavG(configured with
            {
                DestinationId = sourceId,
            });
            var overlapping = editing.PreviewDoorNavigationUx2NavG(configured with
            {
                TraversableStateIds = configured.BlockingStateIds,
            });
            var invalidBlocked = !selfTarget.Succeeded && !overlapping.Succeeded;
            var staleSource = !editing.PreviewDoorNavigationUx2NavG(configured with
            {
                SourceFingerprint = "STALE",
            }).Succeeded;

            var approved = editing.PreviewDoorNavigationUx2NavG(configured);
            var intervening = configured with { ExitYawDegrees = 120 };
            var interveningBuilt = editing.TryBuildDoorNavigationUx2NavG(intervening, false,
                out var interveningCommand, out _, out _, out _, out _);
            var interveningApplied = interveningBuilt
                ? editing._setNavigationTopologyUx2NavG.Handle(history.State, interveningCommand!)
                : null;
            if (interveningApplied?.Succeeded == true) history.Record(interveningApplied);
            var staleApprovalResult = editing.ApplyApprovedDoorNavigationUx2NavG();
            var staleApproval = approved.Succeeded && interveningApplied?.Succeeded == true &&
                                !staleApprovalResult.Succeeded && staleApprovalResult.Stale;
            if (interveningApplied?.Succeeded == true && !editing.Undo().Succeeded)
                return FailedUx2NavGSmoke();

            var finalDraft = editing.CreateDoorNavigationDraftUx2NavG(sourceId);
            if (finalDraft is null) return FailedUx2NavGSmoke();
            finalDraft = Configure(finalDraft);
            var finalPreview = editing.PreviewDoorNavigationUx2NavG(finalDraft);
            var journalBeforeApply = history.Journal.Count;
            var applied = editing.ApplyApprovedDoorNavigationUx2NavG();
            var appliedProject = history.State.Project;
            var appliedSource = appliedProject.Module.WorldFeatures.Doors.Single(item => item.Id == sourceId);
            var appliedTarget = appliedProject.Module.WorldFeatures.Doors.Single(item => item.Id == targetId);
            var appliedNeighbor = appliedProject.Module.WorldFeatures.Doors.Single(item => item.Id == neighborId);
            var originalFoundation = original.Foundation!;
            var appliedFoundation = appliedSource.Foundation!;
            var originalAccess = original.AccessEvents!;
            var appliedAccess = appliedSource.AccessEvents!;
            var appliedTargetFoundation = appliedTarget.Foundation!;
            var canonicalCommand = finalPreview.Succeeded && applied.Succeeded &&
                                   history.Journal.Count == journalBeforeApply + 1 &&
                                   history.Journal.Last().CommandName == "set_navigation_topology" &&
                                   history.Journal.Last().Patch.Path == "navigation/topology";
            var portalUseInPlace = appliedAccess.PortalUse.Id == originalAccess.PortalUse.Id &&
                                   appliedAccess.PortalUse.RequestedBinding == "PortalUseRequested" &&
                                   appliedAccess.PortalUse.CompletedBinding == "PortalUseCompleted" &&
                                   appliedAccess.PortalUse.ForgeScriptApiVersion == "1.1.0";
            var ux1Preserved = ReferenceEquals(original.Damage, appliedSource.Damage) &&
                               ReferenceEquals(original.Loading, appliedSource.Loading) &&
                               ReferenceEquals(original.Events, appliedSource.Events) &&
                               ReferenceEquals(original.Assembly, appliedSource.Assembly) &&
                               ReferenceEquals(originalFoundation.States, appliedFoundation.States) &&
                               ReferenceEquals(originalFoundation.Transitions, appliedFoundation.Transitions) &&
                               ReferenceEquals(originalFoundation.Interaction, appliedFoundation.Interaction) &&
                               ReferenceEquals(originalFoundation.Overrides, appliedFoundation.Overrides) &&
                               ReferenceEquals(originalAccess.Lock, appliedAccess.Lock) &&
                               ReferenceEquals(originalAccess.Rejection, appliedAccess.Rejection) &&
                               ReferenceEquals(originalAccess.Trap, appliedAccess.Trap) &&
                               ReferenceEquals(originalAccess.StateEvents, appliedAccess.StateEvents) &&
                               ReferenceEquals(originalAccess.Overrides, appliedAccess.Overrides);
            var neighborLinks = ReferenceEquals(preservedLink, appliedFoundation.Links[1]) &&
                                ReferenceEquals(originalNeighbor, appliedNeighbor) &&
                                ReferenceEquals(originalTarget.Damage, appliedTarget.Damage) &&
                                appliedTargetFoundation.Links.Count == 1 &&
                                appliedTargetFoundation.Links[0].TargetId == sourceId;
            var afterApplyJson = ProjectJsonSerializer.Serialize(appliedProject);
            var undo = editing.Undo();
            var undoneSource = history.State.Project.Module.WorldFeatures.Doors.Single(item => item.Id == sourceId);
            var redo = editing.Redo();
            var redoneSource = history.State.Project.Module.WorldFeatures.Doors.Single(item => item.Id == sourceId);
            var undoRedo = undo.Succeeded && redo.Succeeded &&
                           undoneSource.Foundation!.Portal!.ExitYawDegrees == 90 &&
                           redoneSource.Foundation!.Portal!.ExitYawDegrees == 145;
            var saved = editing.Save();
            var opened = reopened.Open(tempRoot);
            var persistence = saved.Succeeded && opened.Succeeded && reopened.Project is not null &&
                              string.Equals(afterApplyJson, ProjectJsonSerializer.Serialize(reopened.Project),
                                  StringComparison.Ordinal);
            var sourceUntouched = dryRun && staleSource && invalidBlocked;
            editing.Close();
            reopened.Close();
            cleanup = DeleteUx2NavGTemporaryWorkspace(tempRoot);
            var succeeded = explicitSelection && humanLabels && typedDestination && atomicDirection &&
                            portalUseInPlace && portalRuntime && dynamicRuntime && invalidBlocked && dryRun &&
                            staleSource && staleApproval && canonicalCommand && undoRedo && persistence &&
                            ux1Preserved && neighborLinks && sourceUntouched && cleanup;
            return new(succeeded, explicitSelection, humanLabels, typedDestination, atomicDirection,
                portalUseInPlace, portalRuntime, dynamicRuntime, invalidBlocked, dryRun, staleSource,
                staleApproval, canonicalCommand, undoRedo, persistence, ux1Preserved, neighborLinks,
                sourceUntouched, cleanup);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_NAV_G_CONTRACT_FAILED " +
                              $"type={exception.GetType().Name} message={exception.Message}");
            return FailedUx2NavGSmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            if (!cleanup) cleanup = DeleteUx2NavGTemporaryWorkspace(tempRoot);
            if (!cleanup) Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_NAV_G_TEMP_WORKSPACE_FAILED");
        }
    }

    internal static bool DeleteUx2NavGTemporaryWorkspace(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return true;
        try
        {
            var full = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux2-nav-g-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(full)) Directory.Delete(full, true);
            return !Directory.Exists(full);
        }
        catch
        {
            return false;
        }
    }

    private static StudioUx2NavGSmokeResult FailedUx2NavGSmoke() => new(false, false, false,
        false, false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, false);
}
