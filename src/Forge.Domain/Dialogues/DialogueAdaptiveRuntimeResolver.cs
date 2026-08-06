using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Validation;

namespace Forge.Domain.Dialogues;

public sealed record DialogueAdaptiveRuntimeContext(
    string Locale,
    bool IsServer,
    long ServerRevision,
    bool PauseRequested,
    bool AccessibilityPauseRequested,
    int Seed,
    Guid SessionId,
    IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> RecentVariantIds);

public sealed record DialogueTimedChoiceRuntimeState(
    DialogueTimedChoiceRule Rule,
    int RemainingMilliseconds,
    bool Paused,
    bool TimedOut,
    DialogueChoiceId? SelectedChoiceId,
    string? TimeoutScriptId);

public sealed record DialogueVariantSelection(
    DialogueVariantSet Set,
    DialogueTextVariant Variant,
    string Locale,
    int DeterministicRoll);

public sealed record DialogueAdaptiveResolution(
    DialogueTimedChoiceRuntimeState? TimedChoice,
    DialogueVariantSelection? Variant,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class DialogueAdaptiveRuntimeResolver
{
    public const string AuthorityDeniedCode = "dialogue.adaptive.authority_denied";
    public const string RevisionStaleCode = "dialogue.timed_choice.revision_stale";
    public const string VariantRequiredCode = "dialogue.variant.required_missing";

    public static DialogueAdaptiveResolution Resolve(
        DialogueDefinition dialogue,
        DialogueNodeId nodeId,
        int elapsedMilliseconds,
        DialogueAdaptiveRuntimeContext context,
        IDialogueConditionEvaluator? evaluator = null)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentNullException.ThrowIfNull(nodeId);
        ArgumentNullException.ThrowIfNull(context);
        if (elapsedMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMilliseconds));
        if (dialogue.Presentation is null) return new(null, null, []);

        var validation = DialoguePresentationValidator.Validate(dialogue);
        if (validation.Count > 0) return new(null, null, validation);

        var diagnostics = new List<DomainDiagnostic>();
        var timed = ResolveTimedChoice(dialogue, nodeId, elapsedMilliseconds, context, diagnostics);
        var variant = ResolveVariant(dialogue, nodeId, context, evaluator, diagnostics);
        return new(timed, variant, diagnostics);
    }

    private static DialogueTimedChoiceRuntimeState? ResolveTimedChoice(
        DialogueDefinition dialogue,
        DialogueNodeId nodeId,
        int elapsedMilliseconds,
        DialogueAdaptiveRuntimeContext context,
        ICollection<DomainDiagnostic> diagnostics)
    {
        var rule = dialogue.Presentation!.TimedChoices.SingleOrDefault(item => item.NodeId == nodeId);
        if (rule is null) return null;
        var path = $"dialogues/{dialogue.Id}/presentation/timedChoices/{rule.Id}";
        if (rule.AuthorityScope == DialogueRuntimeAuthorityScope.Server && !context.IsServer)
        {
            diagnostics.Add(new(AuthorityDeniedCode,
                "Timed dialogue choices can only advance on the authoritative server.", path));
            return null;
        }
        if (rule.AuthorityScope == DialogueRuntimeAuthorityScope.Server && context.ServerRevision < rule.ServerRevision)
        {
            diagnostics.Add(new(RevisionStaleCode,
                "The timed-choice runtime revision is older than the authored server revision.",
                $"{path}/serverRevision"));
            return null;
        }

        var paused = context.PauseRequested && (rule.PausePolicy == DialogueTimedChoicePausePolicy.Always ||
            rule.PausePolicy == DialogueTimedChoicePausePolicy.AccessibilityOnly && context.AccessibilityPauseRequested);
        var remaining = Math.Max(0, rule.DurationMilliseconds - elapsedMilliseconds);
        var timedOut = !paused && remaining == 0;
        return new(rule, remaining, paused, timedOut, timedOut ? rule.DefaultChoiceId : null,
            timedOut ? rule.TimeoutScriptId : null);
    }

    private static DialogueVariantSelection? ResolveVariant(
        DialogueDefinition dialogue,
        DialogueNodeId nodeId,
        DialogueAdaptiveRuntimeContext context,
        IDialogueConditionEvaluator? evaluator,
        ICollection<DomainDiagnostic> diagnostics)
    {
        var set = dialogue.Presentation!.VariantSets.SingleOrDefault(item => item.NodeId == nodeId);
        if (set is null) return null;
        var path = $"dialogues/{dialogue.Id}/presentation/variantSets/{set.Id}";
        if (set.AuthorityScope == DialogueRuntimeAuthorityScope.Server && !context.IsServer)
        {
            diagnostics.Add(new(AuthorityDeniedCode,
                "Procedural dialogue variants can only be selected by the authoritative server.", path));
            return null;
        }

        var eligible = set.Variants
            .Where(item => item.Conditions.Count == 0 || evaluator is not null && item.Conditions.All(evaluator.IsSatisfied))
            .OrderBy(item => item.Id)
            .ToArray();
        if (eligible.Length == 0)
        {
            if (set.RequiredAtRuntime)
                diagnostics.Add(new(VariantRequiredCode,
                    "The required procedural variant set has no eligible localized variant.", path));
            return null;
        }

        if (set.AntiRepeatWindow > 0 && context.RecentVariantIds.TryGetValue(set.Id, out var recent))
        {
            var excluded = recent.TakeLast(set.AntiRepeatWindow).ToHashSet();
            var withoutRecent = eligible.Where(item => !excluded.Contains(item.Id)).ToArray();
            if (withoutRecent.Length > 0) eligible = withoutRecent;
        }

        var totalWeight = eligible.Sum(item => item.Weight);
        var roll = StableRoll(dialogue, nodeId, set, context, totalWeight);
        var cursor = roll;
        foreach (var candidate in eligible)
        {
            if (cursor < candidate.Weight) return new(set, candidate, context.Locale, roll);
            cursor -= candidate.Weight;
        }

        throw new InvalidOperationException("Deterministic dialogue variant selection exhausted its weight range.");
    }

    private static int StableRoll(DialogueDefinition dialogue, DialogueNodeId nodeId, DialogueVariantSet set,
        DialogueAdaptiveRuntimeContext context, int totalWeight)
    {
        var scope = set.SeedScope switch
        {
            DialogueVariantSeedScope.Dialogue => dialogue.Id.ToString(),
            DialogueVariantSeedScope.Node => nodeId.ToString(),
            DialogueVariantSeedScope.Session => context.SessionId.ToString("D"),
            DialogueVariantSeedScope.ServerRevision => context.ServerRevision.ToString(),
            _ => throw new ArgumentOutOfRangeException(nameof(set.SeedScope)),
        };
        var input = $"{dialogue.Id}|{nodeId}|{set.Id}|{scope}|{context.Seed}|{set.SeedOffset}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return (int)(BinaryPrimitives.ReadUInt64BigEndian(hash) % (ulong)totalWeight);
    }
}
