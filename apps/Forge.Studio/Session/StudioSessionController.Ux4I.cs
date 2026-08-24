using System.Security.Cryptography;
using System.Text;
using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Creatures;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Combat;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SetCreatureLootHandler _setCreatureLootUx4I = new(new SystemClock());
    private StudioCreatureLootDraftUx4I? _approvedCreatureLootDraftUx4I;
    private CreatureLootProfile? _approvedCreatureLootCandidateUx4I;
    private ProjectRevision? _approvedCreatureLootRevisionUx4I;
    private string? _approvedCreatureLootProjectFingerprintUx4I;
    private string? _approvedCreatureLootDraftFingerprintUx4I;
    private string? _approvedCreatureLootCandidateFingerprintUx4I;

    internal IReadOnlyList<StudioCreatureLootChoiceUx4I> CreatureLootChoicesUx4I => Project?.Blueprints
        .Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
        .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
        .ThenBy(item => item.Slug, StringComparer.Ordinal)
        .Select(item => new StudioCreatureLootChoiceUx4I(item.Id,
            $"{item.DisplayName} · {item.Kind.ToString().ToLowerInvariant()} · " +
            $"{item.CreatureLoot?.Entries.Count ?? 0} entrée(s)", item.CreatureLoot is not null,
            item.Combat?.Loot.Count > 0, item.CreatureProgression is not null, item.IsReadOnly()))
        .ToArray() ?? [];

    internal IReadOnlyList<StudioCreatureLootItemChoiceUx4I> CreatureLootItemChoicesUx4I(BlueprintId id)
    {
        var project = Project;
        var blueprint = project?.Blueprints.SingleOrDefault(item => item.Id == id);
        if (project is null || blueprint?.Combat is null) return [];
        return blueprint.Combat.Loot.Select(item => item.ItemSlug).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).Select(slug =>
            {
                var item = project.Blueprints.FirstOrDefault(candidate => candidate.Kind == BlueprintKind.Item &&
                    string.Equals(candidate.Slug, slug, StringComparison.Ordinal));
                return new StudioCreatureLootItemChoiceUx4I(slug,
                    item is null ? $"Objet de table de combat · {slug}" : $"{item.DisplayName} · {slug}");
            }).ToArray();
    }

    internal IReadOnlyList<StudioCreatureLootSkillChoiceUx4I> CreatureLootSkillChoicesUx4I(BlueprintId id) =>
        Project?.Blueprints.SingleOrDefault(item => item.Id == id)?.CreatureProgression?.Skills
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new StudioCreatureLootSkillChoiceUx4I(item.SkillId,
                $"{item.DisplayName} · {item.SkillId}")).ToArray() ?? [];

    internal StudioCreatureLootDraftUx4I? CreateCreatureLootDraftUx4I(BlueprintId blueprintId)
    {
        var project = Project; var history = History;
        var blueprint = project?.Blueprints.SingleOrDefault(item => item.Id == blueprintId);
        if (project is null || history is null || blueprint is null || blueprint.IsReadOnly() ||
            blueprint.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn) ||
            blueprint.Combat?.Loot.Count is not > 0) return null;
        var profile = blueprint.CreatureLoot ?? CreatureLootProfile.CreateDefault(
            StableCreatureRulesIdUx4F(blueprint.Id.Value, "ux4i-loot-profile"), "studio:ux4-i");
        return new(history.State.Revision, FingerprintProjectUx4I(project), blueprint.Id, blueprint, profile,
            profile.RequiredAtRuntime, profile.Entries, profile.Theft, profile.Corpse);
    }

    internal StudioCreatureLootPreviewUx4I PreviewCreatureLootUx4I(StudioCreatureLootDraftUx4I draft)
    {
        ArgumentNullException.ThrowIfNull(draft); CancelCreatureLootUx4I();
        var project = Project; var history = History;
        if (project is null || history is null)
            return FailedCreatureLootUx4I("Aucun module n’est ouvert.", "studio.creature.project_missing");
        if (history.State.Revision != draft.BaseRevision ||
            !string.Equals(FingerprintProjectUx4I(project), draft.SourceProjectFingerprint, StringComparison.Ordinal))
            return FailedCreatureLootUx4I("Le projet a changé ; reconstruisez l’aperçu.",
                "command.revision_conflict");
        var source = project.Blueprints.SingleOrDefault(item => item.Id == draft.BlueprintId);
        if (source is null) return FailedCreatureLootUx4I("La créature sélectionnée n’existe plus.",
            "creature.loot.blueprint_missing");
        var candidate = CreateCreatureLootCandidateUx4I(draft);
        var items = (source.Combat?.Loot ?? []).Select(item => item.ItemSlug).ToHashSet(StringComparer.Ordinal);
        var skills = (source.CreatureProgression?.Skills ?? []).Select(item => item.SkillId)
            .ToHashSet(StringComparer.Ordinal);
        var validation = CreatureLootValidator.Validate(candidate,
            $"blueprints/{source.Id}/creatureLoot", items, skills);
        if (validation.Count > 0)
            return new(false, false, "Le profil de loot est invalide.", string.Empty, string.Empty,
                validation.Select(CreatureLootDiagnosticUx4I).ToArray(), DropSummaryUx4I(draft),
                TheftSummaryUx4I(draft), CorpseSummaryUx4I(draft), "Runtime non évalué.");
        var dryRun = _setCreatureLootUx4I.Handle(history.State, new(CommandId.From(Guid.NewGuid()),
            history.State.Revision, draft.BlueprintId, candidate, DryRun: true));
        if (!dryRun.Succeeded)
            return new(false, false, "La commande canonique refuse cet aperçu.", string.Empty, string.Empty,
                CreatureLootDiagnosticsUx4I(dryRun.Diagnostics), DropSummaryUx4I(draft), TheftSummaryUx4I(draft),
                CorpseSummaryUx4I(draft), "Runtime non évalué.");
        try
        {
            var service = new CreatureLootScriptService(candidate, source.Combat?.Loot ?? [], skills);
            var snapshot = service.Resolve(service.Handle);
            var death = service.ResolveDeath(service.Handle, .2, "player.preview", "party.preview",
                DateTimeOffset.UnixEpoch);
            var theft = candidate.Theft.Enabled
                ? service.ResolvePickpocket(service.Handle,
                    StableCreatureRulesIdUx4F(source.Id.Value, "ux4i-preview-theft"), .2,
                    "player.preview", "party.preview", candidate.Theft.Difficulty + 1, 0, true,
                    DateTimeOffset.UnixEpoch, null)
                : null;
            var frozen = FreezeCreatureLootDraftUx4I(draft);
            _approvedCreatureLootDraftUx4I = frozen; _approvedCreatureLootCandidateUx4I = candidate;
            _approvedCreatureLootRevisionUx4I = history.State.Revision;
            _approvedCreatureLootProjectFingerprintUx4I = FingerprintProjectUx4I(project);
            _approvedCreatureLootDraftFingerprintUx4I = FingerprintCreatureLootDraftUx4I(frozen);
            _approvedCreatureLootCandidateFingerprintUx4I =
                FingerprintCreatureLootCandidateUx4I(project, draft.BlueprintId, candidate);
            var runtime = $"Projection typée : {snapshot.Entries.Count} entrée(s), " +
                          $"cadavre={(death.Corpse.Created ? death.Corpse.Mode : "aucun")}, " +
                          $"pickpocket={(theft is null ? "désactivé" : theft.Succeeded ? "accordé" : "refusé")}.";
            return new(true, true, dryRun.Patch?.Summary ?? "Prévisualiser le loot.",
                dryRun.Patch?.Summary ?? "Définir le loot de créature.",
                dryRun.Inverse?.Summary ?? "Restaurer le loot précédent.", [], DropSummaryUx4I(draft),
                TheftSummaryUx4I(draft), CorpseSummaryUx4I(draft), runtime);
        }
        catch (Exception exception)
        {
            return FailedCreatureLootUx4I($"La projection runtime refuse le profil : {exception.Message}",
                "creature.loot.runtime_invalid");
        }
    }

    internal StudioCreatureLootActionResultUx4I ApplyApprovedCreatureLootUx4I(
        StudioCreatureLootDraftUx4I currentDraft)
    {
        ArgumentNullException.ThrowIfNull(currentDraft);
        var project = Project; var history = History; var approved = _approvedCreatureLootDraftUx4I;
        var candidate = _approvedCreatureLootCandidateUx4I;
        if (project is null || history is null || approved is null || candidate is null ||
            _approvedCreatureLootRevisionUx4I is null || _approvedCreatureLootProjectFingerprintUx4I is null ||
            _approvedCreatureLootDraftFingerprintUx4I is null || _approvedCreatureLootCandidateFingerprintUx4I is null)
            return new(false, "Prévisualisez le brouillon avant de l’appliquer.");
        var stale = history.State.Revision != _approvedCreatureLootRevisionUx4I ||
                    !string.Equals(FingerprintProjectUx4I(project), _approvedCreatureLootProjectFingerprintUx4I,
                        StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureLootDraftUx4I(approved),
                        _approvedCreatureLootDraftFingerprintUx4I, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureLootDraftUx4I(currentDraft),
                        _approvedCreatureLootDraftFingerprintUx4I, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureLootCandidateUx4I(project, currentDraft.BlueprintId, candidate),
                        _approvedCreatureLootCandidateFingerprintUx4I, StringComparison.Ordinal);
        if (stale) { CancelCreatureLootUx4I(); return new(false, "Le brouillon ou le projet a changé."); }
        var recheck = _setCreatureLootUx4I.Handle(history.State, new(CommandId.From(Guid.NewGuid()),
            history.State.Revision, currentDraft.BlueprintId, candidate, DryRun: true));
        if (!recheck.Succeeded) { CancelCreatureLootUx4I(); return new(false, "La commande refuse le brouillon."); }
        var result = _setCreatureLootUx4I.Handle(history.State, new(CommandId.From(Guid.NewGuid()),
            history.State.Revision, currentDraft.BlueprintId, candidate, DryRun: false));
        CancelCreatureLootUx4I();
        if (!result.Succeeded) return new(false, result.Diagnostics.FirstOrDefault()?.Message ?? "Application refusée.");
        history.Record(result); InvalidateContentIndexes(); return new(true, result.Patch?.Summary ?? "Loot appliqué.");
    }

    internal StudioCreatureLootActionResultUx4I UndoCreatureLootUx4I()
    { CancelCreatureLootUx4I(); var result = Undo(); return new(result.Succeeded, result.Message); }
    internal StudioCreatureLootActionResultUx4I RedoCreatureLootUx4I()
    { CancelCreatureLootUx4I(); var result = Redo(); return new(result.Succeeded, result.Message); }

    internal void CancelCreatureLootUx4I()
    {
        _approvedCreatureLootDraftUx4I = null; _approvedCreatureLootCandidateUx4I = null;
        _approvedCreatureLootRevisionUx4I = null; _approvedCreatureLootProjectFingerprintUx4I = null;
        _approvedCreatureLootDraftFingerprintUx4I = null; _approvedCreatureLootCandidateFingerprintUx4I = null;
    }

    internal void CloseUx4I() => CancelCreatureLootUx4I();
    internal StudioCreatureLootApprovalSnapshotUx4I CaptureCreatureLootApprovalUx4I() => new(
        _approvedCreatureLootDraftUx4I, _approvedCreatureLootCandidateUx4I, _approvedCreatureLootRevisionUx4I,
        _approvedCreatureLootProjectFingerprintUx4I, _approvedCreatureLootDraftFingerprintUx4I,
        _approvedCreatureLootCandidateFingerprintUx4I);
    internal void RestoreCreatureLootApprovalUx4I(StudioCreatureLootApprovalSnapshotUx4I snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot); _approvedCreatureLootDraftUx4I = snapshot.Draft;
        _approvedCreatureLootCandidateUx4I = snapshot.Candidate; _approvedCreatureLootRevisionUx4I = snapshot.Revision;
        _approvedCreatureLootProjectFingerprintUx4I = snapshot.ProjectFingerprint;
        _approvedCreatureLootDraftFingerprintUx4I = snapshot.DraftFingerprint;
        _approvedCreatureLootCandidateFingerprintUx4I = snapshot.CandidateFingerprint;
    }

    internal StudioCreatureLootEvidenceProjectUx4I? CreateUx4IEvidenceProject()
    {
        var project = Project; if (project is null) return null;
        var real = project.Blueprints.Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn &&
                                                   !item.IsReadOnly() && item.CreatureLoot is not null)
            .FirstOrDefault(item => CreatureLootValidator.Validate(item.CreatureLoot!,
                availableItemSlugs: (item.Combat?.Loot ?? []).Select(drop => drop.ItemSlug)
                    .ToHashSet(StringComparer.Ordinal),
                availableSkillIds: (item.CreatureProgression?.Skills ?? []).Select(skill => skill.SkillId)
                    .ToHashSet(StringComparer.Ordinal)).Count == 0);
        if (real is not null) return new(project, real.Id, true, false);
        var source = CreateUx4HEvidenceProject(); if (source is null) return null;
        var target = source.Project.Blueprints.Single(item => item.Id == source.BlueprintId);
        var combat = new CombatProfile(target.Combat?.Stats ?? new CombatStats(30, 10, 5, 4, "neutral", "hostile"),
            target.Combat?.Abilities ?? [], [new LootDrop("ux4i-relic", 1), new LootDrop("ux4i-coin", 3, .75)]);
        var baseProgression = target.CreatureProgression ?? CreateCanonicalCreatureProgressionUx4G(target);
        var skills = baseProgression.Skills
            .Where(item => item.SkillId is not ("sleight" or "awareness"))
            .Concat([
                new("sleight", "Larcin", "agility", 2, 10, 0, true, false, 10),
                new("awareness", "Vigilance", "insight", 2, 10, 0, true, false, 10),
            ]).ToArray();
        var progression = new CreatureProgressionProfile(baseProgression.Id, baseProgression.SchemaVersion,
            baseProgression.Provenance, baseProgression.RulesetId, baseProgression.AllowMultipleTracks,
            baseProgression.RequiredAtRuntime, baseProgression.Tracks, baseProgression.DispositionAxes, skills,
            baseProgression.Talents, baseProgression.Loadout, baseProgression.Overrides);
        var loot = new CreatureLootProfile(StableCreatureRulesIdUx4F(target.Id.Value, "ux4i-loot-profile"),
            CreatureLootProfile.CurrentSchemaVersion, "studio:ux4-i-evidence", true,
            [
                new(StableCreatureRulesIdUx4F(target.Id.Value, "ux4i-relic"), "ux4i-relic", "relic", 1, 1,
                    1, true, false, LootOwnershipMode.Party, LootInstancingMode.PerParty, null),
                new(StableCreatureRulesIdUx4F(target.Id.Value, "ux4i-coin"), "ux4i-coin", "pouch", 1, 3,
                    .75, true, true, LootOwnershipMode.Killer, LootInstancingMode.PerPlayer, null),
            ], new(true, "sleight", "awareness", 12, 10, true, "crime.theft", 30, "loot.pickpocket"),
            new(CreatureCorpseMode.RagdollContainer, 60, "corpse.standard", "ragdoll.humanoid", true,
                "party", 8, CorpseCleanupMode.Timed), new Dictionary<string, string>());
        var updated = new BlueprintDefinition(target.Id, target.Slug, target.DisplayName, target.Kind, combat,
            target.PaletteOrigin, target.IsFavorite, target.Provenance, target.CreaturePresentation, progression, loot,
            target.CreatureRoleBehavior, target.CreatureEvents, target.ItemAdvanced, target.Authoring,
            target.CreatureFoundation, target.ItemFoundation);
        var fallback = new ProjectDefinition(source.Project.Id, source.Project.Title, source.Project.Module,
            source.Project.Blueprints.Select(item => item.Id == updated.Id ? updated : item).ToArray(),
            source.Project.Areas, source.Project.Dialogues, source.Project.Quests);
        return new(fallback, updated.Id, false, true);
    }

    internal StudioUx4ISmokeResult RunUx4IContractSmoke()
    {
        var result = new StudioUx4ISmokeResult(false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false);
        var evidence = CreateUx4IEvidenceProject(); if (evidence is null) return result;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4i-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController(); var reopened = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(root, evidence.Project).Succeeded || !editing.Open(root).Succeeded) return result;
            var explicitSelection = editing.CreatureLootChoicesUx4I.Count > 0;
            var draft = editing.CreateCreatureLootDraftUx4I(evidence.BlueprintId);
            if (draft is null) return result with { ExplicitSelection = explicitSelection };
            var items = editing.CreatureLootItemChoicesUx4I(evidence.BlueprintId);
            var skills = editing.CreatureLootSkillChoicesUx4I(evidence.BlueprintId);
            var changed = ChangedCreatureLootDraftUx4I(draft, items, skills);
            var sourceProject = editing.Project!; var sourceHistory = editing.History!;
            var preview = editing.PreviewCreatureLootUx4I(changed);
            var dryRun = preview.Succeeded && ReferenceEquals(sourceProject, editing.Project) &&
                         ReferenceEquals(sourceHistory, editing.History) && sourceHistory.State.Revision == draft.BaseRevision;
            var badEntries = changed.Entries.ToArray(); badEntries[0] = badEntries[0] with { Chance = 2 };
            var invalid = editing.PreviewCreatureLootUx4I(changed with { Entries = badEntries });
            var invalidBlocked = !invalid.Succeeded && invalid.Diagnostics.Any(item => item.Code == "creature.loot.entry_invalid");
            badEntries = changed.Entries.ToArray(); badEntries[0] = badEntries[0] with { ItemSlug = "missing-item" };
            var missing = editing.PreviewCreatureLootUx4I(changed with { Entries = badEntries });
            var missingBlocked = !missing.Succeeded && missing.Diagnostics.Any(item => item.Code == "creature.loot.item_missing");

            var approvedForDraft = editing.PreviewCreatureLootUx4I(changed);
            var intermediate = ToggleCreatureLootUx4I(editing, evidence.BlueprintId);
            var staleDraft = editing.PreviewCreatureLootUx4I(changed);
            var staleDraftBlocked = approvedForDraft.Succeeded && intermediate && !staleDraft.Succeeded;
            var fresh = editing.CreateCreatureLootDraftUx4I(evidence.BlueprintId)!;
            var freshChanged = ChangedCreatureLootDraftUx4I(fresh,
                editing.CreatureLootItemChoicesUx4I(evidence.BlueprintId),
                editing.CreatureLootSkillChoicesUx4I(evidence.BlueprintId));
            var approved = editing.PreviewCreatureLootUx4I(freshChanged);
            var secondIntermediate = ToggleCreatureLootUx4I(editing, evidence.BlueprintId);
            var staleApproval = editing.ApplyApprovedCreatureLootUx4I(freshChanged);
            var staleApprovalBlocked = approved.Succeeded && secondIntermediate && !staleApproval.Succeeded;

            fresh = editing.CreateCreatureLootDraftUx4I(evidence.BlueprintId)!;
            freshChanged = ChangedCreatureLootDraftUx4I(fresh,
                editing.CreatureLootItemChoicesUx4I(evidence.BlueprintId),
                editing.CreatureLootSkillChoicesUx4I(evidence.BlueprintId));
            var expected = CreateCreatureLootCandidateUx4I(freshChanged);
            var finalPreview = editing.PreviewCreatureLootUx4I(freshChanged);
            var beforeApply = editing.Project!;
            var beforeBlueprint = beforeApply.Blueprints.Single(item => item.Id == evidence.BlueprintId);
            var apply = editing.ApplyApprovedCreatureLootUx4I(freshChanged);
            var appliedProject = editing.Project!;
            var appliedBlueprint = appliedProject.Blueprints.Single(item => item.Id == evidence.BlueprintId);
            var applied = appliedBlueprint.CreatureLoot;
            var drops = applied?.Entries.Any(item => item.DropOnDeath && item.MaximumQuantity >= 2) == true;
            var pickpocket = applied?.Theft is { Enabled: true, RequireServerConsent: true } &&
                             applied.Entries.Any(item => item.Pickpocketable);
            var corpse = applied?.Corpse is
            {
                Mode: CreatureCorpseMode.RagdollContainer,
                CleanupMode: CorpseCleanupMode.Timed, AllowResurrection: true
            };
            var neighbors = ReferenceEquals(beforeBlueprint.Combat, appliedBlueprint.Combat) &&
                            ReferenceEquals(beforeBlueprint.CreaturePresentation, appliedBlueprint.CreaturePresentation) &&
                            ReferenceEquals(beforeBlueprint.CreatureProgression, appliedBlueprint.CreatureProgression) &&
                            ReferenceEquals(beforeBlueprint.CreatureRoleBehavior, appliedBlueprint.CreatureRoleBehavior) &&
                            ReferenceEquals(beforeBlueprint.CreatureEvents, appliedBlueprint.CreatureEvents) &&
                            ReferenceEquals(beforeBlueprint.CreatureFoundation, appliedBlueprint.CreatureFoundation) &&
                            ProjectJsonSerializer.Serialize(WithCreatureLootUx4I(beforeApply, evidence.BlueprintId, expected)) ==
                            ProjectJsonSerializer.Serialize(appliedProject);
            var canonical = finalPreview.Succeeded && apply.Succeeded && applied is not null;
            var undo = editing.UndoCreatureLootUx4I(); var undone = undo.Succeeded && ReferenceEquals(beforeApply, editing.Project);
            var redo = editing.RedoCreatureLootUx4I(); var redone = redo.Succeeded && ReferenceEquals(appliedProject, editing.Project);
            var saved = redone && editing.Save().Succeeded;
            var persisted = saved && reopened.Open(root).Succeeded &&
                            ProjectJsonSerializer.Serialize(reopened.Project!) == ProjectJsonSerializer.Serialize(appliedProject);
            result = new(canonical && explicitSelection && drops && pickpocket && corpse && invalidBlocked &&
                         missingBlocked && dryRun && staleDraftBlocked && staleApprovalBlocked && undone && redone &&
                         persisted && neighbors, explicitSelection, drops, pickpocket, corpse, invalidBlocked,
                missingBlocked, dryRun, staleDraftBlocked, staleApprovalBlocked, canonical, undone && redone,
                persisted, neighbors, false);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX4I_CONTRACT_FAILED stage=exception " + exception);
        }
        finally
        {
            reopened.Close(); editing.Close(); var released = DeleteUx4ISmokeRoot(root);
            result = result with { Succeeded = result.Succeeded && released, TemporaryWorkspaceReleased = released };
        }
        return result;
    }

    internal static StudioCreatureLootDraftUx4I ChangedCreatureLootDraftUx4I(
        StudioCreatureLootDraftUx4I draft, IReadOnlyList<StudioCreatureLootItemChoiceUx4I> items,
        IReadOnlyList<StudioCreatureLootSkillChoiceUx4I> skills)
    {
        var entries = draft.Entries.ToList(); var item = items[0].ItemSlug;
        var index = entries.FindIndex(candidate => candidate.ItemSlug == item);
        var entry = index >= 0 ? entries[index] : new CreatureLootProfile.LootEntryPolicy(
            StableCreatureRulesIdUx4F(draft.BlueprintId.Value, "ux4i-changed-entry"), item, "pouch", 1, 1,
            1, true, false, LootOwnershipMode.Killer, LootInstancingMode.PerPlayer, null);
        entry = entry with
        {
            MinimumQuantity = 1,
            MaximumQuantity = 2,
            Chance = .8,
            DropOnDeath = true,
            Pickpocketable = true,
            Ownership = LootOwnershipMode.Party,
            Instancing = LootInstancingMode.PerParty,
            FixedOwnerId = null
        };
        if (index >= 0) entries[index] = entry; else entries.Add(entry);
        var skill = skills.FirstOrDefault()?.SkillId ?? "awareness";
        var perception = skills.Skip(1).FirstOrDefault()?.SkillId ?? skill;
        return draft with
        {
            RequiredAtRuntime = true,
            Entries = entries.ToArray(),
            Theft = new(true, skill, perception, 11, 9, true, "crime.ux4i-theft", 25, "loot.ux4i-pickpocket"),
            Corpse = new(CreatureCorpseMode.RagdollContainer, 75, "corpse.ux4i", "ragdoll.ux4i", true,
                "party", 12, CorpseCleanupMode.Timed),
        };
    }

    private static CreatureLootProfile CreateCreatureLootCandidateUx4I(StudioCreatureLootDraftUx4I draft) => new(
        draft.SourceLoot.Id, draft.SourceLoot.SchemaVersion, draft.SourceLoot.Provenance, draft.RequiredAtRuntime,
        draft.Entries, draft.Theft, draft.Corpse, draft.SourceLoot.Overrides);

    private static ProjectDefinition WithCreatureLootUx4I(ProjectDefinition project, BlueprintId id,
        CreatureLootProfile loot)
    {
        var blueprints = project.Blueprints.Select(item => item.Id != id ? item : new BlueprintDefinition(item.Id,
            item.Slug, item.DisplayName, item.Kind, item.Combat, item.PaletteOrigin, item.IsFavorite, item.Provenance,
            item.CreaturePresentation, item.CreatureProgression, loot, item.CreatureRoleBehavior, item.CreatureEvents,
            item.ItemAdvanced, item.Authoring, item.CreatureFoundation, item.ItemFoundation)).ToArray();
        return new(project.Id, project.Title, project.Module, blueprints, project.Areas, project.Dialogues, project.Quests);
    }

    private static bool ToggleCreatureLootUx4I(StudioSessionController session, BlueprintId id)
    {
        var project = session.Project; var history = session.History;
        var source = project?.Blueprints.SingleOrDefault(item => item.Id == id)?.CreatureLoot;
        if (project is null || history is null || source is null) return false;
        var changed = new CreatureLootProfile(source.Id, source.SchemaVersion, source.Provenance,
            !source.RequiredAtRuntime, source.Entries, source.Theft, source.Corpse, source.Overrides);
        var result = new SetCreatureLootHandler(new SystemClock()).Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, id, changed, DryRun: false));
        if (!result.Succeeded) return false; history.Record(result); session.InvalidateContentIndexes(); return true;
    }

    private static StudioCreatureLootPreviewUx4I FailedCreatureLootUx4I(string message, string code) =>
        new(false, false, message, string.Empty, string.Empty,
            [new(code, "creatureLoot", HumanCreatureLootMessageUx4I(code, message))], string.Empty, string.Empty,
            string.Empty, "Runtime non évalué.");
    private static IReadOnlyList<StudioCreatureLootDiagnosticUx4I> CreatureLootDiagnosticsUx4I(
        IReadOnlyList<CommandDiagnostic> diagnostics) => diagnostics.Select(item =>
        {
            var separator = item.Message.IndexOf(": ", StringComparison.Ordinal);
            var path = separator > 0 ? item.Message[..separator] : "creatureLoot";
            var message = separator > 0 ? item.Message[(separator + 2)..] : item.Message;
            return new StudioCreatureLootDiagnosticUx4I(item.Code, path,
                HumanCreatureLootMessageUx4I(item.Code, message));
        }).ToArray();
    private static StudioCreatureLootDiagnosticUx4I CreatureLootDiagnosticUx4I(CreatureLootDiagnostic item) =>
        new(item.Code, item.Path, HumanCreatureLootMessageUx4I(item.Code, item.Message));
    private static string HumanCreatureLootMessageUx4I(string code, string fallback) => code switch
    {
        "creature.loot.entry_invalid" => "Une entrée de loot contient une quantité, chance ou propriété incohérente.",
        "creature.loot.item_missing" => "Chaque objet doit provenir de la table de combat de la créature.",
        "creature.loot.theft_invalid" => "La politique de pickpocket est incomplète ou incohérente.",
        "creature.loot.theft_skill_missing" => "Les compétences de larcin et perception doivent exister sur la créature.",
        "creature.loot.corpse_invalid" => "La politique de cadavre, conteneur ou nettoyage est incohérente.",
        _ => fallback,
    };
    private static string DropSummaryUx4I(StudioCreatureLootDraftUx4I draft) =>
        $"Drops : {draft.Entries.Count} entrée(s), {draft.Entries.Count(item => item.DropOnDeath)} au décès.";
    private static string TheftSummaryUx4I(StudioCreatureLootDraftUx4I draft) => draft.Theft.Enabled
        ? $"Pickpocket : {draft.Theft.SkillId} contre {draft.Theft.PerceptionSkillId}, cooldown {draft.Theft.CooldownSeconds:R} s."
        : "Pickpocket désactivé.";
    private static string CorpseSummaryUx4I(StudioCreatureLootDraftUx4I draft) =>
        $"Cadavre : {draft.Corpse.Mode}, nettoyage {draft.Corpse.CleanupMode}, {draft.Corpse.MaximumItems} objet(s) max.";

    private static StudioCreatureLootDraftUx4I FreezeCreatureLootDraftUx4I(StudioCreatureLootDraftUx4I draft) =>
        draft with { Entries = draft.Entries.ToArray() };
    private static string FingerprintProjectUx4I(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));
    private static string FingerprintCreatureLootCandidateUx4I(ProjectDefinition project, BlueprintId id,
        CreatureLootProfile candidate) => FingerprintProjectUx4I(WithCreatureLootUx4I(project, id, candidate));
    private static string FingerprintCreatureLootDraftUx4I(StudioCreatureLootDraftUx4I draft)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(draft.BaseRevision.Value); WriteUx4I(writer, draft.SourceProjectFingerprint);
        WriteUx4I(writer, draft.BlueprintId.ToString()); writer.Write(draft.RequiredAtRuntime);
        writer.Write(draft.Entries.Count);
        foreach (var item in draft.Entries.OrderBy(item => item.ItemSlug, StringComparer.Ordinal).ThenBy(item => item.Id))
        {
            writer.Write(item.Id.ToByteArray()); WriteUx4I(writer, item.ItemSlug); WriteUx4I(writer, item.SlotId);
            writer.Write(item.MinimumQuantity); writer.Write(item.MaximumQuantity); writer.Write(item.Chance);
            writer.Write(item.DropOnDeath); writer.Write(item.Pickpocketable); writer.Write((int)item.Ownership);
            writer.Write((int)item.Instancing); WriteUx4I(writer, item.FixedOwnerId);
        }
        var theft = draft.Theft; writer.Write(theft.Enabled); WriteUx4I(writer, theft.SkillId);
        WriteUx4I(writer, theft.PerceptionSkillId); writer.Write(theft.Difficulty);
        writer.Write(theft.PerceptionDifficulty); writer.Write(theft.RequireServerConsent);
        WriteUx4I(writer, theft.CrimeId); writer.Write(theft.CooldownSeconds); WriteUx4I(writer, theft.AuditTag);
        var corpse = draft.Corpse; writer.Write((int)corpse.Mode); writer.Write(corpse.LifetimeSeconds);
        WriteUx4I(writer, corpse.ContainerId); WriteUx4I(writer, corpse.RagdollProfileId);
        writer.Write(corpse.AllowResurrection); WriteUx4I(writer, corpse.OwnershipPolicy);
        writer.Write(corpse.MaximumItems); writer.Write((int)corpse.CleanupMode); writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
    private static void WriteUx4I(BinaryWriter writer, string? value)
    { var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty); writer.Write(bytes.Length); writer.Write(bytes); }
    private static bool DeleteUx4ISmokeRoot(string root)
    {
        try
        {
            var full = Path.GetFullPath(root); var temp = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4i-smoke-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, true); return !Directory.Exists(full);
        }
        catch { return false; }
    }
}
