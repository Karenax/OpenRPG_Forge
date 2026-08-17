using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Factions;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Factions;
using Forge.Domain.Projects;
using Forge.Domain.Validation;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Factions;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux4AProvenance = "studio:ux4-a";
    private readonly SetFactionCatalogHandler _setFactionCatalogUx4A = new(new SystemClock());
    private StudioFactionFoundationDraftUx4A? _approvedFactionDraftUx4A;
    private FactionCatalog? _approvedFactionCandidateUx4A;
    private ProjectRevision? _approvedFactionRevisionUx4A;
    private string? _approvedFactionProjectFingerprintUx4A;
    private string? _approvedFactionDraftFingerprintUx4A;
    private string? _approvedFactionCandidateFingerprintUx4A;

    internal IReadOnlyList<StudioFactionChoiceUx4A> FactionChoicesUx4A
    {
        get
        {
            var factions = Project?.Module.Factions.Factions ?? [];
            var duplicateNames = factions
                .GroupBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.CurrentCultureIgnoreCase);
            return factions
                .Select(item =>
                {
                    var label = duplicateNames.Contains(item.DisplayName)
                        ? $"{item.DisplayName} · {item.Slug}"
                        : item.DisplayName;
                    if (item.Archived)
                    {
                        label += " · archivée";
                    }
                    if (item.ParentIds.Count > 0)
                    {
                        label += $" · {item.ParentIds.Count} parent(s)";
                    }
                    return new StudioFactionChoiceUx4A(
                        item.Id,
                        item.Slug,
                        label,
                        item.Archived,
                        item.ParentIds.Count);
                })
                .OrderBy(item => item.Archived)
                .ThenBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal StudioFactionFoundationDraftUx4A? CreateFactionDraftUx4A(Guid factionId)
    {
        var history = History;
        var project = Project;
        var faction = project?.Module.Factions.Factions.SingleOrDefault(item => item.Id == factionId);
        if (history is null || project is null || faction is null)
        {
            return null;
        }

        var catalog = project.Module.Factions;
        var inheritance = catalog.Advanced?.Inheritance.SingleOrDefault(item => item.FactionId == faction.Id);
        return CreateFactionDraftUx4A(
            project,
            history.State.Revision,
            catalog,
            faction.Id,
            isNew: false,
            faction.Slug,
            faction.DisplayName,
            faction.Archived,
            faction.ParentIds,
            inheritance);
    }

    internal StudioFactionFoundationDraftUx4A? CreateNewFactionDraftUx4A()
    {
        var history = History;
        var project = Project;
        if (history is null || project is null)
        {
            return null;
        }

        var catalog = project.Module.Factions;
        return CreateFactionDraftUx4A(
            project,
            history.State.Revision,
            catalog,
            Guid.NewGuid(),
            isNew: true,
            UniqueFactionSlugUx4A(catalog, "nouvelle-faction"),
            "Nouvelle faction",
            archived: false,
            [],
            inheritance: null);
    }

    private static StudioFactionFoundationDraftUx4A CreateFactionDraftUx4A(
        ProjectDefinition project,
        ProjectRevision revision,
        FactionCatalog catalog,
        Guid workingFactionId,
        bool isNew,
        string slug,
        string displayName,
        bool archived,
        IReadOnlyList<Guid> parentIds,
        FactionInheritancePolicy? inheritance) =>
        new(
            revision,
            FingerprintProjectUx4A(project),
            catalog,
            workingFactionId,
            isNew,
            StudioFactionOperationUx4A.Edit,
            slug,
            displayName,
            archived,
            parentIds.ToArray(),
            inheritance?.InheritTags ?? false,
            inheritance?.InheritPolicies ?? false,
            inheritance?.InheritRelationships ?? false,
            catalog.Advanced?.Id ?? Guid.NewGuid(),
            inheritance?.Id ?? Guid.NewGuid(),
            null,
            catalog.Relationships.Select(item => new StudioFactionRelationshipDraftUx4A(
                item.SourceId,
                item.TargetId,
                NumberUx4A(item.Score),
                item.Preset,
                item.Explanation)).ToArray(),
            catalog.Bands.Select(item => new StudioDispositionBandDraftUx4A(
                item.Id,
                NumberUx4A(item.Minimum),
                NumberUx4A(item.Maximum),
                item.Priority.ToString(CultureInfo.InvariantCulture))).ToArray());

    internal StudioFactionPreviewUx4A PreviewFactionFoundationUx4A(StudioFactionFoundationDraftUx4A draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelFactionFoundationUx4A();
        var history = History;
        var project = Project;
        if (history is null || project is null)
        {
            return FailedFactionPreviewUx4A("Aucun module n’est ouvert.", "studio.faction.project_missing");
        }
        if (history.State.Revision != draft.BaseRevision ||
            !string.Equals(
                FingerprintProjectUx4A(project),
                draft.SourceProjectFingerprint,
                StringComparison.Ordinal))
        {
            return FailedFactionPreviewUx4A(
                "Le catalogue a changé depuis l’ouverture de la fiche. Rechargez puis prévisualisez à nouveau.",
                SetFactionCatalogHandler.RevisionConflictCode);
        }

        if (!TryBuildFactionCandidateUx4A(project, draft, out var candidate, out var diagnostics))
        {
            return new(
                false,
                false,
                "Configuration refusée avant toute écriture.",
                string.Empty,
                string.Empty,
                AnalyzeFactionImpactUx4A(draft.SourceCatalog, draft.WorkingFactionId),
                diagnostics,
                DirectionalSummaryUx4A(draft.SourceCatalog),
                BandSummaryUx4A(draft.SourceCatalog));
        }

        var command = new SetFactionCatalogCommand(
            CommandId.From(Guid.NewGuid()),
            draft.BaseRevision,
            candidate,
            DryRun: true);
        var preview = _setFactionCatalogUx4A.Handle(history.State, command);
        if (!preview.Succeeded)
        {
            return new(
                false,
                false,
                "Le validateur projet refuse cette modification. Corrigez les dépendances indiquées.",
                string.Empty,
                string.Empty,
                AnalyzeFactionImpactUx4A(draft.SourceCatalog, draft.WorkingFactionId),
                DiagnosticsUx4A(preview.Diagnostics),
                DirectionalSummaryUx4A(draft.SourceCatalog),
                BandSummaryUx4A(draft.SourceCatalog));
        }

        var candidateProject = WithFactionCatalogUx4A(project, candidate);
        var projectDiagnostics = ProjectValidator.Validate(candidateProject);
        if (projectDiagnostics.Count > 0)
        {
            return new(
                false,
                false,
                "Le validateur projet refuse cette modification. Corrigez les dépendances indiquées.",
                string.Empty,
                string.Empty,
                AnalyzeFactionImpactUx4A(draft.SourceCatalog, draft.WorkingFactionId),
                DiagnosticsUx4A(projectDiagnostics),
                DirectionalSummaryUx4A(candidate),
                BandSummaryUx4A(candidate));
        }

        var frozenDraft = CloneFactionDraftUx4A(draft);
        _approvedFactionDraftUx4A = frozenDraft;
        _approvedFactionCandidateUx4A = candidate;
        _approvedFactionRevisionUx4A = history.State.Revision;
        _approvedFactionProjectFingerprintUx4A = FingerprintProjectUx4A(project);
        _approvedFactionDraftFingerprintUx4A = FingerprintDraftUx4A(frozenDraft);
        _approvedFactionCandidateFingerprintUx4A = FingerprintCatalogUx4A(project, candidate);
        return new(
            true,
            true,
            "Aperçu canonique prêt · aucune donnée du module n’a été modifiée.",
            preview.Patch?.Summary ?? "Catalogue de factions prévisualisé.",
            preview.Inverse?.Summary ?? "État précédent restaurable.",
            AnalyzeFactionImpactUx4A(draft.SourceCatalog, draft.WorkingFactionId),
            [],
            DirectionalSummaryUx4A(candidate),
            BandSummaryUx4A(candidate));
    }

    internal StudioFactionActionResultUx4A ApplyApprovedFactionFoundationUx4A(
        StudioFactionFoundationDraftUx4A currentDraft)
    {
        ArgumentNullException.ThrowIfNull(currentDraft);
        var history = History;
        var project = Project;
        var approvedDraft = _approvedFactionDraftUx4A;
        var candidate = _approvedFactionCandidateUx4A;
        if (history is null || project is null || approvedDraft is null || candidate is null ||
            _approvedFactionRevisionUx4A is null || _approvedFactionProjectFingerprintUx4A is null ||
            _approvedFactionDraftFingerprintUx4A is null || _approvedFactionCandidateFingerprintUx4A is null)
        {
            return new(false, "Prévisualisez la fiche avant de l’appliquer.");
        }

        var stale = history.State.Revision != _approvedFactionRevisionUx4A ||
                    !string.Equals(
                        FingerprintProjectUx4A(project),
                        _approvedFactionProjectFingerprintUx4A,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        FingerprintDraftUx4A(currentDraft),
                        _approvedFactionDraftFingerprintUx4A,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        FingerprintDraftUx4A(approvedDraft),
                        _approvedFactionDraftFingerprintUx4A,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        FingerprintCatalogUx4A(project, candidate),
                        _approvedFactionCandidateFingerprintUx4A,
                        StringComparison.Ordinal);
        if (stale)
        {
            CancelFactionFoundationUx4A();
            return new(false, "Le projet ou la fiche a changé. Prévisualisez à nouveau avant d’appliquer.");
        }

        var command = new SetFactionCatalogCommand(
            CommandId.From(Guid.NewGuid()),
            history.State.Revision,
            candidate,
            DryRun: false);
        var result = _setFactionCatalogUx4A.Handle(history.State, command);
        CancelFactionFoundationUx4A();
        if (!result.Succeeded)
        {
            var diagnostic = DiagnosticsUx4A(result.Diagnostics).FirstOrDefault();
            return new(false, diagnostic?.HumanMessage ?? "La commande canonique a refusé la modification.");
        }
        history.Record(result);
        InvalidateContentIndexes();
        return new(true, "Catalogue appliqué · Ctrl+Z pour annuler, Ctrl+Y pour rétablir.");
    }

    internal StudioFactionActionResultUx4A UndoFactionFoundationUx4A()
    {
        CancelFactionFoundationUx4A();
        var result = Undo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioFactionActionResultUx4A RedoFactionFoundationUx4A()
    {
        CancelFactionFoundationUx4A();
        var result = Redo();
        return new(result.Succeeded, result.Message);
    }

    internal void CancelFactionFoundationUx4A()
    {
        _approvedFactionDraftUx4A = null;
        _approvedFactionCandidateUx4A = null;
        _approvedFactionRevisionUx4A = null;
        _approvedFactionProjectFingerprintUx4A = null;
        _approvedFactionDraftFingerprintUx4A = null;
        _approvedFactionCandidateFingerprintUx4A = null;
    }

    internal void CloseUx4A() => CancelFactionFoundationUx4A();

    internal StudioFactionApprovalSnapshotUx4A CaptureFactionApprovalUx4A() => new(
        _approvedFactionDraftUx4A,
        _approvedFactionCandidateUx4A,
        _approvedFactionRevisionUx4A,
        _approvedFactionProjectFingerprintUx4A,
        _approvedFactionDraftFingerprintUx4A,
        _approvedFactionCandidateFingerprintUx4A);

    internal void RestoreFactionApprovalUx4A(StudioFactionApprovalSnapshotUx4A snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedFactionDraftUx4A = snapshot.Draft;
        _approvedFactionCandidateUx4A = snapshot.Candidate;
        _approvedFactionRevisionUx4A = snapshot.Revision;
        _approvedFactionProjectFingerprintUx4A = snapshot.ProjectFingerprint;
        _approvedFactionDraftFingerprintUx4A = snapshot.DraftFingerprint;
        _approvedFactionCandidateFingerprintUx4A = snapshot.CandidateFingerprint;
    }

    internal StudioFactionEvidenceProjectUx4A? CreateUx4AEvidenceProject()
    {
        var project = Project;
        if (project is null)
        {
            return null;
        }
        if (IsCryptFactionEvidenceUx4A(project))
        {
            return new(project, UsesRealCryptSource: true, UsesCanonicalFallback: false);
        }

        var fallback = CreateCanonicalEvidenceProjectUx4A(project.Id.Value);
        return new(fallback, UsesRealCryptSource: false, UsesCanonicalFallback: true);
    }

    private static ProjectDefinition CreateCanonicalEvidenceProjectUx4A(Guid sourceProjectId)
    {
        var projectId = ProjectId.From(StableEvidenceIdUx4A(sourceProjectId, "project"));
        var area = new AreaDefinition(
            AreaId.From(StableEvidenceIdUx4A(sourceProjectId, "area")),
            "atelier-factions",
            "Atelier des factions",
            [],
            AreaKind.Interior,
            new AreaDimensions(32, 32));
        var project = new ProjectDefinition(
            projectId,
            "Studio UX4-A · preuve factions",
            [],
            [area]);
        return WithFactionCatalogUx4A(project, CreateCanonicalEvidenceCatalogUx4A(projectId.Value));
    }

    private static bool TryBuildFactionCandidateUx4A(
        ProjectDefinition project,
        StudioFactionFoundationDraftUx4A draft,
        out FactionCatalog candidate,
        out IReadOnlyList<StudioFactionDiagnosticUx4A> diagnostics)
    {
        candidate = draft.SourceCatalog;
        diagnostics = [];
        if (!ReferenceEquals(project.Module.Factions, draft.SourceCatalog) &&
            !string.Equals(
                FingerprintCatalogUx4A(project, project.Module.Factions),
                FingerprintCatalogUx4A(project, draft.SourceCatalog),
                StringComparison.Ordinal))
        {
            diagnostics = [DiagnosticUx4A(SetFactionCatalogHandler.RevisionConflictCode)];
            return false;
        }

        var state = new FactionProjectState(draft.BaseRevision.Value, draft.SourceCatalog);
        FactionMutationResult dryRun;
        FactionMutationResult planned;
        if (draft.Operation == StudioFactionOperationUx4A.Edit)
        {
            var existing = draft.SourceCatalog.Factions.SingleOrDefault(item => item.Id == draft.WorkingFactionId);
            if (!draft.IsNew && existing is null)
            {
                diagnostics = [DiagnosticUx4A("RPG_FACTION_NOT_FOUND")];
                return false;
            }
            if (draft.IsNew && existing is not null)
            {
                diagnostics = [DiagnosticUx4A("RPG_FACTION_UUID_DUPLICATE")];
                return false;
            }
            var definition = new FactionDefinition(
                draft.WorkingFactionId,
                existing?.SchemaVersion ?? 1,
                existing?.Provenance ?? Ux4AProvenance,
                draft.Slug,
                draft.DisplayName,
                draft.Archived,
                draft.ParentIds.ToArray(),
                existing?.Tags.ToArray() ?? [],
                existing?.Policies is null
                    ? null
                    : new Dictionary<string, string>(existing.Policies, StringComparer.Ordinal));
            dryRun = FactionAuthoringService.Upsert(state, draft.BaseRevision.Value, definition, dryRun: true);
            planned = FactionAuthoringService.Upsert(state, draft.BaseRevision.Value, definition, dryRun: false);
            if (!dryRun.Succeeded || !planned.Succeeded)
            {
                diagnostics = [DiagnosticUx4A(dryRun.DiagnosticCode ?? planned.DiagnosticCode ??
                                               "RPG_FACTION_INVALID")];
                return false;
            }
            if (!TryParseRelationshipsUx4A(draft.Relationships, out var relationships, out diagnostics) ||
                !TryParseBandsUx4A(draft.Bands, out var bands, out diagnostics))
            {
                return false;
            }
            candidate = planned.State.Catalog with
            {
                Relationships = relationships,
                Bands = bands,
            };
            candidate = ApplyInheritanceUx4A(candidate, draft);
        }
        else
        {
            if (draft.IsNew || draft.SourceCatalog.Factions.All(item => item.Id != draft.WorkingFactionId))
            {
                diagnostics = [DiagnosticUx4A("RPG_FACTION_NOT_FOUND")];
                return false;
            }
            var remove = draft.Operation == StudioFactionOperationUx4A.Remove;
            if (remove && draft.MigrationTargetId == draft.WorkingFactionId)
            {
                diagnostics = [DiagnosticUx4A("RPG_FACTION_MIGRATION_TARGET_INVALID")];
                return false;
            }
            dryRun = FactionAuthoringService.ArchiveOrRemove(
                state,
                draft.BaseRevision.Value,
                draft.WorkingFactionId,
                remove ? draft.MigrationTargetId : null,
                remove,
                dryRun: true);
            planned = FactionAuthoringService.ArchiveOrRemove(
                state,
                draft.BaseRevision.Value,
                draft.WorkingFactionId,
                remove ? draft.MigrationTargetId : null,
                remove,
                dryRun: false);
            if (!dryRun.Succeeded || !planned.Succeeded)
            {
                diagnostics = [DiagnosticUx4A(dryRun.DiagnosticCode ?? planned.DiagnosticCode ??
                                               "RPG_FACTION_INVALID")];
                return false;
            }
            candidate = planned.State.Catalog;
        }

        if (draft.SourceCatalog.PersonalReputation is null)
        {
            candidate = candidate with { PersonalReputation = null };
        }
        var domainDiagnostics = FactionValidator.Validate(candidate);
        if (domainDiagnostics.Count > 0)
        {
            diagnostics = domainDiagnostics.Select(DiagnosticUx4A).ToArray();
            return false;
        }
        return true;
    }

    private static FactionCatalog ApplyInheritanceUx4A(
        FactionCatalog catalog,
        StudioFactionFoundationDraftUx4A draft)
    {
        var enabled = draft.InheritTags || draft.InheritPolicies || draft.InheritRelationships;
        var advanced = catalog.Advanced;
        if (advanced is null && !enabled)
        {
            return catalog;
        }
        if (advanced is null)
        {
            advanced = new FactionAdvancedProfile(
                draft.AdvancedProfileId,
                1,
                Ux4AProvenance,
                true,
                [],
                [],
                [],
                []);
        }

        var existing = advanced.Inheritance.SingleOrDefault(item => item.FactionId == draft.WorkingFactionId);
        IReadOnlyList<FactionInheritancePolicy> inheritance;
        if (!enabled)
        {
            inheritance = advanced.Inheritance.Where(item => item.FactionId != draft.WorkingFactionId).ToArray();
        }
        else
        {
            var policy = new FactionInheritancePolicy(
                existing?.Id ?? draft.InheritancePolicyId,
                draft.WorkingFactionId,
                draft.InheritTags,
                draft.InheritPolicies,
                draft.InheritRelationships);
            inheritance = existing is null
                ? [.. advanced.Inheritance, policy]
                : advanced.Inheritance.Select(item =>
                    item.FactionId == draft.WorkingFactionId ? policy : item).ToArray();
        }
        return catalog with { Advanced = advanced with { Inheritance = inheritance } };
    }

    private static bool TryParseRelationshipsUx4A(
        IReadOnlyList<StudioFactionRelationshipDraftUx4A> drafts,
        out IReadOnlyList<FactionRelationship> relationships,
        out IReadOnlyList<StudioFactionDiagnosticUx4A> diagnostics)
    {
        var parsed = new List<FactionRelationship>();
        var failures = new List<StudioFactionDiagnosticUx4A>();
        for (var index = 0; index < drafts.Count; index++)
        {
            var draft = drafts[index];
            if (!double.TryParse(draft.Score, NumberStyles.Float, CultureInfo.InvariantCulture, out var score))
            {
                failures.Add(new(
                    "RPG_FACTION_RELATION_INVALID",
                    $"module/factions/relationships/{index}/score",
                    "Le score directionnel doit être un nombre compris entre −100 et 100."));
                continue;
            }
            parsed.Add(new(
                draft.SourceId,
                draft.TargetId,
                score,
                draft.Explanation,
                draft.Preset));
        }
        relationships = parsed;
        diagnostics = failures;
        return failures.Count == 0;
    }

    private static bool TryParseBandsUx4A(
        IReadOnlyList<StudioDispositionBandDraftUx4A> drafts,
        out IReadOnlyList<DispositionBand> bands,
        out IReadOnlyList<StudioFactionDiagnosticUx4A> diagnostics)
    {
        var parsed = new List<DispositionBand>();
        var failures = new List<StudioFactionDiagnosticUx4A>();
        for (var index = 0; index < drafts.Count; index++)
        {
            var draft = drafts[index];
            if (!double.TryParse(draft.Minimum, NumberStyles.Float, CultureInfo.InvariantCulture, out var minimum) ||
                !double.TryParse(draft.Maximum, NumberStyles.Float, CultureInfo.InvariantCulture, out var maximum) ||
                !int.TryParse(draft.Priority, NumberStyles.Integer, CultureInfo.InvariantCulture, out var priority))
            {
                failures.Add(new(
                    "RPG_FACTION_BAND_INVALID",
                    $"module/factions/bands/{index}",
                    "Chaque bande exige un minimum, un maximum et une priorité numériques."));
                continue;
            }
            parsed.Add(new(draft.Id, minimum, maximum, priority));
        }
        bands = parsed;
        diagnostics = failures;
        return failures.Count == 0;
    }

    private static StudioFactionImpactUx4A AnalyzeFactionImpactUx4A(FactionCatalog catalog, Guid factionId) =>
        new(
            catalog.Factions.Count(item => item.Id != factionId && item.ParentIds.Contains(factionId)),
            catalog.Relationships.Count(item => item.SourceId == factionId || item.TargetId == factionId),
            catalog.Members.Count(item => item.FactionId == factionId),
            catalog.EffectivePersonalReputation.Count(item =>
                item.SourceFactionId == factionId || item.TargetFactionId == factionId),
            catalog.Advanced?.Inheritance.Count(item => item.FactionId == factionId) ?? 0,
            catalog.Advanced?.WitnessPolicies.Count(item => item.FactionId == factionId) ?? 0,
            catalog.Advanced?.Memberships.Count(item => item.FactionId == factionId) ?? 0,
            catalog.Advanced?.DynamicFactions.Count(item => item.FactionId == factionId) ?? 0,
            catalog.Advanced?.DynamicFactions.Sum(item =>
                item.Diplomacy.Count(state => state.TargetFactionId == factionId)) ?? 0,
            catalog.Reputation?.Transactions.Count(item =>
                item.SourceFactionId == factionId || item.TargetFactionId == factionId) ?? 0,
            catalog.Reputation?.HeatmapHistory.Sum(snapshot => snapshot.Relationships.Count(item =>
                item.SourceId == factionId || item.TargetId == factionId)) ?? 0);

    private static IReadOnlyList<StudioFactionDiagnosticUx4A> DiagnosticsUx4A(
        IReadOnlyList<CommandDiagnostic> diagnostics) => diagnostics.Select(item =>
    {
        var path = string.Empty;
        var message = item.Message;
        var separator = item.Message.IndexOf(": ", StringComparison.Ordinal);
        if (separator > 0 && item.Message[..separator].Contains('/', StringComparison.Ordinal))
        {
            path = item.Message[..separator];
            message = item.Message[(separator + 2)..];
        }
        return new StudioFactionDiagnosticUx4A(item.Code, path, HumanizeFactionDiagnosticUx4A(item.Code, message));
    }).ToArray();

    private static IReadOnlyList<StudioFactionDiagnosticUx4A> DiagnosticsUx4A(
        IReadOnlyList<DomainDiagnostic> diagnostics) => diagnostics.Select(item =>
        new StudioFactionDiagnosticUx4A(
            item.Code,
            item.Path,
            HumanizeFactionDiagnosticUx4A(item.Code, item.Message))).ToArray();

    private static StudioFactionDiagnosticUx4A DiagnosticUx4A(FactionDiagnostic diagnostic) =>
        new(diagnostic.Code, diagnostic.Path, HumanizeFactionDiagnosticUx4A(diagnostic.Code, diagnostic.Message));

    private static StudioFactionDiagnosticUx4A DiagnosticUx4A(string code) =>
        new(code, "module/factions", HumanizeFactionDiagnosticUx4A(code, string.Empty));

    private static string HumanizeFactionDiagnosticUx4A(string code, string fallback) => code switch
    {
        SetFactionCatalogHandler.RevisionConflictCode or "RPG_FACTION_REVISION_CONFLICT" =>
            "Le projet a changé depuis l’ouverture de la fiche.",
        "RPG_FACTION_NOT_FOUND" => "La faction choisie n’existe plus.",
        "RPG_FACTION_MIGRATION_TARGET_MISSING" => "La destination de migration n’existe plus.",
        "RPG_FACTION_MIGRATION_TARGET_INVALID" => "Une faction ne peut pas être sa propre destination de migration.",
        "RPG_FACTION_MEMBERS_REQUIRE_MIGRATION" =>
            "Des membres utilisent encore cette faction. Choisissez une destination de migration.",
        "RPG_FACTION_INVALID" or "RPG_FACTION_IDENTITY_INVALID" =>
            "Le nom interne, le nom affiché ou la provenance est invalide.",
        "RPG_FACTION_UUID_DUPLICATE" => "Deux définitions partagent la même identité interne.",
        "RPG_FACTION_SLUG_DUPLICATE" => "Ce nom interne est déjà utilisé par une autre faction.",
        "RPG_FACTION_PARENT_ORPHAN" => "Un parent sélectionné n’existe plus.",
        "RPG_FACTION_INHERITANCE_CYCLE" => "Cette hiérarchie créerait un cycle de parents.",
        "RPG_FACTION_RELATION_INVALID" =>
            "Chaque direction exige une source et une cible existantes, un score de −100 à 100, un preset et une explication.",
        "RPG_FACTION_RELATION_DUPLICATE" => "Cette direction existe déjà dans la matrice.",
        "RPG_FACTION_BAND_INVALID" =>
            "La bande doit être nommée, bornée entre −100 et 100 et posséder une priorité positive ou nulle.",
        "RPG_FACTION_BAND_DUPLICATE" => "Deux bandes portent le même nom.",
        "RPG_FACTION_BANDS_OVERLAP" =>
            "Deux bandes de même priorité se chevauchent. Ajustez leurs bornes ou leur priorité.",
        "world.placeable.faction_reference_missing" =>
            "Un placeable dépend encore de cette faction. Modifiez cette dépendance avant la suppression.",
        "world.trigger.faction_reference_missing" =>
            "Un filtre de déclencheur dépend encore de cette faction. Modifiez cette dépendance avant la suppression.",
        ProjectValidator.MissingCreatureInteractionFactionCode =>
            "Une créature dépend encore de cette faction. Modifiez cette dépendance avant la suppression.",
        _ when !string.IsNullOrWhiteSpace(fallback) && !ContainsGuidUx4A(fallback) => fallback,
        _ => "Le validateur canonique refuse cette configuration de factions.",
    };

    private static StudioFactionPreviewUx4A FailedFactionPreviewUx4A(string message, string code) =>
        new(false, false, message, string.Empty, string.Empty,
            new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            [DiagnosticUx4A(code)], string.Empty, string.Empty);

    private static string DirectionalSummaryUx4A(FactionCatalog catalog)
    {
        var asymmetric = catalog.Relationships.Count(item =>
        {
            var reverse = catalog.Relationships.SingleOrDefault(candidate =>
                candidate.SourceId == item.TargetId && candidate.TargetId == item.SourceId);
            return reverse is null || Math.Abs(reverse.Score - item.Score) > double.Epsilon ||
                   !string.Equals(reverse.Preset, item.Preset, StringComparison.Ordinal) ||
                   !string.Equals(reverse.Explanation, item.Explanation, StringComparison.Ordinal);
        });
        var examples = catalog.Relationships.Take(3).Select(item =>
            $"{FactionNameUx4A(catalog, item.SourceId)} → {FactionNameUx4A(catalog, item.TargetId)} : " +
            $"{NumberUx4A(item.Score)} · {item.Preset} · {FactionValidator.ResolveBand(catalog, item.SourceId, item.TargetId)}");
        return $"{catalog.Relationships.Count} direction(s), dont {asymmetric} indépendante(s). " +
               (catalog.Relationships.Count == 0
                   ? "Aucune relation authorée."
                   : string.Join(" | ", examples));
    }

    private static string BandSummaryUx4A(FactionCatalog catalog) => catalog.Bands.Count == 0
        ? "Aucune bande de disposition authorée."
        : string.Join(" · ", catalog.Bands
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.Minimum)
            .Select(item => $"{item.Id} {NumberUx4A(item.Minimum)}…{NumberUx4A(item.Maximum)} (priorité {item.Priority})"));

    private static string FactionNameUx4A(FactionCatalog catalog, Guid factionId) =>
        catalog.Factions.SingleOrDefault(item => item.Id == factionId)?.DisplayName ?? "Faction indisponible";

    private static StudioFactionFoundationDraftUx4A CloneFactionDraftUx4A(
        StudioFactionFoundationDraftUx4A draft) => draft with
    {
        ParentIds = draft.ParentIds.ToArray(),
        Relationships = draft.Relationships.Select(item => item with { }).ToArray(),
        Bands = draft.Bands.Select(item => item with { }).ToArray(),
    };

    private static string FingerprintDraftUx4A(StudioFactionFoundationDraftUx4A draft)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            WriteFingerprintStringUx4A(writer, 1, "openrpg-forge:studio:ux4-a:draft");
            WriteFingerprintInt64Ux4A(writer, 2, 1);
            WriteFingerprintInt64Ux4A(writer, 3, draft.BaseRevision.Value);
            WriteFingerprintStringUx4A(writer, 4, draft.SourceProjectFingerprint);
            WriteFingerprintGuidUx4A(writer, 5, draft.WorkingFactionId);
            WriteFingerprintBooleanUx4A(writer, 6, draft.IsNew);
            WriteFingerprintInt64Ux4A(writer, 7, (int)draft.Operation);
            WriteFingerprintStringUx4A(writer, 8, draft.Slug);
            WriteFingerprintStringUx4A(writer, 9, draft.DisplayName);
            WriteFingerprintBooleanUx4A(writer, 10, draft.Archived);
            WriteFingerprintBooleanUx4A(writer, 11, draft.InheritTags);
            WriteFingerprintBooleanUx4A(writer, 12, draft.InheritPolicies);
            WriteFingerprintBooleanUx4A(writer, 13, draft.InheritRelationships);
            WriteFingerprintGuidUx4A(writer, 14, draft.AdvancedProfileId);
            WriteFingerprintGuidUx4A(writer, 15, draft.InheritancePolicyId);
            WriteFingerprintNullableGuidUx4A(writer, 16, draft.MigrationTargetId);

            WriteFingerprintCollectionUx4A(writer, 17, draft.ParentIds.Count);
            foreach (var parent in draft.ParentIds)
            {
                WriteFingerprintGuidUx4A(writer, 1, parent);
            }

            WriteFingerprintCollectionUx4A(writer, 18, draft.Relationships.Count);
            foreach (var relationship in draft.Relationships)
            {
                WriteFingerprintRecordUx4A(writer, 1, 5);
                WriteFingerprintGuidUx4A(writer, 1, relationship.SourceId);
                WriteFingerprintGuidUx4A(writer, 2, relationship.TargetId);
                WriteFingerprintStringUx4A(writer, 3, relationship.Score);
                WriteFingerprintStringUx4A(writer, 4, relationship.Preset);
                WriteFingerprintStringUx4A(writer, 5, relationship.Explanation);
            }

            WriteFingerprintCollectionUx4A(writer, 19, draft.Bands.Count);
            foreach (var band in draft.Bands)
            {
                WriteFingerprintRecordUx4A(writer, 1, 4);
                WriteFingerprintStringUx4A(writer, 1, band.Id);
                WriteFingerprintStringUx4A(writer, 2, band.Minimum);
                WriteFingerprintStringUx4A(writer, 3, band.Maximum);
                WriteFingerprintStringUx4A(writer, 4, band.Priority);
            }
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteFingerprintHeaderUx4A(BinaryWriter writer, ushort field, byte type)
    {
        writer.Write(field);
        writer.Write(type);
    }

    private static void WriteFingerprintStringUx4A(BinaryWriter writer, ushort field, string? value)
    {
        WriteFingerprintHeaderUx4A(writer, field, 1);
        if (value is null)
        {
            writer.Write(-1);
            return;
        }
        var bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static void WriteFingerprintInt64Ux4A(BinaryWriter writer, ushort field, long value)
    {
        WriteFingerprintHeaderUx4A(writer, field, 2);
        writer.Write(value);
    }

    private static void WriteFingerprintBooleanUx4A(BinaryWriter writer, ushort field, bool value)
    {
        WriteFingerprintHeaderUx4A(writer, field, 3);
        writer.Write(value);
    }

    private static void WriteFingerprintGuidUx4A(BinaryWriter writer, ushort field, Guid value)
    {
        WriteFingerprintHeaderUx4A(writer, field, 4);
        writer.Write(16);
        writer.Write(value.ToByteArray());
    }

    private static void WriteFingerprintNullableGuidUx4A(BinaryWriter writer, ushort field, Guid? value)
    {
        WriteFingerprintHeaderUx4A(writer, field, 5);
        writer.Write(value.HasValue);
        if (value.HasValue)
        {
            writer.Write(16);
            writer.Write(value.Value.ToByteArray());
        }
    }

    private static void WriteFingerprintCollectionUx4A(BinaryWriter writer, ushort field, int count)
    {
        WriteFingerprintHeaderUx4A(writer, field, 6);
        writer.Write(count);
    }

    private static void WriteFingerprintRecordUx4A(BinaryWriter writer, ushort field, int fieldCount)
    {
        WriteFingerprintHeaderUx4A(writer, field, 7);
        writer.Write(fieldCount);
    }

    private static string FingerprintProjectUx4A(ProjectDefinition project) =>
        HashUx4A(ProjectJsonSerializer.Serialize(project));

    private static string FingerprintCatalogUx4A(ProjectDefinition project, FactionCatalog catalog)
        => FingerprintProjectUx4A(WithFactionCatalogUx4A(project, catalog));

    private static ProjectDefinition WithFactionCatalogUx4A(ProjectDefinition project, FactionCatalog catalog) =>
        new(
            project.Id,
            project.Title,
            project.Module.WithFactions(catalog),
            project.Blueprints,
            project.Areas,
            project.Dialogues,
            project.Quests);

    private static string HashUx4A(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool ContainsGuidUx4A(string value) => value
        .Split([' ', '/', ':', '\'', '"', '(', ')', '[', ']', ',', '.', ';', '='],
            StringSplitOptions.RemoveEmptyEntries)
        .Any(part => Guid.TryParse(part, out _));

    private static string UniqueFactionSlugUx4A(FactionCatalog catalog, string basis)
    {
        var used = catalog.Factions.Select(item => item.Slug).ToHashSet(StringComparer.Ordinal);
        if (!used.Contains(basis))
        {
            return basis;
        }
        for (var suffix = 2; suffix < int.MaxValue; suffix++)
        {
            var candidate = $"{basis}-{suffix}";
            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }
        throw new InvalidOperationException("Unable to allocate a faction slug.");
    }

    private static bool IsCryptFactionEvidenceUx4A(ProjectDefinition project)
    {
        if (!string.Equals(project.Module.Slug, "crypt-of-ashes", StringComparison.Ordinal))
        {
            return false;
        }
        var catalog = project.Module.Factions;
        var slugs = catalog.Factions.Select(item => item.Slug).ToHashSet(StringComparer.Ordinal);
        return slugs.IsSupersetOf(["crypt-covenant", "ash-watch", "surface-alliance"]) &&
               catalog.Relationships.Count >= 2 && catalog.Bands.Count >= 3 &&
               catalog.Advanced?.Inheritance is { Count: >= 1 } &&
               catalog.PersonalReputation is { Count: >= 1 } && catalog.Reputation is not null;
    }

    private static FactionCatalog CreateCanonicalEvidenceCatalogUx4A(Guid projectId)
    {
        Guid Id(string segment) => StableEvidenceIdUx4A(projectId, segment);
        var covenant = new FactionDefinition(
            Id("faction:crypt-covenant"), 1, "studio:ux4-a:evidence-fallback", "crypt-covenant",
            "Pacte de la crypte", false, [], ["crypt", "guardian"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["intruder-response"] = "challenge",
                ["relic-duty"] = "protect",
            });
        var watch = new FactionDefinition(
            Id("faction:ash-watch"), 1, "studio:ux4-a:evidence-fallback", "ash-watch",
            "Veilleurs des cendres", false, [covenant.Id], ["watch", "undead"]);
        var surface = new FactionDefinition(
            Id("faction:surface-alliance"), 1, "studio:ux4-a:evidence-fallback", "surface-alliance",
            "Alliance de la surface", false, [], ["adventurer", "civilian"]);
        var advanced = new FactionAdvancedProfile(
            Id("advanced"),
            1,
            "studio:ux4-a:evidence-fallback",
            true,
            [new FactionInheritancePolicy(Id("inheritance:ash-watch"), watch.Id, true, true, true)],
            [],
            [],
            []);
        return new FactionCatalog(
            [covenant, watch, surface],
            [
                new FactionRelationship(covenant.Id, surface.Id, -75, "intrusion dans la crypte", "hostile"),
                new FactionRelationship(surface.Id, covenant.Id, -30, "crainte de la crypte", "wary"),
            ],
            [
                new DispositionBand("hostile", -100, -1, 1),
                new DispositionBand("neutral", 0, 50, 1),
                new DispositionBand("allied", 51, 100, 1),
            ],
            [],
            advanced,
            RequireAdvancedRuntime: true,
            PersonalReputation: null,
            RequireFoundationRuntime: true);
    }

    private static Guid StableEvidenceIdUx4A(Guid projectId, string segment)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"ux4-a:evidence:{projectId:N}:{segment}"));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guid);
        guid[7] = (byte)((guid[7] & 0x0f) | 0x40);
        guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
        return new Guid(guid);
    }

    private static string NumberUx4A(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    internal StudioUx4ASmokeResult RunUx4AContractSmoke()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "openrpg-forge-studio-ux4a-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        StudioUx4ASmokeResult ExecuteSmoke()
        {
            try
            {
                var parent = new FactionDefinition(
                    Guid.Parse("f4a00000-0000-4000-8000-000000000001"),
                    1,
                    "test:ux4-a",
                    "pacte-cendres",
                    "Pacte des Cendres",
                    false,
                    [],
                    ["guardian"],
                    new Dictionary<string, string> { ["civilian-response"] = "protect" });
            var child = new FactionDefinition(
                Guid.Parse("f4a00000-0000-4000-8000-000000000002"),
                1,
                "test:ux4-a",
                "veilleurs-braises",
                "Veilleurs des Braises",
                false,
                [parent.Id],
                ["watch"]);
            var outsider = new FactionDefinition(
                Guid.Parse("f4a00000-0000-4000-8000-000000000003"),
                1,
                "test:ux4-a",
                "alliance-surface",
                "Alliance de la Surface",
                false,
                [],
                ["civilian"]);
            var createdAt = DateTimeOffset.Parse("2026-08-17T12:00:00Z", CultureInfo.InvariantCulture);
            var advanced = new FactionAdvancedProfile(
                Guid.Parse("f4a00000-0000-4000-8000-000000000010"),
                1,
                "test:ux4-a-neighbor",
                true,
                [new(Guid.Parse("f4a00000-0000-4000-8000-000000000011"), child.Id, true, false, false)],
                [new(Guid.Parse("f4a00000-0000-4000-8000-000000000012"), child.Id, .6, .5, 20, 120, -10, true)],
                [new(Guid.Parse("f4a00000-0000-4000-8000-000000000013"), 1, "test:ux4-a-neighbor",
                    "sentinel", outsider.Id, "observer", null, 20, 1, true, null)],
                [new(Guid.Parse("f4a00000-0000-4000-8000-000000000014"), 1, "test:ux4-a-neighbor",
                    child.Id, DynamicFactionKind.Temporary, createdAt, createdAt.AddDays(2), true, true, 1,
                    [new(Guid.Parse("f4a00000-0000-4000-8000-000000000015"), outsider.Id,
                        FactionDiplomacyStatus.Truce, createdAt, null, "test-truce")])]);
            var catalog = new FactionCatalog(
                [parent, child, outsider],
                [
                    new(parent.Id, outsider.Id, -70, "ancienne rivalité", "hostile"),
                    new(outsider.Id, parent.Id, 20, "commerce prudent", "wary"),
                ],
                [
                    new("hostile", -100, -1, 1),
                    new("neutral", 0, 50, 1),
                    new("allied", 51, 100, 1),
                ],
                [new("legacy-observer", outsider.Id)],
                advanced,
                true,
                PersonalReputation: null,
                RequireFoundationRuntime: true);

            var blueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("f4a00000-0000-4000-8000-000000000020")),
                "autel-factionnel",
                "Autel factionnel",
                BlueprintKind.Placeable);
            var triggerBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("f4a00000-0000-4000-8000-000000000025")),
                "filtre-factionnel",
                "Filtre factionnel",
                BlueprintKind.Trigger);
            var area = new AreaDefinition(
                AreaId.From(Guid.Parse("f4a00000-0000-4000-8000-000000000021")),
                "atelier-factions",
                "Atelier des factions",
                [],
                AreaKind.Interior,
                new AreaDimensions(32, 32));
            var project = new ProjectDefinition(
                ProjectId.From(Guid.Parse("f4a00000-0000-4000-8000-000000000022")),
                "Studio UX4-A",
                [blueprint, triggerBlueprint],
                [area]);
            var ownership = new PlaceableFactionOwnershipProfile(
                Guid.Parse("f4a00000-0000-4000-8000-000000000023"),
                PlaceableFactionOwnershipProfile.CurrentSchemaVersion,
                "test:ux4-a-external",
                outsider.Id,
                [outsider.Id],
                true,
                false,
                false,
                true);
            var placeable = CreatePlaceableTransformParent(
                blueprint,
                Guid.Parse("f4a00000-0000-4000-8000-000000000024"),
                "test:ux4-a-placeable") with
            {
                FactionOwnership = ownership,
                FactionOwnershipRequiredAtRuntime = false,
            };
            var trigger = CreateUx1ACSmokeTrigger(
                triggerBlueprint.Id,
                area.Id,
                "f4a00000-0000-4000-8001",
                advanced: true,
                foundation: true);
            var triggerAdvanced = trigger.Advanced!;
            trigger = trigger with
            {
                Advanced = triggerAdvanced with
                {
                    TargetFilter = triggerAdvanced.TargetFilter with { FactionIds = [outsider.Id] },
                },
            };
            var worldFeatures = new WorldFeatureCatalog(
                project.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux4-a-world",
                placeables: [placeable],
                triggers: [trigger]);
            project = new ProjectDefinition(
                project.Id,
                project.Title,
                project.Module.WithFactions(catalog).WithWorldFeatures(worldFeatures),
                project.Blueprints,
                project.Areas,
                project.Dialogues,
                project.Quests);
            if (!ProjectSession.Create(tempRoot, project).Succeeded || !editing.Open(tempRoot).Succeeded)
            {
                return FailedUx4ASmoke();
            }

            var choices = editing.FactionChoicesUx4A;
            var explicitSelection = choices.Count == 3 &&
                                    editing.CreateFactionDraftUx4A(Guid.Empty) is null && choices.All(choice =>
                !choice.Label.Contains(choice.FactionId.ToString("D"), StringComparison.OrdinalIgnoreCase) &&
                !choice.Label.Contains("UUID", StringComparison.OrdinalIgnoreCase));
            var sourceDraft = editing.CreateFactionDraftUx4A(child.Id);
            if (sourceDraft is null)
            {
                return FailedUx4ASmoke();
            }
            var typedDraftFingerprint = !string.Equals(
                FingerprintDraftUx4A(sourceDraft with { Slug = "alpha|beta", DisplayName = "gamma" }),
                FingerprintDraftUx4A(sourceDraft with { Slug = "alpha", DisplayName = "beta|gamma" }),
                StringComparison.Ordinal);
            var editedRelationships = sourceDraft.Relationships.Select(item =>
                item.SourceId == parent.Id && item.TargetId == outsider.Id
                    ? item with { Score = "-65", Preset = "hostile", Explanation = "rivalité visible" }
                    : item).ToArray();
            var editedBands = sourceDraft.Bands.Select(item => item.Id switch
            {
                "neutral" => item with { Maximum = "49" },
                "allied" => item with { Minimum = "50" },
                _ => item,
            }).ToArray();
            Guid[] editedParents = [parent.Id, outsider.Id];
            var editedDraft = sourceDraft with
            {
                DisplayName = "Veilleurs des Braises unis",
                ParentIds = editedParents,
                InheritTags = true,
                InheritPolicies = true,
                InheritRelationships = true,
                Relationships = editedRelationships,
                Bands = editedBands,
            };

            var originalProject = editing.Project;
            var originalHistory = editing.History;
            var originalRevision = originalHistory?.State.Revision;
            var originalJournal = originalHistory?.Journal.ToArray() ?? [];
            var originalDirty = editing.IsDirty;
            var originalCanUndo = originalHistory?.CanUndo;
            var originalCanRedo = originalHistory?.CanRedo;
            var preview = editing.PreviewFactionFoundationUx4A(editedDraft);
            var dryRun = preview.Succeeded && preview.CanApply &&
                         ReferenceEquals(originalProject, editing.Project) &&
                         ReferenceEquals(originalHistory, editing.History) &&
                         originalHistory?.State.Revision == originalRevision &&
                         (originalHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                         originalHistory?.CanUndo == originalCanUndo &&
                         originalHistory?.CanRedo == originalCanRedo &&
                         editing.IsDirty == originalDirty &&
                         editing.Project?.Module.Factions.PersonalReputation is null;

            var invalidCycle = editing.PreviewFactionFoundationUx4A(
                sourceDraft with { ParentIds = [child.Id] });
            var invalidDirection = editing.PreviewFactionFoundationUx4A(sourceDraft with
            {
                Relationships =
                [
                    .. sourceDraft.Relationships,
                    new StudioFactionRelationshipDraftUx4A(child.Id, outsider.Id, "120", "hostile", "hors borne"),
                ],
            });
            var invalidBands = editing.PreviewFactionFoundationUx4A(sourceDraft with
            {
                Bands = sourceDraft.Bands.Select(item => item.Id == "hostile"
                    ? item with { Maximum = "10" }
                    : item).ToArray(),
            });
            var invalidConfiguration = !invalidCycle.Succeeded && !invalidDirection.Succeeded &&
                                       !invalidBands.Succeeded;

            var intermediate = editing.Project!.Module.Factions with
            {
                RequireFoundationRuntime = !editing.Project!.Module.Factions.RequireFoundationRuntime,
            };
            var intermediateApplied = ApplyFactionCatalogForSmokeUx4A(editing, intermediate);
            var staleDraftPreview = editing.PreviewFactionFoundationUx4A(editedDraft);
            var staleDraftBlocked = intermediateApplied && !staleDraftPreview.Succeeded &&
                                    staleDraftPreview.Diagnostics.Any(item =>
                                        item.Code is SetFactionCatalogHandler.RevisionConflictCode or
                                            "RPG_FACTION_REVISION_CONFLICT");

            var freshDraft = editing.CreateFactionDraftUx4A(child.Id)! with
            {
                DisplayName = "Veilleurs des Braises unis",
                ParentIds = editedParents,
                InheritTags = true,
                InheritPolicies = true,
                InheritRelationships = true,
                Relationships = editedRelationships,
                Bands = editedBands,
            };
            var approvalPreview = editing.PreviewFactionFoundationUx4A(freshDraft);
            var secondIntermediate = editing.Project!.Module.Factions with
            {
                RequireFoundationRuntime = !editing.Project!.Module.Factions.RequireFoundationRuntime,
            };
            var secondIntermediateApplied = ApplyFactionCatalogForSmokeUx4A(editing, secondIntermediate);
            var staleApproval = editing.ApplyApprovedFactionFoundationUx4A(freshDraft);
            var staleApprovalBlocked = approvalPreview.Succeeded && secondIntermediateApplied &&
                                       !staleApproval.Succeeded && staleApproval.Message.Contains(
                                           "Prévisualisez", StringComparison.OrdinalIgnoreCase);

            freshDraft = editing.CreateFactionDraftUx4A(child.Id)! with
            {
                DisplayName = "Veilleurs des Braises unis",
                ParentIds = editedParents,
                InheritTags = true,
                InheritPolicies = true,
                InheritRelationships = true,
                Relationships = editedRelationships,
                Bands = editedBands,
            };
            var finalPreview = editing.PreviewFactionFoundationUx4A(freshDraft);
            var beforeApply = editing.Project;
            var beforeWorldFeatures = beforeApply?.Module.WorldFeatures;
            var beforeWitnesses = beforeApply?.Module.Factions.Advanced?.WitnessPolicies;
            var beforeMemberships = beforeApply?.Module.Factions.Advanced?.Memberships;
            var beforeDynamics = beforeApply?.Module.Factions.Advanced?.DynamicFactions;
            var beforeCatalog = beforeApply?.Module.Factions;
            var beforeChild = beforeCatalog?.Factions.Single(item => item.Id == child.Id);
            var applied = editing.ApplyApprovedFactionFoundationUx4A(freshDraft);
            var appliedProject = editing.Project;
            var appliedCatalog = appliedProject?.Module.Factions;
            var appliedChild = appliedCatalog?.Factions.Single(item => item.Id == child.Id);
            var canonicalCommand = finalPreview.Succeeded && finalPreview.CanApply && applied.Succeeded &&
                                   editing.History?.Journal.LastOrDefault()?.CommandName ==
                                   "set_faction_catalog";
            var hierarchy = appliedProject is not null &&
                            appliedProject.Module.Factions.Factions.Single(item => item.Id == child.Id).ParentIds
                                .SequenceEqual(editedParents) &&
                            appliedProject.Module.Factions.Advanced?.Inheritance.Single(item =>
                                item.FactionId == child.Id) is
                            { InheritTags: true, InheritPolicies: true, InheritRelationships: true };
            var directional = appliedProject is not null &&
                              appliedProject.Module.Factions.Relationships.Single(item =>
                                  item.SourceId == parent.Id && item.TargetId == outsider.Id).Score == -65 &&
                              appliedProject.Module.Factions.Relationships.Single(item =>
                                  item.SourceId == outsider.Id && item.TargetId == parent.Id).Score == 20;
            var bands = appliedProject is not null &&
                        appliedProject.Module.Factions.Bands.Single(item => item.Id == "neutral").Maximum == 49 &&
                        appliedProject.Module.Factions.Bands.Single(item => item.Id == "allied").Minimum == 50;
            var nullPersonalPreserved = appliedProject?.Module.Factions.PersonalReputation is null;
            var expectedProject = beforeApply is null || appliedCatalog is null
                ? null
                : WithFactionCatalogUx4A(beforeApply, appliedCatalog);
            var neighborsPreserved = expectedProject is not null && appliedProject is not null &&
                                     ProjectJsonSerializer.Serialize(expectedProject) ==
                                     ProjectJsonSerializer.Serialize(appliedProject) &&
                                     beforeCatalog is not null && appliedCatalog is not null &&
                                     beforeChild is not null && appliedChild is not null &&
                                     beforeCatalog.Factions.Where(item => item.Id != child.Id)
                                         .SequenceEqual(appliedCatalog.Factions.Where(item => item.Id != child.Id)) &&
                                     beforeCatalog.Members.SequenceEqual(appliedCatalog.Members) &&
                                     beforeCatalog.RequireAdvancedRuntime == appliedCatalog.RequireAdvancedRuntime &&
                                     beforeCatalog.RequireFoundationRuntime == appliedCatalog.RequireFoundationRuntime &&
                                     beforeCatalog.RequireReputationRuntime == appliedCatalog.RequireReputationRuntime &&
                                     ReferenceEquals(beforeCatalog.Reputation, appliedCatalog.Reputation) &&
                                     beforeChild.Id == appliedChild.Id &&
                                     beforeChild.SchemaVersion == appliedChild.SchemaVersion &&
                                     beforeChild.Provenance == appliedChild.Provenance &&
                                     beforeChild.Slug == appliedChild.Slug &&
                                     beforeChild.Archived == appliedChild.Archived &&
                                     beforeChild.Tags.SequenceEqual(appliedChild.Tags) &&
                                     SamePoliciesUx4A(beforeChild.Policies, appliedChild.Policies) &&
                                     ReferenceEquals(beforeWorldFeatures, appliedProject.Module.WorldFeatures) &&
                                     ReferenceEquals(beforeWitnesses,
                                         appliedProject.Module.Factions.Advanced?.WitnessPolicies) &&
                                     ReferenceEquals(beforeMemberships,
                                         appliedProject.Module.Factions.Advanced?.Memberships) &&
                                     ReferenceEquals(beforeDynamics,
                                         appliedProject.Module.Factions.Advanced?.DynamicFactions);

            var undone = editing.UndoFactionFoundationUx4A();
            var undoRestored = undone.Succeeded && ReferenceEquals(beforeApply, editing.Project);
            var redone = editing.RedoFactionFoundationUx4A();
            var undoRedo = undoRestored && redone.Succeeded && ReferenceEquals(appliedProject, editing.Project);

            var archiveDraft = editing.CreateFactionDraftUx4A(child.Id)! with
            {
                Operation = StudioFactionOperationUx4A.Archive,
            };
            var archivePreview = editing.PreviewFactionFoundationUx4A(archiveDraft);
            var archiveApplied = editing.ApplyApprovedFactionFoundationUx4A(archiveDraft);
            var archive = archivePreview.Succeeded && archiveApplied.Succeeded &&
                          editing.Project?.Module.Factions.Factions.Single(item => item.Id == child.Id).Archived == true &&
                          editing.Project!.Module.Factions.PersonalReputation is null;
            _ = editing.UndoFactionFoundationUx4A();

            var newDraft = editing.CreateNewFactionDraftUx4A()! with
            {
                Slug = "observateurs-studio",
                DisplayName = "Observateurs du Studio",
            };
            var createPreview = editing.PreviewFactionFoundationUx4A(newDraft);
            var createApplied = editing.ApplyApprovedFactionFoundationUx4A(newDraft);
            var creation = createPreview.Succeeded && createApplied.Succeeded &&
                           editing.Project?.Module.Factions.Factions.Any(item =>
                               item.Id == newDraft.WorkingFactionId && item.Slug == "observateurs-studio") == true;
            var removalDraft = editing.CreateFactionDraftUx4A(newDraft.WorkingFactionId)! with
            {
                Operation = StudioFactionOperationUx4A.Remove,
                MigrationTargetId = null,
            };
            var removalPreview = editing.PreviewFactionFoundationUx4A(removalDraft);
            var removalApplied = editing.ApplyApprovedFactionFoundationUx4A(removalDraft);
            var safeRemoval = removalPreview.Succeeded && removalApplied.Succeeded &&
                              editing.Project?.Module.Factions.Factions.All(item =>
                                  item.Id != newDraft.WorkingFactionId) == true &&
                              editing.Project!.Module.Factions.PersonalReputation is null;

            var externalDraft = editing.CreateFactionDraftUx4A(outsider.Id)! with
            {
                Operation = StudioFactionOperationUx4A.Remove,
                MigrationTargetId = parent.Id,
            };
            var externalPreview = editing.PreviewFactionFoundationUx4A(externalDraft);
            var externalReferenceBlocked = !externalPreview.Succeeded &&
                                           externalPreview.Diagnostics.Any(item =>
                                               item.Code == "world.placeable.faction_reference_missing") &&
                                           externalPreview.Diagnostics.Any(item =>
                                               item.Code == "world.trigger.faction_reference_missing") &&
                                           externalPreview.Diagnostics.All(item =>
                                               !ContainsGuidUx4A(item.HumanMessage));

            var saved = editing.Save().Succeeded;
            var reopenedOk = saved && reopened.Open(tempRoot).Succeeded;
            var persistence = reopenedOk &&
                              ProjectJsonSerializer.Serialize(editing.Project!) ==
                              ProjectJsonSerializer.Serialize(reopened.Project!) &&
                              reopened.Project!.Module.Factions.PersonalReputation is null;
            var succeeded = explicitSelection && creation && hierarchy && directional && bands &&
                             invalidConfiguration && dryRun && staleDraftBlocked && staleApprovalBlocked &&
                             typedDraftFingerprint && canonicalCommand && nullPersonalPreserved && archive && safeRemoval &&
                             externalReferenceBlocked && undoRedo && persistence && neighborsPreserved;
            return new(
                succeeded,
                explicitSelection,
                creation,
                hierarchy,
                directional,
                bands,
                invalidConfiguration,
                dryRun,
                staleDraftBlocked,
                staleApprovalBlocked,
                typedDraftFingerprint,
                canonicalCommand,
                nullPersonalPreserved,
                archive,
                safeRemoval,
                externalReferenceBlocked,
                undoRedo,
                persistence,
                neighborsPreserved,
                TemporaryWorkspaceReleased: false);
            }
            catch (Exception exception)
            {
                Godot.GD.PrintErr(
                    $"OPENRPG_FORGE_STUDIO_UX4A_CONTRACT_FAILED type={exception.GetType().Name} message={exception.Message}");
                return FailedUx4ASmoke();
            }
        }

        var result = ExecuteSmoke();
        try
        {
            reopened.Close();
            editing.Close();
        }
        finally
        {
            var released = DeleteUx4ASmokeRoot(tempRoot);
            result = result with
            {
                Succeeded = result.Succeeded && released,
                TemporaryWorkspaceReleased = released,
            };
        }
        return result;
    }

    private static bool ApplyFactionCatalogForSmokeUx4A(
        StudioSessionController session,
        FactionCatalog catalog)
    {
        var history = session.History;
        if (history is null)
        {
            return false;
        }
        var command = new SetFactionCatalogCommand(
            CommandId.From(Guid.NewGuid()),
            history.State.Revision,
            catalog,
            DryRun: false);
        var result = session._setFactionCatalogUx4A.Handle(history.State, command);
        if (!result.Succeeded)
        {
            return false;
        }
        history.Record(result);
        session.InvalidateContentIndexes();
        return true;
    }

    private static bool SamePoliciesUx4A(
        IReadOnlyDictionary<string, string>? left,
        IReadOnlyDictionary<string, string>? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }
        return left.Count == right.Count && left.All(item =>
            right.TryGetValue(item.Key, out var value) && string.Equals(item.Value, value, StringComparison.Ordinal));
    }

    private static bool DeleteUx4ASmokeRoot(string tempRoot)
    {
        try
        {
            var fullRoot = Path.GetFullPath(tempRoot);
            var tempBase = Path.GetFullPath(Path.GetTempPath());
            var prefix = tempBase.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                         Path.DirectorySeparatorChar;
            if (!fullRoot.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(fullRoot).StartsWith(
                    "openrpg-forge-studio-ux4a-smoke-",
                    StringComparison.Ordinal))
            {
                return false;
            }
            if (Directory.Exists(fullRoot))
            {
                Directory.Delete(fullRoot, recursive: true);
            }
            return !Directory.Exists(fullRoot) && !File.Exists(fullRoot);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX4A_SMOKE_CLEANUP_FAILED type={exception.GetType().Name} message={exception.Message}");
            return false;
        }
    }

    private static StudioUx4ASmokeResult FailedUx4ASmoke() => new(
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false);
}
