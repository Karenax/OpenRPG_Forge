using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Creatures;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Lifecycle;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Creatures;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SetCreatureFoundationHandler _setCreatureEventAiUx4P = new(new SystemClock());
    private StudioCreatureEventAiDraftUx4P? _approvedCreatureEventAiDraftUx4P;
    private StudioCreatureEventAiCandidateUx4P? _approvedCreatureEventAiCandidateUx4P;
    private ProjectRevision? _approvedCreatureEventAiRevisionUx4P;
    private string? _approvedCreatureEventAiProjectFingerprintUx4P;
    private string? _approvedCreatureEventAiDraftFingerprintUx4P;
    private string? _approvedCreatureEventAiCandidateFingerprintUx4P;

    internal IReadOnlyList<StudioCreatureEventAiChoiceUx4P> CreatureEventAiChoicesUx4P =>
        Project?.Blueprints
            .Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => new StudioCreatureEventAiChoiceUx4P(item.Id,
                $"{item.DisplayName} · {(item.CreatureFoundation is null ? "fondation requise" :
                    item.CreatureFoundation.EventAiComposition is null ? "profil Event/IA absent" :
                    "profil Event/IA configuré")}", item.CreatureFoundation is not null,
                item.CreatureFoundation?.EventAiComposition is not null, item.IsReadOnly())).ToArray() ?? [];

    internal StudioCreatureEventAiDraftUx4P? CreateCreatureEventAiDraftUx4P(BlueprintId blueprintId)
    {
        var project = Project;
        var history = History;
        var blueprint = project?.Blueprints.SingleOrDefault(item => item.Id == blueprintId);
        var foundation = blueprint?.CreatureFoundation;
        if (project is null || history is null || blueprint is null || foundation is null ||
            blueprint.IsReadOnly() || blueprint.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn))
            return null;
        var source = foundation.EventAiComposition;
        var working = source ?? CanonicalCreatureEventAiUx4P(blueprint.Id, foundation.Provenance);
        return new(history.State.Revision, FingerprintProjectUx4D(project), blueprint.Id, blueprint, foundation,
            source, source is not null, foundation.EventAiCompositionRequiredAtRuntime, working.AssetId,
            working.Perception.Detected.Handler, working.Perception.Lost.Handler,
            working.PhysicalAttack.Declared.Handler, working.PhysicalAttack.Resolved.Handler,
            working.Ability.Targeted.Handler, working.Ability.Impacted.Handler, working.Blocked.Blocked.Handler,
            string.Join(", ", working.Blocked.RecoveryOrder.Select(item => item.ToString())),
            working.Controller.Kind, working.Controller.DefinitionId,
            string.Join('\n', working.Controller.Blackboard.Select(item =>
                $"{item.Key}|{item.Kind}|{item.Value}|{item.Persistent.ToString().ToLowerInvariant()}")),
            string.Join(", ", working.Controller.AllowedActions),
            string.Join('\n', working.Overrides.Select(item => $"{item.Key}={item.Value}")));
    }

    internal StudioCreatureEventAiPreviewUx4P PreviewCreatureEventAiUx4P(StudioCreatureEventAiDraftUx4P draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelCreatureEventAiUx4P();
        var project = Project;
        var history = History;
        if (project is null || history is null)
            return FailedCreatureEventAiUx4P("Aucun module n’est ouvert.", "studio.creature.project_missing");
        if (history.State.Revision != draft.BaseRevision ||
            !string.Equals(FingerprintProjectUx4D(project), draft.SourceProjectFingerprint,
                StringComparison.Ordinal))
            return FailedCreatureEventAiUx4P("Le projet a changé ; rechargez la fiche.",
                "command.revision_conflict");
        if (!TryBuildCreatureEventAiCandidateUx4P(draft, out var candidate, out var diagnostics))
            return new(false, false, "Le profil événements/IA est invalide.", string.Empty, string.Empty,
                diagnostics, "Perception non évaluée.", "Combat non évalué.", "Blocage non évalué.",
                "Contrôleur non évalué.", "Runtime non évalué.");
        var dryRun = _setCreatureEventAiUx4P.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, draft.BlueprintId, candidate.Foundation,
                DryRun: true));
        if (!dryRun.Succeeded)
            return new(false, false, "La commande canonique refuse la fondation.", string.Empty, string.Empty,
                CreatureEventAiDiagnosticsUx4P(dryRun.Diagnostics),
                PerceptionSummaryUx4P(candidate.Foundation.EventAiComposition),
                CombatSummaryUx4P(candidate.Foundation.EventAiComposition),
                BlockedSummaryUx4P(candidate.Foundation.EventAiComposition),
                ControllerSummaryUx4P(candidate.Foundation.EventAiComposition), "Runtime non évalué.");

        var profile = candidate.Foundation.EventAiComposition;
        var runtime = "Profil Event/IA optionnel désactivé ; aucun binding ni décision produit.";
        if (profile is not null)
        {
            var projected = WithCreatureFoundationUx4D(project, draft.BlueprintId, candidate.Foundation);
            var script = CreatureEventAiCompositionScriptService.Read(projected,
                CreatureEventAiCompositionScriptService.CreateHandle(draft.BlueprintId));
            var rpg = CreatureEventAiCompositionFoundationService.Resolve(projected, draft.BlueprintId);
            var decision = CreatureEventAiCompositionValidator.Decide(profile,
                new Dictionary<string, double> { ["attack"] = .9, ["guard"] = .4, ["patrol"] = .2 });
            runtime = $"Projection pure : {CreatureEventAiCompositionFoundationService.EnumerateBindings(rpg).Length} " +
                      $"bindings, décision {decision.Action} ({decision.Score:0.##}), " +
                      $"{script?.Blackboard.Count ?? 0} valeur(s) de blackboard. " +
                      "Aucun stimulus, attaque, capacité, mouvement, script, acteur ou réseau n’est exécuté.";
        }
        var frozen = draft with { };
        _approvedCreatureEventAiDraftUx4P = frozen;
        _approvedCreatureEventAiCandidateUx4P = candidate;
        _approvedCreatureEventAiRevisionUx4P = history.State.Revision;
        _approvedCreatureEventAiProjectFingerprintUx4P = FingerprintProjectUx4D(project);
        _approvedCreatureEventAiDraftFingerprintUx4P = FingerprintCreatureEventAiDraftUx4P(frozen);
        _approvedCreatureEventAiCandidateFingerprintUx4P =
            FingerprintCreatureEventAiCandidateUx4P(project, draft.BlueprintId, candidate);
        return new(true, true, "Événements et contrôleur IA conformes · prêts à être appliqués.",
            dryRun.Patch?.Summary ?? "Définir le profil événements/IA.",
            dryRun.Inverse?.Summary ?? "Restaurer la fondation précédente.", [],
            PerceptionSummaryUx4P(profile), CombatSummaryUx4P(profile), BlockedSummaryUx4P(profile),
            ControllerSummaryUx4P(profile), runtime);
    }

    internal StudioCreatureEventAiActionResultUx4P ApplyApprovedCreatureEventAiUx4P(
        StudioCreatureEventAiDraftUx4P currentDraft)
    {
        ArgumentNullException.ThrowIfNull(currentDraft);
        var project = Project;
        var history = History;
        var approved = _approvedCreatureEventAiDraftUx4P;
        var candidate = _approvedCreatureEventAiCandidateUx4P;
        if (project is null || history is null || approved is null || candidate is null ||
            _approvedCreatureEventAiRevisionUx4P is null ||
            _approvedCreatureEventAiProjectFingerprintUx4P is null ||
            _approvedCreatureEventAiDraftFingerprintUx4P is null ||
            _approvedCreatureEventAiCandidateFingerprintUx4P is null)
            return new(false, "Prévisualisez le profil avant de l’appliquer.");
        var stale = history.State.Revision != _approvedCreatureEventAiRevisionUx4P ||
                    !string.Equals(FingerprintProjectUx4D(project),
                        _approvedCreatureEventAiProjectFingerprintUx4P, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureEventAiDraftUx4P(currentDraft),
                        _approvedCreatureEventAiDraftFingerprintUx4P, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureEventAiDraftUx4P(approved),
                        _approvedCreatureEventAiDraftFingerprintUx4P, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureEventAiCandidateUx4P(project, currentDraft.BlueprintId,
                            candidate), _approvedCreatureEventAiCandidateFingerprintUx4P,
                        StringComparison.Ordinal);
        if (stale)
        {
            CancelCreatureEventAiUx4P();
            return new(false, "Le brouillon, le projet ou l’approbation a changé ; prévisualisez à nouveau.");
        }
        var validation = _setCreatureEventAiUx4P.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, currentDraft.BlueprintId,
                candidate.Foundation, DryRun: true));
        if (!validation.Succeeded)
        {
            CancelCreatureEventAiUx4P();
            return new(false, "La revalidation canonique refuse le profil.");
        }
        var result = _setCreatureEventAiUx4P.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, currentDraft.BlueprintId,
                candidate.Foundation, DryRun: false));
        CancelCreatureEventAiUx4P();
        if (!result.Succeeded) return new(false, "La commande n’a pas appliqué le profil.");
        history.Record(result);
        InvalidateContentIndexes();
        return new(true, result.Patch?.Summary ?? "Profil événements/IA appliqué.");
    }

    internal StudioCreatureEventAiActionResultUx4P UndoCreatureEventAiUx4P()
    { CancelCreatureEventAiUx4P(); var result = Undo(); return new(result.Succeeded, result.Message); }

    internal StudioCreatureEventAiActionResultUx4P RedoCreatureEventAiUx4P()
    { CancelCreatureEventAiUx4P(); var result = Redo(); return new(result.Succeeded, result.Message); }

    internal void CancelCreatureEventAiUx4P()
    {
        _approvedCreatureEventAiDraftUx4P = null;
        _approvedCreatureEventAiCandidateUx4P = null;
        _approvedCreatureEventAiRevisionUx4P = null;
        _approvedCreatureEventAiProjectFingerprintUx4P = null;
        _approvedCreatureEventAiDraftFingerprintUx4P = null;
        _approvedCreatureEventAiCandidateFingerprintUx4P = null;
    }

    internal void CloseUx4P() => CancelCreatureEventAiUx4P();

    internal StudioCreatureEventAiApprovalSnapshotUx4P CaptureCreatureEventAiApprovalUx4P() => new(
        _approvedCreatureEventAiDraftUx4P, _approvedCreatureEventAiCandidateUx4P,
        _approvedCreatureEventAiRevisionUx4P, _approvedCreatureEventAiProjectFingerprintUx4P,
        _approvedCreatureEventAiDraftFingerprintUx4P, _approvedCreatureEventAiCandidateFingerprintUx4P);

    internal void RestoreCreatureEventAiApprovalUx4P(StudioCreatureEventAiApprovalSnapshotUx4P snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedCreatureEventAiDraftUx4P = snapshot.Draft;
        _approvedCreatureEventAiCandidateUx4P = snapshot.Candidate;
        _approvedCreatureEventAiRevisionUx4P = snapshot.Revision;
        _approvedCreatureEventAiProjectFingerprintUx4P = snapshot.ProjectFingerprint;
        _approvedCreatureEventAiDraftFingerprintUx4P = snapshot.DraftFingerprint;
        _approvedCreatureEventAiCandidateFingerprintUx4P = snapshot.CandidateFingerprint;
    }

    internal StudioCreatureEventAiEvidenceProjectUx4P? CreateUx4PEvidenceProject()
    {
        var project = Project;
        if (project is null) return null;
        var real = project.Blueprints.Where(item =>
                (item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn) && !item.IsReadOnly() &&
                item.CreatureFoundation?.EventAiComposition is not null)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault(item => CreatureEventAiCompositionValidator.Validate(
                item.CreatureFoundation!.EventAiComposition!).Count == 0);
        if (real is not null) return new(project, real.Id, true, false);
        var editable = project.Blueprints.Where(item =>
                (item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn) && !item.IsReadOnly())
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase).FirstOrDefault();
        if (editable is null) return null;
        var foundation = editable.CreatureFoundation ?? CreateCanonicalEvidenceFoundationUx4D(editable);
        var profile = CanonicalCreatureEventAiUx4P(editable.Id, "studio:ux4-p-evidence");
        var candidate = WithEventAiUx4P(foundation, profile, true);
        return new(WithCreatureFoundationUx4D(project, editable.Id, candidate), editable.Id, false, true);
    }

    internal StudioUx4PSmokeResult RunUx4PContractSmoke()
    {
        var empty = new StudioUx4PSmokeResult(false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, false, false, false, false);
        var evidence = CreateUx4PEvidenceProject();
        if (evidence is null) return empty;
        var root = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux4p-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        StudioUx4PSmokeResult Execute()
        {
            try
            {
                if (!ProjectSession.Create(root, evidence.Project).Succeeded || !editing.Open(root).Succeeded)
                    return empty;
                var explicitSelection = editing.CreatureEventAiChoicesUx4P.Count > 0 &&
                    editing.CreatureEventAiChoicesUx4P.All(item =>
                        !item.Label.Contains(item.BlueprintId.ToString(), StringComparison.OrdinalIgnoreCase));
                var draft = editing.CreateCreatureEventAiDraftUx4P(evidence.BlueprintId);
                if (draft is null) return empty with { ExplicitSelection = explicitSelection };
                var changed = ChangedCreatureEventAiDraftUx4P(draft);
                var sourceProject = editing.Project!;
                var sourceHistory = editing.History!;
                var preview = editing.PreviewCreatureEventAiUx4P(changed);
                var dryRun = preview.Succeeded && ReferenceEquals(sourceProject, editing.Project) &&
                             ReferenceEquals(sourceHistory, editing.History) &&
                             sourceHistory.State.Revision == draft.BaseRevision;
                var runtime = preview.Succeeded && preview.RuntimeSummary.Contains("7 bindings",
                    StringComparison.OrdinalIgnoreCase) && preview.RuntimeSummary.Contains("décision attack",
                    StringComparison.OrdinalIgnoreCase);
                var invalidBinding = editing.PreviewCreatureEventAiUx4P(changed with
                {
                    PerceptionDetectedHandler = "handler invalide"
                });
                var bindingBlocked = !invalidBinding.Succeeded && invalidBinding.Diagnostics.Any(item =>
                    item.Code == "creature.event_ai.binding_invalid");
                var invalidBlackboard = editing.PreviewCreatureEventAiUx4P(changed with
                {
                    Blackboard = "courage|Number|NaN|true"
                });
                var blackboardBlocked = !invalidBlackboard.Succeeded && invalidBlackboard.Diagnostics.Any(item =>
                    item.Code == "creature.event_ai.controller_invalid");
                var invalidRecovery = editing.PreviewCreatureEventAiUx4P(changed with
                {
                    RecoveryOrder = "Repath, Repath"
                });
                var recoveryBlocked = !invalidRecovery.Succeeded;
                var optional = editing.PreviewCreatureEventAiUx4P(changed with
                {
                    Enabled = false, RequiredAtRuntime = false
                });
                var optionalRemoval = optional.Succeeded && optional.ControllerSummary.Contains("désactivé",
                    StringComparison.OrdinalIgnoreCase);

                var approvedBeforeRevision = editing.PreviewCreatureEventAiUx4P(changed);
                var neighborChanged = ToggleCreatureRequiredUx4D(editing, evidence.BlueprintId);
                var staleDraftBlocked = approvedBeforeRevision.Succeeded && neighborChanged &&
                    !editing.ApplyApprovedCreatureEventAiUx4P(changed).Succeeded;
                if (neighborChanged) editing.UndoCreatureEventAiUx4P();

                var fresh = editing.CreateCreatureEventAiDraftUx4P(evidence.BlueprintId)!;
                var freshChanged = ChangedCreatureEventAiDraftUx4P(fresh);
                var approvedBeforeTamper = editing.PreviewCreatureEventAiUx4P(freshChanged);
                var staleApprovalBlocked = approvedBeforeTamper.Succeeded &&
                    !editing.ApplyApprovedCreatureEventAiUx4P(freshChanged with
                    {
                        BlockedHandler = freshChanged.BlockedHandler + "Tampered"
                    }).Succeeded;

                fresh = editing.CreateCreatureEventAiDraftUx4P(evidence.BlueprintId)!;
                freshChanged = ChangedCreatureEventAiDraftUx4P(fresh);
                var beforeApply = editing.Project!;
                var beforeFoundation = beforeApply.Blueprints.Single(item => item.Id == evidence.BlueprintId)
                    .CreatureFoundation!;
                var finalPreview = editing.PreviewCreatureEventAiUx4P(freshChanged);
                var apply = finalPreview.Succeeded
                    ? editing.ApplyApprovedCreatureEventAiUx4P(freshChanged)
                    : new StudioCreatureEventAiActionResultUx4P(false, "preview failed");
                var appliedProject = editing.Project;
                var appliedFoundation = appliedProject?.Blueprints.Single(item => item.Id == evidence.BlueprintId)
                    .CreatureFoundation;
                var applied = appliedFoundation?.EventAiComposition;
                var perception = applied?.Perception is
                {
                    Detected.Handler: "OnStimulusDetected",
                    Lost.Handler: "OnStimulusLost"
                };
                var physical = applied?.PhysicalAttack is
                {
                    Declared.Handler: "BeforePhysicalAttack",
                    Resolved.Handler: "OnPhysicalAttackResolved"
                };
                var ability = applied?.Ability is
                {
                    Targeted.Handler: "BeforeAbilityTargeted",
                    Impacted.Handler: "OnAbilityImpacted"
                };
                var blocked = applied?.Blocked.Blocked.Handler == "OnMovementBlocked" &&
                              applied.Blocked.RecoveryOrder.SequenceEqual(new[]
                              {
                                  CreatureEventAiRecoveryStrategy.Repath,
                                  CreatureEventAiRecoveryStrategy.Avoid,
                                  CreatureEventAiRecoveryStrategy.Wait,
                                  CreatureEventAiRecoveryStrategy.Abort,
                              });
                var controller = applied?.Controller.Kind == CreatureEventAiControllerKind.Utility &&
                                 applied.Controller.DefinitionId == "ai.utility.warden" &&
                                 applied.Controller.AllowedActions.SequenceEqual(new[] { "attack", "guard", "patrol" });
                var canonical = apply.Succeeded && perception && physical && ability && blocked && controller;
                var expected = appliedFoundation is null ? null : WithCreatureFoundationUx4D(beforeApply,
                    evidence.BlueprintId, appliedFoundation);
                var neighbors = canonical && expected is not null && appliedProject is not null &&
                                ProjectJsonSerializer.Serialize(expected) ==
                                ProjectJsonSerializer.Serialize(appliedProject) &&
                                FoundationNeighborsEqualUx4P(beforeFoundation, appliedFoundation!);
                var appliedJson = appliedProject is null ? string.Empty :
                    ProjectJsonSerializer.Serialize(appliedProject);
                var beforeJson = ProjectJsonSerializer.Serialize(beforeApply);
                var undo = canonical && editing.UndoCreatureEventAiUx4P().Succeeded &&
                           ProjectJsonSerializer.Serialize(editing.Project!) == beforeJson;
                var redo = undo && editing.RedoCreatureEventAiUx4P().Succeeded &&
                           ProjectJsonSerializer.Serialize(editing.Project!) == appliedJson;
                var saved = redo && editing.Save().Succeeded;
                var persisted = saved && reopened.Open(root).Succeeded &&
                                ProjectJsonSerializer.Serialize(reopened.Project!) == appliedJson;
                return new(canonical && explicitSelection && perception && physical && ability && blocked &&
                        controller && optionalRemoval && bindingBlocked && blackboardBlocked && recoveryBlocked &&
                        dryRun && runtime && staleDraftBlocked && staleApprovalBlocked && undo && redo &&
                        persisted && neighbors,
                    explicitSelection, perception, physical, ability, blocked, controller, optionalRemoval,
                    bindingBlocked, blackboardBlocked, recoveryBlocked, dryRun, runtime, staleDraftBlocked,
                    staleApprovalBlocked, canonical, undo && redo, persisted, neighbors, false);
            }
            catch
            {
                return empty;
            }
        }

        var result = Execute();
        reopened.Close();
        editing.Close();
        var released = DeleteUx4PSmokeRoot(root);
        return result with { Succeeded = result.Succeeded && released, TemporaryWorkspaceReleased = released };
    }

    internal static StudioCreatureEventAiDraftUx4P ChangedCreatureEventAiDraftUx4P(
        StudioCreatureEventAiDraftUx4P draft) => draft with
    {
        Enabled = true,
        RequiredAtRuntime = true,
        AssetId = "asset.creature-event-ai.warden",
        PerceptionDetectedHandler = "OnStimulusDetected",
        PerceptionLostHandler = "OnStimulusLost",
        AttackDeclaredHandler = "BeforePhysicalAttack",
        AttackResolvedHandler = "OnPhysicalAttackResolved",
        AbilityTargetedHandler = "BeforeAbilityTargeted",
        AbilityImpactedHandler = "OnAbilityImpacted",
        BlockedHandler = "OnMovementBlocked",
        RecoveryOrder = "Repath, Avoid, Wait, Abort",
        ControllerKind = CreatureEventAiControllerKind.Utility,
        ControllerDefinitionId = "ai.utility.warden",
        Blackboard = "state|Text|guard|true\ntarget|Entity|entity.hero|false\ncourage|Number|0.8|true",
        AllowedActions = "attack, guard, patrol",
        Overrides = "decision.threshold=0.65",
    };

    private bool TryBuildCreatureEventAiCandidateUx4P(
        StudioCreatureEventAiDraftUx4P draft,
        out StudioCreatureEventAiCandidateUx4P candidate,
        out IReadOnlyList<StudioCreatureEventAiDiagnosticUx4P> diagnostics)
    {
        var errors = new List<StudioCreatureEventAiDiagnosticUx4P>();
        if (!draft.Enabled)
        {
            if (draft.RequiredAtRuntime)
                errors.Add(new(CreatureEventAiCompositionValidator.MissingRequiredCode,
                    "creatureFoundation/eventAiComposition", "Un profil requis ne peut pas être désactivé."));
            candidate = new(WithEventAiUx4P(draft.SourceFoundation, null, false));
            diagnostics = errors;
            return errors.Count == 0;
        }
        try
        {
            if (!Enum.IsDefined(draft.ControllerKind))
                errors.Add(new("creature.event_ai.controller_invalid", "creatureFoundation/eventAiComposition/controller",
                    "Le type de contrôleur IA est inconnu."));
            var source = draft.SourceProfile ?? CanonicalCreatureEventAiUx4P(draft.BlueprintId,
                draft.SourceFoundation.Provenance);
            var recovery = ParseEnumCsvUx4P<CreatureEventAiRecoveryStrategy>(draft.RecoveryOrder,
                "ordre de récupération");
            if (recovery.Distinct().Count() != recovery.Count)
                errors.Add(new("creature.event_ai.recovery_invalid",
                    "creatureFoundation/eventAiComposition/blocked/recoveryOrder",
                    "Chaque stratégie de récupération doit apparaître une seule fois."));
            var blackboardRows = ParseRowsUx4P(draft.Blackboard, 4, "blackboard");
            var keys = blackboardRows.Select(row => row[0]).ToArray();
            if (keys.Distinct(StringComparer.Ordinal).Count() != keys.Length)
                errors.Add(new("creature.event_ai.controller_invalid",
                    "creatureFoundation/eventAiComposition/controller/blackboard",
                    "Chaque clé de blackboard doit être unique."));
            var blackboard = blackboardRows.Select((row, index) =>
            {
                if (!Enum.TryParse<CreatureEventAiBlackboardValueKind>(row[1], true, out var kind) ||
                    !bool.TryParse(row[3], out var persistent))
                    throw new FormatException("Blackboard attendu : clé|type|valeur|persistant.");
                var existing = source.Controller.Blackboard.FirstOrDefault(item => item.Key == row[0]);
                return new CreatureEventAiBlackboardEntry(existing?.Id ??
                    StableCreatureIdUx4D(source.Id, $"blackboard:{row[0]}:{index}"), row[0], kind, row[2], persistent);
            }).ToArray();
            var actions = SplitCsvUx4P(draft.AllowedActions);
            if (actions.Distinct(StringComparer.Ordinal).Count() != actions.Count)
                errors.Add(new("creature.event_ai.controller_invalid",
                    "creatureFoundation/eventAiComposition/controller/allowedActions",
                    "Chaque action autorisée doit apparaître une seule fois."));
            var profile = new CreatureEventAiCompositionProfile(source.Id,
                CreatureEventAiCompositionProfile.CurrentSchemaVersion, source.Provenance, draft.AssetId,
                source.PreviewTransform,
                new(source.Perception.Id,
                    UpdateEventAiBindingUx4P(source.Perception.Detected, "creature.stimulus_detected",
                        draft.PerceptionDetectedHandler),
                    UpdateEventAiBindingUx4P(source.Perception.Lost, "creature.stimulus_lost",
                        draft.PerceptionLostHandler)),
                new(source.PhysicalAttack.Id,
                    UpdateEventAiBindingUx4P(source.PhysicalAttack.Declared, "creature.attack_declared",
                        draft.AttackDeclaredHandler),
                    UpdateEventAiBindingUx4P(source.PhysicalAttack.Resolved, "creature.attack_resolved",
                        draft.AttackResolvedHandler)),
                new(source.Ability.Id,
                    UpdateEventAiBindingUx4P(source.Ability.Targeted, "creature.ability_targeted",
                        draft.AbilityTargetedHandler),
                    UpdateEventAiBindingUx4P(source.Ability.Impacted, "creature.ability_impacted",
                        draft.AbilityImpactedHandler)),
                new(source.Blocked.Id,
                    UpdateEventAiBindingUx4P(source.Blocked.Blocked, "creature.movement_blocked",
                        draft.BlockedHandler), recovery),
                new(source.Controller.Id, draft.ControllerKind, draft.ControllerDefinitionId, blackboard, actions),
                ParseMapUx4P(draft.Overrides));
            errors.AddRange(CreatureEventAiCompositionValidator.Validate(profile,
                    $"blueprints/{draft.BlueprintId}/creatureFoundation/eventAiComposition")
                .Select(CreatureEventAiDiagnosticUx4P));
            candidate = new(WithEventAiUx4P(draft.SourceFoundation, profile, draft.RequiredAtRuntime));
        }
        catch (FormatException exception)
        {
            candidate = new(draft.SourceFoundation);
            errors.Add(new("studio.creature.event_ai.form_invalid", "creatureFoundation/eventAiComposition",
                exception.Message));
        }
        diagnostics = errors.GroupBy(item => (item.Code, item.Path)).Select(item => item.First()).ToArray();
        return diagnostics.Count == 0;
    }

    private static CreatureFoundationProfile WithEventAiUx4P(CreatureFoundationProfile source,
        CreatureEventAiCompositionProfile? profile, bool required) => new(source.Id, source.SchemaVersion,
        source.Provenance, source.RequiredAtRuntime, source.Identity, source.DefaultTransform, source.Resources,
        source.Movement, source.References, source.Spawned, source.Overrides, source.AuthoringComposition,
        source.AuthoringCompositionRequiredAtRuntime, source.RulesComposition,
        source.RulesCompositionRequiredAtRuntime, source.InteractionComposition,
        source.InteractionCompositionRequiredAtRuntime, profile, required);

    private static CreatureEventAiCompositionProfile CanonicalCreatureEventAiUx4P(BlueprintId blueprintId,
        string provenance)
    {
        var id = StableCreatureIdUx4D(blueprintId.Value, "event-ai-profile");
        EventBinding Binding(string key, string eventName, string handler) => new(
            StableCreatureIdUx4D(id, key), 1, provenance, "1.0", eventName, handler,
            EventAuthorityScope.Server);
        return new(id, CreatureEventAiCompositionProfile.CurrentSchemaVersion, provenance,
            "asset.creature-event-ai.default", Forge.Domain.Spatial.TransformValue.Identity,
            new(StableCreatureIdUx4D(id, "perception"),
                Binding("perception-detected", "creature.stimulus_detected", "OnPerceptionDetected"),
                Binding("perception-lost", "creature.stimulus_lost", "OnPerceptionLost")),
            new(StableCreatureIdUx4D(id, "physical-attack"),
                Binding("attack-declared", "creature.attack_declared", "BeforePhysicalAttacked"),
                Binding("attack-resolved", "creature.attack_resolved", "OnPhysicalAttacked")),
            new(StableCreatureIdUx4D(id, "ability"),
                Binding("ability-targeted", "creature.ability_targeted", "BeforeAbilityTargeted"),
                Binding("ability-impacted", "creature.ability_impacted", "OnSpellCastAt")),
            new(StableCreatureIdUx4D(id, "blocked"),
                Binding("movement-blocked", "creature.movement_blocked", "OnBlocked"),
                [CreatureEventAiRecoveryStrategy.Repath, CreatureEventAiRecoveryStrategy.Avoid,
                    CreatureEventAiRecoveryStrategy.Teleport, CreatureEventAiRecoveryStrategy.Abort]),
            new(StableCreatureIdUx4D(id, "controller"), CreatureEventAiControllerKind.StateMachine,
                "ai.default",
                [new(StableCreatureIdUx4D(id, "blackboard-state"), "state",
                    CreatureEventAiBlackboardValueKind.Text, "idle", true),
                 new(StableCreatureIdUx4D(id, "blackboard-target"), "target",
                    CreatureEventAiBlackboardValueKind.Entity, "entity.none", false)],
                ["idle", "patrol"]), new Dictionary<string, string>());
    }

    private static EventBinding UpdateEventAiBindingUx4P(EventBinding source, string eventName, string handler) =>
        source with { SchemaVersion = 1, ForgeScriptApiVersion = "1.0", EventName = eventName,
            Handler = handler, Authority = EventAuthorityScope.Server };

    private static bool FoundationNeighborsEqualUx4P(CreatureFoundationProfile before,
        CreatureFoundationProfile after) => JsonFoundationWithoutEventAiUx4P(before) ==
                                             JsonFoundationWithoutEventAiUx4P(after);

    private static string JsonFoundationWithoutEventAiUx4P(CreatureFoundationProfile source) =>
        System.Text.Json.JsonSerializer.Serialize(WithEventAiUx4P(source, null, false));

    private static StudioCreatureEventAiPreviewUx4P FailedCreatureEventAiUx4P(string message, string code) =>
        new(false, false, message, string.Empty, string.Empty,
            [new(code, "creatureFoundation/eventAiComposition", message)], "Perception non évaluée.",
            "Combat non évalué.", "Blocage non évalué.", "Contrôleur non évalué.", "Runtime non évalué.");

    private static StudioCreatureEventAiDiagnosticUx4P CreatureEventAiDiagnosticUx4P(
        CreatureFoundationDiagnostic item) => new(item.Code, item.Path,
        HumanCreatureEventAiUx4P(item.Code, item.Message));

    private static IReadOnlyList<StudioCreatureEventAiDiagnosticUx4P> CreatureEventAiDiagnosticsUx4P(
        IReadOnlyList<CommandDiagnostic> diagnostics) => diagnostics.Select(item =>
    {
        var separator = item.Message.IndexOf(": ", StringComparison.Ordinal);
        var path = separator > 0 ? item.Message[..separator] : "creatureFoundation/eventAiComposition";
        var message = separator > 0 ? item.Message[(separator + 2)..] : item.Message;
        return new StudioCreatureEventAiDiagnosticUx4P(item.Code, path,
            HumanCreatureEventAiUx4P(item.Code, message));
    }).ToArray();

    private static string HumanCreatureEventAiUx4P(string code, string fallback) => code switch
    {
        "creature.event_ai.binding_invalid" => "Chaque binding doit utiliser un handler valide, API 1.0 et autorité serveur.",
        "creature.event_ai.recovery_invalid" => "Choisissez une à cinq stratégies de récupération uniques.",
        "creature.event_ai.controller_invalid" => "Le contrôleur exige définition, blackboard typé et actions valides.",
        "creature.event_ai.component_uuid_invalid" => "Les identités internes doivent rester stables et uniques.",
        _ => fallback,
    };

    private static string PerceptionSummaryUx4P(CreatureEventAiCompositionProfile? profile) => profile is null
        ? "OnPerception désactivé."
        : $"OnPerception · détecté → {profile.Perception.Detected.Handler} · perdu → {profile.Perception.Lost.Handler}.";

    private static string CombatSummaryUx4P(CreatureEventAiCompositionProfile? profile) => profile is null
        ? "Attaque et capacité désactivées."
        : $"OnPhysicalAttacked · {profile.PhysicalAttack.Declared.Handler} / {profile.PhysicalAttack.Resolved.Handler} ; " +
          $"OnSpellCastAt · {profile.Ability.Targeted.Handler} / {profile.Ability.Impacted.Handler}.";

    private static string BlockedSummaryUx4P(CreatureEventAiCompositionProfile? profile) => profile is null
        ? "OnBlocked désactivé."
        : $"OnBlocked → {profile.Blocked.Blocked.Handler} · " +
          string.Join(" → ", profile.Blocked.RecoveryOrder);

    private static string ControllerSummaryUx4P(CreatureEventAiCompositionProfile? profile) => profile is null
        ? "Contrôleur IA par profil désactivé."
        : $"IA par profil · {profile.Controller.Kind} · {profile.Controller.DefinitionId} · " +
          $"{profile.Controller.Blackboard.Count} valeur(s) · {profile.Controller.AllowedActions.Count} action(s).";

    private static IReadOnlyList<string[]> ParseRowsUx4P(string text, int columns, string label) =>
        text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line =>
            {
                var parts = line.Split('|', StringSplitOptions.TrimEntries);
                if (parts.Length != columns || parts.Any(string.IsNullOrWhiteSpace))
                    throw new FormatException($"{label} attendu sur {columns} colonnes séparées par |.");
                return parts;
            }).ToArray();

    private static IReadOnlyList<string> SplitCsvUx4P(string text) => text
        .Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IReadOnlyList<T> ParseEnumCsvUx4P<T>(string text, string label) where T : struct, Enum =>
        SplitCsvUx4P(text).Select(value => Enum.TryParse<T>(value, true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed : throw new FormatException($"{label} invalide : {value}.")).ToArray();

    private static IReadOnlyDictionary<string, string> ParseMapUx4P(string text) => text
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(line =>
        {
            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace))
                throw new FormatException("Override attendu : clé=valeur.");
            return (parts[0], parts[1]);
        }).ToDictionary(item => item.Item1, item => item.Item2, StringComparer.Ordinal);

    private static string FingerprintCreatureEventAiCandidateUx4P(ProjectDefinition project,
        BlueprintId blueprintId, StudioCreatureEventAiCandidateUx4P candidate) =>
        FingerprintProjectUx4D(WithCreatureFoundationUx4D(project, blueprintId, candidate.Foundation));

    private static string FingerprintCreatureEventAiDraftUx4P(StudioCreatureEventAiDraftUx4P draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(draft.BaseRevision.Value);
        WriteUx4D(writer, draft.SourceProjectFingerprint);
        WriteUx4D(writer, draft.BlueprintId.ToString());
        writer.Write(draft.Enabled); writer.Write(draft.RequiredAtRuntime);
        WriteUx4D(writer, draft.AssetId);
        WriteUx4D(writer, draft.PerceptionDetectedHandler); WriteUx4D(writer, draft.PerceptionLostHandler);
        WriteUx4D(writer, draft.AttackDeclaredHandler); WriteUx4D(writer, draft.AttackResolvedHandler);
        WriteUx4D(writer, draft.AbilityTargetedHandler); WriteUx4D(writer, draft.AbilityImpactedHandler);
        WriteUx4D(writer, draft.BlockedHandler); WriteUx4D(writer, draft.RecoveryOrder);
        writer.Write((int)draft.ControllerKind); WriteUx4D(writer, draft.ControllerDefinitionId);
        WriteUx4D(writer, draft.Blackboard); WriteUx4D(writer, draft.AllowedActions);
        WriteUx4D(writer, draft.Overrides);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static bool DeleteUx4PSmokeRoot(string root)
    {
        try
        {
            var full = Path.GetFullPath(root);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) +
                       Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4p-smoke-",
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
