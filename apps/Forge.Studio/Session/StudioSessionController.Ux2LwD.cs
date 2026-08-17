using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Domain.Rules;
using Forge.Domain.RuntimePolicies;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2LwDProvenance = "studio:ux2-lw-d:area-combat-policy";
    private readonly SetAreaRuntimePoliciesHandler _setAreaCombatPoliciesUx2LwD = new(new SystemClock());
    private StudioUx2LwDDraft? _approvedAreaCombatUx2LwD;
    private ProjectRevision? _approvedAreaCombatRevisionUx2LwD;
    private string? _approvedAreaCombatSourceFingerprintUx2LwD;
    private string? _approvedAreaCombatDraftFingerprintUx2LwD;

    internal IReadOnlyList<StudioUx2LwDAreaChoice> AreaCombatChoicesUx2LwD
    {
        get
        {
            var project = Project;
            if (project is null)
            {
                return [];
            }
            var evidenceTarget = ActiveAreaId ?? project.Areas.FirstOrDefault()?.Id;
            var duplicates = project.Areas.GroupBy(area => area.DisplayName,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(),
                    StringComparer.CurrentCultureIgnoreCase);
            return project.Areas.Select(area =>
                {
                    var name = HumanAreaCombatNameUx2LwD(area);
                    var label = duplicates[area.DisplayName] > 1
                        ? $"{name} · {HumanAreaCombatTokenUx2LwD(area.Slug)}"
                        : name;
                    var combat = area.RuntimePolicies.Rules.Combat;
                    return new StudioUx2LwDAreaChoice(area.Id,
                        $"{label} · {HumanPvpModeUx2LwD(combat.PvpMode)} · " +
                        (combat.AggroEnabled ? "aggro active" : "aggro suspendue"),
                        area.Id == evidenceTarget);
                })
                .OrderBy(choice => choice.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal StudioUx2LwDDraft? CreateAreaCombatDraftUx2LwD(AreaId? areaId)
    {
        var history = History;
        var area = areaId is null ? null : Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (history is null || area is null)
        {
            return null;
        }
        var combat = area.RuntimePolicies.Rules.Combat;
        return new StudioUx2LwDDraft(
            area.Id,
            history.State.Revision,
            AreaCombatSourceFingerprintUx2LwD(area),
            HumanAreaCombatNameUx2LwD(area),
            combat.PvpMode,
            combat.FriendlyFire,
            combat.CrimeEnabled,
            combat.AggroEnabled,
            combat.EnvironmentalDamageEnabled,
            combat.ServerAuthoritative,
            combat.Provenance);
    }

    internal StudioUx2LwDPreview PreviewAreaCombatUx2LwD(StudioUx2LwDDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelAreaCombatPreviewUx2LwD();
        var history = History;
        if (history is null)
        {
            return FailedAreaCombatPreviewUx2LwD("Aucun module n’est ouvert.");
        }
        if (!TryBuildAreaCombatCandidateUx2LwD(draft, dryRun: true, out var command,
                out var sourceArea, out var candidateArea, out var diagnostics, out var message))
        {
            return new(false, false, message, new([]), diagnostics, string.Empty,
                BuildAreaCombatProjectionUx2LwD(draft));
        }
        var dryRun = _setAreaCombatPoliciesUx2LwD.Handle(history.State, command!);
        var diff = BuildAreaCombatDiffUx2LwD(sourceArea!, candidateArea!);
        if (!dryRun.Succeeded)
        {
            var commandDiagnostics = dryRun.Diagnostics.Select(item => new StudioUx2LwDDiagnostic(
                item.Code,
                $"areas/{draft.AreaId}/runtimePolicies/rules/combat",
                HumanizeAreaCombatDiagnosticUx2LwD(item.Code))).ToArray();
            return new(false, false,
                commandDiagnostics.FirstOrDefault()?.Message ?? "La commande canonique a refusé cet aperçu.",
                diff, commandDiagnostics, dryRun.Patch?.Summary ?? string.Empty,
                BuildAreaCombatProjectionUx2LwD(draft));
        }

        var canApply = diff.HasChanges;
        if (canApply)
        {
            _approvedAreaCombatUx2LwD = draft;
            _approvedAreaCombatRevisionUx2LwD = history.State.Revision;
            _approvedAreaCombatSourceFingerprintUx2LwD = draft.SourceFingerprint;
            _approvedAreaCombatDraftFingerprintUx2LwD = AreaCombatDraftFingerprintUx2LwD(draft);
        }
        var projection = BuildAreaCombatProjectionUx2LwD(draft);
        return new(true, canApply,
            canApply
                ? "Politique de combat prévisualisée par la commande canonique ; aucune décision live ni aucun dégât simulé."
                : "Cette fiche est déjà identique à la politique persistée ; aucune mutation à appliquer.",
            diff, diagnostics, dryRun.Patch?.Summary ?? string.Empty, projection);
    }

    internal StudioUx2LwDActionResult ApplyApprovedAreaCombatUx2LwD()
    {
        var history = History;
        var project = Project;
        var draft = _approvedAreaCombatUx2LwD;
        if (history is null || project is null || draft is null ||
            _approvedAreaCombatRevisionUx2LwD != history.State.Revision ||
            !string.Equals(_approvedAreaCombatSourceFingerprintUx2LwD, draft.SourceFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(_approvedAreaCombatDraftFingerprintUx2LwD,
                AreaCombatDraftFingerprintUx2LwD(draft), StringComparison.Ordinal))
        {
            CancelAreaCombatPreviewUx2LwD();
            return new(false, "Prévisualisez à nouveau cette politique avant de l’appliquer.");
        }
        var currentArea = project.Areas.SingleOrDefault(item => item.Id == draft.AreaId);
        if (currentArea is null || !string.Equals(draft.SourceFingerprint,
                AreaCombatSourceFingerprintUx2LwD(currentArea), StringComparison.Ordinal))
        {
            CancelAreaCombatPreviewUx2LwD();
            return new(false, "La zone a changé. Rechargez sa politique avant de l’appliquer.");
        }
        if (!TryBuildAreaCombatCandidateUx2LwD(draft, dryRun: false, out var command,
                out _, out _, out var diagnostics, out var message))
        {
            CancelAreaCombatPreviewUx2LwD();
            return new(false, diagnostics.FirstOrDefault()?.Message ?? message);
        }
        var applied = _setAreaCombatPoliciesUx2LwD.Handle(history.State, command!);
        if (!applied.Succeeded)
        {
            CancelAreaCombatPreviewUx2LwD();
            return new(false, HumanizeAreaCombatDiagnosticUx2LwD(applied.Diagnostics.FirstOrDefault()?.Code));
        }
        history.Record(applied);
        CancelAreaCombatPreviewUx2LwD();
        return new(true, "Politique de combat de zone appliquée par la commande canonique.");
    }

    internal void CancelAreaCombatPreviewUx2LwD()
    {
        _approvedAreaCombatUx2LwD = null;
        _approvedAreaCombatRevisionUx2LwD = null;
        _approvedAreaCombatSourceFingerprintUx2LwD = null;
        _approvedAreaCombatDraftFingerprintUx2LwD = null;
    }

    internal void CloseUx2LwD() => CancelAreaCombatPreviewUx2LwD();

    internal StudioUx2LwDApprovalSnapshot CaptureAreaCombatApprovalUx2LwD() => new(
        _approvedAreaCombatUx2LwD,
        _approvedAreaCombatRevisionUx2LwD,
        _approvedAreaCombatSourceFingerprintUx2LwD,
        _approvedAreaCombatDraftFingerprintUx2LwD);

    internal void RestoreAreaCombatApprovalUx2LwD(StudioUx2LwDApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedAreaCombatUx2LwD = snapshot.Draft;
        _approvedAreaCombatRevisionUx2LwD = snapshot.Revision;
        _approvedAreaCombatSourceFingerprintUx2LwD = snapshot.SourceFingerprint;
        _approvedAreaCombatDraftFingerprintUx2LwD = snapshot.DraftFingerprint;
    }

    private bool TryBuildAreaCombatCandidateUx2LwD(
        StudioUx2LwDDraft draft,
        bool dryRun,
        out SetAreaRuntimePoliciesCommand? command,
        out AreaDefinition? sourceArea,
        out AreaDefinition? candidateArea,
        out IReadOnlyList<StudioUx2LwDDiagnostic> diagnostics,
        out string message)
    {
        command = null;
        sourceArea = null;
        candidateArea = null;
        diagnostics = [];
        message = string.Empty;
        var history = History;
        var project = Project;
        if (history is null || project is null)
        {
            message = "Aucun module n’est ouvert.";
            return false;
        }
        if (draft.BaseRevision != history.State.Revision)
        {
            message = "Le projet a changé. Rechargez la zone avant de prévisualiser.";
            return false;
        }
        sourceArea = project.Areas.SingleOrDefault(item => item.Id == draft.AreaId);
        if (sourceArea is null)
        {
            message = "La zone choisie n’existe plus.";
            return false;
        }
        if (!string.Equals(draft.SourceFingerprint, AreaCombatSourceFingerprintUx2LwD(sourceArea),
                StringComparison.Ordinal))
        {
            message = "La politique de combat a changé. Rechargez la fiche avant de prévisualiser.";
            return false;
        }
        if (!Enum.IsDefined(draft.PvpMode) || !Enum.IsDefined(draft.FriendlyFire))
        {
            diagnostics = [new("studio.area_combat.enum_invalid",
                $"areas/{draft.AreaId}/runtimePolicies/rules/combat",
                "Choisissez des modes PvP et de tir allié proposés par la fiche.")];
            message = diagnostics[0].Message;
            return false;
        }

        try
        {
            var sourceProfile = sourceArea.RuntimePolicies;
            var sourceRules = sourceProfile.Rules;
            var sourceCombat = sourceRules.Combat;
            var combat = SameAreaCombatUx2LwD(draft, sourceCombat)
                ? sourceCombat
                : new AreaCombatPolicy(
                    sourceCombat.Id,
                    sourceCombat.SchemaVersion,
                    draft.PvpMode,
                    draft.FriendlyFire,
                    draft.CrimeEnabled,
                    draft.AggroEnabled,
                    draft.EnvironmentalDamageEnabled,
                    draft.ServerAuthoritative,
                    Ux2LwDProvenance);
            var rules = ReferenceEquals(combat, sourceCombat)
                ? sourceRules
                : new AreaRulesProfile(sourceRules.Id, sourceRules.SchemaVersion,
                    sourceRules.Perception, combat, Ux2LwDProvenance);
            var candidateProfile = ReferenceEquals(rules, sourceRules)
                ? sourceProfile
                : new AreaRuntimePolicyProfile(
                    sourceProfile.Id,
                    sourceProfile.SchemaVersion,
                    sourceProfile.Heartbeats,
                    sourceProfile.Rest,
                    Ux2LwDProvenance,
                    sourceProfile.Cinematics,
                    rules,
                    sourceProfile.Experience);
            candidateArea = sourceArea.WithRuntimePolicies(candidateProfile);
            command = new SetAreaRuntimePoliciesCommand(
                CommandId.From(Guid.NewGuid()),
                history.State.Revision,
                sourceArea.Id,
                candidateProfile,
                dryRun);
            diagnostics = draft.ServerAuthoritative
                ? []
                : [new StudioUx2LwDDiagnostic(
                    "studio.area_combat.server_authority_disabled",
                    $"areas/{draft.AreaId}/runtimePolicies/rules/combat/serverAuthoritative",
                    "Le runtime canonique refusera toute décision de combat tant que l’autorité serveur est désactivée.",
                    Blocking: false)];
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            diagnostics = [new("studio.area_combat.invalid",
                $"areas/{draft.AreaId}/runtimePolicies/rules/combat",
                "La politique de combat contient une valeur incompatible avec le contrat canonique.")];
            message = diagnostics[0].Message + $" ({exception.GetType().Name})";
            return false;
        }
    }

    private static bool SameAreaCombatUx2LwD(StudioUx2LwDDraft draft, AreaCombatPolicy source) =>
        draft.PvpMode == source.PvpMode &&
        draft.FriendlyFire == source.FriendlyFire &&
        draft.CrimeEnabled == source.CrimeEnabled &&
        draft.AggroEnabled == source.AggroEnabled &&
        draft.EnvironmentalDamageEnabled == source.EnvironmentalDamageEnabled &&
        draft.ServerAuthoritative == source.ServerAuthoritative;

    internal static bool AreaCombatNeighborsMatchUx2LwD(
        AreaDefinition source,
        AreaDefinition candidate)
    {
        var sourceProfile = source.RuntimePolicies;
        var candidateProfile = candidate.RuntimePolicies;
        if (candidateProfile.Id != sourceProfile.Id ||
            candidateProfile.Rules.Id != sourceProfile.Rules.Id ||
            candidateProfile.Rules.Combat.Id != sourceProfile.Rules.Combat.Id)
        {
            return false;
        }
        var expectedRules = new AreaRulesProfile(
            candidateProfile.Rules.Id,
            candidateProfile.Rules.SchemaVersion,
            sourceProfile.Rules.Perception,
            candidateProfile.Rules.Combat,
            candidateProfile.Rules.Provenance);
        var expectedProfile = new AreaRuntimePolicyProfile(
            candidateProfile.Id,
            candidateProfile.SchemaVersion,
            sourceProfile.Heartbeats,
            sourceProfile.Rest,
            candidateProfile.Provenance,
            sourceProfile.Cinematics,
            expectedRules,
            sourceProfile.Experience);
        var expected = source.WithRuntimePolicies(expectedProfile);
        return string.Equals(
            AreaJsonSerializer.Serialize(expected),
            AreaJsonSerializer.Serialize(candidate),
            StringComparison.Ordinal);
    }

    private static StudioUx2LwDSemanticDiff BuildAreaCombatDiffUx2LwD(
        AreaDefinition source,
        AreaDefinition candidate)
    {
        var before = source.RuntimePolicies.Rules.Combat;
        var after = candidate.RuntimePolicies.Rules.Combat;
        var changes = new List<string>();
        if (before.PvpMode != after.PvpMode)
            changes.Add($"PvP · {HumanPvpModeUx2LwD(after.PvpMode)}");
        if (before.FriendlyFire != after.FriendlyFire)
            changes.Add($"Tir allié · {HumanFriendlyFireUx2LwD(after.FriendlyFire)}");
        if (before.CrimeEnabled != after.CrimeEnabled)
            changes.Add(after.CrimeEnabled ? "Crime évalué par l’autorité" : "Actions criminelles refusées");
        if (before.AggroEnabled != after.AggroEnabled)
            changes.Add(after.AggroEnabled ? "Initiation hostile autorisée" : "Initiation hostile suspendue");
        if (before.EnvironmentalDamageEnabled != after.EnvironmentalDamageEnabled)
            changes.Add(after.EnvironmentalDamageEnabled
                ? "Dégâts environnementaux autorisables"
                : "Dégâts environnementaux refusés");
        if (before.ServerAuthoritative != after.ServerAuthoritative)
            changes.Add(after.ServerAuthoritative
                ? "Décision réservée au serveur"
                : "Autorité serveur désactivée · combat live bloqué");
        return new(changes);
    }

    private static StudioUx2LwDRuntimeProjection BuildAreaCombatProjectionUx2LwD(StudioUx2LwDDraft draft) => new(
        draft.ServerAuthoritative
            ? "Le runtime exige une décision SinglePlayer/Serveur avant tout coût ou dégât."
            : "Bloqué : la politique ne permet aucune décision de combat live.",
        draft.PvpMode switch
        {
            AreaPvpMode.Disabled => "Les dégâts entre joueurs sont refusés.",
            AreaPvpMode.DuelsOnly => "Un duel autorisé est requis entre joueurs.",
            AreaPvpMode.HostileFactions => "Une hostilité de factions est requise entre joueurs.",
            AreaPvpMode.Open => "Le PvP est ouvert, sous réserve du tir allié.",
            _ => "Mode PvP non reconnu.",
        },
        draft.FriendlyFire switch
        {
            FriendlyFireMode.Disabled => "Les dégâts contre un allié sont refusés.",
            FriendlyFireMode.PartyOnly => "Les alliés de groupe restent protégés.",
            FriendlyFireMode.Enabled => "Le tir allié peut être autorisé.",
            _ => "Mode de tir allié non reconnu.",
        },
        draft.CrimeEnabled
            ? "Les actions criminelles peuvent être évaluées."
            : "Toute action marquée criminelle est refusée.",
        draft.AggroEnabled
            ? "L’initiation d’un combat hostile peut être autorisée."
            : "Toute initiation d’un combat hostile est refusée avant le dégât.",
        draft.EnvironmentalDamageEnabled
            ? "Les dégâts environnementaux peuvent être autorisés."
            : "Les dégâts environnementaux sont refusés.",
        "Projection de politique uniquement : aucun joueur, groupe, duel, faction, crime, dégât, moteur ou dispatch réseau n’est simulé.",
        EvaluatedLive: false);

    internal ProjectDefinition? CreateUx2LwDEvidenceProject() => Project;

    internal StudioUx2LwDSmokeResult RunUx2LwDContractSmoke()
    {
        var sourceSession = Session;
        var sourceProject = Project;
        var sourceHistory = History;
        if (sourceSession is null || sourceProject is null || sourceHistory is null)
        {
            return FailedUx2LwDSmoke();
        }
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceCanUndo = sourceHistory.CanUndo;
        var sourceCanRedo = sourceHistory.CanRedo;
        var sourceAreaId = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelectedEntity = SelectedEntityId;
        var sourceApproval = CaptureAreaCombatApprovalUx2LwD();
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-lw-d-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var created = ProjectSession.Create(tempRoot, sourceProject);
            if (!created.Succeeded || !editing.Open(tempRoot, ActiveAreaId?.ToString()).Succeeded)
                return FailedUx2LwDSmoke();

            var choices = editing.AreaCombatChoicesUx2LwD;
            var explicitSelection = editing.CreateAreaCombatDraftUx2LwD(null) is null && choices.Count > 0;
            var target = choices.FirstOrDefault(item => item.IsEvidenceTarget) ?? choices.FirstOrDefault();
            if (target is null)
                return FailedUx2LwDSmoke() with { ExplicitSelection = explicitSelection };

            StudioUx2LwDDraft Configure(StudioUx2LwDDraft draft) => draft with
            {
                PvpMode = draft.PvpMode == AreaPvpMode.Open ? AreaPvpMode.DuelsOnly : AreaPvpMode.Open,
                FriendlyFire = draft.FriendlyFire == FriendlyFireMode.Enabled
                    ? FriendlyFireMode.Disabled
                    : FriendlyFireMode.Enabled,
                CrimeEnabled = !draft.CrimeEnabled,
                AggroEnabled = !draft.AggroEnabled,
                EnvironmentalDamageEnabled = !draft.EnvironmentalDamageEnabled,
                ServerAuthoritative = true,
            };

            var initial = Configure(editing.CreateAreaCombatDraftUx2LwD(target.AreaId)!);
            var noAuthority = editing.PreviewAreaCombatUx2LwD(initial with { ServerAuthoritative = false });
            var serverAuthorityDiagnostic = noAuthority.Succeeded && noAuthority.Diagnostics.Any(item =>
                item.Code == "studio.area_combat.server_authority_disabled" && !item.Blocking) &&
                noAuthority.Runtime.Authority.Contains("Bloqué", StringComparison.OrdinalIgnoreCase);
            var aggroDraft = initial with { AggroEnabled = false };
            var aggroPreview = editing.PreviewAreaCombatUx2LwD(aggroDraft);
            var aggroPolicyApplied = aggroPreview.Succeeded &&
                                     aggroPreview.Runtime.Aggro.Contains("refusée avant le dégât",
                                         StringComparison.OrdinalIgnoreCase) &&
                                     !aggroPreview.Runtime.EvaluatedLive;
            var structuredDiagnostics = noAuthority.Diagnostics.All(item =>
                !string.IsNullOrWhiteSpace(item.Code) && !string.IsNullOrWhiteSpace(item.Path));

            var beforePreviewProject = editing.Project;
            var beforePreviewHistory = editing.History!;
            var beforePreviewRevision = beforePreviewHistory.State.Revision;
            var beforePreviewJournal = beforePreviewHistory.Journal.ToArray();
            var beforePreviewDirty = editing.IsDirty;
            var preview = editing.PreviewAreaCombatUx2LwD(initial);
            var afterPreviewHistory = editing.History;
            var dryRunPure = preview.Succeeded && ReferenceEquals(beforePreviewProject, editing.Project) &&
                             ReferenceEquals(beforePreviewHistory, afterPreviewHistory) &&
                             afterPreviewHistory?.State.Revision == beforePreviewRevision &&
                             (afterPreviewHistory?.Journal ?? []).SequenceEqual(beforePreviewJournal) &&
                             editing.IsDirty == beforePreviewDirty;
            var runtimeProjection = preview.Runtime is { EvaluatedLive: false } &&
                                    preview.Runtime.Boundary.Contains("aucun joueur",
                                        StringComparison.OrdinalIgnoreCase) &&
                                    preview.Runtime.Authority.Contains("avant tout coût ou dégât",
                                        StringComparison.OrdinalIgnoreCase);

            var staleDraft = Configure(editing.CreateAreaCombatDraftUx2LwD(target.AreaId)!);
            var revisionMutation = ApplyUx2LwDRevisionMutation(editing, target.AreaId);
            var stalePreview = editing.PreviewAreaCombatUx2LwD(staleDraft);
            var staleDraftBlocked = revisionMutation && !stalePreview.Succeeded &&
                                    stalePreview.Message.Contains("projet a changé", StringComparison.OrdinalIgnoreCase) &&
                                    editing.Undo().Succeeded;

            var staleApprovalDraft = Configure(editing.CreateAreaCombatDraftUx2LwD(target.AreaId)!);
            var staleApprovalPreview = editing.PreviewAreaCombatUx2LwD(staleApprovalDraft);
            var staleApprovalMutation = ApplyUx2LwDRevisionMutation(editing, target.AreaId);
            var staleApprovalApply = editing.ApplyApprovedAreaCombatUx2LwD();
            var staleApprovalBlocked = staleApprovalPreview.Succeeded && staleApprovalMutation &&
                                       !staleApprovalApply.Succeeded && editing.Undo().Succeeded;

            var beforeApplyProject = editing.Project!;
            var beforeTarget = beforeApplyProject.Areas.Single(item => item.Id == target.AreaId);
            var beforeProfile = beforeTarget.RuntimePolicies;
            var neighboringAreas = beforeApplyProject.Areas.Where(item => item.Id != target.AreaId).ToArray();
            var fresh = Configure(editing.CreateAreaCombatDraftUx2LwD(target.AreaId)!);
            var freshPreview = editing.PreviewAreaCombatUx2LwD(fresh);
            var applied = editing.ApplyApprovedAreaCombatUx2LwD();
            var appliedProject = editing.Project!;
            var appliedTarget = appliedProject.Areas.Single(item => item.Id == target.AreaId);
            var appliedProfile = appliedTarget.RuntimePolicies;
            var canonicalCommand = freshPreview.Succeeded && applied.Succeeded &&
                                   editing.History!.Journal.LastOrDefault()?.CommandName ==
                                   "set_area_runtime_policies" &&
                                   appliedProfile.Rules.Combat.AggroEnabled == fresh.AggroEnabled;
            var heartbeatsPreserved = beforeProfile.Heartbeats.SequenceEqual(appliedProfile.Heartbeats) &&
                                      beforeProfile.Heartbeats.Zip(appliedProfile.Heartbeats, ReferenceEquals)
                                          .All(value => value);
            var restPreserved = ReferenceEquals(beforeProfile.Rest, appliedProfile.Rest);
            var cinematicsPreserved = ReferenceEquals(beforeProfile.Cinematics, appliedProfile.Cinematics);
            var perceptionPreserved = ReferenceEquals(beforeProfile.Rules.Perception,
                appliedProfile.Rules.Perception);
            var experiencePreserved = ReferenceEquals(beforeProfile.Experience, appliedProfile.Experience);
            var targetNeighbors = ReferenceEquals(beforeTarget.Foundation, appliedTarget.Foundation) &&
                                  ReferenceEquals(beforeTarget.Navigation, appliedTarget.Navigation) &&
                                  ReferenceEquals(beforeTarget.Environment, appliedTarget.Environment) &&
                                  ReferenceEquals(beforeTarget.World, appliedTarget.World) &&
                                  ReferenceEquals(beforeTarget.Playtest, appliedTarget.Playtest) &&
                                  beforeTarget.Entities.SequenceEqual(appliedTarget.Entities) &&
                                  beforeTarget.Portals.SequenceEqual(appliedTarget.Portals) &&
                                  AreaCombatNeighborsMatchUx2LwD(beforeTarget, appliedTarget);
            var projectNeighbors = ReferenceEquals(beforeApplyProject.Module, appliedProject.Module) &&
                                   beforeApplyProject.Blueprints.SequenceEqual(appliedProject.Blueprints) &&
                                   beforeApplyProject.Dialogues.SequenceEqual(appliedProject.Dialogues) &&
                                   beforeApplyProject.Quests.SequenceEqual(appliedProject.Quests) &&
                                   neighboringAreas.All(neighbor => ReferenceEquals(neighbor,
                                       appliedProject.Areas.Single(item => item.Id == neighbor.Id)));

            var expectedArea = AreaJsonSerializer.Serialize(appliedTarget);
            var undo = editing.Undo();
            var undone = undo.Succeeded && ReferenceEquals(beforeApplyProject, editing.Project);
            var redo = editing.Redo();
            var redone = redo.Succeeded && ReferenceEquals(appliedProject, editing.Project);
            var undoRedo = undone && redone;
            var saved = editing.Save();
            var opened = reopened.Open(tempRoot, target.AreaId.ToString());
            var restoredProject = reopened.Project;
            var restoredTarget = restoredProject?.Areas.SingleOrDefault(item => item.Id == target.AreaId);
            var persistence = saved.Succeeded && opened.Succeeded && restoredProject is not null &&
                              restoredTarget is not null &&
                              string.Equals(expectedArea, AreaJsonSerializer.Serialize(restoredTarget),
                                  StringComparison.Ordinal) &&
                              ProjectRoundTripMatchesUx2A(appliedProject, restoredProject, target.AreaId);

            var humanText = !ContainsTechnicalIdentifierUx2LwD(string.Join(' ',
                choices.Select(item => item.Label)) + " " +
                string.Join(' ', freshPreview.Diff.Changes) + " " + freshPreview.Runtime.Boundary);
            var currentSourceHistory = History;
            var sourceUntouched = ReferenceEquals(sourceProject, Project) &&
                                  ReferenceEquals(sourceHistory, currentSourceHistory) &&
                                  currentSourceHistory?.State.Revision == sourceRevision &&
                                  (currentSourceHistory?.Journal ?? []).SequenceEqual(sourceJournal) &&
                                  currentSourceHistory?.CanUndo == sourceCanUndo &&
                                  currentSourceHistory?.CanRedo == sourceCanRedo &&
                                  IsDirty == sourceDirty && ActiveAreaId == sourceAreaId &&
                                  SelectedEntityIds.SequenceEqual(sourceSelection) &&
                                  SelectedEntityId == sourceSelectedEntity &&
                                  CaptureAreaCombatApprovalUx2LwD() == sourceApproval;
            var succeeded = explicitSelection && dryRunPure && runtimeProjection &&
                            serverAuthorityDiagnostic && aggroPolicyApplied && staleDraftBlocked &&
                            staleApprovalBlocked && canonicalCommand && undoRedo && persistence &&
                            targetNeighbors && projectNeighbors && heartbeatsPreserved && restPreserved &&
                            cinematicsPreserved && perceptionPreserved && experiencePreserved &&
                            structuredDiagnostics && humanText && sourceUntouched;
            return new(succeeded, explicitSelection, dryRunPure, runtimeProjection,
                serverAuthorityDiagnostic, aggroPolicyApplied, staleDraftBlocked, staleApprovalBlocked,
                canonicalCommand, undoRedo, persistence, targetNeighbors, projectNeighbors,
                heartbeatsPreserved, restPreserved, cinematicsPreserved, perceptionPreserved,
                experiencePreserved, structuredDiagnostics, humanText, sourceUntouched);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_LW_D_CONTRACT_FAILED " +
                              $"type={exception.GetType().Name} message={exception.Message}");
            return FailedUx2LwDSmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            DeleteUx2LwDTemporaryWorkspace(tempRoot);
        }
    }

    internal static bool DeleteUx2LwDTemporaryWorkspace(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return true;
        try
        {
            var root = Path.GetFullPath(directory);
            var temp = Path.GetFullPath(Path.GetTempPath());
            var prefix = temp.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                         Path.DirectorySeparatorChar;
            var leaf = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
            if (!root.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !leaf.StartsWith("openrpg-forge-studio-ux2-lw-d-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
            return !Directory.Exists(root);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool ApplyUx2LwDRevisionMutation(StudioSessionController controller, AreaId areaId)
    {
        var history = controller.History;
        var area = controller.Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (history is null || area is null)
            return false;

        var profile = area.RuntimePolicies;
        var rules = profile.Rules;
        var source = rules.Combat;
        var combat = new AreaCombatPolicy(
            source.Id,
            source.SchemaVersion,
            source.PvpMode,
            source.FriendlyFire,
            source.CrimeEnabled,
            source.AggroEnabled,
            !source.EnvironmentalDamageEnabled,
            source.ServerAuthoritative,
            Ux2LwDProvenance);
        var candidateRules = new AreaRulesProfile(rules.Id, rules.SchemaVersion,
            rules.Perception, combat, Ux2LwDProvenance);
        var candidate = new AreaRuntimePolicyProfile(profile.Id, profile.SchemaVersion,
            profile.Heartbeats, profile.Rest, Ux2LwDProvenance,
            profile.Cinematics, candidateRules, profile.Experience);
        var result = new SetAreaRuntimePoliciesHandler(new SystemClock()).Handle(history.State,
            new SetAreaRuntimePoliciesCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                area.Id, candidate, DryRun: false));
        if (!result.Succeeded)
            return false;
        history.Record(result);
        return true;
    }

    private static string AreaCombatSourceFingerprintUx2LwD(AreaDefinition area) =>
        HashTypedAreaCombatUx2LwD(builder =>
        {
            AppendTypedAreaCombatUx2LwD(builder, "route", "ORF-AREA-027");
            AppendTypedAreaCombatUx2LwD(builder, "area", AreaJsonSerializer.Serialize(area));
        });

    private static string AreaCombatDraftFingerprintUx2LwD(StudioUx2LwDDraft draft) =>
        HashTypedAreaCombatUx2LwD(builder =>
        {
            AppendTypedAreaCombatUx2LwD(builder, "area", draft.AreaId.ToString());
            AppendTypedAreaCombatUx2LwD(builder, "revision",
                draft.BaseRevision.Value.ToString(CultureInfo.InvariantCulture));
            AppendTypedAreaCombatUx2LwD(builder, "source", draft.SourceFingerprint);
            AppendTypedAreaCombatUx2LwD(builder, "display", draft.AreaDisplayName);
            AppendTypedAreaCombatUx2LwD(builder, "pvp", ((int)draft.PvpMode).ToString(CultureInfo.InvariantCulture));
            AppendTypedAreaCombatUx2LwD(builder, "friendly",
                ((int)draft.FriendlyFire).ToString(CultureInfo.InvariantCulture));
            AppendTypedAreaCombatUx2LwD(builder, "crime", draft.CrimeEnabled ? "1" : "0");
            AppendTypedAreaCombatUx2LwD(builder, "aggro", draft.AggroEnabled ? "1" : "0");
            AppendTypedAreaCombatUx2LwD(builder, "environment",
                draft.EnvironmentalDamageEnabled ? "1" : "0");
            AppendTypedAreaCombatUx2LwD(builder, "server", draft.ServerAuthoritative ? "1" : "0");
            AppendTypedAreaCombatUx2LwD(builder, "provenance", draft.Provenance);
        });

    private static string HashTypedAreaCombatUx2LwD(Action<StringBuilder> append)
    {
        var builder = new StringBuilder();
        append(builder);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static void AppendTypedAreaCombatUx2LwD(StringBuilder builder, string name, string value)
    {
        builder.Append(name.Length).Append(':').Append(name).Append('=')
            .Append(value.Length).Append(':').Append(value).Append(';');
    }

    private static StudioUx2LwDPreview FailedAreaCombatPreviewUx2LwD(string message) => new(
        false, false, message, new([]), [], string.Empty, EmptyAreaCombatProjectionUx2LwD());

    private static StudioUx2LwDRuntimeProjection EmptyAreaCombatProjectionUx2LwD() => new(
        "Non évalué", "Non évalué", "Non évalué", "Non évalué", "Non évalué", "Non évalué",
        "Aucune politique n’a été projetée ; aucun combat, dégât, moteur ou réseau n’a été simulé.", false);

    private static StudioUx2LwDSmokeResult FailedUx2LwDSmoke() => new(
        false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false);

    private static string HumanAreaCombatNameUx2LwD(AreaDefinition area) =>
        ContainsTechnicalIdentifierUx2LwD(area.DisplayName)
            ? "Zone sans nom lisible"
            : area.DisplayName.Trim();

    internal static string HumanAreaCombatTokenUx2LwD(string value)
    {
        if (ContainsTechnicalIdentifierUx2LwD(value))
            return "référence interne masquée";
        var words = (value ?? string.Empty).Replace('_', ' ').Replace('-', ' ').Replace('.', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length == 0
            ? "Sans nom"
            : string.Join(' ', words.Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    }

    internal static string HumanPvpModeUx2LwD(AreaPvpMode mode) => mode switch
    {
        AreaPvpMode.Disabled => "PvP désactivé",
        AreaPvpMode.DuelsOnly => "Duels consentis",
        AreaPvpMode.HostileFactions => "Factions hostiles",
        AreaPvpMode.Open => "PvP ouvert",
        _ => "PvP non reconnu",
    };

    internal static string HumanFriendlyFireUx2LwD(FriendlyFireMode mode) => mode switch
    {
        FriendlyFireMode.Disabled => "Tir allié désactivé",
        FriendlyFireMode.PartyOnly => "Groupe protégé",
        FriendlyFireMode.Enabled => "Tir allié autorisé",
        _ => "Tir allié non reconnu",
    };

    internal static bool ContainsTechnicalIdentifierUx2LwD(string text) =>
        !string.IsNullOrWhiteSpace(text) && Regex.IsMatch(text,
            @"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\b",
            RegexOptions.CultureInvariant);

    private static string HumanizeAreaCombatDiagnosticUx2LwD(string? code) => code switch
    {
        "studio.area_combat.server_authority_disabled" =>
            "Activez l’autorité serveur pour autoriser une décision de combat au runtime.",
        "studio.area_combat.enum_invalid" =>
            "Choisissez les modes PvP et de tir allié proposés par la fiche.",
        "studio.area_combat.invalid" =>
            "Vérifiez les valeurs de la politique de combat.",
        null => "La commande canonique n’a pas pu valider cette politique.",
        _ => "La commande canonique a refusé cette politique ; consultez le chemin localisé.",
    };
}
