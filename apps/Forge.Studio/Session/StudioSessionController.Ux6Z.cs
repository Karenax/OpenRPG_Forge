using System.Security.Cryptography;
using System.Text;
using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Combat;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Abilities;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly ApplyAssistedAbilityHandler _assistedAbilityUx6Z = new(new SystemClock());
    private readonly UpsertBlueprintHandler _abilityNeighborUx6Z = new(new SystemClock());
    private StudioUx6ZDraft? _approvedAbilityDraftUx6Z;
    private AssistedAbilityProposal? _approvedAbilityProposalUx6Z;
    private ProjectRevision? _approvedAbilityRevisionUx6Z;
    private string? _approvedAbilityProjectFingerprintUx6Z;
    private string? _approvedAbilityDraftFingerprintUx6Z;
    private Guid _approvedAbilityTransactionUx6Z;
    private Guid _approvedAbilityPlanUx6Z;

    internal IReadOnlyList<StudioUx6ZOwnerChoice> AbilityOwnerChoicesUx6Z =>
        Project?.Blueprints
            .Where(item => item.Kind is (BlueprintKind.Creature or BlueprintKind.PlayerSpawn) &&
                           item.Combat is not null && !item.IsReadOnly())
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => new StudioUx6ZOwnerChoice(item.Id.Value,
                $"{item.DisplayName} · {HumanBlueprintKindUx6Z(item.Kind)} · réserve {item.Combat!.Stats.MaximumResource}",
                item.Combat.Stats.MaximumResource, item.Combat.Abilities.Count))
            .ToArray() ?? [];

    internal IReadOnlyList<StudioUx6ZScriptChoice> AbilityScriptChoicesUx6Z => FindForgeScripts()
        .Select(item => new StudioUx6ZScriptChoice(item.RelativePath, item.DisplayName))
        .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
        .ThenBy(item => item.RelativePath, StringComparer.Ordinal)
        .ToArray();

    internal IReadOnlyList<StudioUx6ZItemChoice> AbilityItemChoicesUx6Z =>
        Project?.Blueprints.Where(item => item.Kind == BlueprintKind.Item)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => new StudioUx6ZItemChoice(item.Slug, $"{item.DisplayName} · {item.Slug}"))
            .ToArray() ?? [];

    internal StudioUx6ZDraft? CreateAbilityDraftUx6Z(Guid ownerBlueprintId)
    {
        var project = Project;
        var history = History;
        var owner = project?.Blueprints.SingleOrDefault(item => item.Id.Value == ownerBlueprintId);
        var script = AbilityScriptChoicesUx6Z.FirstOrDefault();
        if (project is null || history is null || owner?.Combat is null || owner.IsReadOnly() ||
            owner.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn) || script is null)
            return null;
        var slug = NextAbilitySlugUx6Z(owner);
        var id = StableAbilityGuidUx6Z(project.Id.Value, $"{owner.Id.Value:D}:{slug}");
        var resource = Math.Max(0, Math.Min(owner.Combat.Stats.MaximumResource, 12));
        var item = AbilityItemChoicesUx6Z.FirstOrDefault();
        return new(history.State.Revision, ProjectFingerprintUx6Z(project), id, owner.Id.Value,
            slug, "Élan de braise", resource, 6, AbilityTargeting.SingleEnemy,
            [new StudioUx6ZEffectDraft(AbilityEffectKind.Damage, 18)],
            "vfx.elan-de-braise", "audio.elan-de-braise", script.RelativePath, item?.Slug,
            new HashSet<string>(["offensive", "telegraphiee"], StringComparer.Ordinal),
            "Attaque lisible à coût significatif, réservée à une fenêtre d’ouverture.",
            ["module:combat#porteur", "module:style#lisibilite"],
            ["combat télégraphié", "effet lisible"], "studio-author", 20260821, false);
    }

    internal StudioUx6ZPreview PreviewAssistedAbilityUx6Z(
        StudioUx6ZDraft draft,
        AssistantPermissionLevel permission = AssistantPermissionLevel.ApplyApprovedPlan)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelAbilityApprovalUx6Z();
        var project = Project;
        var history = History;
        if (project is null || history is null)
            return FailedAbilityUx6Z("Aucun module n’est ouvert.", "assistant.ability.project_missing");
        if (history.State.Revision != draft.BaseRevision ||
            !string.Equals(ProjectFingerprintUx6Z(project), draft.SourceProjectFingerprint,
                StringComparison.Ordinal))
            return FailedAbilityUx6Z("Le projet a changé ; rechargez la création de capacité.",
                ApplyAssistedAbilityHandler.RevisionConflictCode);
        AssistedAbilityProposal proposal;
        try
        {
            proposal = AssistedAbilityCreationService.Generate(BuildAbilityBriefUx6Z(draft), project,
                AbilityScriptChoicesUx6Z.Select(item => item.RelativePath).ToHashSet(StringComparer.Ordinal),
                providerKey: "offline-studio", estimatedCostUsd: 0,
                provenance: "assistant:studio:deterministic", promptSha256: AbilityDraftFingerprintUx6Z(draft));
            proposal = proposal.WithApproval(draft.Approved,
                draft.Approved ? "author-approved:studio:ux6-z" : proposal.Provenance);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return FailedAbilityUx6Z(exception.Message, "assistant.ability.brief_invalid");
        }

        var transaction = Guid.NewGuid();
        var plan = Guid.NewGuid();
        var dryRun = _assistedAbilityUx6Z.Handle(history.State, new ApplyAssistedAbilityCommand(
            CommandId.From(Guid.NewGuid()), transaction, plan, history.State.Revision, proposal, permission, true));
        var diagnostics = proposal.Diagnostics
            .Select(item => new StudioUx6ZDiagnostic(item.Code, item.Path,
                HumanAbilityMessageUx6Z(item.Code, item.Message)))
            .Concat(dryRun.Diagnostics.Select(item => new StudioUx6ZDiagnostic(item.Code, "ability/plan",
                HumanAbilityMessageUx6Z(item.Code, item.Message))))
            .GroupBy(item => (item.Code, item.Path))
            .Select(item => item.First())
            .ToArray();
        var diff = new[]
        {
            $"Ajouter « {proposal.Orchestration.Ability.DisplayName} » au profil de combat de {OwnerLabelUx6Z(project, draft.OwnerBlueprintId)}.",
            $"Ciblage {HumanTargetingUx6Z(proposal.Orchestration.Ability.Targeting)} · coût {proposal.Orchestration.Ability.ResourceCost} · recharge {proposal.Orchestration.Ability.CooldownSeconds:0.##} s.",
            $"{proposal.Orchestration.Ability.Effects.Count} effet(s) · VFX {proposal.Orchestration.Ability.VisualCue.CueKey} · audio {proposal.Orchestration.AudioCueKey}.",
            $"Créer le document runtime « {proposal.RuntimeDocument.Key} » avec cinq résultats de combat et la note d’équilibrage.",
        };
        var permissionSatisfied = permission >= AssistantPermissionLevel.ApplyApprovedPlan;
        if (!dryRun.Succeeded)
            return new(false, false, false, permissionSatisfied, true,
                diagnostics.FirstOrDefault()?.Message ?? "Le dry-run refuse la capacité.", string.Empty,
                string.Empty, diff, diagnostics, proposal);

        var frozen = FreezeAbilityDraftUx6Z(draft);
        _approvedAbilityDraftUx6Z = frozen;
        _approvedAbilityProposalUx6Z = proposal;
        _approvedAbilityRevisionUx6Z = history.State.Revision;
        _approvedAbilityProjectFingerprintUx6Z = ProjectFingerprintUx6Z(project);
        _approvedAbilityDraftFingerprintUx6Z = AbilityDraftFingerprintUx6Z(frozen);
        _approvedAbilityTransactionUx6Z = transaction;
        _approvedAbilityPlanUx6Z = plan;
        return new(true, proposal.CanApply, true, true, true,
            "Plan canonique prêt · cinq scénarios de combat et équilibrage validés.",
            dryRun.Command.Patch?.Summary ?? "Ajouter la capacité et son document runtime.",
            dryRun.Command.Inverse?.Summary ?? "Restaurer le combat et les extensions runtime précédents.",
            diff, diagnostics, proposal);
    }

    internal StudioUx6ZActionResult ApplyApprovedAbilityUx6Z(StudioUx6ZDraft current)
    {
        var project = Project;
        var history = History;
        var approved = _approvedAbilityDraftUx6Z;
        var proposal = _approvedAbilityProposalUx6Z;
        if (project is null || history is null || approved is null || proposal is null ||
            _approvedAbilityRevisionUx6Z is null || _approvedAbilityProjectFingerprintUx6Z is null ||
            _approvedAbilityDraftFingerprintUx6Z is null || _approvedAbilityTransactionUx6Z == Guid.Empty ||
            _approvedAbilityPlanUx6Z == Guid.Empty)
            return new(false, "Prévisualisez et approuvez d’abord la capacité.");
        var stale = history.State.Revision != _approvedAbilityRevisionUx6Z ||
                    !string.Equals(ProjectFingerprintUx6Z(project), _approvedAbilityProjectFingerprintUx6Z,
                        StringComparison.Ordinal) ||
                    !string.Equals(AbilityDraftFingerprintUx6Z(approved), _approvedAbilityDraftFingerprintUx6Z,
                        StringComparison.Ordinal) ||
                    !string.Equals(AbilityDraftFingerprintUx6Z(current), _approvedAbilityDraftFingerprintUx6Z,
                        StringComparison.Ordinal);
        if (stale)
        {
            CancelAbilityApprovalUx6Z();
            return new(false, "Le brouillon ou le projet a changé ; relancez le plan.");
        }
        var dryRun = _assistedAbilityUx6Z.Handle(history.State, new ApplyAssistedAbilityCommand(
            CommandId.From(Guid.NewGuid()), _approvedAbilityTransactionUx6Z, _approvedAbilityPlanUx6Z,
            history.State.Revision, proposal, AssistantPermissionLevel.ApplyApprovedPlan, true));
        if (!dryRun.Succeeded)
        {
            CancelAbilityApprovalUx6Z();
            return new(false, dryRun.Diagnostics.FirstOrDefault()?.Message ?? "Le re-dry-run refuse la capacité.");
        }
        var result = _assistedAbilityUx6Z.Handle(history.State, new ApplyAssistedAbilityCommand(
            CommandId.From(Guid.NewGuid()), _approvedAbilityTransactionUx6Z, _approvedAbilityPlanUx6Z,
            history.State.Revision, proposal, AssistantPermissionLevel.ApplyApprovedPlan, false));
        CancelAbilityApprovalUx6Z();
        if (!result.Succeeded)
            return new(false, result.Diagnostics.FirstOrDefault()?.Message ?? "Application de la capacité refusée.");
        history.Record(result.Command);
        return new(true, result.Command.Patch?.Summary ?? "Capacité et document runtime appliqués atomiquement.");
    }

    internal StudioUx6ZActionResult UndoAbilityUx6Z()
    {
        CancelAbilityApprovalUx6Z();
        var result = Undo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx6ZActionResult RedoAbilityUx6Z()
    {
        CancelAbilityApprovalUx6Z();
        var result = Redo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx6ZActionResult SaveAbilityUx6Z()
    {
        CancelAbilityApprovalUx6Z();
        var result = Save();
        return new(result.Succeeded, result.Message);
    }

    internal void CancelAbilityApprovalUx6Z()
    {
        _approvedAbilityDraftUx6Z = null;
        _approvedAbilityProposalUx6Z = null;
        _approvedAbilityRevisionUx6Z = null;
        _approvedAbilityProjectFingerprintUx6Z = null;
        _approvedAbilityDraftFingerprintUx6Z = null;
        _approvedAbilityTransactionUx6Z = Guid.Empty;
        _approvedAbilityPlanUx6Z = Guid.Empty;
    }

    internal void CloseUx6Z() => CancelAbilityApprovalUx6Z();

    internal StudioUx6ZApprovalSnapshot CaptureAbilityApprovalUx6Z() => new(
        _approvedAbilityDraftUx6Z, _approvedAbilityProposalUx6Z, _approvedAbilityRevisionUx6Z,
        _approvedAbilityProjectFingerprintUx6Z, _approvedAbilityDraftFingerprintUx6Z,
        _approvedAbilityTransactionUx6Z, _approvedAbilityPlanUx6Z);

    internal void RestoreAbilityApprovalUx6Z(StudioUx6ZApprovalSnapshot snapshot)
    {
        _approvedAbilityDraftUx6Z = snapshot.Draft;
        _approvedAbilityProposalUx6Z = snapshot.Proposal;
        _approvedAbilityRevisionUx6Z = snapshot.Revision;
        _approvedAbilityProjectFingerprintUx6Z = snapshot.ProjectFingerprint;
        _approvedAbilityDraftFingerprintUx6Z = snapshot.DraftFingerprint;
        _approvedAbilityTransactionUx6Z = snapshot.TransactionId;
        _approvedAbilityPlanUx6Z = snapshot.ExecutionPlanId;
    }

    internal string AbilityDraftFingerprintUx6Z(StudioUx6ZDraft draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteAbilityUx6Z(writer, draft.BaseRevision.Value);
        WriteAbilityUx6Z(writer, draft.SourceProjectFingerprint);
        WriteAbilityUx6Z(writer, draft.AbilityId);
        WriteAbilityUx6Z(writer, draft.OwnerBlueprintId);
        WriteAbilityUx6Z(writer, draft.Slug);
        WriteAbilityUx6Z(writer, draft.DisplayName);
        WriteAbilityUx6Z(writer, draft.ResourceCost);
        writer.Write(draft.CooldownSeconds);
        WriteAbilityUx6Z(writer, (int)draft.Targeting);
        WriteAbilityUx6Z(writer, draft.Effects.Count);
        foreach (var effect in draft.Effects)
        {
            WriteAbilityUx6Z(writer, (int)effect.Kind);
            WriteAbilityUx6Z(writer, effect.Magnitude);
        }
        WriteAbilityUx6Z(writer, draft.VfxCueKey);
        WriteAbilityUx6Z(writer, draft.AudioCueKey);
        WriteAbilityUx6Z(writer, draft.ScriptPath);
        WriteAbilityUx6Z(writer, draft.RequiredItemSlug ?? string.Empty);
        WriteAbilityStringsUx6Z(writer, draft.AiTags.Order(StringComparer.Ordinal));
        WriteAbilityUx6Z(writer, draft.BalanceIntent);
        WriteAbilityStringsUx6Z(writer, draft.ContextCitations);
        WriteAbilityStringsUx6Z(writer, draft.StyleRules);
        WriteAbilityUx6Z(writer, draft.AuthorId);
        WriteAbilityUx6Z(writer, draft.Seed);
        writer.Write(draft.Approved);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    internal StudioUx6ZEvidenceProject? CreateAbilityEvidenceProjectUx6Z() =>
        Project is { } project ? BuildAbilityEvidenceProjectUx6Z(project) : null;

    internal StudioUx6ZSmokeResult RunUx6ZContractSmoke()
    {
        var empty = EmptyAbilitySmokeUx6Z();
        var source = Project;
        var sourceRoot = Session?.RootDirectory;
        if (source is null || string.IsNullOrWhiteSpace(sourceRoot)) return empty;
        var evidence = BuildAbilityEvidenceProjectUx6Z(source);
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux6-z-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        StudioUx6ZSmokeResult Execute()
        {
            try
            {
                if (!ProjectSession.Create(root, evidence.Project).Succeeded) return empty;
                var scriptCopy = CopyAbilityEvidenceScriptsUx6Z(sourceRoot, root);
                if (!scriptCopy.Resolved || !editing.Open(root).Succeeded) return empty;
                var owners = editing.AbilityOwnerChoicesUx6Z;
                var explicitSelection = editing.CreateAbilityDraftUx6Z(Guid.Empty) is null && owners.Count > 0;
                var owner = owners.FirstOrDefault(item => item.Id == evidence.OwnerId) ?? owners.FirstOrDefault();
                if (owner is null) return empty;
                var draft = editing.CreateAbilityDraftUx6Z(owner.Id);
                if (draft is null) return empty;
                var configured = ConfigureAbilityEvidenceUx6Z(draft, editing);
                var optionalItem = string.IsNullOrWhiteSpace(configured.RequiredItemSlug) ||
                                   editing.AbilityItemChoicesUx6Z.Any(item => item.Slug == configured.RequiredItemSlug);
                var approvalPreview = editing.PreviewAssistedAbilityUx6Z(configured with { Approved = false });
                var approvalRequired = !approvalPreview.Succeeded &&
                                       approvalPreview.Diagnostics.Any(item =>
                                           item.Code == ApplyAssistedAbilityHandler.ApprovalRequiredCode);
                var permissionPreview = editing.PreviewAssistedAbilityUx6Z(configured,
                    AssistantPermissionLevel.Propose);
                var permissionRequired = !permissionPreview.Succeeded &&
                                         permissionPreview.Diagnostics.Any(item =>
                                             item.Code == ApplyAssistedAbilityHandler.PermissionDeniedCode);
                var invalid = editing.PreviewAssistedAbilityUx6Z(configured with
                {
                    ScriptPath = "scripts/absent.forge",
                    Effects = [],
                });
                var invalidBlocked = !invalid.Succeeded;

                var before = editing.Project!;
                var beforeHistory = editing.History!;
                var preview = editing.PreviewAssistedAbilityUx6Z(configured);
                var dryRun = preview.Succeeded && ReferenceEquals(before, editing.Project) &&
                             ReferenceEquals(beforeHistory, editing.History) &&
                             beforeHistory.State.Revision == configured.BaseRevision;
                var tests = preview.Proposal?.Tests.Count == 5 &&
                            preview.Proposal.Tests.All(item => item.Passed) &&
                            Enum.GetValues<Forge.RpgKit.Domain.Abilities.AbilityTestKind>()
                                .All(kind => preview.Proposal.Tests.Any(item => item.Kind == kind));
                var balance = preview.Proposal?.Balance.Passed == true &&
                              double.IsFinite(preview.Proposal.Balance.PowerScore);

                var currentProject = editing.Project!;
                var currentHistory = editing.History!;
                var neighbor = currentProject.Blueprints.FirstOrDefault(item => item.Id.Value != owner.Id);
                var staleSourceBlocked = false;
                if (neighbor is not null)
                {
                    var neighborResult = editing._abilityNeighborUx6Z.Handle(currentHistory.State,
                        new UpsertBlueprintCommand(CommandId.From(Guid.NewGuid()), currentHistory.State.Revision,
                            neighbor.Id, neighbor.Slug, neighbor.DisplayName + " témoin", neighbor.Kind, false,
                            neighbor.Authoring));
                    if (neighborResult.Succeeded)
                    {
                        currentHistory.Record(neighborResult);
                        staleSourceBlocked = preview.Succeeded &&
                                             !editing.ApplyApprovedAbilityUx6Z(configured).Succeeded;
                        editing.Undo();
                    }
                }

                draft = editing.CreateAbilityDraftUx6Z(owner.Id);
                if (draft is null) return empty;
                configured = ConfigureAbilityEvidenceUx6Z(draft, editing);
                preview = editing.PreviewAssistedAbilityUx6Z(configured);
                var staleApprovalBlocked = preview.Succeeded &&
                                           !editing.ApplyApprovedAbilityUx6Z(configured with
                                           {
                                               BalanceIntent = configured.BalanceIntent + " altérée",
                                           }).Succeeded;

                draft = editing.CreateAbilityDraftUx6Z(owner.Id);
                if (draft is null) return empty;
                configured = ConfigureAbilityEvidenceUx6Z(draft, editing);
                var sourceProject = editing.Project!;
                var sourceOwner = sourceProject.Blueprints.Single(item => item.Id.Value == owner.Id);
                var sourceOtherBlueprints = sourceProject.Blueprints.Where(item => item.Id.Value != owner.Id).ToArray();
                var sourceDocuments = sourceProject.Module.RuntimePolicies.Extensions.JsonDocuments.ToArray();
                var finalPreview = editing.PreviewAssistedAbilityUx6Z(configured);
                var applied = finalPreview.Succeeded
                    ? editing.ApplyApprovedAbilityUx6Z(configured)
                    : new StudioUx6ZActionResult(false, "aperçu refusé");
                var appliedProject = editing.Project;
                var appliedOwner = appliedProject?.Blueprints.SingleOrDefault(item => item.Id.Value == owner.Id);
                var proposal = finalPreview.Proposal;
                var canonical = applied.Succeeded && proposal is not null && appliedOwner?.Combat is not null &&
                                appliedOwner.Combat.Abilities.Any(item => item.Id.Value == configured.AbilityId) &&
                                editing.History?.State.Revision == configured.BaseRevision.Next();
                var runtimeDocument = proposal is not null && appliedProject?.Module.RuntimePolicies.Extensions
                    .JsonDocuments.Any(item => item.Id == proposal.RuntimeDocument.Id &&
                                               item.Key == proposal.RuntimeDocument.Key) == true;
                var ownerNeighbors = canonical && appliedOwner is not null &&
                                     appliedOwner.Combat!.Stats == sourceOwner.Combat!.Stats &&
                                     appliedOwner.Combat.Loot == sourceOwner.Combat.Loot &&
                                     sourceOwner.Combat.Abilities.All(item => appliedOwner.Combat.Abilities.Contains(item)) &&
                                     appliedOwner.PaletteOrigin == sourceOwner.PaletteOrigin &&
                                     appliedOwner.Provenance == sourceOwner.Provenance &&
                                     appliedOwner.Authoring == sourceOwner.Authoring &&
                                     sourceOtherBlueprints.SequenceEqual(appliedProject!.Blueprints
                                         .Where(item => item.Id.Value != owner.Id));
                var extensions = appliedProject?.Module.RuntimePolicies.Extensions;
                var sourceExtensions = sourceProject.Module.RuntimePolicies.Extensions;
                var moduleNeighbors = extensions is not null &&
                                      sourceDocuments.SequenceEqual(extensions.JsonDocuments
                                          .Where(item => proposal is null || item.Id != proposal.RuntimeDocument.Id)) &&
                                      sourceExtensions.RuleSchemas.SequenceEqual(extensions.RuleSchemas) &&
                                      sourceExtensions.UiDocuments.SequenceEqual(extensions.UiDocuments) &&
                                      sourceExtensions.SnapshotPolicy == extensions.SnapshotPolicy &&
                                      sourceExtensions.ModernTooling == extensions.ModernTooling &&
                                      sourceExtensions.OperationalReadiness == extensions.OperationalReadiness &&
                                      sourceExtensions.PostLaunch == extensions.PostLaunch;
                var projectNeighbors = appliedProject is not null && appliedProject.Id == sourceProject.Id &&
                                       appliedProject.Title == sourceProject.Title &&
                                       appliedProject.Areas.SequenceEqual(sourceProject.Areas) &&
                                       appliedProject.Dialogues.SequenceEqual(sourceProject.Dialogues) &&
                                       appliedProject.Quests.SequenceEqual(sourceProject.Quests);
                var appliedJson = appliedProject is null ? string.Empty : ProjectJsonSerializer.Serialize(appliedProject);
                var undo = editing.UndoAbilityUx6Z();
                var undone = undo.Succeeded && ProjectJsonSerializer.Serialize(editing.Project!) ==
                             ProjectJsonSerializer.Serialize(sourceProject);
                var redo = editing.RedoAbilityUx6Z();
                var redone = redo.Succeeded && ProjectJsonSerializer.Serialize(editing.Project!) == appliedJson;
                var saved = editing.SaveAbilityUx6Z();
                editing.Close();
                var persistence = saved.Succeeded && reopened.Open(root).Succeeded &&
                                  ProjectJsonSerializer.Serialize(reopened.Project!) == appliedJson;
                reopened.Close();
                var human = configured.DisplayName.Contains("braise", StringComparison.OrdinalIgnoreCase) &&
                            !configured.DisplayName.Contains(configured.AbilityId.ToString("D"),
                                StringComparison.OrdinalIgnoreCase);
                var bounded = finalPreview.RuntimeClaimsBounded && finalPreview.Proposal?.ProviderKey == "offline-studio" &&
                              finalPreview.Proposal.EstimatedCostUsd == 0;
                var origin = evidence.UsesRealModule ^ evidence.UsesCanonicalFallback;
                var succeeded = explicitSelection && origin && scriptCopy.Resolved && optionalItem && invalidBlocked &&
                                approvalRequired && permissionRequired && tests && balance && dryRun &&
                                staleSourceBlocked && staleApprovalBlocked && canonical && runtimeDocument &&
                                undone && redone && persistence && ownerNeighbors && moduleNeighbors &&
                                projectNeighbors && human && bounded;
                return new(succeeded, explicitSelection, evidence.UsesRealModule,
                    evidence.UsesCanonicalFallback, scriptCopy.Resolved, optionalItem, invalidBlocked,
                    approvalRequired, permissionRequired, tests, balance, dryRun, staleSourceBlocked,
                    staleApprovalBlocked, canonical, runtimeDocument, undone && redone, persistence,
                    ownerNeighbors, moduleNeighbors, projectNeighbors, human, bounded, false);
            }
            catch
            {
                return empty;
            }
            finally
            {
                editing.Close();
                reopened.Close();
            }
        }

        var result = Execute();
        var released = DeleteTemporaryAbilityUx6Z(root);
        return result with { TemporaryWorkspaceReleased = released, Succeeded = result.Succeeded && released };
    }

    internal static bool CopyAbilityScriptsUx6Z(string sourceRoot, string destinationRoot)
    {
        var sourceScripts = Path.Combine(sourceRoot, "scripts");
        if (!Directory.Exists(sourceScripts)) return false;
        var files = Directory.GetFiles(sourceScripts, "*.forge", SearchOption.AllDirectories);
        if (files.Length == 0) return false;
        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(sourceRoot, file);
            var destination = Path.Combine(destinationRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
        return true;
    }

    internal static bool DeleteTemporaryAbilityUx6Z(string root)
    {
        try
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            return !Directory.Exists(root);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal static StudioUx6ZDraft ChangedAbilityDraftUx6Z(StudioUx6ZDraft draft) => draft with
    {
        DisplayName = "Élan de braise témoin",
        Slug = "elan-de-braise-temoin",
        ResourceCost = Math.Min(20, Math.Max(1, draft.ResourceCost)),
        CooldownSeconds = 7.5,
        Targeting = AbilityTargeting.SingleEnemy,
        Effects =
        [
            new StudioUx6ZEffectDraft(AbilityEffectKind.Damage, 24),
            new StudioUx6ZEffectDraft(AbilityEffectKind.Damage, 6),
        ],
        VfxCueKey = "vfx.elan-de-braise-temoin",
        AudioCueKey = "audio.elan-de-braise-temoin",
        AiTags = new HashSet<string>(["offensive", "finisher", "telegraphiee"], StringComparer.Ordinal),
        BalanceIntent = "Dégâts concentrés, télégaphiés et limités par le coût et la recharge.",
        ContextCitations = ["module:combat#porteur", "module:style#lisibilite"],
        StyleRules = ["combat télégraphié", "lecture claire"],
        AuthorId = "studio-author",
        Seed = 20260821,
        Approved = true,
    };

    private static AssistedAbilityBrief BuildAbilityBriefUx6Z(StudioUx6ZDraft draft) => new(
        draft.AbilityId, draft.OwnerBlueprintId, draft.Slug.Trim(), draft.DisplayName.Trim(),
        draft.ResourceCost, draft.CooldownSeconds, draft.Targeting,
        draft.Effects.Select(item => new AbilityEffect(item.Kind, item.Magnitude)).ToArray(),
        draft.VfxCueKey.Trim(), draft.AudioCueKey.Trim(), draft.ScriptPath.Trim(),
        string.IsNullOrWhiteSpace(draft.RequiredItemSlug) ? null : draft.RequiredItemSlug.Trim(),
        draft.AiTags.Select(item => item.Trim()).Where(item => item.Length > 0).ToHashSet(StringComparer.Ordinal),
        draft.BalanceIntent.Trim(), draft.ContextCitations.Select(item => item.Trim()).ToArray(),
        draft.StyleRules.Select(item => item.Trim()).ToArray(), draft.AuthorId.Trim(), draft.Seed);

    private static StudioUx6ZDraft FreezeAbilityDraftUx6Z(StudioUx6ZDraft draft) => draft with
    {
        Effects = draft.Effects.ToArray(),
        AiTags = draft.AiTags.ToHashSet(StringComparer.Ordinal),
        ContextCitations = draft.ContextCitations.ToArray(),
        StyleRules = draft.StyleRules.ToArray(),
    };

    private static StudioUx6ZPreview FailedAbilityUx6Z(string message, string code) => new(false, false,
        false, code != ApplyAssistedAbilityHandler.PermissionDeniedCode, true,
        HumanAbilityMessageUx6Z(code, message), string.Empty, string.Empty, [],
        [new StudioUx6ZDiagnostic(code, "ability/preview", HumanAbilityMessageUx6Z(code, message))], null);

    private static string HumanAbilityMessageUx6Z(string code, string fallback) => code switch
    {
        ApplyAssistedAbilityHandler.ApprovalRequiredCode =>
            "Cochez l’approbation auteur avant de préparer le dry-run.",
        ApplyAssistedAbilityHandler.PermissionDeniedCode =>
            "La permission d’appliquer un plan approuvé est requise.",
        ApplyAssistedAbilityHandler.RevisionConflictCode =>
            "Le projet a changé ; reconstruisez la proposition.",
        ApplyAssistedAbilityHandler.PlanNotPreviewedCode =>
            "Le plan exact doit réussir son dry-run avant application.",
        ApplyAssistedAbilityHandler.ProposalInvalidCode =>
            "La capacité, ses dépendances, ses tests ou son équilibrage sont invalides.",
        "RPG_ABILITY_SCRIPT_MISSING" => "Le ForgeScript sélectionné n’existe plus dans le workspace.",
        "RPG_ABILITY_ITEM_MISSING" => "L’objet requis n’existe plus dans le catalogue du projet.",
        "RPG_ABILITY_COST_EXCEEDS_POOL" => "Le coût dépasse la réserve maximale du porteur.",
        "RPG_ABILITY_TEST_FAILED" => "Un scénario du résolveur de combat a échoué.",
        "RPG_ABILITY_BALANCE_FAILED" => "La note d’équilibrage sort des limites canoniques.",
        _ => fallback,
    };

    private static string OwnerLabelUx6Z(ProjectDefinition project, Guid ownerId) =>
        project.Blueprints.SingleOrDefault(item => item.Id.Value == ownerId)?.DisplayName ?? "porteur sélectionné";

    private static string HumanBlueprintKindUx6Z(BlueprintKind kind) => kind switch
    {
        BlueprintKind.Creature => "créature",
        BlueprintKind.PlayerSpawn => "joueur",
        _ => "profil",
    };

    private static string HumanTargetingUx6Z(AbilityTargeting targeting) => targeting switch
    {
        AbilityTargeting.Self => "soi-même",
        AbilityTargeting.SingleAlly => "allié unique",
        AbilityTargeting.SingleEnemy => "ennemi unique",
        AbilityTargeting.PointArea => "zone au sol",
        _ => targeting.ToString(),
    };

    private static string NextAbilitySlugUx6Z(BlueprintDefinition owner)
    {
        const string root = "elan-de-braise";
        var slug = root;
        var index = 2;
        while (owner.Combat!.Abilities.Any(item => item.Slug == slug)) slug = $"{root}-{index++}";
        return slug;
    }

    private static string ProjectFingerprintUx6Z(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))))
            .ToLowerInvariant();

    private static Guid StableAbilityGuidUx6Z(Guid root, string discriminator)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{root:D}:{discriminator}"));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes);
    }

    private static void WriteAbilityUx6Z(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static void WriteAbilityUx6Z(BinaryWriter writer, Guid value) => writer.Write(value.ToByteArray());
    private static void WriteAbilityUx6Z(BinaryWriter writer, int value) => writer.Write(value);
    private static void WriteAbilityUx6Z(BinaryWriter writer, long value) => writer.Write(value);

    private static void WriteAbilityStringsUx6Z(BinaryWriter writer, IEnumerable<string> values)
    {
        var array = values.ToArray();
        writer.Write(array.Length);
        foreach (var value in array) WriteAbilityUx6Z(writer, value);
    }

    private static StudioUx6ZEvidenceProject BuildAbilityEvidenceProjectUx6Z(ProjectDefinition source)
    {
        var blueprints = source.Blueprints.ToList();
        var owner = blueprints.FirstOrDefault(item => item.Kind is (BlueprintKind.Creature or BlueprintKind.PlayerSpawn) &&
                                                       item.Combat is not null && !item.IsReadOnly());
        var fallback = owner is null;
        if (owner is null)
        {
            owner = new BlueprintDefinition(
                BlueprintId.From(StableAbilityGuidUx6Z(source.Id.Value, "ux6-z:owner")),
                "porteur-capacite-temoin", "Porteuse de capacité témoin", BlueprintKind.Creature,
                new CombatProfile(new CombatStats(160, 100, 14, 6, "heroes", "heroes")));
            blueprints.Add(owner);
        }
        if (blueprints.All(item => item.Kind != BlueprintKind.Item))
        {
            fallback = true;
            blueprints.Add(new BlueprintDefinition(
                BlueprintId.From(StableAbilityGuidUx6Z(source.Id.Value, "ux6-z:item")),
                "focus-capacite-temoin", "Focus de capacité témoin", BlueprintKind.Item));
        }
        var project = fallback
            ? new ProjectDefinition(source.Id, source.Title, source.Module, blueprints, source.Areas,
                source.Dialogues, source.Quests)
            : source;
        return new(project, owner.Id.Value, !fallback, fallback);
    }

    internal static (bool Resolved, bool Real) CopyAbilityEvidenceScriptsUx6Z(string sourceRoot, string root)
    {
        try
        {
            if (CopyAbilityScriptsUx6Z(sourceRoot, root)) return (true, true);
            var directory = Path.Combine(root, "scripts");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "ability_evidence.forge"),
                "void main()\n{\n    say(\"Capacité témoin.\");\n}\n");
            return (true, false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (false, false);
        }
    }

    private static StudioUx6ZDraft ConfigureAbilityEvidenceUx6Z(
        StudioUx6ZDraft draft,
        StudioSessionController editing)
    {
        var script = editing.AbilityScriptChoicesUx6Z.First();
        var item = editing.AbilityItemChoicesUx6Z.FirstOrDefault();
        return ChangedAbilityDraftUx6Z(draft) with
        {
            ScriptPath = script.RelativePath,
            RequiredItemSlug = item?.Slug,
        };
    }

    private static StudioUx6ZSmokeResult EmptyAbilitySmokeUx6Z() => new(false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false);

}
