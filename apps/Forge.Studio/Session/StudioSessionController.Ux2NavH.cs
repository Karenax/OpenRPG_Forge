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
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2NavHProvenance = "studio:ux2-nav-h:trigger-portal";
    private readonly SetNavigationTopologyHandler _setNavigationTopologyUx2NavH = new(new SystemClock());
    private StudioUx2NavHDraft? _approvedTriggerPortalUx2NavH;
    private ProjectRevision? _approvedTriggerPortalRevisionUx2NavH;
    private string? _approvedTriggerPortalSourceFingerprintUx2NavH;
    private string? _approvedTriggerPortalDraftFingerprintUx2NavH;

    internal IReadOnlyList<StudioUx2NavHTriggerChoice> TriggerPortalChoicesUx2NavH
    {
        get
        {
            var project = Project;
            var area = ActiveArea;
            if (project is null || area is null) return [];
            var blueprints = project.Blueprints.ToDictionary(item => item.Id);
            var candidates = project.Module.WorldFeatures.Triggers
                .Where(item => item.AreaId == area.Id)
                .Select(item =>
                {
                    var blueprint = blueprints.GetValueOrDefault(item.BlueprintId);
                    var name = HumanTriggerTextUx2NavH(blueprint?.DisplayName, "Déclencheur protégé");
                    var slug = HumanTriggerTextUx2NavH(item.Foundation?.Slug ?? blueprint?.Slug,
                        "volume protégé");
                    return (Profile: item, BaseLabel: $"{name} · {slug}");
                })
                .OrderBy(item => item.BaseLabel, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.Profile.Id)
                .ToArray();
            return candidates.Select((item, index) =>
            {
                var duplicateIndex = candidates.Take(index + 1).Count(candidate =>
                    string.Equals(candidate.BaseLabel, item.BaseLabel, StringComparison.CurrentCultureIgnoreCase));
                var duplicateCount = candidates.Count(candidate =>
                    string.Equals(candidate.BaseLabel, item.BaseLabel, StringComparison.CurrentCultureIgnoreCase));
                var label = duplicateCount > 1 ? $"{item.BaseLabel} · exemplaire {duplicateIndex}" : item.BaseLabel;
                var availability = TriggerPortalAvailabilityUx2NavH(project, item.Profile);
                var state = item.Profile.Foundation?.Portal is null ? "transition à configurer" : "transition configurée";
                return new StudioUx2NavHTriggerChoice(item.Profile.Id, $"{label} · {state}",
                    availability is null, availability ?? string.Empty);
            }).ToArray();
        }
    }

    internal IReadOnlyList<StudioUx2NavHDestinationChoice> TriggerDestinationChoicesUx2NavH(Guid? sourceId)
    {
        var project = Project;
        if (project is null || sourceId is null) return [];
        return NavigationDestinationCatalog.Build(project).Entries
            .Where(item => item.Kind is NavigationDestinationKind.Door or NavigationDestinationKind.Trigger or
                NavigationDestinationKind.Waypoint or NavigationDestinationKind.SpawnAnchor)
            .Select(item => new StudioUx2NavHDestinationChoice(item.Kind, item.Id, item.HumanLabel))
            .ToArray();
    }

    internal IReadOnlyList<StudioUx2NavHArrivalChoice> TriggerArrivalChoicesUx2NavH()
    {
        var project = Project;
        if (project is null) return [];
        return NavigationDestinationCatalog.Build(project).Entries
            .Where(item => item.Kind == NavigationDestinationKind.SpawnAnchor &&
                           !string.IsNullOrWhiteSpace(item.ArrivalAnchorKey))
            .Select(item => new StudioUx2NavHArrivalChoice(item.ArrivalAnchorKey!, item.HumanLabel))
            .ToArray();
    }

    internal StudioUx2NavHDraft? CreateTriggerPortalDraftUx2NavH(Guid? triggerId) =>
        TryCreateTriggerPortalDraftUx2NavH(triggerId, out var draft, out _) ? draft : null;

    internal bool TryCreateTriggerPortalDraftUx2NavH(Guid? triggerId, out StudioUx2NavHDraft? draft,
        out string message)
    {
        draft = null;
        message = string.Empty;
        var project = Project;
        var history = History;
        var area = ActiveArea;
        if (project is null || history is null || area is null || triggerId is null)
        {
            message = "Choisissez explicitement un déclencheur.";
            return false;
        }
        var profile = project.Module.WorldFeatures.Triggers.SingleOrDefault(item =>
            item.Id == triggerId.Value && item.AreaId == area.Id);
        if (profile is null)
        {
            message = "Le déclencheur choisi n’existe plus dans cette zone.";
            return false;
        }
        var availability = TriggerPortalAvailabilityUx2NavH(project, profile);
        if (availability is not null)
        {
            message = availability;
            return false;
        }
        var portal = profile.Foundation!.Portal;
        draft = new(profile.Id, history.State.Revision, TriggerPortalSourceFingerprintUx2NavH(project), profile,
            portal is not null,
            portal is null ? NavigationDestinationKind.AreaEntity : MapDestinationUx2NavH(portal.Destination),
            portal?.DestinationId ?? Guid.Empty,
            portal?.SpawnAnchorId ?? string.Empty,
            portal?.ExitOrientationDegrees ?? Vector3Value.Zero,
            portal?.Preload ?? true,
            portal?.Permissions.ToArray() ?? []);
        return true;
    }

    internal StudioUx2NavHPreview PreviewTriggerPortalUx2NavH(StudioUx2NavHDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelTriggerPortalApprovalUx2NavH();
        var history = History;
        if (history is null || Project is null)
            return FailedTriggerPortalPreviewUx2NavH("Aucun module n’est ouvert.");
        if (!TryBuildTriggerPortalUx2NavH(draft, true, out var command, out var candidate,
                out var updated, out var synchronized, out var diagnostics, out var message))
            return FailedTriggerPortalPreviewUx2NavH(message, diagnostics);

        var projectBefore = history.State.Project;
        var revisionBefore = history.State.Revision;
        var journalBefore = history.Journal.ToArray();
        var dryRun = _setNavigationTopologyUx2NavH.Handle(history.State, command!);
        if (!dryRun.Succeeded)
        {
            var items = dryRun.Diagnostics.Select(item => new StudioUx2NavHDiagnostic(item.Code,
                "navigation/déclencheurs", HumanTriggerPortalDiagnosticUx2NavH(item.Message))).ToArray();
            return FailedTriggerPortalPreviewUx2NavH(items.FirstOrDefault()?.Message ??
                                                       "La transition a été refusée.", items);
        }
        if (!ReferenceEquals(projectBefore, history.State.Project) || history.State.Revision != revisionBefore ||
            !history.Journal.SequenceEqual(journalBefore) || dryRun.NewRevision is not null)
            return FailedTriggerPortalPreviewUx2NavH("L’aperçu n’a pas garanti l’absence d’écriture.");

        if (!TryPreviewTriggerPortalRuntimeUx2NavH(candidate!, updated!, draft,
                out var destinationResolved, out var triggerFired, out var permissionPolicy,
                out var runtimeMessage))
            return FailedTriggerPortalPreviewUx2NavH(runtimeMessage,
                [new("trigger.portal.runtime_preview_failed", "navigation/déclencheurs/aperçu", runtimeMessage)]);

        var diff = BuildTriggerPortalDiffUx2NavH(candidate!, draft.OriginalProfile, updated!);
        if (diff.Count > 0)
        {
            _approvedTriggerPortalUx2NavH = draft;
            _approvedTriggerPortalRevisionUx2NavH = history.State.Revision;
            _approvedTriggerPortalSourceFingerprintUx2NavH = draft.SourceFingerprint;
            _approvedTriggerPortalDraftFingerprintUx2NavH = TriggerPortalDraftFingerprintUx2NavH(draft);
        }
        return new(true, diff.Count > 0,
            "Aperçu sec validé · aucune transition, écriture, scène ou connexion n’a été exécutée.",
            BuildTriggerPortalSummaryUx2NavH(candidate!, updated!, draft), diff, [], destinationResolved,
            triggerFired, permissionPolicy, synchronized, true);
    }

    internal StudioUx2NavHActionResult ApplyApprovedTriggerPortalUx2NavH()
    {
        var draft = _approvedTriggerPortalUx2NavH;
        var history = History;
        if (draft is null || history is null || _approvedTriggerPortalRevisionUx2NavH != history.State.Revision ||
            !string.Equals(_approvedTriggerPortalSourceFingerprintUx2NavH, draft.SourceFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(_approvedTriggerPortalDraftFingerprintUx2NavH,
                TriggerPortalDraftFingerprintUx2NavH(draft), StringComparison.Ordinal))
        {
            CancelTriggerPortalApprovalUx2NavH();
            return new(false, "Le projet ou l’aperçu a changé · prévisualisez de nouveau.", true);
        }
        if (!TryBuildTriggerPortalUx2NavH(draft, false, out var command, out _, out _, out _, out _,
                out var message))
        {
            CancelTriggerPortalApprovalUx2NavH();
            return new(false, message, true);
        }
        var confirmation = _setNavigationTopologyUx2NavH.Handle(history.State, command! with { DryRun = true });
        if (!confirmation.Succeeded)
        {
            CancelTriggerPortalApprovalUx2NavH();
            return new(false, HumanTriggerPortalDiagnosticUx2NavH(
                confirmation.Diagnostics.FirstOrDefault()?.Message), true);
        }
        var applied = _setNavigationTopologyUx2NavH.Handle(history.State, command!);
        CancelTriggerPortalApprovalUx2NavH();
        if (!applied.Succeeded)
            return new(false, HumanTriggerPortalDiagnosticUx2NavH(applied.Diagnostics.FirstOrDefault()?.Message));
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, "Transition de déclencheur appliquée · Ctrl+Z pour annuler, Ctrl+Y pour rétablir.");
    }

    internal void CancelTriggerPortalApprovalUx2NavH()
    {
        _approvedTriggerPortalUx2NavH = null;
        _approvedTriggerPortalRevisionUx2NavH = null;
        _approvedTriggerPortalSourceFingerprintUx2NavH = null;
        _approvedTriggerPortalDraftFingerprintUx2NavH = null;
    }

    internal void CloseUx2NavH() => CancelTriggerPortalApprovalUx2NavH();

    internal StudioUx2NavHApprovalSnapshot CaptureTriggerPortalApprovalUx2NavH() => new(
        _approvedTriggerPortalUx2NavH, _approvedTriggerPortalRevisionUx2NavH,
        _approvedTriggerPortalSourceFingerprintUx2NavH, _approvedTriggerPortalDraftFingerprintUx2NavH);

    internal void RestoreTriggerPortalApprovalUx2NavH(StudioUx2NavHApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedTriggerPortalUx2NavH = snapshot.Draft;
        _approvedTriggerPortalRevisionUx2NavH = snapshot.Revision;
        _approvedTriggerPortalSourceFingerprintUx2NavH = snapshot.SourceFingerprint;
        _approvedTriggerPortalDraftFingerprintUx2NavH = snapshot.DraftFingerprint;
    }

    private bool TryBuildTriggerPortalUx2NavH(StudioUx2NavHDraft draft, bool dryRun,
        out SetNavigationTopologyCommand? command, out ProjectDefinition? candidate,
        out TriggerWorldProfile? updated, out bool validationSynchronized,
        out IReadOnlyList<StudioUx2NavHDiagnostic> diagnostics, out string message)
    {
        command = null;
        candidate = null;
        updated = null;
        validationSynchronized = false;
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
            message = "Le projet a changé · rechargez le déclencheur.";
            return false;
        }
        if (!string.Equals(draft.SourceFingerprint, TriggerPortalSourceFingerprintUx2NavH(project),
                StringComparison.Ordinal))
        {
            message = "La topologie ou le déclencheur a changé · rechargez sa fiche.";
            return false;
        }
        var source = project.Module.WorldFeatures.Triggers.SingleOrDefault(item => item.Id == draft.TriggerId);
        if (source is null || source != draft.OriginalProfile || source.Foundation is null)
        {
            message = "Le déclencheur sélectionné n’est plus dans l’état attendu.";
            return false;
        }
        var local = ValidateTriggerPortalDraftUx2NavH(project, draft);
        if (local.Count > 0)
        {
            diagnostics = local;
            message = local[0].Message;
            return false;
        }

        try
        {
            var foundation = source.Foundation!;
            TriggerWorldProfile.PortalPolicy? portal = null;
            if (draft.Enabled)
            {
                portal = new(MapDestinationUx2NavH(draft.DestinationKind), draft.DestinationId,
                    draft.SpawnAnchorKey.Trim(), draft.ExitOrientationDegrees, draft.Preload,
                    NormalizeTriggerPortalListUx2NavH(draft.Permissions));
            }
            var validation = SynchronizeTriggerPortalValidationUx2NavH(foundation.Validation,
                foundation.Portal?.DestinationId, portal?.DestinationId);
            validationSynchronized = portal is null || validation is null ||
                validation.KnownPortalDestinationIds.Contains(portal.DestinationId);
            var updatedFoundation = foundation with { Portal = portal, Validation = validation };
            var updatedProfile = source with { Foundation = updatedFoundation };
            updated = updatedProfile;
            var world = project.Module.WorldFeatures;
            var triggers = world.Triggers.Select(item => item.Id == source.Id ? updatedProfile : item).ToArray();
            var replacement = new WorldFeatureCatalog(world.Id, world.SchemaVersion, world.Provenance,
                world.Placeables, world.Doors, triggers, world.Encounters, world.VehicleRoutes,
                world.Merchants, world.Companions, world.Scripts);
            candidate = new ProjectDefinition(project.Id, project.Title,
                project.Module.WithWorldFeatures(replacement), project.Blueprints, project.Areas,
                project.Dialogues, project.Quests);
            command = new SetNavigationTopologyCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                [], [], replacement, dryRun);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            message = HumanTriggerPortalDiagnosticUx2NavH(exception.Message);
            return false;
        }
    }

    private static IReadOnlyList<StudioUx2NavHDiagnostic> ValidateTriggerPortalDraftUx2NavH(
        ProjectDefinition project, StudioUx2NavHDraft draft)
    {
        if (!draft.Enabled) return [];
        var result = new List<StudioUx2NavHDiagnostic>();
        var catalog = NavigationDestinationCatalog.Build(project);
        if (draft.DestinationKind is not (NavigationDestinationKind.Door or NavigationDestinationKind.Trigger or
                NavigationDestinationKind.Waypoint or NavigationDestinationKind.SpawnAnchor) ||
            draft.DestinationId == Guid.Empty || catalog.Find(draft.DestinationKind, draft.DestinationId) is null)
            result.Add(new("navigation.destination.missing", "navigation/déclencheurs/destination",
                "Choisissez une destination canonique existante."));
        if (catalog.FindArrivalAnchor(draft.SpawnAnchorKey) is null)
            result.Add(new("navigation.arrival_anchor.missing", "navigation/déclencheurs/arrivée",
                "Choisissez une ancre d’arrivée canonique."));
        var orientation = draft.ExitOrientationDegrees;
        if (!float.IsFinite(orientation.X) || !float.IsFinite(orientation.Y) || !float.IsFinite(orientation.Z) ||
            orientation.X is < -360 or > 360 || orientation.Y is < -360 or > 360 ||
            orientation.Z is < -360 or > 360)
            result.Add(new("world.trigger.portal_orientation_invalid", "navigation/déclencheurs/orientation",
                "Chaque axe d’orientation doit rester entre -360° et 360°."));
        var permissions = NormalizeTriggerPortalListUx2NavH(draft.Permissions);
        if (permissions.Count != draft.Permissions.Count || draft.Permissions.Any(item =>
                !string.Equals(item, item.Trim(), StringComparison.Ordinal) || item.Length > 80 ||
                ContainsGuidTokenUx2NavH(item)))
            result.Add(new("world.trigger.portal_permissions_invalid", "navigation/déclencheurs/permissions",
                "Les permissions doivent être humaines, uniques et limitées à 80 caractères."));
        return result;
    }

    private static TriggerWorldProfile.RuntimeValidationPolicy? SynchronizeTriggerPortalValidationUx2NavH(
        TriggerWorldProfile.RuntimeValidationPolicy? validation, Guid? previousDestination, Guid? destination)
    {
        if (validation is null || destination is null) return validation;
        var values = validation.KnownPortalDestinationIds.ToList();
        if (values.Contains(destination.Value))
        {
            if (previousDestination is { } old && old != destination.Value)
                values.Remove(old);
        }
        else if (previousDestination is { } old)
        {
            var index = values.IndexOf(old);
            if (index >= 0) values[index] = destination.Value;
            else values.Add(destination.Value);
        }
        else values.Add(destination.Value);
        return values.SequenceEqual(validation.KnownPortalDestinationIds)
            ? validation
            : validation with { KnownPortalDestinationIds = values.ToArray() };
    }

    private static bool TryPreviewTriggerPortalRuntimeUx2NavH(ProjectDefinition candidate,
        TriggerWorldProfile profile, StudioUx2NavHDraft draft, out bool destinationResolved,
        out bool triggerFired, out bool permissionPolicy, out string message)
    {
        destinationResolved = false;
        triggerFired = false;
        permissionPolicy = false;
        message = string.Empty;
        try
        {
            var catalog = NavigationDestinationCatalog.Build(candidate);
            destinationResolved = !draft.Enabled ||
                                  catalog.Find(draft.DestinationKind, draft.DestinationId) is not null &&
                                  catalog.FindArrivalAnchor(draft.SpawnAnchorKey) is not null;
            var foundation = profile.Foundation!;
            var context = foundation.Conditions.Distinct(StringComparer.Ordinal)
                .ToDictionary(item => item, _ => "true", StringComparer.Ordinal);
            var tags = foundation.Filter.RequiredTags.ToHashSet(StringComparer.Ordinal);
            var layers = foundation.Filter.Layers.ToHashSet(StringComparer.Ordinal);
            var state = new WorldFeatureRuntimeResolver.TriggerRuntimeState(
                new Dictionary<string, DateTimeOffset>(), new Dictionary<string, DateTimeOffset>(),
                new HashSet<string>(), false);
            var entered = WorldFeatureRuntimeResolver.EnterTrigger(profile, state, "auteur-studio",
                foundation.Filter.EntityKinds.FirstOrDefault() ?? "creature", tags, layers,
                foundation.Transform.Position, new(0, 0, 1), context, DateTimeOffset.UnixEpoch);
            triggerFired = entered.Fired && entered.Event?.EventBinding == foundation.OnEnter.EventBinding;
            var granted = foundation.Portal?.Permissions.ToHashSet(StringComparer.Ordinal) ??
                          new HashSet<string>(StringComparer.Ordinal);
            var accepted = TriggerPortalTransitionService.Resolve(profile, entered, granted);
            if (!draft.Enabled)
            {
                permissionPolicy = !accepted.Requested && accepted.Reason == "portal-optional-fallback";
            }
            else if (foundation.Portal!.Permissions.Count == 0)
            {
                permissionPolicy = accepted is { Requested: true, Authorized: true };
            }
            else
            {
                var deniedPermissions = granted.Skip(1).ToHashSet(StringComparer.Ordinal);
                var denied = TriggerPortalTransitionService.Resolve(profile, entered, deniedPermissions);
                permissionPolicy = accepted is { Requested: true, Authorized: true, Portal: not null } &&
                                   denied is { Requested: true, Authorized: false, Portal: null } &&
                                   denied.DiagnosticCode == TriggerPortalTransitionService.PermissionDeniedCode;
            }
            if (destinationResolved && triggerFired && permissionPolicy) return true;
            message = "La résolution canonique du volume, de la destination ou des permissions n’a pas convergé.";
            return false;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            message = HumanTriggerPortalDiagnosticUx2NavH(exception.Message);
            return false;
        }
    }

    private static IReadOnlyList<string> BuildTriggerPortalDiffUx2NavH(ProjectDefinition project,
        TriggerWorldProfile source, TriggerWorldProfile updated)
    {
        var before = source.Foundation!.Portal;
        var after = updated.Foundation!.Portal;
        var result = new List<string>();
        if (before is null && after is not null) result.Add("Activer la transition de zone sur ce volume.");
        if (before is not null && after is null) result.Add("Désactiver la transition sans modifier le volume.");
        if (after is not null && !TriggerPortalEquivalentUx2NavH(before, after))
        {
            var catalog = NavigationDestinationCatalog.Build(project);
            var destination = catalog.Find(MapDestinationUx2NavH(after.Destination), after.DestinationId)
                ?.HumanLabel ?? "Destination protégée";
            var arrival = catalog.FindArrivalAnchor(after.SpawnAnchorId)?.HumanLabel ?? "Ancre protégée";
            result.Add($"Destination · {destination} · arrivée {arrival}.");
            result.Add($"Orientation X {after.ExitOrientationDegrees.X:0.##}° · Y " +
                       $"{after.ExitOrientationDegrees.Y:0.##}° · Z {after.ExitOrientationDegrees.Z:0.##}°." );
            result.Add($"Préchargement {(after.Preload ? "demandé" : "désactivé")} · " +
                       $"{after.Permissions.Count} permission(s).");
        }
        var beforeKnown = source.Foundation!.Validation?.KnownPortalDestinationIds ?? [];
        var afterKnown = updated.Foundation!.Validation?.KnownPortalDestinationIds ?? [];
        if (!beforeKnown.SequenceEqual(afterKnown))
            result.Add("Synchroniser uniquement la destination connue liée à ce portail.");
        return result;
    }

    private static string BuildTriggerPortalSummaryUx2NavH(ProjectDefinition project,
        TriggerWorldProfile profile, StudioUx2NavHDraft draft)
    {
        if (!draft.Enabled)
            return "Transition désactivée · volume et événement VolumeEntered préservés.";
        var catalog = NavigationDestinationCatalog.Build(project);
        var destination = catalog.Find(draft.DestinationKind, draft.DestinationId)?.HumanLabel ??
                          "Destination protégée";
        var arrival = catalog.FindArrivalAnchor(draft.SpawnAnchorKey)?.HumanLabel ?? "Ancre protégée";
        var access = profile.Foundation!.Portal!.Permissions.Count == 0
            ? "accès sans permission dédiée"
            : "autorisation et refus de permission vérifiés";
        return $"{destination} · arrivée {arrival} · {access} · " +
               $"préchargement {(draft.Preload ? "déclaré" : "non demandé")}.";
    }

    private static bool TriggerPortalEquivalentUx2NavH(TriggerWorldProfile.PortalPolicy? left,
        TriggerWorldProfile.PortalPolicy? right) => left is null && right is null ||
        left is not null && right is not null && left.Destination == right.Destination &&
        left.DestinationId == right.DestinationId &&
        string.Equals(left.SpawnAnchorId, right.SpawnAnchorId, StringComparison.Ordinal) &&
        left.ExitOrientationDegrees == right.ExitOrientationDegrees && left.Preload == right.Preload &&
        left.Permissions.SequenceEqual(right.Permissions, StringComparer.Ordinal);

    private static string? TriggerPortalAvailabilityUx2NavH(ProjectDefinition project, TriggerWorldProfile profile)
    {
        _ = project;
        var foundation = profile.Foundation;
        if (foundation is null) return "Fondation UX1-Y absente · configurez d’abord le volume de base.";
        var portal = foundation.Portal;
        if (portal is null) return null;
        var orientation = portal.ExitOrientationDegrees;
        if (!float.IsFinite(orientation.X) || !float.IsFinite(orientation.Y) || !float.IsFinite(orientation.Z) ||
            orientation.X is < -360 or > 360 || orientation.Y is < -360 or > 360 ||
            orientation.Z is < -360 or > 360)
            return "Orientation existante hors limites · aucune valeur ne sera réduite silencieusement.";
        if (portal.Permissions.Any(item => string.IsNullOrWhiteSpace(item) || item.Length > 80 ||
                !string.Equals(item, item.Trim(), StringComparison.Ordinal) || ContainsGuidTokenUx2NavH(item)) ||
            portal.Permissions.Distinct(StringComparer.Ordinal).Count() != portal.Permissions.Count)
            return "Permissions existantes ambiguës ou techniques · qualifiez-les avant cette édition.";
        return null;
    }

    private static TriggerWorldProfile.DestinationKind MapDestinationUx2NavH(NavigationDestinationKind kind) =>
        kind switch
        {
            NavigationDestinationKind.Door => TriggerWorldProfile.DestinationKind.Door,
            NavigationDestinationKind.Trigger => TriggerWorldProfile.DestinationKind.Trigger,
            NavigationDestinationKind.Waypoint => TriggerWorldProfile.DestinationKind.Waypoint,
            NavigationDestinationKind.SpawnAnchor => TriggerWorldProfile.DestinationKind.SpawnAnchor,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    private static NavigationDestinationKind MapDestinationUx2NavH(TriggerWorldProfile.DestinationKind kind) =>
        kind switch
        {
            TriggerWorldProfile.DestinationKind.Door => NavigationDestinationKind.Door,
            TriggerWorldProfile.DestinationKind.Trigger => NavigationDestinationKind.Trigger,
            TriggerWorldProfile.DestinationKind.Waypoint => NavigationDestinationKind.Waypoint,
            TriggerWorldProfile.DestinationKind.SpawnAnchor => NavigationDestinationKind.SpawnAnchor,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    private static IReadOnlyList<string> NormalizeTriggerPortalListUx2NavH(IEnumerable<string> values) =>
        values.Select(item => item.Trim()).Where(item => item.Length > 0)
            .Distinct(StringComparer.Ordinal).ToArray();

    private static string TriggerPortalSourceFingerprintUx2NavH(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));

    private static string TriggerPortalDraftFingerprintUx2NavH(StudioUx2NavHDraft draft) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(draft))));

    private static StudioUx2NavHPreview FailedTriggerPortalPreviewUx2NavH(string message,
        IReadOnlyList<StudioUx2NavHDiagnostic>? diagnostics = null) => new(false, false, message,
        "Aucune résolution exécutée.", [], diagnostics ?? [], false, false, false, false, true);

    private static string HumanTriggerPortalDiagnosticUx2NavH(string? value) => value switch
    {
        null or "" => "La transition a été refusée par la validation canonique.",
        TriggerPortalTransitionService.FoundationRequiredCode => "La fondation UX1-Y requise est absente.",
        TriggerPortalTransitionService.PermissionDeniedCode => "Une permission requise manque pour cette transition.",
        _ when value.Contains(':', StringComparison.Ordinal) => value[(value.IndexOf(':') + 1)..].Trim(),
        _ => value,
    };

    private static string HumanTriggerTextUx2NavH(string? value, string fallback)
    {
        var text = value?.Trim();
        return string.IsNullOrWhiteSpace(text) || ContainsGuidTokenUx2NavH(text) ? fallback : text;
    }

    private static bool ContainsGuidTokenUx2NavH(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        foreach (var token in value.Split([' ', '\t', '\r', '\n', ':', '/', '\\', ',', ';', '(', ')', '[', ']'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (Guid.TryParse(token.TrimEnd('.', '!', '?'), out _)) return true;
        return Guid.TryParse(value, out _);
    }

    internal ProjectDefinition? CreateUx2NavHEvidenceProject()
    {
        var seed = Project?.Id.Value ?? Guid.Parse("ac630000-0000-4000-8000-000000000050");
        return CreateUx2NavHEvidenceProject(seed);
    }

    private static ProjectDefinition CreateUx2NavHEvidenceProject(Guid seed)
    {
        var project = CreateUx2NavBEvidenceProject(seed);
        var blueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("ac630000-0000-4000-8000-000000000001")),
            "seuil-des-brumes", "Seuil des brumes", BlueprintKind.Trigger);
        var sourceArea = AreaId.From(Guid.Parse("2b000000-0000-4000-8000-000000000001"));
        var targetArea = AreaId.From(Guid.Parse("2b000000-0000-4000-8000-000000000002"));
        var transitionId = Guid.Parse("2b000000-0000-4000-8000-000000000021");
        var sourceSpawnId = Guid.Parse("2b000000-0000-4000-8000-000000000020");
        var targetSpawnId = Guid.Parse("2b000000-0000-4000-8000-000000000022");
        var source = CreateUx2NavHFixtureTrigger(
            Guid.Parse("ac630000-0000-4000-8000-000000000010"), blueprint.Id, sourceArea,
            "passage-des-brumes", transitionId, "clairiere-depart", sourceSpawnId, neighbor: false);
        var neighbor = CreateUx2NavHFixtureTrigger(
            Guid.Parse("ac630000-0000-4000-8000-000000000011"), blueprint.Id, targetArea,
            "passage-du-belvedere", targetSpawnId, "lisiere-arrivee", transitionId, neighbor: true);
        var protectedProfile = new TriggerWorldProfile(
            Guid.Parse("ac630000-0000-4000-8000-000000000012"), 1, Ux2NavHProvenance,
            blueprint.Id, sourceArea, null, null, null, "trigger.protege", null, null, null, null, null,
            Foundation: null, FoundationRequiredAtRuntime: false);
        var sourceCatalog = project.Module.WorldFeatures;
        var catalog = new WorldFeatureCatalog(sourceCatalog.Id, sourceCatalog.SchemaVersion,
            sourceCatalog.Provenance, sourceCatalog.Placeables, sourceCatalog.Doors,
            [source, neighbor, protectedProfile], sourceCatalog.Encounters, sourceCatalog.VehicleRoutes,
            sourceCatalog.Merchants, sourceCatalog.Companions, sourceCatalog.Scripts);
        return new ProjectDefinition(project.Id, project.Title, project.Module.WithWorldFeatures(catalog),
            project.Blueprints.Append(blueprint).ToArray(), project.Areas, project.Dialogues, project.Quests);
    }

    private static TriggerWorldProfile CreateUx2NavHFixtureTrigger(Guid id, BlueprintId blueprintId,
        AreaId areaId, string slug, Guid destinationId, string arrival, Guid neighborDestination,
        bool neighbor)
    {
        Guid Component(int suffix) => Guid.Parse($"ac630000-0000-4000-{(neighbor ? "8200" : "8100")}-{suffix:000000000000}");
        var portal = new TriggerWorldProfile.PortalPolicy(
            neighbor ? TriggerWorldProfile.DestinationKind.SpawnAnchor :
                TriggerWorldProfile.DestinationKind.Waypoint,
            destinationId, arrival, new(0, neighbor ? -90 : 90, 0), true,
            neighbor ? ["travel"] : ["travel", "chapter.one"]);
        var foundation = new TriggerWorldProfile.FoundationPolicy(
            Component(1), 1, Ux2NavHProvenance, slug,
            new(TriggerWorldProfile.ShapeKind.Box3D, [], [], new(5, 3, 4), 3, .25, true),
            new(["creature"], ["player"], ["undead"], ["characters"]),
            ["navigation.ready"], 1, TriggerWorldProfile.FirePolicy.OncePerEntity,
            [new(Component(2), "transition", "gate:mist", new Dictionary<string, string>())],
            portal,
            new(new(neighbor ? 4 : -4, 0, 2), Vector3Value.Zero, Vector3Value.One, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["editor.overlay"] = "volume",
                ["author.note"] = neighbor ? "belvedere" : "brumes",
            },
            new(Component(3), 1, Ux2NavHProvenance, "1.0.0", "VolumeEntered", null, null),
            new(Component(4), 1, Ux2NavHProvenance, "1.0.0", "VolumeExited", null, null),
            new([destinationId, neighborDestination], ["gate:mist"], ["scripts/trigger-travel"],
                ["characters"]), true);
        return new(id, 1, Ux2NavHProvenance, blueprintId, areaId,
            new(["sight"], 8, id, "trigger.revealed", true, TriggerWorldProfile.DiscoveryScope.Party),
            new("traverser", "portal", "outline", "trigger.travel", "scripts/trigger-travel", true, "travel"),
            new(1, 30, true, true), $"trigger.{slug}",
            new("key.mist", false, "trigger.denied"), null, null, null,
            new(true, true, true, true, true), foundation, true);
    }

    internal StudioUx2NavHSmokeResult RunUx2NavHContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-nav-h-smoke-" + Guid.NewGuid().ToString("N"));
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceRevision = sourceHistory?.State.Revision;
        var sourceJournal = sourceHistory?.Journal.ToArray() ?? [];
        var sourceDirty = IsDirty;
        var sourceApproval = CaptureTriggerPortalApprovalUx2NavH();
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var cleanup = false;
        try
        {
            var fixture = CreateUx2NavHEvidenceProject(
                Guid.Parse("ac630000-0000-4000-8000-000000000060"));
            var created = ProjectSession.Create(tempRoot, fixture);
            if (!created.Succeeded)
            {
                Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_NAV_H_CONTRACT_CREATE_FAILED " +
                                  string.Join(" | ", created.Diagnostics.Select(item =>
                                      $"{item.Code}:{item.Message}")));
                return FailedUx2NavHSmoke();
            }
            var opened = editing.Open(tempRoot);
            if (!opened.Succeeded || editing.Project is null || editing.History is null)
            {
                Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_NAV_H_CONTRACT_OPEN_FAILED " +
                                  string.Join(" | ", opened.Diagnostics.Select(item =>
                                      $"{item.Code}:{item.Message}")));
                return FailedUx2NavHSmoke();
            }

            var project = editing.Project;
            var history = editing.History;
            var sourceId = Guid.Parse("ac630000-0000-4000-8000-000000000010");
            var neighborId = Guid.Parse("ac630000-0000-4000-8000-000000000011");
            var protectedId = Guid.Parse("ac630000-0000-4000-8000-000000000012");
            var targetSpawnId = Guid.Parse("2b000000-0000-4000-8000-000000000022");
            var preservedDestination = Guid.Parse("2b000000-0000-4000-8000-000000000020");
            var original = project.Module.WorldFeatures.Triggers.Single(item => item.Id == sourceId);
            var originalNeighbor = project.Module.WorldFeatures.Triggers.Single(item => item.Id == neighborId);
            var originalProtected = project.Module.WorldFeatures.Triggers.Single(item => item.Id == protectedId);
            var choices = editing.TriggerPortalChoicesUx2NavH;
            var explicitSelection = editing.CreateTriggerPortalDraftUx2NavH(null) is null;
            var humanLabels = choices.Count == 2 && choices.All(item => !ContainsGuidTokenUx2NavH(item.Label) &&
                !item.Label.Contains(item.TriggerId.ToString("D"), StringComparison.OrdinalIgnoreCase));
            var foundationRequired = choices.Single(item => item.TriggerId == protectedId) is
                { CanEdit: false, Availability: var availability } &&
                availability.Contains("Fondation UX1-Y", StringComparison.Ordinal);
            var draft = editing.CreateTriggerPortalDraftUx2NavH(sourceId);
            var destination = editing.TriggerDestinationChoicesUx2NavH(sourceId)
                .Single(item => item.Kind == NavigationDestinationKind.SpawnAnchor && item.Id == targetSpawnId);
            var arrival = editing.TriggerArrivalChoicesUx2NavH()
                .Single(item => item.AnchorKey == "lisiere-arrivee");
            if (draft is null) return FailedUx2NavHSmoke();

            StudioUx2NavHDraft Configure(StudioUx2NavHDraft value) => value with
            {
                Enabled = true,
                DestinationKind = destination.Kind,
                DestinationId = destination.Id,
                SpawnAnchorKey = arrival.AnchorKey,
                ExitOrientationDegrees = new(5, 135, -2),
                Preload = false,
                Permissions = ["travel", "chapter.two"],
            };
            var configured = Configure(draft);
            var invalidDestination = editing.PreviewTriggerPortalUx2NavH(configured with
            {
                DestinationId = Guid.Empty,
            });
            var invalidPermissions = editing.PreviewTriggerPortalUx2NavH(configured with
            {
                Permissions = ["travel", "travel"],
            });
            var invalidBlocked = !invalidDestination.Succeeded && !invalidPermissions.Succeeded;
            var staleSourceBlocked = !editing.PreviewTriggerPortalUx2NavH(configured with
            {
                SourceFingerprint = "STALE",
            }).Succeeded;

            var projectBeforePreview = history.State.Project;
            var revisionBeforePreview = history.State.Revision;
            var journalBeforePreview = history.Journal.ToArray();
            var preview = editing.PreviewTriggerPortalUx2NavH(configured);
            var dryRun = preview.Succeeded && ReferenceEquals(projectBeforePreview, history.State.Project) &&
                         history.State.Revision == revisionBeforePreview &&
                         history.Journal.SequenceEqual(journalBeforePreview);
            var typedDestination = preview.DestinationResolved &&
                                   destination.Label.Contains("Apparition", StringComparison.Ordinal) &&
                                   !destination.Label.Contains(targetSpawnId.ToString("D"),
                                       StringComparison.OrdinalIgnoreCase) &&
                                   !ContainsGuidTokenUx2NavH(arrival.Label);
            var runtimeTransition = preview.TriggerFired && preview.PermissionPolicyValidated;
            var permissionRefusal = preview.PermissionPolicyValidated &&
                                    configured.Permissions.Count > 0;

            var approval = editing.PreviewTriggerPortalUx2NavH(configured);
            var intervening = ApplyUx2NavHIntermediateMutation(editing, neighborId);
            var staleApply = editing.ApplyApprovedTriggerPortalUx2NavH();
            var staleApprovalBlocked = approval.Succeeded && intervening && !staleApply.Succeeded && staleApply.Stale;
            if (intervening && !editing.Undo().Succeeded) return FailedUx2NavHSmoke();

            var finalDraft = editing.CreateTriggerPortalDraftUx2NavH(sourceId);
            if (finalDraft is null) return FailedUx2NavHSmoke();
            finalDraft = Configure(finalDraft);
            var finalPreview = editing.PreviewTriggerPortalUx2NavH(finalDraft);
            var journalBeforeApply = history.Journal.Count;
            var applied = editing.ApplyApprovedTriggerPortalUx2NavH();
            var appliedProject = history.State.Project;
            var appliedProfile = appliedProject.Module.WorldFeatures.Triggers.Single(item => item.Id == sourceId);
            var appliedNeighbor = appliedProject.Module.WorldFeatures.Triggers.Single(item => item.Id == neighborId);
            var appliedProtected = appliedProject.Module.WorldFeatures.Triggers.Single(item => item.Id == protectedId);
            var canonicalCommand = finalPreview.Succeeded && applied.Succeeded &&
                                   history.Journal.Count == journalBeforeApply + 1 &&
                                   history.Journal.Last().CommandName == "set_navigation_topology" &&
                                   history.Journal.Last().Patch.Path == "navigation/topology";
            var appliedPortal = appliedProfile.Foundation!.Portal;
            var portalApplied = appliedPortal is not null &&
                                appliedPortal.Destination == TriggerWorldProfile.DestinationKind.SpawnAnchor &&
                                appliedPortal.DestinationId == targetSpawnId &&
                                appliedPortal.SpawnAnchorId == "lisiere-arrivee" &&
                                appliedPortal.ExitOrientationDegrees == new Vector3Value(5, 135, -2) &&
                                !appliedPortal.Preload &&
                                appliedPortal.Permissions.SequenceEqual(["travel", "chapter.two"]);
            var originalValidation = original.Foundation!.Validation!;
            var validationNeighbor = appliedProfile.Foundation!.Validation is { } validation &&
                                     validation.KnownPortalDestinationIds.SequenceEqual(
                                         [targetSpawnId, preservedDestination]) &&
                                     ReferenceEquals(originalValidation.KnownTargetIds, validation.KnownTargetIds) &&
                                     ReferenceEquals(originalValidation.KnownScriptIds, validation.KnownScriptIds) &&
                                     ReferenceEquals(originalValidation.AccessibleLayers, validation.AccessibleLayers);
            var ux1Preserved = TriggerPortalNeighborsPreservedUx2NavH(original, appliedProfile);
            var worldNeighbors = ReferenceEquals(originalNeighbor, appliedNeighbor) &&
                                 ReferenceEquals(originalProtected, appliedProtected) &&
                                 project.Module.WorldFeatures.Triggers.Select(item => item.Id)
                                     .SequenceEqual(appliedProject.Module.WorldFeatures.Triggers.Select(item => item.Id)) &&
                                 project.Module.WorldFeatures.Doors.SequenceEqual(appliedProject.Module.WorldFeatures.Doors) &&
                                 project.Module.WorldFeatures.Placeables.SequenceEqual(
                                     appliedProject.Module.WorldFeatures.Placeables) &&
                                 project.Module.WorldFeatures.Encounters.SequenceEqual(
                                     appliedProject.Module.WorldFeatures.Encounters) &&
                                 project.Module.WorldFeatures.VehicleRoutes.SequenceEqual(
                                     appliedProject.Module.WorldFeatures.VehicleRoutes) &&
                                 project.Module.WorldFeatures.Merchants.SequenceEqual(
                                     appliedProject.Module.WorldFeatures.Merchants) &&
                                 project.Module.WorldFeatures.Companions.SequenceEqual(
                                     appliedProject.Module.WorldFeatures.Companions) &&
                                 ReferenceEquals(project.Module.WorldFeatures.Scripts,
                                     appliedProject.Module.WorldFeatures.Scripts) &&
                                 project.Blueprints.SequenceEqual(appliedProject.Blueprints) &&
                                 project.Areas.SequenceEqual(appliedProject.Areas) &&
                                 project.Dialogues.SequenceEqual(appliedProject.Dialogues) &&
                                 project.Quests.SequenceEqual(appliedProject.Quests);
            var afterApplyJson = ProjectJsonSerializer.Serialize(appliedProject);
            var undo = editing.Undo();
            var undonePortal = history.State.Project.Module.WorldFeatures.Triggers.Single(item => item.Id == sourceId)
                .Foundation!.Portal;
            var redo = editing.Redo();
            var redonePortal = history.State.Project.Module.WorldFeatures.Triggers.Single(item => item.Id == sourceId)
                .Foundation!.Portal;
            var undoRedo = undo.Succeeded && redo.Succeeded &&
                           undonePortal?.DestinationId == original.Foundation!.Portal!.DestinationId &&
                           redonePortal?.DestinationId == targetSpawnId;
            var saved = editing.Save();
            var reopenedResult = reopened.Open(tempRoot);
            var persistence = saved.Succeeded && reopenedResult.Succeeded && reopened.Project is not null &&
                              string.Equals(afterApplyJson, ProjectJsonSerializer.Serialize(reopened.Project),
                                  StringComparison.Ordinal);
            var sourceUntouched = ReferenceEquals(sourceProject, Project) && ReferenceEquals(sourceHistory, History) &&
                                  History?.State.Revision == sourceRevision &&
                                  (History?.Journal ?? []).SequenceEqual(sourceJournal) && IsDirty == sourceDirty &&
                                  CaptureTriggerPortalApprovalUx2NavH() == sourceApproval;
            editing.Close();
            reopened.Close();
            cleanup = DeleteUx2NavHTemporaryWorkspace(tempRoot);
            var succeeded = explicitSelection && humanLabels && foundationRequired && typedDestination &&
                            portalApplied && runtimeTransition && permissionRefusal && invalidBlocked && dryRun &&
                            staleSourceBlocked && staleApprovalBlocked && canonicalCommand && undoRedo && persistence &&
                            ux1Preserved && validationNeighbor && worldNeighbors && sourceUntouched && cleanup;
            return new(succeeded, explicitSelection, humanLabels, foundationRequired, typedDestination,
                runtimeTransition, permissionRefusal, invalidBlocked, dryRun, staleSourceBlocked,
                staleApprovalBlocked, canonicalCommand, undoRedo, persistence, ux1Preserved,
                validationNeighbor, worldNeighbors, sourceUntouched, cleanup);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_NAV_H_CONTRACT_FAILED " +
                              $"type={exception.GetType().Name} message={exception.Message}");
            return FailedUx2NavHSmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            if (!cleanup) cleanup = DeleteUx2NavHTemporaryWorkspace(tempRoot);
            if (!cleanup) Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_NAV_H_TEMP_WORKSPACE_FAILED");
        }
    }

    private static bool ApplyUx2NavHIntermediateMutation(StudioSessionController editing, Guid neighborId)
    {
        var project = editing.Project;
        var history = editing.History;
        if (project is null || history is null) return false;
        var world = project.Module.WorldFeatures;
        var triggers = world.Triggers.Select(item => item.Id == neighborId
            ? item with { CustomEventNamespace = item.CustomEventNamespace + ".revision" }
            : item).ToArray();
        var replacement = new WorldFeatureCatalog(world.Id, world.SchemaVersion, world.Provenance,
            world.Placeables, world.Doors, triggers, world.Encounters, world.VehicleRoutes,
            world.Merchants, world.Companions, world.Scripts);
        var command = new SetNavigationTopologyCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            [], [], replacement, false);
        var result = editing._setNavigationTopologyUx2NavH.Handle(history.State, command);
        if (!result.Succeeded) return false;
        history.Record(result);
        return true;
    }

    private static bool TriggerPortalNeighborsPreservedUx2NavH(TriggerWorldProfile source,
        TriggerWorldProfile updated)
    {
        var before = source.Foundation!;
        var after = updated.Foundation!;
        return source.Id == updated.Id && source.SchemaVersion == updated.SchemaVersion &&
               source.Provenance == updated.Provenance && source.BlueprintId == updated.BlueprintId &&
               source.AreaId == updated.AreaId &&
               ReferenceEquals(source.Discovery, updated.Discovery) &&
               ReferenceEquals(source.Interaction, updated.Interaction) &&
               ReferenceEquals(source.Scheduler, updated.Scheduler) &&
               source.CustomEventNamespace == updated.CustomEventNamespace &&
               ReferenceEquals(source.Access, updated.Access) && ReferenceEquals(source.Rest, updated.Rest) &&
               ReferenceEquals(source.Clue, updated.Clue) && ReferenceEquals(source.Narrative, updated.Narrative) &&
               ReferenceEquals(source.Director, updated.Director) &&
               source.FoundationRequiredAtRuntime == updated.FoundationRequiredAtRuntime &&
               ReferenceEquals(source.Advanced, updated.Advanced) &&
               source.AdvancedRequiredAtRuntime == updated.AdvancedRequiredAtRuntime &&
               before.Id == after.Id && before.SchemaVersion == after.SchemaVersion &&
               before.Provenance == after.Provenance && before.Slug == after.Slug &&
               ReferenceEquals(before.Shape, after.Shape) && ReferenceEquals(before.Filter, after.Filter) &&
               ReferenceEquals(before.Conditions, after.Conditions) &&
               before.CooldownSeconds == after.CooldownSeconds && before.Fire == after.Fire &&
               ReferenceEquals(before.Actions, after.Actions) && ReferenceEquals(before.Transform, after.Transform) &&
               ReferenceEquals(before.Overrides, after.Overrides) && ReferenceEquals(before.OnEnter, after.OnEnter) &&
               ReferenceEquals(before.OnExit, after.OnExit) &&
               before.ValidationRequiredAtRuntime == after.ValidationRequiredAtRuntime;
    }

    private static StudioUx2NavHSmokeResult FailedUx2NavHSmoke() => new(false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false, false, false,
        false, false);

    internal static bool DeleteUx2NavHTemporaryWorkspace(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return true;
        try
        {
            var full = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var name = Path.GetFileName(full);
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !name.StartsWith("openrpg-forge-studio-ux2-nav-h-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full);
        }
        catch
        {
            return false;
        }
    }
}
