using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Application.WorldFeatures;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Encounters;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2LwKEvidenceProvenance = "studio:ux2-lw-k:evidence";
    private const string Ux2LwKAuthoringProvenance = "studio:ux2-lw-k:authoring";
    private readonly SetWorldFeatureCatalogHandler _setEncounterStatisticsCatalogUx2LwK = new(new SystemClock());
    private StudioUx2LwKDraft? _approvedEncounterStatisticsUx2LwK;
    private ProjectRevision? _approvedEncounterStatisticsRevisionUx2LwK;
    private string? _approvedEncounterStatisticsSourceFingerprintUx2LwK;
    private string? _approvedEncounterStatisticsDraftFingerprintUx2LwK;

    internal IReadOnlyList<StudioUx2LwKEncounterChoice> EncounterChoicesUx2LwK
    {
        get
        {
            var project = Project;
            var area = ActiveArea;
            if (project is null || area is null) return [];
            return project.Module.WorldFeatures.Encounters
                .Where(item => item.AreaId == area.Id)
                .Select(item => new StudioUx2LwKEncounterChoice(item.Id,
                    $"{HumanEncounterNameUx2LwK(item)} · " +
                    (IsEligibleEncounterStatisticsUx2LwK(item)
                        ? item.Statistics is null
                            ? "profil statistique à créer"
                            : $"{item.Statistics.Runs} simulations · " +
                              $"{item.Statistics.Percentiles.Count} percentile(s)"
                        : "Foundation et composition requises"),
                    IsEligibleEncounterStatisticsUx2LwK(item), item.Statistics is not null,
                    string.Equals(item.Statistics?.Provenance, Ux2LwKEvidenceProvenance,
                        StringComparison.Ordinal)))
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal StudioUx2LwKDraft? CreateEncounterStatisticsDraftUx2LwK(Guid? profileId)
    {
        var history = History;
        var project = Project;
        var area = ActiveArea;
        if (history is null || project is null || area is null || profileId is null) return null;
        var profile = project.Module.WorldFeatures.Encounters.SingleOrDefault(item =>
            item.Id == profileId && item.AreaId == area.Id);
        if (!IsEligibleEncounterStatisticsUx2LwK(profile)) return null;
        var selected = profile!;
        var statistics = selected.Statistics;
        return new(selected.Id, history.State.Revision, ProjectFingerprintUx2LwK(project),
            statistics?.Id ?? StableUx2LwK(selected.Id, "statistics"),
            statistics?.SchemaVersion ?? EncounterStatisticsProfile.CurrentSchemaVersion,
            statistics?.Provenance ?? Ux2LwKAuthoringProvenance,
            statistics?.Runs ?? 256,
            statistics?.BaseSeed ?? selected.Composition!.Selection.DeterministicSeed,
            statistics?.DurationEstimatorId ?? "duration:actors-linear",
            statistics?.SecondsPerActor ?? 5,
            statistics?.RewardEstimatorId ?? "reward:cost-linear",
            statistics?.RewardPerCost ?? .5,
            statistics?.Percentiles.ToArray() ?? [.5, .9, .99],
            statistics?.IncludeRunDetails ?? false,
            statistics?.Overrides.ToDictionary(item => item.Key, item => item.Value,
                StringComparer.Ordinal) ?? new Dictionary<string, string>(StringComparer.Ordinal),
            selected.StatisticsRequiredAtRuntime, selected);
    }

    internal StudioUx2LwKPreview PreviewEncounterStatisticsUx2LwK(StudioUx2LwKDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelEncounterStatisticsPreviewUx2LwK();
        var history = History;
        var project = Project;
        if (history is null || project is null)
            return FailedEncounterStatisticsPreviewUx2LwK("Aucun module n’est ouvert.");
        if (draft.BaseRevision != history.State.Revision)
            return FailedEncounterStatisticsPreviewUx2LwK(
                "Le projet a changé depuis l’ouverture de la fiche. Rechargez la rencontre.");
        if (!string.Equals(draft.SourceFingerprint, ProjectFingerprintUx2LwK(project), StringComparison.Ordinal))
            return FailedEncounterStatisticsPreviewUx2LwK(
                "Le catalogue a changé. Rechargez la rencontre avant la simulation.");

        var sourceProject = project;
        var sourceRevision = history.State.Revision;
        var sourceJournalCount = history.Journal.Count;
        if (!TryBuildEncounterStatisticsCandidateUx2LwK(draft, true, out var command,
                out var candidate, out var diagnostics))
            return new(false, false,
                diagnostics.FirstOrDefault()?.Message ?? "Le profil statistique contient une valeur invalide.",
                string.Empty, BuildEncounterStatisticsDiffUx2LwK(draft.OriginalProfile, candidate), diagnostics,
                null, true);

        var result = _setEncounterStatisticsCatalogUx2LwK.Handle(history.State, command!);
        if (!result.Succeeded)
        {
            var commandDiagnostics = result.Diagnostics.Select(item => new StudioUx2LwKDiagnostic(
                item.Code, $"module/worldFeatures/encounters/{draft.ProfileId:D}/statistics",
                HumanEncounterStatisticsDiagnosticUx2LwK(item.Code))).ToArray();
            return new(false, false,
                commandDiagnostics.FirstOrDefault()?.Message ?? "La commande canonique a refusé le profil.",
                result.Patch?.Summary ?? string.Empty,
                BuildEncounterStatisticsDiffUx2LwK(draft.OriginalProfile, candidate), commandDiagnostics,
                null, true);
        }

        StudioUx2LwKSimulation simulation;
        try
        {
            simulation = BuildEncounterStatisticsSimulationUx2LwK(
                EncounterCompositionFoundationService.PreviewStatistics(candidate!), project);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            var diagnostic = new StudioUx2LwKDiagnostic("world.encounter.statistics_simulation_invalid",
                $"module/worldFeatures/encounters/{draft.ProfileId:D}/statistics",
                "La simulation déterministe a refusé ce profil. Corrigez les paramètres avant application.");
            return new(false, false, diagnostic.Message, result.Patch?.Summary ?? string.Empty,
                BuildEncounterStatisticsDiffUx2LwK(draft.OriginalProfile, candidate), [diagnostic], null, true);
        }

        var diff = BuildEncounterStatisticsDiffUx2LwK(draft.OriginalProfile, candidate);
        var canApply = diff.Any(item => item.Changed);
        var dryRunPure = ReferenceEquals(sourceProject, Project) && history.State.Revision == sourceRevision &&
                         history.Journal.Count == sourceJournalCount;
        if (canApply && dryRunPure)
        {
            _approvedEncounterStatisticsUx2LwK = draft;
            _approvedEncounterStatisticsRevisionUx2LwK = history.State.Revision;
            _approvedEncounterStatisticsSourceFingerprintUx2LwK = draft.SourceFingerprint;
            _approvedEncounterStatisticsDraftFingerprintUx2LwK =
                EncounterStatisticsDraftFingerprintUx2LwK(draft);
        }
        return new(true, canApply,
            canApply
                ? "Simulation offline validée · le document n’a pas encore été modifié."
                : "Simulation offline validée · ce profil est déjà identique au document canonique.",
            result.Patch?.Summary ?? string.Empty, diff, [], simulation, dryRunPure);
    }

    internal StudioUx2LwKActionResult ApplyApprovedEncounterStatisticsUx2LwK()
    {
        var history = History;
        var project = Project;
        var draft = _approvedEncounterStatisticsUx2LwK;
        if (history is null || project is null || draft is null ||
            _approvedEncounterStatisticsRevisionUx2LwK != history.State.Revision ||
            !string.Equals(_approvedEncounterStatisticsSourceFingerprintUx2LwK,
                ProjectFingerprintUx2LwK(project), StringComparison.Ordinal) ||
            !string.Equals(_approvedEncounterStatisticsDraftFingerprintUx2LwK,
                EncounterStatisticsDraftFingerprintUx2LwK(draft), StringComparison.Ordinal))
        {
            CancelEncounterStatisticsPreviewUx2LwK();
            return new(false, "Relancez la simulation avant d’appliquer ce profil.");
        }
        if (!TryBuildEncounterStatisticsCandidateUx2LwK(draft, false, out var command, out _,
                out var diagnostics))
        {
            CancelEncounterStatisticsPreviewUx2LwK();
            return new(false, diagnostics.FirstOrDefault()?.Message ?? "Le profil statistique est invalide.");
        }
        var result = _setEncounterStatisticsCatalogUx2LwK.Handle(history.State, command!);
        CancelEncounterStatisticsPreviewUx2LwK();
        if (!result.Succeeded)
            return new(false, HumanEncounterStatisticsDiagnosticUx2LwK(result.Diagnostics.FirstOrDefault()?.Code));
        history.Record(result);
        InvalidateContentIndexes();
        return new(true, "Profil statistique appliqué · Ctrl+Z pour annuler, Ctrl+Y pour rétablir.");
    }

    internal void CancelEncounterStatisticsPreviewUx2LwK()
    {
        _approvedEncounterStatisticsUx2LwK = null;
        _approvedEncounterStatisticsRevisionUx2LwK = null;
        _approvedEncounterStatisticsSourceFingerprintUx2LwK = null;
        _approvedEncounterStatisticsDraftFingerprintUx2LwK = null;
    }

    internal StudioUx2LwKApprovalSnapshot CaptureEncounterStatisticsApprovalUx2LwK() => new(
        _approvedEncounterStatisticsUx2LwK, _approvedEncounterStatisticsRevisionUx2LwK,
        _approvedEncounterStatisticsSourceFingerprintUx2LwK,
        _approvedEncounterStatisticsDraftFingerprintUx2LwK);

    internal void RestoreEncounterStatisticsApprovalUx2LwK(StudioUx2LwKApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedEncounterStatisticsUx2LwK = snapshot.Draft;
        _approvedEncounterStatisticsRevisionUx2LwK = snapshot.Revision;
        _approvedEncounterStatisticsSourceFingerprintUx2LwK = snapshot.SourceFingerprint;
        _approvedEncounterStatisticsDraftFingerprintUx2LwK = snapshot.DraftFingerprint;
    }

    internal void CloseUx2LwK() => CancelEncounterStatisticsPreviewUx2LwK();

    internal ProjectDefinition? CreateUx2LwKEvidenceProject()
    {
        var source = CreateUx2LwFEvidenceProject();
        if (source is null) return null;
        var target = source.Module.WorldFeatures.Encounters.FirstOrDefault(IsEligibleEncounterStatisticsUx2LwK);
        if (target is null) return null;
        var statistics = new EncounterStatisticsProfile(StableUx2LwK(target.Id, "evidence-statistics"),
            EncounterStatisticsProfile.CurrentSchemaVersion, Ux2LwKEvidenceProvenance,
            64, 2026, "duration:actors-linear", 4.5, "reward:cost-linear", .6,
            [.25, .5, .9], true,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["audience"] = "studio",
                ["mode"] = "offline-deterministic",
            });
        var authored = target with { Statistics = statistics, StatisticsRequiredAtRuntime = true };
        var catalog = ReplaceEncounterUx2LwK(source.Module.WorldFeatures, authored);
        return new(source.Id, source.Title, source.Module.WithWorldFeatures(catalog), source.Blueprints,
            source.Areas, source.Dialogues, source.Quests);
    }

    private bool TryBuildEncounterStatisticsCandidateUx2LwK(
        StudioUx2LwKDraft draft,
        bool dryRun,
        out SetWorldFeatureCatalogCommand? command,
        out EncounterWorldProfile? candidate,
        out IReadOnlyList<StudioUx2LwKDiagnostic> diagnostics)
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
        if (!string.Equals(draft.SourceFingerprint, ProjectFingerprintUx2LwK(project), StringComparison.Ordinal))
        {
            diagnostics = [new("command.source_changed", "module/worldFeatures/encounters",
                "Le catalogue de rencontres a changé. Rechargez la fiche.")];
            return false;
        }
        var current = project.Module.WorldFeatures.Encounters.SingleOrDefault(item => item.Id == draft.ProfileId);
        if (!IsEligibleEncounterStatisticsUx2LwK(current))
        {
            diagnostics = [new("world.encounter.statistics_dependencies_required",
                $"module/worldFeatures/encounters/{draft.ProfileId:D}",
                "Terminez d’abord Foundation et composition pour cette rencontre.")];
            return false;
        }
        if ((long)draft.BaseSeed + draft.Runs - 1 > int.MaxValue)
        {
            diagnostics = [new("world.encounter.statistics_seed_range_invalid",
                $"module/worldFeatures/encounters/{draft.ProfileId:D}/statistics/baseSeed",
                "La graine et le nombre de simulations dépassent la plage déterministe autorisée.")];
            return false;
        }

        var statistics = new EncounterStatisticsProfile(draft.StatisticsId, draft.StatisticsSchemaVersion,
            draft.StatisticsProvenance, draft.Runs, draft.BaseSeed, draft.DurationEstimatorId.Trim(),
            draft.SecondsPerActor, draft.RewardEstimatorId.Trim(), draft.RewardPerCost,
            draft.Percentiles.ToArray(), draft.IncludeRunDetails,
            draft.Overrides.OrderBy(item => item.Key, StringComparer.Ordinal)
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal));
        var currentProfile = current!;
        var candidateProfile = currentProfile with
        {
            Statistics = statistics,
            StatisticsRequiredAtRuntime = draft.RequiredAtRuntime,
        };
        candidate = candidateProfile;
        var source = project.Module.WorldFeatures;
        var catalog = ReplaceEncounterUx2LwK(source, candidateProfile);
        var candidateProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(catalog), project.Blueprints, project.Areas,
            project.Dialogues, project.Quests);
        var path = $"module/worldFeatures/encounters/{candidateProfile.Id}/statistics";
        var issues = EncounterStatisticsProfileValidator.Validate(statistics, path)
            .Concat(WorldFeatureCatalogValidator.Validate(candidateProject)
                .Where(item => item.Path.StartsWith($"module/worldFeatures/encounters/{candidateProfile.Id}",
                    StringComparison.Ordinal)))
            .DistinctBy(item => (item.Code, item.Path))
            .Select(item => new StudioUx2LwKDiagnostic(item.Code, item.Path,
                HumanEncounterStatisticsDiagnosticUx2LwK(item.Code)))
            .ToArray();
        if (issues.Length > 0)
        {
            diagnostics = issues;
            return false;
        }
        command = new(CommandId.From(Guid.NewGuid()), history.State.Revision, catalog, dryRun);
        return true;
    }

    private static WorldFeatureCatalog ReplaceEncounterUx2LwK(
        WorldFeatureCatalog source,
        EncounterWorldProfile candidate)
    {
        var encounters = source.Encounters.Select(item => item.Id == candidate.Id ? candidate : item).ToArray();
        return new(source.Id, source.SchemaVersion, source.Provenance,
            source.Placeables, source.Doors, source.Triggers, encounters, source.VehicleRoutes,
            source.Merchants, source.Companions, source.Scripts);
    }

    private static bool IsEligibleEncounterStatisticsUx2LwK(EncounterWorldProfile? profile) =>
        profile is { Foundation: not null, Composition: not null };

    private static IReadOnlyList<StudioUx2LwKDiff> BuildEncounterStatisticsDiffUx2LwK(
        EncounterWorldProfile before,
        EncounterWorldProfile? after)
    {
        if (after is null) return [];
        return
        [
            DiffUx2LwK("Échantillon et graine", SamplingSummaryUx2LwK(before.Statistics),
                SamplingSummaryUx2LwK(after.Statistics)),
            DiffUx2LwK("Durée estimée", DurationSummaryUx2LwK(before.Statistics),
                DurationSummaryUx2LwK(after.Statistics)),
            DiffUx2LwK("Récompense estimée", RewardSummaryUx2LwK(before.Statistics),
                RewardSummaryUx2LwK(after.Statistics)),
            DiffUx2LwK("Percentiles", PercentileSummaryUx2LwK(before.Statistics),
                PercentileSummaryUx2LwK(after.Statistics)),
            DiffUx2LwK("Détails et paramètres", DetailSummaryUx2LwK(before),
                DetailSummaryUx2LwK(after)),
        ];
    }

    private static StudioUx2LwKDiff DiffUx2LwK(string section, string before, string after) =>
        new(section, before, after, !string.Equals(before, after, StringComparison.Ordinal));

    private static StudioUx2LwKSimulation BuildEncounterStatisticsSimulationUx2LwK(
        EncounterStatisticalPreview preview,
        ProjectDefinition project)
    {
        var statistics = preview.Statistics;
        var names = project.Blueprints.ToDictionary(item => item.Id.ToString(),
            item => item.DisplayName, StringComparer.Ordinal);
        string HumanCreature(string value) => names.TryGetValue(value, out var name)
            ? name
            : "Créature référencée";
        var percentiles = (statistics.Percentiles ?? new Dictionary<double, Forge.RpgKit.Domain.Encounters.EncounterPercentile>())
            .OrderBy(item => item.Key)
            .Select(item => new StudioUx2LwKPercentile($"P{item.Key * 100:0.##}",
                item.Value.SpawnedCount, item.Value.Cost, item.Value.DurationSeconds, item.Value.RewardValue))
            .ToArray();
        var composition = statistics.CompositionTotals
            .OrderBy(item => HumanCreature(item.Key), StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new StudioUx2LwKComposition(HumanCreature(item.Key), item.Value,
                (double)item.Value / statistics.Runs))
            .ToArray();
        var displayed = statistics.Results.Take(12).Select((item, index) => new StudioUx2LwKRun(index + 1,
            item.Seed, item.SpawnedCount, item.Cost, item.DurationSeconds, item.RewardValue,
            item.Composition.Count == 0
                ? "Aucune créature retenue"
                : string.Join(", ", item.Composition
                    .OrderBy(entry => HumanCreature(entry.Key), StringComparer.CurrentCultureIgnoreCase)
                    .Select(entry => $"{HumanCreature(entry.Key)} × {entry.Value}"))))
            .ToArray();
        return new(statistics.Runs, preview.Profile.BaseSeed,
            checked(preview.Profile.BaseSeed + statistics.Runs - 1),
            statistics.MeanCount, statistics.MeanCost, statistics.MeanDurationSeconds,
            statistics.MeanRewardValue, statistics.CountVariance, statistics.CostVariance,
            statistics.DurationVariance, statistics.RewardVariance, percentiles, composition, displayed,
            Math.Max(0, statistics.Results.Count - displayed.Length), preview.Summary,
            "Simulation déterministe offline uniquement : aucun acteur n’est instancié, aucun spawn, combat, " +
            "loot, script, audio, rendu ou message réseau n’est exécuté.");
    }

    private static StudioUx2LwKPreview FailedEncounterStatisticsPreviewUx2LwK(string message) =>
        new(false, false, message, string.Empty, [], [], null, true);

    private static string SamplingSummaryUx2LwK(EncounterStatisticsProfile? profile) => profile is null
        ? "Absent · aucun calcul enregistré"
        : $"{profile.Runs} simulation(s) · graine {profile.BaseSeed}";

    private static string DurationSummaryUx2LwK(EncounterStatisticsProfile? profile) => profile is null
        ? "Absent"
        : $"{HumanTokenUx2LwK(profile.DurationEstimatorId)} · {profile.SecondsPerActor:0.###} s par acteur";

    private static string RewardSummaryUx2LwK(EncounterStatisticsProfile? profile) => profile is null
        ? "Absent"
        : $"{HumanTokenUx2LwK(profile.RewardEstimatorId)} · {profile.RewardPerCost:0.###} par coût";

    private static string PercentileSummaryUx2LwK(EncounterStatisticsProfile? profile) => profile is null
        ? "Absent"
        : string.Join(" · ", profile.Percentiles.Select(item => $"P{item * 100:0.##}"));

    private static string DetailSummaryUx2LwK(EncounterWorldProfile profile) => profile.Statistics is null
        ? $"Profil absent · requis au runtime {YesNoUx2LwK(profile.StatisticsRequiredAtRuntime)}"
        : $"détails par run {YesNoUx2LwK(profile.Statistics.IncludeRunDetails)} · " +
          $"{profile.Statistics.Overrides.Count} paramètre(s) · " +
          $"requis au runtime {YesNoUx2LwK(profile.StatisticsRequiredAtRuntime)}";

    internal static string HumanEncounterStatisticsDiagnosticUx2LwK(string? code) => code switch
    {
        "world.encounter.statistics_identity_invalid" =>
            "L’identité interne du profil statistique est invalide. Rechargez ou recréez le profil.",
        "world.encounter.statistics_configuration_invalid" =>
            "Choisissez 1 à 100 000 simulations, des estimateurs nommés et des coefficients finis autorisés.",
        "world.encounter.statistics_percentiles_invalid" =>
            "Saisissez 1 à 10 percentiles uniques, croissants et strictement compris entre 0 et 100 %.",
        "world.encounter.statistics_override_invalid" =>
            "Chaque paramètre avancé exige une clé et une valeur non vides.",
        "world.encounter.statistics_required" =>
            "Cette rencontre exige un profil statistique enregistré.",
        "world.encounter.statistics_seed_range_invalid" =>
            "La graine et le nombre de simulations dépassent la plage déterministe autorisée.",
        "world.encounter.statistics_dependencies_required" or "world.encounter.foundation_required" =>
            "Terminez d’abord Foundation et composition pour cette rencontre.",
        "command.revision_conflict" or "command.source_changed" =>
            "Le projet a changé. Rechargez la rencontre et relancez la simulation.",
        null or "" => "La commande canonique a refusé la modification.",
        _ => "Le profil ne respecte pas encore le contrat statistique canonique.",
    };

    private static string ProjectFingerprintUx2LwK(ProjectDefinition project)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteFingerprintFieldUx2LwK(writer, "project-json");
        WriteFingerprintFieldUx2LwK(writer, ProjectJsonSerializer.Serialize(project));
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static string EncounterStatisticsDraftFingerprintUx2LwK(StudioUx2LwKDraft draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteFingerprintFieldUx2LwK(writer, "ux2-lw-k-draft-v1");
        WriteFingerprintFieldUx2LwK(writer, draft.ProfileId.ToString("D"));
        writer.Write(draft.BaseRevision.Value);
        WriteFingerprintFieldUx2LwK(writer, draft.SourceFingerprint);
        WriteFingerprintFieldUx2LwK(writer, draft.StatisticsId.ToString("D"));
        writer.Write(draft.StatisticsSchemaVersion);
        WriteFingerprintFieldUx2LwK(writer, draft.StatisticsProvenance);
        writer.Write(draft.Runs);
        writer.Write(draft.BaseSeed);
        WriteFingerprintFieldUx2LwK(writer, draft.DurationEstimatorId);
        writer.Write(draft.SecondsPerActor);
        WriteFingerprintFieldUx2LwK(writer, draft.RewardEstimatorId);
        writer.Write(draft.RewardPerCost);
        foreach (var percentile in draft.Percentiles)
        {
            writer.Write((byte)1);
            writer.Write(percentile);
        }
        writer.Write((byte)0);
        writer.Write(draft.IncludeRunDetails);
        foreach (var item in draft.Overrides.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            writer.Write((byte)1);
            WriteFingerprintFieldUx2LwK(writer, item.Key);
            WriteFingerprintFieldUx2LwK(writer, item.Value);
        }
        writer.Write((byte)0);
        writer.Write(draft.RequiredAtRuntime);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteFingerprintFieldUx2LwK(BinaryWriter writer, string? value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string HumanEncounterNameUx2LwK(EncounterWorldProfile profile) =>
        profile.Composition?.DisplayName ?? "Rencontre sans composition";

    private static string HumanTokenUx2LwK(string? value) => string.IsNullOrWhiteSpace(value)
        ? "non renseigné"
        : value.Replace('-', ' ').Replace('_', ' ').Replace('.', ' ').Replace(':', ' ').Trim();

    private static string YesNoUx2LwK(bool value) => value ? "oui" : "non";

    private static Guid StableUx2LwK(Guid seed, string purpose)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"ux2-lw-k:{seed:D}:{purpose}"));
        return new Guid(bytes[..16]);
    }

    internal StudioUx2LwKSmokeResult RunUx2LwKContractSmoke()
    {
        var fixture = CreateUx2LwKSmokeProject();
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-lw-k-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(tempRoot, fixture.Project).Succeeded ||
                !WriteUx2LwFEvidenceScript(tempRoot) || !editing.Open(tempRoot).Succeeded ||
                !editing.SelectArea(fixture.AreaId))
                return FailedUx2LwKSmoke();
            var loadedProject = editing.Project!;
            var initialCatalog = loadedProject.Module.WorldFeatures;
            var loadedTarget = initialCatalog.Encounters.Single(item => item.Id == fixture.TargetId);
            var loadedNeighbor = initialCatalog.Encounters.Single(item => item.Id == fixture.NeighborId);
            var explicitSelection = editing.CreateEncounterStatisticsDraftUx2LwK(null) is null;
            var dependenciesRequired = !IsEligibleEncounterStatisticsUx2LwK(loadedTarget with { Foundation = null }) &&
                !IsEligibleEncounterStatisticsUx2LwK(loadedTarget with { Composition = null }) &&
                editing.CreateEncounterStatisticsDraftUx2LwK(fixture.TargetId) is not null;
            var optional = EncounterStatisticsProfileValidator.Resolve(loadedTarget with
            { Statistics = null, StatisticsRequiredAtRuntime = false });
            var required = EncounterStatisticsProfileValidator.Resolve(loadedTarget with
            { Statistics = null, StatisticsRequiredAtRuntime = true });
            var optionalAndRequiredResolution = optional.OptionalFallback && !optional.Available &&
                !required.OptionalFallback && required.Diagnostics.Any(item =>
                    item.Code == "world.encounter.statistics_required");
            var draft = editing.CreateEncounterStatisticsDraftUx2LwK(fixture.TargetId);
            if (draft is null) return FailedUx2LwKSmoke();

            var invalidRunsLow = editing.PreviewEncounterStatisticsUx2LwK(draft with { Runs = 0 });
            var invalidRunsHigh = editing.PreviewEncounterStatisticsUx2LwK(draft with { Runs = 100_001 });
            var invalidDuration = editing.PreviewEncounterStatisticsUx2LwK(draft with
            { SecondsPerActor = double.NaN });
            var invalidReward = editing.PreviewEncounterStatisticsUx2LwK(draft with { RewardPerCost = -1 });
            var invalidEstimator = editing.PreviewEncounterStatisticsUx2LwK(draft with
            { DurationEstimatorId = " " });
            var configurationValidation = new[]
                { invalidRunsLow, invalidRunsHigh, invalidDuration, invalidReward, invalidEstimator }
                .All(item => !item.Succeeded && item.Diagnostics.Any(diagnostic =>
                    diagnostic.Code == "world.encounter.statistics_configuration_invalid"));

            var invalidEmptyPercentiles = editing.PreviewEncounterStatisticsUx2LwK(draft with
            { Percentiles = [] });
            var invalidOrderedPercentiles = editing.PreviewEncounterStatisticsUx2LwK(draft with
            { Percentiles = [.9, .5] });
            var invalidDuplicatePercentiles = editing.PreviewEncounterStatisticsUx2LwK(draft with
            { Percentiles = [.5, .5] });
            var invalidBoundaryPercentile = editing.PreviewEncounterStatisticsUx2LwK(draft with
            { Percentiles = [0, .5] });
            var percentileValidation = new[]
                { invalidEmptyPercentiles, invalidOrderedPercentiles, invalidDuplicatePercentiles,
                    invalidBoundaryPercentile }
                .All(item => !item.Succeeded && item.Diagnostics.Any(diagnostic =>
                    diagnostic.Code == "world.encounter.statistics_percentiles_invalid"));

            var invalidOverrides = editing.PreviewEncounterStatisticsUx2LwK(draft with
            {
                Overrides = new Dictionary<string, string>(StringComparer.Ordinal) { ["mode"] = "" },
            });
            var overrideValidation = !invalidOverrides.Succeeded && invalidOverrides.Diagnostics.Any(item =>
                item.Code == "world.encounter.statistics_override_invalid");
            var invalidSeed = editing.PreviewEncounterStatisticsUx2LwK(draft with
            { BaseSeed = int.MaxValue, Runs = 2 });
            var seedRangeValidation = !invalidSeed.Succeeded && invalidSeed.Diagnostics.Any(item =>
                item.Code == "world.encounter.statistics_seed_range_invalid");

            var staleSource = draft;
            var changedBeforePreview = ApplyUx2LwKIntermediateMutation(editing, fixture.NeighborId, 1);
            var staleDraft = editing.PreviewEncounterStatisticsUx2LwK(staleSource);
            var staleDraftBlocked = changedBeforePreview && !staleDraft.Succeeded && editing.Undo().Succeeded;

            var changed = editing.CreateEncounterStatisticsDraftUx2LwK(fixture.TargetId)! with
            {
                Runs = 12,
                BaseSeed = 2026,
                SecondsPerActor = 4.75,
                RewardPerCost = .7,
                Percentiles = [.25, .5, .9],
                IncludeRunDetails = true,
                RequiredAtRuntime = true,
                Overrides = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["audience"] = "studio",
                    ["mode"] = "offline",
                },
            };
            var firstPreview = editing.PreviewEncounterStatisticsUx2LwK(changed);
            var secondPreview = editing.PreviewEncounterStatisticsUx2LwK(changed);
            var deterministicSimulation = firstPreview.Succeeded && secondPreview.Succeeded &&
                SameSimulationUx2LwK(firstPreview.Simulation, secondPreview.Simulation);
            var completeMetrics = firstPreview.Simulation is { } metrics &&
                metrics.Runs == changed.Runs && metrics.FirstSeed == changed.BaseSeed &&
                metrics.LastSeed == changed.BaseSeed + changed.Runs - 1 &&
                double.IsFinite(metrics.MeanCount) && double.IsFinite(metrics.MeanCost) &&
                double.IsFinite(metrics.MeanDurationSeconds) && double.IsFinite(metrics.MeanRewardValue) &&
                double.IsFinite(metrics.CountVariance) && double.IsFinite(metrics.CostVariance) &&
                double.IsFinite(metrics.DurationVariance) && double.IsFinite(metrics.RewardVariance) &&
                metrics.Percentiles.Count == changed.Percentiles.Count && metrics.Composition.Count > 0;
            var withDetails = firstPreview.Simulation?.DisplayedRuns.Count > 0;
            var withoutDetailsPreview = editing.PreviewEncounterStatisticsUx2LwK(changed with
            { IncludeRunDetails = false });
            var detailsPolicy = withDetails && withoutDetailsPreview.Succeeded &&
                withoutDetailsPreview.Simulation is { DisplayedRuns.Count: 0, HiddenRunCount: 0 };

            changed = editing.CreateEncounterStatisticsDraftUx2LwK(fixture.TargetId)! with
            {
                Runs = 12,
                BaseSeed = 2026,
                SecondsPerActor = 4.75,
                RewardPerCost = .7,
                Percentiles = [.25, .5, .9],
                IncludeRunDetails = true,
                RequiredAtRuntime = true,
                Overrides = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["audience"] = "studio",
                    ["mode"] = "offline",
                },
            };
            var approvalPreview = editing.PreviewEncounterStatisticsUx2LwK(changed);
            var changedAfterPreview = ApplyUx2LwKIntermediateMutation(editing, fixture.NeighborId, 2);
            var staleApply = editing.ApplyApprovedEncounterStatisticsUx2LwK();
            var staleApprovalBlocked = approvalPreview.Succeeded && changedAfterPreview && !staleApply.Succeeded &&
                                       editing.Undo().Succeeded;

            changed = editing.CreateEncounterStatisticsDraftUx2LwK(fixture.TargetId)! with
            {
                Runs = 12,
                BaseSeed = 2026,
                SecondsPerActor = 4.75,
                RewardPerCost = .7,
                Percentiles = [.25, .5, .9],
                IncludeRunDetails = true,
                RequiredAtRuntime = true,
                Overrides = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["audience"] = "studio",
                    ["mode"] = "offline",
                },
            };
            var beforeDryRunJson = ProjectJsonSerializer.Serialize(editing.Project!);
            var beforeDryRunJournal = editing.History!.Journal.Count;
            var preview = editing.PreviewEncounterStatisticsUx2LwK(changed);
            var dryRunPure = preview.Succeeded && preview.CanApply && preview.DryRunPure &&
                             beforeDryRunJson == ProjectJsonSerializer.Serialize(editing.Project!) &&
                             editing.History.Journal.Count == beforeDryRunJournal;
            var offlineBoundary = preview.Simulation?.Boundary.Contains("offline", StringComparison.OrdinalIgnoreCase) == true &&
                                  preview.Simulation.Boundary.Contains("aucun acteur", StringComparison.OrdinalIgnoreCase) &&
                                  preview.Simulation.Boundary.Contains("spawn", StringComparison.OrdinalIgnoreCase) &&
                                  preview.Simulation.Boundary.Contains("combat", StringComparison.OrdinalIgnoreCase) &&
                                  preview.Simulation.Boundary.Contains("réseau", StringComparison.OrdinalIgnoreCase);
            var applied = editing.ApplyApprovedEncounterStatisticsUx2LwK();
            if (!applied.Succeeded) return FailedUx2LwKSmoke();
            var appliedProject = editing.Project!;
            var appliedCatalog = appliedProject.Module.WorldFeatures;
            var appliedTarget = appliedCatalog.Encounters.Single(item => item.Id == fixture.TargetId);
            var appliedNeighbor = appliedCatalog.Encounters.Single(item => item.Id == fixture.NeighborId);
            var encounterTailsPreserved = loadedTarget.Id == appliedTarget.Id &&
                loadedTarget.SchemaVersion == appliedTarget.SchemaVersion &&
                loadedTarget.Provenance == appliedTarget.Provenance && loadedTarget.AreaId == appliedTarget.AreaId &&
                ReferenceEquals(loadedTarget.Foundation, appliedTarget.Foundation) &&
                ReferenceEquals(loadedTarget.Composition, appliedTarget.Composition) &&
                loadedTarget.CompositionRequiredAtRuntime == appliedTarget.CompositionRequiredAtRuntime &&
                ReferenceEquals(loadedTarget.Scenario, appliedTarget.Scenario) &&
                loadedTarget.ScenarioRequiredAtRuntime == appliedTarget.ScenarioRequiredAtRuntime &&
                ReferenceEquals(loadedTarget.Respawn, appliedTarget.Respawn) &&
                loadedTarget.SpawnFactionId == appliedTarget.SpawnFactionId &&
                ReferenceEquals(loadedTarget.Scheduler, appliedTarget.Scheduler) &&
                ReferenceEquals(loadedTarget.Reinforcements, appliedTarget.Reinforcements) &&
                ReferenceEquals(loadedTarget.Visibility, appliedTarget.Visibility) &&
                ReferenceEquals(loadedTarget.Cleanup, appliedTarget.Cleanup) &&
                loadedTarget.Statistics?.Id == appliedTarget.Statistics?.Id &&
                loadedTarget.Statistics?.SchemaVersion == appliedTarget.Statistics?.SchemaVersion &&
                loadedTarget.Statistics?.Provenance == appliedTarget.Statistics?.Provenance;
            var encounterNeighborsPreserved = ReferenceEquals(loadedNeighbor, appliedNeighbor);
            var projectNeighborsPreserved = loadedProject.Blueprints.SequenceEqual(appliedProject.Blueprints) &&
                loadedProject.Areas.SequenceEqual(appliedProject.Areas) &&
                loadedProject.Dialogues.SequenceEqual(appliedProject.Dialogues) &&
                loadedProject.Quests.SequenceEqual(appliedProject.Quests) &&
                ReferenceEquals(loadedProject.Module.Factions, appliedProject.Module.Factions);
            var collectionsPreserved = SameReferencesUx2LwK(initialCatalog.Placeables, appliedCatalog.Placeables) &&
                SameReferencesUx2LwK(initialCatalog.Doors, appliedCatalog.Doors) &&
                SameReferencesUx2LwK(initialCatalog.Triggers, appliedCatalog.Triggers) &&
                SameReferencesUx2LwK(initialCatalog.VehicleRoutes, appliedCatalog.VehicleRoutes) &&
                SameReferencesUx2LwK(initialCatalog.Merchants, appliedCatalog.Merchants) &&
                SameReferencesUx2LwK(initialCatalog.Companions, appliedCatalog.Companions) &&
                ReferenceEquals(initialCatalog.Scripts, appliedCatalog.Scripts);

            var undo = editing.Undo();
            var undoneProject = editing.Project!;
            var undone = undoneProject.Module.WorldFeatures.Encounters.Single(item => item.Id == fixture.TargetId);
            var redo = editing.Redo();
            var redoneProject = editing.Project!;
            var redone = redoneProject.Module.WorldFeatures.Encounters.Single(item => item.Id == fixture.TargetId);
            var undoRedo = undo.Succeeded && redo.Succeeded &&
                EncounterProfileJsonUx2LwK(undone, undoneProject) ==
                EncounterProfileJsonUx2LwK(loadedTarget, loadedProject) &&
                EncounterProfileJsonUx2LwK(redone, redoneProject) ==
                EncounterProfileJsonUx2LwK(appliedTarget, appliedProject);
            var saved = editing.Save();
            var opened = saved.Succeeded && reopened.Open(tempRoot).Succeeded;
            var persisted = opened ? reopened.Project!.Module.WorldFeatures.Encounters.Single(item =>
                item.Id == fixture.TargetId) : null;
            var persistence = persisted is not null &&
                EncounterProfileJsonUx2LwK(persisted, reopened.Project!) ==
                EncounterProfileJsonUx2LwK(appliedTarget, appliedProject) &&
                ProjectJsonSerializer.Serialize(editing.Project!) == ProjectJsonSerializer.Serialize(reopened.Project!);
            reopened.Close();
            var canonicalCommand = editing.History!.Journal.Count >= 3 &&
                                   editing.History.Journal.All(item =>
                                       item.CommandName == "set_world_feature_catalog");
            var structuredDiagnostics = new[]
                { invalidRunsLow, invalidRunsHigh, invalidDuration, invalidReward, invalidEstimator,
                    invalidEmptyPercentiles, invalidOrderedPercentiles, invalidDuplicatePercentiles,
                    invalidBoundaryPercentile, invalidOverrides, invalidSeed }
                .SelectMany(item => item.Diagnostics)
                .All(item => !string.IsNullOrWhiteSpace(item.Code) && !string.IsNullOrWhiteSpace(item.Path));
            var humanText = editing.EncounterChoicesUx2LwK.All(item => IsHumanUx2LwKText(item.Label)) &&
                            preview.Simulation is { } humanSimulation &&
                            humanSimulation.Composition.All(item => IsHumanUx2LwKText(item.Creature)) &&
                            humanSimulation.DisplayedRuns.All(item => IsHumanUx2LwKText(item.Composition));
            var succeeded = explicitSelection && dependenciesRequired && optionalAndRequiredResolution &&
                            configurationValidation && percentileValidation && overrideValidation &&
                            seedRangeValidation && deterministicSimulation && completeMetrics && detailsPolicy &&
                            dryRunPure && staleDraftBlocked && staleApprovalBlocked && canonicalCommand &&
                            undoRedo && persistence && encounterTailsPreserved && encounterNeighborsPreserved &&
                            projectNeighborsPreserved && collectionsPreserved && structuredDiagnostics && humanText &&
                            offlineBoundary;
            return new(succeeded, explicitSelection, dependenciesRequired, optionalAndRequiredResolution,
                configurationValidation, percentileValidation, overrideValidation, seedRangeValidation,
                deterministicSimulation, completeMetrics, detailsPolicy, dryRunPure, staleDraftBlocked,
                staleApprovalBlocked, canonicalCommand, undoRedo, persistence, encounterTailsPreserved,
                encounterNeighborsPreserved, projectNeighborsPreserved, collectionsPreserved,
                structuredDiagnostics, humanText, offlineBoundary);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_LW_K_CONTRACT_FAILED exception={exception.Message}");
            return FailedUx2LwKSmoke();
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
                Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_LW_K_CLEANUP_FAILED " + exception.Message);
            }
        }
    }

    private static bool ApplyUx2LwKIntermediateMutation(
        StudioSessionController session,
        Guid profileId,
        double delta)
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
        var catalog = ReplaceEncounterUx2LwK(source, updated);
        var result = new SetWorldFeatureCatalogHandler(new SystemClock()).Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, catalog, false));
        if (!result.Succeeded) return false;
        history.Record(result);
        return true;
    }

    private static Ux2LwKSmokeFixture CreateUx2LwKSmokeProject()
    {
        var fixture = CreateUx2LwFSmokeProject();
        var source = fixture.Project.Module.WorldFeatures;
        var target = source.Encounters.Single(item => item.Id == fixture.TargetId) with
        {
            Respawn = new(28.5, 3, "area.ready", true, true, .75),
            Scheduler = new(false, 8.5, 10, "encounter.statistics"),
        };
        var neighbor = source.Encounters.Single(item => item.Id == fixture.NeighborId) with
        {
            Respawn = new(45.25, 2, "area.ready", false, false, .9),
            Scheduler = new(true, 12.5, 6, "encounter.neighbor"),
        };
        var encounters = source.Encounters.Select(item => item.Id == target.Id ? target :
            item.Id == neighbor.Id ? neighbor : item).ToArray();
        var catalog = new WorldFeatureCatalog(source.Id, source.SchemaVersion, source.Provenance,
            source.Placeables, source.Doors, source.Triggers, encounters, source.VehicleRoutes,
            source.Merchants, source.Companions, source.Scripts);
        var project = new ProjectDefinition(fixture.Project.Id, "Studio UX2-LW-K",
            fixture.Project.Module.WithWorldFeatures(catalog), fixture.Project.Blueprints,
            fixture.Project.Areas, fixture.Project.Dialogues, fixture.Project.Quests);
        return new(project, fixture.AreaId, fixture.TargetId, fixture.NeighborId);
    }

    private static bool SameSimulationUx2LwK(StudioUx2LwKSimulation? left, StudioUx2LwKSimulation? right) =>
        left is not null && right is not null &&
        left.Runs == right.Runs && left.FirstSeed == right.FirstSeed && left.LastSeed == right.LastSeed &&
        left.MeanCount.Equals(right.MeanCount) && left.MeanCost.Equals(right.MeanCost) &&
        left.MeanDurationSeconds.Equals(right.MeanDurationSeconds) &&
        left.MeanRewardValue.Equals(right.MeanRewardValue) &&
        left.CountVariance.Equals(right.CountVariance) && left.CostVariance.Equals(right.CostVariance) &&
        left.DurationVariance.Equals(right.DurationVariance) && left.RewardVariance.Equals(right.RewardVariance) &&
        left.Percentiles.SequenceEqual(right.Percentiles) && left.Composition.SequenceEqual(right.Composition) &&
        left.DisplayedRuns.SequenceEqual(right.DisplayedRuns) && left.HiddenRunCount == right.HiddenRunCount &&
        left.CanonicalSummary == right.CanonicalSummary;

    private static string EncounterProfileJsonUx2LwK(EncounterWorldProfile profile, ProjectDefinition project)
    {
        var source = project.Module.WorldFeatures;
        var catalog = new WorldFeatureCatalog(source.Id, source.SchemaVersion, source.Provenance,
            source.Placeables, source.Doors, source.Triggers, [profile], source.VehicleRoutes,
            source.Merchants, source.Companions, source.Scripts);
        return ProjectJsonSerializer.Serialize(new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(catalog), project.Blueprints, project.Areas,
            project.Dialogues, project.Quests));
    }

    private static bool SameReferencesUx2LwK<T>(IReadOnlyList<T> left, IReadOnlyList<T> right)
        where T : class => left.Count == right.Count &&
                           left.Zip(right).All(pair => ReferenceEquals(pair.First, pair.Second));

    private static bool IsHumanUx2LwKText(string value) =>
        !string.IsNullOrWhiteSpace(value) && !value.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !value.Contains("module/worldFeatures", StringComparison.Ordinal) &&
        !value.Split([' ', '·', '/', ':', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Any(token => Guid.TryParse(token, out _));

    private static StudioUx2LwKSmokeResult FailedUx2LwKSmoke() => new(
        false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false, false);

    private sealed record Ux2LwKSmokeFixture(
        ProjectDefinition Project,
        Forge.Domain.Areas.AreaId AreaId,
        Guid TargetId,
        Guid NeighborId);
}
