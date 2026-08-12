using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Assistant;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Validation;

namespace Forge.RpgKit.Application.Bosses;

public sealed record ApplyAssistedBossCommand(
    CommandId CommandId,
    Guid TransactionId,
    Guid ExecutionPlanId,
    ProjectRevision BaseRevision,
    AssistedBossProposal Proposal,
    AssistantPermissionLevel Permission,
    bool DryRun);

public sealed record AssistedBossApplyResult(
    bool Succeeded,
    CommandResult Command,
    Guid TransactionId,
    Guid ExecutionPlanId,
    string SemanticDiff,
    IReadOnlyList<CommandDiagnostic> Diagnostics);

public sealed class ApplyAssistedBossHandler
{
    public const string RevisionConflictCode = "assistant.boss.revision_conflict";
    public const string ApprovalRequiredCode = "assistant.boss.approval_required";
    public const string PermissionDeniedCode = "assistant.boss.permission_denied";
    public const string ProposalInvalidCode = "assistant.boss.proposal_invalid";
    public const string PlanNotPreviewedCode = "assistant.boss.plan_not_previewed";
    private readonly IClock _clock;
    private readonly Dictionary<Guid, (ProjectRevision Revision, Guid TransactionId, string Fingerprint)> _previews = [];

    public ApplyAssistedBossHandler(IClock clock) =>
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public AssistedBossApplyResult Handle(ProjectState state, ApplyAssistedBossCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
            return Failure(state, command, RevisionConflictCode, "Project changed before assisted boss application.");
        if (command.TransactionId == Guid.Empty || command.ExecutionPlanId == Guid.Empty)
            return Failure(state, command, PlanNotPreviewedCode, "Transaction and preview plan IDs are required.");
        if (!command.Proposal.ApprovedForApplication)
            return Failure(state, command, ApprovalRequiredCode, "Assisted boss requires explicit author approval.");
        if (command.Permission < AssistantPermissionLevel.ApplyApprovedPlan)
            return Failure(state, command, PermissionDeniedCode, "ApplyApprovedPlan permission is required.");
        if (!command.Proposal.CanApply)
            return Failure(state, command, ProposalInvalidCode, "Boss dependencies, validation, style and balance simulation must pass.");

        ProjectDefinition candidate;
        try
        {
            candidate = BuildCandidate(state.Project, command.Proposal);
        }
        catch (ArgumentException exception)
        {
            return Failure(state, command, ProposalInvalidCode, exception.Message);
        }
        var projectDiagnostic = ProjectValidator.Validate(candidate).FirstOrDefault();
        if (projectDiagnostic is not null)
            return Failure(state, command, projectDiagnostic.Code, projectDiagnostic.Message);

        var existingBlueprint = state.Project.Blueprints.Any(item => item.Id == command.Proposal.Boss.Blueprint.Id);
        var patch = new ProjectPatch(existingBlueprint ? "replace" : "add", $"bosses/{command.Proposal.Boss.Id:D}",
            $"Apply atomic boss package '{command.Proposal.Boss.DisplayName}': blueprint, creature combat, encounter, phases, dialogue, rewards and runtime document.");
        var inverse = new ProjectPatch(existingBlueprint ? "replace" : "remove", $"bosses/{command.Proposal.Boss.Id:D}",
            existingBlueprint ? "Restore previous canonical boss package." : "Remove assisted boss package.");
        var fingerprint = Fingerprint(command.Proposal);
        if (command.DryRun)
        {
            lock (_previews)
            {
                if (_previews.Count >= 128) _previews.Clear();
                _previews[command.ExecutionPlanId] = (command.BaseRevision, command.TransactionId, fingerprint);
            }
            var preview = new CommandResult(true, state, command.BaseRevision, null, [], patch, inverse,
                new AuditEntry(command.CommandId, "apply_assisted_boss", _clock.UtcNow, false));
            return new(true, preview, command.TransactionId, command.ExecutionPlanId, command.Proposal.SemanticDiff, []);
        }
        lock (_previews)
        {
            if (!_previews.TryGetValue(command.ExecutionPlanId, out var preview) ||
                preview.Revision != command.BaseRevision || preview.TransactionId != command.TransactionId ||
                preview.Fingerprint != fingerprint)
                return Failure(state, command, PlanNotPreviewedCode,
                    "Assisted boss must exactly match its successful dry-run transaction.");
            _previews.Remove(command.ExecutionPlanId);
        }
        var revision = state.Revision.Next();
        var applied = new CommandResult(true, new ProjectState(candidate, revision), command.BaseRevision, revision, [],
            patch, inverse, new AuditEntry(command.CommandId, "apply_assisted_boss", _clock.UtcNow, true));
        return new(true, applied, command.TransactionId, command.ExecutionPlanId,
            command.Proposal.SemanticDiff.Replace("batch:add-or-replace", "batch:applied", StringComparison.Ordinal), []);
    }

