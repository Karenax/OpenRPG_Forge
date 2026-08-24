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
    private readonly SetCreatureProgressionHandler _setCreatureProgressionUx4G = new(new SystemClock());
    private StudioCreatureProgressionDraftUx4G? _approvedCreatureProgressionDraftUx4G;
    private CreatureProgressionProfile? _approvedCreatureProgressionCandidateUx4G;
    private ProjectRevision? _approvedCreatureProgressionRevisionUx4G;
    private string? _approvedCreatureProgressionProjectFingerprintUx4G;
    private string? _approvedCreatureProgressionDraftFingerprintUx4G;
    private string? _approvedCreatureProgressionCandidateFingerprintUx4G;

    internal IReadOnlyList<StudioCreatureProgressionChoiceUx4G> CreatureProgressionChoicesUx4G => Project?.Blueprints
        .Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
        .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
        .ThenBy(item => item.Slug, StringComparer.Ordinal)
        .Select(item => new StudioCreatureProgressionChoiceUx4G(item.Id,
            $"{item.DisplayName} · {(item.Kind == BlueprintKind.PlayerSpawn ? "joueur" : "créature")} · " +
            (item.CreatureProgression is null ? "progression à initialiser" :
                $"niveau {item.CreatureProgression.Tracks.Sum(track => track.Level)}"),
            item.CreatureProgression is not null, item.Combat?.Abilities.Count > 0, item.IsReadOnly()))
        .ToArray() ?? [];

    internal IReadOnlyList<StudioCreatureAbilityChoiceUx4G> CreatureAbilityChoicesUx4G(BlueprintId blueprintId) =>
        Project?.Blueprints.SingleOrDefault(item => item.Id == blueprintId)?.Combat?.Abilities
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => new StudioCreatureAbilityChoiceUx4G(item.Slug, $"{item.DisplayName} · {item.Slug}"))
            .ToArray() ?? [];

    internal StudioCreatureProgressionDraftUx4G? CreateCreatureProgressionDraftUx4G(BlueprintId blueprintId)
    {
        var project = Project;
        var history = History;
        var blueprint = project?.Blueprints.SingleOrDefault(item => item.Id == blueprintId);
        if (project is null || history is null || blueprint is null || blueprint.IsReadOnly() ||
            blueprint.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn)) return null;
        var source = blueprint.CreatureProgression ?? CreatureProgressionProfile.CreateDefault(
            StableCreatureProgressionIdUx4G(blueprint.Id.Value, "profile"), "studio:ux4-g");
        return new(history.State.Revision, FingerprintProjectUx4G(project), blueprint.Id, blueprint, source,
            source.RulesetId, source.AllowMultipleTracks, source.RequiredAtRuntime, source.Tracks.ToArray(),
            source.DispositionAxes.ToArray(), source.Skills.ToArray(), source.Talents.ToArray(),
            source.Loadout.ProviderId, source.Loadout.Mode, source.Loadout.Capacity,
            source.Loadout.LearnedAbilityIds.ToArray(), source.Loadout.PreparedAbilities.ToArray());
    }

    internal StudioCreatureProgressionPreviewUx4G PreviewCreatureProgressionUx4G(
        StudioCreatureProgressionDraftUx4G draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelCreatureProgressionUx4G();
        var project = Project;
        var history = History;
        if (project is null || history is null)
            return FailedCreatureProgressionPreviewUx4G("Aucun module n’est ouvert.", "studio.creature.project_missing");
        if (history.State.Revision != draft.BaseRevision ||
            !string.Equals(FingerprintProjectUx4G(project), draft.SourceProjectFingerprint, StringComparison.Ordinal))
            return FailedCreatureProgressionPreviewUx4G(
                "Le projet a changé depuis l’ouverture de la fiche. Rechargez-la avant de prévisualiser.",
                SetCreatureProgressionHandler.RevisionConflictCode);

        var candidate = CreateCreatureProgressionCandidateUx4G(draft);
        var abilities = AvailableAbilityIdsUx4G(draft.SourceBlueprint);
        var validation = CreatureProgressionValidator.Validate(candidate,
            $"blueprints/{draft.BlueprintId}/creatureProgression", abilities);
        if (validation.Count > 0)
            return new(false, false, "La progression contient des valeurs invalides.", string.Empty, string.Empty,
                validation.Select(CreatureProgressionDiagnosticUx4G).ToArray(), TrackSummaryUx4G(draft),
                DispositionSummaryUx4G(draft), SkillSummaryUx4G(draft), TalentSummaryUx4G(draft),
                LoadoutSummaryUx4G(draft), "Runtime non évalué.");

        var dryRun = _setCreatureProgressionUx4G.Handle(history.State, new(CommandId.From(Guid.NewGuid()),
            draft.BaseRevision, draft.BlueprintId, candidate, DryRun: true));
        if (!dryRun.Succeeded)
            return new(false, false, "La commande canonique refuse cette progression.", string.Empty, string.Empty,
                CreatureProgressionDiagnosticsUx4G(dryRun.Diagnostics), TrackSummaryUx4G(draft),
                DispositionSummaryUx4G(draft), SkillSummaryUx4G(draft), TalentSummaryUx4G(draft),
                LoadoutSummaryUx4G(draft), "Runtime non évalué.");

        var runtime = CreatureProgressionRuntimeResolver.Resolve(candidate, abilities, required: true);
        if (!runtime.Succeeded)
            return new(false, false, "La projection runtime refuse cette progression.", string.Empty, string.Empty,
                runtime.Diagnostics.Select(CreatureProgressionDiagnosticUx4G).ToArray(), TrackSummaryUx4G(draft),
                DispositionSummaryUx4G(draft), SkillSummaryUx4G(draft), TalentSummaryUx4G(draft),
                LoadoutSummaryUx4G(draft), "Projection runtime refusée.");

        var script = new CreatureProgressionScriptService(candidate, abilities);
        var snapshot = script.Resolve(script.Handle);
        var firstSkill = candidate.Skills.FirstOrDefault();
        var skillCheck = firstSkill is null ? (double?)null :
            script.ResolveSkillCheck(script.Handle, firstSkill.SkillId, 2, 1);
        var frozen = FreezeCreatureProgressionDraftUx4G(draft);
        _approvedCreatureProgressionDraftUx4G = frozen;
        _approvedCreatureProgressionCandidateUx4G = candidate;
        _approvedCreatureProgressionRevisionUx4G = history.State.Revision;
        _approvedCreatureProgressionProjectFingerprintUx4G = FingerprintProjectUx4G(project);
        _approvedCreatureProgressionDraftFingerprintUx4G = FingerprintCreatureProgressionDraftUx4G(frozen);
        _approvedCreatureProgressionCandidateFingerprintUx4G =
            FingerprintCreatureProgressionCandidateUx4G(project, draft.BlueprintId, candidate);
        return new(true, true, "Progression valide · prête à être appliquée.",
            dryRun.Patch?.Summary ?? "Remplacer la progression de créature.",
            dryRun.Inverse?.Summary ?? "Restaurer la progression précédente.", [], TrackSummaryUx4G(draft),
            DispositionSummaryUx4G(draft), SkillSummaryUx4G(draft), TalentSummaryUx4G(draft),
            LoadoutSummaryUx4G(draft),
            $"Projection typée · niveau {snapshot.TotalLevel} · {snapshot.PreparedAbilityIds.Count} pouvoir(s) préparé(s)" +
            (skillCheck is null ? string.Empty : $" · test {firstSkill!.DisplayName} = {skillCheck:0.##}") +
            " · aucune montée de niveau ni capacité exécutée");
    }

    internal StudioCreatureProgressionActionResultUx4G ApplyApprovedCreatureProgressionUx4G(
        StudioCreatureProgressionDraftUx4G currentDraft)
    {
        ArgumentNullException.ThrowIfNull(currentDraft);
        var project = Project;
        var history = History;
        var approved = _approvedCreatureProgressionDraftUx4G;
        var candidate = _approvedCreatureProgressionCandidateUx4G;
        if (project is null || history is null || approved is null || candidate is null ||
            _approvedCreatureProgressionRevisionUx4G is null ||
            _approvedCreatureProgressionProjectFingerprintUx4G is null ||
            _approvedCreatureProgressionDraftFingerprintUx4G is null ||
            _approvedCreatureProgressionCandidateFingerprintUx4G is null)
            return new(false, "Prévisualisez la progression avant de l’appliquer.");
        var stale = history.State.Revision != _approvedCreatureProgressionRevisionUx4G ||
                    !string.Equals(FingerprintProjectUx4G(project),
                        _approvedCreatureProgressionProjectFingerprintUx4G, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureProgressionDraftUx4G(approved),
                        _approvedCreatureProgressionDraftFingerprintUx4G, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureProgressionDraftUx4G(currentDraft),
                        _approvedCreatureProgressionDraftFingerprintUx4G, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureProgressionCandidateUx4G(project, currentDraft.BlueprintId,
                            candidate), _approvedCreatureProgressionCandidateFingerprintUx4G,
                        StringComparison.Ordinal);
        if (stale)
        {
            CancelCreatureProgressionUx4G();
            return new(false, "Le projet ou la fiche a changé depuis l’aperçu. Prévisualisez à nouveau.");
        }
        var recheck = _setCreatureProgressionUx4G.Handle(history.State, new(CommandId.From(Guid.NewGuid()),
            history.State.Revision, currentDraft.BlueprintId, candidate, DryRun: true));
        if (!recheck.Succeeded)
        {
            CancelCreatureProgressionUx4G();
            return new(false, "La revalidation canonique a refusé la progression.");
        }
        var result = _setCreatureProgressionUx4G.Handle(history.State, new(CommandId.From(Guid.NewGuid()),
            history.State.Revision, currentDraft.BlueprintId, candidate, DryRun: false));
        CancelCreatureProgressionUx4G();
        if (!result.Succeeded) return new(false, "La commande n’a pas pu appliquer la progression.");
        history.Record(result);
        InvalidateContentIndexes();
        return new(true, "Progression appliquée · Ctrl+Z pour annuler.");
    }

    internal StudioCreatureProgressionActionResultUx4G UndoCreatureProgressionUx4G()
    {
        CancelCreatureProgressionUx4G();
        var result = Undo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioCreatureProgressionActionResultUx4G RedoCreatureProgressionUx4G()
    {
        CancelCreatureProgressionUx4G();
        var result = Redo();
        return new(result.Succeeded, result.Message);
    }

    internal void CancelCreatureProgressionUx4G()
    {
        _approvedCreatureProgressionDraftUx4G = null;
        _approvedCreatureProgressionCandidateUx4G = null;
        _approvedCreatureProgressionRevisionUx4G = null;
        _approvedCreatureProgressionProjectFingerprintUx4G = null;
        _approvedCreatureProgressionDraftFingerprintUx4G = null;
        _approvedCreatureProgressionCandidateFingerprintUx4G = null;
    }

    internal void CloseUx4G() => CancelCreatureProgressionUx4G();

    internal StudioCreatureProgressionApprovalSnapshotUx4G CaptureCreatureProgressionApprovalUx4G() => new(
        _approvedCreatureProgressionDraftUx4G, _approvedCreatureProgressionCandidateUx4G,
        _approvedCreatureProgressionRevisionUx4G, _approvedCreatureProgressionProjectFingerprintUx4G,
        _approvedCreatureProgressionDraftFingerprintUx4G, _approvedCreatureProgressionCandidateFingerprintUx4G);

    internal void RestoreCreatureProgressionApprovalUx4G(StudioCreatureProgressionApprovalSnapshotUx4G snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedCreatureProgressionDraftUx4G = snapshot.Draft;
        _approvedCreatureProgressionCandidateUx4G = snapshot.Candidate;
        _approvedCreatureProgressionRevisionUx4G = snapshot.Revision;
        _approvedCreatureProgressionProjectFingerprintUx4G = snapshot.ProjectFingerprint;
        _approvedCreatureProgressionDraftFingerprintUx4G = snapshot.DraftFingerprint;
        _approvedCreatureProgressionCandidateFingerprintUx4G = snapshot.CandidateFingerprint;
    }

    internal StudioCreatureProgressionEvidenceProjectUx4G? CreateUx4GEvidenceProject()
    {
        var project = Project;
        if (project is null) return null;
        var real = project.Blueprints.Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn &&
                                                    !item.IsReadOnly() && item.CreatureProgression is not null)
            .FirstOrDefault(item => CreatureProgressionValidator.Validate(item.CreatureProgression!,
                availableAbilityIds: AvailableAbilityIdsUx4G(item)).Count == 0);
        if (real is not null) return new(project, real.Id, true, false);
        var target = project.Blueprints.Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn &&
                                                     !item.IsReadOnly() && item.CreatureRoleBehavior is null)
            .OrderByDescending(item => item.Combat?.Abilities.Count ?? 0)
            .ThenBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase).FirstOrDefault();
        if (target is null) return null;
        var profile = CreateCanonicalCreatureProgressionUx4G(target);
        return new(WithCreatureProgressionUx4G(project, target.Id, profile), target.Id, false, true);
    }

    internal StudioUx4GSmokeResult RunUx4GContractSmoke()
    {
        var result = new StudioUx4GSmokeResult(false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false);
        var evidence = CreateUx4GEvidenceProject();
        if (evidence is null) return result;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4g-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(root, evidence.Project).Succeeded || !editing.Open(root).Succeeded)
                return result;
            var explicitSelection = editing.CreatureProgressionChoicesUx4G.Count > 0;
            var draft = editing.CreateCreatureProgressionDraftUx4G(evidence.BlueprintId);
            if (draft is null) return result with { ExplicitSelection = explicitSelection };
            var changed = ChangedCreatureProgressionDraftUx4G(draft);
            var sourceProject = editing.Project!;
            var sourceHistory = editing.History!;
            var preview = editing.PreviewCreatureProgressionUx4G(changed);
            var dryRun = preview.Succeeded && ReferenceEquals(sourceProject, editing.Project) &&
                         ReferenceEquals(sourceHistory, editing.History) &&
                         sourceHistory.State.Revision == draft.BaseRevision;
            var invalidTracks = changed.Tracks.ToArray();
            invalidTracks[0] = invalidTracks[0] with { Level = 0 };
            var invalid = editing.PreviewCreatureProgressionUx4G(changed with { Tracks = invalidTracks });
            var invalidBlocked = !invalid.Succeeded && invalid.Diagnostics.Any(item =>
                item.Code == "creature.progression.track_invalid");

            var approvedForDraft = editing.PreviewCreatureProgressionUx4G(changed);
            var intermediate = ToggleCreatureProgressionUx4G(editing, evidence.BlueprintId);
            var staleDraft = editing.PreviewCreatureProgressionUx4G(changed);
            var staleDraftBlocked = approvedForDraft.Succeeded && intermediate && !staleDraft.Succeeded;

            var fresh = editing.CreateCreatureProgressionDraftUx4G(evidence.BlueprintId)!;
            var freshChanged = ChangedCreatureProgressionDraftUx4G(fresh);
            var approved = editing.PreviewCreatureProgressionUx4G(freshChanged);
            var secondIntermediate = ToggleCreatureProgressionUx4G(editing, evidence.BlueprintId);
            var staleApproval = editing.ApplyApprovedCreatureProgressionUx4G(freshChanged);
            var staleApprovalBlocked = approved.Succeeded && secondIntermediate && !staleApproval.Succeeded;

            fresh = editing.CreateCreatureProgressionDraftUx4G(evidence.BlueprintId)!;
            freshChanged = ChangedCreatureProgressionDraftUx4G(fresh);
            var expected = CreateCreatureProgressionCandidateUx4G(freshChanged);
            var finalPreview = editing.PreviewCreatureProgressionUx4G(freshChanged);
            var beforeApply = editing.Project!;
            var beforeBlueprint = beforeApply.Blueprints.Single(item => item.Id == evidence.BlueprintId);
            var apply = editing.ApplyApprovedCreatureProgressionUx4G(freshChanged);
            var appliedProject = editing.Project!;
            var appliedBlueprint = appliedProject.Blueprints.Single(item => item.Id == evidence.BlueprintId);
            var applied = appliedBlueprint.CreatureProgression;
            var tracks = applied is { AllowMultipleTracks: true } && applied.Tracks.Any(item =>
                item.TrackId == "scholar" && item.Level == 2);
            var disposition = applied?.DispositionAxes.Any(item => item.AxisId == "mercy" && item.Value == .55) == true;
            var skills = applied?.Skills.Any(item => item.SkillId == "lore" && item.Expertise && item.Rank == 3) == true;
            var talents = applied?.Talents.Any(item => item.TalentId == "discipline" && item.Passive &&
                item.HookIds.Contains("progression.on-rank", StringComparer.Ordinal)) == true;
            var ability = appliedBlueprint.Combat?.Abilities.FirstOrDefault()?.Slug;
            var loadout = ability is null
                ? applied?.Loadout.PreparedAbilities.Count == 0
                : applied?.Loadout.PreparedAbilities.Any(item => item.AbilityId == ability && item.Charges == 3) == true;
            var neighbors = ReferenceEquals(beforeBlueprint.Combat, appliedBlueprint.Combat) &&
                            ReferenceEquals(beforeBlueprint.CreatureFoundation, appliedBlueprint.CreatureFoundation) &&
                            ReferenceEquals(beforeBlueprint.CreaturePresentation, appliedBlueprint.CreaturePresentation) &&
                            ReferenceEquals(beforeBlueprint.CreatureLoot, appliedBlueprint.CreatureLoot) &&
                            ReferenceEquals(beforeBlueprint.CreatureRoleBehavior, appliedBlueprint.CreatureRoleBehavior) &&
                            ReferenceEquals(beforeBlueprint.CreatureEvents, appliedBlueprint.CreatureEvents) &&
                            ProjectJsonSerializer.Serialize(WithCreatureProgressionUx4G(
                                beforeApply, evidence.BlueprintId, expected)) ==
                            ProjectJsonSerializer.Serialize(appliedProject);
            var canonical = finalPreview.Succeeded && apply.Succeeded && applied is not null;
            var undo = editing.UndoCreatureProgressionUx4G();
            var undone = undo.Succeeded && ReferenceEquals(beforeApply, editing.Project);
            var redo = editing.RedoCreatureProgressionUx4G();
            var redone = redo.Succeeded && ReferenceEquals(appliedProject, editing.Project);
            var saved = redone && editing.Save().Succeeded;
            var persisted = saved && reopened.Open(root).Succeeded &&
                            ProjectJsonSerializer.Serialize(reopened.Project!) ==
                            ProjectJsonSerializer.Serialize(appliedProject);
            result = new(canonical && explicitSelection && tracks && disposition && skills && talents && loadout &&
                         invalidBlocked && dryRun && staleDraftBlocked && staleApprovalBlocked && undone && redone &&
                         persisted && neighbors, explicitSelection, tracks, disposition, skills, talents, loadout,
                invalidBlocked, dryRun, staleDraftBlocked, staleApprovalBlocked, canonical, undone && redone,
                persisted, neighbors, false);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX4G_CONTRACT_FAILED stage=exception " + exception);
        }
        finally
        {
            reopened.Close();
            editing.Close();
            var released = DeleteUx4GSmokeRoot(root);
            result = result with { Succeeded = result.Succeeded && released, TemporaryWorkspaceReleased = released };
        }
        return result;
    }

    internal static StudioCreatureProgressionDraftUx4G ChangedCreatureProgressionDraftUx4G(
        StudioCreatureProgressionDraftUx4G draft)
    {
        var firstTrack = draft.Tracks.First();
        var autoLevelOwnsTrackSet = draft.SourceBlueprint.CreatureRoleBehavior?.AutoLevel.ManualLock == false;
        var tracks = autoLevelOwnsTrackSet
            ? draft.Tracks.Select((item, index) => index == 0
                ? item with { Experience = item.Experience + 900 }
                : item).ToArray()
            : draft.Tracks.Where(item => item.TrackId != "scholar")
                .Concat([new CreatureProgressionProfile.ProgressionTrack("scholar", "Érudit", 2, 20, 900, [])])
                .ToArray();
        var axes = draft.DispositionAxes.Where(item => item.AxisId != "mercy")
            .Concat([new CreatureProgressionProfile.DispositionAxis("mercy", "Impitoyable", "Miséricordieux", .55)])
            .ToArray();
        var skills = draft.Skills.Where(item => item.SkillId != "lore")
            .Concat([new CreatureProgressionProfile.SkillProgression("lore", "Savoir ancien", "insight",
                3, 10, 4, true, true, 12)])
            .ToArray();
        var ability = draft.SourceBlueprint.Combat?.Abilities.FirstOrDefault()?.Slug;
        var grants = ability is null ? Array.Empty<string>() : [ability];
        var talents = draft.Talents.Where(item => item.TalentId != "discipline")
            .Concat([new CreatureProgressionProfile.TalentProgression("discipline", "Discipline éprouvée", 1, 3,
                true, [new("track", firstTrack.TrackId, Math.Min(firstTrack.Level, 1)), new("skill", "lore", 2)],
                grants, ["progression.on-rank"])])
            .ToArray();
        var learned = ability is null ? draft.LearnedAbilityIds.ToArray() :
            draft.LearnedAbilityIds.Append(ability).Distinct(StringComparer.Ordinal).ToArray();
        var prepared = ability is null ? Array.Empty<CreatureProgressionProfile.PreparedAbility>() :
            [new CreatureProgressionProfile.PreparedAbility(ability, 1, 3, 4.5, null, 0, ["empowered"])];
        return draft with
        {
            RulesetId = "forge.generic",
            AllowMultipleTracks = autoLevelOwnsTrackSet ? draft.AllowMultipleTracks : true,
            RequiredAtRuntime = true,
            Tracks = tracks,
            DispositionAxes = axes,
            Skills = skills,
            Talents = talents,
            LoadoutProviderId = "prepared",
            LoadoutMode = AbilityLoadoutMode.Prepared,
            LoadoutCapacity = Math.Max(3, prepared.Length),
            LearnedAbilityIds = learned,
            PreparedAbilities = prepared,
        };
    }

    private static CreatureProgressionProfile CreateCreatureProgressionCandidateUx4G(
        StudioCreatureProgressionDraftUx4G draft) => new(
        draft.SourceProgression.Id, draft.SourceProgression.SchemaVersion, draft.SourceProgression.Provenance,
        draft.RulesetId, draft.AllowMultipleTracks, draft.RequiredAtRuntime, draft.Tracks, draft.DispositionAxes,
        draft.Skills, draft.Talents, new(draft.LoadoutProviderId, draft.LoadoutMode, draft.LoadoutCapacity,
            draft.LearnedAbilityIds, draft.PreparedAbilities), draft.SourceProgression.Overrides);

    private static CreatureProgressionProfile CreateCanonicalCreatureProgressionUx4G(BlueprintDefinition blueprint)
    {
        var source = CreatureProgressionProfile.CreateDefault(
            StableCreatureProgressionIdUx4G(blueprint.Id.Value, "profile"), "studio:ux4-g-evidence");
        var ability = blueprint.Combat?.Abilities.FirstOrDefault()?.Slug;
        var learned = ability is null ? Array.Empty<string>() : [ability];
        return new(source.Id, source.SchemaVersion, source.Provenance, source.RulesetId, true, false,
            source.Tracks, [new("mercy", "Impitoyable", "Miséricordieux", 0)], source.Skills, source.Talents,
            new("standard", AbilityLoadoutMode.Learned, learned.Length, learned, []), source.Overrides);
    }

    private static ProjectDefinition WithCreatureProgressionUx4G(ProjectDefinition project, BlueprintId blueprintId,
        CreatureProgressionProfile progression)
    {
        var blueprints = project.Blueprints.Select(item => item.Id != blueprintId ? item : new BlueprintDefinition(
            item.Id, item.Slug, item.DisplayName, item.Kind, item.Combat, item.PaletteOrigin, item.IsFavorite,
            item.Provenance, item.CreaturePresentation, progression, item.CreatureLoot, item.CreatureRoleBehavior,
            item.CreatureEvents, item.ItemAdvanced, item.Authoring, item.CreatureFoundation, item.ItemFoundation)).ToArray();
        return new(project.Id, project.Title, project.Module, blueprints, project.Areas, project.Dialogues,
            project.Quests);
    }

    private static bool ToggleCreatureProgressionUx4G(StudioSessionController session, BlueprintId blueprintId)
    {
        var project = session.Project;
        var history = session.History;
        var source = project?.Blueprints.SingleOrDefault(item => item.Id == blueprintId)?.CreatureProgression;
        if (project is null || history is null || source is null || source.Tracks.Count == 0) return false;
        var tracks = source.Tracks.Select((item, index) => index == 0 ? item with { Experience = item.Experience + 1 } : item)
            .ToArray();
        var changed = new CreatureProgressionProfile(source.Id, source.SchemaVersion, source.Provenance,
            source.RulesetId, source.AllowMultipleTracks, source.RequiredAtRuntime, tracks, source.DispositionAxes,
            source.Skills, source.Talents, source.Loadout, source.Overrides);
        var handler = new SetCreatureProgressionHandler(new SystemClock());
        var result = handler.Handle(history.State, new(CommandId.From(Guid.NewGuid()), history.State.Revision,
            blueprintId, changed, DryRun: false));
        if (!result.Succeeded) return false;
        history.Record(result);
        session.InvalidateContentIndexes();
        return true;
    }

    private static IReadOnlySet<string> AvailableAbilityIdsUx4G(BlueprintDefinition blueprint) =>
        (blueprint.Combat?.Abilities ?? []).Select(item => item.Slug).ToHashSet(StringComparer.Ordinal);

    private static StudioCreatureProgressionPreviewUx4G FailedCreatureProgressionPreviewUx4G(
        string message, string code) => new(false, false, message, string.Empty, string.Empty,
        [new(code, "creature/progression", message)], "Pistes non évaluées.", "Disposition non évaluée.",
        "Compétences non évaluées.", "Talents non évalués.", "Pouvoirs non évalués.",
        "Runtime non évalué.");

    private static IReadOnlyList<StudioCreatureProgressionDiagnosticUx4G> CreatureProgressionDiagnosticsUx4G(
        IReadOnlyList<CommandDiagnostic> diagnostics) => diagnostics.Select(item =>
    {
        var separator = item.Message.IndexOf(": ", StringComparison.Ordinal);
        var path = separator > 0 ? item.Message[..separator] : "creature/progression";
        var message = separator > 0 ? item.Message[(separator + 2)..] : item.Message;
        return new StudioCreatureProgressionDiagnosticUx4G(item.Code, path,
            HumanCreatureProgressionMessageUx4G(item.Code, message));
    }).ToArray();

    private static StudioCreatureProgressionDiagnosticUx4G CreatureProgressionDiagnosticUx4G(
        CreatureProgressionDiagnostic item) => new(item.Code, item.Path,
        HumanCreatureProgressionMessageUx4G(item.Code, item.Message));

    private static string HumanCreatureProgressionMessageUx4G(string code, string fallback) => code switch
    {
        "creature.progression.track_invalid" => "Chaque classe exige une clé unique, un niveau borné et une expérience finie.",
        "creature.progression.disposition_invalid" => "Chaque axe exige deux libellés et une valeur comprise entre -1 et 1.",
        "creature.progression.skill_invalid" => "Les compétences exigent rangs bornés, entraînement cohérent et difficulté finie.",
        "creature.progression.talent_invalid" => "Les talents exigent des prérequis résolus et des grants canoniques.",
        "creature.progression.loadout_invalid" => "Le loadout exige une capacité cohérente et des pouvoirs appris avant préparation.",
        "creature.progression.ability_missing" => "Chaque pouvoir doit exister dans le profil de combat de cette créature.",
        _ => fallback,
    };

    private static string TrackSummaryUx4G(StudioCreatureProgressionDraftUx4G draft) =>
        $"{draft.Tracks.Count} piste(s) · niveau {draft.Tracks.Sum(item => item.Level)} · multiclassage {draft.AllowMultipleTracks.ToString().ToLowerInvariant()}";
    private static string DispositionSummaryUx4G(StudioCreatureProgressionDraftUx4G draft) =>
        $"{draft.DispositionAxes.Count} axe(s) défini(s) par le projet";
    private static string SkillSummaryUx4G(StudioCreatureProgressionDraftUx4G draft) =>
        $"{draft.Skills.Count} compétence(s) · {draft.Skills.Count(item => item.Expertise)} expertise(s)";
    private static string TalentSummaryUx4G(StudioCreatureProgressionDraftUx4G draft) =>
        $"{draft.Talents.Count} talent(s) · {draft.Talents.SelectMany(item => item.HookIds).Distinct().Count()} hook(s) typé(s)";
    private static string LoadoutSummaryUx4G(StudioCreatureProgressionDraftUx4G draft) =>
        $"{draft.LoadoutMode} · {draft.LearnedAbilityIds.Count} appris · {draft.PreparedAbilities.Count}/{draft.LoadoutCapacity} préparés";

    private static StudioCreatureProgressionDraftUx4G FreezeCreatureProgressionDraftUx4G(
        StudioCreatureProgressionDraftUx4G draft) => draft with
        {
            Tracks = draft.Tracks.Select(item => item with { GrantedTalentIds = item.GrantedTalentIds.ToArray() }).ToArray(),
            DispositionAxes = draft.DispositionAxes.ToArray(),
            Skills = draft.Skills.ToArray(),
            Talents = draft.Talents.Select(item => item with
            {
                Prerequisites = item.Prerequisites.ToArray(),
                GrantedAbilityIds = item.GrantedAbilityIds.ToArray(),
                HookIds = item.HookIds.ToArray(),
            }).ToArray(),
            LearnedAbilityIds = draft.LearnedAbilityIds.ToArray(),
            PreparedAbilities = draft.PreparedAbilities.Select(item => item with { Modifiers = item.Modifiers.ToArray() })
            .ToArray(),
        };

    private static string FingerprintProjectUx4G(ProjectDefinition project) => HashUx4G(ProjectJsonSerializer.Serialize(project));
    private static string FingerprintCreatureProgressionCandidateUx4G(ProjectDefinition project,
        BlueprintId blueprintId, CreatureProgressionProfile candidate) =>
        FingerprintProjectUx4G(WithCreatureProgressionUx4G(project, blueprintId, candidate));

    private static string FingerprintCreatureProgressionDraftUx4G(StudioCreatureProgressionDraftUx4G draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(draft.BaseRevision.Value); WriteUx4G(writer, draft.SourceProjectFingerprint);
        WriteUx4G(writer, draft.BlueprintId.ToString()); WriteUx4G(writer, draft.RulesetId);
        writer.Write(draft.AllowMultipleTracks); writer.Write(draft.RequiredAtRuntime);
        writer.Write(draft.Tracks.Count);
        foreach (var item in draft.Tracks)
        {
            WriteUx4G(writer, item.TrackId); WriteUx4G(writer, item.DisplayName); writer.Write(item.Level);
            writer.Write(item.MaximumLevel); writer.Write(item.Experience); WriteListUx4G(writer, item.GrantedTalentIds);
        }
        writer.Write(draft.DispositionAxes.Count);
        foreach (var item in draft.DispositionAxes)
        { WriteUx4G(writer, item.AxisId); WriteUx4G(writer, item.NegativeLabel); WriteUx4G(writer, item.PositiveLabel); writer.Write(item.Value); }
        writer.Write(draft.Skills.Count);
        foreach (var item in draft.Skills)
        {
            WriteUx4G(writer, item.SkillId); WriteUx4G(writer, item.DisplayName); WriteUx4G(writer, item.GoverningAttributeId);
            writer.Write(item.Rank); writer.Write(item.MaximumRank); writer.Write(item.TrainingPoints);
            writer.Write(item.Trained); writer.Write(item.Expertise); writer.Write(item.DefaultDifficulty);
        }
        writer.Write(draft.Talents.Count);
        foreach (var item in draft.Talents)
        {
            WriteUx4G(writer, item.TalentId); WriteUx4G(writer, item.DisplayName); writer.Write(item.Rank);
            writer.Write(item.MaximumRank); writer.Write(item.Passive); writer.Write(item.Prerequisites.Count);
            foreach (var prerequisite in item.Prerequisites)
            { WriteUx4G(writer, prerequisite.Kind); WriteUx4G(writer, prerequisite.ReferenceId); writer.Write(prerequisite.MinimumRank); }
            WriteListUx4G(writer, item.GrantedAbilityIds); WriteListUx4G(writer, item.HookIds);
        }
        WriteUx4G(writer, draft.LoadoutProviderId); writer.Write((int)draft.LoadoutMode);
        writer.Write(draft.LoadoutCapacity); WriteListUx4G(writer, draft.LearnedAbilityIds);
        writer.Write(draft.PreparedAbilities.Count);
        foreach (var item in draft.PreparedAbilities)
        {
            WriteUx4G(writer, item.AbilityId); writer.Write(item.Rank); writer.Write(item.Charges);
            writer.Write(item.CooldownSeconds); WriteUx4G(writer, item.ResourceId); writer.Write(item.ResourceCost);
            WriteListUx4G(writer, item.Modifiers);
        }
        writer.Flush(); return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteListUx4G(BinaryWriter writer, IReadOnlyList<string> values)
    { writer.Write(values.Count); foreach (var value in values) WriteUx4G(writer, value); }
    private static void WriteUx4G(BinaryWriter writer, string? value)
    { var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty); writer.Write(bytes.Length); writer.Write(bytes); }
    private static string HashUx4G(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    internal static Guid StableCreatureProgressionIdUx4G(Guid basis, string segment)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{basis:D}:ux4-g:{segment}"));
        Span<byte> id = stackalloc byte[16]; bytes.AsSpan(0, 16).CopyTo(id);
        id[7] = (byte)((id[7] & 0x0F) | 0x40); id[8] = (byte)((id[8] & 0x3F) | 0x80);
        return new Guid(id);
    }

    private static bool DeleteUx4GSmokeRoot(string root)
    {
        try
        {
            var full = Path.GetFullPath(root); var temp = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4g-smoke-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full) && !File.Exists(full);
        }
        catch { return false; }
    }
}
