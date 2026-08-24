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
    private const string Ux2LwGEvidenceProvenance = "studio:ux2-lw-g:evidence";
    private readonly SetWorldFeatureCatalogHandler _setEncounterPolicyCatalogUx2LwG = new(new SystemClock());
    private StudioUx2LwGDraft? _approvedEncounterPolicyUx2LwG;
    private ProjectRevision? _approvedEncounterPolicyRevisionUx2LwG;
    private string? _approvedEncounterPolicySourceFingerprintUx2LwG;
    private string? _approvedEncounterPolicyDraftFingerprintUx2LwG;

    internal IReadOnlyList<StudioUx2LwGEncounterChoice> EncounterChoicesUx2LwG
    {
        get
        {
            var project = Project;
            var area = ActiveArea;
            if (project is null || area is null) return [];
            return project.Module.WorldFeatures.Encounters
                .Where(item => item.AreaId == area.Id)
                .Select(item => new StudioUx2LwGEncounterChoice(item.Id,
                    $"{HumanEncounterNameUx2LwF(item)} · " +
                    (IsEligibleEncounterUx2LwG(item)
                        ? $"groupe {item.Composition!.Difficulty.ExpectedPartySize}, " +
                          $"{HumanActivationUx2LwG(item.Composition.Activation.Mode)}"
                        : "fiche Foundation/LW-F requise"),
                    IsEligibleEncounterUx2LwG(item),
                    string.Equals(item.Provenance, Ux2LwGEvidenceProvenance, StringComparison.Ordinal)))
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal IReadOnlyList<StudioUx2LwGScriptChoice> EncounterScriptChoicesUx2LwG() =>
        EncounterScriptChoicesUx2LwF()
            .Select(item => new StudioUx2LwGScriptChoice(item.Id, item.Label, item.Provenance))
            .ToArray();

    internal StudioUx2LwGDraft? CreateEncounterPolicyDraftUx2LwG(Guid? profileId)
    {
        var history = History;
        var project = Project;
        var area = ActiveArea;
        if (history is null || project is null || area is null || profileId is null) return null;
        var profile = project.Module.WorldFeatures.Encounters.SingleOrDefault(item =>
            item.Id == profileId && item.AreaId == area.Id);
        if (!IsEligibleEncounterUx2LwG(profile)) return null;
        var composition = profile!.Composition!;
        return new(profile.Id, history.State.Revision, ProjectFingerprintUx2LwF(project),
            composition.Selection.Id, composition.Selection.PartyAssessmentProviderId,
            composition.Selection.Strategy, composition.Selection.DeterministicSeed,
            composition.Selection.PreviewComposition, composition.Activation.Id,
            composition.Activation.Mode, composition.Activation.IntervalSeconds,
            composition.Activation.ScriptId, composition.Activation.Distribution, profile);
    }

    internal StudioUx2LwGPreview PreviewEncounterPolicyUx2LwG(StudioUx2LwGDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelEncounterPolicyPreviewUx2LwG();
        var history = History;
        var project = Project;
        if (history is null || project is null)
            return FailedEncounterPolicyPreviewUx2LwG("Aucun module n’est ouvert.");
        if (draft.BaseRevision != history.State.Revision)
            return FailedEncounterPolicyPreviewUx2LwG(
                "Le projet a changé depuis l’ouverture de la fiche. Rechargez la rencontre.");
        if (!string.Equals(draft.SourceFingerprint, ProjectFingerprintUx2LwF(project), StringComparison.Ordinal))
            return FailedEncounterPolicyPreviewUx2LwG(
                "Le catalogue a changé. Rechargez la rencontre avant l’aperçu.");
        if (!TryBuildEncounterPolicyCandidateUx2LwG(draft, true, out var command, out var candidate,
                out var diagnostics))
            return new(false, false,
                diagnostics.FirstOrDefault()?.Message ?? "La politique contient une valeur invalide.",
                string.Empty, BuildEncounterPolicyDiffUx2LwG(draft.OriginalProfile, candidate), diagnostics,
                EmptyEncounterPolicyRuntimeUx2LwG(), true);

        var result = _setEncounterPolicyCatalogUx2LwG.Handle(history.State, command!);
        if (!result.Succeeded)
        {
            var commandDiagnostics = result.Diagnostics.Select(item => new StudioUx2LwGDiagnostic(
                item.Code, $"module/worldFeatures/encounters/{draft.ProfileId:D}",
                HumanEncounterPolicyDiagnosticUx2LwG(item.Code))).ToArray();
            return new(false, false,
                commandDiagnostics.FirstOrDefault()?.Message ?? "La commande canonique a refusé la politique.",
                result.Patch?.Summary ?? string.Empty,
                BuildEncounterPolicyDiffUx2LwG(draft.OriginalProfile, candidate), commandDiagnostics,
                EmptyEncounterPolicyRuntimeUx2LwG(), true);
        }

        var runtime = BuildEncounterPolicyRuntimeUx2LwG(candidate!);
        var diff = BuildEncounterPolicyDiffUx2LwG(draft.OriginalProfile, candidate);
        var canApply = diff.Any(item => item.Changed);
        if (canApply)
        {
            _approvedEncounterPolicyUx2LwG = draft;
            _approvedEncounterPolicyRevisionUx2LwG = history.State.Revision;
            _approvedEncounterPolicySourceFingerprintUx2LwG = draft.SourceFingerprint;
            _approvedEncounterPolicyDraftFingerprintUx2LwG = EncounterPolicyDraftFingerprintUx2LwG(draft);
        }
        return new(true, canApply,
            canApply
                ? "Aperçu sec validé · composition déterministe et politique d’activation uniquement."
                : "La sélection et l’activation sont déjà identiques au document canonique.",
            result.Patch?.Summary ?? string.Empty, diff, [], runtime, true);
    }

    internal StudioUx2LwGActionResult ApplyApprovedEncounterPolicyUx2LwG()
    {
        var history = History;
        var project = Project;
        var draft = _approvedEncounterPolicyUx2LwG;
        if (history is null || project is null || draft is null ||
            _approvedEncounterPolicyRevisionUx2LwG != history.State.Revision ||
            !string.Equals(_approvedEncounterPolicySourceFingerprintUx2LwG,
                ProjectFingerprintUx2LwF(project), StringComparison.Ordinal) ||
            !string.Equals(_approvedEncounterPolicyDraftFingerprintUx2LwG,
                EncounterPolicyDraftFingerprintUx2LwG(draft), StringComparison.Ordinal))
        {
            CancelEncounterPolicyPreviewUx2LwG();
            return new(false, "Prévisualisez à nouveau cette politique avant de l’appliquer.");
        }
        if (!TryBuildEncounterPolicyCandidateUx2LwG(draft, false, out var command, out _,
                out var diagnostics))
        {
            CancelEncounterPolicyPreviewUx2LwG();
            return new(false, diagnostics.FirstOrDefault()?.Message ?? "La politique est invalide.");
        }
        var result = _setEncounterPolicyCatalogUx2LwG.Handle(history.State, command!);
        CancelEncounterPolicyPreviewUx2LwG();
        if (!result.Succeeded)
            return new(false, HumanEncounterPolicyDiagnosticUx2LwG(result.Diagnostics.FirstOrDefault()?.Code));
        history.Record(result);
        InvalidateContentIndexes();
        return new(true, "Politiques appliquées · Ctrl+Z pour annuler, Ctrl+Y pour rétablir.");
    }

    internal void CancelEncounterPolicyPreviewUx2LwG()
    {
        _approvedEncounterPolicyUx2LwG = null;
        _approvedEncounterPolicyRevisionUx2LwG = null;
        _approvedEncounterPolicySourceFingerprintUx2LwG = null;
        _approvedEncounterPolicyDraftFingerprintUx2LwG = null;
    }

    internal StudioUx2LwGApprovalSnapshot CaptureEncounterPolicyApprovalUx2LwG() => new(
        _approvedEncounterPolicyUx2LwG, _approvedEncounterPolicyRevisionUx2LwG,
        _approvedEncounterPolicySourceFingerprintUx2LwG, _approvedEncounterPolicyDraftFingerprintUx2LwG);

    internal void RestoreEncounterPolicyApprovalUx2LwG(StudioUx2LwGApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedEncounterPolicyUx2LwG = snapshot.Draft;
        _approvedEncounterPolicyRevisionUx2LwG = snapshot.Revision;
        _approvedEncounterPolicySourceFingerprintUx2LwG = snapshot.SourceFingerprint;
        _approvedEncounterPolicyDraftFingerprintUx2LwG = snapshot.DraftFingerprint;
    }

    internal void CloseUx2LwG() => CancelEncounterPolicyPreviewUx2LwG();

    internal ProjectDefinition? CreateUx2LwGEvidenceProject() => CreateUx2LwFEvidenceProject();

    private bool TryBuildEncounterPolicyCandidateUx2LwG(
        StudioUx2LwGDraft draft,
        bool dryRun,
        out SetWorldFeatureCatalogCommand? command,
        out EncounterWorldProfile? candidate,
        out IReadOnlyList<StudioUx2LwGDiagnostic> diagnostics)
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
        if (!string.Equals(draft.SourceFingerprint, ProjectFingerprintUx2LwF(project), StringComparison.Ordinal))
        {
            diagnostics = [new("command.source_changed", "module/worldFeatures/encounters",
                "Le catalogue a changé. Rechargez la rencontre.")];
            return false;
        }
        var current = project.Module.WorldFeatures.Encounters.SingleOrDefault(item => item.Id == draft.ProfileId);
        if (!IsEligibleEncounterUx2LwG(current))
        {
            diagnostics = [new("world.encounter.foundation_required",
                $"module/worldFeatures/encounters/{draft.ProfileId:D}",
                "Terminez d’abord la fiche Foundation et composition du lot LW-F.")];
            return false;
        }
        var sourceComposition = current!.Composition!;
        if (draft.SelectionId != sourceComposition.Selection.Id ||
            draft.ActivationId != sourceComposition.Activation.Id)
        {
            diagnostics = [new("world.encounter.policy_identity_changed",
                $"module/worldFeatures/encounters/{draft.ProfileId:D}/composition",
                "Les identités internes sont protégées. Rechargez la rencontre.")];
            return false;
        }
        if (!string.Equals(draft.PartyAssessmentProviderId,
                sourceComposition.Difficulty.PartyAssessmentProviderId, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(draft.Strategy))
        {
            diagnostics = [new("world.encounter.selection_invalid",
                $"module/worldFeatures/encounters/{draft.ProfileId:D}/composition/selection",
                "La stratégie doit être renseignée et conserver le fournisseur de groupe de la difficulté.")];
            return false;
        }
        if (!Enum.IsDefined(draft.ActivationMode) || !Enum.IsDefined(draft.Distribution) ||
            !double.IsFinite(draft.IntervalSeconds) || draft.IntervalSeconds < 0 ||
            draft.ActivationMode == EncounterActivationMode.Timed && draft.IntervalSeconds <= 0)
        {
            diagnostics = [new("world.encounter.activation_invalid",
                $"module/worldFeatures/encounters/{draft.ProfileId:D}/composition/activation",
                "Choisissez un mode, une distribution et une cadence valides.")];
            return false;
        }
        var scriptId = NormalizeScriptUx2LwF(draft.ScriptId);
        if (draft.ActivationMode == EncounterActivationMode.Script && string.IsNullOrWhiteSpace(scriptId))
        {
            diagnostics = [new("world.encounter.activation_script_required",
                $"module/worldFeatures/encounters/{draft.ProfileId:D}/composition/activation/scriptId",
                "Le mode Script exige un ForgeScript réel du module.")];
            return false;
        }
        if (scriptId is not null && !EncounterScriptChoicesUx2LwG().Any(item =>
                string.Equals(item.Id, scriptId, StringComparison.Ordinal)))
        {
            diagnostics = [new("world.encounter.script_missing",
                $"module/worldFeatures/encounters/{draft.ProfileId:D}/composition/activation/scriptId",
                "Choisissez un ForgeScript réel encore présent dans le module.")];
            return false;
        }

        var selection = new EncounterCompositionProfile.SelectionPolicy(
            sourceComposition.Selection.Id,
            sourceComposition.Difficulty.PartyAssessmentProviderId,
            draft.Strategy.Trim(), draft.DeterministicSeed, draft.PreviewComposition);
        var activation = new EncounterCompositionProfile.ActivationPolicy(
            sourceComposition.Activation.Id, draft.ActivationMode, draft.IntervalSeconds,
            scriptId, draft.Distribution);
        var composition = sourceComposition with { Selection = selection, Activation = activation };
        var candidateProfile = current with { Composition = composition };
        candidate = candidateProfile;
        var source = project.Module.WorldFeatures;
        var encounters = source.Encounters.Select(item => item.Id == candidateProfile.Id ? candidateProfile : item)
            .ToArray();
        var catalog = new WorldFeatureCatalog(source.Id, source.SchemaVersion, source.Provenance,
            source.Placeables, source.Doors, source.Triggers, encounters, source.VehicleRoutes,
            source.Merchants, source.Companions, source.Scripts);
        var candidateProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(catalog), project.Blueprints, project.Areas,
            project.Dialogues, project.Quests);
        var path = $"module/worldFeatures/encounters/{candidateProfile.Id}";
        var canonical = WorldFeatureCatalogValidator.Validate(candidateProject)
            .Where(item => item.Path.StartsWith(path, StringComparison.Ordinal))
            .Select(item => new StudioUx2LwGDiagnostic(item.Code, item.Path,
                HumanEncounterPolicyDiagnosticUx2LwG(item.Code))).ToList();
        if (canonical.Count == 0)
        {
            try
            {
                canonical.AddRange(EncounterCompositionFoundationService.Preview(candidateProfile,
                        composition.Difficulty.ExpectedPartySize, composition.Difficulty.ExpectedPartyPower,
                        new HashSet<string>(["party"], StringComparer.Ordinal)).Diagnostics
                    .Select(item => new StudioUx2LwGDiagnostic(item.Code, item.Path,
                        HumanEncounterPolicyDiagnosticUx2LwG(item.Code))));
            }
            catch (InvalidOperationException exception)
            {
                canonical.Add(new("world.encounter.runtime_mapping_invalid", path,
                    HumanEncounterPolicyDiagnosticUx2LwG(exception.Message)));
            }
        }
        if (canonical.Count > 0)
        {
            diagnostics = canonical.DistinctBy(item => (item.Code, item.Path)).ToArray();
            return false;
        }
        command = new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()),
            history.State.Revision, catalog, dryRun);
        return true;
    }

    private static IReadOnlyList<StudioUx2LwGDiff> BuildEncounterPolicyDiffUx2LwG(
        EncounterWorldProfile before,
        EncounterWorldProfile? after)
    {
        if (after?.Composition is null || before.Composition is null) return [];
        var rows = new List<StudioUx2LwGDiff>();
        Add("Sélection selon la difficulté", SelectionSummaryUx2LwG(before.Composition),
            SelectionSummaryUx2LwG(after.Composition));
        Add("Rencontre unique ou continue", ActivationSummaryUx2LwG(before.Composition.Activation),
            ActivationSummaryUx2LwG(after.Composition.Activation));
        return rows;

        void Add(string section, string oldValue, string newValue) =>
            rows.Add(new(section, oldValue, newValue,
                !string.Equals(oldValue, newValue, StringComparison.Ordinal)));
    }

    private static StudioUx2LwGRuntimePreview BuildEncounterPolicyRuntimeUx2LwG(
        EncounterWorldProfile profile)
    {
        var composition = profile.Composition!;
        var difficulty = composition.Difficulty;
        var first = EncounterCompositionFoundationService.Preview(profile,
            difficulty.ExpectedPartySize, difficulty.ExpectedPartyPower,
            new HashSet<string>(["party"], StringComparer.Ordinal));
        var second = EncounterCompositionFoundationService.Preview(profile,
            difficulty.ExpectedPartySize, difficulty.ExpectedPartyPower,
            new HashSet<string>(["party"], StringComparer.Ordinal));
        var deterministic = first.Composition.Summary == second.Composition.Summary &&
                            first.Composition.Candidates.Select(item => item.CreatureId).SequenceEqual(
                                second.Composition.Candidates.Select(item => item.CreatureId));
        return new(
            $"{difficulty.ExpectedPartySize} membre(s), puissance {difficulty.ExpectedPartyPower:0.##} · " +
            $"fournisseur {HumanPolicyTokenUx2LwG(composition.Selection.PartyAssessmentProviderId)} et stratégie " +
            $"{HumanPolicyTokenUx2LwG(composition.Selection.Strategy)} enregistrés, non invoqués dans le Studio",
            $"{first.Composition.TotalCount} acteur(s) proposés · coût {first.Composition.TotalCost:0.##} · " +
            $"graine {composition.Selection.DeterministicSeed} · " +
            (deterministic ? "aperçu reproductible" : "écart détecté"),
            ActivationSummaryUx2LwG(composition.Activation),
            "Décision de composition et représentation de politique uniquement : aucun minuteur moteur, " +
            "ForgeScript, spawn d’acteur ou transport réseau n’est exécuté ici.");
    }

    private static StudioUx2LwGRuntimePreview EmptyEncounterPolicyRuntimeUx2LwG() => new(
        "Groupe non évalué", "Composition non évaluée", "Activation non évaluée",
        "Aucun fournisseur, minuteur, ForgeScript, spawn d’acteur ou réseau n’a été exécuté.");

    private static StudioUx2LwGPreview FailedEncounterPolicyPreviewUx2LwG(string message) => new(
        false, false, message, string.Empty, [], [], EmptyEncounterPolicyRuntimeUx2LwG(), true);

    private static string SelectionSummaryUx2LwG(EncounterCompositionProfile composition) =>
        $"{HumanPolicyTokenUx2LwG(composition.Selection.Strategy)} · " +
        $"graine {composition.Selection.DeterministicSeed} · " +
        (composition.Selection.PreviewComposition ? "aperçu demandé" : "aperçu désactivé") +
        $" · fournisseur {HumanPolicyTokenUx2LwG(composition.Selection.PartyAssessmentProviderId)}";

    private static string ActivationSummaryUx2LwG(EncounterCompositionProfile.ActivationPolicy activation) =>
        $"{HumanActivationUx2LwG(activation.Mode)} · {HumanDistributionUx2LwG(activation.Distribution)}" +
        (activation.IntervalSeconds > 0 ? $" · cadence {activation.IntervalSeconds:0.##} s" : string.Empty) +
        (activation.ScriptId is null ? string.Empty : $" · script {HumanScriptNameUx2LwF(activation.ScriptId)}");

    internal static string HumanEncounterPolicyDiagnosticUx2LwG(string? code) => code switch
    {
        "world.encounter.foundation_required" or "world.encounter.foundation_missing" =>
            "Terminez d’abord la fiche Foundation et composition dans l’assistant LW-F.",
        "world.encounter.selection_invalid" =>
            "Renseignez une stratégie et conservez le fournisseur d’évaluation de la difficulté.",
        "world.encounter.activation_invalid" or "RPG_ENCOUNTER_INTERVAL_INVALID" =>
            "Choisissez un mode, une distribution et une cadence cohérents.",
        "world.encounter.activation_script_required" or "world.encounter.script_missing" =>
            "Le mode Script exige un fichier ForgeScript réel du module.",
        "command.revision_conflict" or "command.source_changed" =>
            "Le projet a changé. Rechargez la rencontre et recommencez l’aperçu.",
        "world.encounter.policy_identity_changed" =>
            "Les identités internes sont protégées. Rechargez la rencontre.",
        _ => HumanEncounterDiagnosticUx2LwF(code),
    };

    internal static string HumanActivationUx2LwG(EncounterActivationMode mode) => mode switch
    {
        EncounterActivationMode.Once => "Une seule fois",
        EncounterActivationMode.Repeat => "Répétée",
        EncounterActivationMode.Timed => "Planifiée par cadence",
        EncounterActivationMode.Wave => "Par vagues",
        EncounterActivationMode.Persistent => "Persistante",
        EncounterActivationMode.PopulationMaintained => "Population maintenue",
        EncounterActivationMode.Script => "Pilotée par script",
        _ => "Mode inconnu",
    };

    internal static string HumanDistributionUx2LwG(EncounterSpawnDistribution distribution) => distribution switch
    {
        EncounterSpawnDistribution.Sequential => "ordre séquentiel",
        EncounterSpawnDistribution.Random => "répartition aléatoire déterministe",
        EncounterSpawnDistribution.Weighted => "répartition pondérée",
        EncounterSpawnDistribution.Even => "répartition équilibrée",
        _ => "répartition inconnue",
    };

    internal static string HumanPolicyTokenUx2LwG(string? value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "non renseigné" : value.Trim();
        return Guid.TryParse(text, out _) ? "référence protégée" :
            text.Replace(':', ' ').Replace('-', ' ').Replace('_', ' ');
    }

    private static bool IsEligibleEncounterUx2LwG(EncounterWorldProfile? profile) =>
        profile?.Foundation is not null && profile.Composition is not null;

    private static string EncounterPolicyDraftFingerprintUx2LwG(StudioUx2LwGDraft draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        Write(draft.ProfileId.ToString("D"));
        Write(draft.BaseRevision.Value.ToString(CultureInfo.InvariantCulture));
        Write(draft.SourceFingerprint); Write(draft.SelectionId.ToString("D"));
        Write(draft.PartyAssessmentProviderId); Write(draft.Strategy);
        Write(draft.DeterministicSeed.ToString(CultureInfo.InvariantCulture));
        Write(draft.PreviewComposition ? "1" : "0"); Write(draft.ActivationId.ToString("D"));
        Write(((int)draft.ActivationMode).ToString(CultureInfo.InvariantCulture));
        Write(draft.IntervalSeconds.ToString("R", CultureInfo.InvariantCulture)); Write(draft.ScriptId);
        Write(((int)draft.Distribution).ToString(CultureInfo.InvariantCulture));
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));

        void Write(string? value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
    }

    internal StudioUx2LwGSmokeResult RunUx2LwGContractSmoke()
    {
        var fixture = CreateUx2LwFSmokeProject();
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-lw-g-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(tempRoot, fixture.Project).Succeeded ||
                !WriteUx2LwFEvidenceScript(tempRoot) || !editing.Open(tempRoot).Succeeded)
                return FailedUx2LwGSmoke();
            var loadedProject = editing.Project!;
            var initialCatalog = loadedProject.Module.WorldFeatures;
            var loadedTarget = initialCatalog.Encounters.Single(item => item.Id == fixture.TargetId);
            var loadedNeighbor = initialCatalog.Encounters.Single(item => item.Id == fixture.NeighborId);
            var draft = editing.CreateEncounterPolicyDraftUx2LwG(fixture.TargetId);
            var explicitSelection = editing.CreateEncounterPolicyDraftUx2LwG(null) is null && draft is not null;
            if (draft is null) return FailedUx2LwGSmoke();
            var foundationRequired = !IsEligibleEncounterUx2LwG(loadedTarget with { Foundation = null }) &&
                                     !IsEligibleEncounterUx2LwG(loadedTarget with { Composition = null });

            var invalidStrategy = editing.PreviewEncounterPolicyUx2LwG(draft with { Strategy = " " });
            var invalidProvider = editing.PreviewEncounterPolicyUx2LwG(draft with
            { PartyAssessmentProviderId = "party:forged" });
            var invalidInterval = editing.PreviewEncounterPolicyUx2LwG(draft with
            { ActivationMode = EncounterActivationMode.Timed, IntervalSeconds = 0 });
            var invalidScript = editing.PreviewEncounterPolicyUx2LwG(draft with
            { ActivationMode = EncounterActivationMode.Script, ScriptId = "scripts/absent.forge" });
            var strategyValidation = !invalidStrategy.Succeeded && invalidStrategy.Diagnostics.Any(item =>
                item.Code == "world.encounter.selection_invalid");
            var providerGuard = !invalidProvider.Succeeded && invalidProvider.Diagnostics.Any(item =>
                item.Code == "world.encounter.selection_invalid");
            var activationValidation = !invalidInterval.Succeeded && invalidInterval.Diagnostics.Any(item =>
                item.Code == "world.encounter.activation_invalid");
            var realScriptValidation = !invalidScript.Succeeded && invalidScript.Diagnostics.Any(item =>
                item.Code == "world.encounter.script_missing") &&
                editing.EncounterScriptChoicesUx2LwG().Any(item =>
                    item.Id == "scripts/encounter_activate.forge");

            var staleDraft = editing.CreateEncounterPolicyDraftUx2LwG(fixture.TargetId)!;
            var changedBeforePreview = ApplyUx2LwFIntermediateMutation(editing, fixture.NeighborId, 21);
            var stalePreview = editing.PreviewEncounterPolicyUx2LwG(staleDraft);
            var staleDraftBlocked = changedBeforePreview && !stalePreview.Succeeded &&
                                    stalePreview.Message.Contains("changé", StringComparison.OrdinalIgnoreCase) &&
                                    editing.Undo().Succeeded;

            var changedDraft = editing.CreateEncounterPolicyDraftUx2LwG(fixture.TargetId)! with
            {
                Strategy = "challenge-budget-evidence",
                DeterministicSeed = 6_007,
                PreviewComposition = true,
                ActivationMode = EncounterActivationMode.Script,
                IntervalSeconds = 17,
                ScriptId = "scripts/encounter_activate.forge",
                Distribution = EncounterSpawnDistribution.Even,
            };
            var approval = editing.PreviewEncounterPolicyUx2LwG(changedDraft);
            var changedAfterPreview = ApplyUx2LwFIntermediateMutation(editing, fixture.NeighborId, 22);
            var staleApply = editing.ApplyApprovedEncounterPolicyUx2LwG();
            var staleApprovalBlocked = approval.Succeeded && changedAfterPreview && !staleApply.Succeeded &&
                                       staleApply.Message.Contains("Prévisualisez", StringComparison.Ordinal) &&
                                       editing.Undo().Succeeded;

            changedDraft = editing.CreateEncounterPolicyDraftUx2LwG(fixture.TargetId)! with
            {
                Strategy = "challenge-budget-evidence",
                DeterministicSeed = 6_007,
                PreviewComposition = true,
                ActivationMode = EncounterActivationMode.Script,
                IntervalSeconds = 17,
                ScriptId = "scripts/encounter_activate.forge",
                Distribution = EncounterSpawnDistribution.Even,
            };
            var historyBefore = editing.History!;
            var projectBefore = editing.Project;
            var revisionBefore = historyBefore.State.Revision;
            var journalBefore = historyBefore.Journal.ToArray();
            var dirtyBefore = editing.IsDirty;
            var preview = editing.PreviewEncounterPolicyUx2LwG(changedDraft);
            var repeat = editing.PreviewEncounterPolicyUx2LwG(changedDraft);
            var dryRunPure = preview.Succeeded && repeat.Succeeded && preview.DryRunPure &&
                             ReferenceEquals(projectBefore, editing.Project) &&
                             ReferenceEquals(historyBefore, editing.History) &&
                             historyBefore.State.Revision == revisionBefore &&
                             historyBefore.Journal.SequenceEqual(journalBefore) && editing.IsDirty == dirtyBefore;
            var deterministicPreview = preview.Runtime.Composition == repeat.Runtime.Composition &&
                                       preview.Runtime.PartyAssessment == repeat.Runtime.PartyAssessment &&
                                       preview.Runtime.Boundary.Contains("aucun minuteur", StringComparison.Ordinal);
            var applied = editing.ApplyApprovedEncounterPolicyUx2LwG();
            if (!applied.Succeeded) return FailedUx2LwGSmoke();
            var appliedCatalog = editing.Project!.Module.WorldFeatures;
            var appliedTarget = appliedCatalog.Encounters.Single(item => item.Id == fixture.TargetId);
            var appliedNeighbor = appliedCatalog.Encounters.Single(item => item.Id == fixture.NeighborId);
            var targetNeighborsPreserved = ReferenceEquals(loadedTarget.Foundation, appliedTarget.Foundation) &&
                ReferenceEquals(loadedTarget.Respawn, appliedTarget.Respawn) &&
                ReferenceEquals(loadedTarget.Scheduler, appliedTarget.Scheduler) &&
                ReferenceEquals(loadedTarget.Reinforcements, appliedTarget.Reinforcements) &&
                ReferenceEquals(loadedTarget.Visibility, appliedTarget.Visibility) &&
                ReferenceEquals(loadedTarget.Cleanup, appliedTarget.Cleanup) &&
                ReferenceEquals(loadedTarget.Scenario, appliedTarget.Scenario) &&
                ReferenceEquals(loadedTarget.Statistics, appliedTarget.Statistics) &&
                ReferenceEquals(loadedTarget.Composition!.Difficulty, appliedTarget.Composition!.Difficulty) &&
                ReferenceEquals(loadedTarget.Composition.Groups, appliedTarget.Composition.Groups) &&
                ReferenceEquals(loadedTarget.Composition.Overrides, appliedTarget.Composition.Overrides);
            var projectNeighborsPreserved = ReferenceEquals(loadedNeighbor, appliedNeighbor) &&
                loadedProject.Blueprints.SequenceEqual(editing.Project.Blueprints) &&
                loadedProject.Areas.SequenceEqual(editing.Project.Areas) &&
                loadedProject.Dialogues.SequenceEqual(editing.Project.Dialogues) &&
                loadedProject.Quests.SequenceEqual(editing.Project.Quests);
            var collectionsPreserved = SameReferencesUx2LwF(initialCatalog.Placeables, appliedCatalog.Placeables) &&
                SameReferencesUx2LwF(initialCatalog.Doors, appliedCatalog.Doors) &&
                SameReferencesUx2LwF(initialCatalog.Triggers, appliedCatalog.Triggers) &&
                SameReferencesUx2LwF(initialCatalog.VehicleRoutes, appliedCatalog.VehicleRoutes) &&
                SameReferencesUx2LwF(initialCatalog.Merchants, appliedCatalog.Merchants) &&
                SameReferencesUx2LwF(initialCatalog.Companions, appliedCatalog.Companions) &&
                ReferenceEquals(initialCatalog.Scripts, appliedCatalog.Scripts) &&
                appliedCatalog.Encounters.Select(item => item.Id).SequenceEqual(
                    initialCatalog.Encounters.Select(item => item.Id));

            var undo = editing.Undo();
            var undone = editing.Project!.Module.WorldFeatures.Encounters.Single(item => item.Id == fixture.TargetId);
            var redo = editing.Redo();
            var redone = editing.Project!.Module.WorldFeatures.Encounters.Single(item => item.Id == fixture.TargetId);
            var undoRedo = undo.Succeeded && redo.Succeeded &&
                EncounterProfileJsonUx2LwF(undone, editing.Project!) ==
                EncounterProfileJsonUx2LwF(loadedTarget, loadedProject) &&
                EncounterProfileJsonUx2LwF(redone, editing.Project!) ==
                EncounterProfileJsonUx2LwF(appliedTarget, editing.Project!);
            var saved = editing.Save();
            var opened = saved.Succeeded && reopened.Open(tempRoot).Succeeded;
            var persisted = opened ? reopened.Project!.Module.WorldFeatures.Encounters.Single(item =>
                item.Id == fixture.TargetId) : null;
            var persistence = persisted is not null && persisted.Composition?.Selection == appliedTarget.Composition?.Selection &&
                persisted.Composition?.Activation == appliedTarget.Composition?.Activation &&
                ProjectJsonSerializer.Serialize(editing.Project!) == ProjectJsonSerializer.Serialize(reopened.Project!);
            reopened.Close();
            var canonicalCommand = editing.History!.Journal.Count >= 3 &&
                                   editing.History.Journal.All(item => item.CommandName == "set_world_feature_catalog");
            var structuredDiagnostics = new[] { invalidStrategy, invalidProvider, invalidInterval, invalidScript }
                .SelectMany(item => item.Diagnostics)
                .All(item => !string.IsNullOrWhiteSpace(item.Code) && !string.IsNullOrWhiteSpace(item.Path));
            var humanText = editing.EncounterChoicesUx2LwG.All(item => IsHumanUx2LwFText(item.Label)) &&
                            editing.EncounterScriptChoicesUx2LwG().All(item =>
                                IsHumanUx2LwFText(item.Label) && IsHumanUx2LwFText(item.Provenance));
            var succeeded = explicitSelection && foundationRequired && strategyValidation && providerGuard &&
                            activationValidation && realScriptValidation && deterministicPreview && dryRunPure &&
                            staleDraftBlocked && staleApprovalBlocked && canonicalCommand && undoRedo && persistence &&
                            targetNeighborsPreserved && projectNeighborsPreserved && collectionsPreserved &&
                            structuredDiagnostics && humanText;
            return new(succeeded, explicitSelection, foundationRequired, strategyValidation, providerGuard,
                activationValidation, realScriptValidation, deterministicPreview, dryRunPure, staleDraftBlocked,
                staleApprovalBlocked, canonicalCommand, undoRedo, persistence, targetNeighborsPreserved,
                projectNeighborsPreserved, collectionsPreserved, structuredDiagnostics, humanText);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_LW_G_CONTRACT_FAILED exception={exception.Message}");
            return FailedUx2LwGSmoke();
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
                Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_LW_G_CLEANUP_FAILED " + exception.Message);
            }
        }
    }

    private static StudioUx2LwGSmokeResult FailedUx2LwGSmoke() => new(
        false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false);
}
