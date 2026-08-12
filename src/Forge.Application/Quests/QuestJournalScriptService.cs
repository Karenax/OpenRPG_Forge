using Forge.Domain.Quests;

namespace Forge.Application.Quests;

public sealed record QuestJournalHandle(QuestId Id, int SchemaVersion);

public sealed record QuestJournalScriptSnapshot(
    QuestJournalHandle Handle,
    string Slug,
    string Category,
    int Priority,
    string Owner,
    QuestProgressScope DefaultScope,
    IReadOnlyList<QuestProgressScope> AllowedScopes,
    int StateCount,
    int TerminalStateCount,
    int LocalizedTextCount,
    int ContextLinkCount);

public sealed record QuestTransitionScriptSnapshot(
    QuestTransitionId Id,
    int SchemaVersion,
    string EventName,
    QuestEventKind EventKind,
    QuestStateId FromStateId,
    QuestStateId ToStateId,
    int DisplayOrder,
    int Priority,
    QuestTransitionIdempotence Idempotence,
    int ConditionCount,
    int ActionCount,
    int ForgeScriptApiVersion);

public sealed record QuestSimulationScriptSnapshot(
    Guid ScenarioId,
    QuestSimulationStatus Status,
    QuestStateId FinalStateId,
    int StepCount,
    int AppliedTransitionCount,
    int DiagnosticCount,
    IReadOnlyDictionary<string, string> Variables);

public sealed record QuestAuthoringScriptSnapshot(
    QuestJournalHandle Handle,
    Guid ProfileId,
    int SchemaVersion,
    int ObjectiveCount,
    int ParticipantCount,
    int RewardChannelCount,
    int ReviewCount,
    int WizardZoneCount,
    int GeneratedTestCount,
    bool GeneratesOpaqueCode,
    QuestEditorialStatus EditorialStatus);

public sealed record QuestExperienceScriptSnapshot(
    QuestJournalHandle Handle,
    Guid ProfileId,
    int SchemaVersion,
    int ResourceCount,
    int StepCount,
    QuestExperienceVisibility Visibility,
    int HistoryEventCount,
    int MarkerCount,
    int TrackedMarkerCount);

public sealed record QuestQualityScriptSnapshot(
    QuestJournalHandle Handle,
    Guid ProfileId,
    int SchemaVersion,
    int ScenarioCount,
    int StepCount,
    int AssertionCount,
    Guid PlanId,
    Guid TransactionId,
    QuestAiAuthorizationLevel Authorization,
    bool HumanApproved,
    bool Applied);

/// <summary>Read-only typed boundary used by ForgeScript adapters.</summary>
public sealed class QuestJournalScriptService
{
    private readonly QuestDefinition _quest;

    public QuestJournalScriptService(QuestDefinition quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var diagnostics = QuestValidator.Validate(quest);
        if (diagnostics.Count > 0)
            throw new ArgumentException(string.Join(';', diagnostics.Select(item => item.Code)), nameof(quest));
        _quest = quest;
    }

    public QuestJournalHandle Handle => new(_quest.Id, _quest.SchemaVersion);

    public QuestJournalScriptSnapshot Read(QuestJournalHandle handle)
    {
        Ensure(handle);
        return new(handle, _quest.Slug, _quest.Category, _quest.Priority, _quest.Owner,
            _quest.InstancePolicy.DefaultScope, _quest.InstancePolicy.AllowedScopes,
            _quest.States.Count, _quest.States.Count(item => item.IsTerminal),
            _quest.States.Sum(item => 2 + item.Journal.Objectives.Count +
                                      item.Journal.ContextLinks.Count(link => link.Label is not null)),
            _quest.States.Sum(item => item.Journal.ContextLinks.Count));
    }

    public QuestStateDefinition ReadState(QuestJournalHandle handle, QuestStateId stateId)
    {
        Ensure(handle);
        return _quest.States.SingleOrDefault(item => item.Id == stateId)
            ?? throw new KeyNotFoundException("QUEST_STATE_MISSING");
    }

