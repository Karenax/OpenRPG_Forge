using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Validation;

namespace Forge.RpgKit.Application.Abilities;

public sealed record ApplyAssistedAbilityCommand(
    CommandId CommandId,
    Guid TransactionId,
    Guid ExecutionPlanId,
    ProjectRevision BaseRevision,
    AssistedAbilityProposal Proposal,
    AssistantPermissionLevel Permission,
    bool DryRun);

public sealed record AssistedAbilityApplyResult(
    bool Succeeded,
    CommandResult Command,
    Guid TransactionId,
    Guid ExecutionPlanId,
    string SemanticDiff,
    IReadOnlyList<CommandDiagnostic> Diagnostics);

public sealed class ApplyAssistedAbilityHandler
{
    public const string RevisionConflictCode = "assistant.ability.revision_conflict";
    public const string ApprovalRequiredCode = "assistant.ability.approval_required";
    public const string PermissionDeniedCode = "assistant.ability.permission_denied";
    public const string ProposalInvalidCode = "assistant.ability.proposal_invalid";
    public const string PlanNotPreviewedCode = "assistant.ability.plan_not_previewed";
    private readonly IClock _clock;
    private readonly Dictionary<Guid, (ProjectRevision Revision, Guid TransactionId, string Fingerprint)> _previews = [];

    public ApplyAssistedAbilityHandler(IClock clock) =>
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public AssistedAbilityApplyResult Handle(ProjectState state, ApplyAssistedAbilityCommand command)
    {
        if (command.BaseRevision != state.Revision)
            return Failure(state, command, RevisionConflictCode, "Project changed before assisted ability application.");
        if (command.TransactionId == Guid.Empty || command.ExecutionPlanId == Guid.Empty)
            return Failure(state, command, PlanNotPreviewedCode, "Transaction and preview plan IDs are required.");
        if (!command.Proposal.ApprovedForApplication)
            return Failure(state, command, ApprovalRequiredCode, "Assisted ability requires explicit author approval.");
        if (command.Permission < AssistantPermissionLevel.ApplyApprovedPlan)
            return Failure(state, command, PermissionDeniedCode, "ApplyApprovedPlan permission is required.");
        if (!command.Proposal.CanApply)
            return Failure(state, command, ProposalInvalidCode, "Ability dependencies, tests, style and balance must pass.");
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
        var patch = new ProjectPatch("replace", $"blueprints/{command.Proposal.Orchestration.OwnerBlueprintId}/combat/abilities/{command.Proposal.Orchestration.Id:D}",
            $"Apply canonical ability '{command.Proposal.Orchestration.Ability.DisplayName}' and its runtime orchestration document.");
        var inverse = new ProjectPatch("replace", $"blueprints/{command.Proposal.Orchestration.OwnerBlueprintId}/combat",
            "Restore the previous combat profile and runtime ability document.");
        var fingerprint = Fingerprint(command.Proposal);
        if (command.DryRun)
        {
            lock (_previews)
            {
                if (_previews.Count >= 128) _previews.Clear();
                _previews[command.ExecutionPlanId] = (command.BaseRevision, command.TransactionId, fingerprint);
            }
            var preview = new CommandResult(true, state, command.BaseRevision, null, [], patch, inverse,
                new AuditEntry(command.CommandId, "apply_assisted_ability", _clock.UtcNow, false));
            return new(true, preview, command.TransactionId, command.ExecutionPlanId, command.Proposal.SemanticDiff, []);
        }
        lock (_previews)
        {
            if (!_previews.TryGetValue(command.ExecutionPlanId, out var preview) ||
                preview.Revision != command.BaseRevision || preview.TransactionId != command.TransactionId ||
                preview.Fingerprint != fingerprint)
                return Failure(state, command, PlanNotPreviewedCode,
                    "Assisted ability must exactly match its successful dry-run transaction.");
            _previews.Remove(command.ExecutionPlanId);
        }
        var revision = state.Revision.Next();
        var applied = new CommandResult(true, new ProjectState(candidate, revision), command.BaseRevision, revision, [],
            patch, inverse, new AuditEntry(command.CommandId, "apply_assisted_ability", _clock.UtcNow, true));
        return new(true, applied, command.TransactionId, command.ExecutionPlanId,
            command.Proposal.SemanticDiff.Replace("batch:set", "batch:applied", StringComparison.Ordinal), []);
    }

    private static ProjectDefinition BuildCandidate(ProjectDefinition project, AssistedAbilityProposal proposal)
    {
        var ownerId = proposal.Orchestration.OwnerBlueprintId;
        var owner = project.Blueprints.SingleOrDefault(item => item.Id == ownerId)
            ?? throw new ArgumentException("Ability owner blueprint no longer exists.");
        if (owner.IsReadOnly()) throw new ArgumentException("Installed blueprint combat is read-only.");
        var updatedOwner = new BlueprintDefinition(owner.Id, owner.Slug, owner.DisplayName, owner.Kind,
            proposal.UpdatedCombat, owner.PaletteOrigin, owner.IsFavorite, owner.Provenance,
            owner.CreaturePresentation, owner.CreatureProgression);
        var blueprints = project.Blueprints.Select(item => item.Id == owner.Id ? updatedOwner : item).ToArray();
        var currentPolicies = project.Module.RuntimePolicies;
        var currentExtensions = currentPolicies.Extensions;
        var documents = currentExtensions.JsonDocuments
            .Where(item => item.Id != proposal.RuntimeDocument.Id && item.Key != proposal.RuntimeDocument.Key)
            .Append(proposal.RuntimeDocument).ToArray();
        var extensions = new ModuleRuntimeExtensionProfile(currentExtensions.Id, currentExtensions.SchemaVersion,
            currentExtensions.RuleSchemas, currentExtensions.UiDocuments, documents, currentExtensions.SnapshotPolicy,
            "assistant:ability", currentExtensions.ModernTooling, currentExtensions.OperationalReadiness);
        var policies = new ModuleRuntimePolicyProfile(currentPolicies.Id, currentPolicies.SchemaVersion,
            currentPolicies.Heartbeats, currentPolicies.Rest, currentPolicies.SequenceAbort, "assistant:ability",
            currentPolicies.Cinematics, currentPolicies.Gameplay, currentPolicies.Rules, extensions);
        return new ProjectDefinition(project.Id, project.Title, project.Module.WithRuntimePolicies(policies),
            blueprints, project.Areas, project.Dialogues, project.Quests);
    }

    private AssistedAbilityApplyResult Failure(ProjectState state, ApplyAssistedAbilityCommand command,
        string code, string message)
    {
        var diagnostic = new CommandDiagnostic(code, message);
        var result = new CommandResult(false, state, command.BaseRevision, null, [diagnostic], null, null,
            new AuditEntry(command.CommandId, "apply_assisted_ability", _clock.UtcNow, false));
        return new(false, result, command.TransactionId, command.ExecutionPlanId, "no-change", [diagnostic]);
    }

    private static string Fingerprint(AssistedAbilityProposal proposal)
    {
        var value = $"{proposal.Orchestration.Id:D}|{proposal.Orchestration.OwnerBlueprintId}|" +
                    $"{proposal.RuntimeDocument.InitialValue}|{proposal.PromptSha256}|{proposal.Provenance}|{proposal.SemanticDiff}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}
