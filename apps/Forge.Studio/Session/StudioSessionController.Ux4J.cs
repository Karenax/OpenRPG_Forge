using System.Security.Cryptography;
using System.Text;
using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Creatures;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Dialogues;
using Forge.Domain.Factions;
using Forge.Domain.Lifecycle;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Creatures;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SetCreatureFoundationHandler _setCreatureInteractionUx4J = new(new SystemClock());
    private StudioCreatureInteractionDraftUx4J? _approvedCreatureInteractionDraftUx4J;
    private CreatureFoundationProfile? _approvedCreatureInteractionCandidateUx4J;
    private ProjectRevision? _approvedCreatureInteractionRevisionUx4J;
    private string? _approvedCreatureInteractionProjectFingerprintUx4J;
    private string? _approvedCreatureInteractionDraftFingerprintUx4J;
    private string? _approvedCreatureInteractionCandidateFingerprintUx4J;

    internal IReadOnlyList<StudioCreatureInteractionChoiceUx4J> CreatureInteractionChoicesUx4J =>
        Project?.Blueprints.Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => new StudioCreatureInteractionChoiceUx4J(item.Id,
                $"{item.DisplayName} · {item.Kind.ToString().ToLowerInvariant()} · " +
                $"{item.CreatureFoundation?.InteractionComposition?.Factions.Memberships.Count ?? 0} faction(s)",
                item.CreatureFoundation?.InteractionComposition is not null, item.IsReadOnly())).ToArray() ?? [];

    internal IReadOnlyList<StudioCreatureInteractionFactionChoiceUx4J> CreatureInteractionFactionChoicesUx4J =>
        Project?.Module.Factions.Factions.Where(item => !item.Archived)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => new StudioCreatureInteractionFactionChoiceUx4J(item.Id,
                $"{item.DisplayName} · {item.Slug}")).ToArray() ?? [];

    internal IReadOnlyList<StudioCreatureInteractionDialogueChoiceUx4J> CreatureInteractionDialogueChoicesUx4J =>
        Project?.Dialogues.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => new StudioCreatureInteractionDialogueChoiceUx4J(item.Id,
                $"{item.DisplayName} · {item.Slug}")).ToArray() ?? [];

    internal StudioCreatureInteractionDraftUx4J? CreateCreatureInteractionDraftUx4J(BlueprintId blueprintId)
    {
        var project = Project; var history = History;
        var blueprint = project?.Blueprints.SingleOrDefault(item => item.Id == blueprintId);
        if (project is null || history is null || blueprint?.CreatureFoundation is null || blueprint.IsReadOnly() ||
            blueprint.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn)) return null;
        var source = blueprint.CreatureFoundation;
        var interaction = source.InteractionComposition ?? CreateDefaultCreatureInteractionUx4J(project, blueprint);
        if (interaction is null) return null;
        return new(history.State.Revision, FingerprintProjectUx4J(project), blueprint.Id, blueprint, source, interaction,
            source.InteractionCompositionRequiredAtRuntime, interaction.Factions, interaction.Perception,
            interaction.DialogueBindings, interaction.OnConversation.Handler, interaction.Protection);
    }

    internal StudioCreatureInteractionPreviewUx4J PreviewCreatureInteractionUx4J(
        StudioCreatureInteractionDraftUx4J draft)
    {
        ArgumentNullException.ThrowIfNull(draft); CancelCreatureInteractionUx4J();
        var project = Project; var history = History;
        if (project is null || history is null)
            return FailedCreatureInteractionUx4J("Aucun module n’est ouvert.", "studio.creature.project_missing");
        if (history.State.Revision != draft.BaseRevision ||
            !string.Equals(FingerprintProjectUx4J(project), draft.SourceProjectFingerprint, StringComparison.Ordinal))
            return FailedCreatureInteractionUx4J("Le projet a changé ; reconstruisez l’aperçu.",
                "command.revision_conflict");
        var source = project.Blueprints.SingleOrDefault(item => item.Id == draft.BlueprintId);
        if (source?.CreatureFoundation is null)
            return FailedCreatureInteractionUx4J("La fondation de la créature n’existe plus.",
                "creature.foundation.blueprint_missing");
        var profile = CreateCreatureInteractionCandidateUx4J(draft);
        var foundation = WithCreatureInteractionUx4J(source.CreatureFoundation, profile, draft.RequiredAtRuntime);
        var validation = CreatureInteractionCompositionValidator.Validate(profile,
            $"blueprints/{source.Id}/creatureFoundation/interactionComposition");
        if (validation.Count > 0)
            return new(false, false, "La composition d’interaction est invalide.", string.Empty, string.Empty,
                validation.Select(CreatureInteractionDiagnosticUx4J).ToArray(), FactionSummaryUx4J(draft),
                PerceptionSummaryUx4J(draft), DialogueSummaryUx4J(draft), ProtectionSummaryUx4J(draft),
                "Runtime non évalué.");
        var dryRun = _setCreatureInteractionUx4J.Handle(history.State, new(CommandId.From(Guid.NewGuid()),
            history.State.Revision, draft.BlueprintId, foundation, DryRun: true));
        if (!dryRun.Succeeded)
            return new(false, false, "La commande canonique refuse cet aperçu.", string.Empty, string.Empty,
                CreatureInteractionDiagnosticsUx4J(dryRun.Diagnostics), FactionSummaryUx4J(draft),
                PerceptionSummaryUx4J(draft), DialogueSummaryUx4J(draft), ProtectionSummaryUx4J(draft),
                "Runtime non évalué.");
        var candidateProject = WithCreatureInteractionFoundationUx4J(project, draft.BlueprintId, foundation);
        var runtime = CreatureInteractionCompositionFoundationService.Resolve(candidateProject, draft.BlueprintId);
        var script = CreatureInteractionCompositionScriptService.Read(candidateProject,
            CreatureInteractionCompositionScriptService.CreateHandle(draft.BlueprintId));
        var runtimeCreature = runtime.Creature;
        if (!runtime.Succeeded || runtimeCreature is null || script is null)
            return new(false, false, "La projection typée refuse cette composition.", string.Empty, string.Empty,
                runtime.Diagnostics.Select(CreatureInteractionDiagnosticUx4J).ToArray(), FactionSummaryUx4J(draft),
                PerceptionSummaryUx4J(draft), DialogueSummaryUx4J(draft), ProtectionSummaryUx4J(draft),
                "Runtime non résolu.");
        var request = profile.DialogueBindings.FirstOrDefault() is { } binding
            ? CreatureInteractionCompositionValidator.RequestDialogue(profile, binding.Context,
                binding.InterlocutorId, binding.RequiredState)
            : null;
        var frozen = FreezeCreatureInteractionDraftUx4J(draft);
        _approvedCreatureInteractionDraftUx4J = frozen;
        _approvedCreatureInteractionCandidateUx4J = foundation;
        _approvedCreatureInteractionRevisionUx4J = history.State.Revision;
        _approvedCreatureInteractionProjectFingerprintUx4J = FingerprintProjectUx4J(project);
        _approvedCreatureInteractionDraftFingerprintUx4J = FingerprintCreatureInteractionDraftUx4J(frozen);
        _approvedCreatureInteractionCandidateFingerprintUx4J =
            FingerprintCreatureInteractionCandidateUx4J(project, draft.BlueprintId, foundation);
        var runtimeSummary = $"Projection typée : {runtimeCreature.Factions?.Memberships.Count ?? 0} faction(s), " +
                              $"{runtimeCreature.Perception?.Senses.Count ?? 0} sens, " +
                              $"dialogue={(request?.Accepted == true ? "résolu" : "sans décision")}, " +
                              $"OnConversation={script.OnConversationHandler} validé côté serveur sans exécution, " +
                              $"protection={(script.Invulnerable ? "invulnérable" : "standard")}.";
        return new(true, true, dryRun.Patch?.Summary ?? "Prévisualiser les interactions.",
            dryRun.Patch?.Summary ?? "Définir les interactions.",
            dryRun.Inverse?.Summary ?? "Restaurer les interactions précédentes.", [], FactionSummaryUx4J(draft),
            PerceptionSummaryUx4J(draft), DialogueSummaryUx4J(draft), ProtectionSummaryUx4J(draft), runtimeSummary);
    }

    internal StudioCreatureInteractionActionResultUx4J ApplyApprovedCreatureInteractionUx4J(
        StudioCreatureInteractionDraftUx4J currentDraft)
    {
        ArgumentNullException.ThrowIfNull(currentDraft);
        var project = Project; var history = History; var approved = _approvedCreatureInteractionDraftUx4J;
        var candidate = _approvedCreatureInteractionCandidateUx4J;
        if (project is null || history is null || approved is null || candidate is null ||
            _approvedCreatureInteractionRevisionUx4J is null ||
            _approvedCreatureInteractionProjectFingerprintUx4J is null ||
            _approvedCreatureInteractionDraftFingerprintUx4J is null ||
            _approvedCreatureInteractionCandidateFingerprintUx4J is null)
            return new(false, "Prévisualisez le brouillon avant de l’appliquer.");
        var stale = history.State.Revision != _approvedCreatureInteractionRevisionUx4J ||
                    !string.Equals(FingerprintProjectUx4J(project),
                        _approvedCreatureInteractionProjectFingerprintUx4J, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureInteractionDraftUx4J(approved),
                        _approvedCreatureInteractionDraftFingerprintUx4J, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureInteractionDraftUx4J(currentDraft),
                        _approvedCreatureInteractionDraftFingerprintUx4J, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureInteractionCandidateUx4J(project, currentDraft.BlueprintId,
                            candidate), _approvedCreatureInteractionCandidateFingerprintUx4J,
                        StringComparison.Ordinal);
        if (stale) { CancelCreatureInteractionUx4J(); return new(false, "Le brouillon ou le projet a changé."); }
        var recheck = _setCreatureInteractionUx4J.Handle(history.State, new(CommandId.From(Guid.NewGuid()),
            history.State.Revision, currentDraft.BlueprintId, candidate, DryRun: true));
        if (!recheck.Succeeded) { CancelCreatureInteractionUx4J(); return new(false, "La commande refuse le brouillon."); }
        var result = _setCreatureInteractionUx4J.Handle(history.State, new(CommandId.From(Guid.NewGuid()),
            history.State.Revision, currentDraft.BlueprintId, candidate, DryRun: false));
        CancelCreatureInteractionUx4J();
        if (!result.Succeeded)
            return new(false, result.Diagnostics.FirstOrDefault()?.Message ?? "Application refusée.");
        history.Record(result); InvalidateContentIndexes();
        return new(true, result.Patch?.Summary ?? "Interactions appliquées.");
    }

    internal StudioCreatureInteractionActionResultUx4J UndoCreatureInteractionUx4J()
    { CancelCreatureInteractionUx4J(); var result = Undo(); return new(result.Succeeded, result.Message); }
    internal StudioCreatureInteractionActionResultUx4J RedoCreatureInteractionUx4J()
    { CancelCreatureInteractionUx4J(); var result = Redo(); return new(result.Succeeded, result.Message); }

    internal void CancelCreatureInteractionUx4J()
    {
        _approvedCreatureInteractionDraftUx4J = null; _approvedCreatureInteractionCandidateUx4J = null;
        _approvedCreatureInteractionRevisionUx4J = null;
        _approvedCreatureInteractionProjectFingerprintUx4J = null;
        _approvedCreatureInteractionDraftFingerprintUx4J = null;
        _approvedCreatureInteractionCandidateFingerprintUx4J = null;
    }

    internal void CloseUx4J() => CancelCreatureInteractionUx4J();
    internal StudioCreatureInteractionApprovalSnapshotUx4J CaptureCreatureInteractionApprovalUx4J() => new(
        _approvedCreatureInteractionDraftUx4J, _approvedCreatureInteractionCandidateUx4J,
        _approvedCreatureInteractionRevisionUx4J, _approvedCreatureInteractionProjectFingerprintUx4J,
        _approvedCreatureInteractionDraftFingerprintUx4J, _approvedCreatureInteractionCandidateFingerprintUx4J);
    internal void RestoreCreatureInteractionApprovalUx4J(StudioCreatureInteractionApprovalSnapshotUx4J snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedCreatureInteractionDraftUx4J = snapshot.Draft;
        _approvedCreatureInteractionCandidateUx4J = snapshot.CandidateFoundation;
        _approvedCreatureInteractionRevisionUx4J = snapshot.Revision;
        _approvedCreatureInteractionProjectFingerprintUx4J = snapshot.ProjectFingerprint;
        _approvedCreatureInteractionDraftFingerprintUx4J = snapshot.DraftFingerprint;
        _approvedCreatureInteractionCandidateFingerprintUx4J = snapshot.CandidateFingerprint;
    }

    internal StudioCreatureInteractionEvidenceProjectUx4J? CreateUx4JEvidenceProject()
    {
        var project = Project; if (project is null) return null;
        var factionIds = project.Module.Factions.Factions.Where(item => !item.Archived)
            .Select(item => item.Id).ToHashSet();
        var dialogueIds = project.Dialogues.Select(item => item.Id).ToHashSet();
        var real = project.Blueprints.Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
            .Where(item => !item.IsReadOnly() && item.CreatureFoundation?.InteractionComposition is { } profile &&
                           CreatureInteractionCompositionValidator.Validate(profile).Count == 0 &&
                           profile.Factions.Memberships.All(member => factionIds.Contains(member.FactionId)) &&
                           profile.DialogueBindings.All(binding => dialogueIds.Contains(binding.DialogueId)))
            .FirstOrDefault();
        if (real is not null) return new(project, real.Id, true, false);

        var creatureEvidence = CreateUx4IEvidenceProject(); if (creatureEvidence is null) return null;
        var fallback = creatureEvidence.Project;
        if (fallback.Module.Factions.Factions.Count == 0)
            fallback = WithFactionCatalogUx4A(fallback, CreateCanonicalEvidenceCatalogUx4A(fallback.Id.Value));
        if (fallback.Dialogues.Count == 0) fallback = WithCanonicalDialogueUx4J(fallback);
        var target = fallback.Blueprints.Single(item => item.Id == creatureEvidence.BlueprintId);
        var foundation = target.CreatureFoundation ?? CreateCanonicalCreatureRulesFoundationUx4F(target);
        var profile = CreateDefaultCreatureInteractionUx4J(fallback, target);
        if (profile is null) return null;
        foundation = WithCreatureInteractionUx4J(foundation, profile, true);
        fallback = WithCreatureInteractionFoundationUx4J(fallback, target.Id, foundation);
        return new(fallback, target.Id, false, true);
    }

    internal StudioUx4JSmokeResult RunUx4JContractSmoke()
    {
        var empty = new StudioUx4JSmokeResult(false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, false, false);
        var evidence = CreateUx4JEvidenceProject(); if (evidence is null) return empty;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4j-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController(); var reopened = new StudioSessionController();
        StudioUx4JSmokeResult Execute()
        {
            try
            {
                if (!ProjectSession.Create(root, evidence.Project).Succeeded || !editing.Open(root).Succeeded) return empty;
                var explicitSelection = editing.CreatureInteractionChoicesUx4J.Count > 0;
                var draft = editing.CreateCreatureInteractionDraftUx4J(evidence.BlueprintId);
                if (draft is null) return empty with { ExplicitSelection = explicitSelection };
                var changed = ChangedCreatureInteractionDraftUx4J(draft);
                var sourceProject = editing.Project!; var sourceHistory = editing.History!;
                var preview = editing.PreviewCreatureInteractionUx4J(changed);
                var dryRun = preview.Succeeded && ReferenceEquals(sourceProject, editing.Project) &&
                             ReferenceEquals(sourceHistory, editing.History) && sourceHistory.State.Revision == draft.BaseRevision;
                var runtimeProjection = preview.Succeeded && preview.RuntimeSummary.Contains("Projection typée", StringComparison.Ordinal);

                var members = changed.Factions.Memberships.ToArray(); members[0] = members[0] with { Weight = 2 };
                var invalid = editing.PreviewCreatureInteractionUx4J(changed with
                { Factions = new(changed.Factions.Id, changed.Factions.PrimaryFactionId, members) });
                var invalidBlocked = !invalid.Succeeded && invalid.Diagnostics.Any(item =>
                    item.Code == "creature.interaction.factions_invalid");

                var missingFactionId = StableCreatureRulesIdUx4F(draft.BlueprintId.Value, "ux4j-missing-faction");
                members = changed.Factions.Memberships.ToArray();
                members[0] = members[0] with { FactionId = missingFactionId };
                var missingFaction = editing.PreviewCreatureInteractionUx4J(changed with
                { Factions = new(changed.Factions.Id, missingFactionId, members) });
                var missingFactionBlocked = !missingFaction.Succeeded && missingFaction.Diagnostics.Any(item =>
                    item.Code is "creature.interaction.faction_missing" or "world.creature.interaction.faction_missing");

                var bindings = changed.DialogueBindings.ToArray();
                if (bindings.Length > 0)
                    bindings[0] = bindings[0] with
                    { DialogueId = DialogueId.From(StableCreatureRulesIdUx4F(draft.BlueprintId.Value, "ux4j-missing-dialogue")) };
                var missingDialogue = editing.PreviewCreatureInteractionUx4J(changed with { DialogueBindings = bindings });
                var missingDialogueBlocked = bindings.Length > 0 && !missingDialogue.Succeeded &&
                                             missingDialogue.Diagnostics.Any(item =>
                                                 item.Code is "creature.interaction.dialogue_missing" or
                                                     "world.creature.interaction.dialogue_missing");

                var approvedBeforeRevision = editing.PreviewCreatureInteractionUx4J(changed);
                var neighborChanged = ToggleCreatureInteractionNeighborUx4J(editing, evidence.BlueprintId);
                var staleDraftBlocked = approvedBeforeRevision.Succeeded && neighborChanged &&
                                        !editing.ApplyApprovedCreatureInteractionUx4J(changed).Succeeded;
                if (neighborChanged) editing.Undo();

                var freshDraft = editing.CreateCreatureInteractionDraftUx4J(evidence.BlueprintId)!;
                var freshChanged = ChangedCreatureInteractionDraftUx4J(freshDraft);
                var approvedBeforeTamper = editing.PreviewCreatureInteractionUx4J(freshChanged);
                var staleApprovalBlocked = approvedBeforeTamper.Succeeded &&
                    !editing.ApplyApprovedCreatureInteractionUx4J(freshChanged with
                    { RequiredAtRuntime = !freshChanged.RequiredAtRuntime }).Succeeded;

                freshDraft = editing.CreateCreatureInteractionDraftUx4J(evidence.BlueprintId)!;
                freshChanged = ChangedCreatureInteractionDraftUx4J(freshDraft);
                var beforeApply = editing.Project!; var beforeBlueprint = beforeApply.Blueprints.Single(item => item.Id == evidence.BlueprintId);
                var beforeFoundation = beforeBlueprint.CreatureFoundation!;
                var finalPreview = editing.PreviewCreatureInteractionUx4J(freshChanged);
                var appliedAction = finalPreview.Succeeded
                    ? editing.ApplyApprovedCreatureInteractionUx4J(freshChanged)
                    : new StudioCreatureInteractionActionResultUx4J(false, "preview failed");
                var appliedProject = editing.Project;
                var appliedBlueprint = appliedProject?.Blueprints.Single(item => item.Id == evidence.BlueprintId);
                var appliedFoundation = appliedBlueprint?.CreatureFoundation;
                var appliedProfile = appliedFoundation?.InteractionComposition;
                var canonical = appliedAction.Succeeded && appliedProfile is not null &&
                                appliedProfile.Protection.GameMasterPermissions.Contains("creature.preview.inspect") &&
                                appliedProfile.Perception.MemorySeconds == 18;
                var factions = canonical && appliedProfile!.Factions.Memberships.Count >= 1 &&
                               appliedProfile.Factions.Memberships.Any(item => item.Inherited);
                var perception = canonical && appliedProfile!.Perception.Senses.Count >= 1 &&
                                 appliedProfile.Perception.Senses.All(item => item.Range > 0);
                var dialogue = canonical && appliedProfile!.DialogueBindings.Count >= 1;
                var onConversation = canonical && appliedProfile!.OnConversation.Handler == "OnConversationWitness" &&
                                     finalPreview.RuntimeSummary.Contains("OnConversation=OnConversationWitness",
                                         StringComparison.Ordinal);
                var protection = canonical && appliedProfile!.Protection.GameMasterPermissions.Count >= 1;
                var expectedFoundation = WithCreatureInteractionUx4J(beforeFoundation,
                    CreateCreatureInteractionCandidateUx4J(freshChanged), freshChanged.RequiredAtRuntime);
                var canonicalProjectPreserved = ProjectJsonSerializer.Serialize(WithCreatureInteractionFoundationUx4J(
                                                    beforeApply, evidence.BlueprintId, expectedFoundation)) ==
                                                ProjectJsonSerializer.Serialize(appliedProject!);
                var neighbors = canonical && canonicalProjectPreserved;
                var afterApplyJson = appliedProject is null ? string.Empty : ProjectJsonSerializer.Serialize(appliedProject);
                var undo = editing.UndoCreatureInteractionUx4J();
                var undoRestored = editing.Project?.Blueprints.Single(item => item.Id == evidence.BlueprintId)
                    .CreatureFoundation?.InteractionComposition == freshDraft.SourceFoundation.InteractionComposition;
                var redo = editing.RedoCreatureInteractionUx4J();
                var redoRestored = editing.Project?.Blueprints.Single(item => item.Id == evidence.BlueprintId)
                    .CreatureFoundation?.InteractionComposition?.Perception.MemorySeconds == 18;
                var undoRedo = undo.Succeeded && redo.Succeeded && undoRestored && redoRestored;
                var saved = editing.Save().Succeeded; editing.Close();
                var reopenedOk = saved && reopened.Open(root).Succeeded;
                var persistence = reopenedOk && ProjectJsonSerializer.Serialize(reopened.Project!) == afterApplyJson;
                reopened.Close();
                var succeeded = explicitSelection && factions && perception && dialogue && onConversation && protection && invalidBlocked &&
                                 missingFactionBlocked && missingDialogueBlocked && dryRun && runtimeProjection &&
                                 staleDraftBlocked && staleApprovalBlocked && canonical && undoRedo && persistence && neighbors;
                return new(succeeded, explicitSelection, factions, perception, dialogue, onConversation, protection, invalidBlocked,
                    missingFactionBlocked, missingDialogueBlocked, dryRun, runtimeProjection, staleDraftBlocked,
                    staleApprovalBlocked, canonical, undoRedo, persistence, neighbors, false);
            }
            catch { return empty; }
            finally { editing.Close(); reopened.Close(); }
        }
        var result = Execute();
        var released = DeleteUx4JSmokeRoot(root);
        return result with { TemporaryWorkspaceReleased = released, Succeeded = result.Succeeded && released };
    }

    private static CreatureInteractionCompositionProfile? CreateDefaultCreatureInteractionUx4J(
        ProjectDefinition project, BlueprintDefinition blueprint)
    {
        var faction = project.Module.Factions.Factions.FirstOrDefault(item => !item.Archived);
        if (faction is null) return null;
        var profileId = StableCreatureRulesIdUx4F(blueprint.Id.Value, "ux4j-interaction");
        var dialogue = project.Dialogues.FirstOrDefault();
        var bindings = dialogue is null ? Array.Empty<CreatureInteractionDialogueBinding>() :
        [
            new CreatureInteractionDialogueBinding(
                StableCreatureRulesIdUx4F(blueprint.Id.Value, "ux4j-dialogue"), "greeting", 1,
                dialogue.Id, null, null, true),
        ];
        return new(profileId, CreatureInteractionCompositionProfile.CurrentSchemaVersion, "studio:ux4-j",
            $"asset.creature-interaction.{blueprint.Slug}", Forge.Domain.Spatial.TransformValue.Identity,
            new(StableCreatureRulesIdUx4F(blueprint.Id.Value, "ux4j-factions"), faction.Id,
            [new(StableCreatureRulesIdUx4F(blueprint.Id.Value, "ux4j-membership"), faction.Id, 1,
                "rank.member", 0, false)]),
            new(StableCreatureRulesIdUx4F(blueprint.Id.Value, "ux4j-perception"),
            [new(StableCreatureRulesIdUx4F(blueprint.Id.Value, "ux4j-vision"), CreatureInteractionSenseKind.Vision,
                20, 120, true, ["stimulus.visual"])], 10), bindings,
            new(StableCreatureRulesIdUx4F(blueprint.Id.Value, "ux4j-protection"), false, false, true, []),
            new(StableCreatureRulesIdUx4F(blueprint.Id.Value, "ux4j-event"), 1, "studio:ux4-j", "1.0",
                "creature.interaction_requested", "OnConversation", EventAuthorityScope.Server),
            new Dictionary<string, string>());
    }

    private static StudioCreatureInteractionDraftUx4J ChangedCreatureInteractionDraftUx4J(
        StudioCreatureInteractionDraftUx4J draft)
    {
        var memberships = draft.Factions.Memberships.Select((item, index) => index == 0
            ? item with { Weight = .8, PersonalReputation = 15, Inherited = true }
            : item).ToArray();
        var senses = draft.Perception.Senses.Select((item, index) => index == 0
            ? item with { Range = Math.Min(100000, item.Range + 1), UsesOcclusion = true }
            : item).ToArray();
        var bindings = draft.DialogueBindings.Select((item, index) => index == 0
            ? item with { Priority = Math.Min(1000, item.Priority + 1) }
            : item).ToArray();
        var permissions = draft.Protection.GameMasterPermissions.Append("creature.preview.inspect")
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return draft with
        {
            RequiredAtRuntime = true,
            Factions = new(draft.Factions.Id, draft.Factions.PrimaryFactionId, memberships),
            Perception = new(draft.Perception.Id, senses, 18),
            DialogueBindings = bindings,
            OnConversationHandler = "OnConversationWitness",
            Protection = new(draft.Protection.Id, draft.Protection.Invulnerable,
                draft.Protection.UsesNarrativeDeath, draft.Protection.DialogueInterruptible, permissions),
        };
    }

    private static CreatureInteractionCompositionProfile CreateCreatureInteractionCandidateUx4J(
        StudioCreatureInteractionDraftUx4J draft) => new(draft.SourceProfile.Id, draft.SourceProfile.SchemaVersion,
        draft.SourceProfile.Provenance, draft.SourceProfile.AssetId, draft.SourceProfile.PreviewTransform,
        draft.Factions, draft.Perception, draft.DialogueBindings, draft.Protection,
        draft.SourceProfile.OnConversation with { Handler = draft.OnConversationHandler },
        draft.SourceProfile.Overrides);

    private static CreatureFoundationProfile WithCreatureInteractionUx4J(CreatureFoundationProfile source,
        CreatureInteractionCompositionProfile profile, bool required) => new(source.Id, source.SchemaVersion,
        source.Provenance, source.RequiredAtRuntime, source.Identity, source.DefaultTransform, source.Resources,
        source.Movement, source.References, source.Spawned, source.Overrides, source.AuthoringComposition,
        source.AuthoringCompositionRequiredAtRuntime, source.RulesComposition, source.RulesCompositionRequiredAtRuntime,
        profile, required, source.EventAiComposition, source.EventAiCompositionRequiredAtRuntime);

    private static ProjectDefinition WithCreatureInteractionFoundationUx4J(ProjectDefinition project,
        BlueprintId blueprintId, CreatureFoundationProfile foundation) => new(project.Id, project.Title, project.Module,
        project.Blueprints.Select(item => item.Id == blueprintId ? item.WithCreatureFoundation(foundation) : item).ToArray(),
        project.Areas, project.Dialogues, project.Quests);

    private static bool ToggleCreatureInteractionNeighborUx4J(StudioSessionController session, BlueprintId blueprintId)
    {
        var project = session.Project; var history = session.History;
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
        var result = new SetCreatureFoundationHandler(new SystemClock()).Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, blueprintId, changed, DryRun: false));
        if (!result.Succeeded) return false; history.Record(result); session.InvalidateContentIndexes(); return true;
    }

    private static ProjectDefinition WithCanonicalDialogueUx4J(ProjectDefinition project)
    {
        var guid = StableCreatureRulesIdUx4F(project.Id.Value, "ux4j-dialogue");
        var nodeId = DialogueNodeId.From(StableCreatureRulesIdUx4F(guid, "node"));
        var dialogue = new DialogueDefinition(DialogueId.From(guid), "interaction-temoin", "Conversation témoin",
            nodeId, [new DialogueNode(nodeId, null, new LocalizedText("dialogue.ux4j.greeting", "Salutations."),
                role: DialogueNodeRole.Narration)], provenance: "studio:ux4-j-evidence");
        return new(project.Id, project.Title, project.Module, project.Blueprints, project.Areas,
            [.. project.Dialogues, dialogue], project.Quests);
    }

    private static StudioCreatureInteractionPreviewUx4J FailedCreatureInteractionUx4J(string message, string code) =>
        new(false, false, message, string.Empty, string.Empty,
            [new(code, "creatureFoundation/interactionComposition", HumanCreatureInteractionMessageUx4J(code, message))],
            string.Empty, string.Empty, string.Empty, string.Empty, "Runtime non évalué.");

    private static IReadOnlyList<StudioCreatureInteractionDiagnosticUx4J> CreatureInteractionDiagnosticsUx4J(
        IReadOnlyList<CommandDiagnostic> diagnostics) => diagnostics.Select(item =>
        {
            var separator = item.Message.IndexOf(": ", StringComparison.Ordinal);
            var path = separator > 0 ? item.Message[..separator] : "creatureFoundation/interactionComposition";
            var message = separator > 0 ? item.Message[(separator + 2)..] : item.Message;
            return new StudioCreatureInteractionDiagnosticUx4J(item.Code, path,
                HumanCreatureInteractionMessageUx4J(item.Code, message));
        }).ToArray();

    private static StudioCreatureInteractionDiagnosticUx4J CreatureInteractionDiagnosticUx4J(
        CreatureFoundationDiagnostic item) => new(item.Code, item.Path,
        HumanCreatureInteractionMessageUx4J(item.Code, item.Message));

    private static string HumanCreatureInteractionMessageUx4J(string code, string fallback) => code switch
    {
        "creature.interaction.factions_invalid" => "Les factions exigent une principale et des appartenances uniques et bornées.",
        "creature.interaction.faction_missing" => "Chaque appartenance doit référencer une faction active du module.",
        "creature.interaction.perception_invalid" => "Les sens, portées, cônes, stimuli et mémoire sont incohérents.",
        "creature.interaction.dialogue_bindings_invalid" => "Les liaisons de dialogue contiennent un contexte ou une priorité invalide.",
        "creature.interaction.dialogue_missing" => "Chaque liaison doit référencer un dialogue réel du projet.",
        "creature.interaction.protection_invalid" => "Les permissions de protection doivent être des identifiants canoniques.",
        _ => fallback,
    };

    private static string FactionSummaryUx4J(StudioCreatureInteractionDraftUx4J draft) =>
        $"Factions : {draft.Factions.Memberships.Count} appartenance(s), principale définie.";
    private static string PerceptionSummaryUx4J(StudioCreatureInteractionDraftUx4J draft) =>
        $"Perception : {draft.Perception.Senses.Count} sens, mémoire {draft.Perception.MemorySeconds:R} s.";
    private static string DialogueSummaryUx4J(StudioCreatureInteractionDraftUx4J draft) =>
        $"Dialogues : {draft.DialogueBindings.Count} liaison(s), {draft.DialogueBindings.Count(item => item.Fallback)} fallback(s).";
    private static string ProtectionSummaryUx4J(StudioCreatureInteractionDraftUx4J draft) =>
        $"Protection : invulnérable={draft.Protection.Invulnerable}, mort narrative={draft.Protection.UsesNarrativeDeath}, " +
        $"interruptible={draft.Protection.DialogueInterruptible}.";

    private static StudioCreatureInteractionDraftUx4J FreezeCreatureInteractionDraftUx4J(
        StudioCreatureInteractionDraftUx4J draft) => draft with
        {
            Factions = new(draft.Factions.Id, draft.Factions.PrimaryFactionId, draft.Factions.Memberships.ToArray()),
            Perception = new(draft.Perception.Id, draft.Perception.Senses.ToArray(), draft.Perception.MemorySeconds),
            DialogueBindings = draft.DialogueBindings.ToArray(),
            Protection = new(draft.Protection.Id, draft.Protection.Invulnerable,
            draft.Protection.UsesNarrativeDeath, draft.Protection.DialogueInterruptible,
            draft.Protection.GameMasterPermissions.ToArray()),
        };

    private static string FingerprintProjectUx4J(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));
    private static string FingerprintCreatureInteractionCandidateUx4J(ProjectDefinition project, BlueprintId id,
        CreatureFoundationProfile candidate) => FingerprintProjectUx4J(WithCreatureInteractionFoundationUx4J(project, id, candidate));
    private static string FingerprintCreatureInteractionDraftUx4J(StudioCreatureInteractionDraftUx4J draft)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(draft.BaseRevision.Value); WriteUx4J(writer, draft.SourceProjectFingerprint);
        WriteUx4J(writer, draft.BlueprintId.ToString()); writer.Write(draft.RequiredAtRuntime);
        writer.Write(draft.Factions.PrimaryFactionId.ToByteArray()); writer.Write(draft.Factions.Memberships.Count);
        foreach (var item in draft.Factions.Memberships.OrderBy(item => item.Id))
        {
            writer.Write(item.Id.ToByteArray()); writer.Write(item.FactionId.ToByteArray()); writer.Write(item.Weight);
            WriteUx4J(writer, item.Rank); writer.Write(item.PersonalReputation); writer.Write(item.Inherited);
        }
        writer.Write(draft.Perception.MemorySeconds); writer.Write(draft.Perception.Senses.Count);
        foreach (var item in draft.Perception.Senses.OrderBy(item => item.Id))
        {
            writer.Write(item.Id.ToByteArray()); writer.Write((int)item.Kind); writer.Write(item.Range);
            writer.Write(item.ConeDegrees); writer.Write(item.UsesOcclusion); writer.Write(item.StimulusTags.Count);
            foreach (var tag in item.StimulusTags) WriteUx4J(writer, tag);
        }
        writer.Write(draft.DialogueBindings.Count);
        foreach (var item in draft.DialogueBindings.OrderBy(item => item.Id))
        {
            writer.Write(item.Id.ToByteArray()); WriteUx4J(writer, item.Context); writer.Write(item.Priority);
            WriteUx4J(writer, item.DialogueId.ToString()); WriteUx4J(writer, item.InterlocutorId);
            WriteUx4J(writer, item.RequiredState); writer.Write(item.Fallback);
        }
        WriteUx4J(writer, draft.OnConversationHandler);
        writer.Write(draft.Protection.Invulnerable); writer.Write(draft.Protection.UsesNarrativeDeath);
        writer.Write(draft.Protection.DialogueInterruptible); writer.Write(draft.Protection.GameMasterPermissions.Count);
        foreach (var permission in draft.Protection.GameMasterPermissions) WriteUx4J(writer, permission);
        writer.Flush(); return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
    private static void WriteUx4J(BinaryWriter writer, string? value)
    { var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty); writer.Write(bytes.Length); writer.Write(bytes); }

    private static bool DeleteUx4JSmokeRoot(string root)
    {
        try
        {
            var full = Path.GetFullPath(root); var temp = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4j-smoke-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, true); return !Directory.Exists(full);
        }
        catch { return false; }
    }
}
