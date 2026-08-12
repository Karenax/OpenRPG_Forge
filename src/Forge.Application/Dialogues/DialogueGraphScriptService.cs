using Forge.Domain.Dialogues;

namespace Forge.Application.Dialogues;

public sealed record DialogueGraphHandle(DialogueId Id, int SchemaVersion);

public sealed record DialogueGraphScriptSnapshot(
    DialogueGraphHandle Handle,
    string Slug,
    string DisplayName,
    DialogueNodeId EntryNodeId,
    int NodeCount,
    int EdgeCount,
    int ReferenceEdgeCount,
    int LocalizationVariantCount,
    int ConditionCount,
    int ActionCount,
    IReadOnlyList<DialogueNodeRole> Roles);

public sealed record DialogueAuthoringScriptSnapshot(
    DialogueGraphHandle Handle,
    bool Enabled,
    bool RequiredAtRuntime,
    int ParticipantCount,
    int NodePolicyCount,
    int TokenCount,
    int WizardEntryCount,
    IReadOnlyList<DialogueChoiceEvaluationMode> EvaluationModes);

public sealed record DialogueExperienceScriptSnapshot(
    DialogueGraphHandle Handle,
    bool Enabled,
    bool RequiredAtRuntime,
    int CompletedActionCount,
    int AbortedActionCount,
    bool RollbackOnAbort,
    bool CreateResumeState,
    string SearchPattern,
    bool SafeRegex,
    IReadOnlyList<DialogueReviewBadge> ReviewBadges,
    int MemorySlotCount,
    int SkillCheckCount);

public sealed record DialogueProductionScriptSnapshot(
    DialogueGraphHandle Handle,
    bool Enabled,
    bool RequiredAtRuntime,
    int SubgraphCount,
    int SubgraphInvocationCount,
    int ConsequenceCount,
    int CoverageEvidenceCount,
    int CoveredNodeCount,
    double CoverageRatio,
    int RevisionCount,
    int LockCount,
    int CommentCount,
    int ConflictCount,
    int AiPreviewCount);

/// <summary>Read-only typed boundary used by ForgeScript adapters.</summary>
public sealed class DialogueGraphScriptService
{
    private readonly DialogueDefinition _dialogue;

    public DialogueGraphScriptService(DialogueDefinition dialogue)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        var diagnostics = DialogueValidator.Validate(dialogue);
        if (diagnostics.Count > 0)
            throw new ArgumentException(string.Join(';', diagnostics.Select(item => item.Code)), nameof(dialogue));
        _dialogue = dialogue;
    }

    public DialogueGraphHandle Handle => new(_dialogue.Id, _dialogue.SchemaVersion);

    public DialogueGraphScriptSnapshot Read(DialogueGraphHandle handle)
    {
        Ensure(handle);
        return new(handle, _dialogue.Slug, _dialogue.DisplayName, _dialogue.EntryNodeId,
            _dialogue.Nodes.Count, _dialogue.Edges.Count,
            _dialogue.Edges.Count(item => item.Kind == DialogueEdgeKind.Reference),
            _dialogue.Nodes.Sum(item => item.Text.Variants.Count + item.Choices.Sum(choice => choice.Text.Variants.Count)),
            _dialogue.Nodes.Sum(item => item.Choices.Sum(choice => choice.Conditions.Count)),
            _dialogue.Nodes.Sum(item => item.OnEnterActions.Count + item.Choices.Sum(choice => choice.Actions.Count)),
            _dialogue.Nodes.Select(item => item.Role).Distinct().Order().ToArray());
    }

    public DialogueNode ReadNode(DialogueGraphHandle handle, DialogueNodeId nodeId)
    {
        Ensure(handle);
        return _dialogue.Nodes.SingleOrDefault(item => item.Id == nodeId)
            ?? throw new KeyNotFoundException("DIALOGUE_NODE_MISSING");
    }

    public DialogueAuthoringScriptSnapshot ReadAuthoring(DialogueGraphHandle handle)
    {
        Ensure(handle);
        var profile = _dialogue.Authoring;
        return profile is null
            ? new(handle, false, false, 0, 0, 0, 0, [])
            : new(handle, profile.Enabled, profile.RequiredAtRuntime, profile.Participants.Count, profile.Nodes.Count,
                profile.Nodes.Sum(item => item.Tokens.Count), profile.Nodes.Sum(item => item.WizardEntries.Count),
                profile.Nodes.Select(item => item.EvaluationMode).Distinct().Order().ToArray());
    }

    public DialogueNodeAuthoring? ReadNodeAuthoring(DialogueGraphHandle handle, DialogueNodeId nodeId)
    {
        Ensure(handle);
        return _dialogue.Authoring?.Nodes.SingleOrDefault(item => item.NodeId == nodeId);
    }

    public DialogueExperienceScriptSnapshot ReadExperience(DialogueGraphHandle handle)
    {
        Ensure(handle);
        var profile = _dialogue.Experience;
        return profile is null
            ? new(handle, false, false, 0, 0, false, false, string.Empty, false, [], 0, 0)
            : new(handle, profile.Enabled, profile.RequiredAtRuntime,
                profile.Lifecycle.CompletedActions.Count, profile.Lifecycle.AbortedActions.Count,
                profile.Lifecycle.RollbackOnAbort, profile.Lifecycle.CreateResumeState, profile.Search.Pattern,
                profile.Search.UseRegex, profile.Review.Badges, profile.MemorySlots.Count,
                profile.SkillChecks.Count);
    }

    public DialogueSkillCheck? ReadSkillCheck(DialogueGraphHandle handle, DialogueNodeId nodeId)
    {
        Ensure(handle);
        return _dialogue.Experience?.SkillChecks.SingleOrDefault(item => item.NodeId == nodeId);
    }

    public DialogueProductionScriptSnapshot ReadProduction(DialogueGraphHandle handle)
    {
        Ensure(handle);
        var profile = _dialogue.Production;
        if (profile is null) return new(handle, false, false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var coverage = DialogueProductionRuntimeService.MeasureCoverage(_dialogue);
        return new(handle, profile.Enabled, profile.RequiredAtRuntime, profile.Subgraphs.Count,
            profile.Invocations.Count, profile.Consequences.Count, profile.Coverage.Evidence.Count,
            coverage.CoveredNodes.Count, coverage.Ratio, profile.Collaboration.Revisions.Count,
            profile.Collaboration.Locks.Count, profile.Collaboration.Comments.Count,
            profile.Collaboration.Conflicts.Count, profile.AiPlans.Count(item => item.PreviewOnly && !item.Applied));
    }

    public DialogueSubgraphContract? ReadSubgraph(DialogueGraphHandle handle, string key)
    {
        Ensure(handle);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _dialogue.Production?.Subgraphs.SingleOrDefault(item =>
            string.Equals(item.Key, key, StringComparison.Ordinal));
    }

    public IReadOnlyList<DialogueTypedConsequence> ReadConsequences(
        DialogueGraphHandle handle,
        DialogueNodeId nodeId)
    {
        Ensure(handle);
        return _dialogue.Production?.Consequences.Where(item => item.NodeId == nodeId)
            .OrderBy(item => item.Kind).ThenBy(item => item.TargetId, StringComparer.Ordinal).ToArray() ?? [];
    }

    private void Ensure(DialogueGraphHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (handle.Id != _dialogue.Id || handle.SchemaVersion != _dialogue.SchemaVersion)
            throw new InvalidOperationException("DIALOGUE_GRAPH_HANDLE_STALE");
    }
}
