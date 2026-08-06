using Forge.Domain.Validation;

namespace Forge.Domain.Dialogues;

public static class DialoguePresentationRuntimeResolver
{
    public static DialoguePresentationResolution Resolve(
        DialogueDefinition dialogue,
        DialogueNodeId nodeId,
        DialoguePresentationRuntimeContext context)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentNullException.ThrowIfNull(nodeId);
        ArgumentNullException.ThrowIfNull(context);
        if (dialogue.Presentation is null)
            return context.RequirePresentation
                ? new([], null, true, null, null,
                    [new DomainDiagnostic(DialoguePresentationValidator.RequiredMissingCode,
                        "The runtime requires a dialogue presentation profile.",
                        $"dialogues/{dialogue.Id}/presentation")], false)
                : new([], null, true, null, null, [], true);

        var profile = dialogue.Presentation;
        var diagnostics = DialoguePresentationValidator.Validate(dialogue).ToList();
        if (diagnostics.Count > 0)
            return new([], null, true, null, null, diagnostics, false);

        var animations = profile.AnimationCues.Where(item => item.NodeId == nodeId)
            .OrderBy(item => item.StartMilliseconds).ThenBy(item => item.Id).ToArray();
        var voice = profile.VoiceCues.Where(item => item.NodeId == nodeId)
            .OrderBy(item => LocaleRank(item.Locale, context.Locale)).ThenBy(item => item.Id)
            .FirstOrDefault();
        var subtitleFallback = voice is null || string.IsNullOrWhiteSpace(voice.Resource);

        var camera = profile.CameraShots.Where(item => item.NodeId == nodeId &&
                item.Mode != DialogueCameraShotMode.Disabled &&
                !(context.PlayerControlsCamera && item.AllowPlayerOverride) &&
                !(context.ReduceMotion && item.ReduceMotionFallback))
            .OrderByDescending(item => item.Priority).ThenBy(item => item.Id).FirstOrDefault();

        DialogueInterjectionRule? interjection = null;
        if (profile.InterjectionRules.Any(item => item.TriggerNodeId == nodeId) && !context.IsServer)
            diagnostics.Add(new("dialogue.interjection.authority_denied",
                "Companion interjections can only be selected by the authoritative server.",
                $"dialogues/{dialogue.Id}/presentation/interjectionRules"));
        else
            interjection = ResolveInterjection(profile, nodeId, context);

        return new(animations, voice, subtitleFallback, camera, interjection, diagnostics, false);
    }

    private static DialogueInterjectionRule? ResolveInterjection(
        DialoguePresentationProfile profile,
        DialogueNodeId nodeId,
        DialoguePresentationRuntimeContext context)
    {
        var candidates = profile.InterjectionRules.Where(rule => rule.TriggerNodeId == nodeId)
            .Where(rule => !context.LastInterjections.TryGetValue(rule.Id, out var last) ||
                           context.Now - last >= TimeSpan.FromSeconds(rule.CooldownSeconds))
            .Where(rule => context.Companions.Any(companion => companion.BlueprintId == rule.CompanionBlueprintId &&
                (!rule.RequirePresent || companion.Present) && companion.Relation >= rule.MinimumRelation &&
                rule.PersonalityTags.All(companion.PersonalityTags.Contains)))
            .OrderByDescending(rule => rule.Priority).ThenBy(rule => rule.Id).ToArray();
        if (candidates.Length == 0)
            return null;
        var first = candidates[0];
        if (first.ConflictPolicy == DialogueInterjectionConflictPolicy.SuppressAll &&
            candidates.Count(item => string.Equals(item.ConflictGroup, first.ConflictGroup, StringComparison.Ordinal)) > 1)
            return null;
        return first;
    }

    private static int LocaleRank(string candidate, string requested)
    {
        if (string.Equals(candidate, requested, StringComparison.OrdinalIgnoreCase)) return 0;
        var language = requested.Split('-', 2)[0];
        if (string.Equals(candidate, language, StringComparison.OrdinalIgnoreCase) ||
            candidate.StartsWith(language + "-", StringComparison.OrdinalIgnoreCase)) return 1;
        return 2;
    }
}
