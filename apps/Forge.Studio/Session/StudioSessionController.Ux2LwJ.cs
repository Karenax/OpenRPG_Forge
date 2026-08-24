using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Factions;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2LwJEvidenceProvenance = "studio:ux2-lw-j:evidence";
    private readonly SetWorldFeatureCatalogHandler _setEncounterTacticalCatalogUx2LwJ = new(new SystemClock());
    private StudioUx2LwJDraft? _approvedEncounterTacticalUx2LwJ;
    private ProjectRevision? _approvedEncounterTacticalRevisionUx2LwJ;
    private string? _approvedEncounterTacticalSourceFingerprintUx2LwJ;
    private string? _approvedEncounterTacticalDraftFingerprintUx2LwJ;

    internal IReadOnlyList<StudioUx2LwJEncounterChoice> EncounterChoicesUx2LwJ
    {
        get
        {
            var project = Project;
            var area = ActiveArea;
            if (project is null || area is null) return [];
            return project.Module.WorldFeatures.Encounters
                .Where(item => item.AreaId == area.Id)
                .Select(item => new StudioUx2LwJEncounterChoice(item.Id,
                    $"{HumanEncounterNameUx2LwJ(item)} · " +
                    (!IsEligibleEncounterTacticalUx2LwJ(item)
                        ? "fiche Foundation requise"
                        : $"renforts {OptionalStateUx2LwJ(item.Reinforcements)} · " +
                          $"visibilité {HumanTokenUx2LwJ(item.Visibility.Mode.ToString())} · " +
                          "cleanup configuré"),
                    IsEligibleEncounterTacticalUx2LwJ(item),
                    string.Equals(item.Provenance, Ux2LwJEvidenceProvenance, StringComparison.Ordinal)))
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal IReadOnlyList<StudioUx2LwJFactionChoice> EncounterFactionChoicesUx2LwJ() =>
        Project?.Module.Factions.Factions
            .Where(item => !item.Archived)
            .Select(item => new StudioUx2LwJFactionChoice(item.Slug,
                $"{item.DisplayName} · {HumanTokenUx2LwJ(item.Slug)}",
                HumanProvenanceUx2LwJ(item.Provenance)))
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray() ?? [];

    internal IReadOnlyList<StudioUx2LwJRouteChoice> EncounterRouteChoicesUx2LwJ() =>
        ActiveArea?.Navigation.Waypoints.PatrolRoutes
            .Select((item, index) => new StudioUx2LwJRouteChoice(item.Id,
                $"Itinéraire {index + 1} · {item.Points.Count} étape(s) · " +
                HumanTokenUx2LwJ(item.Traversal.ToString()),
                HumanProvenanceUx2LwJ(item.Provenance), item.Points.Count))
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray() ?? [];

    internal StudioUx2LwJDraft? CreateEncounterTacticalDraftUx2LwJ(Guid? profileId)
    {
        var history = History;
        var project = Project;
        var area = ActiveArea;
        if (history is null || project is null || area is null || profileId is null) return null;
        var profile = project.Module.WorldFeatures.Encounters.SingleOrDefault(item =>
            item.Id == profileId && item.AreaId == area.Id);
        if (!IsEligibleEncounterTacticalUx2LwJ(profile)) return null;
        var selected = profile!;
        var reinforcement = selected.Reinforcements;
        return new(selected.Id, history.State.Revision, ProjectFingerprintUx2LwJ(project),
            reinforcement is not null, reinforcement?.MinimumAlertLevel ?? 2,
            reinforcement?.FactionId, reinforcement?.RouteId,
            reinforcement?.MaximumActors ?? 4, reinforcement?.DelaySeconds ?? 3,
            reinforcement?.CancelCondition ?? "encounter.cancelled",
            selected.Visibility.Mode, selected.Visibility.MinimumCameraDistance,
            selected.Visibility.RequireLineOfSightBlocked, selected.Visibility.SpawnEffectId,
            selected.Cleanup.DeadAfterSeconds, selected.Cleanup.EscapedAfterSeconds,
            selected.Cleanup.AbandonedAfterSeconds, selected.Cleanup.CleanupOnAreaUnload,
            selected.Cleanup.PreservePersistentActors, selected);
    }

    internal StudioUx2LwJPreview PreviewEncounterTacticalUx2LwJ(StudioUx2LwJDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelEncounterTacticalPreviewUx2LwJ();
        var history = History;
        var project = Project;
        if (history is null || project is null)
            return FailedEncounterTacticalPreviewUx2LwJ("Aucun module n’est ouvert.");
        if (draft.BaseRevision != history.State.Revision)
            return FailedEncounterTacticalPreviewUx2LwJ(
                "Le projet a changé depuis l’ouverture de la fiche. Rechargez la rencontre.");
        if (!string.Equals(draft.SourceFingerprint, ProjectFingerprintUx2LwJ(project), StringComparison.Ordinal))
            return FailedEncounterTacticalPreviewUx2LwJ(
                "Le catalogue a changé. Rechargez la rencontre avant l’aperçu.");
        if (!TryBuildEncounterTacticalCandidateUx2LwJ(draft, true, out var command, out var candidate,
                out var diagnostics))
            return new(false, false,
                diagnostics.FirstOrDefault()?.Message ?? "La politique contient une valeur invalide.",
                string.Empty, BuildEncounterTacticalDiffUx2LwJ(draft.OriginalProfile, candidate), diagnostics,
                EmptyEncounterTacticalRuntimeUx2LwJ(), true);

        var result = _setEncounterTacticalCatalogUx2LwJ.Handle(history.State, command!);
        if (!result.Succeeded)
        {
            var commandDiagnostics = result.Diagnostics.Select(item => new StudioUx2LwJDiagnostic(
                item.Code, $"module/worldFeatures/encounters/{draft.ProfileId:D}",
                HumanEncounterTacticalDiagnosticUx2LwJ(item.Code))).ToArray();
            return new(false, false,
                commandDiagnostics.FirstOrDefault()?.Message ?? "La commande canonique a refusé la politique.",
                result.Patch?.Summary ?? string.Empty,
                BuildEncounterTacticalDiffUx2LwJ(draft.OriginalProfile, candidate), commandDiagnostics,
                EmptyEncounterTacticalRuntimeUx2LwJ(), true);
        }

        var diff = BuildEncounterTacticalDiffUx2LwJ(draft.OriginalProfile, candidate);
        var canApply = diff.Any(item => item.Changed);
        if (canApply)
        {
            _approvedEncounterTacticalUx2LwJ = draft;
            _approvedEncounterTacticalRevisionUx2LwJ = history.State.Revision;
            _approvedEncounterTacticalSourceFingerprintUx2LwJ = draft.SourceFingerprint;
            _approvedEncounterTacticalDraftFingerprintUx2LwJ = EncounterTacticalDraftFingerprintUx2LwJ(draft);
        }
        return new(true, canApply,
            canApply
                ? "Aperçu sec validé · renforts, visibilité et cleanup uniquement."
                : "Ces politiques sont déjà identiques au document canonique.",
            result.Patch?.Summary ?? string.Empty, diff, [],
            BuildEncounterTacticalRuntimeUx2LwJ(candidate!, project), true);
    }

    internal StudioUx2LwJActionResult ApplyApprovedEncounterTacticalUx2LwJ()
    {
        var history = History;
        var project = Project;
        var draft = _approvedEncounterTacticalUx2LwJ;
        if (history is null || project is null || draft is null ||
            _approvedEncounterTacticalRevisionUx2LwJ != history.State.Revision ||
            !string.Equals(_approvedEncounterTacticalSourceFingerprintUx2LwJ,
                ProjectFingerprintUx2LwJ(project), StringComparison.Ordinal) ||
            !string.Equals(_approvedEncounterTacticalDraftFingerprintUx2LwJ,
                EncounterTacticalDraftFingerprintUx2LwJ(draft), StringComparison.Ordinal))
        {
            CancelEncounterTacticalPreviewUx2LwJ();
            return new(false, "Prévisualisez à nouveau ces politiques avant de les appliquer.");
        }
        if (!TryBuildEncounterTacticalCandidateUx2LwJ(draft, false, out var command, out _,
                out var diagnostics))
        {
            CancelEncounterTacticalPreviewUx2LwJ();
            return new(false, diagnostics.FirstOrDefault()?.Message ?? "La politique est invalide.");
        }
        var result = _setEncounterTacticalCatalogUx2LwJ.Handle(history.State, command!);
        CancelEncounterTacticalPreviewUx2LwJ();
        if (!result.Succeeded)
            return new(false, HumanEncounterTacticalDiagnosticUx2LwJ(result.Diagnostics.FirstOrDefault()?.Code));
        history.Record(result);
        InvalidateContentIndexes();
        return new(true, "Politiques appliquées · Ctrl+Z pour annuler, Ctrl+Y pour rétablir.");
    }

    internal void CancelEncounterTacticalPreviewUx2LwJ()
    {
        _approvedEncounterTacticalUx2LwJ = null;
        _approvedEncounterTacticalRevisionUx2LwJ = null;
        _approvedEncounterTacticalSourceFingerprintUx2LwJ = null;
        _approvedEncounterTacticalDraftFingerprintUx2LwJ = null;
    }

    internal StudioUx2LwJApprovalSnapshot CaptureEncounterTacticalApprovalUx2LwJ() => new(
        _approvedEncounterTacticalUx2LwJ, _approvedEncounterTacticalRevisionUx2LwJ,
        _approvedEncounterTacticalSourceFingerprintUx2LwJ,
        _approvedEncounterTacticalDraftFingerprintUx2LwJ);

    internal void RestoreEncounterTacticalApprovalUx2LwJ(StudioUx2LwJApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedEncounterTacticalUx2LwJ = snapshot.Draft;
        _approvedEncounterTacticalRevisionUx2LwJ = snapshot.Revision;
        _approvedEncounterTacticalSourceFingerprintUx2LwJ = snapshot.SourceFingerprint;
        _approvedEncounterTacticalDraftFingerprintUx2LwJ = snapshot.DraftFingerprint;
    }

    internal void CloseUx2LwJ() => CancelEncounterTacticalPreviewUx2LwJ();

    internal ProjectDefinition? CreateUx2LwJEvidenceProject()
    {
        var source = CreateUx2LwFEvidenceProject();
        if (source is null) return null;
        var target = source.Module.WorldFeatures.Encounters.FirstOrDefault(item => item.Foundation is not null);
        if (target is null) return null;
        var area = source.Areas.Single(item => item.Id == target.AreaId);
        var factions = source.Module.Factions.Factions.ToList();
        var faction = factions.FirstOrDefault(item => !item.Archived);
        if (faction is null)
        {
            faction = new(StableUx2LwJ(source.Module.Id.Value, "evidence-faction"), 1,
                Ux2LwJEvidenceProvenance, "veilleurs-braises", "Veilleurs des braises", false, [],
                ["rencontre", "garde"]);
            factions.Add(faction);
        }
        var route = CreateEvidenceRouteUx2LwJ(area.Id.Value);
        var sourceWaypoints = area.Navigation.Waypoints;
        var routes = sourceWaypoints.PatrolRoutes.Where(item => item.Id != route.Id).Append(route).ToArray();
        var waypoints = new WaypointAuthoringProfile(sourceWaypoints.Id, sourceWaypoints.SchemaVersion,
            sourceWaypoints.Provenance, sourceWaypoints.AreaId, sourceWaypoints.RequiredAtRuntime,
            sourceWaypoints.Waypoints, routes, sourceWaypoints.Locators, sourceWaypoints.Collections,
            sourceWaypoints.RouteVisualization);
        var authoredArea = area.WithNavigation(area.Navigation.WithWaypoints(waypoints));
        var authored = target with
        {
            Reinforcements = new(2, faction.Slug, route.Id, 4, 3.5, "boss.defeated"),
            Visibility = new(EncounterWorldProfile.VisibilityMode.BehindCover, 12.5, true,
                "vfx.spawn.embers"),
            Cleanup = new(31.5, 47.5, 126.5, true, true),
        };
        var catalog = ReplaceEncounterUx2LwJ(source.Module.WorldFeatures, authored);
        var factionCatalog = source.Module.Factions with { Factions = factions.ToArray() };
        var module = source.Module.WithFactions(factionCatalog).WithWorldFeatures(catalog);
        var areas = source.Areas.Select(item => item.Id == authoredArea.Id ? authoredArea : item).ToArray();
        return new(source.Id, source.Title, module, source.Blueprints, areas,
            source.Dialogues, source.Quests);
    }

    private bool TryBuildEncounterTacticalCandidateUx2LwJ(
        StudioUx2LwJDraft draft,
        bool dryRun,
        out SetWorldFeatureCatalogCommand? command,
        out EncounterWorldProfile? candidate,
        out IReadOnlyList<StudioUx2LwJDiagnostic> diagnostics)
    {
        command = null;
        candidate = null;
        diagnostics = [];
        var history = History;
        var project = Project;
        if (history is null || project is null || draft.BaseRevision != history.State.Revision)
        {
            diagnostics = [new("command.revision_conflict", "project/revision",
                "Le projet a changé. Rechargez la rencontre.")];
            return false;
        }
        if (!string.Equals(draft.SourceFingerprint, ProjectFingerprintUx2LwJ(project), StringComparison.Ordinal))
        {
            diagnostics = [new("command.source_changed", "module/worldFeatures/encounters",
                "Le catalogue a changé. Rechargez la rencontre.")];
            return false;
        }
        var current = project.Module.WorldFeatures.Encounters.SingleOrDefault(item => item.Id == draft.ProfileId);
        if (!IsEligibleEncounterTacticalUx2LwJ(current))
        {
            diagnostics = [new("world.encounter.foundation_required",
                $"module/worldFeatures/encounters/{draft.ProfileId:D}/foundation",
                "Terminez d’abord la fiche Foundation de cette rencontre.")];
            return false;
        }
        var currentProfile = current!;
        var area = project.Areas.SingleOrDefault(item => item.Id == currentProfile.AreaId);
        if (area is null)
        {
            diagnostics = [new("world.reference.area_missing",
                $"module/worldFeatures/encounters/{draft.ProfileId:D}/areaId",
                "La zone de la rencontre n’est plus disponible.")];
            return false;
        }

        var reinforcement = draft.ReinforcementEnabled
            ? new EncounterWorldProfile.ReinforcementPolicy(draft.MinimumAlertLevel,
                draft.FactionSlug?.Trim() ?? string.Empty, draft.RouteId ?? Guid.Empty,
                draft.MaximumActors, draft.ReinforcementDelaySeconds, draft.CancelCondition.Trim())
            : null;
        var candidateProfile = currentProfile with
        {
            Reinforcements = reinforcement,
            Visibility = new(draft.VisibilityMode, draft.MinimumCameraDistance,
                draft.RequireLineOfSightBlocked, draft.SpawnEffectId.Trim()),
            Cleanup = new(draft.DeadAfterSeconds, draft.EscapedAfterSeconds,
                draft.AbandonedAfterSeconds, draft.CleanupOnAreaUnload,
                draft.PreservePersistentActors),
        };
        candidate = candidateProfile;
        var source = project.Module.WorldFeatures;
        var catalog = ReplaceEncounterUx2LwJ(source, candidateProfile);
        var candidateProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(catalog), project.Blueprints, project.Areas,
            project.Dialogues, project.Quests);
        var path = $"module/worldFeatures/encounters/{candidateProfile.Id}";
        var issues = EncounterTacticalRuntimeService.Validate(candidateProfile, project.Module.Factions,
                area.Navigation.Waypoints)
            .Concat(WorldFeatureCatalogValidator.Validate(candidateProject)
                .Where(item => item.Path.StartsWith(path, StringComparison.Ordinal)))
            .DistinctBy(item => (item.Code, item.Path))
            .Select(item => new StudioUx2LwJDiagnostic(item.Code, item.Path,
                HumanEncounterTacticalDiagnosticUx2LwJ(item.Code)))
            .ToArray();
        if (issues.Length > 0)
        {
            diagnostics = issues;
            return false;
        }
        command = new(CommandId.From(Guid.NewGuid()), history.State.Revision, catalog, dryRun);
        return true;
    }

    private static WorldFeatureCatalog ReplaceEncounterUx2LwJ(
        WorldFeatureCatalog source,
        EncounterWorldProfile candidate)
    {
        var encounters = source.Encounters.Select(item => item.Id == candidate.Id ? candidate : item).ToArray();
        return new(source.Id, source.SchemaVersion, source.Provenance,
            source.Placeables, source.Doors, source.Triggers, encounters, source.VehicleRoutes,
            source.Merchants, source.Companions, source.Scripts);
    }

    private static bool IsEligibleEncounterTacticalUx2LwJ(EncounterWorldProfile? profile) =>
        profile?.Foundation is not null;

    private static IReadOnlyList<StudioUx2LwJDiff> BuildEncounterTacticalDiffUx2LwJ(
        EncounterWorldProfile before,
        EncounterWorldProfile? after)
    {
        if (after is null) return [];
        return
        [
            DiffUx2LwJ("Renforts et alertes", ReinforcementSummaryUx2LwJ(before.Reinforcements),
                ReinforcementSummaryUx2LwJ(after.Reinforcements)),
            DiffUx2LwJ("Visibilité et caméra", VisibilitySummaryUx2LwJ(before.Visibility),
                VisibilitySummaryUx2LwJ(after.Visibility)),
            DiffUx2LwJ("Nettoyage", CleanupSummaryUx2LwJ(before.Cleanup),
                CleanupSummaryUx2LwJ(after.Cleanup)),
        ];
    }

    private static StudioUx2LwJDiff DiffUx2LwJ(string section, string before, string after) =>
        new(section, before, after, !string.Equals(before, after, StringComparison.Ordinal));

    private static StudioUx2LwJRuntimePreview BuildEncounterTacticalRuntimeUx2LwJ(
        EncounterWorldProfile profile,
        ProjectDefinition project)
    {
        var area = project.Areas.Single(item => item.Id == profile.AreaId);
        var visibility = EncounterTacticalRuntimeService.ResolveVisibility(profile,
            new(Math.Max(profile.Visibility.MinimumCameraDistance, 0), false, true));
        var reinforcementPolicy = profile.Reinforcements;
        var reinforcement = reinforcementPolicy is null
            ? null
            : EncounterTacticalRuntimeService.ResolveReinforcement(profile, project.Module.Factions,
                area.Navigation.Waypoints,
                new(reinforcementPolicy.MinimumAlertLevel, reinforcementPolicy.DelaySeconds, 0,
                    new HashSet<string>(StringComparer.Ordinal),
                    new(Math.Max(profile.Visibility.MinimumCameraDistance, 0), false, true), true, true));
        var dead = EncounterTacticalRuntimeService.ResolveCleanup(profile,
            new(EncounterActorLifecycleState.Dead, profile.Cleanup.DeadAfterSeconds,
                false, false, true, true));
        var escaped = EncounterTacticalRuntimeService.ResolveCleanup(profile,
            new(EncounterActorLifecycleState.Escaped, profile.Cleanup.EscapedAfterSeconds,
                false, false, true, true));
        var abandoned = EncounterTacticalRuntimeService.ResolveCleanup(profile,
            new(EncounterActorLifecycleState.Abandoned, profile.Cleanup.AbandonedAfterSeconds,
                false, false, true, true));
        var persistent = EncounterTacticalRuntimeService.ResolveCleanup(profile,
            new(EncounterActorLifecycleState.Dead, profile.Cleanup.DeadAfterSeconds,
                true, true, true, true));
        return new(
            reinforcement is null
                ? "Non évalué · aucune politique optionnelle"
                : $"{(reinforcement.Reinforce ? "autorisé" : "refusé")} · faction " +
                  $"{HumanTokenUx2LwJ(reinforcement.FactionSlug)} · itinéraire réel · " +
                  $"{reinforcement.RemainingActorSlots} place(s) · raison {HumanTokenUx2LwJ(reinforcement.Reason)}",
            $"{(visibility.SpawnAllowed ? "autorisé" : "refusé")} · " +
            $"distance {profile.Visibility.MinimumCameraDistance:0.###} m · " +
            $"LOS bloquée {YesNoUx2LwJ(profile.Visibility.RequireLineOfSightBlocked)} · " +
            $"effet {HumanTokenUx2LwJ(visibility.SpawnEffectId)} · raison {HumanTokenUx2LwJ(visibility.Reason)}",
            $"mort {(dead.Eligible ? "éligible" : "conservée")} · " +
            $"fuite {(escaped.Eligible ? "éligible" : "conservée")} · " +
            $"abandon {(abandoned.Eligible ? "éligible" : "conservé")} · " +
            $"acteur persistant {(persistent.Preserve ? "préservé" : "éligible")}",
            "Décisions de politique seulement : aucun acteur n’est instancié, aucun trajet n’est suivi, " +
            "aucune caméra/LOS moteur n’est interrogée, aucune entité n’est supprimée et aucun message réseau n’est transporté.");
    }

    private static StudioUx2LwJRuntimePreview EmptyEncounterTacticalRuntimeUx2LwJ() => new(
        "Renforts non évalués", "Visibilité non évaluée", "Cleanup non évalué",
        "Aucun spawn, trajet, caméra, LOS, nettoyage moteur ou transport réseau n’a été exécuté.");

    private static StudioUx2LwJPreview FailedEncounterTacticalPreviewUx2LwJ(string message) => new(
        false, false, message, string.Empty, [], [], EmptyEncounterTacticalRuntimeUx2LwJ(), true);

    private static string ReinforcementSummaryUx2LwJ(EncounterWorldProfile.ReinforcementPolicy? policy) =>
        policy is null
            ? "Absent · fallback sûr"
            : $"alerte {policy.MinimumAlertLevel} · faction {HumanTokenUx2LwJ(policy.FactionId)} · " +
              $"itinéraire authored · plafond {policy.MaximumActors} · délai {policy.DelaySeconds:0.###} s · " +
              $"annulation {HumanTokenUx2LwJ(policy.CancelCondition)}";

    private static string VisibilitySummaryUx2LwJ(EncounterWorldProfile.VisibilityPolicy policy) =>
        $"{HumanTokenUx2LwJ(policy.Mode.ToString())} · distance {policy.MinimumCameraDistance:0.###} m · " +
        $"LOS bloquée {YesNoUx2LwJ(policy.RequireLineOfSightBlocked)} · " +
        $"effet {HumanTokenUx2LwJ(policy.SpawnEffectId)}";

    private static string CleanupSummaryUx2LwJ(EncounterWorldProfile.CleanupPolicy policy) =>
        $"mort {policy.DeadAfterSeconds:0.###} s · fuite {policy.EscapedAfterSeconds:0.###} s · " +
        $"abandon {policy.AbandonedAfterSeconds:0.###} s · unload {YesNoUx2LwJ(policy.CleanupOnAreaUnload)} · " +
        $"persistants {YesNoUx2LwJ(policy.PreservePersistentActors)}";

    internal static string HumanEncounterTacticalDiagnosticUx2LwJ(string? code) => code switch
    {
        EncounterTacticalRuntimeService.ReinforcementInvalidCode =>
            "Les renforts exigent alerte, plafond et délai valides, plus une condition d’annulation sûre.",
        EncounterTacticalRuntimeService.ReinforcementFactionMissingCode =>
            "Choisissez une faction réelle et active du module.",
        EncounterTacticalRuntimeService.ReinforcementRouteMissingCode =>
            "Choisissez un itinéraire réel et non vide de cette zone.",
        EncounterTacticalRuntimeService.VisibilityInvalidCode =>
            "La visibilité exige un mode connu, une distance finie et un effet sûr pour le mode Effet.",
        EncounterTacticalRuntimeService.CleanupInvalidCode =>
            "Les délais de cleanup mort, fuite et abandon doivent être finis et positifs ou nuls.",
        "world.encounter.foundation_required" => "Terminez d’abord la fiche Foundation de la rencontre.",
        "command.revision_conflict" or "command.source_changed" =>
            "Le projet a changé. Rechargez la rencontre et prévisualisez à nouveau.",
        null or "" => "La commande canonique a refusé la modification.",
        _ => "La politique ne respecte pas encore le contrat canonique.",
    };

    private static string ProjectFingerprintUx2LwJ(ProjectDefinition project)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteFingerprintFieldUx2LwJ(writer, "project-json");
        WriteFingerprintFieldUx2LwJ(writer, ProjectJsonSerializer.Serialize(project));
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static string EncounterTacticalDraftFingerprintUx2LwJ(StudioUx2LwJDraft draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteFingerprintFieldUx2LwJ(writer, "ux2-lw-j-draft-v1");
        WriteFingerprintFieldUx2LwJ(writer, draft.ProfileId.ToString("D"));
        writer.Write(draft.BaseRevision.Value);
        WriteFingerprintFieldUx2LwJ(writer, draft.SourceFingerprint);
        writer.Write(draft.ReinforcementEnabled);
        writer.Write(draft.MinimumAlertLevel);
        WriteFingerprintFieldUx2LwJ(writer, draft.FactionSlug);
        WriteFingerprintFieldUx2LwJ(writer, draft.RouteId?.ToString("D"));
        writer.Write(draft.MaximumActors);
        writer.Write(draft.ReinforcementDelaySeconds);
        WriteFingerprintFieldUx2LwJ(writer, draft.CancelCondition);
        writer.Write((int)draft.VisibilityMode);
        writer.Write(draft.MinimumCameraDistance);
        writer.Write(draft.RequireLineOfSightBlocked);
        WriteFingerprintFieldUx2LwJ(writer, draft.SpawnEffectId);
        writer.Write(draft.DeadAfterSeconds);
        writer.Write(draft.EscapedAfterSeconds);
        writer.Write(draft.AbandonedAfterSeconds);
        writer.Write(draft.CleanupOnAreaUnload);
        writer.Write(draft.PreservePersistentActors);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteFingerprintFieldUx2LwJ(BinaryWriter writer, string? value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string OptionalStateUx2LwJ(object? value) => value is null ? "absents" : "configurés";
    private static string YesNoUx2LwJ(bool value) => value ? "oui" : "non";
    private static string HumanEncounterNameUx2LwJ(EncounterWorldProfile profile) =>
        profile.Composition?.DisplayName ?? $"Rencontre {profile.Id.ToString("N")[..6].ToUpperInvariant()}";
    private static string HumanTokenUx2LwJ(string? value) => string.IsNullOrWhiteSpace(value)
        ? "aucun"
        : value.Replace('-', ' ').Replace('_', ' ').Replace('.', ' ').Replace(':', ' ').Trim();
    private static string HumanProvenanceUx2LwJ(string? value) => string.IsNullOrWhiteSpace(value)
        ? "provenance non renseignée"
        : value.StartsWith("studio:", StringComparison.Ordinal) ? "créé dans le Studio" : "source module vérifiée";

    private static Guid StableUx2LwJ(Guid seed, string purpose)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"ux2-lw-j:{seed:D}:{purpose}"));
        return new(bytes[..16]);
    }

    private static PatrolRoute CreateEvidenceRouteUx2LwJ(Guid seed)
    {
        var routeId = StableUx2LwJ(seed, "route");
        return new(routeId, 1, Ux2LwJEvidenceProvenance, PatrolTraversalPolicy.Once,
        [
            new(StableUx2LwJ(routeId, "point:entry"), new(-4, 0, 2), 0, 90, 2, null, null),
            new(StableUx2LwJ(routeId, "point:arrival"), new(4, 0, 2), 0, 90, 2, null, null),
        ]);
    }

    internal StudioUx2LwJSmokeResult RunUx2LwJContractSmoke()
    {
        var fixture = CreateUx2LwJSmokeProject();
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-lw-j-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(tempRoot, fixture.Project).Succeeded || !editing.Open(tempRoot).Succeeded ||
                !editing.SelectArea(fixture.AreaId))
                return FailedUx2LwJSmoke();
            var loadedProject = editing.Project!;
            var initialCatalog = loadedProject.Module.WorldFeatures;
            var loadedTarget = initialCatalog.Encounters.Single(item => item.Id == fixture.TargetId);
            var loadedNeighbor = initialCatalog.Encounters.Single(item => item.Id == fixture.NeighborId);
            var explicitSelection = editing.CreateEncounterTacticalDraftUx2LwJ(null) is null;
            var incomplete = loadedTarget with { Foundation = null };
            var foundationRequired = !IsEligibleEncounterTacticalUx2LwJ(incomplete) &&
                editing.CreateEncounterTacticalDraftUx2LwJ(fixture.TargetId) is not null &&
                editing.EncounterChoicesUx2LwJ.Single(item => item.ProfileId == fixture.TargetId).IsComplete;
            var factionChoices = editing.EncounterFactionChoicesUx2LwJ();
            var routeChoices = editing.EncounterRouteChoicesUx2LwJ();
            var realReferencesOnly = factionChoices.Any(item => item.Slug == fixture.SecondFactionSlug) &&
                                     routeChoices.Any(item => item.Id == fixture.SecondRouteId) &&
                                     routeChoices.All(item => item.PointCount > 0);
            var draft = editing.CreateEncounterTacticalDraftUx2LwJ(fixture.TargetId);
            if (draft is null) return FailedUx2LwJSmoke();

            var optional = editing.PreviewEncounterTacticalUx2LwJ(draft with
            { ReinforcementEnabled = false, FactionSlug = null, RouteId = null });
            var optionalFallback = optional.Succeeded &&
                optional.Runtime.Reinforcement.Contains("Non évalué", StringComparison.Ordinal);
            var invalidAlert = editing.PreviewEncounterTacticalUx2LwJ(draft with { MinimumAlertLevel = -1 });
            var invalidFaction = editing.PreviewEncounterTacticalUx2LwJ(draft with
            { FactionSlug = "faction-inconnue" });
            var invalidRoute = editing.PreviewEncounterTacticalUx2LwJ(draft with
            { RouteId = StableUx2LwJ(fixture.TargetId, "missing-route") });
            var invalidCap = editing.PreviewEncounterTacticalUx2LwJ(draft with { MaximumActors = 0 });
            var invalidDelay = editing.PreviewEncounterTacticalUx2LwJ(draft with
            { ReinforcementDelaySeconds = double.NaN });
            var invalidCancel = editing.PreviewEncounterTacticalUx2LwJ(draft with
            { CancelCondition = "custom::" });
            var reinforcementValidation =
                new[] { invalidAlert, invalidCap, invalidDelay, invalidCancel }.All(item =>
                    !item.Succeeded && item.Diagnostics.Any(diagnostic =>
                        diagnostic.Code == EncounterTacticalRuntimeService.ReinforcementInvalidCode)) &&
                !invalidFaction.Succeeded && invalidFaction.Diagnostics.Any(item =>
                    item.Code == EncounterTacticalRuntimeService.ReinforcementFactionMissingCode) &&
                !invalidRoute.Succeeded && invalidRoute.Diagnostics.Any(item =>
                    item.Code == EncounterTacticalRuntimeService.ReinforcementRouteMissingCode);
            var invalidMode = editing.PreviewEncounterTacticalUx2LwJ(draft with
            { VisibilityMode = (EncounterWorldProfile.VisibilityMode)999 });
            var invalidDistance = editing.PreviewEncounterTacticalUx2LwJ(draft with
            { MinimumCameraDistance = -1 });
            var invalidEffect = editing.PreviewEncounterTacticalUx2LwJ(draft with
            { VisibilityMode = EncounterWorldProfile.VisibilityMode.Effect, SpawnEffectId = string.Empty });
            var visibilityValidation = new[] { invalidMode, invalidDistance, invalidEffect }.All(item =>
                !item.Succeeded && item.Diagnostics.Any(diagnostic =>
                    diagnostic.Code == EncounterTacticalRuntimeService.VisibilityInvalidCode));
            var invalidDead = editing.PreviewEncounterTacticalUx2LwJ(draft with
            { DeadAfterSeconds = double.NaN });
            var invalidEscaped = editing.PreviewEncounterTacticalUx2LwJ(draft with
            { EscapedAfterSeconds = -1 });
            var invalidAbandoned = editing.PreviewEncounterTacticalUx2LwJ(draft with
            { AbandonedAfterSeconds = double.PositiveInfinity });
            var cleanupValidation = new[] { invalidDead, invalidEscaped, invalidAbandoned }.All(item =>
                !item.Succeeded && item.Diagnostics.Any(diagnostic =>
                    diagnostic.Code == EncounterTacticalRuntimeService.CleanupInvalidCode));

            var staleSource = draft;
            var changedBeforePreview = ApplyUx2LwJIntermediateMutation(editing, fixture.NeighborId, 1);
            var staleDraft = editing.PreviewEncounterTacticalUx2LwJ(staleSource);
            var staleDraftBlocked = changedBeforePreview && !staleDraft.Succeeded && editing.Undo().Succeeded;

            var changed = editing.CreateEncounterTacticalDraftUx2LwJ(fixture.TargetId)! with
            {
                MinimumAlertLevel = draft.MinimumAlertLevel + 1,
                FactionSlug = fixture.SecondFactionSlug,
                RouteId = fixture.SecondRouteId,
                MaximumActors = draft.MaximumActors + 2,
                ReinforcementDelaySeconds = draft.ReinforcementDelaySeconds + .75,
                MinimumCameraDistance = draft.MinimumCameraDistance + 1.25,
                DeadAfterSeconds = draft.DeadAfterSeconds + .5,
                EscapedAfterSeconds = draft.EscapedAfterSeconds + .75,
                AbandonedAfterSeconds = draft.AbandonedAfterSeconds + 1.25,
                CleanupOnAreaUnload = !draft.CleanupOnAreaUnload,
            };
            var approvalPreview = editing.PreviewEncounterTacticalUx2LwJ(changed);
            var changedAfterPreview = ApplyUx2LwJIntermediateMutation(editing, fixture.NeighborId, 2);
            var staleApply = editing.ApplyApprovedEncounterTacticalUx2LwJ();
            var staleApprovalBlocked = approvalPreview.Succeeded && changedAfterPreview &&
                                       !staleApply.Succeeded && editing.Undo().Succeeded;

            changed = editing.CreateEncounterTacticalDraftUx2LwJ(fixture.TargetId)! with
            {
                MinimumAlertLevel = draft.MinimumAlertLevel + 1,
                FactionSlug = fixture.SecondFactionSlug,
                RouteId = fixture.SecondRouteId,
                MaximumActors = draft.MaximumActors + 2,
                ReinforcementDelaySeconds = draft.ReinforcementDelaySeconds + .75,
                MinimumCameraDistance = draft.MinimumCameraDistance + 1.25,
                DeadAfterSeconds = draft.DeadAfterSeconds + .5,
                EscapedAfterSeconds = draft.EscapedAfterSeconds + .75,
                AbandonedAfterSeconds = draft.AbandonedAfterSeconds + 1.25,
                CleanupOnAreaUnload = !draft.CleanupOnAreaUnload,
            };
            var preview = editing.PreviewEncounterTacticalUx2LwJ(changed);
            var dryRunPure = preview.Succeeded && preview.CanApply && preview.DryRunPure &&
                             ReferenceEquals(loadedProject, editing.Project);
            var runtimeResolution = preview.Runtime.Reinforcement.Contains("autorisé",
                                        StringComparison.OrdinalIgnoreCase) &&
                                    preview.Runtime.Visibility.Contains("autorisé",
                                        StringComparison.OrdinalIgnoreCase) &&
                                    preview.Runtime.Cleanup.Contains("éligible",
                                        StringComparison.OrdinalIgnoreCase) &&
                                    preview.Runtime.Boundary.Contains("aucun acteur",
                                        StringComparison.OrdinalIgnoreCase) &&
                                    preview.Runtime.Boundary.Contains("réseau",
                                        StringComparison.OrdinalIgnoreCase);
            var applied = editing.ApplyApprovedEncounterTacticalUx2LwJ();
            if (!applied.Succeeded) return FailedUx2LwJSmoke();
            var appliedProject = editing.Project!;
            var appliedCatalog = appliedProject.Module.WorldFeatures;
            var appliedTarget = appliedCatalog.Encounters.Single(item => item.Id == fixture.TargetId);
            var appliedNeighbor = appliedCatalog.Encounters.Single(item => item.Id == fixture.NeighborId);
            var targetNeighborsPreserved = loadedTarget.Id == appliedTarget.Id &&
                loadedTarget.SchemaVersion == appliedTarget.SchemaVersion && loadedTarget.AreaId == appliedTarget.AreaId &&
                ReferenceEquals(loadedTarget.Respawn, appliedTarget.Respawn) &&
                loadedTarget.SpawnFactionId == appliedTarget.SpawnFactionId &&
                ReferenceEquals(loadedTarget.Scheduler, appliedTarget.Scheduler) &&
                ReferenceEquals(loadedTarget.Foundation, appliedTarget.Foundation) &&
                ReferenceEquals(loadedTarget.Composition, appliedTarget.Composition) &&
                loadedTarget.CompositionRequiredAtRuntime == appliedTarget.CompositionRequiredAtRuntime &&
                ReferenceEquals(loadedTarget.Scenario, appliedTarget.Scenario) &&
                loadedTarget.ScenarioRequiredAtRuntime == appliedTarget.ScenarioRequiredAtRuntime &&
                ReferenceEquals(loadedTarget.Statistics, appliedTarget.Statistics) &&
                loadedTarget.StatisticsRequiredAtRuntime == appliedTarget.StatisticsRequiredAtRuntime;
            var projectNeighborsPreserved = ReferenceEquals(loadedNeighbor, appliedNeighbor) &&
                loadedProject.Blueprints.SequenceEqual(appliedProject.Blueprints) &&
                loadedProject.Areas.SequenceEqual(appliedProject.Areas) &&
                loadedProject.Dialogues.SequenceEqual(appliedProject.Dialogues) &&
                loadedProject.Quests.SequenceEqual(appliedProject.Quests) &&
                ReferenceEquals(loadedProject.Module.Factions, appliedProject.Module.Factions);
            var collectionsPreserved = SameReferencesUx2LwJ(initialCatalog.Placeables, appliedCatalog.Placeables) &&
                SameReferencesUx2LwJ(initialCatalog.Doors, appliedCatalog.Doors) &&
                SameReferencesUx2LwJ(initialCatalog.Triggers, appliedCatalog.Triggers) &&
                SameReferencesUx2LwJ(initialCatalog.VehicleRoutes, appliedCatalog.VehicleRoutes) &&
                SameReferencesUx2LwJ(initialCatalog.Merchants, appliedCatalog.Merchants) &&
                SameReferencesUx2LwJ(initialCatalog.Companions, appliedCatalog.Companions) &&
                ReferenceEquals(initialCatalog.Scripts, appliedCatalog.Scripts);

            var undo = editing.Undo();
            var undoneProject = editing.Project!;
            var undone = undoneProject.Module.WorldFeatures.Encounters.Single(item => item.Id == fixture.TargetId);
            var redo = editing.Redo();
            var redoneProject = editing.Project!;
            var redone = redoneProject.Module.WorldFeatures.Encounters.Single(item => item.Id == fixture.TargetId);
            var undoRedo = undo.Succeeded && redo.Succeeded &&
                EncounterProfileJsonUx2LwJ(undone, undoneProject) ==
                EncounterProfileJsonUx2LwJ(loadedTarget, loadedProject) &&
                EncounterProfileJsonUx2LwJ(redone, redoneProject) ==
                EncounterProfileJsonUx2LwJ(appliedTarget, appliedProject);
            var saved = editing.Save();
            var opened = saved.Succeeded && reopened.Open(tempRoot).Succeeded;
            var persisted = opened ? reopened.Project!.Module.WorldFeatures.Encounters.Single(item =>
                item.Id == fixture.TargetId) : null;
            var persistence = persisted is not null &&
                persisted.Reinforcements == appliedTarget.Reinforcements &&
                persisted.Visibility == appliedTarget.Visibility && persisted.Cleanup == appliedTarget.Cleanup &&
                ProjectJsonSerializer.Serialize(editing.Project!) == ProjectJsonSerializer.Serialize(reopened.Project!);
            reopened.Close();
            var canonicalCommand = editing.History!.Journal.Count >= 3 &&
                                   editing.History.Journal.All(item => item.CommandName == "set_world_feature_catalog");
            var structuredDiagnostics = new[]
                { invalidAlert, invalidFaction, invalidRoute, invalidCap, invalidDelay, invalidCancel,
                    invalidMode, invalidDistance, invalidEffect, invalidDead, invalidEscaped, invalidAbandoned }
                .SelectMany(item => item.Diagnostics)
                .All(item => !string.IsNullOrWhiteSpace(item.Code) && !string.IsNullOrWhiteSpace(item.Path));
            var humanText = editing.EncounterChoicesUx2LwJ.All(item => IsHumanUx2LwJText(item.Label)) &&
                            factionChoices.All(item => IsHumanUx2LwJText(item.Label) &&
                                                       IsHumanUx2LwJText(item.Provenance)) &&
                            routeChoices.All(item => IsHumanUx2LwJText(item.Label) &&
                                                     IsHumanUx2LwJText(item.Provenance));
            var succeeded = explicitSelection && foundationRequired && realReferencesOnly && optionalFallback &&
                            reinforcementValidation && visibilityValidation && cleanupValidation &&
                            runtimeResolution && dryRunPure && staleDraftBlocked && staleApprovalBlocked &&
                            canonicalCommand && undoRedo && persistence && targetNeighborsPreserved &&
                            projectNeighborsPreserved && collectionsPreserved && structuredDiagnostics && humanText;
            return new(succeeded, explicitSelection, foundationRequired, realReferencesOnly, optionalFallback,
                reinforcementValidation, visibilityValidation, cleanupValidation, runtimeResolution,
                dryRunPure, staleDraftBlocked, staleApprovalBlocked, canonicalCommand, undoRedo, persistence,
                targetNeighborsPreserved, projectNeighborsPreserved, collectionsPreserved,
                structuredDiagnostics, humanText);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_LW_J_CONTRACT_FAILED exception={exception.Message}");
            return FailedUx2LwJSmoke();
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
                Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_LW_J_CLEANUP_FAILED " + exception.Message);
            }
        }
    }

    private static bool ApplyUx2LwJIntermediateMutation(StudioSessionController session, Guid profileId, int delta)
    {
        var history = session.History;
        var project = session.Project;
        if (history is null || project is null) return false;
        var source = project.Module.WorldFeatures;
        var neighbor = source.Encounters.Single(item => item.Id == profileId);
        var updated = neighbor with
        {
            Respawn = neighbor.Respawn is { } respawn
                ? respawn with { DelaySeconds = respawn.DelaySeconds + delta }
                : new(delta, 1, "area.ready", false, false, 1),
        };
        var catalog = ReplaceEncounterUx2LwJ(source, updated);
        var result = new SetWorldFeatureCatalogHandler(new SystemClock()).Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, catalog, false));
        if (!result.Succeeded) return false;
        history.Record(result);
        return true;
    }

    private static Ux2LwJSmokeFixture CreateUx2LwJSmokeProject()
    {
        var fixture = CreateUx2LwFSmokeProject();
        var firstFaction = new FactionDefinition(StableUx2LwJ(fixture.Project.Module.Id.Value, "faction-one"), 1,
            Ux2LwJEvidenceProvenance, "veilleurs-braises", "Veilleurs des braises", false, [], ["garde"]);
        var secondFaction = new FactionDefinition(StableUx2LwJ(fixture.Project.Module.Id.Value, "faction-two"), 1,
            Ux2LwJEvidenceProvenance, "pisteurs-cendre", "Pisteurs de cendre", false, [], ["scout"]);
        var area = fixture.Project.Areas.Single(item => item.Id == fixture.AreaId);
        var firstRoute = CreateEvidenceRouteUx2LwJ(area.Id.Value);
        var secondRoute = CreateRouteUx2LwJ(area.Id.Value, "route-secondary", 6);
        var sourceWaypoints = area.Navigation.Waypoints;
        var waypoints = new WaypointAuthoringProfile(sourceWaypoints.Id, sourceWaypoints.SchemaVersion,
            sourceWaypoints.Provenance, sourceWaypoints.AreaId, sourceWaypoints.RequiredAtRuntime,
            sourceWaypoints.Waypoints,
            [.. sourceWaypoints.PatrolRoutes.Where(item => item.Id != firstRoute.Id && item.Id != secondRoute.Id),
                firstRoute, secondRoute],
            sourceWaypoints.Locators, sourceWaypoints.Collections, sourceWaypoints.RouteVisualization);
        var authoredArea = area.WithNavigation(area.Navigation.WithWaypoints(waypoints));
        var source = fixture.Project.Module.WorldFeatures;
        var target = source.Encounters.Single(item => item.Id == fixture.TargetId) with
        {
            Reinforcements = new(2, firstFaction.Slug, firstRoute.Id, 4, 3.25, "boss.defeated"),
            Visibility = new(EncounterWorldProfile.VisibilityMode.BehindCover, 10.5, true,
                "vfx.spawn.embers"),
            Cleanup = new(30.25, 45.5, 120.75, true, true),
        };
        var neighbor = source.Encounters.Single(item => item.Id == fixture.NeighborId) with
        {
            Reinforcements = new(4, secondFaction.Slug, secondRoute.Id, 7, 8.5, "area.safe"),
            Visibility = new(EncounterWorldProfile.VisibilityMode.Hidden, 17.25, false, string.Empty),
            Cleanup = new(60.5, 75.75, 180.25, false, true),
        };
        var encounters = source.Encounters.Select(item => item.Id == target.Id ? target :
            item.Id == neighbor.Id ? neighbor : item).ToArray();
        var catalog = new WorldFeatureCatalog(source.Id, source.SchemaVersion, source.Provenance,
            source.Placeables, source.Doors, source.Triggers, encounters, source.VehicleRoutes,
            source.Merchants, source.Companions, source.Scripts);
        var factions = fixture.Project.Module.Factions with { Factions = [firstFaction, secondFaction] };
        var module = fixture.Project.Module.WithFactions(factions).WithWorldFeatures(catalog);
        var areas = fixture.Project.Areas.Select(item => item.Id == authoredArea.Id ? authoredArea : item).ToArray();
        var project = new ProjectDefinition(fixture.Project.Id, "Studio UX2-LW-J", module,
            fixture.Project.Blueprints, areas, fixture.Project.Dialogues, fixture.Project.Quests);
        return new(project, fixture.AreaId, fixture.TargetId, fixture.NeighborId,
            secondFaction.Slug, secondRoute.Id);
    }

    private static PatrolRoute CreateRouteUx2LwJ(Guid seed, string purpose, double offset)
    {
        var routeId = StableUx2LwJ(seed, purpose);
        return new(routeId, 1, Ux2LwJEvidenceProvenance, PatrolTraversalPolicy.Once,
        [
            new(StableUx2LwJ(routeId, "point:entry"), new(-(float)offset, 0, 3), 0, 45, 2, null, null),
            new(StableUx2LwJ(routeId, "point:arrival"), new((float)offset, 0, 3), 0, 45, 2, null, null),
        ]);
    }

    private static string EncounterProfileJsonUx2LwJ(EncounterWorldProfile profile, ProjectDefinition project)
    {
        var source = project.Module.WorldFeatures;
        var catalog = new WorldFeatureCatalog(source.Id, source.SchemaVersion, source.Provenance,
            source.Placeables, source.Doors, source.Triggers, [profile], source.VehicleRoutes,
            source.Merchants, source.Companions, source.Scripts);
        return ProjectJsonSerializer.Serialize(new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(catalog), project.Blueprints, project.Areas,
            project.Dialogues, project.Quests));
    }

    private static bool SameReferencesUx2LwJ<T>(IReadOnlyList<T> left, IReadOnlyList<T> right)
        where T : class => left.Count == right.Count &&
                           left.Zip(right).All(pair => ReferenceEquals(pair.First, pair.Second));

    private static bool IsHumanUx2LwJText(string value) =>
        !string.IsNullOrWhiteSpace(value) && !value.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !value.Contains("module/worldFeatures", StringComparison.Ordinal) &&
        !value.Split([' ', '·', '/', ':', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Any(token => Guid.TryParse(token, out _));

    private static StudioUx2LwJSmokeResult FailedUx2LwJSmoke() => new(
        false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false);

    private sealed record Ux2LwJSmokeFixture(
        ProjectDefinition Project,
        AreaId AreaId,
        Guid TargetId,
        Guid NeighborId,
        string SecondFactionSlug,
        Guid SecondRouteId);
}
