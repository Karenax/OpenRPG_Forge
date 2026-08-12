using System.Globalization;
using System.Text.RegularExpressions;

namespace Forge.Domain.Dialogues;

public enum DialogueAbortReason
{
    PlayerCancelled,
    ParticipantUnavailable,
    CombatStarted,
    AreaChanged,
    Scripted,
    Error,
}

public enum DialogueMemoryScope
{
    Dialogue,
    Participant,
    Player,
    Party,
    World,
}

public enum DialogueMemoryDuration
{
    Node,
    Session,
    Area,
    Campaign,
    Permanent,
}

public enum DialogueReviewBadge
{
    Condition,
    Action,
    Voice,
    Localization,
    Error,
    Link,
}

public enum DialogueSkillCheckVisibility
{
    Hidden,
    DifficultyOnly,
    ResultOnly,
    FullExplanation,
}

public sealed record DialogueLifecyclePolicy(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    IReadOnlyList<DialogueAction> CompletedActions,
    IReadOnlyList<DialogueAction> AbortedActions,
    bool RollbackOnAbort,
    bool CreateResumeState)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record DialogueSearchProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Pattern,
    bool UseRegex,
    bool IgnoreCase,
    IReadOnlyList<string> SpeakerRoles,
    IReadOnlyList<DialogueConditionKind> ConditionKinds,
    IReadOnlyList<DialogueActionKind> ActionKinds,
    string ReplacementPreview)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record DialogueReviewProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    IReadOnlyList<DialogueReviewBadge> Badges,
    IReadOnlyList<string> SpeakerRoles,
    bool ReviewMode)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record DialogueMemorySlot(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    DialogueMemoryScope Scope,
    string ScopeId,
    string Name,
    DialogueTextTokenType Type,
    DialogueMemoryDuration Duration,
    string? DefaultValue)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record DialogueSkillCheck(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    DialogueNodeId NodeId,
    string SkillId,
    double Difficulty,
    string? OpponentId,
    bool Advantage,
    DialogueSkillCheckVisibility Visibility,
    DialogueNodeId SuccessNodeId,
    DialogueNodeId FailureNodeId)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record DialogueExperienceProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool Enabled,
    bool RequiredAtRuntime,
    DialogueLifecyclePolicy Lifecycle,
    DialogueSearchProfile Search,
    DialogueReviewProfile Review,
    IReadOnlyList<DialogueMemorySlot> MemorySlots,
    IReadOnlyList<DialogueSkillCheck> SkillChecks)
{
    public const int CurrentSchemaVersion = 1;

    public static DialogueExperienceProfile CreateDefault(DialogueDefinition dialogue, string provenance)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var firstRole = dialogue.Participants.FirstOrDefault()?.Role ?? "narrator";
        return new(
            DialogueAuthoringProfile.StableId($"{dialogue.Id}:experience"),
            CurrentSchemaVersion,
            provenance,
            true,
            false,
            new DialogueLifecyclePolicy(
                DialogueAuthoringProfile.StableId($"{dialogue.Id}:experience:lifecycle"),
                DialogueLifecyclePolicy.CurrentSchemaVersion,
                provenance,
                [],
                [],
                false,
                true),
            new DialogueSearchProfile(
                DialogueAuthoringProfile.StableId($"{dialogue.Id}:experience:search"),
                DialogueSearchProfile.CurrentSchemaVersion,
                provenance,
                dialogue.DisplayName,
                false,
                true,
                [firstRole],
                [],
                [],
                string.Empty),
            new DialogueReviewProfile(
                DialogueAuthoringProfile.StableId($"{dialogue.Id}:experience:review"),
                DialogueReviewProfile.CurrentSchemaVersion,
                provenance,
                Enum.GetValues<DialogueReviewBadge>(),
                [],
                true),
            [],
            []);
    }
}

public sealed record DialogueLifecycleEvent(
    Guid Id,
    DialogueId DialogueId,
    string PlayerId,
    bool Completed,
    DialogueAbortReason? AbortReason,
    bool RolledBack,
    DialogueResumeState? ResumeState,
    IReadOnlyList<DialogueAction> Actions,
    DateTimeOffset At);

