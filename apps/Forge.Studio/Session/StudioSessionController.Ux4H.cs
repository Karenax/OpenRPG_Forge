using System.Security.Cryptography;
using System.Text;
using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Creatures;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SetCreatureFoundationHandler _setCreatureFoundationUx4H = new(new SystemClock());
    private StudioCreatureCapabilityDraftUx4H? _approvedCreatureCapabilityDraftUx4H;
    private CreatureFoundationProfile? _approvedCreatureCapabilityCandidateUx4H;
    private ProjectRevision? _approvedCreatureCapabilityRevisionUx4H;
    private string? _approvedCreatureCapabilityProjectFingerprintUx4H;
    private string? _approvedCreatureCapabilityDraftFingerprintUx4H;
    private string? _approvedCreatureCapabilityCandidateFingerprintUx4H;

    internal IReadOnlyList<StudioCreatureCapabilityChoiceUx4H> CreatureCapabilityChoicesUx4H => Project?.Blueprints
        .Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
        .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
        .ThenBy(item => item.Slug, StringComparer.Ordinal)
        .Select(item => new StudioCreatureCapabilityChoiceUx4H(item.Id,
            $"{item.DisplayName} · {(item.Kind == BlueprintKind.PlayerSpawn ? "joueur" : "créature")} · " +
            (item.CreatureFoundation is null ? "fondation requise" :
                item.CreatureFoundation.RulesComposition is null ? "règles à initialiser" :
                $"{item.CreatureFoundation.RulesComposition.Abilities.Count} capacité(s)"),
            item.CreatureFoundation is not null, item.CreatureFoundation?.RulesComposition is not null,
            item.IsReadOnly()))
        .ToArray() ?? [];

    internal IReadOnlyList<StudioItemChoiceUx4H> ItemChoicesUx4H => Project?.Blueprints
        .Where(item => item.Kind == BlueprintKind.Item)
        .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
        .ThenBy(item => item.Slug, StringComparer.Ordinal)
        .Select(item => new StudioItemChoiceUx4H(item.Id, $"{item.DisplayName} · {item.Slug}"))
        .ToArray() ?? [];

    internal StudioCreatureCapabilityDraftUx4H? CreateCreatureCapabilityDraftUx4H(BlueprintId blueprintId)
    {
        var project = Project;
        var history = History;
        var blueprint = project?.Blueprints.SingleOrDefault(item => item.Id == blueprintId);
        var foundation = blueprint?.CreatureFoundation;
        if (project is null || history is null || blueprint is null || foundation is null || blueprint.IsReadOnly() ||
            blueprint.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn)) return null;
        var rules = foundation.RulesComposition ?? CreatureRulesCompositionProfile.CreateDefault(
            StableCreatureRulesIdUx4F(blueprint.Id.Value, "ux4h-rules"), "studio:ux4-h");
        return new(history.State.Revision, FingerprintProjectUx4H(project), blueprint.Id, blueprint, foundation, rules,
            rules.Abilities.ToArray(), rules.Inventory.Containers.Select(container => container with
            {
                Items = container.Items.ToArray(),
            }).ToArray(), rules.Inventory.EquipmentSlots.ToDictionary(), rules.Inventory.OwnerId,
            rules.Inventory.Persistent);
    }

    internal StudioCreatureCapabilityPreviewUx4H PreviewCreatureCapabilityUx4H(
        StudioCreatureCapabilityDraftUx4H draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelCreatureCapabilityUx4H();
        var project = Project;
        var history = History;
        if (project is null || history is null)
            return FailedCreatureCapabilityUx4H("Aucun module n’est ouvert.", "studio.creature.project_missing");
        if (history.State.Revision != draft.BaseRevision ||
            !string.Equals(FingerprintProjectUx4H(project), draft.SourceProjectFingerprint, StringComparison.Ordinal))
            return FailedCreatureCapabilityUx4H(
                "Le projet a changé depuis l’ouverture de la fiche. Rechargez-la avant de prévisualiser.",
                "command.revision_conflict");

        var candidate = CreateCreatureCapabilityCandidateUx4H(draft);
        var rules = candidate.RulesComposition!;
        var validation = CreatureRulesCompositionValidator.Validate(rules,
            $"blueprints/{draft.BlueprintId}/creatureFoundation/rulesComposition");
        if (validation.Count > 0)
            return new(false, false, "Capacités ou inventaire invalides.", string.Empty, string.Empty,
                validation.Select(CreatureCapabilityDiagnosticUx4H).ToArray(), AbilitySummaryUx4H(draft),
                InventorySummaryUx4H(draft), "Runtime non évalué.");

        var dryRun = _setCreatureFoundationUx4H.Handle(history.State, new(CommandId.From(Guid.NewGuid()),
            draft.BaseRevision, draft.BlueprintId, candidate, DryRun: true));
        if (!dryRun.Succeeded)
            return new(false, false, "La commande canonique refuse cette composition.", string.Empty, string.Empty,
                CreatureCapabilityDiagnosticsUx4H(dryRun.Diagnostics), AbilitySummaryUx4H(draft),
                InventorySummaryUx4H(draft), "Runtime non évalué.");

        var runtime = CreatureRulesCompositionRuntimeResolver.Resolve(rules, required: true);
        var candidateProject = WithCreatureCapabilityFoundationUx4H(project, draft.BlueprintId, candidate);
        var view = CreatureRulesCompositionScriptService.Read(candidateProject,
            CreatureRulesCompositionScriptService.CreateHandle(draft.BlueprintId));
        if (!runtime.Succeeded || view is null)
            return new(false, false, "La projection runtime refuse cette composition.", string.Empty, string.Empty,
                runtime.Diagnostics.Select(CreatureCapabilityDiagnosticUx4H).ToArray(), AbilitySummaryUx4H(draft),
                InventorySummaryUx4H(draft), "Projection runtime refusée.");

        var frozen = FreezeCreatureCapabilityDraftUx4H(draft);
        _approvedCreatureCapabilityDraftUx4H = frozen;
        _approvedCreatureCapabilityCandidateUx4H = candidate;
        _approvedCreatureCapabilityRevisionUx4H = history.State.Revision;
        _approvedCreatureCapabilityProjectFingerprintUx4H = FingerprintProjectUx4H(project);
        _approvedCreatureCapabilityDraftFingerprintUx4H = FingerprintCreatureCapabilityDraftUx4H(frozen);
        _approvedCreatureCapabilityCandidateFingerprintUx4H =
            FingerprintCreatureCapabilityCandidateUx4H(project, draft.BlueprintId, candidate);
        return new(true, true, "Capacités et inventaire valides · prêts à être appliqués.",
            dryRun.Patch?.Summary ?? "Remplacer les règles de créature.",
            dryRun.Inverse?.Summary ?? "Restaurer les règles précédentes.", [], AbilitySummaryUx4H(draft),
            InventorySummaryUx4H(draft),
            $"Projection typée · {view.AbilityIds.Count} capacité(s) · {view.InventoryItemCount} objet(s) · " +
            $"{view.EquipmentSlots.Count} équipement(s) · aucune capacité ni transaction d’inventaire exécutée");
    }

    internal StudioCreatureCapabilityActionResultUx4H ApplyApprovedCreatureCapabilityUx4H(
        StudioCreatureCapabilityDraftUx4H currentDraft)
    {
        ArgumentNullException.ThrowIfNull(currentDraft);
        var project = Project;
        var history = History;
        var approved = _approvedCreatureCapabilityDraftUx4H;
        var candidate = _approvedCreatureCapabilityCandidateUx4H;
        if (project is null || history is null || approved is null || candidate is null ||
            _approvedCreatureCapabilityRevisionUx4H is null ||
            _approvedCreatureCapabilityProjectFingerprintUx4H is null ||
            _approvedCreatureCapabilityDraftFingerprintUx4H is null ||
            _approvedCreatureCapabilityCandidateFingerprintUx4H is null)
            return new(false, "Prévisualisez les capacités et l’inventaire avant de les appliquer.");
        var stale = history.State.Revision != _approvedCreatureCapabilityRevisionUx4H ||
                    !string.Equals(FingerprintProjectUx4H(project),
                        _approvedCreatureCapabilityProjectFingerprintUx4H, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureCapabilityDraftUx4H(approved),
                        _approvedCreatureCapabilityDraftFingerprintUx4H, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureCapabilityDraftUx4H(currentDraft),
                        _approvedCreatureCapabilityDraftFingerprintUx4H, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureCapabilityCandidateUx4H(project, currentDraft.BlueprintId,
                            candidate), _approvedCreatureCapabilityCandidateFingerprintUx4H,
                        StringComparison.Ordinal);
        if (stale)
        {
            CancelCreatureCapabilityUx4H();
            return new(false, "Le projet ou la fiche a changé depuis l’aperçu. Prévisualisez à nouveau.");
        }
        var recheck = _setCreatureFoundationUx4H.Handle(history.State, new(CommandId.From(Guid.NewGuid()),
            history.State.Revision, currentDraft.BlueprintId, candidate, DryRun: true));
        if (!recheck.Succeeded)
        {
            CancelCreatureCapabilityUx4H();
            return new(false, "La revalidation canonique a refusé la composition.");
        }
        var result = _setCreatureFoundationUx4H.Handle(history.State, new(CommandId.From(Guid.NewGuid()),
            history.State.Revision, currentDraft.BlueprintId, candidate, DryRun: false));
        CancelCreatureCapabilityUx4H();
        if (!result.Succeeded) return new(false, "La commande n’a pas pu appliquer la composition.");
        history.Record(result);
        InvalidateContentIndexes();
        return new(true, "Capacités et inventaire appliqués · Ctrl+Z pour annuler.");
    }

    internal StudioCreatureCapabilityActionResultUx4H UndoCreatureCapabilityUx4H()
    { CancelCreatureCapabilityUx4H(); var result = Undo(); return new(result.Succeeded, result.Message); }
    internal StudioCreatureCapabilityActionResultUx4H RedoCreatureCapabilityUx4H()
    { CancelCreatureCapabilityUx4H(); var result = Redo(); return new(result.Succeeded, result.Message); }

    internal void CancelCreatureCapabilityUx4H()
    {
        _approvedCreatureCapabilityDraftUx4H = null;
        _approvedCreatureCapabilityCandidateUx4H = null;
        _approvedCreatureCapabilityRevisionUx4H = null;
        _approvedCreatureCapabilityProjectFingerprintUx4H = null;
        _approvedCreatureCapabilityDraftFingerprintUx4H = null;
        _approvedCreatureCapabilityCandidateFingerprintUx4H = null;
    }

    internal void CloseUx4H() => CancelCreatureCapabilityUx4H();
    internal StudioCreatureCapabilityApprovalSnapshotUx4H CaptureCreatureCapabilityApprovalUx4H() => new(
        _approvedCreatureCapabilityDraftUx4H, _approvedCreatureCapabilityCandidateUx4H,
        _approvedCreatureCapabilityRevisionUx4H, _approvedCreatureCapabilityProjectFingerprintUx4H,
        _approvedCreatureCapabilityDraftFingerprintUx4H, _approvedCreatureCapabilityCandidateFingerprintUx4H);
    internal void RestoreCreatureCapabilityApprovalUx4H(StudioCreatureCapabilityApprovalSnapshotUx4H snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedCreatureCapabilityDraftUx4H = snapshot.Draft;
        _approvedCreatureCapabilityCandidateUx4H = snapshot.Candidate;
        _approvedCreatureCapabilityRevisionUx4H = snapshot.Revision;
        _approvedCreatureCapabilityProjectFingerprintUx4H = snapshot.ProjectFingerprint;
        _approvedCreatureCapabilityDraftFingerprintUx4H = snapshot.DraftFingerprint;
        _approvedCreatureCapabilityCandidateFingerprintUx4H = snapshot.CandidateFingerprint;
    }

    internal StudioCreatureCapabilityEvidenceProjectUx4H? CreateUx4HEvidenceProject()
    {
        var source = CreateUx4FEvidenceProject();
        if (source is null) return null;
        var project = source.Project;
        var target = project.Blueprints.Single(item => item.Id == source.BlueprintId);
        var rules = target.CreatureFoundation?.RulesComposition;
        var real = source.UsesRealProfile && rules is not null &&
                   CreatureRulesCompositionValidator.Validate(rules).Count == 0;
        if (project.Blueprints.All(item => item.Kind != BlueprintKind.Item))
        {
            var item = new BlueprintDefinition(
                BlueprintId.From(StableCreatureRulesIdUx4F(target.Id.Value, "ux4h-evidence-item")),
                "ux4h-evidence-relic", "Relique témoin UX4-H", BlueprintKind.Item);
            project = new(project.Id, project.Title, project.Module, project.Blueprints.Append(item).ToArray(),
                project.Areas, project.Dialogues, project.Quests);
            real = false;
        }
        return new(project, source.BlueprintId, real, !real);
    }

    internal StudioUx4HSmokeResult RunUx4HContractSmoke()
    {
        var result = new StudioUx4HSmokeResult(false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false);
        var evidence = CreateUx4HEvidenceProject();
        if (evidence is null) return result;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4h-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(root, evidence.Project).Succeeded || !editing.Open(root).Succeeded) return result;
            var explicitSelection = editing.CreatureCapabilityChoicesUx4H.Count > 0;
            var draft = editing.CreateCreatureCapabilityDraftUx4H(evidence.BlueprintId);
            if (draft is null) return result with { ExplicitSelection = explicitSelection };
            var changed = ChangedCreatureCapabilityDraftUx4H(draft, editing.ItemChoicesUx4H[0].BlueprintId);
            var sourceProject = editing.Project!;
            var sourceHistory = editing.History!;
            var preview = editing.PreviewCreatureCapabilityUx4H(changed);
            var dryRun = preview.Succeeded && ReferenceEquals(sourceProject, editing.Project) &&
                         ReferenceEquals(sourceHistory, editing.History) &&
                         sourceHistory.State.Revision == draft.BaseRevision;
            var badAbilities = changed.Abilities.ToArray();
            badAbilities[0] = badAbilities[0] with { Charges = -1 };
            var invalid = editing.PreviewCreatureCapabilityUx4H(changed with { Abilities = badAbilities });
            var invalidBlocked = !invalid.Succeeded && invalid.Diagnostics.Any(item =>
                item.Code == "creature.rules.abilities_invalid");
            var missingItemId = BlueprintId.From(StableCreatureRulesIdUx4F(draft.BlueprintId.Value, "missing-item"));
            var evidenceItemId = editing.ItemChoicesUx4H[0].BlueprintId;
            var badContainers = changed.Containers.Select(container => container with
            {
                Items = container.Items.Select(item => item.ItemBlueprintId == evidenceItemId
                    ? item with { ItemBlueprintId = missingItemId }
                    : item).ToArray(),
            }).ToArray();
            var missing = editing.PreviewCreatureCapabilityUx4H(changed with { Containers = badContainers });
            var missingBlocked = !missing.Succeeded && missing.Diagnostics.Any(item =>
                item.Code == "creature.rules.inventory_item_missing");

            var approvedForDraft = editing.PreviewCreatureCapabilityUx4H(changed);
            var intermediate = ToggleCreatureRulesResourceUx4F(editing, evidence.BlueprintId);
            var staleDraft = editing.PreviewCreatureCapabilityUx4H(changed);
            var staleDraftBlocked = approvedForDraft.Succeeded && intermediate && !staleDraft.Succeeded;

            var fresh = editing.CreateCreatureCapabilityDraftUx4H(evidence.BlueprintId)!;
            var freshChanged = ChangedCreatureCapabilityDraftUx4H(fresh, editing.ItemChoicesUx4H[0].BlueprintId);
            var approved = editing.PreviewCreatureCapabilityUx4H(freshChanged);
            var secondIntermediate = ToggleCreatureRulesResourceUx4F(editing, evidence.BlueprintId);
            var staleApproval = editing.ApplyApprovedCreatureCapabilityUx4H(freshChanged);
            var staleApprovalBlocked = approved.Succeeded && secondIntermediate && !staleApproval.Succeeded;

            fresh = editing.CreateCreatureCapabilityDraftUx4H(evidence.BlueprintId)!;
            freshChanged = ChangedCreatureCapabilityDraftUx4H(fresh, editing.ItemChoicesUx4H[0].BlueprintId);
            var expected = CreateCreatureCapabilityCandidateUx4H(freshChanged);
            var finalPreview = editing.PreviewCreatureCapabilityUx4H(freshChanged);
            var beforeApply = editing.Project!;
            var beforeBlueprint = beforeApply.Blueprints.Single(item => item.Id == evidence.BlueprintId);
            var apply = editing.ApplyApprovedCreatureCapabilityUx4H(freshChanged);
            var appliedProject = editing.Project!;
            var appliedBlueprint = appliedProject.Blueprints.Single(item => item.Id == evidence.BlueprintId);
            var appliedRules = appliedBlueprint.CreatureFoundation?.RulesComposition;
            var abilities = appliedRules?.Abilities.Any(item => item.AbilityId == "ability.studio-ward" &&
                item.Charges == 3 && item.ScriptId == "script.studio-ward") == true;
            var inventory = appliedRules?.Inventory.Containers.SelectMany(item => item.Items)
                .Any(item => item.ItemBlueprintId == editing.ItemChoicesUx4H[0].BlueprintId && item.Quantity == 2) == true;
            var equipment = appliedRules?.Inventory.EquipmentSlots.ContainsKey("relic") == true;
            var neighbors = ReferenceEquals(beforeBlueprint.Combat, appliedBlueprint.Combat) &&
                            ReferenceEquals(beforeBlueprint.CreatureProgression, appliedBlueprint.CreatureProgression) &&
                            ReferenceEquals(beforeBlueprint.CreatureLoot, appliedBlueprint.CreatureLoot) &&
                            ReferenceEquals(beforeBlueprint.CreatureRoleBehavior, appliedBlueprint.CreatureRoleBehavior) &&
                            ReferenceEquals(beforeBlueprint.CreatureEvents, appliedBlueprint.CreatureEvents) &&
                            ProjectJsonSerializer.Serialize(WithCreatureCapabilityFoundationUx4H(
                                beforeApply, evidence.BlueprintId, expected)) ==
                            ProjectJsonSerializer.Serialize(appliedProject);
            var canonical = finalPreview.Succeeded && apply.Succeeded && appliedRules is not null;
            var undo = editing.UndoCreatureCapabilityUx4H();
            var undone = undo.Succeeded && ReferenceEquals(beforeApply, editing.Project);
            var redo = editing.RedoCreatureCapabilityUx4H();
            var redone = redo.Succeeded && ReferenceEquals(appliedProject, editing.Project);
            var saved = redone && editing.Save().Succeeded;
            var persisted = saved && reopened.Open(root).Succeeded &&
                            ProjectJsonSerializer.Serialize(reopened.Project!) ==
                            ProjectJsonSerializer.Serialize(appliedProject);
            result = new(canonical && explicitSelection && abilities && inventory && equipment && invalidBlocked &&
                         missingBlocked && dryRun && staleDraftBlocked && staleApprovalBlocked && undone && redone &&
                         persisted && neighbors, explicitSelection, abilities, inventory, equipment, invalidBlocked,
                missingBlocked, dryRun, staleDraftBlocked, staleApprovalBlocked, canonical, undone && redone,
                persisted, neighbors, false);
        }
        catch
        {
        }
        finally
        {
            reopened.Close(); editing.Close();
            var released = DeleteUx4HSmokeRoot(root);
            result = result with { Succeeded = result.Succeeded && released, TemporaryWorkspaceReleased = released };
        }
        return result;
    }

    internal static StudioCreatureCapabilityDraftUx4H ChangedCreatureCapabilityDraftUx4H(
        StudioCreatureCapabilityDraftUx4H draft, BlueprintId itemBlueprintId)
    {
        var ability = new CreatureRulesAbilityInstance(
            StableCreatureRulesIdUx4F(draft.BlueprintId.Value, "ux4h-ability-studio-ward"),
            "ability.studio-ward", 2, 3, 8.5, ["combat.active"], ["defense", "guardian"],
            "script.studio-ward");
        var abilities = draft.Abilities.Where(item => item.AbilityId != ability.AbilityId).Append(ability).ToArray();
        var containers = draft.Containers.ToList();
        if (containers.Count == 0)
            containers.Add(new(StableCreatureRulesIdUx4F(draft.BlueprintId.Value, "ux4h-container"),
                "container.backpack", 8, 100, 100, []));
        var sourceContainer = containers[0];
        var entry = new CreatureRulesInventoryEntry(
            StableCreatureRulesIdUx4F(draft.BlueprintId.Value, "ux4h-item-entry"), itemBlueprintId, 2, 1.25, .5);
        var items = sourceContainer.Items.Where(item => item.Id != entry.Id).Append(entry).ToArray();
        containers[0] = sourceContainer with
        {
            SlotCount = Math.Max(sourceContainer.SlotCount, items.Length),
            MaximumWeight = Math.Max(sourceContainer.MaximumWeight, items.Sum(item => item.UnitWeight * item.Quantity) + 1),
            MaximumVolume = Math.Max(sourceContainer.MaximumVolume, items.Sum(item => item.UnitVolume * item.Quantity) + 1),
            Items = items,
        };
        var equipment = draft.EquipmentSlots.Where(item => item.Value != entry.Id)
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        equipment["relic"] = entry.Id;
        return draft with
        {
            Abilities = abilities,
            Containers = containers,
            EquipmentSlots = equipment,
            OwnerId = "creature.ux4h",
            Persistent = true,
        };
    }

    private static CreatureFoundationProfile CreateCreatureCapabilityCandidateUx4H(
        StudioCreatureCapabilityDraftUx4H draft)
    {
        var source = draft.SourceFoundation;
        var rulesSource = draft.SourceRules;
        var inventory = new CreatureRulesInventoryProfile(rulesSource.Inventory.Id, draft.Containers,
            draft.EquipmentSlots, draft.OwnerId, draft.Persistent);
        var rules = new CreatureRulesCompositionProfile(rulesSource.Id, rulesSource.SchemaVersion,
            rulesSource.Provenance, rulesSource.AssetId, rulesSource.PreviewTransform, rulesSource.InitialState,
            rulesSource.Attributes, rulesSource.Defenses, draft.Abilities, inventory, rulesSource.Overrides);
        return new(source.Id, source.SchemaVersion, source.Provenance, source.RequiredAtRuntime, source.Identity,
            source.DefaultTransform, source.Resources, source.Movement, source.References, source.Spawned,
            source.Overrides, source.AuthoringComposition, source.AuthoringCompositionRequiredAtRuntime, rules,
            source.RulesCompositionRequiredAtRuntime, source.InteractionComposition,
            source.InteractionCompositionRequiredAtRuntime, source.EventAiComposition,
            source.EventAiCompositionRequiredAtRuntime);
    }

    private static ProjectDefinition WithCreatureCapabilityFoundationUx4H(ProjectDefinition project,
        BlueprintId blueprintId, CreatureFoundationProfile foundation) => new(project.Id, project.Title,
        project.Module, project.Blueprints.Select(item => item.Id == blueprintId
            ? item.WithCreatureFoundation(foundation) : item).ToArray(), project.Areas, project.Dialogues,
        project.Quests);

    private static StudioCreatureCapabilityPreviewUx4H FailedCreatureCapabilityUx4H(string message, string code) =>
        new(false, false, message, string.Empty, string.Empty,
            [new(code, "creature/foundation/rules", message)], "Capacités non évaluées.",
            "Inventaire non évalué.", "Runtime non évalué.");

    private static IReadOnlyList<StudioCreatureCapabilityDiagnosticUx4H> CreatureCapabilityDiagnosticsUx4H(
        IReadOnlyList<CommandDiagnostic> diagnostics) => diagnostics.Select(item =>
    {
        var separator = item.Message.IndexOf(": ", StringComparison.Ordinal);
        var path = separator > 0 ? item.Message[..separator] : "creature/foundation/rules";
        var message = separator > 0 ? item.Message[(separator + 2)..] : item.Message;
        return new StudioCreatureCapabilityDiagnosticUx4H(item.Code, path, HumanCreatureCapabilityMessageUx4H(
            item.Code, message));
    }).ToArray();

    private static StudioCreatureCapabilityDiagnosticUx4H CreatureCapabilityDiagnosticUx4H(
        CreatureFoundationDiagnostic item) => new(item.Code, item.Path,
        HumanCreatureCapabilityMessageUx4H(item.Code, item.Message));

    private static string HumanCreatureCapabilityMessageUx4H(string code, string fallback) => code switch
    {
        "creature.rules.abilities_invalid" => "Chaque capacité exige une clé unique, des bornes valides, des tags et un script canoniques.",
        "creature.rules.inventory_invalid" => "Les conteneurs, objets, limites et équipements doivent rester cohérents.",
        "creature.rules.inventory_item_missing" => "Chaque objet d’inventaire doit résoudre un blueprint Objet du projet.",
        _ => fallback,
    };

    private static string AbilitySummaryUx4H(StudioCreatureCapabilityDraftUx4H draft) =>
        $"{draft.Abilities.Count} capacité(s) · {draft.Abilities.Sum(item => item.Charges)} charge(s) · " +
        $"{draft.Abilities.SelectMany(item => item.AiTags).Distinct().Count()} tag(s) IA";
    private static string InventorySummaryUx4H(StudioCreatureCapabilityDraftUx4H draft) =>
        $"{draft.Containers.Count} conteneur(s) · {draft.Containers.SelectMany(item => item.Items).Sum(item => item.Quantity)} objet(s) · " +
        $"{draft.EquipmentSlots.Count} équipé(s) · persistant {draft.Persistent.ToString().ToLowerInvariant()}";

    private static StudioCreatureCapabilityDraftUx4H FreezeCreatureCapabilityDraftUx4H(
        StudioCreatureCapabilityDraftUx4H draft) => draft with
    {
        Abilities = draft.Abilities.Select(item => item with
        { Conditions = item.Conditions.ToArray(), AiTags = item.AiTags.ToArray() }).ToArray(),
        Containers = draft.Containers.Select(container => container with { Items = container.Items.ToArray() }).ToArray(),
        EquipmentSlots = draft.EquipmentSlots.ToDictionary(),
    };

    private static string FingerprintProjectUx4H(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));
    private static string FingerprintCreatureCapabilityCandidateUx4H(ProjectDefinition project,
        BlueprintId blueprintId, CreatureFoundationProfile candidate) =>
        FingerprintProjectUx4H(WithCreatureCapabilityFoundationUx4H(project, blueprintId, candidate));
    private static string FingerprintCreatureCapabilityDraftUx4H(StudioCreatureCapabilityDraftUx4H draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(draft.BaseRevision.Value); WriteUx4H(writer, draft.SourceProjectFingerprint);
        WriteUx4H(writer, draft.BlueprintId.ToString()); writer.Write(draft.Abilities.Count);
        foreach (var item in draft.Abilities)
        {
            writer.Write(item.Id.ToByteArray()); WriteUx4H(writer, item.AbilityId); writer.Write(item.Level);
            writer.Write(item.Charges); writer.Write(item.CooldownSeconds); WriteListUx4H(writer, item.Conditions);
            WriteListUx4H(writer, item.AiTags); WriteUx4H(writer, item.ScriptId);
        }
        writer.Write(draft.Containers.Count);
        foreach (var container in draft.Containers)
        {
            writer.Write(container.Id.ToByteArray()); WriteUx4H(writer, container.ContainerId);
            writer.Write(container.SlotCount); writer.Write(container.MaximumWeight); writer.Write(container.MaximumVolume);
            writer.Write(container.Items.Count);
            foreach (var item in container.Items)
            {
                writer.Write(item.Id.ToByteArray()); WriteUx4H(writer, item.ItemBlueprintId.ToString());
                writer.Write(item.Quantity); writer.Write(item.UnitWeight); writer.Write(item.UnitVolume);
            }
        }
        writer.Write(draft.EquipmentSlots.Count);
        foreach (var item in draft.EquipmentSlots.OrderBy(item => item.Key, StringComparer.Ordinal))
        { WriteUx4H(writer, item.Key); writer.Write(item.Value.ToByteArray()); }
        WriteUx4H(writer, draft.OwnerId); writer.Write(draft.Persistent); writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
    private static void WriteListUx4H(BinaryWriter writer, IReadOnlyList<string> values)
    { writer.Write(values.Count); foreach (var value in values) WriteUx4H(writer, value); }
    private static void WriteUx4H(BinaryWriter writer, string? value)
    { var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty); writer.Write(bytes.Length); writer.Write(bytes); }

    private static bool DeleteUx4HSmokeRoot(string root)
    {
        try
        {
            var full = Path.GetFullPath(root); var temp = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4h-smoke-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full) && !File.Exists(full);
        }
        catch { return false; }
    }
}
