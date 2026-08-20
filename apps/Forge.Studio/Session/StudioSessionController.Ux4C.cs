using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Commands;
using Forge.Application.Factions;
using Forge.Application.Projects;
using Forge.Domain.Factions;
using Forge.Domain.Projects;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Domain.Factions;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private StudioFactionMatrixDraftUx4C? _approvedFactionMatrixDraftUx4C;
    private FactionCatalog? _approvedFactionMatrixCandidateUx4C;
    private ProjectRevision? _approvedFactionMatrixRevisionUx4C;
    private string? _approvedFactionMatrixProjectFingerprintUx4C;
    private string? _approvedFactionMatrixDraftFingerprintUx4C;
    private string? _approvedFactionMatrixCandidateFingerprintUx4C;

    internal StudioFactionMatrixDraftUx4C? CreateFactionMatrixDraftUx4C(Guid focusFactionId = default)
    {
        var project = Project;
        var history = History;
        if (project is null || history is null || project.Module.Factions.Factions.Count < 2)
        {
            return null;
        }

        var catalog = project.Module.Factions;
        var selected = catalog.Factions.Any(item => item.Id == focusFactionId) ? focusFactionId : Guid.Empty;
        var profile = catalog.Reputation;
        var reputationId = profile?.Id ?? StableFactionMatrixIdUx4C(project.Id.Value, "reputation");
        var reaction = profile?.Reaction ?? new(
            StableFactionMatrixIdUx4C(reputationId, "reaction"), 1, "studio:ux4-c",
            ReputationScope.World, "world", true, true, true, true, true);
        var source = selected != Guid.Empty ? selected : catalog.Factions[0].Id;
        var target = catalog.Factions.First(item => item.Id != source).Id;
        return new(
            history.State.Revision,
            FingerprintProjectUx4A(project),
            catalog,
            selected,
            catalog.Factions.ToDictionary(
                item => item.Id,
                item => (IReadOnlyList<string>)item.Tags.ToArray()),
            catalog.Relationships.ToArray(),
            profile is not null,
            catalog.RequireReputationRuntime,
            profile?.HeatmapHistory.Select(CloneHeatmapUx4C).ToArray() ?? [],
            reaction,
            "player",
            source,
            target,
            0,
            0,
            0,
            null,
            null);
    }

    internal StudioFactionMatrixPreviewUx4C PreviewFactionMatrixUx4C(StudioFactionMatrixDraftUx4C draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelFactionMatrixUx4C();
        var project = Project;
        var history = History;
        if (project is null || history is null)
        {
            return FailedFactionMatrixPreviewUx4C("Aucun module n’est ouvert.", "studio.faction.project_missing");
        }
        if (draft.FocusFactionId == Guid.Empty ||
            !draft.SourceCatalog.Factions.Any(item => item.Id == draft.FocusFactionId))
        {
            return FailedFactionMatrixPreviewUx4C(
                "Sélectionnez explicitement une faction.", "studio.faction.selection_required");
        }
        if (history.State.Revision != draft.BaseRevision ||
            !string.Equals(FingerprintProjectUx4A(project), draft.SourceProjectFingerprint, StringComparison.Ordinal))
        {
            return FailedFactionMatrixPreviewUx4C(
                "Le projet a changé ; rechargez la surface avant l’aperçu.", "studio.faction.source_stale");
        }
        if (!TryBuildFactionMatrixCandidateUx4C(project, draft, out var candidate, out var diagnostics))
        {
            return new(false, false, "Configuration invalide.", string.Empty, string.Empty, diagnostics,
                TagsSummaryUx4C(draft.SourceCatalog, draft.FocusFactionId),
                MatrixSummaryUx4C(draft.SourceCatalog), ReactionSummaryUx4C(draft.SourceCatalog, draft),
                ValidationSummaryUx4C(diagnostics));
        }

        var candidateProject = WithFactionCatalogUx4A(project, candidate);
        var projectDiagnostics = ProjectValidator.Validate(candidateProject);
        if (projectDiagnostics.Count > 0)
        {
            var mapped = projectDiagnostics.Select(DiagnosticUx4C).ToArray();
            return new(false, false, "Le projet candidat est invalide.", string.Empty, string.Empty, mapped,
                TagsSummaryUx4C(candidate, draft.FocusFactionId), MatrixSummaryUx4C(candidate),
                ReactionSummaryUx4C(candidate, draft), ValidationSummaryUx4C(mapped));
        }

        var preview = _setFactionCatalogUx4A.Handle(history.State, new SetFactionCatalogCommand(
            CommandId.From(Guid.NewGuid()), history.State.Revision, candidate, DryRun: true));
        if (!preview.Succeeded)
        {
            var mapped = DiagnosticsUx4C(preview.Diagnostics);
            return new(false, false, "Le dry-run canonique a refusé la matrice.", string.Empty, string.Empty,
                mapped, TagsSummaryUx4C(candidate, draft.FocusFactionId), MatrixSummaryUx4C(candidate),
                ReactionSummaryUx4C(candidate, draft), ValidationSummaryUx4C(mapped));
        }

        var frozen = CloneFactionMatrixDraftUx4C(draft);
        _approvedFactionMatrixDraftUx4C = frozen;
        _approvedFactionMatrixCandidateUx4C = candidate;
        _approvedFactionMatrixRevisionUx4C = history.State.Revision;
        _approvedFactionMatrixProjectFingerprintUx4C = FingerprintProjectUx4A(project);
        _approvedFactionMatrixDraftFingerprintUx4C = FingerprintFactionMatrixDraftUx4C(frozen);
        _approvedFactionMatrixCandidateFingerprintUx4C = FingerprintCatalogUx4A(project, candidate);
        return new(true, true, "Aperçu canonique prêt · aucune donnée du module n’a été modifiée.",
            preview.Patch?.Summary ?? "Catalogue de factions mis à jour.",
            preview.Inverse?.Summary ?? "État précédent restaurable.", [],
            TagsSummaryUx4C(candidate, draft.FocusFactionId), MatrixSummaryUx4C(candidate),
            ReactionSummaryUx4C(candidate, draft), "Validation canonique : aucun diagnostic bloquant.");
    }

    internal StudioFactionMatrixActionResultUx4C ApplyApprovedFactionMatrixUx4C(
        StudioFactionMatrixDraftUx4C currentDraft)
    {
        var project = Project;
        var history = History;
        var approvedDraft = _approvedFactionMatrixDraftUx4C;
        var candidate = _approvedFactionMatrixCandidateUx4C;
        if (project is null || history is null || approvedDraft is null || candidate is null ||
            _approvedFactionMatrixRevisionUx4C is null || _approvedFactionMatrixProjectFingerprintUx4C is null ||
            _approvedFactionMatrixDraftFingerprintUx4C is null ||
            _approvedFactionMatrixCandidateFingerprintUx4C is null)
        {
            return new(false, "Prévisualisez avant d’appliquer.");
        }

        var stale = history.State.Revision != _approvedFactionMatrixRevisionUx4C ||
                    !string.Equals(FingerprintProjectUx4A(project),
                        _approvedFactionMatrixProjectFingerprintUx4C, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintFactionMatrixDraftUx4C(currentDraft),
                        _approvedFactionMatrixDraftFingerprintUx4C, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintFactionMatrixDraftUx4C(approvedDraft),
                        _approvedFactionMatrixDraftFingerprintUx4C, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCatalogUx4A(project, candidate),
                        _approvedFactionMatrixCandidateFingerprintUx4C, StringComparison.Ordinal);
        if (stale)
        {
            CancelFactionMatrixUx4C();
            return new(false, "Aperçu périmé ; rechargez puis prévisualisez de nouveau.");
        }

        var result = _setFactionCatalogUx4A.Handle(history.State, new SetFactionCatalogCommand(
            CommandId.From(Guid.NewGuid()), history.State.Revision, candidate, DryRun: false));
        CancelFactionMatrixUx4C();
        if (!result.Succeeded)
        {
            var diagnostic = DiagnosticsUx4C(result.Diagnostics).FirstOrDefault();
            return new(false, diagnostic?.HumanMessage ?? "La commande canonique a échoué.");
        }
        history.Record(result);
        InvalidateContentIndexes();
        return new(true, "Matrice factionnelle appliquée dans l’historique du projet.");
    }

    internal StudioFactionMatrixActionResultUx4C UndoFactionMatrixUx4C()
    {
        CancelFactionMatrixUx4C();
        var result = Undo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioFactionMatrixActionResultUx4C RedoFactionMatrixUx4C()
    {
        CancelFactionMatrixUx4C();
        var result = Redo();
        return new(result.Succeeded, result.Message);
    }

    internal void CancelFactionMatrixUx4C()
    {
        _approvedFactionMatrixDraftUx4C = null;
        _approvedFactionMatrixCandidateUx4C = null;
        _approvedFactionMatrixRevisionUx4C = null;
        _approvedFactionMatrixProjectFingerprintUx4C = null;
        _approvedFactionMatrixDraftFingerprintUx4C = null;
        _approvedFactionMatrixCandidateFingerprintUx4C = null;
    }

    internal void CloseUx4C() => CancelFactionMatrixUx4C();

    internal StudioFactionMatrixApprovalSnapshotUx4C CaptureFactionMatrixApprovalUx4C() => new(
        _approvedFactionMatrixDraftUx4C, _approvedFactionMatrixCandidateUx4C,
        _approvedFactionMatrixRevisionUx4C, _approvedFactionMatrixProjectFingerprintUx4C,
        _approvedFactionMatrixDraftFingerprintUx4C, _approvedFactionMatrixCandidateFingerprintUx4C);

    internal void RestoreFactionMatrixApprovalUx4C(StudioFactionMatrixApprovalSnapshotUx4C snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedFactionMatrixDraftUx4C = snapshot.Draft;
        _approvedFactionMatrixCandidateUx4C = snapshot.Candidate;
        _approvedFactionMatrixRevisionUx4C = snapshot.Revision;
        _approvedFactionMatrixProjectFingerprintUx4C = snapshot.ProjectFingerprint;
        _approvedFactionMatrixDraftFingerprintUx4C = snapshot.DraftFingerprint;
        _approvedFactionMatrixCandidateFingerprintUx4C = snapshot.CandidateFingerprint;
    }

    internal StudioFactionEvidenceProjectUx4A? CreateUx4CEvidenceProject()
    {
        var evidence = CreateUx4BEvidenceProject();
        if (evidence is null) return null;
        var catalog = evidence.Project.Module.Factions;
        if (catalog.Reputation is null || catalog.Factions.Count < 3 || catalog.Relationships.Count == 0)
            return null;
        if (catalog.Reputation.HeatmapHistory.Count >= 2 && catalog.Factions.All(item => item.Tags.Count > 0))
            return evidence;

        var now = new DateTimeOffset(2026, 8, 17, 13, 0, 0, TimeSpan.Zero);
        var factions = catalog.Factions.Select((item, index) => item.Tags.Count > 0
            ? item
            : item with { Tags = [index == 0 ? "guardian" : index == 1 ? "undead" : "adventurer"] })
            .ToArray();
        var before = catalog.Relationships.Select(item => item with { }).ToArray();
        var after = before.Select((item, index) => index == 0
            ? item with { Score = Math.Clamp(item.Score + 1, -100, 100), Explanation = "preuve UX4-C" }
            : item).ToArray();
        var profile = catalog.Reputation;
        var history = new[]
        {
            new FactionHeatmapSnapshot(StableFactionMatrixIdUx4C(profile.Id, "baseline"), 1,
                "studio:ux4-c-evidence", now, "État initial", before),
            new FactionHeatmapSnapshot(StableFactionMatrixIdUx4C(profile.Id, "comparison"), 1,
                "studio:ux4-c-evidence", now.AddMinutes(1), "Comparaison", after),
        };
        var enriched = catalog with
        {
            Factions = factions,
            Reputation = profile with { HeatmapHistory = history },
        };
        if (FactionValidator.Validate(enriched).Count > 0) return null;
        return new(WithFactionCatalogUx4A(evidence.Project, enriched), evidence.UsesRealCryptSource,
            evidence.UsesCanonicalFallback);
    }

    private static bool TryBuildFactionMatrixCandidateUx4C(
        ProjectDefinition project,
        StudioFactionMatrixDraftUx4C draft,
        out FactionCatalog candidate,
        out IReadOnlyList<StudioFactionMatrixDiagnosticUx4C> diagnostics)
    {
        candidate = draft.SourceCatalog;
        if (!draft.TagsByFaction.Keys.All(id => draft.SourceCatalog.Factions.Any(item => item.Id == id)))
        {
            diagnostics = [new("studio.faction.tags_orphan", "module/factions/definitions",
                "Une collection de tags référence une faction inconnue.")];
            return false;
        }
        if (!draft.ReputationConfigured && draft.HeatmapHistory.Count > 0)
        {
            diagnostics = [new("studio.faction.reputation_not_configured", "module/factions/reputation",
                "Activez la réputation avant de conserver un historique de matrice.")];
            return false;
        }
        var factions = draft.SourceCatalog.Factions.Select(item => item with
        {
            Tags = draft.TagsByFaction.TryGetValue(item.Id, out var tags) ? tags.ToArray() : item.Tags,
        }).ToArray();
        FactionReputationProfile? profile = null;
        if (draft.ReputationConfigured)
        {
            var reputationId = draft.SourceCatalog.Reputation?.Id ??
                               StableFactionMatrixIdUx4C(project.Id.Value, "reputation");
            profile = (draft.SourceCatalog.Reputation ?? new(
                reputationId, 1, "studio:ux4-c", [], [], draft.Reaction)) with
            {
                HeatmapHistory = draft.HeatmapHistory.Select(CloneHeatmapUx4C).ToArray(),
                Reaction = draft.Reaction,
            };
        }
        candidate = draft.SourceCatalog with
        {
            Factions = factions,
            Relationships = draft.Relationships.ToArray(),
            Reputation = profile,
            RequireReputationRuntime = draft.RequireReputationRuntime,
        };
        diagnostics = FactionValidator.Validate(candidate).Select(DiagnosticUx4C).ToArray();
        return diagnostics.Count == 0;
    }

    private static StudioFactionMatrixDraftUx4C CloneFactionMatrixDraftUx4C(
        StudioFactionMatrixDraftUx4C draft) => draft with
        {
            TagsByFaction = draft.TagsByFaction.ToDictionary(
            item => item.Key, item => (IReadOnlyList<string>)item.Value.ToArray()),
            Relationships = draft.Relationships.ToArray(),
            HeatmapHistory = draft.HeatmapHistory.Select(CloneHeatmapUx4C).ToArray(),
        };

    private static FactionHeatmapSnapshot CloneHeatmapUx4C(FactionHeatmapSnapshot item) =>
        item with { Relationships = item.Relationships.ToArray() };

    private static string FingerprintFactionMatrixDraftUx4C(StudioFactionMatrixDraftUx4C draft)
    {
        var payload = new
        {
            draft.BaseRevision,
            draft.SourceProjectFingerprint,
            draft.FocusFactionId,
            Tags = draft.TagsByFaction.OrderBy(item => item.Key)
                .Select(item => new { item.Key, Values = item.Value.ToArray() }).ToArray(),
            Relationships = draft.Relationships,
            Heatmap = draft.HeatmapHistory,
            draft.ReputationConfigured,
            draft.RequireReputationRuntime,
            draft.Reaction,
            draft.SubjectId,
            draft.ReactionSourceId,
            draft.ReactionTargetId,
            draft.DisguiseModifier,
            draft.CrimeModifier,
            draft.DialogueModifier,
            draft.ZoneOverride,
            draft.GameMasterOverride,
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))));
    }

    private static Guid StableFactionMatrixIdUx4C(Guid basis, string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{basis:D}:{value}"));
        return new Guid(bytes[..16]);
    }

    private static StudioFactionMatrixPreviewUx4C FailedFactionMatrixPreviewUx4C(string message, string code) =>
        new(false, false, message, string.Empty, string.Empty,
            [new(code, "module/factions", HumanizeFactionMatrixDiagnosticUx4C(code, message))],
            "Tags non évalués.", "Matrice non évaluée.", "Réaction non évaluée.", "Validation interrompue.");

    private static IReadOnlyList<StudioFactionMatrixDiagnosticUx4C> DiagnosticsUx4C(
        IReadOnlyList<CommandDiagnostic> diagnostics) => diagnostics.Select(item =>
    {
        var separator = item.Message.IndexOf(':');
        var path = separator > 0 ? item.Message[..separator] : "module/factions";
        var message = separator > 0 ? item.Message[(separator + 1)..].Trim() : item.Message;
        return new StudioFactionMatrixDiagnosticUx4C(
            item.Code, path, HumanizeFactionMatrixDiagnosticUx4C(item.Code, message));
    }).ToArray();

    private static StudioFactionMatrixDiagnosticUx4C DiagnosticUx4C(FactionDiagnostic item) =>
        new(item.Code, item.Path, HumanizeFactionMatrixDiagnosticUx4C(item.Code, item.Message));

    private static StudioFactionMatrixDiagnosticUx4C DiagnosticUx4C(DomainDiagnostic item) =>
        new(item.Code, item.Path, HumanizeFactionMatrixDiagnosticUx4C(item.Code, item.Message));

    private static string HumanizeFactionMatrixDiagnosticUx4C(string code, string fallback) => code switch
    {
        "RPG_FACTION_TAG_INVALID" => "Tag invalide : utilisez uniquement minuscules, chiffres, tiret, point ou souligné.",
        "RPG_FACTION_TAG_DUPLICATE" => "Un tag ne peut apparaître qu’une fois dans une faction.",
        "RPG_FACTION_INHERITANCE_CYCLE" => "Le graphe d’héritage contient un cycle.",
        "RPG_FACTION_PARENT_ORPHAN" => "Une faction parente n’existe plus.",
        "RPG_FACTION_BANDS_OVERLAP" => "Deux bandes de même priorité se chevauchent.",
        "RPG_FACTION_MEMBER_ORPHAN" => "Un membre référence une faction absente.",
        "RPG_FACTION_HEATMAP_SNAPSHOT_INVALID" => "Un instantané de matrice est incomplet.",
        "RPG_FACTION_HEATMAP_RELATION_INVALID" => "Une relation historique est invalide.",
        "RPG_FACTION_REACTION_POLICY_INVALID" => "La politique de réaction est incomplète.",
        _ => fallback,
    };

    private static string TagsSummaryUx4C(FactionCatalog catalog, Guid focusId)
    {
        var selected = catalog.Factions.Single(item => item.Id == focusId);
        var effective = FactionRuntimeResolver.EffectiveDefinition(catalog, focusId).Tags;
        return $"{selected.DisplayName} · {selected.Tags.Count} tag(s) directs · {effective.Count} tag(s) effectifs";
    }

    private static string MatrixSummaryUx4C(FactionCatalog catalog)
    {
        var history = catalog.Reputation?.HeatmapHistory ?? [];
        var differences = history.Count >= 2
            ? FactionReputationService.Compare(history[^2], history[^1]).Count
            : 0;
        var target = catalog.Factions[0].Id;
        var simulated = FactionReputationService.SimulateEncounter(
            catalog, catalog.Factions.Where(item => item.Id != target).Select(item => item.Id), target);
        return $"{catalog.Relationships.Count} direction(s) · {history.Count} instantané(s) · " +
               $"{differences} différence(s) · {simulated.Count} réaction(s) simulée(s)";
    }

    private static string ReactionSummaryUx4C(FactionCatalog catalog, StudioFactionMatrixDraftUx4C draft)
    {
        if (catalog.Reputation is null) return "Profil de réputation absent : réaction optionnelle non évaluée.";
        var context = new ReactionContext(draft.SubjectId, draft.ReactionSourceId, draft.ReactionTargetId,
            new DateTimeOffset(2026, 8, 17, 14, 0, 0, TimeSpan.Zero), draft.DisguiseModifier,
            draft.CrimeModifier, draft.DialogueModifier, draft.GameMasterOverride, draft.ZoneOverride);
        var result = FactionReputationService.ResolveReaction(
            catalog, FactionReputationService.InitialState(catalog), context);
        return $"Score {NumberUx4C(result.Score)} · disposition {result.Disposition} · " +
               string.Join(" → ", result.Explanation);
    }

    private static string ValidationSummaryUx4C(IReadOnlyList<StudioFactionMatrixDiagnosticUx4C> diagnostics) =>
        diagnostics.Count == 0
            ? "Validation canonique : aucun diagnostic bloquant."
            : $"Validation canonique : {diagnostics.Count} diagnostic(s) bloquant(s).";

    private static string NumberUx4C(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    internal StudioUx4CSmokeResult RunUx4CContractSmoke()
    {
        var result = new StudioUx4CSmokeResult(false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false);
        var evidence = CreateUx4CEvidenceProject();
        if (evidence is null) return result;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4c-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(root, evidence.Project).Succeeded || !editing.Open(root).Succeeded)
                return result;
            var choices = editing.FactionChoicesUx4A;
            var empty = editing.CreateFactionMatrixDraftUx4C();
            if (empty is null || choices.Count < 3) return result;
            var explicitSelection = !editing.PreviewFactionMatrixUx4C(empty).Succeeded;
            var focus = choices[0].FactionId;
            var draft = editing.CreateFactionMatrixDraftUx4C(focus)!;
            var tags = draft.TagsByFaction.ToDictionary(item => item.Key,
                item => (IReadOnlyList<string>)item.Value.ToArray());
            tags[focus] = tags[focus].Concat(["ux4-proof"]).Distinct(StringComparer.Ordinal).ToArray();
            var relationships = draft.Relationships.Select((item, index) => index == 0
                ? item with { Score = Math.Clamp(item.Score + 1, -100, 100), Explanation = "preuve UX4-C" }
                : item).ToArray();
            var snapshots = draft.HeatmapHistory.Select(CloneHeatmapUx4C).ToList();
            var snapshot = new FactionHeatmapSnapshot(
                StableFactionMatrixIdUx4C(draft.SourceCatalog.Reputation!.Id, "smoke"), 1,
                "studio:ux4-c-smoke", new DateTimeOffset(2026, 8, 17, 14, 0, 0, TimeSpan.Zero),
                "Preuve Studio", relationships);
            snapshots.RemoveAll(item => item.Id == snapshot.Id);
            snapshots.Add(snapshot);
            var changed = draft with
            {
                TagsByFaction = tags,
                Relationships = relationships,
                ReputationConfigured = true,
                RequireReputationRuntime = true,
                HeatmapHistory = snapshots,
                Reaction = draft.Reaction with { ApplyDisguise = !draft.Reaction.ApplyDisguise },
                DisguiseModifier = 7,
                CrimeModifier = -3,
                DialogueModifier = 2,
                ZoneOverride = -12,
                GameMasterOverride = 44,
            };
            var sourceProject = editing.Project!;
            var beforeJson = ProjectJsonSerializer.Serialize(sourceProject);
            var preview = editing.PreviewFactionMatrixUx4C(changed);
            var dryRun = preview.Succeeded && ReferenceEquals(sourceProject, editing.Project) &&
                         beforeJson == ProjectJsonSerializer.Serialize(editing.Project!);
            var invalidTags = tags.ToDictionary(item => item.Key,
                item => item.Key == focus ? (IReadOnlyList<string>)["Bad Tag"] : item.Value);
            var invalid = editing.PreviewFactionMatrixUx4C(changed with { TagsByFaction = invalidTags });
            var invalidBlocked = !invalid.Succeeded && invalid.Diagnostics.Any(item =>
                item.Code == "RPG_FACTION_TAG_INVALID");
            var invalidCatalog = changed.SourceCatalog with
            {
                Members = [new FactionMember("orphan", Guid.NewGuid())],
                Bands = [new("a", -100, 10, 1), new("b", 0, 100, 1)],
            };
            var validationDiagnostics = FactionValidator.Validate(invalidCatalog);
            var validation = validationDiagnostics.Any(item => item.Code == "RPG_FACTION_MEMBER_ORPHAN") &&
                             validationDiagnostics.Any(item => item.Code == "RPG_FACTION_BANDS_OVERLAP");

            var staleCatalog = editing.Project!.Module.Factions with
            {
                RequireFoundationRuntime = !editing.Project.Module.Factions.RequireFoundationRuntime,
            };
            var intermediate = ApplyFactionCatalogForSmokeUx4A(editing, staleCatalog);
            var staleDraft = editing.PreviewFactionMatrixUx4C(changed);
            var staleDraftBlocked = intermediate && !staleDraft.Succeeded;

            draft = editing.CreateFactionMatrixDraftUx4C(focus)!;
            tags = draft.TagsByFaction.ToDictionary(item => item.Key,
                item => (IReadOnlyList<string>)item.Value.ToArray());
            tags[focus] = tags[focus].Concat(["ux4-proof"]).Distinct(StringComparer.Ordinal).ToArray();
            changed = draft with { TagsByFaction = tags, Reaction = draft.Reaction with { ApplyCrimes = !draft.Reaction.ApplyCrimes } };
            var approval = editing.PreviewFactionMatrixUx4C(changed);
            var secondCatalog = editing.Project!.Module.Factions with
            {
                RequireFoundationRuntime = !editing.Project.Module.Factions.RequireFoundationRuntime,
            };
            var secondIntermediate = ApplyFactionCatalogForSmokeUx4A(editing, secondCatalog);
            var staleApproval = editing.ApplyApprovedFactionMatrixUx4C(changed);
            var staleApprovalBlocked = approval.Succeeded && secondIntermediate && !staleApproval.Succeeded;

            draft = editing.CreateFactionMatrixDraftUx4C(focus)!;
            tags = draft.TagsByFaction.ToDictionary(item => item.Key,
                item => (IReadOnlyList<string>)item.Value.ToArray());
            tags[focus] = tags[focus].Concat(["ux4-proof"]).Distinct(StringComparer.Ordinal).ToArray();
            relationships = draft.Relationships.Select((item, index) => index == 0
                ? item with { Score = Math.Clamp(item.Score + 1, -100, 100), Explanation = "preuve UX4-C" }
                : item).ToArray();
            snapshots = draft.HeatmapHistory.Select(CloneHeatmapUx4C).ToList();
            snapshot = snapshot with { Relationships = relationships };
            snapshots.RemoveAll(item => item.Id == snapshot.Id);
            snapshots.Add(snapshot);
            changed = draft with
            {
                TagsByFaction = tags,
                Relationships = relationships,
                HeatmapHistory = snapshots,
                Reaction = draft.Reaction with { ApplyDialogue = !draft.Reaction.ApplyDialogue },
                GameMasterOverride = 44,
            };
            var finalPreview = editing.PreviewFactionMatrixUx4C(changed);
            var beforeApply = editing.Project!;
            var beforeCatalog = beforeApply.Module.Factions;
            var apply = editing.ApplyApprovedFactionMatrixUx4C(changed);
            var appliedProject = editing.Project;
            var applied = appliedProject?.Module.Factions;
            var tagsApplied = applied?.Factions.Single(item => item.Id == focus).Tags.Contains("ux4-proof") == true;
            var matrixApplied = applied?.Relationships.First().Explanation == "preuve UX4-C";
            var reactionApplied = applied?.Reputation?.Reaction.ApplyDialogue == changed.Reaction.ApplyDialogue;
            var historyApplied = applied?.Reputation?.HeatmapHistory.Any(item => item.Id == snapshot.Id) == true;
            var exported = FactionReputationService.ExportSnapshot(snapshot);
            var imported = FactionReputationService.ImportSnapshot(exported);
            var importExport = FactionReputationService.ExportSnapshot(imported) == exported;
            var neighbors = applied is not null &&
                            ReferenceEquals(beforeCatalog.Bands, applied.Bands) &&
                            ReferenceEquals(beforeCatalog.Members, applied.Members) &&
                            ReferenceEquals(beforeCatalog.Advanced, applied.Advanced) &&
                            ReferenceEquals(beforeCatalog.PersonalReputation, applied.PersonalReputation) &&
                            ReferenceEquals(beforeCatalog.Reputation?.Transactions, applied.Reputation?.Transactions);
            var undo = editing.UndoFactionMatrixUx4C();
            var undone = undo.Succeeded && ReferenceEquals(beforeApply, editing.Project);
            var redo = editing.RedoFactionMatrixUx4C();
            var redone = redo.Succeeded && ReferenceEquals(appliedProject, editing.Project);
            var saved = redone && editing.Save().Succeeded;
            var persisted = saved && reopened.Open(root).Succeeded &&
                            ProjectJsonSerializer.Serialize(reopened.Project!) == ProjectJsonSerializer.Serialize(editing.Project!);
            result = new(
                finalPreview.Succeeded && tagsApplied && matrixApplied && reactionApplied && historyApplied &&
                validation && importExport && invalidBlocked && dryRun && staleDraftBlocked && staleApprovalBlocked &&
                apply.Succeeded && undone && redone && persisted && neighbors,
                explicitSelection, tagsApplied, matrixApplied && historyApplied, reactionApplied, validation,
                importExport, invalidBlocked, dryRun, staleDraftBlocked, staleApprovalBlocked,
                finalPreview.Succeeded && apply.Succeeded, undone && redone, persisted, neighbors, false);
        }
        catch
        {
            return result;
        }
        finally
        {
            reopened.Close();
            editing.Close();
            var released = DeleteUx4CSmokeRoot(root);
            result = result with { Succeeded = result.Succeeded && released, TemporaryWorkspaceReleased = released };
        }
        return result;
    }

    private static bool DeleteUx4CSmokeRoot(string root)
    {
        try
        {
            var full = Path.GetFullPath(root);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) +
                       Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4c-smoke-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full) && !File.Exists(full);
        }
        catch
        {
            return false;
        }
    }
}
