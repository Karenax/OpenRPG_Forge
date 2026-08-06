using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Quests;
using Forge.Application.Time;
using Forge.Domain.Assistant;

namespace Forge.RpgKit.Application.Quests;

public sealed record ApplyAssistedQuestCommand(CommandId CommandId, Guid TransactionId, Guid ExecutionPlanId,
    ProjectRevision BaseRevision, AssistedQuestProposal Proposal, AssistantPermissionLevel Permission, bool DryRun);
public sealed record AssistedQuestApplyResult(bool Succeeded, CommandResult Command, Guid TransactionId,
    Guid ExecutionPlanId, string SemanticDiff, IReadOnlyList<CommandDiagnostic> Diagnostics);

public sealed class ApplyAssistedQuestHandler
{
    public const string RevisionConflictCode = "assistant.quest.revision_conflict";
    public const string ApprovalRequiredCode = "assistant.quest.approval_required";
    public const string PermissionDeniedCode = "assistant.quest.permission_denied";
    public const string ProposalInvalidCode = "assistant.quest.proposal_invalid";
    public const string PlanNotPreviewedCode = "assistant.quest.plan_not_previewed";
    private readonly IClock _clock;
    private readonly SetQuestHandler _setQuest;
    private readonly Dictionary<Guid, (ProjectRevision Revision, Guid TransactionId, string Fingerprint)> _previews = [];

    public ApplyAssistedQuestHandler(IClock clock) { _clock = clock; _setQuest = new(clock); }

    public AssistedQuestApplyResult Handle(ProjectState state, ApplyAssistedQuestCommand command)
    {
        if (command.BaseRevision != state.Revision) return Failure(state, command, RevisionConflictCode, "Project changed before assisted quest application.");
        if (command.TransactionId == Guid.Empty || command.ExecutionPlanId == Guid.Empty) return Failure(state, command, PlanNotPreviewedCode, "Transaction and preview plan IDs are required.");
        if (!command.Proposal.ApprovedForApplication) return Failure(state, command, ApprovalRequiredCode, "Assisted quest requires explicit author approval.");
        if (command.Permission < AssistantPermissionLevel.ApplyApprovedPlan) return Failure(state, command, PermissionDeniedCode, "ApplyApprovedPlan permission is required.");
        if (!command.Proposal.CanApply) return Failure(state, command, ProposalInvalidCode, "Quest lint, dependencies, style and scenarios must pass.");
        var result = _setQuest.Handle(state, new SetQuestCommand(command.CommandId, state.Revision, command.Proposal.Quest, command.DryRun));
        if (!result.Succeeded) return Failure(state, command, result.Diagnostics[0].Code, result.Diagnostics[0].Message);
        var fingerprint = Fingerprint(command.Proposal);
        if (command.DryRun)
        {
            lock (_previews) { if (_previews.Count >= 128) _previews.Clear(); _previews[command.ExecutionPlanId] = (command.BaseRevision, command.TransactionId, fingerprint); }
            return new(true, result, command.TransactionId, command.ExecutionPlanId, command.Proposal.SemanticDiff, []);
        }
        lock (_previews)
        {
            if (!_previews.TryGetValue(command.ExecutionPlanId, out var preview) || preview.Revision != command.BaseRevision || preview.TransactionId != command.TransactionId || preview.Fingerprint != fingerprint)
                return Failure(state, command, PlanNotPreviewedCode, "Assisted quest must match its successful dry-run transaction.");
            _previews.Remove(command.ExecutionPlanId);
        }
        return new(true, result, command.TransactionId, command.ExecutionPlanId,
            command.Proposal.SemanticDiff.Replace("add-quest", "apply-quest", StringComparison.Ordinal), []);
    }

    private AssistedQuestApplyResult Failure(ProjectState state, ApplyAssistedQuestCommand command, string code, string message)
    {
        var diagnostic = new CommandDiagnostic(code, message);
        return new(false, new CommandResult(false, state, command.BaseRevision, null, [diagnostic], null, null,
            new AuditEntry(command.CommandId, "apply_assisted_quest", _clock.UtcNow, false)), command.TransactionId,
            command.ExecutionPlanId, "no-change", [diagnostic]);
    }

    private static string Fingerprint(AssistedQuestProposal proposal)
    {
        var value = $"{proposal.Quest.Id}|{proposal.PromptSha256}|{proposal.Provenance}|" +
                    string.Join('|', proposal.Quest.States.OrderBy(item => item.Id.Value).Select(item => $"{item.Id}:{item.DisplayName}:{item.IsTerminal}")) + "|" +
                    string.Join('|', proposal.Quest.Transitions.OrderBy(item => item.Id.Value).Select(item => $"{item.Id}:{item.FromStateId}:{item.ToStateId}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}
