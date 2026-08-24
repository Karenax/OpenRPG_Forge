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
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SetCreatureRoleBehaviorHandler _setCreatureRoleBehaviorUx4M = new(new SystemClock());
    private StudioCreatureRoleBehaviorDraftUx4M? _approvedCreatureRoleBehaviorDraftUx4M;
    private StudioCreatureRoleBehaviorCandidateUx4M? _approvedCreatureRoleBehaviorCandidateUx4M;
    private ProjectRevision? _approvedCreatureRoleBehaviorRevisionUx4M;
    private string? _approvedCreatureRoleBehaviorProjectFingerprintUx4M;
    private string? _approvedCreatureRoleBehaviorDraftFingerprintUx4M;
    private string? _approvedCreatureRoleBehaviorCandidateFingerprintUx4M;

    internal IReadOnlyList<StudioCreatureRoleBehaviorChoiceUx4M> CreatureRoleBehaviorChoicesUx4M =>
        Project?.Blueprints
            .Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => new StudioCreatureRoleBehaviorChoiceUx4M(item.Id,
                $"{item.DisplayName} · {(item.CreatureRoleBehavior is null ? "profil absent" : item.CreatureRoleBehavior.Role.RulesetTemplateId)}",
                item.CreatureRoleBehavior is not null, item.IsReadOnly())).ToArray() ?? [];

    internal StudioCreatureRoleBehaviorDraftUx4M? CreateCreatureRoleBehaviorDraftUx4M(BlueprintId blueprintId)
    {
        var project = Project;
        var history = History;
        var blueprint = project?.Blueprints.SingleOrDefault(item => item.Id == blueprintId);
        if (project is null || history is null || blueprint is null || blueprint.IsReadOnly() ||
            blueprint.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn)) return null;
        var profile = blueprint.CreatureRoleBehavior;
        var working = profile ?? CreatureRoleBehaviorProfile.CreateDefault(
            StableCreatureRoleBehaviorIdUx4M(blueprint.Id.Value, "profile"), "studio:ux4-m");
        if (!Enum.IsDefined(working.Ambient.Interruption.ResumeMode)) return null;
        return new(history.State.Revision, FingerprintProjectUx4M(project), blueprint.Id, blueprint, profile,
            blueprint.CreatureProgression, profile is not null, working.RequiredAtRuntime,
            working.Role.RulesetTemplateId, JoinMapUx4M(working.Role.LoreFields),
            JoinCsvUx4M(working.Role.RoleTags), working.Ambient.GraphId,
            string.Join('\n', working.Ambient.IdleSets.Select(item =>
                $"{item.SetId}|{JoinCsvUx4M(item.AnimationIds)}|{FormatUx4M(item.Weight)}|" +
                $"{FormatUx4M(item.MinimumSeconds)}|{FormatUx4M(item.MaximumSeconds)}")),
            string.Join('\n', working.Ambient.Schedules.Select(item =>
                $"{item.ScheduleId}|{item.StartMinute}|{item.EndMinute}|{item.IdleSetId}|" +
                $"{item.WaypointId ?? string.Empty}|{item.ConversationId ?? string.Empty}")),
            string.Join('\n', working.Ambient.Interactions.Select(item =>
                $"{item.InteractionId}|{item.TargetId}|{item.AnimationId}|{FormatUx4M(item.CooldownSeconds)}")),
            working.Ambient.Interruption.InterruptOnCombat,
            working.Ambient.Interruption.InterruptOnConversation,
            working.Ambient.Interruption.InterruptOnMovement,
            working.Ambient.Interruption.ResumeMode,
            FormatUx4M(working.Ambient.Interruption.ResumeDelaySeconds),
            working.AutoLevel.TemplateId, working.AutoLevel.Seed.ToString(CultureInfo.InvariantCulture),
            working.AutoLevel.TargetLevel.ToString(CultureInfo.InvariantCulture), working.AutoLevel.ApplyOnSave,
            working.AutoLevel.ManualLock,
            string.Join('\n', working.AutoLevel.Tracks.Select(item => $"{item.TrackId}|{item.Weight}")),
            string.Join('\n', working.AutoLevel.Skills.Select(item =>
                $"{item.SkillId}|{item.RankEveryLevels}|{item.MaximumRank}")),
            string.Join('\n', working.AutoLevel.Talents.Select(item =>
                $"{item.TalentId}|{item.UnlockLevel}")), JoinMapUx4M(working.Overrides));
    }

    internal StudioCreatureRoleBehaviorPreviewUx4M PreviewCreatureRoleBehaviorUx4M(
        StudioCreatureRoleBehaviorDraftUx4M draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelCreatureRoleBehaviorUx4M();
        var project = Project;
        var history = History;
        if (project is null || history is null)
            return FailedCreatureRoleBehaviorUx4M("Aucun module n’est ouvert.",
                "studio.creature.project_missing");
        if (history.State.Revision != draft.BaseRevision ||
            !string.Equals(FingerprintProjectUx4M(project), draft.SourceProjectFingerprint,
                StringComparison.Ordinal))
            return FailedCreatureRoleBehaviorUx4M("Le projet a changé ; rechargez la fiche.",
                SetCreatureRoleBehaviorHandler.RevisionConflictCode);
        var source = project.Blueprints.SingleOrDefault(item => item.Id == draft.BlueprintId);
        if (source is null)
            return FailedCreatureRoleBehaviorUx4M("La créature n’existe plus.",
                SetCreatureRoleBehaviorHandler.BlueprintMissingCode);
        if (!TryBuildCreatureRoleBehaviorCandidateUx4M(draft, out var candidate, out var diagnostics))
            return new(false, false, "Le profil rôle et comportement est invalide.", string.Empty,
                string.Empty, diagnostics, "Rôle non évalué.", "Ambiance non évaluée.",
                "Auto-level non évalué.", "Runtime non évalué.");
        var dryRun = _setCreatureRoleBehaviorUx4M.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, draft.BlueprintId, candidate.Profile,
                DryRun: true));
        if (!dryRun.Succeeded)
            return new(false, false, "La commande canonique refuse le profil.", string.Empty, string.Empty,
                CreatureRoleBehaviorDiagnosticsUx4M(dryRun.Diagnostics), RoleSummaryUx4M(candidate.Profile),
                AmbientSummaryUx4M(candidate.Profile), AutoLevelSummaryUx4M(candidate.Profile, source.CreatureProgression),
                "Runtime non évalué.");
        var runtime = CreatureRoleBehaviorRuntimeResolver.Resolve(candidate.Profile, source.CreatureProgression,
            21 * 60, interrupted: false, required: false);
        if (!runtime.Succeeded)
            return new(false, false, "La projection runtime refuse le profil.", string.Empty, string.Empty,
                runtime.Diagnostics.Select(CreatureRoleBehaviorDiagnosticUx4M).ToArray(),
                RoleSummaryUx4M(candidate.Profile), AmbientSummaryUx4M(candidate.Profile),
                AutoLevelSummaryUx4M(candidate.Profile, source.CreatureProgression), "Runtime refusé.");
        var frozen = draft with { };
        _approvedCreatureRoleBehaviorDraftUx4M = frozen;
        _approvedCreatureRoleBehaviorCandidateUx4M = candidate;
        _approvedCreatureRoleBehaviorRevisionUx4M = history.State.Revision;
        _approvedCreatureRoleBehaviorProjectFingerprintUx4M = FingerprintProjectUx4M(project);
        _approvedCreatureRoleBehaviorDraftFingerprintUx4M = FingerprintCreatureRoleBehaviorDraftUx4M(frozen);
        _approvedCreatureRoleBehaviorCandidateFingerprintUx4M =
            FingerprintCreatureRoleBehaviorCandidateUx4M(project, draft.BlueprintId, candidate);
        var runtimeSummary = candidate.Profile is null
            ? "Profil optionnel retiré · aucune IA, animation ou progression n’est exécutée."
            : $"Projection à 21:00 : set {runtime.ActiveIdleSetId ?? "aucun"}, animation " +
              $"{runtime.ActiveAnimationId ?? "aucune"}, {runtime.AutoLevel?.Changes.Count ?? 0} changement(s). " +
              "Aucun acteur, animation, script ou réseau n’est exécuté dans Studio.";
        return new(true, true, candidate.Profile is null
                ? "Profil optionnel retiré de l’aperçu."
                : "Rôle, activité et auto-level valides · prêts à être appliqués.",
            dryRun.Patch?.Summary ?? "Définir le rôle et le comportement.",
            dryRun.Inverse?.Summary ?? "Restaurer le rôle et le comportement précédents.", [],
            RoleSummaryUx4M(candidate.Profile), AmbientSummaryUx4M(candidate.Profile),
            AutoLevelSummaryUx4M(candidate.Profile, source.CreatureProgression), runtimeSummary);
    }

    internal StudioCreatureRoleBehaviorActionResultUx4M ApplyApprovedCreatureRoleBehaviorUx4M(
        StudioCreatureRoleBehaviorDraftUx4M currentDraft)
    {
        ArgumentNullException.ThrowIfNull(currentDraft);
        var project = Project;
        var history = History;
        var approved = _approvedCreatureRoleBehaviorDraftUx4M;
        var candidate = _approvedCreatureRoleBehaviorCandidateUx4M;
        if (project is null || history is null || approved is null || candidate is null ||
            _approvedCreatureRoleBehaviorRevisionUx4M is null ||
            _approvedCreatureRoleBehaviorProjectFingerprintUx4M is null ||
            _approvedCreatureRoleBehaviorDraftFingerprintUx4M is null ||
            _approvedCreatureRoleBehaviorCandidateFingerprintUx4M is null)
            return new(false, "Prévisualisez le profil avant de l’appliquer.");
        var stale = history.State.Revision != _approvedCreatureRoleBehaviorRevisionUx4M ||
                    !string.Equals(FingerprintProjectUx4M(project),
                        _approvedCreatureRoleBehaviorProjectFingerprintUx4M, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureRoleBehaviorDraftUx4M(currentDraft),
                        _approvedCreatureRoleBehaviorDraftFingerprintUx4M, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureRoleBehaviorDraftUx4M(approved),
                        _approvedCreatureRoleBehaviorDraftFingerprintUx4M, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureRoleBehaviorCandidateUx4M(project,
                            currentDraft.BlueprintId, candidate),
                        _approvedCreatureRoleBehaviorCandidateFingerprintUx4M, StringComparison.Ordinal);
        if (stale)
        {
            CancelCreatureRoleBehaviorUx4M();
            return new(false, "Le brouillon, le projet ou l’approbation a changé ; prévisualisez à nouveau.");
        }
        var validation = _setCreatureRoleBehaviorUx4M.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, currentDraft.BlueprintId,
                candidate.Profile, DryRun: true));
        if (!validation.Succeeded)
        {
            CancelCreatureRoleBehaviorUx4M();
            return new(false, "La revalidation canonique refuse le profil.");
        }
        var result = _setCreatureRoleBehaviorUx4M.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, currentDraft.BlueprintId,
                candidate.Profile, DryRun: false));
        CancelCreatureRoleBehaviorUx4M();
        if (!result.Succeeded) return new(false, "La commande n’a pas appliqué le profil.");
        history.Record(result);
        InvalidateContentIndexes();
        return new(true, result.Patch?.Summary ?? "Rôle et comportement appliqués.");
    }

    internal StudioCreatureRoleBehaviorActionResultUx4M UndoCreatureRoleBehaviorUx4M()
    { CancelCreatureRoleBehaviorUx4M(); var result = Undo(); return new(result.Succeeded, result.Message); }

    internal StudioCreatureRoleBehaviorActionResultUx4M RedoCreatureRoleBehaviorUx4M()
    { CancelCreatureRoleBehaviorUx4M(); var result = Redo(); return new(result.Succeeded, result.Message); }

    internal void CancelCreatureRoleBehaviorUx4M()
    {
        _approvedCreatureRoleBehaviorDraftUx4M = null;
        _approvedCreatureRoleBehaviorCandidateUx4M = null;
        _approvedCreatureRoleBehaviorRevisionUx4M = null;
        _approvedCreatureRoleBehaviorProjectFingerprintUx4M = null;
        _approvedCreatureRoleBehaviorDraftFingerprintUx4M = null;
        _approvedCreatureRoleBehaviorCandidateFingerprintUx4M = null;
    }

    internal void CloseUx4M() => CancelCreatureRoleBehaviorUx4M();

    internal StudioCreatureRoleBehaviorApprovalSnapshotUx4M CaptureCreatureRoleBehaviorApprovalUx4M() => new(
        _approvedCreatureRoleBehaviorDraftUx4M, _approvedCreatureRoleBehaviorCandidateUx4M,
        _approvedCreatureRoleBehaviorRevisionUx4M, _approvedCreatureRoleBehaviorProjectFingerprintUx4M,
        _approvedCreatureRoleBehaviorDraftFingerprintUx4M, _approvedCreatureRoleBehaviorCandidateFingerprintUx4M);

    internal void RestoreCreatureRoleBehaviorApprovalUx4M(
        StudioCreatureRoleBehaviorApprovalSnapshotUx4M snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedCreatureRoleBehaviorDraftUx4M = snapshot.Draft;
        _approvedCreatureRoleBehaviorCandidateUx4M = snapshot.Candidate;
        _approvedCreatureRoleBehaviorRevisionUx4M = snapshot.Revision;
        _approvedCreatureRoleBehaviorProjectFingerprintUx4M = snapshot.ProjectFingerprint;
        _approvedCreatureRoleBehaviorDraftFingerprintUx4M = snapshot.DraftFingerprint;
        _approvedCreatureRoleBehaviorCandidateFingerprintUx4M = snapshot.CandidateFingerprint;
    }

    internal StudioCreatureRoleBehaviorEvidenceProjectUx4M? CreateUx4MEvidenceProject()
    {
        var project = Project;
        if (project is null) return null;
        var real = project.Blueprints.Where(item =>
                (item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn) && !item.IsReadOnly() &&
                item.CreatureRoleBehavior is not null && item.CreatureProgression is not null)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault(item => CreatureRoleBehaviorValidator.Validate(item.CreatureRoleBehavior!,
                item.CreatureProgression).Count == 0);
        if (real is not null) return new(project, real.Id, true, false);

        var editable = project.Blueprints.Where(item =>
                (item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn) && !item.IsReadOnly())
            .OrderByDescending(item => item.CreatureProgression is not null)
            .ThenBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase).FirstOrDefault();
        if (editable is null) return null;
        var progression = editable.CreatureProgression is { Tracks.Count: > 0 }
            ? editable.CreatureProgression
            : CreatureProgressionProfile.CreateDefault(
            StableCreatureRoleBehaviorIdUx4M(editable.Id.Value, "progression"), "studio:ux4-m-evidence");
        var profile = CanonicalCreatureRoleBehaviorUx4M(editable, progression);
        return new(WithCreatureRoleBehaviorUx4M(project, editable.Id, progression, profile), editable.Id,
            false, true);
    }

    internal StudioUx4MSmokeResult RunUx4MContractSmoke()
    {
        var empty = new StudioUx4MSmokeResult(false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, false);
        var evidence = CreateUx4MEvidenceProject();
        if (evidence is null) return empty;
        var root = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux4m-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        StudioUx4MSmokeResult Execute()
        {
            try
            {
                if (!ProjectSession.Create(root, evidence.Project).Succeeded || !editing.Open(root).Succeeded)
                    return empty;
                var explicitSelection = editing.CreatureRoleBehaviorChoicesUx4M.Count > 0 &&
                    editing.CreatureRoleBehaviorChoicesUx4M.All(item =>
                        !item.Label.Contains(item.BlueprintId.ToString(), StringComparison.OrdinalIgnoreCase));
                var draft = editing.CreateCreatureRoleBehaviorDraftUx4M(evidence.BlueprintId);
                if (draft is null) return empty with { ExplicitSelection = explicitSelection };
                var changed = ChangedCreatureRoleBehaviorDraftUx4M(draft);
                var sourceProject = editing.Project!;
                var sourceHistory = editing.History!;
                var preview = editing.PreviewCreatureRoleBehaviorUx4M(changed);
                var dryRun = preview.Succeeded && ReferenceEquals(sourceProject, editing.Project) &&
                             ReferenceEquals(sourceHistory, editing.History) &&
                             sourceHistory.State.Revision == draft.BaseRevision;
                var runtimeProjection = preview.Succeeded &&
                                        preview.RuntimeSummary.Contains("21:00", StringComparison.Ordinal);
                var invalid = editing.PreviewCreatureRoleBehaviorUx4M(changed with
                {
                    RulesetTemplateId = "Forge Invalid"
                });
                var invalidBlocked = !invalid.Succeeded && invalid.Diagnostics.Any(item =>
                    item.Code is "studio.creature.role_behavior.identifier_invalid" or
                        "creature.role.ruleset_invalid");
                var duplicate = editing.PreviewCreatureRoleBehaviorUx4M(changed with
                {
                    IdleSets = "idle.same|idle|1|2|4\nidle.same|inspect|1|2|4"
                });
                var duplicateBlocked = !duplicate.Succeeded && duplicate.Diagnostics.Any(item =>
                    item.Code == "creature.ambient.idle_invalid");
                var optional = editing.PreviewCreatureRoleBehaviorUx4M(changed with { Enabled = false });
                var optionalRemoval = optional.Succeeded &&
                                      optional.RoleSummary.Contains("désactivé", StringComparison.OrdinalIgnoreCase);

                var approvedBeforeRevision = editing.PreviewCreatureRoleBehaviorUx4M(changed);
                var neighborChanged = ToggleCreatureRoleBehaviorNeighborUx4M(editing, evidence.BlueprintId);
                var staleDraftBlocked = approvedBeforeRevision.Succeeded && neighborChanged &&
                    !editing.ApplyApprovedCreatureRoleBehaviorUx4M(changed).Succeeded;
                if (neighborChanged) editing.Undo();

                var fresh = editing.CreateCreatureRoleBehaviorDraftUx4M(evidence.BlueprintId)!;
                var freshChanged = ChangedCreatureRoleBehaviorDraftUx4M(fresh);
                var approvedBeforeTamper = editing.PreviewCreatureRoleBehaviorUx4M(freshChanged);
                var staleApprovalBlocked = approvedBeforeTamper.Succeeded &&
                    !editing.ApplyApprovedCreatureRoleBehaviorUx4M(freshChanged with
                    {
                        RoleTags = freshChanged.RoleTags + ", sentinel"
                    }).Succeeded;

                fresh = editing.CreateCreatureRoleBehaviorDraftUx4M(evidence.BlueprintId)!;
                freshChanged = ChangedCreatureRoleBehaviorDraftUx4M(fresh);
                var beforeApply = editing.Project!;
                var finalPreview = editing.PreviewCreatureRoleBehaviorUx4M(freshChanged);
                var apply = finalPreview.Succeeded
                    ? editing.ApplyApprovedCreatureRoleBehaviorUx4M(freshChanged)
                    : new StudioCreatureRoleBehaviorActionResultUx4M(false, "preview failed");
                var appliedProject = editing.Project;
                var appliedBlueprint = appliedProject?.Blueprints.Single(item => item.Id == evidence.BlueprintId);
                var applied = appliedBlueprint?.CreatureRoleBehavior;
                var role = applied is not null && applied.Role.RulesetTemplateId == "forge.generic" &&
                           applied.Role.LoreFields.GetValueOrDefault("deity") == "deity.embers" &&
                           applied.Role.RoleTags.SequenceEqual(new[] { "guardian", "mystic" });
                var ambient = applied is not null && applied.Ambient.IdleSets.Count == 2 &&
                              applied.Ambient.Schedules.Count == 1 && applied.Ambient.Interactions.Count == 1 &&
                              applied.Ambient.Interruption.ResumeDelaySeconds == .75;
                var auto = applied is not null && !applied.AutoLevel.ManualLock && applied.AutoLevel.ApplyOnSave &&
                           appliedBlueprint!.CreatureProgression!.Tracks.Sum(item => item.Level) ==
                           applied.AutoLevel.TargetLevel;
                var canonical = apply.Succeeded && role && ambient && auto;
                var expected = appliedBlueprint is null ? null : WithCreatureRoleBehaviorUx4M(beforeApply,
                    evidence.BlueprintId, appliedBlueprint.CreatureProgression, appliedBlueprint.CreatureRoleBehavior);
                var neighbors = canonical && expected is not null && appliedProject is not null &&
                                ProjectJsonSerializer.Serialize(expected) ==
                                ProjectJsonSerializer.Serialize(appliedProject);
                var appliedJson = appliedProject is null ? string.Empty :
                    ProjectJsonSerializer.Serialize(appliedProject);
                var beforeJson = ProjectJsonSerializer.Serialize(beforeApply);
                var undo = canonical && editing.UndoCreatureRoleBehaviorUx4M().Succeeded &&
                           ProjectJsonSerializer.Serialize(editing.Project!) == beforeJson;
                var redo = undo && editing.RedoCreatureRoleBehaviorUx4M().Succeeded &&
                           ProjectJsonSerializer.Serialize(editing.Project!) == appliedJson;
                var saved = redo && editing.Save().Succeeded;
                var persisted = saved && reopened.Open(root).Succeeded &&
                                ProjectJsonSerializer.Serialize(reopened.Project!) == appliedJson;
                return new(canonical && explicitSelection && role && ambient && auto && optionalRemoval &&
                        invalidBlocked && duplicateBlocked && dryRun && runtimeProjection && staleDraftBlocked &&
                        staleApprovalBlocked && undo && redo && persisted && neighbors,
                    explicitSelection, role, ambient, auto, optionalRemoval, invalidBlocked, duplicateBlocked,
                    dryRun, runtimeProjection, staleDraftBlocked, staleApprovalBlocked, canonical, undo && redo,
                    persisted, neighbors, false);
            }
            catch
            {
                return empty;
            }
        }

        var result = Execute();
        reopened.Close();
        editing.Close();
        var released = DeleteUx4MSmokeRoot(root);
        return result with { Succeeded = result.Succeeded && released, TemporaryWorkspaceReleased = released };
    }

    internal static StudioCreatureRoleBehaviorDraftUx4M ChangedCreatureRoleBehaviorDraftUx4M(
        StudioCreatureRoleBehaviorDraftUx4M draft)
    {
        var progression = draft.SourceProgression;
        var tracks = progression?.Tracks ?? [];
        var target = Math.Min(999, Math.Max(tracks.Count, tracks.Sum(item => item.Level) + 2));
        return draft with
        {
            Enabled = true,
            RequiredAtRuntime = true,
            RulesetTemplateId = progression?.RulesetId ?? "forge.generic",
            LoreFields = "deity=deity.embers\noccupation=crypt-warden",
            RoleTags = "guardian, mystic",
            AmbientGraphId = "ambient.warden",
            IdleSets = "idle.day|idle, inspect|2|2|5\nidle.night|look-around, warm-hands|1|3|8",
            Schedules = "night-watch|1200|360|idle.night||",
            Interactions = "inspect-brazier|brazier|inspect|12.5",
            InterruptOnCombat = true,
            InterruptOnConversation = true,
            InterruptOnMovement = true,
            ResumeMode = AmbientResumeMode.Resume,
            ResumeDelaySeconds = "0.75",
            AutoLevelTemplateId = "warden-balanced",
            AutoLevelSeed = "42",
            TargetLevel = target.ToString(CultureInfo.InvariantCulture),
            ApplyOnSave = true,
            ManualLock = false,
            TrackAllocations = string.Join('\n', tracks.Select((item, index) =>
                $"{item.TrackId}|{(index == 0 ? 2 : 1)}")),
            SkillAllocations = string.Join('\n', (progression?.Skills ?? []).Select(item =>
                $"{item.SkillId}|2|{item.MaximumRank}")),
            TalentUnlocks = string.Join('\n', (progression?.Talents ?? []).Select(item =>
                $"{item.TalentId}|2")),
            Overrides = "ambient.radius=4",
        };
    }

    private bool TryBuildCreatureRoleBehaviorCandidateUx4M(
        StudioCreatureRoleBehaviorDraftUx4M draft,
        out StudioCreatureRoleBehaviorCandidateUx4M candidate,
        out IReadOnlyList<StudioCreatureRoleBehaviorDiagnosticUx4M> diagnostics)
    {
        var errors = new List<StudioCreatureRoleBehaviorDiagnosticUx4M>();
        if (!draft.Enabled)
        {
            candidate = new(null);
            diagnostics = [];
            return true;
        }

        try
        {
            if (!Enum.IsDefined(draft.ResumeMode))
                errors.Add(new("creature.ambient.interruption_invalid",
                    "creatureRoleBehavior/ambient/interruption", "Le mode de reprise est inconnu."));
            var lore = ParseMapUx4M(draft.LoreFields, "champs de lore");
            var roleTags = SplitCsvUx4M(draft.RoleTags);
            var idleRows = ParseRowsUx4M(draft.IdleSets, 5, "idle set");
            var scheduleRows = ParseRowsUx4M(draft.Schedules, 6, "horaire");
            var interactionRows = ParseRowsUx4M(draft.Interactions, 4, "interaction");
            var trackRows = ParseRowsUx4M(draft.TrackAllocations, 2, "allocation de piste");
            var skillRows = ParseRowsUx4M(draft.SkillAllocations, 3, "allocation de compétence");
            var talentRows = ParseRowsUx4M(draft.TalentUnlocks, 2, "déverrouillage de talent");
            var overrides = ParseMapUx4M(draft.Overrides, "override");
            var identifiers = new List<string> { draft.RulesetTemplateId, draft.AmbientGraphId,
                draft.AutoLevelTemplateId };
            identifiers.AddRange(lore.Keys);
            identifiers.AddRange(roleTags);
            identifiers.AddRange(idleRows.SelectMany(row => new[] { row[0] }.Concat(SplitCsvUx4M(row[1]))));
            identifiers.AddRange(scheduleRows.SelectMany(row =>
                new[] { row[0], row[3] }.Concat(row.Skip(4).Where(value => value.Length > 0))));
            identifiers.AddRange(interactionRows.SelectMany(row => row.Take(3)));
            identifiers.AddRange(trackRows.Select(row => row[0]));
            identifiers.AddRange(skillRows.Select(row => row[0]));
            identifiers.AddRange(talentRows.Select(row => row[0]));
            identifiers.AddRange(overrides.Keys);
            if (identifiers.Any(value => !IsCanonicalIdentifierUx4M(value)))
                errors.Add(new("studio.creature.role_behavior.identifier_invalid", "creatureRoleBehavior",
                    "Les identifiants doivent être canoniques, en minuscules et sans normalisation implicite."));

            var source = draft.SourceProfile ?? CreatureRoleBehaviorProfile.CreateDefault(
                StableCreatureRoleBehaviorIdUx4M(draft.BlueprintId.Value, "profile"), "studio:ux4-m");
            var idleSets = idleRows.Select((row, index) =>
                new CreatureRoleBehaviorProfile.AmbientIdleSet(
                    source.Ambient.IdleSets.FirstOrDefault(item => item.SetId == row[0])?.Id ??
                    StableCreatureRoleBehaviorIdUx4M(source.Id, $"idle:{row[0]}:{index}"), row[0],
                    SplitCsvUx4M(row[1]), ParseDoubleUx4M(row[2], "poids"),
                    ParseDoubleUx4M(row[3], "durée minimale"),
                    ParseDoubleUx4M(row[4], "durée maximale"))).ToArray();
            var schedules = scheduleRows.Select((row, index) =>
                new CreatureRoleBehaviorProfile.AmbientScheduleEntry(
                    source.Ambient.Schedules.FirstOrDefault(item => item.ScheduleId == row[0])?.Id ??
                    StableCreatureRoleBehaviorIdUx4M(source.Id, $"schedule:{row[0]}:{index}"), row[0],
                    ParseIntUx4M(row[1], "début"), ParseIntUx4M(row[2], "fin"), row[3],
                    EmptyUx4M(row[4]), EmptyUx4M(row[5]))).ToArray();
            var interactions = interactionRows.Select((row, index) =>
                new CreatureRoleBehaviorProfile.AmbientInteraction(
                    source.Ambient.Interactions.FirstOrDefault(item => item.InteractionId == row[0])?.Id ??
                    StableCreatureRoleBehaviorIdUx4M(source.Id, $"interaction:{row[0]}:{index}"), row[0],
                    row[1], row[2], ParseDoubleUx4M(row[3], "cooldown"))).ToArray();
            var profile = new CreatureRoleBehaviorProfile(source.Id,
                CreatureRoleBehaviorProfile.CurrentSchemaVersion, source.Provenance, draft.RequiredAtRuntime,
                new(draft.RulesetTemplateId, lore, roleTags),
                new(draft.AmbientGraphId, idleSets, schedules, interactions,
                    new(draft.InterruptOnCombat, draft.InterruptOnConversation, draft.InterruptOnMovement,
                        draft.ResumeMode, ParseDoubleUx4M(draft.ResumeDelaySeconds, "délai de reprise"))),
                new(source.AutoLevel.Id, draft.AutoLevelTemplateId,
                    ParseIntUx4M(draft.AutoLevelSeed, "seed"), ParseIntUx4M(draft.TargetLevel, "niveau cible"),
                    draft.ApplyOnSave, draft.ManualLock,
                    trackRows.Select(row => new CreatureRoleBehaviorProfile.TrackAllocation(row[0],
                        ParseIntUx4M(row[1], "poids de piste"))).ToArray(),
                    skillRows.Select(row => new CreatureRoleBehaviorProfile.SkillAllocation(row[0],
                        ParseIntUx4M(row[1], "fréquence"), ParseIntUx4M(row[2], "rang maximal"))).ToArray(),
                    talentRows.Select(row => new CreatureRoleBehaviorProfile.TalentUnlock(row[0],
                        ParseIntUx4M(row[1], "niveau de talent"))).ToArray()), overrides);
            errors.AddRange(CreatureRoleBehaviorValidator.Validate(profile, draft.SourceProgression,
                    $"blueprints/{draft.BlueprintId}/creatureRoleBehavior")
                .Select(CreatureRoleBehaviorDiagnosticUx4M));
            candidate = new(profile);
        }
        catch (FormatException exception)
        {
            candidate = new(null);
            errors.Add(new("studio.creature.role_behavior.form_invalid", "creatureRoleBehavior",
                exception.Message));
        }
        diagnostics = errors.GroupBy(item => (item.Code, item.Path)).Select(item => item.First()).ToArray();
        return diagnostics.Count == 0;
    }

    private static CreatureRoleBehaviorProfile CanonicalCreatureRoleBehaviorUx4M(
        BlueprintDefinition blueprint, CreatureProgressionProfile progression)
    {
        var tracks = progression.Tracks.Select((item, index) =>
            new CreatureRoleBehaviorProfile.TrackAllocation(item.TrackId, index == 0 ? 2 : 1)).ToArray();
        var skills = progression.Skills.Select(item =>
            new CreatureRoleBehaviorProfile.SkillAllocation(item.SkillId, 2, item.MaximumRank)).ToArray();
        var talents = progression.Talents.Select(item =>
            new CreatureRoleBehaviorProfile.TalentUnlock(item.TalentId, 2)).ToArray();
        return new(StableCreatureRoleBehaviorIdUx4M(blueprint.Id.Value, "profile"),
            CreatureRoleBehaviorProfile.CurrentSchemaVersion, "studio:ux4-m-evidence", true,
            new(progression.RulesetId, new Dictionary<string, string>
            {
                ["deity"] = "deity.embers",
                ["occupation"] = "crypt-warden",
            }, ["guardian", "mystic"]),
            new("ambient.warden",
            [
                new(StableCreatureRoleBehaviorIdUx4M(blueprint.Id.Value, "idle-day"), "idle.day",
                    ["idle", "inspect"], 2, 2, 5),
                new(StableCreatureRoleBehaviorIdUx4M(blueprint.Id.Value, "idle-night"), "idle.night",
                    ["look-around", "warm-hands"], 1, 3, 8),
            ],
            [new(StableCreatureRoleBehaviorIdUx4M(blueprint.Id.Value, "schedule-night"), "night-watch",
                1200, 360, "idle.night", null, null)],
            [new(StableCreatureRoleBehaviorIdUx4M(blueprint.Id.Value, "interaction-brazier"),
                "inspect-brazier", "brazier", "inspect", 12.5)],
            new(true, true, true, AmbientResumeMode.Resume, .75)),
            new(StableCreatureRoleBehaviorIdUx4M(blueprint.Id.Value, "autolevel"), "warden-balanced", 42,
                Math.Min(999, Math.Max(tracks.Length, progression.Tracks.Sum(item => item.Level) + 2)),
                true, false, tracks, skills, talents),
            new Dictionary<string, string> { ["ambient.radius"] = "4" });
    }

    private static ProjectDefinition WithCreatureRoleBehaviorUx4M(ProjectDefinition project,
        BlueprintId blueprintId, CreatureProgressionProfile? progression, CreatureRoleBehaviorProfile? profile) =>
        new(project.Id, project.Title, project.Module, project.Blueprints.Select(item => item.Id == blueprintId
            ? new BlueprintDefinition(item.Id, item.Slug, item.DisplayName, item.Kind, item.Combat,
                item.PaletteOrigin, item.IsFavorite, item.Provenance, item.CreaturePresentation, progression,
                item.CreatureLoot, profile, item.CreatureEvents, item.ItemAdvanced, item.Authoring,
                item.CreatureFoundation, item.ItemFoundation)
            : item).ToArray(), project.Areas, project.Dialogues, project.Quests);

    private static bool ToggleCreatureRoleBehaviorNeighborUx4M(StudioSessionController session,
        BlueprintId blueprintId)
    {
        var project = session.Project;
        var history = session.History;
        var source = project?.Blueprints.SingleOrDefault(item => item.Id == blueprintId)?.CreatureRoleBehavior;
        if (project is null || history is null || source is null) return false;
        var changed = new CreatureRoleBehaviorProfile(source.Id, source.SchemaVersion, source.Provenance,
            !source.RequiredAtRuntime, source.Role, source.Ambient, source.AutoLevel, source.Overrides);
        var result = new SetCreatureRoleBehaviorHandler(new SystemClock()).Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, blueprintId, changed, DryRun: false));
        if (!result.Succeeded) return false;
        history.Record(result);
        session.InvalidateContentIndexes();
        return true;
    }

    private static StudioCreatureRoleBehaviorPreviewUx4M FailedCreatureRoleBehaviorUx4M(
        string message, string code) => new(false, false, message, string.Empty, string.Empty,
        [new(code, "creatureRoleBehavior", HumanCreatureRoleBehaviorMessageUx4M(code, message))],
        "Rôle non évalué.", "Ambiance non évaluée.", "Auto-level non évalué.", "Runtime non évalué.");

    private static StudioCreatureRoleBehaviorDiagnosticUx4M CreatureRoleBehaviorDiagnosticUx4M(
        CreatureRoleBehaviorDiagnostic item) => new(item.Code, item.Path,
        HumanCreatureRoleBehaviorMessageUx4M(item.Code, item.Message));

    private static IReadOnlyList<StudioCreatureRoleBehaviorDiagnosticUx4M>
        CreatureRoleBehaviorDiagnosticsUx4M(IReadOnlyList<CommandDiagnostic> diagnostics) => diagnostics
        .Select(item =>
        {
            var separator = item.Message.IndexOf(": ", StringComparison.Ordinal);
            var path = separator > 0 ? item.Message[..separator] : "creatureRoleBehavior";
            var message = separator > 0 ? item.Message[(separator + 2)..] : item.Message;
            return new StudioCreatureRoleBehaviorDiagnosticUx4M(item.Code, path,
                HumanCreatureRoleBehaviorMessageUx4M(item.Code, message));
        }).ToArray();

    private static string HumanCreatureRoleBehaviorMessageUx4M(string code, string fallback) => code switch
    {
        "creature.role.ruleset_invalid" => "Le template de ruleset doit être un identifiant canonique.",
        "creature.role.tags_invalid" => "Les tags de rôle doivent être uniques et canoniques.",
        "creature.role.lore_invalid" => "Le lore exige des clés canoniques et des valeurs explicites.",
        "creature.ambient.idle_invalid" =>
            "Les idle sets exigent des noms uniques, animations, poids et durées bornées.",
        "creature.ambient.schedule_invalid" =>
            "Les horaires exigent des fenêtres 0..1439 et un idle set résolu.",
        "creature.ambient.interaction_invalid" =>
            "Les interactions exigent cible, animation et cooldown borné.",
        "creature.autolevel.template_invalid" =>
            "Le template d’auto-level exige cible, seed et allocations cohérentes.",
        "creature.autolevel.reference_missing" =>
            "Les pistes, compétences et talents doivent exister dans la progression de la créature.",
        _ => fallback,
    };

    private static string RoleSummaryUx4M(CreatureRoleBehaviorProfile? profile) => profile is null
        ? "Profil rôle et comportement désactivé."
        : $"Rôle {profile.Role.RulesetTemplateId} · {profile.Role.LoreFields.Count} champ(s) de lore · " +
          $"{profile.Role.RoleTags.Count} tag(s).";

    private static string AmbientSummaryUx4M(CreatureRoleBehaviorProfile? profile) => profile is null
        ? "Activité ambiante absente."
        : $"Graphe {profile.Ambient.GraphId} · {profile.Ambient.IdleSets.Count} idle set(s) · " +
          $"{profile.Ambient.Schedules.Count} horaire(s) · {profile.Ambient.Interactions.Count} interaction(s) · " +
          $"reprise {profile.Ambient.Interruption.ResumeMode}.";

    private static string AutoLevelSummaryUx4M(CreatureRoleBehaviorProfile? profile,
        CreatureProgressionProfile? progression)
    {
        if (profile is null) return "Auto-level absent.";
        var preview = CreatureRoleBehaviorRuntimeResolver.PreviewAutoLevel(profile, progression);
        return $"Template {profile.AutoLevel.TemplateId} · seed {profile.AutoLevel.Seed} · cible " +
               $"{profile.AutoLevel.TargetLevel} · {(profile.AutoLevel.ManualLock ? "verrouillé" :
                   $"{preview.Changes.Count} changement(s)")}.";
    }

    private static IReadOnlyList<string[]> ParseRowsUx4M(string value, int fields, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        var rows = value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('|', StringSplitOptions.TrimEntries)).ToArray();
        if (rows.Any(row => row.Length != fields))
            throw new FormatException($"Chaque {label} doit contenir exactement {fields} colonnes séparées par |.");
        return rows;
    }

    private static IReadOnlyDictionary<string, string> ParseMapUx4M(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) return new Dictionary<string, string>();
        var pairs = value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line =>
            {
                var separator = line.IndexOf('=');
                if (separator <= 0 || separator == line.Length - 1)
                    throw new FormatException($"Chaque {label} doit utiliser la forme clé=valeur.");
                return (Key: line[..separator].Trim(), Value: line[(separator + 1)..].Trim());
            }).ToArray();
        if (pairs.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() != pairs.Length)
            throw new FormatException($"Les clés de {label} doivent être uniques.");
        return pairs.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> SplitCsvUx4M(string value) => value
        .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToArray();

    private static string JoinCsvUx4M(IEnumerable<string> values) => string.Join(", ", values);
    private static string JoinMapUx4M(IReadOnlyDictionary<string, string> values) =>
        string.Join('\n', values.Select(item => $"{item.Key}={item.Value}"));
    private static string? EmptyUx4M(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static string FormatUx4M(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static bool IsCanonicalIdentifierUx4M(string value) =>
        System.Text.RegularExpressions.Regex.IsMatch(value, "^[a-z0-9]+(?:[._-][a-z0-9]+)*$",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static double ParseDoubleUx4M(string value, string label) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"{label} doit être un nombre invariant explicite.");

    private static int ParseIntUx4M(string value, string label) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"{label} doit être un entier explicite.");

    private static Guid StableCreatureRoleBehaviorIdUx4M(Guid sourceId, string discriminator)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"ux4-m|{sourceId:D}|{discriminator}"));
        var guidBytes = bytes[..16];
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x40);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        return new Guid(guidBytes);
    }

    private static string FingerprintProjectUx4M(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));

    private static string FingerprintCreatureRoleBehaviorCandidateUx4M(ProjectDefinition project,
        BlueprintId blueprintId, StudioCreatureRoleBehaviorCandidateUx4M candidate)
    {
        var source = project.Blueprints.Single(item => item.Id == blueprintId);
        var progression = candidate.Profile?.AutoLevel.ApplyOnSave == true
            ? CreatureRoleBehaviorRuntimeResolver.PreviewAutoLevel(candidate.Profile, source.CreatureProgression)
                .Result
            : source.CreatureProgression;
        return FingerprintProjectUx4M(WithCreatureRoleBehaviorUx4M(project, blueprintId, progression,
            candidate.Profile));
    }

    private static string FingerprintCreatureRoleBehaviorDraftUx4M(StudioCreatureRoleBehaviorDraftUx4M draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(draft.BaseRevision.Value);
        WriteUx4M(writer, draft.SourceProjectFingerprint);
        WriteUx4M(writer, draft.BlueprintId.ToString());
        writer.Write(draft.Enabled); writer.Write(draft.RequiredAtRuntime);
        WriteUx4M(writer, draft.RulesetTemplateId); WriteUx4M(writer, draft.LoreFields);
        WriteUx4M(writer, draft.RoleTags); WriteUx4M(writer, draft.AmbientGraphId);
        WriteUx4M(writer, draft.IdleSets); WriteUx4M(writer, draft.Schedules);
        WriteUx4M(writer, draft.Interactions); writer.Write(draft.InterruptOnCombat);
        writer.Write(draft.InterruptOnConversation); writer.Write(draft.InterruptOnMovement);
        writer.Write((int)draft.ResumeMode); WriteUx4M(writer, draft.ResumeDelaySeconds);
        WriteUx4M(writer, draft.AutoLevelTemplateId); WriteUx4M(writer, draft.AutoLevelSeed);
        WriteUx4M(writer, draft.TargetLevel); writer.Write(draft.ApplyOnSave); writer.Write(draft.ManualLock);
        WriteUx4M(writer, draft.TrackAllocations); WriteUx4M(writer, draft.SkillAllocations);
        WriteUx4M(writer, draft.TalentUnlocks); WriteUx4M(writer, draft.Overrides);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteUx4M(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static bool DeleteUx4MSmokeRoot(string root)
    {
        try
        {
            var full = Path.GetFullPath(root);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) +
                       Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4m-smoke-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(full)) Directory.Delete(full, true);
            return !Directory.Exists(full);
        }
        catch
        {
            return false;
        }
    }
}