public sealed record DialogueResumeState(
    DialogueId DialogueId,
    DialogueNodeId NodeId,
    IReadOnlyDictionary<string, string> MemorySnapshot,
    int Seed);

public sealed record DialogueSearchHit(
    DialogueId DialogueId,
    DialogueNodeId NodeId,
    string Field,
    string Value,
    int Index,
    int Length);

public sealed record DialogueReplacePreview(
    IReadOnlyList<DialogueSearchHit> Hits,
    IReadOnlyDictionary<string, string> Replacements,
    string SemanticDiff);

public sealed record DialogueReviewEntry(
    DialogueId DialogueId,
    DialogueNodeId NodeId,
    IReadOnlyList<DialogueReviewBadge> Badges,
    DialogueNodeEditorialStatus EditorialStatus);

public sealed record DialogueMemoryState(IReadOnlyDictionary<string, string> Values)
{
    public static DialogueMemoryState Empty { get; } = new(new Dictionary<string, string>());
}

public sealed record DialogueSkillCheckResult(
    bool Success,
    double Roll,
    double Total,
    double Difficulty,
    string Explanation,
    DialogueNodeId TargetNodeId);

public sealed record DialogueExperienceRuntimeResult(
    DialogueExperienceProfile? Profile,
    IReadOnlyList<Validation.DomainDiagnostic> Diagnostics);

