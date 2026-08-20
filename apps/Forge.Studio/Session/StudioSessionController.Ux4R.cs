using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Assistant;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Bosses;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly ApplyAssistedBossHandler _assistedBossUx4R = new(new SystemClock());
    private readonly UpsertBlueprintHandler _bossNeighborUx4R = new(new SystemClock());
    private StudioBossDraftUx4R? _approvedBossDraftUx4R;
    private AssistedBossProposal? _approvedBossProposalUx4R;
    private ProjectRevision? _approvedBossRevisionUx4R;
    private string? _approvedBossProjectFingerprintUx4R;
    private string? _approvedBossDraftFingerprintUx4R;
    private Guid _approvedBossTransactionUx4R;
    private Guid _approvedBossPlanUx4R;

    internal IReadOnlyList<StudioBossAreaChoiceUx4R> BossAreaChoicesUx4R =>
        Project?.Areas.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => new StudioBossAreaChoiceUx4R(item.Id, $"{item.DisplayName} · {item.Slug}"))
            .ToArray() ?? [];

    internal IReadOnlyList<StudioBossBlueprintChoiceUx4R> BossBlueprintChoicesUx4R =>
        Project?.Blueprints.Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn or BlueprintKind.Item)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => new StudioBossBlueprintChoiceUx4R(item.Slug, $"{item.DisplayName} · {item.Slug}",
                item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn, item.Kind == BlueprintKind.Item))
            .ToArray() ?? [];

    internal IReadOnlyList<StudioBossScriptChoiceUx4R> BossScriptChoicesUx4R => FindForgeScripts()
        .Select(item => new StudioBossScriptChoiceUx4R(item.RelativePath, item.DisplayName)).ToArray();

    internal StudioBossDraftUx4R? CreateBossDraftUx4R(AreaId areaId)
    {
        var project = Project; var history = History;
        var area = project?.Areas.SingleOrDefault(item => item.Id == areaId);
        var scripts = BossScriptChoicesUx4R;
        var summon = BossBlueprintChoicesUx4R.FirstOrDefault(item => item.Creature);
        var reward = BossBlueprintChoicesUx4R.FirstOrDefault(item => item.Item);
        if (project is null || history is null || area is null || scripts.Count < 2 || summon is null) return null;
        var slug = NextBossSlugUx4R(project);
        var bossId = StableBossGuidUx4R(project.Id.Value, $"boss:{slug}");
        var blueprintId = StableBossGuidUx4R(bossId, "blueprint");
        return new(history.State.Revision, FingerprintBossProjectUx4R(project), bossId, blueprintId, areaId,
            slug, "Gardienne des Braises", "Une gardienne en trois temps protège le cœur de la zone.",
            480, 24, 12, 18,
            ["Trait de braise", "Cercle de feu", "Couronne ardente"],
            scripts.Take(3).Select(item => item.RelativePath).ToArray(), [summon.Slug],
            [reward?.Slug ?? "récompense-des-braises"], scripts[^1].RelativePath,
            ["contexte-projet#boss"], ["combat télégraphié", "lecture claire"], "studio-author", 20260820, false);
    }

    internal StudioBossPreviewUx4R PreviewBossUx4R(StudioBossDraftUx4R draft)
    {
        ArgumentNullException.ThrowIfNull(draft); CancelBossUx4R();
        var project = Project; var history = History;
        if (project is null || history is null)
            return FailedBossUx4R("Aucun module n’est ouvert.", "studio.boss.project_missing");
        if (history.State.Revision != draft.BaseRevision ||
            !string.Equals(FingerprintBossProjectUx4R(project), draft.SourceProjectFingerprint, StringComparison.Ordinal))
            return FailedBossUx4R("Le projet a changé ; reconstruisez l’aperçu.",
                ApplyAssistedBossHandler.RevisionConflictCode);
        AssistedBossProposal proposal;
        try
        {
            proposal = AssistedBossCreationService.Generate(BuildBossBriefUx4R(draft), project,
                BossScriptChoicesUx4R.Select(item => item.RelativePath).ToHashSet(StringComparer.Ordinal),
                providerKey: "offline-studio", provenance: "assistant:studio:deterministic",
                promptSha256: FingerprintBossDraftUx4R(draft));
            proposal = proposal.WithApproval(draft.Approved,
                draft.Approved ? "author-approved:studio:ux4-r" : proposal.Provenance);
        }
        catch (ArgumentException exception)
        {
            return FailedBossUx4R(exception.Message, "assistant.boss.brief_invalid");
        }
        var transaction = Guid.NewGuid(); var plan = Guid.NewGuid();
        var command = new ApplyAssistedBossCommand(CommandId.From(Guid.NewGuid()), transaction, plan,
            history.State.Revision, proposal, AssistantPermissionLevel.ApplyApprovedPlan, true);
        var dryRun = _assistedBossUx4R.Handle(history.State, command);
        var diagnostics = proposal.Diagnostics.Select(item => new StudioBossDiagnosticUx4R(item.Code, item.Path,
                HumanBossMessageUx4R(item.Code, item.Message)))
            .Concat(dryRun.Diagnostics.Select(item => new StudioBossDiagnosticUx4R(item.Code, "bosses/preview",
                HumanBossMessageUx4R(item.Code, item.Message)))).ToArray();
        if (!dryRun.Succeeded)
            return new(false, false, HumanBossMessageUx4R(diagnostics[0].Code, diagnostics[0].HumanMessage),
                string.Empty, string.Empty, diagnostics, BossPackageSummaryUx4R(proposal),
                proposal.Balance.Summary, proposal);
        var frozen = FreezeBossDraftUx4R(draft);
        _approvedBossDraftUx4R = frozen; _approvedBossProposalUx4R = proposal;
        _approvedBossRevisionUx4R = history.State.Revision;
        _approvedBossProjectFingerprintUx4R = FingerprintBossProjectUx4R(project);
        _approvedBossDraftFingerprintUx4R = FingerprintBossDraftUx4R(frozen);
        _approvedBossTransactionUx4R = transaction; _approvedBossPlanUx4R = plan;
        return new(true, proposal.CanApply, "Boss prêt à appliquer après approbation explicite.",
            "Lot atomique : ajouter blueprint, combat, rencontre, phases, dialogue, récompenses et document runtime.",
            "Retirer le lot boss assisté et restaurer le projet précédent.", diagnostics,
            BossPackageSummaryUx4R(proposal), proposal.Balance.Summary, proposal);
    }

    internal StudioBossActionResultUx4R ApplyApprovedBossUx4R(StudioBossDraftUx4R current)
    {
        var project = Project; var history = History; var approved = _approvedBossDraftUx4R;
        var proposal = _approvedBossProposalUx4R;
        if (project is null || history is null || approved is null || proposal is null ||
            _approvedBossRevisionUx4R is null || _approvedBossProjectFingerprintUx4R is null ||
            _approvedBossDraftFingerprintUx4R is null || _approvedBossTransactionUx4R == Guid.Empty ||
            _approvedBossPlanUx4R == Guid.Empty)
            return new(false, "Prévisualisez et approuvez d’abord le boss.");
        var stale = history.State.Revision != _approvedBossRevisionUx4R ||
                    !string.Equals(FingerprintBossProjectUx4R(project), _approvedBossProjectFingerprintUx4R,
                        StringComparison.Ordinal) ||
                    !string.Equals(FingerprintBossDraftUx4R(approved), _approvedBossDraftFingerprintUx4R,
                        StringComparison.Ordinal) ||
                    !string.Equals(FingerprintBossDraftUx4R(current), _approvedBossDraftFingerprintUx4R,
                        StringComparison.Ordinal);
        if (stale) { CancelBossUx4R(); return new(false, "Le brouillon ou le projet a changé."); }
        var preview = _assistedBossUx4R.Handle(history.State, new ApplyAssistedBossCommand(
            CommandId.From(Guid.NewGuid()), _approvedBossTransactionUx4R, _approvedBossPlanUx4R,
            history.State.Revision, proposal, AssistantPermissionLevel.ApplyApprovedPlan, true));
        if (!preview.Succeeded) { CancelBossUx4R(); return new(false, "Le re-dry-run refuse le boss."); }
        var result = _assistedBossUx4R.Handle(history.State, new ApplyAssistedBossCommand(
            CommandId.From(Guid.NewGuid()), _approvedBossTransactionUx4R, _approvedBossPlanUx4R,
            history.State.Revision, proposal, AssistantPermissionLevel.ApplyApprovedPlan, false));
        CancelBossUx4R();
        if (!result.Succeeded)
            return new(false, result.Diagnostics.FirstOrDefault()?.Message ?? "Application du boss refusée.");
        history.Record(result.Command);
        return new(true, result.Command.Patch?.Summary ?? "Boss assisté appliqué.");
    }

    internal StudioBossActionResultUx4R UndoBossUx4R()
    { CancelBossUx4R(); var result = Undo(); return new(result.Succeeded, result.Message); }
    internal StudioBossActionResultUx4R RedoBossUx4R()
    { CancelBossUx4R(); var result = Redo(); return new(result.Succeeded, result.Message); }
    internal void CancelBossUx4R()
    {
        _approvedBossDraftUx4R = null; _approvedBossProposalUx4R = null; _approvedBossRevisionUx4R = null;
        _approvedBossProjectFingerprintUx4R = null; _approvedBossDraftFingerprintUx4R = null;
        _approvedBossTransactionUx4R = Guid.Empty; _approvedBossPlanUx4R = Guid.Empty;
    }
    internal void CloseUx4R() => CancelBossUx4R();
    internal StudioBossApprovalSnapshotUx4R CaptureBossApprovalUx4R() => new(
        _approvedBossDraftUx4R, _approvedBossProposalUx4R, _approvedBossRevisionUx4R,
        _approvedBossProjectFingerprintUx4R, _approvedBossDraftFingerprintUx4R,
        _approvedBossTransactionUx4R, _approvedBossPlanUx4R);
    internal void RestoreBossApprovalUx4R(StudioBossApprovalSnapshotUx4R snapshot)
    {
        _approvedBossDraftUx4R = snapshot.Draft; _approvedBossProposalUx4R = snapshot.Proposal;
        _approvedBossRevisionUx4R = snapshot.Revision; _approvedBossProjectFingerprintUx4R = snapshot.ProjectFingerprint;
        _approvedBossDraftFingerprintUx4R = snapshot.DraftFingerprint;
        _approvedBossTransactionUx4R = snapshot.TransactionId; _approvedBossPlanUx4R = snapshot.ExecutionPlanId;
    }

    internal StudioBossEvidenceProjectUx4R? CreateUx4REvidenceProject()
    {
        var source = Project; if (source?.Areas.FirstOrDefault() is not { } area) return null;
        var blueprints = source.Blueprints.ToList(); var fallback = false;
        var summon = blueprints.FirstOrDefault(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn);
        if (summon is null)
        {
            fallback = true; summon = new(BlueprintId.From(StableBossGuidUx4R(source.Id.Value, "ux4-r:summon")),
                "sentinelle-temoin", "Sentinelle témoin", BlueprintKind.Creature); blueprints.Add(summon);
        }
        var reward = blueprints.FirstOrDefault(item => item.Kind == BlueprintKind.Item);
        if (reward is null)
        {
            fallback = true; reward = new(BlueprintId.From(StableBossGuidUx4R(source.Id.Value, "ux4-r:reward")),
                "braise-royale", "Braise royale", BlueprintKind.Item); blueprints.Add(reward);
        }
        var project = fallback ? new ProjectDefinition(source.Id, source.Title, source.Module, blueprints,
            source.Areas, source.Dialogues, source.Quests) : source;
        return new(project, area.Id, summon.Slug, reward.Slug, !fallback, fallback);
    }

    internal StudioUx4RSmokeResult RunUx4RContractSmoke()
    {
        var empty = new StudioUx4RSmokeResult(false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false);
        var evidence = CreateUx4REvidenceProject(); if (evidence is null) return empty;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4r-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController(); var reopened = new StudioSessionController();
        StudioUx4RSmokeResult Execute()
        {
            try
            {
                if (!ProjectSession.Create(root, evidence.Project).Succeeded || !EnsureUx4REvidenceScripts(root) ||
                    !editing.Open(root).Succeeded) return empty;
                var explicitSelection = editing.CreateBossDraftUx4R(evidence.AreaId) is not null &&
                                        editing.BossAreaChoicesUx4R.Count > 0;
                var draft = editing.CreateBossDraftUx4R(evidence.AreaId); if (draft is null) return empty;
                var configured = ConfigureBossEvidenceUx4R(draft, evidence);
                var scripts = editing.BossScriptChoicesUx4R;
                var realScripts = scripts.Count >= 3 && scripts.All(item =>
                    File.Exists(Path.Combine(root, item.RelativePath.Replace('/', Path.DirectorySeparatorChar))));
                var approvalRequired = !editing.PreviewBossUx4R(configured with { Approved = false }).Succeeded;
                var invalidScript = configured with { PhaseScripts = ["scripts/absent.forge", configured.PhaseScripts[1]] };
                var invalidScriptBlocked = !editing.PreviewBossUx4R(invalidScript).Succeeded;
                var before = editing.Project!; var beforeHistory = editing.History!;
                var preview = editing.PreviewBossUx4R(configured);
                var dryRun = preview.Succeeded && ReferenceEquals(before, editing.Project) &&
                             ReferenceEquals(beforeHistory, editing.History) && beforeHistory.State.Revision == draft.BaseRevision;
                var abilities = preview.Proposal?.Boss.Blueprint.Combat?.Abilities.Count >= 3;
                var phases = preview.Proposal?.Boss.Phases.Count >= 2;
                var balance = preview.Proposal?.Balance is { Passed: true, Runs: 32 };

                var neighbor = editing.Project!.Blueprints.First(item => item.Slug == evidence.SummonSlug);
                var neighborResult = editing._bossNeighborUx4R.Handle(editing.History!.State,
                    new UpsertBlueprintCommand(CommandId.From(Guid.NewGuid()), editing.History.State.Revision,
                        neighbor.Id, neighbor.Slug, neighbor.DisplayName + " témoin", neighbor.Kind, false, neighbor.Authoring));
                if (neighborResult.Succeeded) editing.History.Record(neighborResult);
                var staleSourceBlocked = preview.Succeeded && neighborResult.Succeeded &&
                    !editing.ApplyApprovedBossUx4R(configured).Succeeded;
                if (neighborResult.Succeeded) editing.Undo();

                draft = editing.CreateBossDraftUx4R(evidence.AreaId)!; configured = ConfigureBossEvidenceUx4R(draft, evidence);
                preview = editing.PreviewBossUx4R(configured);
                var staleApprovalBlocked = preview.Succeeded &&
                    !editing.ApplyApprovedBossUx4R(configured with { DisplayName = configured.DisplayName + " altérée" }).Succeeded;

                draft = editing.CreateBossDraftUx4R(evidence.AreaId)!; configured = ConfigureBossEvidenceUx4R(draft, evidence);
                var sourceProject = editing.Project!; var sourceBlueprints = sourceProject.Blueprints.ToArray();
                var sourceDialogues = sourceProject.Dialogues.ToArray(); var sourceAreas = sourceProject.Areas.ToArray();
                var finalPreview = editing.PreviewBossUx4R(configured);
                var apply = finalPreview.Succeeded ? editing.ApplyApprovedBossUx4R(configured) : new(false, "preview failed");
                var applied = editing.Project; var appliedJson = applied is null ? string.Empty : ProjectJsonSerializer.Serialize(applied);
                var canonical = apply.Succeeded && applied is not null &&
                    applied.Blueprints.Count == sourceBlueprints.Length + 1 &&
                    applied.Dialogues.Count == sourceDialogues.Length + 1 &&
                    applied.Module.RuntimePolicies.Extensions.JsonDocuments.Any(item => item.Key == $"boss.{configured.Slug}");
                var restoredNeighbors = applied is null ? null : new ProjectDefinition(
                    applied.Id, applied.Title, sourceProject.Module,
                    applied.Blueprints.Where(item => sourceBlueprints.Any(source => source.Id == item.Id)).ToArray(),
                    applied.Areas,
                    applied.Dialogues.Where(item => sourceDialogues.Any(source => source.Id == item.Id)).ToArray(),
                    applied.Quests);
                var neighbors = canonical && restoredNeighbors is not null &&
                    ProjectJsonSerializer.Serialize(restoredNeighbors) == ProjectJsonSerializer.Serialize(sourceProject);
                var undo = editing.UndoBossUx4R();
                var undone = undo.Succeeded && ProjectJsonSerializer.Serialize(editing.Project!) ==
                             ProjectJsonSerializer.Serialize(sourceProject);
                var redo = editing.RedoBossUx4R();
                var redone = redo.Succeeded && ProjectJsonSerializer.Serialize(editing.Project!) == appliedJson;
                var saved = editing.Save().Succeeded; editing.Close();
                var persistence = saved && reopened.Open(root).Succeeded &&
                                  ProjectJsonSerializer.Serialize(reopened.Project!) == appliedJson;
                reopened.Close();
                var succeeded = explicitSelection && realScripts && abilities && phases && balance && approvalRequired &&
                                invalidScriptBlocked && dryRun && staleSourceBlocked && staleApprovalBlocked && canonical &&
                                undone && redone && persistence && neighbors;
                return new(succeeded, explicitSelection, realScripts, abilities, phases, balance, approvalRequired,
                    invalidScriptBlocked, dryRun, staleSourceBlocked, staleApprovalBlocked, canonical, undone && redone,
                    persistence, neighbors, false);
            }
            catch { return empty; }
            finally { editing.Close(); reopened.Close(); }
        }
        var result = Execute(); var released = DeleteUx4RRoot(root);
        return result with { TemporaryWorkspaceReleased = released, Succeeded = result.Succeeded && released };
    }

    internal static bool EnsureUx4REvidenceScripts(string root)
    {
        try
        {
            var scripts = Path.Combine(root, "scripts"); Directory.CreateDirectory(scripts);
            for (var index = 1; index <= 3; index++)
            {
                var path = Path.Combine(scripts, $"boss_phase_{index}.forge");
                if (!File.Exists(path)) File.WriteAllText(path, "void main()\n{\n    say(\"Phase témoin.\");\n}\n");
            }
            var death = Path.Combine(scripts, "boss_death.forge");
            if (!File.Exists(death)) File.WriteAllText(death, "void main()\n{\n    say(\"Le boss est vaincu.\");\n}\n");
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return false; }
    }

    private static StudioBossDraftUx4R ConfigureBossEvidenceUx4R(StudioBossDraftUx4R draft,
        StudioBossEvidenceProjectUx4R evidence) => draft with
    {
        Slug = "gardienne-des-braises", DisplayName = "Gardienne des Braises",
        Synopsis = "Une gardienne en trois temps protège le cœur de la zone.", MaximumHealth = 480,
        Offense = 24, Defense = 12, ThreatRating = 18,
        AbilityNames = ["Trait de braise", "Cercle de feu", "Couronne ardente"],
        PhaseScripts = ["scripts/boss_phase_1.forge", "scripts/boss_phase_2.forge", "scripts/boss_phase_3.forge"],
        SummonBlueprintSlugs = [evidence.SummonSlug], RewardItemSlugs = [evidence.RewardSlug],
        DeathScript = "scripts/boss_death.forge", ContextCitations = ["contexte-projet#gardienne"],
        StyleRules = ["combat télégraphié", "lecture claire"], AuthorId = "studio-author", Seed = 20260820,
        Approved = true,
    };

    private static AssistedBossBrief BuildBossBriefUx4R(StudioBossDraftUx4R draft) => new(
        draft.BossId, draft.BlueprintId, draft.Slug.Trim(), draft.DisplayName.Trim(), draft.AreaId.Value,
        draft.Synopsis.Trim(), draft.MaximumHealth, draft.Offense, draft.Defense, draft.ThreatRating,
        draft.AbilityNames.Select(item => item.Trim()).ToArray(),
        draft.PhaseScripts.Select(item => item.Trim()).ToArray(),
        draft.SummonBlueprintSlugs.Select(item => item.Trim()).ToArray(),
        draft.RewardItemSlugs.Select(item => item.Trim()).ToArray(), draft.DeathScript.Trim(),
        draft.ContextCitations.Select(item => item.Trim()).ToArray(),
        draft.StyleRules.Select(item => item.Trim()).ToArray(), draft.AuthorId.Trim(), draft.Seed);

    private static StudioBossDraftUx4R FreezeBossDraftUx4R(StudioBossDraftUx4R draft) => draft with
    {
        AbilityNames = draft.AbilityNames.ToArray(), PhaseScripts = draft.PhaseScripts.ToArray(),
        SummonBlueprintSlugs = draft.SummonBlueprintSlugs.ToArray(), RewardItemSlugs = draft.RewardItemSlugs.ToArray(),
        ContextCitations = draft.ContextCitations.ToArray(), StyleRules = draft.StyleRules.ToArray(),
    };
    private static StudioBossPreviewUx4R FailedBossUx4R(string message, string code) => new(false, false,
        HumanBossMessageUx4R(code, message), string.Empty, string.Empty,
        [new(code, "bosses/preview", HumanBossMessageUx4R(code, message))],
        "Lot boss non évalué.", "Simulation non évaluée.", null);
    private static string HumanBossMessageUx4R(string code, string fallback) => code switch
    {
        ApplyAssistedBossHandler.ApprovalRequiredCode => "Cochez l’approbation auteur avant le dry-run.",
        ApplyAssistedBossHandler.PermissionDeniedCode => "La permission ApplyApprovedPlan est requise.",
        ApplyAssistedBossHandler.RevisionConflictCode => "Le projet a changé ; reconstruisez l’aperçu.",
        ApplyAssistedBossHandler.ProposalInvalidCode => "Le lot boss, ses dépendances ou son équilibrage sont invalides.",
        _ => fallback,
    };
    private static string BossPackageSummaryUx4R(AssistedBossProposal proposal) =>
        $"Lot : 1 blueprint, {proposal.Boss.Blueprint.Combat?.Abilities.Count ?? 0} capacités, " +
        $"{proposal.Boss.Phases.Count} phases, {proposal.Boss.IntroductionDialogue.Nodes.Count} nœuds de dialogue, " +
        $"{proposal.Boss.Rewards.Count} récompense(s), 1 document runtime.";
    private static string NextBossSlugUx4R(ProjectDefinition project)
    {
        const string root = "gardienne-des-braises"; var slug = root; var index = 2;
        while (project.Blueprints.Any(item => item.Slug == slug)) slug = $"{root}-{index++}";
        return slug;
    }
    private static string FingerprintBossProjectUx4R(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));
    private static string FingerprintBossDraftUx4R(StudioBossDraftUx4R draft) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(draft))));
    private static Guid StableBossGuidUx4R(Guid root, string discriminator)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{root:D}:{discriminator}"));
        Span<byte> value = stackalloc byte[16]; hash.AsSpan(0, 16).CopyTo(value);
        value[6] = (byte)((value[6] & 0x0f) | 0x40); value[8] = (byte)((value[8] & 0x3f) | 0x80);
        return new Guid(value);
    }
    private static bool DeleteUx4RRoot(string root)
    {
        try { if (Directory.Exists(root)) Directory.Delete(root, true); return !Directory.Exists(root); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return false; }
    }
}
