using System.Security.Cryptography;
using System.Text;
using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Creatures;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SetCreatureFoundationHandler _setCreatureFoundationUx4F = new(new SystemClock());
    private StudioCreatureRulesDraftUx4F? _approvedCreatureRulesDraftUx4F;
    private CreatureFoundationProfile? _approvedCreatureRulesCandidateUx4F;
    private ProjectRevision? _approvedCreatureRulesRevisionUx4F;
    private string? _approvedCreatureRulesProjectFingerprintUx4F;
    private string? _approvedCreatureRulesDraftFingerprintUx4F;
    private string? _approvedCreatureRulesCandidateFingerprintUx4F;

    internal IReadOnlyList<StudioCreatureRulesChoiceUx4F> CreatureRulesChoicesUx4F => Project?.Blueprints
        .Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
        .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
        .ThenBy(item => item.Slug, StringComparer.Ordinal)
        .Select(item => new StudioCreatureRulesChoiceUx4F(item.Id,
            $"{item.DisplayName} · {(item.Kind == BlueprintKind.PlayerSpawn ? "joueur" : "créature")} · " +
            (item.CreatureFoundation is null ? "fondation requise" :
                item.CreatureFoundation.AuthoringComposition is null ||
                item.CreatureFoundation.RulesComposition is null ? "compositions à initialiser" : "règles prêtes"),
            item.CreatureFoundation is not null,
            item.CreatureFoundation?.AuthoringComposition is not null,
            item.CreatureFoundation?.RulesComposition is not null,
            item.IsReadOnly()))
        .ToArray() ?? [];

    internal StudioCreatureRulesDraftUx4F? CreateCreatureRulesDraftUx4F(BlueprintId blueprintId)
    {
        var project = Project;
        var history = History;
        var blueprint = project?.Blueprints.SingleOrDefault(item => item.Id == blueprintId);
        var foundation = blueprint?.CreatureFoundation;
        if (project is null || history is null || blueprint is null || foundation is null || blueprint.IsReadOnly() ||
            blueprint.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn)) return null;

        var authoring = foundation.AuthoringComposition ?? CreatureAuthoringCompositionProfile.CreateDefault(
            StableCreatureRulesIdUx4F(blueprint.Id.Value, "authoring"), "studio:ux4-f");
        var rules = foundation.RulesComposition ?? CreatureRulesCompositionProfile.CreateDefault(
            StableCreatureRulesIdUx4F(blueprint.Id.Value, "rules"), "studio:ux4-f");
        return new(history.State.Revision, FingerprintProjectUx4F(project), blueprint.Id, blueprint, foundation,
            authoring.AnimationGraph.RetargetProfileId, authoring.AnimationGraph.States.ToArray(),
            authoring.AnimationGraph.Transitions.ToArray(), rules.InitialState.LifeState,
            rules.InitialState.AnimationLoopAssetId, rules.InitialState.PoseId, rules.InitialState.Invisible,
            foundation.Resources.ToArray(), rules.Attributes.RulesetId,
            rules.Attributes.BaseValues.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
            rules.Attributes.Modifiers.ToArray(), rules.Attributes.Derived.ToArray(), rules.Defenses.Avoidance,
            rules.Defenses.Armor,
            rules.Defenses.Resistances.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
            rules.Defenses.Saves.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
            rules.Defenses.Immunities.ToArray(), rules.Defenses.FlatMitigation);
    }

    internal StudioCreatureRulesPreviewUx4F PreviewCreatureRulesUx4F(StudioCreatureRulesDraftUx4F draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelCreatureRulesUx4F();
        var project = Project;
        var history = History;
        if (project is null || history is null)
            return FailedCreatureRulesPreviewUx4F("Aucun module n’est ouvert.", "studio.creature.project_missing");
        if (history.State.Revision != draft.BaseRevision ||
            !string.Equals(FingerprintProjectUx4F(project), draft.SourceProjectFingerprint, StringComparison.Ordinal))
            return FailedCreatureRulesPreviewUx4F(
                "Le projet a changé depuis l’ouverture de la fiche. Rechargez-la avant de prévisualiser.",
                "command.revision_conflict");
        if (!TryBuildCreatureRulesCandidateUx4F(draft, out var candidate, out var diagnostics))
            return new(false, false, "La configuration contient des valeurs invalides.", string.Empty,
                string.Empty, diagnostics, AnimationSummaryUx4F(draft), InitialSummaryUx4F(draft),
                ResourceSummaryUx4F(draft), AttributeSummaryUx4F(draft), DefenseSummaryUx4F(draft),
                "Runtime non évalué.");

        var command = new SetCreatureFoundationCommand(CommandId.From(Guid.NewGuid()), draft.BaseRevision,
            draft.BlueprintId, candidate, DryRun: true);
        var dryRun = _setCreatureFoundationUx4F.Handle(history.State, command);
        if (!dryRun.Succeeded)
            return new(false, false, "La commande canonique refuse cette configuration.", string.Empty,
                string.Empty, CreatureRulesDiagnosticsUx4F(dryRun.Diagnostics), AnimationSummaryUx4F(draft),
                InitialSummaryUx4F(draft), ResourceSummaryUx4F(draft), AttributeSummaryUx4F(draft),
                DefenseSummaryUx4F(draft), "Runtime non évalué.");

        var candidateProject = WithCreatureRulesFoundationUx4F(project, draft.BlueprintId, candidate);
        var authoringRuntime = CreatureAuthoringCompositionFoundationService.Resolve(candidateProject,
            draft.BlueprintId);
        var rulesRuntime = CreatureRulesCompositionFoundationService.Resolve(candidateProject, draft.BlueprintId);
        if (!authoringRuntime.Succeeded || authoringRuntime.Creature is null ||
            !rulesRuntime.Succeeded || rulesRuntime.Creature is null)
        {
            var runtimeDiagnostics = authoringRuntime.Diagnostics.Concat(rulesRuntime.Diagnostics)
                .Select(CreatureRulesDiagnosticUx4F).ToArray();
            return new(false, false, "La projection RPG Kit refuse la configuration.", string.Empty,
                string.Empty, runtimeDiagnostics, AnimationSummaryUx4F(draft), InitialSummaryUx4F(draft),
                ResourceSummaryUx4F(draft), AttributeSummaryUx4F(draft), DefenseSummaryUx4F(draft),
                "Projection runtime refusée.");
        }

        var evaluated = CreatureRulesCompositionValidator.EvaluateAttributes(candidate.RulesComposition!.Attributes);
        var damageType = candidate.RulesComposition.Defenses.Resistances.Keys.FirstOrDefault() ?? "physical";
        var mitigated = CreatureRulesCompositionValidator.Mitigate(candidate.RulesComposition.Defenses,
            damageType, 100);
        var frozen = FreezeCreatureRulesDraftUx4F(draft);
        _approvedCreatureRulesDraftUx4F = frozen;
        _approvedCreatureRulesCandidateUx4F = candidate;
        _approvedCreatureRulesRevisionUx4F = history.State.Revision;
        _approvedCreatureRulesProjectFingerprintUx4F = FingerprintProjectUx4F(project);
        _approvedCreatureRulesDraftFingerprintUx4F = FingerprintCreatureRulesDraftUx4F(frozen);
        _approvedCreatureRulesCandidateFingerprintUx4F =
            FingerprintCreatureRulesCandidateUx4F(project, draft.BlueprintId, candidate);
        return new(true, true, "Règles de créature valides · prêtes à être appliquées.",
            dryRun.Patch?.Summary ?? "Remplacer la fondation de créature.",
            dryRun.Inverse?.Summary ?? "Restaurer la fondation précédente.", [],
            AnimationSummaryUx4F(draft), InitialSummaryUx4F(draft), ResourceSummaryUx4F(draft),
            AttributeSummaryUx4F(draft), DefenseSummaryUx4F(draft),
            $"Projection RPG Kit · {evaluated.Count} attribut(s) résolu(s) · 100 {damageType} → {mitigated:0.###} · " +
            "aucune animation ni attaque exécutée");
    }

    internal StudioCreatureRulesActionResultUx4F ApplyApprovedCreatureRulesUx4F(
        StudioCreatureRulesDraftUx4F currentDraft)
    {
        ArgumentNullException.ThrowIfNull(currentDraft);
        var project = Project;
        var history = History;
        var approved = _approvedCreatureRulesDraftUx4F;
        var candidate = _approvedCreatureRulesCandidateUx4F;
        if (project is null || history is null || approved is null || candidate is null ||
            _approvedCreatureRulesRevisionUx4F is null || _approvedCreatureRulesProjectFingerprintUx4F is null ||
            _approvedCreatureRulesDraftFingerprintUx4F is null ||
            _approvedCreatureRulesCandidateFingerprintUx4F is null)
            return new(false, "Prévisualisez les règles avant de les appliquer.");

        var stale = history.State.Revision != _approvedCreatureRulesRevisionUx4F ||
                    !string.Equals(FingerprintProjectUx4F(project),
                        _approvedCreatureRulesProjectFingerprintUx4F, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureRulesDraftUx4F(approved),
                        _approvedCreatureRulesDraftFingerprintUx4F, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureRulesDraftUx4F(currentDraft),
                        _approvedCreatureRulesDraftFingerprintUx4F, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureRulesCandidateUx4F(project, currentDraft.BlueprintId,
                            candidate), _approvedCreatureRulesCandidateFingerprintUx4F, StringComparison.Ordinal);
        if (stale)
        {
            CancelCreatureRulesUx4F();
            return new(false, "Le projet ou la fiche a changé depuis l’aperçu. Prévisualisez à nouveau.");
        }

        var recheck = _setCreatureFoundationUx4F.Handle(history.State, new(CommandId.From(Guid.NewGuid()),
            history.State.Revision, currentDraft.BlueprintId, candidate, DryRun: true));
        if (!recheck.Succeeded)
        {
            CancelCreatureRulesUx4F();
            return new(false, "La revalidation canonique a refusé les règles.");
        }
        var result = _setCreatureFoundationUx4F.Handle(history.State, new(CommandId.From(Guid.NewGuid()),
            history.State.Revision, currentDraft.BlueprintId, candidate, DryRun: false));
        CancelCreatureRulesUx4F();
        if (!result.Succeeded) return new(false, "La commande n’a pas pu appliquer les règles.");
        history.Record(result);
        InvalidateContentIndexes();
        return new(true, "Règles appliquées · Ctrl+Z pour annuler.");
    }

    internal StudioCreatureRulesActionResultUx4F UndoCreatureRulesUx4F()
    {
        CancelCreatureRulesUx4F();
        var result = Undo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioCreatureRulesActionResultUx4F RedoCreatureRulesUx4F()
    {
        CancelCreatureRulesUx4F();
        var result = Redo();
        return new(result.Succeeded, result.Message);
    }

    internal void CancelCreatureRulesUx4F()
    {
        _approvedCreatureRulesDraftUx4F = null;
        _approvedCreatureRulesCandidateUx4F = null;
        _approvedCreatureRulesRevisionUx4F = null;
        _approvedCreatureRulesProjectFingerprintUx4F = null;
        _approvedCreatureRulesDraftFingerprintUx4F = null;
        _approvedCreatureRulesCandidateFingerprintUx4F = null;
    }

    internal void CloseUx4F() => CancelCreatureRulesUx4F();

    internal StudioCreatureRulesApprovalSnapshotUx4F CaptureCreatureRulesApprovalUx4F() => new(
        _approvedCreatureRulesDraftUx4F, _approvedCreatureRulesCandidateUx4F,
        _approvedCreatureRulesRevisionUx4F, _approvedCreatureRulesProjectFingerprintUx4F,
        _approvedCreatureRulesDraftFingerprintUx4F, _approvedCreatureRulesCandidateFingerprintUx4F);

    internal void RestoreCreatureRulesApprovalUx4F(StudioCreatureRulesApprovalSnapshotUx4F snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedCreatureRulesDraftUx4F = snapshot.Draft;
        _approvedCreatureRulesCandidateUx4F = snapshot.Candidate;
        _approvedCreatureRulesRevisionUx4F = snapshot.Revision;
        _approvedCreatureRulesProjectFingerprintUx4F = snapshot.ProjectFingerprint;
        _approvedCreatureRulesDraftFingerprintUx4F = snapshot.DraftFingerprint;
        _approvedCreatureRulesCandidateFingerprintUx4F = snapshot.CandidateFingerprint;
    }

    internal StudioCreatureRulesEvidenceProjectUx4F? CreateUx4FEvidenceProject()
    {
        var project = Project;
        if (project is null) return null;
        var real = project.Blueprints.Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn &&
                                                   !item.IsReadOnly() &&
                                                   item.CreatureFoundation?.AuthoringComposition is not null &&
                                                   item.CreatureFoundation.RulesComposition is not null)
            .FirstOrDefault(item => CreatureFoundationValidator.Validate(item.CreatureFoundation!).Count == 0);
        if (real is not null) return new(project, real.Id, true, false);

        var target = project.Blueprints.Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn &&
                                                     !item.IsReadOnly())
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase).FirstOrDefault();
        if (target is null) return null;
        var foundation = CreateCanonicalCreatureRulesFoundationUx4F(target);
        return new(WithCreatureRulesFoundationUx4F(project, target.Id, foundation), target.Id, false, true);
    }

    internal StudioUx4FSmokeResult RunUx4FContractSmoke()
    {
        var result = new StudioUx4FSmokeResult(false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false);
        var evidence = CreateUx4FEvidenceProject();
        if (evidence is null) return result;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4f-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(root, evidence.Project).Succeeded || !editing.Open(root).Succeeded)
                return result;
            var explicitSelection = editing.CreatureRulesChoicesUx4F.Count > 0;
            var draft = editing.CreateCreatureRulesDraftUx4F(evidence.BlueprintId);
            if (draft is null) return result with { ExplicitSelection = explicitSelection };
            var changed = ChangedCreatureRulesDraftUx4F(draft);
            var sourceProject = editing.Project!;
            var sourceHistory = editing.History!;
            var preview = editing.PreviewCreatureRulesUx4F(changed);
            var dryRun = preview.Succeeded && ReferenceEquals(sourceProject, editing.Project) &&
                         ReferenceEquals(sourceHistory, editing.History) &&
                         sourceHistory.State.Revision == draft.BaseRevision;
            var invalidResource = changed.Resources.ToArray();
            invalidResource[0] = invalidResource[0] with { Maximum = -1 };
            var invalid = editing.PreviewCreatureRulesUx4F(changed with { Resources = invalidResource });
            var invalidBlocked = !invalid.Succeeded && invalid.Diagnostics.Any(item =>
                item.Code is "creature.foundation.resource_invalid" or "creature.authoring.animation_graph_invalid" or
                    "creature.rules.defenses_invalid");

            var approvedForDraft = editing.PreviewCreatureRulesUx4F(changed);
            var intermediate = ToggleCreatureRulesResourceUx4F(editing, evidence.BlueprintId);
            var staleDraft = editing.PreviewCreatureRulesUx4F(changed);
            var staleDraftBlocked = approvedForDraft.Succeeded && intermediate && !staleDraft.Succeeded;

            var fresh = editing.CreateCreatureRulesDraftUx4F(evidence.BlueprintId)!;
            var freshChanged = ChangedCreatureRulesDraftUx4F(fresh);
            var approved = editing.PreviewCreatureRulesUx4F(freshChanged);
            var secondIntermediate = ToggleCreatureRulesResourceUx4F(editing, evidence.BlueprintId);
            var staleApproval = editing.ApplyApprovedCreatureRulesUx4F(freshChanged);
            var staleApprovalBlocked = approved.Succeeded && secondIntermediate && !staleApproval.Succeeded;

            fresh = editing.CreateCreatureRulesDraftUx4F(evidence.BlueprintId)!;
            freshChanged = ChangedCreatureRulesDraftUx4F(fresh);
            var expectedBuilt = editing.TryBuildCreatureRulesCandidateUx4F(freshChanged, out var expected, out _);
            var finalPreview = editing.PreviewCreatureRulesUx4F(freshChanged);
            var beforeApply = editing.Project!;
            var beforeBlueprint = beforeApply.Blueprints.Single(item => item.Id == evidence.BlueprintId);
            var apply = editing.ApplyApprovedCreatureRulesUx4F(freshChanged);
            var appliedProject = editing.Project!;
            var applied = appliedProject.Blueprints.Single(item => item.Id == evidence.BlueprintId)
                .CreatureFoundation;
            var graph = applied?.AuthoringComposition?.AnimationGraph;
            var rules = applied?.RulesComposition;
            var animation = graph is { RetargetProfileId: "retarget.proof" } &&
                            graph.States.Any(item => item.StateId == "victory" && item.Category == "emote");
            var initial = rules?.InitialState is { LifeState: CreatureRulesInitialLifeState.Sleeping,
                PoseId: "pose.proof.sleeping", Invisible: true };
            var resources = applied?.Resources.Any(item => item.ResourceId == "mana" && item.Maximum == 80) == true;
            var attributes = rules?.Attributes.BaseValues.ContainsKey("strength") == true &&
                             rules.Attributes.Derived.Any(item => item.AttributeId == "power");
            var defenses = rules?.Defenses.Resistances.GetValueOrDefault("fire") == 0.35 &&
                           rules.Defenses.Saves.GetValueOrDefault("will") == 7 &&
                           rules.Defenses.Immunities.Contains("sleep", StringComparer.Ordinal);
            var neighbors = expectedBuilt && applied is not null &&
                            ReferenceEquals(beforeBlueprint.CreatureFoundation?.Movement, applied.Movement) &&
                            Equals(beforeBlueprint.CreatureFoundation?.References, applied.References) &&
                            ReferenceEquals(beforeBlueprint.CreatureFoundation?.InteractionComposition,
                                applied.InteractionComposition) &&
                            ReferenceEquals(beforeBlueprint.CreatureFoundation?.EventAiComposition,
                                applied.EventAiComposition) &&
                            ProjectJsonSerializer.Serialize(WithCreatureRulesFoundationUx4F(
                                beforeApply, evidence.BlueprintId, expected)) ==
                            ProjectJsonSerializer.Serialize(appliedProject);
            var canonical = finalPreview.Succeeded && apply.Succeeded && applied is not null;
            var undo = editing.UndoCreatureRulesUx4F();
            var undone = undo.Succeeded && ReferenceEquals(beforeApply, editing.Project);
            var redo = editing.RedoCreatureRulesUx4F();
            var redone = redo.Succeeded && ReferenceEquals(appliedProject, editing.Project);
            var saved = redone && editing.Save().Succeeded;
            var persisted = saved && reopened.Open(root).Succeeded &&
                            ProjectJsonSerializer.Serialize(reopened.Project!) ==
                            ProjectJsonSerializer.Serialize(appliedProject);
            result = new(canonical && explicitSelection && animation && initial && resources && attributes &&
                         defenses && invalidBlocked && dryRun && staleDraftBlocked && staleApprovalBlocked &&
                         undone && redone && persisted && neighbors, explicitSelection, animation, initial, resources,
                attributes, defenses, invalidBlocked, dryRun, staleDraftBlocked, staleApprovalBlocked, canonical,
                undone && redone, persisted, neighbors, false);
        }
        catch
        {
        }
        finally
        {
            reopened.Close();
            editing.Close();
            var released = DeleteUx4FSmokeRoot(root);
            result = result with { Succeeded = result.Succeeded && released, TemporaryWorkspaceReleased = released };
        }
        return result;
    }

    private bool TryBuildCreatureRulesCandidateUx4F(StudioCreatureRulesDraftUx4F draft,
        out CreatureFoundationProfile candidate,
        out IReadOnlyList<StudioCreatureRulesDiagnosticUx4F> diagnostics)
    {
        var source = draft.SourceFoundation;
        var authoringSource = source.AuthoringComposition ?? CreatureAuthoringCompositionProfile.CreateDefault(
            StableCreatureRulesIdUx4F(draft.BlueprintId.Value, "authoring"), "studio:ux4-f");
        var rulesSource = source.RulesComposition ?? CreatureRulesCompositionProfile.CreateDefault(
            StableCreatureRulesIdUx4F(draft.BlueprintId.Value, "rules"), "studio:ux4-f");
        var graph = new CreatureAnimationGraphProfile(authoringSource.AnimationGraph.Id,
            authoringSource.AnimationGraph.SchemaVersion, authoringSource.AnimationGraph.Provenance,
            draft.AnimationStates, draft.AnimationTransitions, draft.RetargetProfileId);
        var authoring = new CreatureAuthoringCompositionProfile(authoringSource.Id, authoringSource.SchemaVersion,
            authoringSource.Provenance, authoringSource.AssetId, authoringSource.PreviewTransform,
            authoringSource.ArchetypeId, authoringSource.RulesetId, authoringSource.RigId,
            authoringSource.FactionId, authoringSource.EncounterRole, authoringSource.ThreatRating,
            authoringSource.VisualKitId, authoringSource.CombatTags, authoringSource.Species,
            authoringSource.Appearance, graph, authoringSource.Overrides);
        var initial = new CreatureRulesInitialState(rulesSource.InitialState.Id, draft.InitialLifeState,
            draft.InitialAnimationLoopAssetId, draft.InitialPoseId, draft.InitiallyInvisible);
        var attributes = new CreatureRulesAttributeSet(rulesSource.Attributes.Id, draft.RulesetId,
            draft.BaseAttributes, draft.AttributeModifiers, draft.DerivedAttributes);
        var defenses = new CreatureRulesDefenseProfile(rulesSource.Defenses.Id, draft.Avoidance, draft.Armor,
            draft.Resistances, draft.Saves, draft.Immunities, draft.FlatMitigation);
        var rules = new CreatureRulesCompositionProfile(rulesSource.Id, rulesSource.SchemaVersion,
            rulesSource.Provenance, rulesSource.AssetId, rulesSource.PreviewTransform, initial, attributes, defenses,
            rulesSource.Abilities, rulesSource.Inventory, rulesSource.Overrides);
        candidate = new CreatureFoundationProfile(source.Id, source.SchemaVersion, source.Provenance,
            source.RequiredAtRuntime, source.Identity, source.DefaultTransform, draft.Resources, source.Movement,
            source.References, source.Spawned, source.Overrides, authoring,
            source.AuthoringCompositionRequiredAtRuntime, rules, source.RulesCompositionRequiredAtRuntime,
            source.InteractionComposition, source.InteractionCompositionRequiredAtRuntime,
            source.EventAiComposition, source.EventAiCompositionRequiredAtRuntime);
        diagnostics = CreatureFoundationValidator.Validate(candidate,
                $"blueprints/{draft.BlueprintId}/creatureFoundation")
            .Select(CreatureRulesDiagnosticUx4F).GroupBy(item => (item.Code, item.Path))
            .Select(item => item.First()).ToArray();
        return diagnostics.Count == 0;
    }

    private static StudioCreatureRulesDraftUx4F ChangedCreatureRulesDraftUx4F(
        StudioCreatureRulesDraftUx4F draft)
    {
        var states = draft.AnimationStates.Where(item => item.StateId != "victory")
            .Concat([new CreatureAnimationState(StableCreatureRulesIdUx4F(draft.BlueprintId.Value, "state-victory"),
                "victory", "emote", "clip.proof.victory")]).ToArray();
        var transitions = draft.AnimationTransitions
            .Concat([new CreatureAnimationTransition(
                StableCreatureRulesIdUx4F(draft.BlueprintId.Value, "transition-victory"), "guard", "victory",
                "combat.won")]).DistinctBy(item => item.Id).ToArray();
        var resources = draft.Resources.Where(item => item.ResourceId != "mana")
            .Concat([new CreatureFoundationResourcePool(
                StableCreatureRulesIdUx4F(draft.BlueprintId.Value, "resource-mana"), "mana", 80, 32, 1.5, false)])
            .ToArray();
        return draft with
        {
            RetargetProfileId = "retarget.proof",
            AnimationStates = states,
            AnimationTransitions = transitions,
            InitialLifeState = CreatureRulesInitialLifeState.Sleeping,
            InitialAnimationLoopAssetId = "clip.proof.sleeping",
            InitialPoseId = "pose.proof.sleeping",
            InitiallyInvisible = true,
            Resources = resources,
            RulesetId = "ruleset.proof",
            BaseAttributes = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["agility"] = 7,
                ["strength"] = 9,
                ["vitality"] = 11,
            },
            AttributeModifiers =
            [
                new(StableCreatureRulesIdUx4F(draft.BlueprintId.Value, "modifier-strength"), "strength",
                    "species.proof", 2),
            ],
            DerivedAttributes =
            [
                new(StableCreatureRulesIdUx4F(draft.BlueprintId.Value, "derived-power"), "power",
                    CreatureRulesAttributeFormulaOperation.Sum, ["strength", "vitality"], 1),
            ],
            Avoidance = 5,
            Armor = 3,
            Resistances = new Dictionary<string, double>(StringComparer.Ordinal)
                { ["fire"] = 0.35, ["cold"] = 0.1 },
            Saves = new Dictionary<string, double>(StringComparer.Ordinal)
                { ["fortitude"] = 6, ["reflex"] = 5, ["will"] = 7 },
            Immunities = ["sleep"],
            FlatMitigation = 2,
        };
    }

    private static CreatureFoundationProfile CreateCanonicalCreatureRulesFoundationUx4F(
        BlueprintDefinition blueprint)
    {
        var foundation = CreatureFoundationProfile.CreateDefault(
            StableCreatureRulesIdUx4F(blueprint.Id.Value, "foundation"), blueprint.Slug,
            blueprint.DisplayName, "studio:ux4-f-evidence");
        var authoring = CreatureAuthoringCompositionProfile.CreateDefault(
            StableCreatureRulesIdUx4F(blueprint.Id.Value, "authoring"), "studio:ux4-f-evidence");
        var rules = CreatureRulesCompositionProfile.CreateDefault(
            StableCreatureRulesIdUx4F(blueprint.Id.Value, "rules"), "studio:ux4-f-evidence");
        return new(foundation.Id, foundation.SchemaVersion, foundation.Provenance, true, foundation.Identity,
            foundation.DefaultTransform, foundation.Resources, foundation.Movement, foundation.References,
            foundation.Spawned, foundation.Overrides, authoring, true, rules, true,
            foundation.InteractionComposition, foundation.InteractionCompositionRequiredAtRuntime,
            foundation.EventAiComposition, foundation.EventAiCompositionRequiredAtRuntime);
    }

    private static ProjectDefinition WithCreatureRulesFoundationUx4F(ProjectDefinition project,
        BlueprintId blueprintId, CreatureFoundationProfile foundation)
    {
        var blueprints = project.Blueprints.Select(item =>
            item.Id == blueprintId ? item.WithCreatureFoundation(foundation) : item).ToArray();
        return new(project.Id, project.Title, project.Module, blueprints, project.Areas, project.Dialogues,
            project.Quests);
    }

    private static bool ToggleCreatureRulesResourceUx4F(StudioSessionController session, BlueprintId blueprintId)
    {
        var project = session.Project;
        var history = session.History;
        var source = project?.Blueprints.SingleOrDefault(item => item.Id == blueprintId)?.CreatureFoundation;
        if (project is null || history is null || source is null || source.Resources.Count == 0) return false;
        var resources = source.Resources.Select((item, index) => index == 0
            ? item with { Persistent = !item.Persistent }
            : item).ToArray();
        var changed = new CreatureFoundationProfile(source.Id, source.SchemaVersion, source.Provenance,
            source.RequiredAtRuntime, source.Identity, source.DefaultTransform, resources, source.Movement,
            source.References, source.Spawned, source.Overrides, source.AuthoringComposition,
            source.AuthoringCompositionRequiredAtRuntime, source.RulesComposition,
            source.RulesCompositionRequiredAtRuntime, source.InteractionComposition,
            source.InteractionCompositionRequiredAtRuntime, source.EventAiComposition,
            source.EventAiCompositionRequiredAtRuntime);
        var handler = new SetCreatureFoundationHandler(new SystemClock());
        var result = handler.Handle(history.State, new(CommandId.From(Guid.NewGuid()), history.State.Revision,
            blueprintId, changed, DryRun: false));
        if (!result.Succeeded) return false;
        history.Record(result);
        session.InvalidateContentIndexes();
        return true;
    }

    private static StudioCreatureRulesPreviewUx4F FailedCreatureRulesPreviewUx4F(string message, string code) =>
        new(false, false, message, string.Empty, string.Empty,
            [new(code, "creature/rules", message)], "Animation non évaluée.", "État non évalué.",
            "Ressources non évaluées.", "Attributs non évalués.", "Défenses non évaluées.",
            "Runtime non évalué.");

    private static IReadOnlyList<StudioCreatureRulesDiagnosticUx4F> CreatureRulesDiagnosticsUx4F(
        IReadOnlyList<CommandDiagnostic> diagnostics) => diagnostics.Select(item =>
    {
        var separator = item.Message.IndexOf(": ", StringComparison.Ordinal);
        var path = separator > 0 ? item.Message[..separator] : "creature/rules";
        var message = separator > 0 ? item.Message[(separator + 2)..] : item.Message;
        return new StudioCreatureRulesDiagnosticUx4F(item.Code, path, HumanCreatureRulesMessageUx4F(item.Code,
            message));
    }).ToArray();

    private static StudioCreatureRulesDiagnosticUx4F CreatureRulesDiagnosticUx4F(
        CreatureFoundationDiagnostic item) => new(item.Code, item.Path,
        HumanCreatureRulesMessageUx4F(item.Code, item.Message));

    private static string HumanCreatureRulesMessageUx4F(string code, string fallback) => code switch
    {
        "creature.authoring.animation_graph_invalid" =>
            "Le graphe exige des états locomotion, combat, emote et additive, des IDs stables et des transitions résolues.",
        "creature.rules.initial_state_invalid" =>
            "L’état initial, la boucle et la pose doivent être compatibles ; un état mort exige une pose.",
        "creature.rules.attributes_invalid" =>
            "Les attributs exigent des bases finies, des modificateurs sourcés et des formules acycliques.",
        "creature.foundation.resource_invalid" =>
            "Chaque ressource exige un identifiant, un maximum positif, un courant borné et une régénération positive ou nulle.",
        "creature.rules.defenses_invalid" =>
            "Les défenses exigent des valeurs finies, des résistances 0..1 et des sauvegardes bornées.",
        _ => fallback,
    };

    private static string AnimationSummaryUx4F(StudioCreatureRulesDraftUx4F draft) =>
        $"{draft.AnimationStates.Count} état(s) · {draft.AnimationTransitions.Count} transition(s) · {draft.RetargetProfileId}";

    private static string InitialSummaryUx4F(StudioCreatureRulesDraftUx4F draft) =>
        $"{draft.InitialLifeState} · pose {draft.InitialPoseId ?? "aucune"} · invisible {draft.InitiallyInvisible.ToString().ToLowerInvariant()}";

    private static string ResourceSummaryUx4F(StudioCreatureRulesDraftUx4F draft) =>
        $"{draft.Resources.Count} pool(s) · {string.Join(", ", draft.Resources.Select(item => $"{item.ResourceId} {item.Current:0.##}/{item.Maximum:0.##}"))}";

    private static string AttributeSummaryUx4F(StudioCreatureRulesDraftUx4F draft) =>
        $"{draft.RulesetId} · {draft.BaseAttributes.Count} base(s) · {draft.AttributeModifiers.Count} modificateur(s) · {draft.DerivedAttributes.Count} dérivé(s)";

    private static string DefenseSummaryUx4F(StudioCreatureRulesDraftUx4F draft) =>
        $"évitement {draft.Avoidance:0.##} · armure {draft.Armor:0.##} · {draft.Resistances.Count} résistance(s) · {draft.Saves.Count} sauvegarde(s) · {draft.Immunities.Count} immunité(s)";

    private static StudioCreatureRulesDraftUx4F FreezeCreatureRulesDraftUx4F(StudioCreatureRulesDraftUx4F draft) =>
        draft with
        {
            AnimationStates = draft.AnimationStates.ToArray(),
            AnimationTransitions = draft.AnimationTransitions.ToArray(),
            Resources = draft.Resources.ToArray(),
            BaseAttributes = draft.BaseAttributes.ToDictionary(item => item.Key, item => item.Value,
                StringComparer.Ordinal),
            AttributeModifiers = draft.AttributeModifiers.ToArray(),
            DerivedAttributes = draft.DerivedAttributes.Select(item => item with
                { Dependencies = item.Dependencies.ToArray() }).ToArray(),
            Resistances = draft.Resistances.ToDictionary(item => item.Key, item => item.Value,
                StringComparer.Ordinal),
            Saves = draft.Saves.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
            Immunities = draft.Immunities.ToArray(),
        };

    private static string FingerprintProjectUx4F(ProjectDefinition project) =>
        HashUx4F(ProjectJsonSerializer.Serialize(project));

    private static string FingerprintCreatureRulesCandidateUx4F(ProjectDefinition project, BlueprintId blueprintId,
        CreatureFoundationProfile candidate) =>
        FingerprintProjectUx4F(WithCreatureRulesFoundationUx4F(project, blueprintId, candidate));

    private static string FingerprintCreatureRulesDraftUx4F(StudioCreatureRulesDraftUx4F draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(draft.BaseRevision.Value);
        WriteUx4F(writer, draft.SourceProjectFingerprint);
        WriteUx4F(writer, draft.BlueprintId.ToString());
        WriteUx4F(writer, draft.RetargetProfileId);
        writer.Write(draft.AnimationStates.Count);
        foreach (var item in draft.AnimationStates)
        {
            writer.Write(item.Id.ToByteArray()); WriteUx4F(writer, item.StateId); WriteUx4F(writer, item.Category);
            WriteUx4F(writer, item.ClipAssetId);
        }
        writer.Write(draft.AnimationTransitions.Count);
        foreach (var item in draft.AnimationTransitions)
        {
            writer.Write(item.Id.ToByteArray()); WriteUx4F(writer, item.FromStateId); WriteUx4F(writer, item.ToStateId);
            WriteUx4F(writer, item.ConditionId);
        }
        writer.Write((int)draft.InitialLifeState); WriteUx4F(writer, draft.InitialAnimationLoopAssetId);
        WriteUx4F(writer, draft.InitialPoseId); writer.Write(draft.InitiallyInvisible);
        writer.Write(draft.Resources.Count);
        foreach (var item in draft.Resources)
        {
            writer.Write(item.Id.ToByteArray()); WriteUx4F(writer, item.ResourceId); writer.Write(item.Maximum);
            writer.Write(item.Current); writer.Write(item.RegenerationPerSecond); writer.Write(item.Persistent);
        }
        WriteUx4F(writer, draft.RulesetId);
        WriteMapUx4F(writer, draft.BaseAttributes);
        writer.Write(draft.AttributeModifiers.Count);
        foreach (var item in draft.AttributeModifiers)
        {
            writer.Write(item.Id.ToByteArray()); WriteUx4F(writer, item.AttributeId); WriteUx4F(writer, item.SourceId);
            writer.Write(item.Value);
        }
        writer.Write(draft.DerivedAttributes.Count);
        foreach (var item in draft.DerivedAttributes)
        {
            writer.Write(item.Id.ToByteArray()); WriteUx4F(writer, item.AttributeId); writer.Write((int)item.Operation);
            writer.Write(item.Dependencies.Count); foreach (var dependency in item.Dependencies) WriteUx4F(writer, dependency);
            writer.Write(item.Constant);
        }
        writer.Write(draft.Avoidance); writer.Write(draft.Armor); writer.Write(draft.FlatMitigation);
        WriteMapUx4F(writer, draft.Resistances); WriteMapUx4F(writer, draft.Saves);
        writer.Write(draft.Immunities.Count); foreach (var immunity in draft.Immunities) WriteUx4F(writer, immunity);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteMapUx4F(BinaryWriter writer, IReadOnlyDictionary<string, double> values)
    {
        writer.Write(values.Count);
        foreach (var item in values.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            WriteUx4F(writer, item.Key); writer.Write(item.Value);
        }
    }

    private static void WriteUx4F(BinaryWriter writer, string? value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length); writer.Write(bytes);
    }

    private static string HashUx4F(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    internal static Guid StableCreatureRulesIdUx4F(Guid basis, string segment)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{basis:D}:ux4-f:{segment}"));
        Span<byte> id = stackalloc byte[16]; bytes.AsSpan(0, 16).CopyTo(id);
        id[7] = (byte)((id[7] & 0x0F) | 0x40); id[8] = (byte)((id[8] & 0x3F) | 0x80);
        return new Guid(id);
    }

    private static bool DeleteUx4FSmokeRoot(string root)
    {
        try
        {
            var full = Path.GetFullPath(root);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) +
                       Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4f-smoke-", StringComparison.Ordinal))
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