    public IReadOnlyList<QuestTransitionScriptSnapshot> ReadTransitions(QuestJournalHandle handle)
    {
        Ensure(handle);
        return _quest.Transitions.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id.Value)
            .Select(item => new QuestTransitionScriptSnapshot(item.Id, item.SchemaVersion,
                item.EventBinding.EventName, item.EventBinding.Kind, item.FromStateId, item.ToStateId,
                item.DisplayOrder, item.Priority, item.Idempotence, item.Conditions.Count, item.Actions.Count,
                item.ForgeScriptApiVersion)).ToArray();
    }

    public QuestAuthoringScriptSnapshot ReadAuthoring(QuestJournalHandle handle)
    {
        Ensure(handle);
        var profile = _quest.Authoring ?? throw new InvalidOperationException("QUEST_AUTHORING_PROFILE_MISSING");
        return new(handle, profile.Id, profile.SchemaVersion, profile.Objectives.Count, profile.Participants.Count,
            profile.Rewards.Items.Count + profile.Rewards.Currencies.Count + profile.Rewards.Reputation.Count +
            profile.Rewards.Unlocks.Count + profile.Rewards.ChoiceIds.Count,
            profile.Notes.ReviewHistory.Count, profile.Wizard.ZoneIds.Count,
            profile.Wizard.GeneratedTestScenarios.Count, profile.Wizard.GeneratesOpaqueCode, profile.Notes.Status);
    }

    public QuestAuthoringObjective ReadObjective(QuestJournalHandle handle, Guid objectiveId)
    {
        Ensure(handle);
        return _quest.Authoring?.Objectives.SingleOrDefault(item => item.Id == objectiveId)
            ?? throw new KeyNotFoundException("QUEST_AUTHORING_OBJECTIVE_MISSING");
    }

    public QuestAuthoringParticipant ReadParticipant(QuestJournalHandle handle, string role)
    {
        Ensure(handle);
        return _quest.Authoring?.Participants.SingleOrDefault(item =>
                   string.Equals(item.Role, role, StringComparison.Ordinal))
               ?? throw new KeyNotFoundException("QUEST_AUTHORING_PARTICIPANT_MISSING");
    }

    public QuestExperienceScriptSnapshot ReadExperience(QuestJournalHandle handle)
    {
        Ensure(handle);
        var profile = _quest.Experience ?? throw new InvalidOperationException("QUEST_EXPERIENCE_PROFILE_MISSING");
        return new(handle, profile.Id, profile.SchemaVersion, profile.Resources.Count, profile.Steps.Count,
            profile.Visibility.Initial, profile.History.Count, profile.Markers.Count,
            profile.Markers.Count(item => item.Tracked));
    }

    public QuestExperienceMarker ReadMarker(QuestJournalHandle handle, Guid markerId)
    {
        Ensure(handle);
        return _quest.Experience?.Markers.SingleOrDefault(item => item.Id == markerId)
               ?? throw new KeyNotFoundException("QUEST_EXPERIENCE_MARKER_MISSING");
    }

    public IReadOnlyList<QuestExperienceHistoryItem> ReadHistory(
        QuestJournalHandle handle,
        QuestExperienceHistoryView view)
    {
        Ensure(handle);
        var profile = _quest.Experience ?? throw new InvalidOperationException("QUEST_EXPERIENCE_PROFILE_MISSING");
        return QuestExperienceRuntimeService.ReadHistory(profile, view);
    }

    public QuestQualityScriptSnapshot ReadQuality(QuestJournalHandle handle)
    {
        Ensure(handle);
        var profile = _quest.Quality ?? throw new InvalidOperationException("QUEST_QUALITY_PROFILE_MISSING");
        return new(handle, profile.Id, profile.SchemaVersion, profile.TestScenarios.Count,
            profile.TestScenarios.Sum(item => item.Steps.Count),
            profile.TestScenarios.Sum(item => item.Assertions.Count), profile.AiPlan.Id,
            profile.AiPlan.TransactionId, profile.AiPlan.Authorization,
            profile.AiPlan.ApprovedAt is not null && !string.IsNullOrWhiteSpace(profile.AiPlan.ApprovedBy),
            profile.AiPlan.Applied);
    }

    public QuestQualityTestScenario ReadQualityScenario(QuestJournalHandle handle, Guid scenarioId)
    {
        Ensure(handle);
        return _quest.Quality?.TestScenarios.SingleOrDefault(item => item.Id == scenarioId)
               ?? throw new KeyNotFoundException("QUEST_QUALITY_SCENARIO_MISSING");
    }

    public QuestAiGenerationPlan ReadAiPlan(QuestJournalHandle handle)
    {
        Ensure(handle);
        return _quest.Quality?.AiPlan ?? throw new InvalidOperationException("QUEST_AI_PLAN_MISSING");
    }

    public QuestSimulationScriptSnapshot Simulate(
        QuestJournalHandle handle,
        QuestProgressScope scope,
        string scopeOwnerId,
        DateTimeOffset startedAt,
        bool authorityGranted = true)
    {
        Ensure(handle);
        var result = QuestSimulationEngine.Run(_quest, scope, scopeOwnerId, startedAt, authorityGranted);
        return new(result.ScenarioId, result.Status, result.Instance.CurrentStateId, result.Steps.Count,
            result.Audit.Count, result.Diagnostics.Count, result.Instance.Variables);
    }

    private void Ensure(QuestJournalHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (handle.Id != _quest.Id || handle.SchemaVersion != _quest.SchemaVersion)
            throw new InvalidOperationException("QUEST_JOURNAL_HANDLE_STALE");
    }
}
