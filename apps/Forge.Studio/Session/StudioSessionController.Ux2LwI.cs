using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Application.WorldFeatures;
using Forge.Domain.Factions;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2LwIEvidenceProvenance = "studio:ux2-lw-i:evidence";
    private readonly SetWorldFeatureCatalogHandler _setEncounterLifecycleCatalogUx2LwI = new(new SystemClock());
    private StudioUx2LwIDraft? _approvedEncounterLifecycleUx2LwI;
    private ProjectRevision? _approvedEncounterLifecycleRevisionUx2LwI;
    private string? _approvedEncounterLifecycleSourceFingerprintUx2LwI;
    private string? _approvedEncounterLifecycleDraftFingerprintUx2LwI;

    internal IReadOnlyList<StudioUx2LwIEncounterChoice> EncounterChoicesUx2LwI
    {
        get
        {
            var project = Project;
            var area = ActiveArea;
            if (project is null || area is null) return [];
            return project.Module.WorldFeatures.Encounters
                .Where(item => item.AreaId == area.Id)
                .Select(item => new StudioUx2LwIEncounterChoice(item.Id,
                    $"{HumanEncounterNameUx2LwI(item)} · " +
                    (!IsEligibleEncounterLifecycleUx2LwI(item)
                        ? "fiche Foundation requise"
                        : $"respawn {OptionalStateUx2LwI(item.Respawn)}, faction " +
                          $"{(item.SpawnFactionId is null ? "héritée" : HumanTokenUx2LwI(item.SpawnFactionId))}, " +
                          $"scheduler {OptionalStateUx2LwI(item.Scheduler)}"),
                    IsEligibleEncounterLifecycleUx2LwI(item),
                    string.Equals(item.Provenance, Ux2LwIEvidenceProvenance, StringComparison.Ordinal)))
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal IReadOnlyList<StudioUx2LwIFactionChoice> EncounterFactionChoicesUx2LwI() =>
        Project?.Module.Factions.Factions
            .Where(item => !item.Archived)
            .Select(item => new StudioUx2LwIFactionChoice(item.Id, item.Slug,
                $"{item.DisplayName} · {item.Slug}", HumanProvenanceUx2LwI(item.Provenance)))
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray() ?? [];

    internal StudioUx2LwIDraft? CreateEncounterLifecycleDraftUx2LwI(Guid? profileId)
    {
        var history = History;
        var project = Project;
        var area = ActiveArea;
        if (history is null || project is null || area is null || profileId is null) return null;
        var profile = project.Module.WorldFeatures.Encounters.SingleOrDefault(item =>
            item.Id == profileId && item.AreaId == area.Id);
        if (!IsEligibleEncounterLifecycleUx2LwI(profile)) return null;
        var selected = profile!;
        var respawn = selected.Respawn;
        var scheduler = selected.Scheduler;
        return new(selected.Id, history.State.Revision, ProjectFingerprintUx2LwI(project),
            respawn is not null, respawn?.DelaySeconds ?? 30, respawn?.MaximumCycles ?? 3,
            respawn?.Condition ?? "area.occupied", respawn?.ResetWhenEmpty ?? false,
            respawn?.Persistent ?? false, respawn?.AntiFarmMultiplier ?? 1,
            selected.SpawnFactionId, scheduler is not null, scheduler?.HeartbeatEnabled ?? false,
            scheduler?.IntervalSeconds ?? 5, scheduler?.BudgetPerMinute ?? 12,
            scheduler?.CustomEventNamespace ?? "encounter.runtime", selected);
    }

    internal StudioUx2LwIPreview PreviewEncounterLifecycleUx2LwI(StudioUx2LwIDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelEncounterLifecyclePreviewUx2LwI();
        var history = History;
        var project = Project;
        if (history is null || project is null)
            return FailedEncounterLifecyclePreviewUx2LwI("Aucun module n’est ouvert.");
        if (draft.BaseRevision != history.State.Revision)
            return FailedEncounterLifecyclePreviewUx2LwI(
                "Le projet a changé depuis l’ouverture de la fiche. Rechargez la rencontre.");
        if (!string.Equals(draft.SourceFingerprint, ProjectFingerprintUx2LwI(project), StringComparison.Ordinal))
            return FailedEncounterLifecyclePreviewUx2LwI(
                "Le catalogue a changé. Rechargez la rencontre avant l’aperçu.");
        if (!TryBuildEncounterLifecycleCandidateUx2LwI(draft, true, out var command, out var candidate,
                out var diagnostics))
            return new(false, false,
                diagnostics.FirstOrDefault()?.Message ?? "La politique contient une valeur invalide.",
                string.Empty, BuildEncounterLifecycleDiffUx2LwI(draft.OriginalProfile, candidate), diagnostics,
                EmptyEncounterLifecycleRuntimeUx2LwI(), true);

        var result = _setEncounterLifecycleCatalogUx2LwI.Handle(history.State, command!);
        if (!result.Succeeded)
        {
            var commandDiagnostics = result.Diagnostics.Select(item => new StudioUx2LwIDiagnostic(
                item.Code, $"module/worldFeatures/encounters/{draft.ProfileId:D}",
                HumanEncounterLifecycleDiagnosticUx2LwI(item.Code))).ToArray();
            return new(false, false,
                commandDiagnostics.FirstOrDefault()?.Message ?? "La commande canonique a refusé la politique.",
                result.Patch?.Summary ?? string.Empty,
                BuildEncounterLifecycleDiffUx2LwI(draft.OriginalProfile, candidate), commandDiagnostics,
                EmptyEncounterLifecycleRuntimeUx2LwI(), true);
        }

        var diff = BuildEncounterLifecycleDiffUx2LwI(draft.OriginalProfile, candidate);
        var canApply = diff.Any(item => item.Changed);
        if (canApply)
        {
            _approvedEncounterLifecycleUx2LwI = draft;
            _approvedEncounterLifecycleRevisionUx2LwI = history.State.Revision;
            _approvedEncounterLifecycleSourceFingerprintUx2LwI = draft.SourceFingerprint;
            _approvedEncounterLifecycleDraftFingerprintUx2LwI = EncounterLifecycleDraftFingerprintUx2LwI(draft);
        }
        return new(true, canApply,
            canApply
                ? "Aperçu sec validé · politiques de respawn, faction et événements uniquement."
                : "Ces politiques sont déjà identiques au document canonique.",
            result.Patch?.Summary ?? string.Empty, diff, [],
            BuildEncounterLifecycleRuntimeUx2LwI(candidate!, project.Module.Factions), true);
    }

    internal StudioUx2LwIActionResult ApplyApprovedEncounterLifecycleUx2LwI()
    {
        var history = History;
        var project = Project;
        var draft = _approvedEncounterLifecycleUx2LwI;
        if (history is null || project is null || draft is null ||
            _approvedEncounterLifecycleRevisionUx2LwI != history.State.Revision ||
            !string.Equals(_approvedEncounterLifecycleSourceFingerprintUx2LwI,
                ProjectFingerprintUx2LwI(project), StringComparison.Ordinal) ||
            !string.Equals(_approvedEncounterLifecycleDraftFingerprintUx2LwI,
                EncounterLifecycleDraftFingerprintUx2LwI(draft), StringComparison.Ordinal))
        {
            CancelEncounterLifecyclePreviewUx2LwI();
            return new(false, "Prévisualisez à nouveau ces politiques avant de les appliquer.");
        }
        if (!TryBuildEncounterLifecycleCandidateUx2LwI(draft, false, out var command, out _,
                out var diagnostics))
        {
            CancelEncounterLifecyclePreviewUx2LwI();
            return new(false, diagnostics.FirstOrDefault()?.Message ?? "La politique est invalide.");
        }
        var result = _setEncounterLifecycleCatalogUx2LwI.Handle(history.State, command!);
        CancelEncounterLifecyclePreviewUx2LwI();
        if (!result.Succeeded)
            return new(false, HumanEncounterLifecycleDiagnosticUx2LwI(result.Diagnostics.FirstOrDefault()?.Code));
        history.Record(result);
        InvalidateContentIndexes();
        return new(true, "Politiques appliquées · Ctrl+Z pour annuler, Ctrl+Y pour rétablir.");
    }

    internal void CancelEncounterLifecyclePreviewUx2LwI()
    {
        _approvedEncounterLifecycleUx2LwI = null;
        _approvedEncounterLifecycleRevisionUx2LwI = null;
        _approvedEncounterLifecycleSourceFingerprintUx2LwI = null;
        _approvedEncounterLifecycleDraftFingerprintUx2LwI = null;
    }

    internal StudioUx2LwIApprovalSnapshot CaptureEncounterLifecycleApprovalUx2LwI() => new(
        _approvedEncounterLifecycleUx2LwI, _approvedEncounterLifecycleRevisionUx2LwI,
        _approvedEncounterLifecycleSourceFingerprintUx2LwI,
        _approvedEncounterLifecycleDraftFingerprintUx2LwI);

    internal void RestoreEncounterLifecycleApprovalUx2LwI(StudioUx2LwIApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedEncounterLifecycleUx2LwI = snapshot.Draft;
        _approvedEncounterLifecycleRevisionUx2LwI = snapshot.Revision;
        _approvedEncounterLifecycleSourceFingerprintUx2LwI = snapshot.SourceFingerprint;
        _approvedEncounterLifecycleDraftFingerprintUx2LwI = snapshot.DraftFingerprint;
    }

    internal void CloseUx2LwI() => CancelEncounterLifecyclePreviewUx2LwI();

    internal ProjectDefinition? CreateUx2LwIEvidenceProject()
    {
        var source = CreateUx2LwFEvidenceProject();
        if (source is null) return null;
        var factions = source.Module.Factions.Factions.ToList();
        var faction = factions.FirstOrDefault(item => !item.Archived);
        if (faction is null)
        {
            faction = new(StableUx2LwI(source.Module.Id.Value, "evidence-faction"), 1,
                Ux2LwIEvidenceProvenance, "veilleurs-braises", "Veilleurs des braises", false, [],
                ["rencontre", "garde"]);
            factions.Add(faction);
        }
        var target = source.Module.WorldFeatures.Encounters.First(item => item.Foundation is not null);
        var authored = target with
        {
            Respawn = new(24.5, 4, "area.ready", true, true, .65),
            SpawnFactionId = faction.Slug,
            Scheduler = new(false, 7.5, 9, "veille.braises"),
        };
        var catalog = ReplaceEncounterUx2LwI(source.Module.WorldFeatures, authored);
        var factionCatalog = source.Module.Factions with { Factions = factions.ToArray() };
        var module = source.Module.WithFactions(factionCatalog).WithWorldFeatures(catalog);
        return new(source.Id, source.Title, module, source.Blueprints, source.Areas,
            source.Dialogues, source.Quests);
    }

    private bool TryBuildEncounterLifecycleCandidateUx2LwI(
        StudioUx2LwIDraft draft,
        bool dryRun,
        out SetWorldFeatureCatalogCommand? command,
        out EncounterWorldProfile? candidate,
        out IReadOnlyList<StudioUx2LwIDiagnostic> diagnostics)
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
        if (!string.Equals(draft.SourceFingerprint, ProjectFingerprintUx2LwI(project), StringComparison.Ordinal))
        {
            diagnostics = [new("command.source_changed", "module/worldFeatures/encounters",
                "Le catalogue a changé. Rechargez la rencontre.")];
            return false;
        }
        var current = project.Module.WorldFeatures.Encounters.SingleOrDefault(item => item.Id == draft.ProfileId);
        if (!IsEligibleEncounterLifecycleUx2LwI(current))
        {
            diagnostics = [new("world.encounter.foundation_required",
                $"module/worldFeatures/encounters/{draft.ProfileId:D}/foundation",
                "Terminez d’abord la fiche Foundation de cette rencontre.")];
            return false;
        }

        var currentProfile = current!;
        var factionSlug = string.IsNullOrWhiteSpace(draft.SpawnFactionSlug) ? null : draft.SpawnFactionSlug.Trim();
        var respawn = draft.RespawnEnabled
            ? new EncounterWorldProfile.RespawnPolicy(draft.DelaySeconds, draft.MaximumCycles,
                draft.Condition.Trim(), draft.ResetWhenEmpty, draft.Persistent, draft.AntiFarmMultiplier)
            : null;
        var scheduler = draft.SchedulerEnabled
            ? new EncounterWorldProfile.SchedulerPolicy(draft.HeartbeatEnabled, draft.IntervalSeconds,
                draft.BudgetPerMinute, draft.CustomEventNamespace.Trim())
            : null;
        var candidateProfile = currentProfile with
        {
            Respawn = respawn,
            SpawnFactionId = factionSlug,
            Scheduler = scheduler,
        };
        candidate = candidateProfile;
        var source = project.Module.WorldFeatures;
        var catalog = ReplaceEncounterUx2LwI(source, candidateProfile);
        var candidateProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(catalog), project.Blueprints, project.Areas,
            project.Dialogues, project.Quests);
        var path = $"module/worldFeatures/encounters/{candidateProfile.Id}";
        var issues = EncounterLifecycleRuntimeService.Validate(candidateProfile, project.Module.Factions)
            .Concat(WorldFeatureCatalogValidator.Validate(candidateProject)
                .Where(item => item.Path.StartsWith(path, StringComparison.Ordinal)))
            .DistinctBy(item => (item.Code, item.Path))
            .Select(item => new StudioUx2LwIDiagnostic(item.Code, item.Path,
                HumanEncounterLifecycleDiagnosticUx2LwI(item.Code)))
            .ToArray();
        if (issues.Length > 0)
        {
            diagnostics = issues;
            return false;
        }
        command = new(CommandId.From(Guid.NewGuid()), history.State.Revision, catalog, dryRun);
        return true;
    }

    private static WorldFeatureCatalog ReplaceEncounterUx2LwI(
        WorldFeatureCatalog source,
        EncounterWorldProfile candidate)
    {
        var encounters = source.Encounters.Select(item => item.Id == candidate.Id ? candidate : item).ToArray();
        return new(source.Id, source.SchemaVersion, source.Provenance,
            source.Placeables, source.Doors, source.Triggers, encounters, source.VehicleRoutes,
            source.Merchants, source.Companions, source.Scripts);
    }

    private static bool IsEligibleEncounterLifecycleUx2LwI(EncounterWorldProfile? profile) =>
        profile?.Foundation is not null;

    private static IReadOnlyList<StudioUx2LwIDiff> BuildEncounterLifecycleDiffUx2LwI(
        EncounterWorldProfile before,
        EncounterWorldProfile? after)
    {
        if (after is null) return [];
        return
        [
            DiffUx2LwI("Respawn", RespawnSummaryUx2LwI(before.Respawn), RespawnSummaryUx2LwI(after.Respawn)),
            DiffUx2LwI("Faction de spawn", FactionSummaryUx2LwI(before.SpawnFactionId),
                FactionSummaryUx2LwI(after.SpawnFactionId)),
            DiffUx2LwI("OnHeartbeat et OnUserDefined", SchedulerSummaryUx2LwI(before.Scheduler),
                SchedulerSummaryUx2LwI(after.Scheduler)),
        ];
    }

    private static StudioUx2LwIDiff DiffUx2LwI(string section, string before, string after) =>
        new(section, before, after, !string.Equals(before, after, StringComparison.Ordinal));

    private static StudioUx2LwIRuntimePreview BuildEncounterLifecycleRuntimeUx2LwI(
        EncounterWorldProfile profile,
        FactionCatalog factions)
    {
        var respawnPolicy = profile.Respawn;
        var schedulerPolicy = profile.Scheduler;
        var respawn = EncounterLifecycleRuntimeService.ResolveRespawn(profile, factions,
            new(0, respawnPolicy?.DelaySeconds ?? 0, false, false,
                respawnPolicy is { } policy
                    ? new HashSet<string>([policy.Condition], StringComparer.Ordinal)
                    : new HashSet<string>(StringComparer.Ordinal),
                true, true));
        var heartbeat = EncounterLifecycleRuntimeService.ResolveScheduledEvent(profile,
            new("heartbeat", schedulerPolicy?.IntervalSeconds ?? 0, 0, true, true));
        var customName = schedulerPolicy is { } scheduler
            ? $"custom:{scheduler.CustomEventNamespace}:preview"
            : "custom:unavailable:preview";
        var custom = EncounterLifecycleRuntimeService.ResolveScheduledEvent(profile,
            new(customName, 0, 0, true, true));
        return new(
            respawnPolicy is null
                ? "Non évalué · aucune politique optionnelle"
                : $"{(respawn.Respawn ? "autorisé" : "refusé")} · cycle {respawn.NextCycle} · " +
                  $"multiplicateur anti-farm {respawn.RewardMultiplier:0.###} · raison {HumanTokenUx2LwI(respawn.Reason)}",
            profile.SpawnFactionId is null
                ? "Héritée · aucune affectation d’acteur exécutée"
                : $"{HumanTokenUx2LwI(respawn.SpawnFactionSlug)} · projection de faction réelle, sans affectation d’acteur",
            schedulerPolicy is null
                ? "Non évalué · aucun scheduler optionnel"
                : $"{(heartbeat.Dispatch ? "autorisé" : "refusé")} · " +
                  $"{schedulerPolicy.IntervalSeconds:0.###} s · budget {schedulerPolicy.BudgetPerMinute}/min · " +
                  $"raison {HumanTokenUx2LwI(heartbeat.Reason)}",
            schedulerPolicy is null
                ? "Non évalué · aucun espace de noms"
                : $"{(custom.Dispatch ? "autorisé" : "refusé")} · {customName} · " +
                  $"raison {HumanTokenUx2LwI(custom.Reason)}",
            "Décisions de politique seulement : aucun timer moteur, spawn d’acteur, ForgeScript, récompense, " +
            "affectation de faction ou transport réseau n’est exécuté par cet aperçu.");
    }

    private static StudioUx2LwIRuntimePreview EmptyEncounterLifecycleRuntimeUx2LwI() => new(
        "Respawn non évalué", "Faction non évaluée", "Heartbeat non évalué", "Événement custom non évalué",
        "Aucun timer moteur, spawn, ForgeScript, loot, faction ou réseau n’a été exécuté.");

    private static StudioUx2LwIPreview FailedEncounterLifecyclePreviewUx2LwI(string message) => new(
        false, false, message, string.Empty, [], [], EmptyEncounterLifecycleRuntimeUx2LwI(), true);

    private static string RespawnSummaryUx2LwI(EncounterWorldProfile.RespawnPolicy? policy) => policy is null
        ? "Absent · fallback sûr"
        : $"{policy.DelaySeconds:0.###} s · {policy.MaximumCycles} cycle(s) · condition " +
          $"{HumanTokenUx2LwI(policy.Condition)} · reset vide {YesNoUx2LwI(policy.ResetWhenEmpty)} · " +
          $"persistant {YesNoUx2LwI(policy.Persistent)} · anti-farm {policy.AntiFarmMultiplier:0.###}";

    private static string FactionSummaryUx2LwI(string? slug) => slug is null
        ? "Héritée du blueprint ou du runtime"
        : $"Faction réelle {HumanTokenUx2LwI(slug)}";

    private static string SchedulerSummaryUx2LwI(EncounterWorldProfile.SchedulerPolicy? policy) => policy is null
        ? "Absent · fallback sûr"
        : $"heartbeat {OnOffUx2LwI(policy.HeartbeatEnabled)} · {policy.IntervalSeconds:0.###} s · " +
          $"budget {policy.BudgetPerMinute}/min · namespace {HumanTokenUx2LwI(policy.CustomEventNamespace)}";

    internal static string HumanEncounterLifecycleDiagnosticUx2LwI(string? code) => code switch
    {
        EncounterLifecycleRuntimeService.RespawnInvalidCode =>
            "Le respawn exige un délai fini, des cycles positifs, une condition sûre et un anti-farm entre 0 et 1.",
        EncounterLifecycleRuntimeService.FactionMissingCode =>
            "Choisissez une faction réelle, active et toujours présente dans le module.",
        EncounterLifecycleRuntimeService.SchedulerInvalidCode =>
            "Le scheduler exige une cadence finie, un budget positif et un espace de noms sûr.",
        EncounterLifecycleRuntimeService.EventInvalidCode =>
            "L’événement custom doit rester dans l’espace de noms configuré.",
        "world.encounter.foundation_required" => "Terminez d’abord la fiche Foundation de la rencontre.",
        "command.revision_conflict" or "command.source_changed" =>
            "Le projet a changé. Rechargez la rencontre et prévisualisez à nouveau.",
        null or "" => "La commande canonique a refusé la modification.",
        _ => "La politique ne respecte pas encore le contrat canonique.",
    };

    private static string ProjectFingerprintUx2LwI(ProjectDefinition project)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteFingerprintFieldUx2LwI(writer, "project-json");
        WriteFingerprintFieldUx2LwI(writer, ProjectJsonSerializer.Serialize(project));
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static string EncounterLifecycleDraftFingerprintUx2LwI(StudioUx2LwIDraft draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteFingerprintFieldUx2LwI(writer, "ux2-lw-i-draft-v1");
        WriteFingerprintFieldUx2LwI(writer, draft.ProfileId.ToString("D"));
        writer.Write(draft.BaseRevision.Value);
        WriteFingerprintFieldUx2LwI(writer, draft.SourceFingerprint);
        writer.Write(draft.RespawnEnabled);
        writer.Write(draft.DelaySeconds);
        writer.Write(draft.MaximumCycles);
        WriteFingerprintFieldUx2LwI(writer, draft.Condition);
        writer.Write(draft.ResetWhenEmpty);
        writer.Write(draft.Persistent);
        writer.Write(draft.AntiFarmMultiplier);
        WriteFingerprintFieldUx2LwI(writer, draft.SpawnFactionSlug);
        writer.Write(draft.SchedulerEnabled);
        writer.Write(draft.HeartbeatEnabled);
        writer.Write(draft.IntervalSeconds);
        writer.Write(draft.BudgetPerMinute);
        WriteFingerprintFieldUx2LwI(writer, draft.CustomEventNamespace);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteFingerprintFieldUx2LwI(BinaryWriter writer, string? value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string OptionalStateUx2LwI(object? value) => value is null ? "absent" : "configuré";
    private static string YesNoUx2LwI(bool value) => value ? "oui" : "non";
    private static string OnOffUx2LwI(bool value) => value ? "actif" : "dormant";
    private static string HumanEncounterNameUx2LwI(EncounterWorldProfile profile) =>
        profile.Composition?.DisplayName ?? $"Rencontre {profile.Id.ToString("N")[..6].ToUpperInvariant()}";
    private static string HumanTokenUx2LwI(string? value) => string.IsNullOrWhiteSpace(value)
        ? "aucun"
        : value.Replace('-', ' ').Replace('_', ' ').Replace('.', ' ').Replace(':', ' ').Trim();
    private static string HumanProvenanceUx2LwI(string? value) => string.IsNullOrWhiteSpace(value)
        ? "provenance non renseignée"
        : value.StartsWith("studio:", StringComparison.Ordinal) ? "créée dans le Studio" : "source module vérifiée";

    private static Guid StableUx2LwI(Guid seed, string purpose)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"ux2-lw-i:{seed:D}:{purpose}"));
        return new(bytes[..16]);
    }

    internal StudioUx2LwISmokeResult RunUx2LwIContractSmoke()
    {
        var fixture = CreateUx2LwISmokeProject();
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-lw-i-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(tempRoot, fixture.Project).Succeeded || !editing.Open(tempRoot).Succeeded ||
                !editing.SelectArea(fixture.AreaId))
                return FailedUx2LwISmoke();
            var loadedProject = editing.Project!;
            var initialCatalog = loadedProject.Module.WorldFeatures;
            var loadedTarget = initialCatalog.Encounters.Single(item => item.Id == fixture.TargetId);
            var loadedNeighbor = initialCatalog.Encounters.Single(item => item.Id == fixture.NeighborId);
            var explicitSelection = editing.CreateEncounterLifecycleDraftUx2LwI(null) is null;
            var incomplete = loadedTarget with { Foundation = null };
            var foundationRequired = !IsEligibleEncounterLifecycleUx2LwI(incomplete) &&
                editing.CreateEncounterLifecycleDraftUx2LwI(fixture.TargetId) is not null &&
                editing.EncounterChoicesUx2LwI.Single(item => item.ProfileId == fixture.TargetId).IsComplete;
            var draft = editing.CreateEncounterLifecycleDraftUx2LwI(fixture.TargetId);
            if (draft is null) return FailedUx2LwISmoke();

            var optional = editing.PreviewEncounterLifecycleUx2LwI(draft with
            {
                RespawnEnabled = false,
                SpawnFactionSlug = null,
                SchedulerEnabled = false,
            });
            var optionalFallback = optional.Succeeded &&
                optional.Runtime.Respawn.Contains("Non évalué", StringComparison.Ordinal) &&
                optional.Runtime.Heartbeat.Contains("Non évalué", StringComparison.Ordinal);
            var invalidDelay = editing.PreviewEncounterLifecycleUx2LwI(draft with { DelaySeconds = double.NaN });
            var invalidCycles = editing.PreviewEncounterLifecycleUx2LwI(draft with { MaximumCycles = -1 });
            var invalidCondition = editing.PreviewEncounterLifecycleUx2LwI(draft with { Condition = "custom::" });
            var invalidAntiFarm = editing.PreviewEncounterLifecycleUx2LwI(draft with { AntiFarmMultiplier = 1.1 });
            var respawnValidation = new[] { invalidDelay, invalidCycles, invalidCondition, invalidAntiFarm }
                .All(item => !item.Succeeded && item.Diagnostics.Any(diagnostic =>
                    diagnostic.Code == EncounterLifecycleRuntimeService.RespawnInvalidCode));
            var invalidFaction = editing.PreviewEncounterLifecycleUx2LwI(draft with
            { SpawnFactionSlug = "faction-inconnue" });
            var factionValidation = !invalidFaction.Succeeded && invalidFaction.Diagnostics.Any(item =>
                item.Code == EncounterLifecycleRuntimeService.FactionMissingCode);
            var invalidCadence = editing.PreviewEncounterLifecycleUx2LwI(draft with { IntervalSeconds = 0 });
            var invalidBudget = editing.PreviewEncounterLifecycleUx2LwI(draft with { BudgetPerMinute = 0 });
            var invalidNamespace = editing.PreviewEncounterLifecycleUx2LwI(draft with
            { CustomEventNamespace = "custom::" });
            var schedulerValidation = new[] { invalidCadence, invalidBudget, invalidNamespace }
                .All(item => !item.Succeeded && item.Diagnostics.Any(diagnostic =>
                    diagnostic.Code == EncounterLifecycleRuntimeService.SchedulerInvalidCode));

            var changed = draft with
            {
                DelaySeconds = draft.DelaySeconds + .75,
                MaximumCycles = draft.MaximumCycles + 1,
                AntiFarmMultiplier = .55,
                SpawnFactionSlug = fixture.SecondFactionSlug,
                HeartbeatEnabled = false,
            };
            var staleSource = draft;
            var changedBeforePreview = ApplyUx2LwIIntermediateMutation(editing, fixture.NeighborId, 1);
            var staleDraft = editing.PreviewEncounterLifecycleUx2LwI(staleSource);
            var staleDraftBlocked = changedBeforePreview && !staleDraft.Succeeded && editing.Undo().Succeeded;

            changed = editing.CreateEncounterLifecycleDraftUx2LwI(fixture.TargetId)! with
            {
                DelaySeconds = draft.DelaySeconds + .75,
                MaximumCycles = draft.MaximumCycles + 1,
                AntiFarmMultiplier = .55,
                SpawnFactionSlug = fixture.SecondFactionSlug,
                HeartbeatEnabled = false,
            };
            var approvalPreview = editing.PreviewEncounterLifecycleUx2LwI(changed);
            var changedAfterPreview = ApplyUx2LwIIntermediateMutation(editing, fixture.NeighborId, 2);
            var staleApply = editing.ApplyApprovedEncounterLifecycleUx2LwI();
            var staleApprovalBlocked = approvalPreview.Succeeded && changedAfterPreview && !staleApply.Succeeded &&
                                       editing.Undo().Succeeded;

            changed = editing.CreateEncounterLifecycleDraftUx2LwI(fixture.TargetId)! with
            {
                DelaySeconds = draft.DelaySeconds + .75,
                MaximumCycles = draft.MaximumCycles + 1,
                AntiFarmMultiplier = .55,
                SpawnFactionSlug = fixture.SecondFactionSlug,
                HeartbeatEnabled = false,
            };
            var preview = editing.PreviewEncounterLifecycleUx2LwI(changed);
            var dryRunPure = preview.Succeeded && preview.CanApply && preview.DryRunPure &&
                             ReferenceEquals(loadedProject, editing.Project);
            var runtimeResolution = preview.Runtime.Respawn.Contains("autorisé", StringComparison.OrdinalIgnoreCase) &&
                                    preview.Runtime.Faction.Contains("projection", StringComparison.OrdinalIgnoreCase) &&
                                    preview.Runtime.Heartbeat.Contains("refusé", StringComparison.OrdinalIgnoreCase) &&
                                    preview.Runtime.CustomEvent.Contains("autorisé", StringComparison.OrdinalIgnoreCase);
            var applied = editing.ApplyApprovedEncounterLifecycleUx2LwI();
            if (!applied.Succeeded) return FailedUx2LwISmoke();
            var appliedProject = editing.Project!;
            var appliedCatalog = appliedProject.Module.WorldFeatures;
            var appliedTarget = appliedCatalog.Encounters.Single(item => item.Id == fixture.TargetId);
            var appliedNeighbor = appliedCatalog.Encounters.Single(item => item.Id == fixture.NeighborId);
            var dormantPreserved = appliedTarget.Scheduler == loadedTarget.Scheduler! with { HeartbeatEnabled = false } &&
                                   ReferenceEquals(loadedNeighbor.Scheduler, appliedNeighbor.Scheduler);
            var targetNeighborsPreserved = loadedTarget.Id == appliedTarget.Id &&
                loadedTarget.SchemaVersion == appliedTarget.SchemaVersion && loadedTarget.AreaId == appliedTarget.AreaId &&
                ReferenceEquals(loadedTarget.Reinforcements, appliedTarget.Reinforcements) &&
                ReferenceEquals(loadedTarget.Visibility, appliedTarget.Visibility) &&
                ReferenceEquals(loadedTarget.Cleanup, appliedTarget.Cleanup) &&
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
            var collectionsPreserved = SameReferencesUx2LwI(initialCatalog.Placeables, appliedCatalog.Placeables) &&
                SameReferencesUx2LwI(initialCatalog.Doors, appliedCatalog.Doors) &&
                SameReferencesUx2LwI(initialCatalog.Triggers, appliedCatalog.Triggers) &&
                SameReferencesUx2LwI(initialCatalog.VehicleRoutes, appliedCatalog.VehicleRoutes) &&
                SameReferencesUx2LwI(initialCatalog.Merchants, appliedCatalog.Merchants) &&
                SameReferencesUx2LwI(initialCatalog.Companions, appliedCatalog.Companions) &&
                ReferenceEquals(initialCatalog.Scripts, appliedCatalog.Scripts);

            var undo = editing.Undo();
            var undoneProject = editing.Project!;
            var undone = undoneProject.Module.WorldFeatures.Encounters.Single(item => item.Id == fixture.TargetId);
            var redo = editing.Redo();
            var redoneProject = editing.Project!;
            var redone = redoneProject.Module.WorldFeatures.Encounters.Single(item => item.Id == fixture.TargetId);
            var undoRedo = undo.Succeeded && redo.Succeeded &&
                EncounterProfileJsonUx2LwI(undone, undoneProject) ==
                EncounterProfileJsonUx2LwI(loadedTarget, loadedProject) &&
                EncounterProfileJsonUx2LwI(redone, redoneProject) ==
                EncounterProfileJsonUx2LwI(appliedTarget, appliedProject);
            var saved = editing.Save();
            var opened = saved.Succeeded && reopened.Open(tempRoot).Succeeded;
            var persisted = opened ? reopened.Project!.Module.WorldFeatures.Encounters.Single(item =>
                item.Id == fixture.TargetId) : null;
            var persistence = persisted is not null && persisted.Respawn == appliedTarget.Respawn &&
                persisted.SpawnFactionId == appliedTarget.SpawnFactionId &&
                persisted.Scheduler == appliedTarget.Scheduler &&
                ProjectJsonSerializer.Serialize(editing.Project!) == ProjectJsonSerializer.Serialize(reopened.Project!);
            reopened.Close();
            var canonicalCommand = editing.History!.Journal.Count >= 3 &&
                                   editing.History.Journal.All(item => item.CommandName == "set_world_feature_catalog");
            var structuredDiagnostics = new[]
                { invalidDelay, invalidCycles, invalidCondition, invalidAntiFarm, invalidFaction,
                    invalidCadence, invalidBudget, invalidNamespace }
                .SelectMany(item => item.Diagnostics)
                .All(item => !string.IsNullOrWhiteSpace(item.Code) && !string.IsNullOrWhiteSpace(item.Path));
            var humanText = editing.EncounterChoicesUx2LwI.All(item => IsHumanUx2LwIText(item.Label)) &&
                            editing.EncounterFactionChoicesUx2LwI().All(item =>
                                IsHumanUx2LwIText(item.Label) && IsHumanUx2LwIText(item.Provenance));
            var succeeded = explicitSelection && foundationRequired && optionalFallback && respawnValidation &&
                            factionValidation && schedulerValidation && runtimeResolution && dormantPreserved &&
                            dryRunPure && staleDraftBlocked && staleApprovalBlocked && canonicalCommand && undoRedo &&
                            persistence && targetNeighborsPreserved && projectNeighborsPreserved &&
                            collectionsPreserved && structuredDiagnostics && humanText;
            return new(succeeded, explicitSelection, foundationRequired, optionalFallback, respawnValidation,
                factionValidation, schedulerValidation, runtimeResolution, dormantPreserved, dryRunPure,
                staleDraftBlocked, staleApprovalBlocked, canonicalCommand, undoRedo, persistence,
                targetNeighborsPreserved, projectNeighborsPreserved, collectionsPreserved,
                structuredDiagnostics, humanText);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_LW_I_CONTRACT_FAILED exception={exception.Message}");
            return FailedUx2LwISmoke();
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
                Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_LW_I_CLEANUP_FAILED " + exception.Message);
            }
        }
    }

    private static bool ApplyUx2LwIIntermediateMutation(StudioSessionController session, Guid profileId, int delta)
    {
        var history = session.History;
        var project = session.Project;
        if (history is null || project is null) return false;
        var source = project.Module.WorldFeatures;
        var neighbor = source.Encounters.Single(item => item.Id == profileId);
        var updated = neighbor with
        {
            Cleanup = neighbor.Cleanup with
            { AbandonedAfterSeconds = neighbor.Cleanup.AbandonedAfterSeconds + delta },
        };
        var catalog = ReplaceEncounterUx2LwI(source, updated);
        var result = new SetWorldFeatureCatalogHandler(new SystemClock()).Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, catalog, false));
        if (!result.Succeeded) return false;
        history.Record(result);
        return true;
    }

    private static Ux2LwISmokeFixture CreateUx2LwISmokeProject()
    {
        var fixture = CreateUx2LwFSmokeProject();
        var firstFaction = new FactionDefinition(StableUx2LwI(fixture.Project.Module.Id.Value, "faction-one"), 1,
            Ux2LwIEvidenceProvenance, "veilleurs-braises", "Veilleurs des braises", false, [], ["garde"]);
        var secondFaction = new FactionDefinition(StableUx2LwI(fixture.Project.Module.Id.Value, "faction-two"), 1,
            Ux2LwIEvidenceProvenance, "pisteurs-cendre", "Pisteurs de cendre", false, [], ["scout"]);
        var source = fixture.Project.Module.WorldFeatures;
        var target = source.Encounters.Single(item => item.Id == fixture.TargetId) with
        {
            Respawn = new(30.25, 3, "area.ready", true, true, .7),
            SpawnFactionId = firstFaction.Slug,
            Scheduler = new(true, 7.25, 13, "veille.braises"),
        };
        var neighbor = source.Encounters.Single(item => item.Id == fixture.NeighborId) with
        {
            Respawn = new(41.5, 2, "area.ready", false, false, .9),
            SpawnFactionId = secondFaction.Slug,
            Scheduler = new(false, 11.75, 17, "voisin.dormant"),
        };
        var encounters = source.Encounters.Select(item => item.Id == target.Id ? target :
            item.Id == neighbor.Id ? neighbor : item).ToArray();
        var catalog = new WorldFeatureCatalog(source.Id, source.SchemaVersion, source.Provenance,
            source.Placeables, source.Doors, source.Triggers, encounters, source.VehicleRoutes,
            source.Merchants, source.Companions, source.Scripts);
        var factions = fixture.Project.Module.Factions with { Factions = [firstFaction, secondFaction] };
        var module = fixture.Project.Module.WithFactions(factions).WithWorldFeatures(catalog);
        var project = new ProjectDefinition(fixture.Project.Id, "Studio UX2-LW-I", module,
            fixture.Project.Blueprints, fixture.Project.Areas, fixture.Project.Dialogues, fixture.Project.Quests);
        return new(project, fixture.AreaId, fixture.TargetId, fixture.NeighborId, secondFaction.Slug);
    }

    private static string EncounterProfileJsonUx2LwI(EncounterWorldProfile profile, ProjectDefinition project)
    {
        var source = project.Module.WorldFeatures;
        var catalog = new WorldFeatureCatalog(source.Id, source.SchemaVersion, source.Provenance,
            source.Placeables, source.Doors, source.Triggers, [profile], source.VehicleRoutes,
            source.Merchants, source.Companions, source.Scripts);
        return ProjectJsonSerializer.Serialize(new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(catalog), project.Blueprints, project.Areas,
            project.Dialogues, project.Quests));
    }

    private static bool SameReferencesUx2LwI<T>(IReadOnlyList<T> left, IReadOnlyList<T> right)
        where T : class => left.Count == right.Count &&
                           left.Zip(right).All(pair => ReferenceEquals(pair.First, pair.Second));

    private static bool IsHumanUx2LwIText(string value) =>
        !string.IsNullOrWhiteSpace(value) && !value.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !value.Contains("module/worldFeatures", StringComparison.Ordinal) &&
        !value.Split([' ', '·', '/', ':', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Any(token => Guid.TryParse(token, out _));

    private static StudioUx2LwISmokeResult FailedUx2LwISmoke() => new(
        false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false);

    private sealed record Ux2LwISmokeFixture(
        ProjectDefinition Project,
        Forge.Domain.Areas.AreaId AreaId,
        Guid TargetId,
        Guid NeighborId,
        string SecondFactionSlug);
}