public static class DialogueExperienceRuntimeService
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    public static DialogueExperienceRuntimeResult Resolve(DialogueDefinition dialogue)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        var diagnostics = DialogueExperienceProfileValidator.Validate(dialogue);
        return new(dialogue.Experience, diagnostics);
    }

    public static DialogueLifecycleEvent Complete(
        DialogueDefinition dialogue,
        string playerId,
        DateTimeOffset at)
    {
        var profile = RequireProfile(dialogue);
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);
        return new(DialogueAuthoringProfile.StableId($"{dialogue.Id}:completed:{playerId}:{at:O}"), dialogue.Id,
            playerId, true, null, false, null, profile.Lifecycle.CompletedActions, at);
    }

    public static DialogueLifecycleEvent Abort(
        DialogueDefinition dialogue,
        string playerId,
        DialogueNodeId currentNodeId,
        DialogueAbortReason reason,
        DialogueMemoryState memory,
        int seed,
        DateTimeOffset at)
    {
        var profile = RequireProfile(dialogue);
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);
        if (!dialogue.Nodes.Any(item => item.Id == currentNodeId))
            throw new ArgumentException("DIALOGUE_EXPERIENCE_RESUME_NODE_MISSING", nameof(currentNodeId));
        var resume = profile.Lifecycle.CreateResumeState
            ? new DialogueResumeState(dialogue.Id, currentNodeId,
                memory.Values.OrderBy(item => item.Key, StringComparer.Ordinal)
                    .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal), seed)
            : null;
        return new(DialogueAuthoringProfile.StableId($"{dialogue.Id}:aborted:{playerId}:{reason}:{at:O}"),
            dialogue.Id, playerId, false, reason, profile.Lifecycle.RollbackOnAbort, resume,
            profile.Lifecycle.AbortedActions, at);
    }

    public static IReadOnlyList<DialogueSearchHit> Search(
        IReadOnlyList<DialogueDefinition> dialogues,
        DialogueSearchProfile search)
    {
        ArgumentNullException.ThrowIfNull(dialogues);
        ArgumentNullException.ThrowIfNull(search);
        var regex = Compile(search);
        var hits = new List<DialogueSearchHit>();
        foreach (var dialogue in dialogues.OrderBy(item => item.Slug, StringComparer.Ordinal))
            foreach (var node in dialogue.Nodes.OrderBy(item => item.Id.Value))
            {
                var role = dialogue.Participants.Single(item => item.Id == node.ParticipantId).Role;
                if (search.SpeakerRoles.Count > 0 && !search.SpeakerRoles.Contains(role, StringComparer.Ordinal))
                    continue;
                if (search.ConditionKinds.Count > 0 &&
                    !node.Choices.SelectMany(item => item.Conditions).Any(item => search.ConditionKinds.Contains(item.Kind)))
                    continue;
                if (search.ActionKinds.Count > 0 &&
                    !node.OnEnterActions.Concat(node.Choices.SelectMany(item => item.Actions))
                        .Any(item => search.ActionKinds.Contains(item.Kind)))
                    continue;
                AddMatches(dialogue.Id, node.Id, "text", node.Text.Fallback, regex, hits);
                foreach (var choice in node.Choices)
                    AddMatches(dialogue.Id, node.Id, $"choices/{choice.Id}/text", choice.Text.Fallback, regex, hits);
            }
        return hits;
    }

    public static DialogueReplacePreview PreviewReplace(
        IReadOnlyList<DialogueDefinition> dialogues,
        DialogueSearchProfile search)
    {
        var regex = Compile(search);
        var hits = Search(dialogues, search);
        var replacements = hits.GroupBy(item => $"{item.DialogueId}/{item.NodeId}/{item.Field}", StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => regex.Replace(group.First().Value,
                search.ReplacementPreview), StringComparer.Ordinal);
        return new(hits, replacements,
            $"replace-dialogue-text|hits:{hits.Count}|fields:{replacements.Count}|preview-only:true");
    }

    public static IReadOnlyList<DialogueReviewEntry> Review(
        IReadOnlyList<DialogueDefinition> dialogues,
        DialogueReviewProfile review)
    {
        ArgumentNullException.ThrowIfNull(dialogues);
        ArgumentNullException.ThrowIfNull(review);
        var entries = new List<DialogueReviewEntry>();
        foreach (var dialogue in dialogues.OrderBy(item => item.Id.Value))
            foreach (var node in dialogue.Nodes.OrderBy(item => item.Id.Value))
            {
                var role = dialogue.Participants.Single(item => item.Id == node.ParticipantId).Role;
                if (review.SpeakerRoles.Count > 0 && !review.SpeakerRoles.Contains(role, StringComparer.Ordinal))
                    continue;
                var badges = BuildBadges(dialogue, node);
                if (review.Badges.Count > 0 && !review.Badges.Any(badges.Contains)) continue;
                var status = dialogue.Authoring?.Nodes.SingleOrDefault(item => item.NodeId == node.Id)?.Notes.Status ??
                             DialogueNodeEditorialStatus.Draft;
                if (review.ReviewMode && status == DialogueNodeEditorialStatus.Approved) continue;
                entries.Add(new(dialogue.Id, node.Id, badges.Order().ToArray(), status));
            }
        return entries;
    }

    public static DialogueMemoryState Write(
        DialogueExperienceProfile profile,
        DialogueMemoryState state,
        Guid slotId,
        string value)
    {
        var slot = profile.MemorySlots.SingleOrDefault(item => item.Id == slotId) ??
                   throw new KeyNotFoundException("DIALOGUE_MEMORY_SLOT_MISSING");
        if (!CanConvert(slot.Type, value)) throw new ArgumentException("DIALOGUE_MEMORY_VALUE_INVALID", nameof(value));
        var values = state.Values.ToDictionary(StringComparer.Ordinal);
        values[MemoryKey(slot)] = value;
        return new(values);
    }

    public static string? Read(
        DialogueExperienceProfile profile,
        DialogueMemoryState state,
        Guid slotId)
    {
        var slot = profile.MemorySlots.SingleOrDefault(item => item.Id == slotId) ??
                   throw new KeyNotFoundException("DIALOGUE_MEMORY_SLOT_MISSING");
        return state.Values.GetValueOrDefault(MemoryKey(slot)) ?? slot.DefaultValue;
    }

    public static DialogueSkillCheckResult ResolveSkillCheck(
        DialogueSkillCheck check,
        double actorScore,
        double opponentScore,
        int seed)
    {
        var random = new Random(seed);
        var first = random.Next(1, 21);
        var second = random.Next(1, 21);
        var roll = check.Advantage ? Math.Max(first, second) : first;
        var total = roll + actorScore - opponentScore;
        var success = total >= check.Difficulty;
        var explanation = check.Visibility switch
        {
            DialogueSkillCheckVisibility.Hidden => string.Empty,
            DialogueSkillCheckVisibility.DifficultyOnly => $"Difficulté {check.Difficulty:0.##}",
            DialogueSkillCheckVisibility.ResultOnly => success ? "Réussite" : "Échec",
            _ => $"Jet {roll:0} + compétence {actorScore:0.##} - opposant {opponentScore:0.##} = " +
                 $"{total:0.##} contre {check.Difficulty:0.##} : {(success ? "réussite" : "échec")}",
        };
        return new(success, roll, total, check.Difficulty, explanation,
            success ? check.SuccessNodeId : check.FailureNodeId);
    }

    public static void ValidateSearch(DialogueSearchProfile search) => _ = Compile(search);

    internal static Regex Compile(DialogueSearchProfile search)
    {
        if (string.IsNullOrWhiteSpace(search.Pattern) || search.Pattern.Length > 512)
            throw new ArgumentException("DIALOGUE_SEARCH_PATTERN_INVALID", nameof(search));
        var pattern = search.UseRegex ? search.Pattern : Regex.Escape(search.Pattern);
        var options = RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;
        if (search.IgnoreCase) options |= RegexOptions.IgnoreCase;
        try
        {
            return new Regex(pattern, options, RegexTimeout);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            throw new ArgumentException("DIALOGUE_SEARCH_REGEX_UNSAFE_OR_INVALID", nameof(search), exception);
        }
    }

    internal static bool CanConvert(DialogueTextTokenType type, string value) => type switch
    {
        DialogueTextTokenType.Integer => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture,
            out _),
        DialogueTextTokenType.Decimal => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture,
            out _),
        DialogueTextTokenType.Boolean => bool.TryParse(value, out _),
        _ => !string.IsNullOrWhiteSpace(value),
    };

    private static DialogueExperienceProfile RequireProfile(DialogueDefinition dialogue)
    {
        var resolved = Resolve(dialogue);
        if (resolved.Diagnostics.Count > 0)
            throw new ArgumentException(string.Join(';', resolved.Diagnostics.Select(item => item.Code)),
                nameof(dialogue));
        return resolved.Profile is { Enabled: true } profile
            ? profile
            : throw new InvalidOperationException("DIALOGUE_EXPERIENCE_DISABLED");
    }

    private static string MemoryKey(DialogueMemorySlot slot) =>
        $"{slot.Scope}:{slot.ScopeId}:{slot.Name}:{slot.Duration}";

    private static void AddMatches(
        DialogueId dialogueId,
        DialogueNodeId nodeId,
        string field,
        string value,
        Regex regex,
        ICollection<DialogueSearchHit> hits)
    {
        foreach (Match match in regex.Matches(value))
            hits.Add(new(dialogueId, nodeId, field, value, match.Index, match.Length));
    }

    private static HashSet<DialogueReviewBadge> BuildBadges(DialogueDefinition dialogue, DialogueNode node)
    {
        var badges = new HashSet<DialogueReviewBadge>();
        if (node.Choices.Any(item => item.Conditions.Count > 0)) badges.Add(DialogueReviewBadge.Condition);
        if (node.OnEnterActions.Count > 0 || node.Choices.Any(item => item.Actions.Count > 0))
            badges.Add(DialogueReviewBadge.Action);
        if (node.OnEnterActions.Concat(node.Choices.SelectMany(item => item.Actions))
            .Any(item => item.Kind == DialogueActionKind.PlayAudio)) badges.Add(DialogueReviewBadge.Voice);
        if (node.Text.Variants.Count > 0 || node.Choices.Any(item => item.Text.Variants.Count > 0))
            badges.Add(DialogueReviewBadge.Localization);
        if (node.Choices.Any(item => item.TargetNodeId is not null)) badges.Add(DialogueReviewBadge.Link);
        if (DialogueValidator.Validate(dialogue).Any(item =>
                item.Path.Contains($"/nodes/{node.Id}", StringComparison.Ordinal)))
            badges.Add(DialogueReviewBadge.Error);
        return badges;
    }
}
