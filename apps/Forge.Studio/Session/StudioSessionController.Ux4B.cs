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
    private StudioFactionRuntimeDraftUx4B? _approvedFactionRuntimeDraftUx4B;
    private FactionCatalog? _approvedFactionRuntimeCandidateUx4B;
    private ProjectRevision? _approvedFactionRuntimeRevisionUx4B;
    private string? _approvedFactionRuntimeProjectFingerprintUx4B;
    private string? _approvedFactionRuntimeDraftFingerprintUx4B;
    private string? _approvedFactionRuntimeCandidateFingerprintUx4B;

    internal StudioFactionRuntimeDraftUx4B? CreateFactionRuntimeDraftUx4B()
    {
        var project = Project;
        var history = History;
        if (project is null || history is null || project.Module.Factions.Factions.Count < 2)
        {
            return null;
        }

        var catalog = project.Module.Factions;
        return new(
            history.State.Revision,
            FingerprintProjectUx4A(project),
            catalog,
            catalog.PersonalReputation is not null,
            catalog.EffectivePersonalReputation.ToArray(),
            catalog.Advanced is not null,
            catalog.RequireAdvancedRuntime,
            catalog.Advanced?.WitnessPolicies.ToArray() ?? [],
            catalog.Advanced?.Memberships.ToArray() ?? [],
            CloneDynamicsUx4B(catalog.Advanced?.DynamicFactions ?? []),
            catalog.Reputation is not null,
            catalog.RequireReputationRuntime,
            catalog.Reputation?.Transactions.ToArray() ?? []);
    }

    internal StudioFactionRuntimePreviewUx4B PreviewFactionRuntimeUx4B(StudioFactionRuntimeDraftUx4B draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelFactionRuntimeUx4B();
        var project = Project;
        var history = History;
        if (project is null || history is null)
        {
            return FailedFactionRuntimePreviewUx4B("Aucun module n’est ouvert.", "studio.faction.project_missing");
        }
        if (history.State.Revision != draft.BaseRevision ||
            !string.Equals(FingerprintProjectUx4A(project), draft.SourceProjectFingerprint, StringComparison.Ordinal))
        {
            return FailedFactionRuntimePreviewUx4B(
                "Le projet a changé depuis l’ouverture de la fiche. Rechargez avant de prévisualiser.",
                SetFactionCatalogHandler.RevisionConflictCode);
        }
        if (!TryBuildFactionRuntimeCandidateUx4B(project, draft, out var candidate, out var diagnostics))
        {
            return new(false, false, "Configuration refusée avant toute écriture.", string.Empty, string.Empty,
                diagnostics, PersonalSummaryUx4B(draft.SourceCatalog), ReputationSummaryUx4B(draft.SourceCatalog),
                WitnessSummaryUx4B(draft.SourceCatalog), MembershipSummaryUx4B(draft.SourceCatalog),
                DynamicSummaryUx4B(draft.SourceCatalog));
        }

        var command = new SetFactionCatalogCommand(
            CommandId.From(Guid.NewGuid()), draft.BaseRevision, candidate, DryRun: true);
        var preview = _setFactionCatalogUx4A.Handle(history.State, command);
        if (!preview.Succeeded)
        {
            return new(false, false, "Le validateur canonique refuse cette modification.", string.Empty,
                string.Empty, DiagnosticsUx4B(preview.Diagnostics), PersonalSummaryUx4B(candidate),
                ReputationSummaryUx4B(candidate), WitnessSummaryUx4B(candidate), MembershipSummaryUx4B(candidate),
                DynamicSummaryUx4B(candidate));
        }

        var projectDiagnostics = ProjectValidator.Validate(WithFactionCatalogUx4A(project, candidate));
        if (projectDiagnostics.Count > 0)
        {
            return new(false, false, "Le projet contient une référence incompatible avec cette modification.",
                string.Empty, string.Empty, projectDiagnostics.Select(DiagnosticUx4B).ToArray(),
                PersonalSummaryUx4B(candidate), ReputationSummaryUx4B(candidate), WitnessSummaryUx4B(candidate),
                MembershipSummaryUx4B(candidate), DynamicSummaryUx4B(candidate));
        }

        var frozen = CloneFactionRuntimeDraftUx4B(draft);
        _approvedFactionRuntimeDraftUx4B = frozen;
        _approvedFactionRuntimeCandidateUx4B = candidate;
        _approvedFactionRuntimeRevisionUx4B = history.State.Revision;
        _approvedFactionRuntimeProjectFingerprintUx4B = FingerprintProjectUx4A(project);
        _approvedFactionRuntimeDraftFingerprintUx4B = FingerprintFactionRuntimeDraftUx4B(frozen);
        _approvedFactionRuntimeCandidateFingerprintUx4B = FingerprintCatalogUx4A(project, candidate);
        return new(true, true, "Aperçu canonique prêt · aucune donnée du module n’a été modifiée.",
            preview.Patch?.Summary ?? "Catalogue de factions prévisualisé.",
            preview.Inverse?.Summary ?? "État précédent restaurable.", [], PersonalSummaryUx4B(candidate),
            ReputationSummaryUx4B(candidate), WitnessSummaryUx4B(candidate), MembershipSummaryUx4B(candidate),
            DynamicSummaryUx4B(candidate));
    }

    internal StudioFactionRuntimeActionResultUx4B ApplyApprovedFactionRuntimeUx4B(
        StudioFactionRuntimeDraftUx4B currentDraft)
    {
        ArgumentNullException.ThrowIfNull(currentDraft);
        var project = Project;
        var history = History;
        var approvedDraft = _approvedFactionRuntimeDraftUx4B;
        var candidate = _approvedFactionRuntimeCandidateUx4B;
        if (project is null || history is null || approvedDraft is null || candidate is null ||
            _approvedFactionRuntimeRevisionUx4B is null || _approvedFactionRuntimeProjectFingerprintUx4B is null ||
            _approvedFactionRuntimeDraftFingerprintUx4B is null ||
            _approvedFactionRuntimeCandidateFingerprintUx4B is null)
        {
            return new(false, "Prévisualisez la fiche avant de l’appliquer.");
        }

        var stale = history.State.Revision != _approvedFactionRuntimeRevisionUx4B ||
                    !string.Equals(FingerprintProjectUx4A(project),
                        _approvedFactionRuntimeProjectFingerprintUx4B, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintFactionRuntimeDraftUx4B(currentDraft),
                        _approvedFactionRuntimeDraftFingerprintUx4B, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintFactionRuntimeDraftUx4B(approvedDraft),
                        _approvedFactionRuntimeDraftFingerprintUx4B, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCatalogUx4A(project, candidate),
                        _approvedFactionRuntimeCandidateFingerprintUx4B, StringComparison.Ordinal);
        if (stale)
        {
            CancelFactionRuntimeUx4B();
            return new(false, "Le projet ou la fiche a changé. Prévisualisez à nouveau avant d’appliquer.");
        }

        var result = _setFactionCatalogUx4A.Handle(history.State, new(
            CommandId.From(Guid.NewGuid()), history.State.Revision, candidate, DryRun: false));
        CancelFactionRuntimeUx4B();
        if (!result.Succeeded)
        {
            var diagnostic = DiagnosticsUx4B(result.Diagnostics).FirstOrDefault();
            return new(false, diagnostic?.HumanMessage ?? "La commande canonique a refusé la modification.");
        }
        history.Record(result);
        InvalidateContentIndexes();
        return new(true, "Politiques factionnelles appliquées · Ctrl+Z pour annuler.");
    }

    internal StudioFactionRuntimeActionResultUx4B UndoFactionRuntimeUx4B()
    {
        CancelFactionRuntimeUx4B();
        var result = Undo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioFactionRuntimeActionResultUx4B RedoFactionRuntimeUx4B()
    {
        CancelFactionRuntimeUx4B();
        var result = Redo();
        return new(result.Succeeded, result.Message);
    }

    internal void CancelFactionRuntimeUx4B()
    {
        _approvedFactionRuntimeDraftUx4B = null;
        _approvedFactionRuntimeCandidateUx4B = null;
        _approvedFactionRuntimeRevisionUx4B = null;
        _approvedFactionRuntimeProjectFingerprintUx4B = null;
        _approvedFactionRuntimeDraftFingerprintUx4B = null;
        _approvedFactionRuntimeCandidateFingerprintUx4B = null;
    }

    internal void CloseUx4B() => CancelFactionRuntimeUx4B();

    internal StudioFactionRuntimeApprovalSnapshotUx4B CaptureFactionRuntimeApprovalUx4B() => new(
        _approvedFactionRuntimeDraftUx4B,
        _approvedFactionRuntimeCandidateUx4B,
        _approvedFactionRuntimeRevisionUx4B,
        _approvedFactionRuntimeProjectFingerprintUx4B,
        _approvedFactionRuntimeDraftFingerprintUx4B,
        _approvedFactionRuntimeCandidateFingerprintUx4B);

    internal void RestoreFactionRuntimeApprovalUx4B(StudioFactionRuntimeApprovalSnapshotUx4B snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedFactionRuntimeDraftUx4B = snapshot.Draft;
        _approvedFactionRuntimeCandidateUx4B = snapshot.Candidate;
        _approvedFactionRuntimeRevisionUx4B = snapshot.Revision;
        _approvedFactionRuntimeProjectFingerprintUx4B = snapshot.ProjectFingerprint;
        _approvedFactionRuntimeDraftFingerprintUx4B = snapshot.DraftFingerprint;
        _approvedFactionRuntimeCandidateFingerprintUx4B = snapshot.CandidateFingerprint;
    }

    internal StudioFactionEvidenceProjectUx4A? CreateUx4BEvidenceProject()
    {
        var evidence = CreateUx4AEvidenceProject();
        if (evidence is null)
        {
            return null;
        }
        var catalog = evidence.Project.Module.Factions;
        var complete = catalog.PersonalReputation is { Count: > 0 } &&
                       catalog.Reputation?.Transactions is { Count: > 0 } &&
                       catalog.Advanced?.WitnessPolicies is { Count: > 0 } &&
                       catalog.Advanced.Memberships.Count >= 2 &&
                       catalog.Advanced.DynamicFactions is { Count: > 0 };
        if (complete)
        {
            return evidence;
        }

        var factions = catalog.Factions.Where(item => !item.Archived).Take(3).ToArray();
        if (factions.Length < 3)
        {
            return null;
        }
        var source = factions[2];
        var target = factions[1];
        var now = new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);
        var advancedId = catalog.Advanced?.Id ?? StableFactionRuntimeIdUx4B(evidence.Project.Id.Value, "advanced");
        var witness = new FactionWitnessPolicy(
            StableFactionRuntimeIdUx4B(advancedId, "witness"), target.Id, 0.55, 0.45, 35, 900, -12, true);
        var memberships = new[]
        {
            new FactionMembership(StableFactionRuntimeIdUx4B(advancedId, "member-primary"), 1,
                "studio:ux4-b-evidence", "hero", source.Id, "éclaireur", null, 85, 1, true, null),
            new FactionMembership(StableFactionRuntimeIdUx4B(advancedId, "member-cover"), 1,
                "studio:ux4-b-evidence", "hero", target.Id, "novice", "Pèlerin des cendres", -35, 0.2,
                false, now.AddYears(4)),
        };
        var diplomacy = new FactionDiplomacyState(
            StableFactionRuntimeIdUx4B(advancedId, "diplomacy"), source.Id, FactionDiplomacyStatus.War,
            now, null, "intrusion dans la crypte");
        var dynamics = new[]
        {
            new DynamicFactionDefinition(StableFactionRuntimeIdUx4B(advancedId, "dynamic"), 1,
                "studio:ux4-b-evidence", target.Id, DynamicFactionKind.Runtime, now, now.AddYears(4), true,
                true, 1, [diplomacy]),
        };
        var advanced = new FactionAdvancedProfile(advancedId, 1,
            catalog.Advanced?.Provenance ?? "studio:ux4-b-evidence", true,
            catalog.Advanced?.Inheritance ?? [], [witness], memberships, dynamics);
        var personal = new[]
        {
            new PersonalReputationOverlay(StableFactionRuntimeIdUx4B(evidence.Project.Id.Value, "personal"),
                1, "studio:ux4-b-evidence", "hero", source.Id, target.Id, 35, now.AddYears(4),
                "trêve du gardien", PersonalReputationSubjectKind.Player),
        };
        var reputationId = StableFactionRuntimeIdUx4B(evidence.Project.Id.Value, "reputation");
        var reaction = new FactionReactionPolicy(StableFactionRuntimeIdUx4B(reputationId, "reaction"), 1,
            "studio:ux4-b-evidence", ReputationScope.Campaign, "crypt-of-ashes", true, true, true, true, true);
        var transactions = new[]
        {
            new ReputationTransaction(StableFactionRuntimeIdUx4B(reputationId, "transaction"), 1,
                "studio:ux4-b-evidence", ReputationScope.Campaign, "crypt-of-ashes", source.Id, target.Id,
                15, "pacte restauré", now),
        };
        var reputation = new FactionReputationProfile(reputationId, 1, "studio:ux4-b-evidence",
            transactions, [], reaction);
        var enriched = catalog with
        {
            Advanced = advanced,
            RequireAdvancedRuntime = true,
            PersonalReputation = personal,
            RequireFoundationRuntime = true,
            Reputation = reputation,
            RequireReputationRuntime = true,
        };
        if (FactionValidator.Validate(enriched).Count > 0)
        {
            return null;
        }
        return new(WithFactionCatalogUx4A(evidence.Project, enriched), false, true);
    }

    private static bool TryBuildFactionRuntimeCandidateUx4B(
        ProjectDefinition project,
        StudioFactionRuntimeDraftUx4B draft,
        out FactionCatalog candidate,
        out IReadOnlyList<StudioFactionRuntimeDiagnosticUx4B> diagnostics)
    {
        candidate = draft.SourceCatalog;
        if (!string.Equals(FingerprintCatalogUx4A(project, project.Module.Factions),
                FingerprintCatalogUx4A(project, draft.SourceCatalog), StringComparison.Ordinal))
        {
            diagnostics = [new(SetFactionCatalogHandler.RevisionConflictCode, "module/factions",
                "Le catalogue source a changé. Rechargez la fiche.")];
            return false;
        }
        if (!draft.PersonalConfigured && draft.PersonalReputation.Count > 0)
        {
            diagnostics = [new("studio.faction.personal_not_configured", "module/factions/personalReputation",
                "Activez la réputation personnelle avant d’ajouter des entrées.")];
            return false;
        }
        if (!draft.AdvancedConfigured &&
            (draft.WitnessPolicies.Count > 0 || draft.Memberships.Count > 0 || draft.DynamicFactions.Count > 0))
        {
            diagnostics = [new("studio.faction.advanced_not_configured", "module/factions/advanced",
                "Activez le profil avancé avant d’ajouter témoins, affiliations ou factions dynamiques.")];
            return false;
        }
        if (!draft.ReputationConfigured && draft.ReputationTransactions.Count > 0)
        {
            diagnostics = [new("studio.faction.reputation_not_configured", "module/factions/reputation",
                "Activez la réputation globale avant d’ajouter des transactions.")];
            return false;
        }

        var advanced = draft.AdvancedConfigured
            ? (draft.SourceCatalog.Advanced ?? new(
                StableFactionRuntimeIdUx4B(project.Id.Value, "advanced"), 1, "studio:ux4-b", true, [], [], [], []))
                with
                {
                    ServerAuthoritative = true,
                    WitnessPolicies = draft.WitnessPolicies.ToArray(),
                    Memberships = draft.Memberships.ToArray(),
                    DynamicFactions = CloneDynamicsUx4B(draft.DynamicFactions),
                }
            : null;
        FactionReputationProfile? reputation = null;
        if (draft.ReputationConfigured)
        {
            var reputationId = draft.SourceCatalog.Reputation?.Id ??
                               StableFactionRuntimeIdUx4B(project.Id.Value, "reputation");
            var reaction = draft.SourceCatalog.Reputation?.Reaction ?? new(
                StableFactionRuntimeIdUx4B(reputationId, "reaction"), 1, "studio:ux4-b",
                ReputationScope.World, "world", true, true, true, true, true);
            reputation = (draft.SourceCatalog.Reputation ?? new(reputationId, 1, "studio:ux4-b", [], [], reaction))
                with { Transactions = draft.ReputationTransactions.ToArray() };
        }
        candidate = draft.SourceCatalog with
        {
            PersonalReputation = draft.PersonalConfigured ? draft.PersonalReputation.ToArray() : null,
            Advanced = advanced,
            RequireAdvancedRuntime = draft.RequireAdvancedRuntime,
            Reputation = reputation,
            RequireReputationRuntime = draft.RequireReputationRuntime,
        };
        var validation = FactionValidator.Validate(candidate);
        diagnostics = validation.Select(DiagnosticUx4B).ToArray();
        return diagnostics.Count == 0;
    }

    private static StudioFactionRuntimeDraftUx4B CloneFactionRuntimeDraftUx4B(
        StudioFactionRuntimeDraftUx4B draft) => draft with
        {
            PersonalReputation = draft.PersonalReputation.ToArray(),
            WitnessPolicies = draft.WitnessPolicies.ToArray(),
            Memberships = draft.Memberships.ToArray(),
            DynamicFactions = CloneDynamicsUx4B(draft.DynamicFactions),
            ReputationTransactions = draft.ReputationTransactions.ToArray(),
        };

    private static DynamicFactionDefinition[] CloneDynamicsUx4B(
        IReadOnlyList<DynamicFactionDefinition> dynamics) => dynamics
        .Select(item => item with { Diplomacy = item.Diplomacy.ToArray() })
        .ToArray();

    private static string FingerprintFactionRuntimeDraftUx4B(StudioFactionRuntimeDraftUx4B draft) =>
        HashUx4A(JsonSerializer.Serialize(new
        {
            Revision = draft.BaseRevision.Value,
            draft.SourceProjectFingerprint,
            draft.PersonalConfigured,
            Personal = draft.PersonalReputation,
            draft.AdvancedConfigured,
            draft.RequireAdvancedRuntime,
            Witnesses = draft.WitnessPolicies,
            draft.Memberships,
            Dynamics = draft.DynamicFactions,
            draft.ReputationConfigured,
            draft.RequireReputationRuntime,
            Transactions = draft.ReputationTransactions,
        }));

    private static Guid StableFactionRuntimeIdUx4B(Guid basis, string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{basis:D}|ux4-b|{value}"));
        return new Guid(bytes[..16]);
    }

    private static StudioFactionRuntimePreviewUx4B FailedFactionRuntimePreviewUx4B(
        string message,
        string code) => new(false, false, message, string.Empty, string.Empty,
        [new(code, "module/factions", HumanizeFactionRuntimeDiagnosticUx4B(code, message))],
        "Réputation personnelle non prévisualisée.", "Réputation globale non prévisualisée.",
        "Témoins non prévisualisés.", "Affiliations non prévisualisées.",
        "Factions dynamiques non prévisualisées.");

    private static IReadOnlyList<StudioFactionRuntimeDiagnosticUx4B> DiagnosticsUx4B(
        IReadOnlyList<CommandDiagnostic> diagnostics) => diagnostics.Select(item =>
    {
        var separator = item.Message.IndexOf(": ", StringComparison.Ordinal);
        var path = separator > 0 ? item.Message[..separator] : "module/factions";
        var message = separator > 0 ? item.Message[(separator + 2)..] : item.Message;
        return new StudioFactionRuntimeDiagnosticUx4B(
            item.Code, path, HumanizeFactionRuntimeDiagnosticUx4B(item.Code, message));
    }).ToArray();

    private static StudioFactionRuntimeDiagnosticUx4B DiagnosticUx4B(FactionDiagnostic item) =>
        new(item.Code, item.Path, HumanizeFactionRuntimeDiagnosticUx4B(item.Code, item.Message));

    private static StudioFactionRuntimeDiagnosticUx4B DiagnosticUx4B(DomainDiagnostic item) =>
        new(item.Code, item.Path, HumanizeFactionRuntimeDiagnosticUx4B(item.Code, item.Message));

    private static string HumanizeFactionRuntimeDiagnosticUx4B(string code, string fallback) => code switch
    {
        SetFactionCatalogHandler.RevisionConflictCode =>
            "Le projet a changé depuis l’aperçu. Rechargez la fiche.",
        "RPG_FACTION_PERSONAL_REPUTATION_INVALID" =>
            "Une réputation personnelle possède un sujet, une faction, une valeur ou une cause invalide.",
        "RPG_FACTION_REPUTATION_TRANSACTION_INVALID" =>
            "Une transaction globale possède un scope, une faction, une valeur ou une cause invalide.",
        "faction.witness.policy_invalid" =>
            "Une politique de témoin dépasse les bornes de perception, preuve, portée, mémoire ou réputation.",
        "faction.membership.invalid" =>
            "Une affiliation possède un acteur, un rang, une faction, une loyauté ou un poids invalide.",
        "faction.membership.primary_duplicate" =>
            "Un acteur ne peut avoir qu’une seule affiliation principale.",
        "faction.dynamic.invalid" =>
            "Une faction dynamique possède une identité, une durée ou une révision invalide.",
        "faction.dynamic.diplomacy_invalid" =>
            "Un état diplomatique possède une cible, une durée ou une cause invalide.",
        _ => string.IsNullOrWhiteSpace(fallback) ? "Configuration factionnelle invalide." : fallback,
    };

    private static string PersonalSummaryUx4B(FactionCatalog catalog) =>
        $"{catalog.EffectivePersonalReputation.Count} surcharge(s) acteur/joueur/groupe · durées et causes persistées";

    private static string ReputationSummaryUx4B(FactionCatalog catalog)
    {
        var transactions = catalog.Reputation?.Transactions ?? [];
        if (transactions.Count == 0)
        {
            return "Aucune transaction globale · profil optionnel conservé.";
        }
        var first = transactions[0];
        var state = FactionReputationService.InitialState(catalog);
        var score = FactionReputationService.EffectiveScore(catalog, state,
            new("studio-preview", first.SourceFactionId, first.TargetFactionId, first.OccurredAt,
                0, 0, 0, null, null), first.Scope, first.ScopeId);
        return $"{transactions.Count} transaction(s) · score prévisualisé {NumberUx4B(score)} · " +
               $"scope {first.Scope}/{first.ScopeId}";
    }

    private static string WitnessSummaryUx4B(FactionCatalog catalog)
    {
        var policies = catalog.Advanced?.WitnessPolicies ?? [];
        if (policies.Count == 0)
        {
            return "Aucune politique de témoin configurée.";
        }
        var first = policies[0];
        var at = new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);
        var result = FactionRuntimeResolver.PropagateWitnesses(catalog,
            [new(Guid.Parse("31000000-0000-4000-8000-000000000001"), "studio-preview", "hero", "témoin",
                first.FactionId, "incident", first.MinimumPerception, first.MinimumEvidence,
                Math.Min(first.CommunicationRadiusMeters, 1), at, true)], at);
        return $"{policies.Count} politique(s) · {result.Reports.Count} rapport(s) déterministe(s) prévisualisé(s)";
    }

    private static string MembershipSummaryUx4B(FactionCatalog catalog)
    {
        var memberships = catalog.Advanced?.Memberships ?? [];
        if (memberships.Count == 0)
        {
            return "Aucune affiliation multiple configurée.";
        }
        var actor = memberships[0].ActorId;
        var resolved = FactionRuntimeResolver.Memberships(catalog, actor,
            new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero));
        return $"{memberships.Count} affiliation(s) · {resolved.Count} active(s) pour {actor}";
    }

    private static string DynamicSummaryUx4B(FactionCatalog catalog)
    {
        var profile = catalog.Advanced;
        if (profile?.DynamicFactions.Count is not > 0)
        {
            return "Aucune faction dynamique configurée.";
        }
        var first = profile.DynamicFactions[0];
        var advanced = FactionRuntimeResolver.AdvanceExpirations(profile, first.CreatedAt.AddSeconds(1), true);
        return $"{profile.DynamicFactions.Count} faction(s) dynamique(s) · " +
               $"{profile.DynamicFactions.Sum(item => item.Diplomacy.Count)} état(s) diplomatique(s) · " +
               $"révision {advanced.Profile.DynamicFactions[0].Revision}";
    }

    private static string NumberUx4B(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    internal StudioUx4BSmokeResult RunUx4BContractSmoke()
    {
        var result = new StudioUx4BSmokeResult(false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false);
        var evidence = CreateUx4BEvidenceProject();
        if (evidence is null)
        {
            return result;
        }
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4b-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(root, evidence.Project).Succeeded || !editing.Open(root).Succeeded)
            {
                return result;
            }
            var draft = editing.CreateFactionRuntimeDraftUx4B();
            if (draft is null)
            {
                return result;
            }
            var choices = editing.FactionChoicesUx4A;
            var explicitSelection = choices.Count >= 3 && choices.All(item =>
                !item.Label.Contains(item.FactionId.ToString("D"), StringComparison.OrdinalIgnoreCase));
            var sourceProject = editing.Project!;
            var sourceCatalog = sourceProject.Module.Factions;
            var personal = draft.PersonalReputation.Select((item, index) =>
                index == 0 ? item with { Delta = item.Delta - 1, Cause = "preuve Studio UX4-B" } : item).ToArray();
            var transactions = draft.ReputationTransactions.Select((item, index) =>
                index == 0 ? item with { Delta = item.Delta + 1, Cause = "preuve Studio UX4-B" } : item).ToArray();
            var witnesses = draft.WitnessPolicies.Select((item, index) =>
                index == 0 ? item with { MinimumEvidence = Math.Min(1, item.MinimumEvidence + 0.01) } : item).ToArray();
            var memberships = draft.Memberships.Select((item, index) =>
                index == 1 ? item with { Loyalty = item.Loyalty + 1 } : item).ToArray();
            var dynamics = CloneDynamicsUx4B(draft.DynamicFactions);
            if (dynamics.Length > 0 && dynamics[0].Diplomacy.Count > 0)
            {
                dynamics[0] = dynamics[0] with
                {
                    Diplomacy = dynamics[0].Diplomacy.Select((item, index) =>
                        index == 0 ? item with { Status = FactionDiplomacyStatus.Truce, Cause = "preuve Studio UX4-B" } : item)
                        .ToArray(),
                };
            }
            var changed = draft with
            {
                PersonalConfigured = true,
                PersonalReputation = personal,
                AdvancedConfigured = true,
                RequireAdvancedRuntime = true,
                WitnessPolicies = witnesses,
                Memberships = memberships,
                DynamicFactions = dynamics,
                ReputationConfigured = true,
                RequireReputationRuntime = true,
                ReputationTransactions = transactions,
            };
            var beforeJson = ProjectJsonSerializer.Serialize(sourceProject);
            var preview = editing.PreviewFactionRuntimeUx4B(changed);
            var dryRun = preview.Succeeded && ReferenceEquals(sourceProject, editing.Project) &&
                         beforeJson == ProjectJsonSerializer.Serialize(editing.Project!);
            var invalid = editing.PreviewFactionRuntimeUx4B(changed with
            {
                PersonalReputation = personal.Select((item, index) => index == 0 ? item with { Delta = 101 } : item).ToArray(),
            });
            var invalidBlocked = !invalid.Succeeded && invalid.Diagnostics.Any(item =>
                item.Code == "RPG_FACTION_PERSONAL_REPUTATION_INVALID");

            var staleBase = editing.Project!.Module.Factions with
            {
                RequireFoundationRuntime = !editing.Project.Module.Factions.RequireFoundationRuntime,
            };
            var intermediateApplied = ApplyFactionCatalogForSmokeUx4A(editing, staleBase);
            var staleDraft = editing.PreviewFactionRuntimeUx4B(changed);
            var staleDraftBlocked = intermediateApplied && !staleDraft.Succeeded;

            var fresh = editing.CreateFactionRuntimeDraftUx4B()!;
            var freshChanged = fresh with
            {
                PersonalConfigured = true,
                PersonalReputation = personal,
                AdvancedConfigured = true,
                RequireAdvancedRuntime = true,
                WitnessPolicies = witnesses,
                Memberships = memberships,
                DynamicFactions = dynamics,
                ReputationConfigured = true,
                RequireReputationRuntime = true,
                ReputationTransactions = transactions,
            };
            var approval = editing.PreviewFactionRuntimeUx4B(freshChanged);
            var secondBase = editing.Project!.Module.Factions with
            {
                RequireFoundationRuntime = !editing.Project.Module.Factions.RequireFoundationRuntime,
            };
            var secondIntermediate = ApplyFactionCatalogForSmokeUx4A(editing, secondBase);
            var staleApproval = editing.ApplyApprovedFactionRuntimeUx4B(freshChanged);
            var staleApprovalBlocked = approval.Succeeded && secondIntermediate && !staleApproval.Succeeded;

            fresh = editing.CreateFactionRuntimeDraftUx4B()!;
            freshChanged = fresh with
            {
                PersonalConfigured = true,
                PersonalReputation = personal,
                AdvancedConfigured = true,
                RequireAdvancedRuntime = true,
                WitnessPolicies = witnesses,
                Memberships = memberships,
                DynamicFactions = dynamics,
                ReputationConfigured = true,
                RequireReputationRuntime = true,
                ReputationTransactions = transactions,
            };
            var finalPreview = editing.PreviewFactionRuntimeUx4B(freshChanged);
            var beforeApply = editing.Project!;
            var beforeCatalog = beforeApply.Module.Factions;
            var apply = editing.ApplyApprovedFactionRuntimeUx4B(freshChanged);
            var appliedProject = editing.Project;
            var appliedCatalog = appliedProject?.Module.Factions;
            var personalApplied = apply.Succeeded && appliedCatalog?.EffectivePersonalReputation.First().Cause ==
                                  "preuve Studio UX4-B";
            var globalApplied = appliedCatalog?.Reputation?.Transactions.First().Cause == "preuve Studio UX4-B";
            var witnessApplied = appliedCatalog?.Advanced?.WitnessPolicies.First().MinimumEvidence ==
                                 witnesses.First().MinimumEvidence;
            var membershipApplied = appliedCatalog?.Advanced?.Memberships.ElementAt(1).Loyalty ==
                                    memberships.ElementAt(1).Loyalty;
            var dynamicApplied = appliedCatalog?.Advanced?.DynamicFactions.First().Diplomacy.First().Status ==
                                 FactionDiplomacyStatus.Truce;
            var neighbors = appliedCatalog is not null &&
                            ReferenceEquals(beforeCatalog.Factions, appliedCatalog.Factions) &&
                            ReferenceEquals(beforeCatalog.Relationships, appliedCatalog.Relationships) &&
                            ReferenceEquals(beforeCatalog.Bands, appliedCatalog.Bands) &&
                            ReferenceEquals(beforeCatalog.Members, appliedCatalog.Members) &&
                            ReferenceEquals(beforeCatalog.Advanced?.Inheritance, appliedCatalog.Advanced?.Inheritance) &&
                            ReferenceEquals(beforeCatalog.Reputation?.HeatmapHistory,
                                appliedCatalog.Reputation?.HeatmapHistory) &&
                            ReferenceEquals(beforeCatalog.Reputation?.Reaction, appliedCatalog.Reputation?.Reaction);
            var undo = editing.UndoFactionRuntimeUx4B();
            var undone = undo.Succeeded && ReferenceEquals(beforeApply, editing.Project);
            var redo = editing.RedoFactionRuntimeUx4B();
            var redone = redo.Succeeded && ReferenceEquals(appliedProject, editing.Project);
            var saved = redone && editing.Save().Succeeded;
            var persisted = saved && reopened.Open(root).Succeeded &&
                            ProjectJsonSerializer.Serialize(reopened.Project!) ==
                            ProjectJsonSerializer.Serialize(editing.Project!);
            result = new(
                finalPreview.Succeeded && personalApplied && globalApplied && witnessApplied && membershipApplied &&
                dynamicApplied && invalidBlocked && dryRun && staleDraftBlocked && staleApprovalBlocked &&
                apply.Succeeded && undone && redone && persisted && neighbors,
                explicitSelection,
                personalApplied,
                globalApplied,
                witnessApplied,
                membershipApplied,
                dynamicApplied,
                invalidBlocked,
                dryRun,
                staleDraftBlocked,
                staleApprovalBlocked,
                finalPreview.Succeeded && apply.Succeeded,
                undone && redone,
                persisted,
                neighbors,
                false);
        }
        catch
        {
            return result;
        }
        finally
        {
            reopened.Close();
            editing.Close();
            var released = DeleteUx4BSmokeRoot(root);
            result = result with { Succeeded = result.Succeeded && released, TemporaryWorkspaceReleased = released };
        }
        return result;
    }

    private static bool DeleteUx4BSmokeRoot(string root)
    {
        try
        {
            var full = Path.GetFullPath(root);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) +
                       Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4b-smoke-", StringComparison.Ordinal))
            {
                return false;
            }
            if (Directory.Exists(full))
            {
                Directory.Delete(full, recursive: true);
            }
            return !Directory.Exists(full) && !File.Exists(full);
        }
        catch
        {
            return false;
        }
    }
}