    private static ProjectDefinition BuildCandidate(ProjectDefinition project, AssistedBossProposal proposal)
    {
        var blueprint = proposal.Boss.Blueprint;
        var blueprints = project.Blueprints.Any(item => item.Id == blueprint.Id)
            ? project.Blueprints.Select(item => item.Id == blueprint.Id ? blueprint : item).ToArray()
            : [.. project.Blueprints, blueprint];
        var dialogue = proposal.Boss.IntroductionDialogue;
        var dialogues = project.Dialogues.Any(item => item.Id == dialogue.Id)
            ? project.Dialogues.Select(item => item.Id == dialogue.Id ? dialogue : item).ToArray()
            : [.. project.Dialogues, dialogue];
        var currentPolicies = project.Module.RuntimePolicies;
        var currentExtensions = currentPolicies.Extensions;
        var jsonDocuments = currentExtensions.JsonDocuments
            .Where(item => item.Id != proposal.RuntimeDocument.Id && item.Key != proposal.RuntimeDocument.Key)
            .Append(proposal.RuntimeDocument).ToArray();
        var extensions = new ModuleRuntimeExtensionProfile(currentExtensions.Id, currentExtensions.SchemaVersion,
            currentExtensions.RuleSchemas, currentExtensions.UiDocuments, jsonDocuments,
            currentExtensions.SnapshotPolicy, "assistant:boss", currentExtensions.ModernTooling,
            currentExtensions.OperationalReadiness, currentExtensions.PostLaunch);
        var policies = new ModuleRuntimePolicyProfile(currentPolicies.Id, currentPolicies.SchemaVersion,
            currentPolicies.Heartbeats, currentPolicies.Rest, currentPolicies.SequenceAbort, "assistant:boss",
            currentPolicies.Cinematics, currentPolicies.Gameplay, currentPolicies.Rules, extensions,
            currentPolicies.GameplayEvents, currentPolicies.World);
        return new ProjectDefinition(project.Id, project.Title, project.Module.WithRuntimePolicies(policies),
            blueprints, project.Areas, dialogues, project.Quests);
    }

    private AssistedBossApplyResult Failure(ProjectState state, ApplyAssistedBossCommand command,
        string code, string message)
    {
        var diagnostic = new CommandDiagnostic(code, message);
        var result = new CommandResult(false, state, command.BaseRevision, null, [diagnostic], null, null,
            new AuditEntry(command.CommandId, "apply_assisted_boss", _clock.UtcNow, false));
        return new(false, result, command.TransactionId, command.ExecutionPlanId, "no-change", [diagnostic]);
    }

    private static string Fingerprint(AssistedBossProposal proposal)
    {
        var value = $"{proposal.Boss.Id:D}|{proposal.Boss.Blueprint.Id}|{proposal.Boss.IntroductionDialogue.Id}|" +
                    $"{proposal.RuntimeDocument.Id:D}|{proposal.RuntimeDocument.InitialValue}|{proposal.PromptSha256}|" +
                    $"{proposal.Provenance}|{proposal.SemanticDiff}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}
