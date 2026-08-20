using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Items;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Items;
using Forge.RpgKit.Domain.Items;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SetItemAuthoringBundleHandler _setItemBundleUx4S = new(new SystemClock());
    private readonly UpsertBlueprintHandler _itemNeighborUx4S = new(new SystemClock());
    private StudioItemDraftUx4S? _approvedItemDraftUx4S;
    private ProjectRevision? _approvedItemRevisionUx4S;
    private string? _approvedItemProjectFingerprintUx4S;
    private string? _approvedItemCandidateFingerprintUx4S;

    internal IReadOnlyList<StudioItemChoiceUx4S> ItemChoicesUx4S =>
        Project?.Blueprints.Where(item => item.Kind == BlueprintKind.Item)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => new StudioItemChoiceUx4S(item.Id,
                $"{item.DisplayName} · {item.Slug} · " +
                (item.ItemFoundation is null ? "fondation absente" : item.ItemAdvanced is null ? "profil avancé absent" : "profil complet"),
                item.ItemFoundation is not null, item.ItemAdvanced is not null, item.IsReadOnly()))
            .ToArray() ?? [];

    internal StudioItemDraftUx4S? CreateItemDraftUx4S(BlueprintId? blueprintId = null)
    {
        var project = Project; var history = History;
        if (project is null || history is null) return null;
        BlueprintDefinition? original = null;
        if (blueprintId is not null)
        {
            original = project.Blueprints.SingleOrDefault(item => item.Id == blueprintId);
            if (original is null || original.Kind != BlueprintKind.Item || original.IsReadOnly()) return null;
        }

        var id = original?.Id ?? BlueprintId.From(StableItemGuidUx4S(project.Id.Value, NextItemSlugUx4S(project)));
        var slug = original?.Slug ?? NextItemSlugUx4S(project);
        var displayName = original?.DisplayName ?? "Boussole de braise";
        var foundation = original?.ItemFoundation ?? CompleteFoundationUx4S(id, slug, displayName);
        var advanced = original?.ItemAdvanced ?? CompleteAdvancedUx4S(id);
        return new(history.State.Revision, FingerprintItemProjectUx4S(project), id, original, slug, displayName,
            foundation, advanced, false);
    }

    internal StudioItemDraftUx4S CompleteItemDraftUx4S(StudioItemDraftUx4S draft) => draft with
    {
        Foundation = CompleteFoundationUx4S(draft.BlueprintId, draft.Slug, draft.DisplayName),
        Advanced = CompleteAdvancedUx4S(draft.BlueprintId),
    };

    internal StudioItemPreviewUx4S PreviewItemUx4S(StudioItemDraftUx4S draft)
    {
        ArgumentNullException.ThrowIfNull(draft); CancelItemUx4S();
        var project = Project; var history = History;
        if (project is null || history is null)
            return FailedItemUx4S("Aucun module n’est ouvert.", "studio.item.project_missing");
        if (history.State.Revision != draft.BaseRevision ||
            !string.Equals(FingerprintItemProjectUx4S(project), draft.SourceProjectFingerprint, StringComparison.Ordinal))
            return FailedItemUx4S("Le projet a changé ; reconstruisez l’aperçu.", "command.revision_conflict");

        var frozen = FreezeItemDraftUx4S(draft);
        var command = ItemCommandUx4S(frozen, history.State.Revision, true);
        var dryRun = _setItemBundleUx4S.Handle(history.State, command);
        if (!dryRun.Succeeded)
            return new(false, false, HumanItemMessageUx4S(dryRun.Diagnostics.FirstOrDefault()?.Code,
                    dryRun.Diagnostics.FirstOrDefault()?.Message ?? "Le profil objet est invalide."),
                string.Empty, string.Empty,
                dryRun.Diagnostics.Select(item => new StudioItemDiagnosticUx4S(item.Code,
                    DiagnosticPathUx4S(item.Message), HumanItemMessageUx4S(item.Code, item.Message))).ToArray(),
                "Fondation non évaluée.", "Gameplay non évalué.", "Profil avancé non évalué.",
                "Opérations non évaluées.", "Runtime non évalué.");

        var probe = _setItemBundleUx4S.Handle(history.State, command with { DryRun = false });
        if (!probe.Succeeded)
            return FailedItemUx4S("Le candidat atomique n’a pas pu être construit.", "studio.item.candidate_invalid");
        var runtime = ResolveItemRuntimeUx4S(frozen);
        var canApply = runtime.Diagnostics.Count == 0;
        if (canApply)
        {
            _approvedItemDraftUx4S = frozen;
            _approvedItemRevisionUx4S = history.State.Revision;
            _approvedItemProjectFingerprintUx4S = FingerprintItemProjectUx4S(project);
            _approvedItemCandidateFingerprintUx4S = FingerprintItemProjectUx4S(probe.State.Project);
        }

        return new(true, canApply,
            canApply ? "Objet complet prêt à appliquer." : "Le dry-run passe mais une résolution runtime refuse le profil.",
            dryRun.Patch?.Summary ?? "Prévisualiser le lot objet complet.",
            dryRun.Inverse?.Summary ?? "Restaurer le profil objet précédent.", runtime.Diagnostics,
            runtime.Foundation, runtime.Gameplay, runtime.Advanced, runtime.Operations, runtime.Runtime);
    }

    internal StudioItemActionResultUx4S ApplyApprovedItemUx4S(StudioItemDraftUx4S current)
    {
        var project = Project; var history = History; var approved = _approvedItemDraftUx4S;
        if (project is null || history is null || approved is null || _approvedItemRevisionUx4S is null ||
            _approvedItemProjectFingerprintUx4S is null || _approvedItemCandidateFingerprintUx4S is null)
            return new(false, "Prévisualisez d’abord le lot objet complet.");

        var frozenCurrent = FreezeItemDraftUx4S(current);
        if (history.State.Revision != _approvedItemRevisionUx4S ||
            !string.Equals(FingerprintItemProjectUx4S(project), _approvedItemProjectFingerprintUx4S, StringComparison.Ordinal) ||
            !string.Equals(FingerprintItemDraftUx4S(frozenCurrent), FingerprintItemDraftUx4S(approved), StringComparison.Ordinal))
        { CancelItemUx4S(); return new(false, "Le brouillon ou le projet a changé ; reconstruisez l’aperçu."); }

        var command = ItemCommandUx4S(approved, history.State.Revision, true);
        var dryRun = _setItemBundleUx4S.Handle(history.State, command);
        var probe = dryRun.Succeeded
            ? _setItemBundleUx4S.Handle(history.State, command with { DryRun = false })
            : dryRun;
        if (!dryRun.Succeeded || !probe.Succeeded ||
            !string.Equals(FingerprintItemProjectUx4S(probe.State.Project), _approvedItemCandidateFingerprintUx4S,
                StringComparison.Ordinal))
        { CancelItemUx4S(); return new(false, "Le re-dry-run ne correspond plus au candidat approuvé."); }

        var result = _setItemBundleUx4S.Handle(history.State, command with { DryRun = false });
        CancelItemUx4S();
        if (!result.Succeeded)
            return new(false, HumanItemMessageUx4S(result.Diagnostics.FirstOrDefault()?.Code,
                result.Diagnostics.FirstOrDefault()?.Message ?? "Application refusée."));
        history.Record(result);
        return new(true, result.Patch?.Summary ?? "Objet complet appliqué.");
    }

    internal StudioItemActionResultUx4S UndoItemUx4S()
    { CancelItemUx4S(); var result = Undo(); return new(result.Succeeded, result.Message); }
    internal StudioItemActionResultUx4S RedoItemUx4S()
    { CancelItemUx4S(); var result = Redo(); return new(result.Succeeded, result.Message); }
    internal void CancelItemUx4S()
    {
        _approvedItemDraftUx4S = null; _approvedItemRevisionUx4S = null;
        _approvedItemProjectFingerprintUx4S = null; _approvedItemCandidateFingerprintUx4S = null;
    }
    internal void CloseUx4S() => CancelItemUx4S();
    internal StudioItemApprovalSnapshotUx4S CaptureItemApprovalUx4S() => new(
        _approvedItemDraftUx4S, _approvedItemRevisionUx4S, _approvedItemProjectFingerprintUx4S,
        _approvedItemCandidateFingerprintUx4S);
    internal void RestoreItemApprovalUx4S(StudioItemApprovalSnapshotUx4S snapshot)
    {
        _approvedItemDraftUx4S = snapshot.Draft; _approvedItemRevisionUx4S = snapshot.Revision;
        _approvedItemProjectFingerprintUx4S = snapshot.ProjectFingerprint;
        _approvedItemCandidateFingerprintUx4S = snapshot.CandidateFingerprint;
    }

    internal StudioItemEvidenceProjectUx4S? CreateUx4SEvidenceProject()
    {
        var source = Project; if (source is null || source.Areas.Count == 0) return null;
        var existing = source.Blueprints.FirstOrDefault(item => item.Kind == BlueprintKind.Item && !item.IsReadOnly());
        var itemId = existing?.Id ?? BlueprintId.From(StableItemGuidUx4S(source.Id.Value, "ux4-s:item"));
        var slug = existing?.Slug ?? "boussole-de-braise";
        var display = existing?.DisplayName ?? "Boussole de braise";
        var complete = new BlueprintDefinition(itemId, slug, display, BlueprintKind.Item,
            existing?.Combat, existing?.PaletteOrigin ?? BlueprintPaletteOrigin.ProjectContent,
            existing?.IsFavorite ?? false, existing?.Provenance, itemAdvanced: CompleteAdvancedUx4S(itemId),
            authoring: existing?.Authoring, itemFoundation: CompleteFoundationUx4S(itemId, slug, display));
        var blueprints = existing is null
            ? source.Blueprints.Append(complete).ToArray()
            : source.Blueprints.Select(item => item.Id == itemId ? complete : item).ToArray();
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, blueprints, source.Areas,
            source.Dialogues, source.Quests);
        var real = existing?.ItemFoundation is not null && existing.ItemAdvanced is not null &&
                   ItemFoundationValidator.Validate(existing.ItemFoundation).Count == 0 &&
                   ItemAdvancedValidator.Validate(existing.ItemAdvanced, "itemAdvanced").Count == 0;
        return new(project, itemId, real, !real);
    }

    internal StudioUx4SSmokeResult RunUx4SContractSmoke()
    {
        var empty = new StudioUx4SSmokeResult(false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, false);
        var evidence = CreateUx4SEvidenceProject(); if (evidence is null) return empty;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4s-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController(); var reopened = new StudioSessionController();
        StudioUx4SSmokeResult Execute()
        {
            try
            {
                if (!ProjectSession.Create(root, evidence.Project).Succeeded || !editing.Open(root).Succeeded) return empty;
                var draft = editing.CreateItemDraftUx4S(evidence.BlueprintId); if (draft is null) return empty;
                draft = editing.CompleteItemDraftUx4S(draft);
                draft = draft with { DisplayName = draft.DisplayName + " · preuve contractuelle" };
                var explicitSelection = editing.ItemChoicesUx4S.Any(item => item.BlueprintId == evidence.BlueprintId);
                var creation = draft.Foundation.Creation is { Wizard.Sections.Count: 8, Appearance.MeshAssetIds.Count: > 0 };
                var identity = draft.Foundation.Texts.Count > 0 && draft.Foundation.Type.AllowedSlots.Count > 0;
                var appearanceInventory = draft.Foundation.Creation is { Inventory.GridWidth: > 0, Stack.Maximum: > 0,
                    Usage.Durability: > 0 };
                var gameplay = draft.Foundation.Gameplay is { Transfer.AllowedDestinations.Count: 8, Modifiers.Count: > 0,
                    Ability: not null };
                var equipment = draft.Advanced is { Weapon: not null, Armor: not null, Ammo: not null,
                    Durability: not null, Container: not null };
                var lootCraft = draft.Advanced.LootContexts.Count > 0 && draft.Advanced.Unique.Enabled &&
                                draft.Advanced.Recipes.Count > 0 && draft.Advanced.Generator is { Enabled: true };
                var inspectorEvents = draft.Foundation.Operations is { Inspector.Placements.Count: > 0,
                    Events.Bindings.Count: 9 };
                var validation = ItemFoundationValidator.Validate(draft.Foundation).Count == 0 &&
                                 ItemAdvancedValidator.Validate(draft.Advanced, "itemAdvanced").Count == 0;
                var before = editing.Project!; var beforeHistory = editing.History!; var revision = beforeHistory.State.Revision;
                var journal = beforeHistory.Journal.ToArray(); var preview = editing.PreviewItemUx4S(draft);
                var dryRun = preview.Succeeded && preview.CanApply && ReferenceEquals(before, editing.Project) &&
                             ReferenceEquals(beforeHistory, editing.History) && beforeHistory.State.Revision == revision &&
                             beforeHistory.Journal.SequenceEqual(journal);

                var neighbor = editing.Project!.Blueprints.First(item => item.Id == evidence.BlueprintId);
                var changed = editing._itemNeighborUx4S.Handle(editing.History!.State,
                    new UpsertBlueprintCommand(CommandId.From(Guid.NewGuid()), editing.History.State.Revision,
                        neighbor.Id, neighbor.Slug, neighbor.DisplayName + " témoin", neighbor.Kind, false, neighbor.Authoring));
                if (changed.Succeeded) editing.History.Record(changed);
                var staleSource = changed.Succeeded && !editing.ApplyApprovedItemUx4S(draft).Succeeded;
                if (changed.Succeeded) editing.Undo();

                draft = editing.CompleteItemDraftUx4S(editing.CreateItemDraftUx4S(evidence.BlueprintId)!);
                preview = editing.PreviewItemUx4S(draft);
                var staleApproval = preview.Succeeded &&
                    !editing.ApplyApprovedItemUx4S(draft with { DisplayName = draft.DisplayName + " altérée" }).Succeeded;

                draft = editing.CompleteItemDraftUx4S(editing.CreateItemDraftUx4S(evidence.BlueprintId)!);
                var source = editing.Project!; var sourceItem = source.Blueprints.Single(item => item.Id == evidence.BlueprintId);
                var finalPreview = editing.PreviewItemUx4S(draft);
                var appliedResult = finalPreview.CanApply
                    ? editing.ApplyApprovedItemUx4S(draft)
                    : new StudioItemActionResultUx4S(false, "preview failed");
                var applied = editing.Project;
                var appliedJson = applied is null ? string.Empty : ProjectJsonSerializer.Serialize(applied);
                var appliedItem = applied?.Blueprints.SingleOrDefault(item => item.Id == evidence.BlueprintId);
                var canonical = appliedResult.Succeeded && appliedItem?.ItemFoundation == draft.Foundation &&
                                appliedItem.ItemAdvanced == draft.Advanced &&
                                editing.History!.Journal.LastOrDefault()?.CommandName == "set_item_authoring_bundle";
                var restoredBlueprints = applied?.Blueprints.Select(item => item.Id == evidence.BlueprintId ? sourceItem : item).ToArray();
                var neighbors = restoredBlueprints is not null && ProjectJsonSerializer.Serialize(new ProjectDefinition(
                    source.Id, source.Title, source.Module, restoredBlueprints, source.Areas, source.Dialogues, source.Quests)) ==
                    ProjectJsonSerializer.Serialize(source);
                var undo = editing.UndoItemUx4S();
                var undone = undo.Succeeded && ProjectJsonSerializer.Serialize(editing.Project!) == ProjectJsonSerializer.Serialize(source);
                var redo = editing.RedoItemUx4S();
                var redone = redo.Succeeded && ProjectJsonSerializer.Serialize(editing.Project!) == appliedJson;
                var saved = redone && editing.Save().Succeeded; editing.Close();
                var persistence = saved && reopened.Open(root).Succeeded &&
                                  ProjectJsonSerializer.Serialize(reopened.Project!) == appliedJson;
                reopened.Close();
                var succeeded = explicitSelection && creation && identity && appearanceInventory && gameplay && equipment &&
                                lootCraft && inspectorEvents && validation && dryRun && staleSource && staleApproval &&
                                canonical && undone && redone && persistence && neighbors;
                return new(succeeded, explicitSelection, creation, identity, appearanceInventory, gameplay, equipment,
                    lootCraft, inspectorEvents, validation, dryRun, staleSource, staleApproval, canonical,
                    undone && redone, persistence, neighbors, false);
            }
            catch { return empty; }
            finally { editing.Close(); reopened.Close(); }
        }

        var result = Execute(); var released = DeleteItemRootUx4S(root);
        return result with { TemporaryWorkspaceReleased = released, Succeeded = result.Succeeded && released };
    }

    private static SetItemAuthoringBundleCommand ItemCommandUx4S(StudioItemDraftUx4S draft,
        ProjectRevision revision, bool dryRun) => new(CommandId.From(Guid.NewGuid()), revision, draft.BlueprintId,
        draft.Slug, draft.DisplayName, draft.Foundation, draft.Advanced, draft.AcceptTypeMigration, dryRun);

    private static (IReadOnlyList<StudioItemDiagnosticUx4S> Diagnostics, string Foundation, string Gameplay,
        string Advanced, string Operations, string Runtime) ResolveItemRuntimeUx4S(StudioItemDraftUx4S draft)
    {
        var diagnostics = new List<StudioItemDiagnosticUx4S>();
        var foundation = ItemFoundationRuntimeAdapter.Resolve(draft.Foundation, true);
        diagnostics.AddRange(foundation.Diagnostics.Select(item => new StudioItemDiagnosticUx4S(item.Code, item.Path,
            HumanItemMessageUx4S(item.Code, item.Message))));
        var gameplay = ItemGameplayRuntimeAdapter.Resolve(draft.Foundation, true);
        diagnostics.AddRange(gameplay.Diagnostics.Select(item => new StudioItemDiagnosticUx4S(item.Code, item.Path,
            HumanItemMessageUx4S(item.Code, item.Message))));
        var operations = ItemOperationsRuntimeAdapter.Resolve(draft.Foundation, true);
        diagnostics.AddRange(operations.Diagnostics.Select(item => new StudioItemDiagnosticUx4S(item.Code, item.Path,
            HumanItemMessageUx4S(item.Code, item.Message))));
        var advancedDiagnostics = ItemAdvancedValidator.Validate(draft.Advanced, "itemAdvanced");
        diagnostics.AddRange(advancedDiagnostics.Select(item => new StudioItemDiagnosticUx4S(item.Code, item.Path,
            HumanItemMessageUx4S(item.Code, item.Message))));
        var context = new ItemRuntimeContext(Math.Max(99, draft.Advanced.Requirements.MinimumLevel),
            Math.Max(99, draft.Advanced.Requirements.MinimumLore), draft.Advanced.Requirements.Skills.ToHashSet(StringComparer.Ordinal),
            draft.Advanced.Requirements.Traits.ToHashSet(StringComparer.Ordinal),
            draft.Advanced.Requirements.Factions.ToHashSet(StringComparer.Ordinal),
            draft.Advanced.Requirements.ScriptCondition is { } condition ? new HashSet<string>([condition], StringComparer.Ordinal) :
                new HashSet<string>(StringComparer.Ordinal), "default", draft.Advanced.Economy.Currency, 1,
            draft.Advanced.Durability?.Initial ?? 100, 20260820, "studio-author", true);
        string advancedSummary;
        try
        {
            var requirements = ItemAdvancedRuntimeResolver.ResolveRequirements(draft.Advanced, context);
            var value = ItemAdvancedRuntimeResolver.ResolveEconomicValue(draft.Advanced, context);
            var generation = draft.Advanced.Generator is { Enabled: true }
                ? ItemAdvancedRuntimeResolver.Generate(draft.Advanced, context).Explanation
                : "générateur désactivé";
            advancedSummary = $"Valeur simulée {value} {draft.Advanced.Economy.Currency} · prérequis " +
                              $"{(requirements.Allowed ? "satisfaits" : "refusés")} · {generation}.";
        }
        catch (Exception exception)
        {
            diagnostics.Add(new("studio.item.runtime_preview_failed", "itemAdvanced/runtime", exception.Message));
            advancedSummary = "Profil avancé non simulé.";
        }
        return (diagnostics,
            $"Type {draft.Foundation.Type.Slug} · {draft.Foundation.Texts.Count} langue(s) · " +
            $"{draft.Foundation.Creation?.Appearance.MeshAssetIds.Count ?? 0} mesh(es).",
            $"{gameplay.Transfer?.AllowedDestinations.Count ?? 0} transferts · {gameplay.Modifiers.Count} modificateur(s) · " +
            $"{gameplay.Slots.Count} slot(s).", advancedSummary,
            $"{operations.Loot?.Entries.Count ?? 0} entrée(s) loot · {operations.Inventory?.Placements.Count ?? 0} placement(s) · " +
            $"{operations.EventBindings.Count} événement(s).",
            "Résolution pure en mémoire : aucune instance, transaction économique, script, projectile ou drop live exécuté.");
    }

    private static ItemFoundationProfile CompleteFoundationUx4S(BlueprintId blueprintId, string slug, string displayName)
    {
        var provenance = "studio:ux4-s"; var typeId = StableItemGuidUx4S(blueprintId.Value, "type");
        var foundationId = StableItemGuidUx4S(blueprintId.Value, "foundation");
        var result = new ItemFoundationProfile(foundationId, 1, provenance, true, "project-content", "CC0-1.0",
            new string('a', 64), typeId,
            new(typeId, 1, provenance, "relic", ["hand", "inventory", "body"],
                ["utility", "ward", "damage"], "hand-rig"),
            slug, "asset.item.ember-compass", "hand-rig", 1, 2, 3, 125,
            ["hand"], [new(StableItemGuidUx4S(blueprintId.Value, "property:ward"), "ward", 8,
                "effect.ember-ward", "OnUse")],
            [new("fr", displayName, "Un objet rituel.", "Un objet complet préparé dans le Studio.",
                "Objet inconnu", "Relique", "Preuve UX4-S")], ["identified", "durability", "owner"]);
        var creation = ItemCreationProfile.CreateDefault(StableItemGuidUx4S(blueprintId.Value, "creation"),
            StableItemGuidUx4S(blueprintId.Value, "appearance"), provenance) with
        {
            RequiredAtRuntime = true, ContentSha256 = new string('b', 64),
            Wizard = new(["archetype", "appearance", "slots", "quality", "level", "property", "provenance",
                "generation-rules"], ["fixed-seed", "preserve-provenance"]),
            Appearance = new(StableItemGuidUx4S(blueprintId.Value, "appearance"), 1, provenance,
                ["asset.item.ember-compass.mesh"], ["asset.material.ember-steel"], ["hand-r"],
                "asset.item.ember-compass.icon", ["asset.vfx.ember-glow"], "asset.item.ember-compass.equipped"),
            Inventory = new(1.25, .4, 1, 2, .5, .25), Stack = new(10, slug, true),
            Usage = new(3, 3, 2, 2, 5_000, 100, false, "rest-or-refill")
        };
        var gameplay = new ItemGameplayProfile(StableItemGuidUx4S(blueprintId.Value, "gameplay"), 1, provenance,
            true, "project-content", "CC0-1.0", new string('c', 64),
            new(true, true, true, true, true, true, true),
            new(Enum.GetValues<ItemTransferDestination>().ToDictionary(item => item,
                item => item is ItemTransferDestination.Container or ItemTransferDestination.GameMaster),
                ["item.transfer.quest"]),
            [new(StableItemGuidUx4S(blueprintId.Value, "modifier:ward"), 1, provenance, "ward",
                new Dictionary<string, double> { ["value"] = 8 }, "highest", "night", "+8 garde",
                "BeforeItemModifier", "AfterItemModifier")],
            new(StableItemGuidUx4S(blueprintId.Value, "ability"), "point",
                new Dictionary<string, double> { ["focus"] = 1 }, 1, 12_000,
                "ItemAbilityRequested", "ItemAbilityResolved"),
            new([new("hand", "forge", "hand-rig", 1, [])], ["hand"]));
        var operations = new ItemOperationsProfile(StableItemGuidUx4S(blueprintId.Value, "operations"), 1,
            provenance, true, "project-content", "CC0-1.0", new string('d', 64),
            new(StableItemGuidUx4S(blueprintId.Value, "loot"), 1, provenance,
                [new(blueprintId.Value, "relic", 1, 1, 1, 2, "quest-active", true)]),
            new(StableItemGuidUx4S(blueprintId.Value, "inspector"), 1, provenance, 0,
                [new(StableItemGuidUx4S(blueprintId.Value, "instance"), "backpack", null, 0, 0, 1,
                    ["quest"], "asset.item.ember-compass.preview")], true, true, true),
            new("1.0", Enum.GetValues<ItemLifecycleEventType>().Select(kind => new ItemLifecycleBindingPolicy(
                StableItemGuidUx4S(blueprintId.Value, $"event:{kind}"), 1, provenance, kind, $"On{kind}", "server"))
                .ToArray()));
        return result with { Creation = creation, Gameplay = gameplay, Operations = operations };
    }

    private static ItemAdvancedProfile CompleteAdvancedUx4S(BlueprintId blueprintId)
    {
        var provenance = "studio:ux4-s";
        return ItemAdvancedProfile.CreateDefault(StableItemGuidUx4S(blueprintId.Value, "advanced"), provenance) with
        {
            Economy = new("gold", 125, 1.2, 1.4, .8, 1,
                new Dictionary<string, decimal> { ["capital"] = 150 }),
            Requirements = new(3, 2, ["lore"], ["ember-attuned"], [], "quest.active"),
            Identification = new("unknown", 10, 5, "gold", true,
                [new("unknown", 0, []), new("identified", 10, ["*"])]),
            Ownership = new(true, "studio-author", "capital", true,
                [new(StableItemGuidUx4S(blueprintId.Value, "owner-event"), "studio-author", "capital",
                    "authoring", DateTimeOffset.UnixEpoch, provenance)]),
            OnHitProcs = [new(StableItemGuidUx4S(blueprintId.Value, "proc:ember"), "hit", .25, 2,
                "target.marked", "effect.ember", "wielder", true, provenance)],
            Weapon = new("ritual", 1, false,
                [new("primary", "fire", 2, 6, 1.5, "attack.primary", ["ritual"])], ["ritual"]),
            Armor = new(["hand"], 5, .2, 0, "ember-steel",
                new Dictionary<string, double> { ["fire"] = .25 }, ["cracked"]),
            Ammo = new("ember", "asset.projectile.ember", 12, .5, "fire", true),
            Durability = new(true, 100, 100, 1, 2, "repair", 1, "item.broken", "item.broken"),
            Container = new(4, 10, 2, .75, ["gem"], ["inventory.open"], true, true),
            LootContexts = [new(StableItemGuidUx4S(blueprintId.Value, "loot-context:chest"), "chest", "crypt",
                null, 1, 20, "normal", ["undead"], "quest.active", "eclipse", 5, provenance)],
            Unique = new(true, $"unique:{blueprintId.Value:N}", ItemUniqueScope.World, 60, true),
            LootOwnership = new(ItemLootOwnershipMode.Personal, 120, true, true),
            Recipes = [new(StableItemGuidUx4S(blueprintId.Value, "recipe:repair"), "repair-ember-compass",
                [new("tag:ember-metal", 2, true, 1)], "forge", "smithing", 10, 1, 6, [], provenance)],
            Generator = new(true, 6, 1, 2, ["relic"],
                new Dictionary<string, int> { ["common"] = 80, ["rare"] = 20 },
                [new(StableItemGuidUx4S(blueprintId.Value, "affix:keen"), "keen", "relic", 2, 5,
                    "common", [], ["critical"], provenance)], 17)
        };
    }

    private static StudioItemDraftUx4S FreezeItemDraftUx4S(StudioItemDraftUx4S draft) => draft;
    private static StudioItemPreviewUx4S FailedItemUx4S(string message, string code) => new(false, false,
        HumanItemMessageUx4S(code, message), string.Empty, string.Empty,
        [new(code, "items/preview", HumanItemMessageUx4S(code, message))], "Fondation non évaluée.",
        "Gameplay non évalué.", "Profil avancé non évalué.", "Opérations non évaluées.", "Runtime non évalué.");
    private static string HumanItemMessageUx4S(string? code, string fallback) => code switch
    {
        "command.revision_conflict" => "Le projet a changé ; reconstruisez l’aperçu.",
        "item.foundation.type_migration_preview_required" => "Le type immuable change avec pertes ; acceptez explicitement la migration.",
        "blueprint.palette.read_only" => "Cet objet provient d’un pack installé en lecture seule.",
        "blueprint.slug_duplicate" => "Ce slug est déjà utilisé par un autre blueprint.",
        _ => fallback,
    };
    private static string DiagnosticPathUx4S(string message)
    {
        var separator = message.IndexOf(':'); return separator > 0 ? message[..separator] : "items/preview";
    }
    private static string NextItemSlugUx4S(ProjectDefinition project)
    {
        const string root = "boussole-de-braise"; var slug = root; var index = 2;
        while (project.Blueprints.Any(item => item.Slug == slug)) slug = $"{root}-{index++}";
        return slug;
    }
    private static string FingerprintItemProjectUx4S(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));
    private static string FingerprintItemDraftUx4S(StudioItemDraftUx4S draft) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(draft))));
    internal static Guid StableItemGuidUx4S(Guid root, string discriminator)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{root:D}:{discriminator}"));
        Span<byte> value = stackalloc byte[16]; hash.AsSpan(0, 16).CopyTo(value);
        value[6] = (byte)((value[6] & 0x0f) | 0x40); value[8] = (byte)((value[8] & 0x3f) | 0x80);
        return new Guid(value);
    }
    private static bool DeleteItemRootUx4S(string root)
    {
        try { if (Directory.Exists(root)) Directory.Delete(root, true); return !Directory.Exists(root); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return false; }
    }
}
