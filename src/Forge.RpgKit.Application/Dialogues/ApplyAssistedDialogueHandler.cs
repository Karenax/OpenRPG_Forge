using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Dialogues;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Assistant;

namespace Forge.RpgKit.Application.Dialogues;

public sealed record ApplyAssistedDialogueCommand(
    CommandId CommandId,
    Guid TransactionId,
    Guid ExecutionPlanId,
    ProjectRevision BaseRevision,
    AssistedDialogueProposal Proposal,
    AssistantPermissionLevel Permission,
    bool DryRun);

public sealed record AssistedDialogueApplyResult(
    bool Succeeded,
    CommandResult Command,
    Guid TransactionId,
    Guid ExecutionPlanId,
    string SemanticDiff,
    IReadOnlyList<CommandDiagnostic> Diagnostics);

public sealed class ApplyAssistedDialogueHandler
{
    public const string RevisionConflictCode = "assistant.dialogue.revision_conflict";
    public const string TransactionRequiredCode = "assistant.dialogue.transaction_required";
    public const string PlanRequiredCode = "assistant.dialogue.plan_required";
    public const string ApprovalRequiredCode = "assistant.dialogue.approval_required";
    public const string PermissionDeniedCode = "assistant.dialogue.permission_denied";
    public const string ProposalInvalidCode = "assistant.dialogue.proposal_invalid";
    public const string PlanNotPreviewedCode = "assistant.dialogue.plan_not_previewed";
    private const string CommandName = "apply_assisted_dialogue";
    private readonly IClock _clock;
    private readonly SetDialogueHandler _setDialogue;
    private readonly Dictionary<Guid, (ProjectRevision Revision, Guid TransactionId, string Fingerprint)> _previews = [];

    public ApplyAssistedDialogueHandler(IClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _setDialogue = new SetDialogueHandler(clock);
    }

    public AssistedDialogueApplyResult Handle(ProjectState state, ApplyAssistedDialogueCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Proposal);
        if (command.BaseRevision != state.Revision)
            return Failure(state, command, RevisionConflictCode, "Project changed before assisted dialogue application.");
        if (command.TransactionId == Guid.Empty)
            return Failure(state, command, TransactionRequiredCode, "Assisted dialogue requires a transaction ID.");
        if (command.ExecutionPlanId == Guid.Empty)
            return Failure(state, command, PlanRequiredCode, "Assisted dialogue requires a preview plan ID.");
        if (!command.Proposal.ApprovedForApplication)
            return Failure(state, command, ApprovalRequiredCode, "Assisted dialogue requires explicit author approval.");
        if (command.Permission < AssistantPermissionLevel.ApplyApprovedPlan)
            return Failure(state, command, PermissionDeniedCode, "Assisted dialogue requires ApplyApprovedPlan permission.");
        if (!command.Proposal.CanApply || command.Proposal.Citations.Count == 0 ||
            command.Proposal.LocalizationPlaceholders.Count != command.Proposal.Dialogue.Nodes.Count)
            return Failure(state, command, ProposalInvalidCode,
                "Assisted dialogue proposal must pass lint, style, citation and localization validation.");

        var coreCommand = new SetDialogueCommand(
            command.CommandId,
            state.Revision,
            command.Proposal.Dialogue,
            command.DryRun);
        var result = _setDialogue.Handle(state, coreCommand);
        if (!result.Succeeded)
            return Failure(state, command, result.Diagnostics[0].Code, result.Diagnostics[0].Message);
        var fingerprint = Fingerprint(command.Proposal);
        if (command.DryRun)
        {
            lock (_previews)
            {
                if (_previews.Count >= 128) _previews.Clear();
                _previews[command.ExecutionPlanId] = (command.BaseRevision, command.TransactionId, fingerprint);
            }
            return new(true, result, command.TransactionId, command.ExecutionPlanId,
                command.Proposal.SemanticDiff, []);
        }
        lock (_previews)
        {
            if (!_previews.TryGetValue(command.ExecutionPlanId, out var preview) ||
                preview.Revision != command.BaseRevision || preview.TransactionId != command.TransactionId ||
                !string.Equals(preview.Fingerprint, fingerprint, StringComparison.Ordinal))
                return Failure(state, command, PlanNotPreviewedCode,
                    "Assisted dialogue application must match its successful dry-run transaction.");
            _previews.Remove(command.ExecutionPlanId);
        }
        return new(true, result, command.TransactionId, command.ExecutionPlanId,
            command.Proposal.SemanticDiff.Replace("add-dialogue", "apply-dialogue", StringComparison.Ordinal), []);
    }

    private AssistedDialogueApplyResult Failure(
        ProjectState state,
        ApplyAssistedDialogueCommand command,
        string code,
        string message)
    {
        var diagnostic = new CommandDiagnostic(code, message);
        var core = new CommandResult(false, state, command.BaseRevision, null, [diagnostic], null, null,
            new AuditEntry(command.CommandId, CommandName, _clock.UtcNow, false));
        return new(false, core, command.TransactionId, command.ExecutionPlanId, "no-change", [diagnostic]);
    }

    private static string Fingerprint(AssistedDialogueProposal proposal)
    {
        var builder = new StringBuilder()
            .Append(proposal.Dialogue.Id).Append('|')
            .Append(proposal.PromptSha256).Append('|')
            .Append(proposal.Provenance).Append('|');
        foreach (var node in proposal.Dialogue.Nodes.OrderBy(item => item.Id.Value))
        {
            builder.Append(node.Id).Append(':').Append(node.SpeakerBlueprintId).Append(':')
                .Append(node.Text.Key).Append(':').Append(node.Text.Fallback).Append('|');
            foreach (var choice in node.Choices.OrderBy(item => item.Id.Value))
                builder.Append(choice.Id).Append(':').Append(choice.TargetNodeId).Append(':')
                    .Append(choice.Text.Key).Append(':').Append(choice.Text.Fallback).Append('|');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))
            .ToLowerInvariant();
    }
}
