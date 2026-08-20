using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SetCreatureEventsHandler _setCreatureDamageUx4O = new(new SystemClock());
    private StudioCreatureDamageDraftUx4O? _approvedCreatureDamageDraftUx4O;
    private StudioCreatureDamageCandidateUx4O? _approvedCreatureDamageCandidateUx4O;
    private ProjectRevision? _approvedCreatureDamageRevisionUx4O;
    private string? _approvedCreatureDamageProjectFingerprintUx4O;
    private string? _approvedCreatureDamageDraftFingerprintUx4O;
    private string? _approvedCreatureDamageCandidateFingerprintUx4O;

    internal IReadOnlyList<StudioCreatureDamageChoiceUx4O> CreatureDamageChoicesUx4O =>
        Project?.Blueprints
            .Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => new StudioCreatureDamageChoiceUx4O(item.Id,
                $"{item.DisplayName} · {(item.CreatureEvents?.DamageDeath is null ? "dégâts/mort absents" :
                    "cycle transactionnel configuré")}", item.CreatureEvents?.DamageDeath is not null,
                item.IsReadOnly())).ToArray() ?? [];

    internal StudioCreatureDamageDraftUx4O? CreateCreatureDamageDraftUx4O(BlueprintId blueprintId)
    {
        var project = Project;
        var history = History;
        var blueprint = project?.Blueprints.SingleOrDefault(item => item.Id == blueprintId);
        if (project is null || history is null || blueprint is null || blueprint.IsReadOnly() ||
            blueprint.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn)) return null;
        var source = blueprint.CreatureEvents;
        var working = source ?? CanonicalCreatureEventsUx4N(blueprint.Id);
        var damage = working.DamageDeath;
        return new(history.State.Revision, FingerprintProjectUx4N(project), blueprint.Id, blueprint, source,
            damage is not null, damage?.BeforeDamage.Handler ?? "BeforeDamage",
            damage?.DamageApplied.Handler ?? "OnDamaged", damage?.Died.Handler ?? "OnDeath",
            (damage?.BeforeDamageBudgetMilliseconds ?? 4).ToString(CultureInfo.InvariantCulture),
            (damage?.MaximumAdjustmentRatio ?? .5).ToString("R", CultureInfo.InvariantCulture));
    }

    internal StudioCreatureDamagePreviewUx4O PreviewCreatureDamageUx4O(StudioCreatureDamageDraftUx4O draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelCreatureDamageUx4O();
        var project = Project;
        var history = History;
        if (project is null || history is null)
            return FailedCreatureDamageUx4O("Aucun module n’est ouvert.", "studio.creature.project_missing");
        if (history.State.Revision != draft.BaseRevision ||
            !string.Equals(FingerprintProjectUx4N(project), draft.SourceProjectFingerprint,
                StringComparison.Ordinal))
            return FailedCreatureDamageUx4O("Le projet a changé ; rechargez la fiche.",
                SetCreatureEventsHandler.RevisionConflictCode);
        if (project.Blueprints.All(item => item.Id != draft.BlueprintId))
            return FailedCreatureDamageUx4O("La créature n’existe plus.",
                SetCreatureEventsHandler.BlueprintMissingCode);
        if (!TryBuildCreatureDamageCandidateUx4O(draft, out var candidate, out var diagnostics))
            return new(false, false, "Le cycle dégâts/mort ou la créature est invalide.", string.Empty,
                string.Empty, diagnostics, "Dégâts non évalués.", "Mort non évaluée.",
                "Validation refusée.", "Runtime non évalué.");
        var dryRun = _setCreatureDamageUx4O.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, draft.BlueprintId, candidate.Profile,
                DryRun: true));
        if (!dryRun.Succeeded)
            return new(false, false, "La commande canonique refuse le profil.", string.Empty, string.Empty,
                CreatureDamageDiagnosticsUx4O(dryRun.Diagnostics), DamageSummaryUx4O(candidate.Profile),
                DeathSummaryUx4O(candidate.Profile), ValidationSummaryUx4O(candidate.Profile, 0),
                "Runtime non évalué.");

        var profile = candidate.Profile;
        var runtime = "Cycle dégâts/mort optionnel désactivé ; aucun dispatch produit.";
        if (profile.DamageDeath is not null)
        {
            var transaction = StableCreatureEventIdUx4N(profile.Id, "preview-damage-transaction");
            var before = CreatureEventRuntimeResolver.BeforeDamage(profile, transaction, "preview-creature",
                "preview-attacker", 12, "radiant", "studio-preview");
            var applied = CreatureEventRuntimeResolver.DamageApplied(profile, transaction, "preview-creature",
                "preview-attacker", 12, 10, 2, "radiant", "studio-preview");
            var died = CreatureEventRuntimeResolver.EntityDied(profile, "preview-creature", "preview-attacker",
                ["preview-attacker", "preview-witness"], "studio-preview", "area:0,0,0",
                ["preview-loot"], "drop");
            runtime = $"Projection pure : {before?.EventName} ({before?.BudgetMilliseconds} ms), " +
                      $"{applied?.EventName} demandé={applied?.Payload["requested"]} appliqué=" +
                      $"{applied?.Payload["applied"]}, {died?.EventName} avec " +
                      $"{died?.Payload.Count ?? 0} champs post-transaction. " +
                      "Aucun dégât, acteur, loot, script ou réseau n’est exécuté dans Studio.";
        }
        var frozen = draft with { };
        _approvedCreatureDamageDraftUx4O = frozen;
        _approvedCreatureDamageCandidateUx4O = candidate;
        _approvedCreatureDamageRevisionUx4O = history.State.Revision;
        _approvedCreatureDamageProjectFingerprintUx4O = FingerprintProjectUx4N(project);
        _approvedCreatureDamageDraftFingerprintUx4O = FingerprintCreatureDamageDraftUx4O(frozen);
        _approvedCreatureDamageCandidateFingerprintUx4O =
            FingerprintCreatureDamageCandidateUx4O(project, draft.BlueprintId, candidate);
        return new(true, true, "Dégâts, mort et validation ciblée conformes · prêts à être appliqués.",
            dryRun.Patch?.Summary ?? "Définir le cycle dégâts/mort.",
            dryRun.Inverse?.Summary ?? "Restaurer le cycle précédent.", [],
            DamageSummaryUx4O(profile), DeathSummaryUx4O(profile), ValidationSummaryUx4O(profile, 0), runtime);
    }

    internal StudioCreatureDamageActionResultUx4O ApplyApprovedCreatureDamageUx4O(
        StudioCreatureDamageDraftUx4O currentDraft)
    {
        ArgumentNullException.ThrowIfNull(currentDraft);
        var project = Project;
        var history = History;
        var approved = _approvedCreatureDamageDraftUx4O;
        var candidate = _approvedCreatureDamageCandidateUx4O;
        if (project is null || history is null || approved is null || candidate is null ||
            _approvedCreatureDamageRevisionUx4O is null ||
            _approvedCreatureDamageProjectFingerprintUx4O is null ||
            _approvedCreatureDamageDraftFingerprintUx4O is null ||
            _approvedCreatureDamageCandidateFingerprintUx4O is null)
            return new(false, "Prévisualisez le cycle avant de l’appliquer.");
        var stale = history.State.Revision != _approvedCreatureDamageRevisionUx4O ||
                    !string.Equals(FingerprintProjectUx4N(project),
                        _approvedCreatureDamageProjectFingerprintUx4O, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureDamageDraftUx4O(currentDraft),
                        _approvedCreatureDamageDraftFingerprintUx4O, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureDamageDraftUx4O(approved),
                        _approvedCreatureDamageDraftFingerprintUx4O, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureDamageCandidateUx4O(project, currentDraft.BlueprintId,
                            candidate), _approvedCreatureDamageCandidateFingerprintUx4O, StringComparison.Ordinal);
        if (stale)
        {
            CancelCreatureDamageUx4O();
            return new(false, "Le brouillon, le projet ou l’approbation a changé ; prévisualisez à nouveau.");
        }
        var validation = _setCreatureDamageUx4O.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, currentDraft.BlueprintId,
                candidate.Profile, DryRun: true));
        if (!validation.Succeeded)
        {
            CancelCreatureDamageUx4O();
            return new(false, "La revalidation canonique refuse le profil.");
        }
        var result = _setCreatureDamageUx4O.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, currentDraft.BlueprintId,
                candidate.Profile, DryRun: false));
        CancelCreatureDamageUx4O();
        if (!result.Succeeded) return new(false, "La commande n’a pas appliqué le profil.");
        history.Record(result);
        InvalidateContentIndexes();
        return new(true, result.Patch?.Summary ?? "Cycle dégâts/mort appliqué.");
    }

    internal StudioCreatureDamageActionResultUx4O UndoCreatureDamageUx4O()
    { CancelCreatureDamageUx4O(); var result = Undo(); return new(result.Succeeded, result.Message); }

    internal StudioCreatureDamageActionResultUx4O RedoCreatureDamageUx4O()
    { CancelCreatureDamageUx4O(); var result = Redo(); return new(result.Succeeded, result.Message); }

    internal void CancelCreatureDamageUx4O()
    {
        _approvedCreatureDamageDraftUx4O = null;
        _approvedCreatureDamageCandidateUx4O = null;
        _approvedCreatureDamageRevisionUx4O = null;
        _approvedCreatureDamageProjectFingerprintUx4O = null;
        _approvedCreatureDamageDraftFingerprintUx4O = null;
        _approvedCreatureDamageCandidateFingerprintUx4O = null;
    }

    internal void CloseUx4O() => CancelCreatureDamageUx4O();

    internal StudioCreatureDamageApprovalSnapshotUx4O CaptureCreatureDamageApprovalUx4O() => new(
        _approvedCreatureDamageDraftUx4O, _approvedCreatureDamageCandidateUx4O,
        _approvedCreatureDamageRevisionUx4O, _approvedCreatureDamageProjectFingerprintUx4O,
        _approvedCreatureDamageDraftFingerprintUx4O, _approvedCreatureDamageCandidateFingerprintUx4O);

    internal void RestoreCreatureDamageApprovalUx4O(StudioCreatureDamageApprovalSnapshotUx4O snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedCreatureDamageDraftUx4O = snapshot.Draft;
        _approvedCreatureDamageCandidateUx4O = snapshot.Candidate;
        _approvedCreatureDamageRevisionUx4O = snapshot.Revision;
        _approvedCreatureDamageProjectFingerprintUx4O = snapshot.ProjectFingerprint;
        _approvedCreatureDamageDraftFingerprintUx4O = snapshot.DraftFingerprint;
        _approvedCreatureDamageCandidateFingerprintUx4O = snapshot.CandidateFingerprint;
    }

    internal StudioCreatureDamageEvidenceProjectUx4O? CreateUx4OEvidenceProject()
    {
        var project = Project;
        if (project is null) return null;
        var real = project.Blueprints.Where(item =>
                (item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn) && !item.IsReadOnly() &&
                item.CreatureEvents?.DamageDeath is not null)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault(item => CreatureEventValidator.Validate(item.CreatureEvents!).Count == 0);
        if (real is not null) return new(project, real.Id, true, false);
        var editable = project.Blueprints.Where(item =>
                (item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn) && !item.IsReadOnly())
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase).FirstOrDefault();
        if (editable is null) return null;
        var profile = CanonicalCreatureDamageUx4O(editable);
        return new(WithCreatureEventsUx4N(project, editable.Id, profile), editable.Id, false, true);
    }

    internal StudioUx4OSmokeResult RunUx4OContractSmoke()
    {
        var empty = new StudioUx4OSmokeResult(false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, false, false);
        var evidence = CreateUx4OEvidenceProject();
        if (evidence is null) return empty;
        var root = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux4o-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        StudioUx4OSmokeResult Execute()
        {
            try
            {
                if (!ProjectSession.Create(root, evidence.Project).Succeeded || !editing.Open(root).Succeeded)
                    return empty;
                var explicitSelection = editing.CreatureDamageChoicesUx4O.Count > 0 &&
                    editing.CreatureDamageChoicesUx4O.All(item =>
                        !item.Label.Contains(item.BlueprintId.ToString(), StringComparison.OrdinalIgnoreCase));
                var draft = editing.CreateCreatureDamageDraftUx4O(evidence.BlueprintId);
                if (draft is null) return empty with { ExplicitSelection = explicitSelection };
                var changed = ChangedCreatureDamageDraftUx4O(draft);
                var sourceProject = editing.Project!;
                var sourceHistory = editing.History!;
                var preview = editing.PreviewCreatureDamageUx4O(changed);
                var dryRun = preview.Succeeded && ReferenceEquals(sourceProject, editing.Project) &&
                             ReferenceEquals(sourceHistory, editing.History) &&
                             sourceHistory.State.Revision == draft.BaseRevision;
                var runtimeProjection = preview.Succeeded && preview.RuntimeSummary.Contains(
                    "creature.damage_applied", StringComparison.OrdinalIgnoreCase) &&
                    preview.RuntimeSummary.Contains("entity.died", StringComparison.OrdinalIgnoreCase);
                var invalidBudget = editing.PreviewCreatureDamageUx4O(changed with
                {
                    BeforeDamageBudgetMilliseconds = "0"
                });
                var budgetBlocked = !invalidBudget.Succeeded && invalidBudget.Diagnostics.Any(item =>
                    item.Code == "creature.events.before_damage_budget_invalid");
                var invalidRatio = editing.PreviewCreatureDamageUx4O(changed with
                {
                    MaximumAdjustmentRatio = "1.5"
                });
                var ratioBlocked = !invalidRatio.Succeeded && invalidRatio.Diagnostics.Any(item =>
                    item.Code == "creature.events.before_damage_adjustment_invalid");
                var optional = editing.PreviewCreatureDamageUx4O(changed with { DamageDeathEnabled = false });
                var optionalRemoval = optional.Succeeded && optional.DamageSummary.Contains(
                    "désactivé", StringComparison.OrdinalIgnoreCase);

                var approvedBeforeRevision = editing.PreviewCreatureDamageUx4O(changed);
                var neighborChanged = ToggleCreatureEventNeighborUx4N(editing, evidence.BlueprintId);
                var staleDraftBlocked = approvedBeforeRevision.Succeeded && neighborChanged &&
                    !editing.ApplyApprovedCreatureDamageUx4O(changed).Succeeded;
                if (neighborChanged) editing.UndoCreatureDamageUx4O();

                var fresh = editing.CreateCreatureDamageDraftUx4O(evidence.BlueprintId)!;
                var freshChanged = ChangedCreatureDamageDraftUx4O(fresh);
                var approvedBeforeTamper = editing.PreviewCreatureDamageUx4O(freshChanged);
                var staleApprovalBlocked = approvedBeforeTamper.Succeeded &&
                    !editing.ApplyApprovedCreatureDamageUx4O(freshChanged with
                    {
                        DeathHandler = freshChanged.DeathHandler + "Tampered"
                    }).Succeeded;

                fresh = editing.CreateCreatureDamageDraftUx4O(evidence.BlueprintId)!;
                freshChanged = ChangedCreatureDamageDraftUx4O(fresh);
                var beforeApply = editing.Project!;
                var beforeProfile = beforeApply.Blueprints.Single(item => item.Id == evidence.BlueprintId)
                    .CreatureEvents;
                var finalPreview = editing.PreviewCreatureDamageUx4O(freshChanged);
                var apply = finalPreview.Succeeded
                    ? editing.ApplyApprovedCreatureDamageUx4O(freshChanged)
                    : new StudioCreatureDamageActionResultUx4O(false, "preview failed");
                var appliedProject = editing.Project;
                var applied = appliedProject?.Blueprints.Single(item => item.Id == evidence.BlueprintId)
                    .CreatureEvents;
                var damage = applied?.DamageDeath is
                {
                    BeforeDamage.Handler: "BeforeDamageBounded",
                    DamageApplied.Handler: "OnDamageCommitted",
                    BeforeDamageBudgetMilliseconds: 4,
                    MaximumAdjustmentRatio: .25
                };
                var death = applied?.DamageDeath?.Died.Handler == "OnEntityDeathComplete";
                var validation = finalPreview.Succeeded && finalPreview.ValidationSummary.Contains(
                    "0 diagnostic", StringComparison.OrdinalIgnoreCase);
                var ux4NPreserved = beforeProfile is not null && applied is not null &&
                    EqualUx4NBindingsUx4O(beforeProfile, applied);
                var canonical = apply.Succeeded && damage && death && validation;
                var expected = applied is null ? null : WithCreatureEventsUx4N(beforeApply,
                    evidence.BlueprintId, applied);
                var neighbors = canonical && expected is not null && appliedProject is not null &&
                                ProjectJsonSerializer.Serialize(expected) ==
                                ProjectJsonSerializer.Serialize(appliedProject);
                var appliedJson = appliedProject is null ? string.Empty :
                    ProjectJsonSerializer.Serialize(appliedProject);
                var beforeJson = ProjectJsonSerializer.Serialize(beforeApply);
                var undo = canonical && editing.UndoCreatureDamageUx4O().Succeeded &&
                           ProjectJsonSerializer.Serialize(editing.Project!) == beforeJson;
                var redo = undo && editing.RedoCreatureDamageUx4O().Succeeded &&
                           ProjectJsonSerializer.Serialize(editing.Project!) == appliedJson;
                var saved = redo && editing.Save().Succeeded;
                var persisted = saved && reopened.Open(root).Succeeded &&
                                ProjectJsonSerializer.Serialize(reopened.Project!) == appliedJson;
                return new(canonical && explicitSelection && damage && death && validation && optionalRemoval &&
                        budgetBlocked && ratioBlocked && dryRun && runtimeProjection && staleDraftBlocked &&
                        staleApprovalBlocked && undo && redo && persisted && neighbors && ux4NPreserved,
                    explicitSelection, damage, death, validation, optionalRemoval, budgetBlocked, ratioBlocked,
                    dryRun, runtimeProjection, staleDraftBlocked, staleApprovalBlocked, canonical, undo && redo,
                    persisted, neighbors, ux4NPreserved, false);
            }
            catch
            {
                return empty;
            }
        }

        var result = Execute();
        reopened.Close();
        editing.Close();
        var released = DeleteUx4OSmokeRoot(root);
        return result with { Succeeded = result.Succeeded && released, TemporaryWorkspaceReleased = released };
    }

    internal static StudioCreatureDamageDraftUx4O ChangedCreatureDamageDraftUx4O(
        StudioCreatureDamageDraftUx4O draft) => draft with
        {
            DamageDeathEnabled = true,
            BeforeDamageHandler = "BeforeDamageBounded",
            DamageAppliedHandler = "OnDamageCommitted",
            DeathHandler = "OnEntityDeathComplete",
            BeforeDamageBudgetMilliseconds = "4",
            MaximumAdjustmentRatio = "0.25",
        };

    private bool TryBuildCreatureDamageCandidateUx4O(
        StudioCreatureDamageDraftUx4O draft,
        out StudioCreatureDamageCandidateUx4O candidate,
        out IReadOnlyList<StudioCreatureDamageDiagnosticUx4O> diagnostics)
    {
        var errors = new List<StudioCreatureDamageDiagnosticUx4O>();
        try
        {
            var source = draft.SourceProfile ?? CanonicalCreatureEventsUx4N(draft.BlueprintId);
            CreatureEventProfile.CreatureDamageDeathBindings? damage = null;
            if (draft.DamageDeathEnabled)
            {
                var current = source.DamageDeath;
                damage = new(
                    UpdateBindingUx4N(current?.BeforeDamage,
                        StableCreatureEventIdUx4N(source.Id, "before-damage"), "creature.before_damage",
                        draft.BeforeDamageHandler, source.Provenance),
                    UpdateBindingUx4N(current?.DamageApplied,
                        StableCreatureEventIdUx4N(source.Id, "damage-applied"), "creature.damage_applied",
                        draft.DamageAppliedHandler, source.Provenance),
                    UpdateBindingUx4N(current?.Died,
                        StableCreatureEventIdUx4N(source.Id, "entity-died"), "entity.died",
                        draft.DeathHandler, source.Provenance),
                    ParseIntUx4O(draft.BeforeDamageBudgetMilliseconds, "budget BeforeDamage"),
                    ParseDoubleUx4O(draft.MaximumAdjustmentRatio, "ajustement maximal"));
            }
            var profile = new CreatureEventProfile(source.Id, CreatureEventProfile.CurrentSchemaVersion,
                source.Provenance, source.RequiredAtRuntime, source.CombatBoundary, source.Inventory, source.Rest,
                source.Heartbeat, source.CustomEvents, source.Overrides, damage);
            errors.AddRange(CreatureEventValidator.Validate(profile,
                    $"blueprints/{draft.BlueprintId}/creatureEvents")
                .Select(CreatureDamageDiagnosticUx4O));
            var projected = WithCreatureEventsUx4N(Project!, draft.BlueprintId, profile);
            var prefix = $"blueprints/{draft.BlueprintId}";
            errors.AddRange(ProjectValidator.Validate(projected)
                .Where(item => item.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(item => new StudioCreatureDamageDiagnosticUx4O(item.Code, item.Path,
                    HumanCreatureDamageUx4O(item.Code))));
            candidate = new(profile);
        }
        catch (FormatException exception)
        {
            candidate = new(CanonicalCreatureEventsUx4N(draft.BlueprintId));
            errors.Add(new("studio.creature.damage.form_invalid", "creatureEvents/damageDeath",
                exception.Message));
        }
        diagnostics = errors.GroupBy(item => (item.Code, item.Path)).Select(item => item.First()).ToArray();
        return diagnostics.Count == 0;
    }

    private static CreatureEventProfile CanonicalCreatureDamageUx4O(BlueprintDefinition blueprint)
    {
        var source = blueprint.CreatureEvents ?? CanonicalCreatureEventsUx4N(blueprint.Id);
        const string provenance = "studio:ux4-o";
        var damage = new CreatureEventProfile.CreatureDamageDeathBindings(
            UpdateBindingUx4N(source.DamageDeath?.BeforeDamage,
                StableCreatureEventIdUx4N(source.Id, "before-damage"), "creature.before_damage",
                "BeforeDamage", provenance),
            UpdateBindingUx4N(source.DamageDeath?.DamageApplied,
                StableCreatureEventIdUx4N(source.Id, "damage-applied"), "creature.damage_applied",
                "OnDamaged", provenance),
            UpdateBindingUx4N(source.DamageDeath?.Died,
                StableCreatureEventIdUx4N(source.Id, "entity-died"), "entity.died", "OnDeath", provenance),
            4, .5);
        return new(source.Id, source.SchemaVersion, source.Provenance, source.RequiredAtRuntime,
            source.CombatBoundary, source.Inventory, source.Rest, source.Heartbeat, source.CustomEvents,
            source.Overrides, damage);
    }

    private static bool EqualUx4NBindingsUx4O(CreatureEventProfile before, CreatureEventProfile after) =>
        JsonProfileWithoutDamageUx4O(before) == JsonProfileWithoutDamageUx4O(after);

    private static string JsonProfileWithoutDamageUx4O(CreatureEventProfile profile)
    {
        var copy = new CreatureEventProfile(profile.Id, profile.SchemaVersion, profile.Provenance,
            profile.RequiredAtRuntime, profile.CombatBoundary, profile.Inventory, profile.Rest, profile.Heartbeat,
            profile.CustomEvents, profile.Overrides, damageDeath: null);
        return System.Text.Json.JsonSerializer.Serialize(copy);
    }

    private static StudioCreatureDamagePreviewUx4O FailedCreatureDamageUx4O(string message, string code) =>
        new(false, false, message, string.Empty, string.Empty,
            [new(code, "creatureEvents/damageDeath", message)], "Dégâts non évalués.", "Mort non évaluée.",
            "Validation non évaluée.", "Runtime non évalué.");

    private static StudioCreatureDamageDiagnosticUx4O CreatureDamageDiagnosticUx4O(
        CreatureEventDiagnostic diagnostic) => new(diagnostic.Code, diagnostic.Path,
        HumanCreatureDamageUx4O(diagnostic.Code));

    private static IReadOnlyList<StudioCreatureDamageDiagnosticUx4O> CreatureDamageDiagnosticsUx4O(
        IReadOnlyList<CommandDiagnostic> diagnostics) => diagnostics.Select(item =>
    {
        var separator = item.Message.IndexOf(':');
        var path = separator > 0 ? item.Message[..separator] : "creatureEvents/damageDeath";
        return new StudioCreatureDamageDiagnosticUx4O(item.Code, path, HumanCreatureDamageUx4O(item.Code));
    }).ToArray();

    private static string HumanCreatureDamageUx4O(string code) => code switch
    {
        "creature.events.binding_invalid" => "Les trois handlers doivent être valides, API 1.0 et autorité serveur.",
        "creature.events.before_damage_budget_invalid" => "Le hook BeforeDamage doit rester entre 1 et 50 ms.",
        "creature.events.before_damage_adjustment_invalid" => "L’ajustement maximal doit rester fini entre 0 et 1.",
        _ => code,
    };

    private static string DamageSummaryUx4O(CreatureEventProfile profile) => profile.DamageDeath is null
        ? "Cycle dégâts/mort désactivé."
        : $"BeforeDamage → {profile.DamageDeath.BeforeDamage.Handler} · budget " +
          $"{profile.DamageDeath.BeforeDamageBudgetMilliseconds} ms · ajustement maximal " +
          $"{profile.DamageDeath.MaximumAdjustmentRatio.ToString("0.###", CultureInfo.InvariantCulture)} ; " +
          $"DamageApplied → {profile.DamageDeath.DamageApplied.Handler}.";

    private static string DeathSummaryUx4O(CreatureEventProfile profile) => profile.DamageDeath is null
        ? "Mort transactionnelle désactivée."
        : $"EntityDied → {profile.DamageDeath.Died.Handler} · killer, contributeurs, cause, position, loot et policy.";

    private static string ValidationSummaryUx4O(CreatureEventProfile profile, int diagnostics) =>
        $"Validation ciblée : événement {(profile is null ? "absent" : "présent")}, fondation, présentation, " +
        $"progression, loot, rôle, règles et références · {diagnostics} diagnostic(s).";

    private static int ParseIntUx4O(string value, string label) => int.TryParse(value,
        NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
        ? parsed : throw new FormatException($"{label} invalide : '{value}'.");

    private static double ParseDoubleUx4O(string value, string label) => double.TryParse(value,
        NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed)
        ? parsed : throw new FormatException($"{label} invalide : '{value}'.");

    private static string FingerprintCreatureDamageCandidateUx4O(
        ProjectDefinition project, BlueprintId blueprintId, StudioCreatureDamageCandidateUx4O candidate) =>
        FingerprintProjectUx4N(WithCreatureEventsUx4N(project, blueprintId, candidate.Profile));

    private static string FingerprintCreatureDamageDraftUx4O(StudioCreatureDamageDraftUx4O draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteUx4O(writer, draft.BlueprintId.ToString());
        writer.Write(draft.DamageDeathEnabled);
        WriteUx4O(writer, draft.BeforeDamageHandler);
        WriteUx4O(writer, draft.DamageAppliedHandler);
        WriteUx4O(writer, draft.DeathHandler);
        WriteUx4O(writer, draft.BeforeDamageBudgetMilliseconds);
        WriteUx4O(writer, draft.MaximumAdjustmentRatio);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteUx4O(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static bool DeleteUx4OSmokeRoot(string root)
    {
        try
        {
            var full = Path.GetFullPath(root);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) +
                       Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4o-smoke-",
                    StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full) && !File.Exists(full);
        }
        catch
        {
            return false;
        }
    }
}
