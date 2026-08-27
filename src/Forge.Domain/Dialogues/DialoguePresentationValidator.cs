using Forge.Domain.Validation;

namespace Forge.Domain.Dialogues;

public static class DialoguePresentationValidator
{
    public const string RequiredMissingCode = "dialogue.presentation.required_missing";
    public const string VoiceInvalidCode = "dialogue.voice.invalid";

    public static IReadOnlyList<DomainDiagnostic> Validate(
        DialogueDefinition dialogue,
        IReadOnlySet<Guid>? cameraAnchorIds = null)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        if (dialogue.Presentation is null)
            return [];

        var profile = dialogue.Presentation;
        var root = $"dialogues/{dialogue.Id}/presentation";
        var diagnostics = new List<DomainDiagnostic>();
        var nodes = dialogue.Nodes.Select(item => item.Id).ToHashSet();
        var uuids = new HashSet<Guid>();
        if (profile.Id == Guid.Empty || profile.SchemaVersion != 1 || string.IsNullOrWhiteSpace(profile.Provenance))
            diagnostics.Add(new("dialogue.presentation.identity_invalid",
                "Dialogue presentation identity, schema, or provenance is invalid.", root));
        if (!profile.ServerAuthoritativeInterjections)
            diagnostics.Add(new("dialogue.interjection.authority_required",
                "Companion interjections must be selected by the authoritative server.",
                $"{root}/serverAuthoritativeInterjections"));
        AddUuid(profile.Id, root, uuids, diagnostics);

        foreach (var cue in profile.AnimationCues)
        {
            var path = $"{root}/animationCues/{cue.Id}";
            AddUuid(cue.Id, path, uuids, diagnostics);
            CheckNode(cue.NodeId, nodes, path, diagnostics);
            if (string.IsNullOrWhiteSpace(cue.AnimationId) || cue.StartMilliseconds < 0 ||
                cue.DurationMilliseconds is < 1 or > 3_600_000 ||
                cue.BlendMilliseconds is < 0 or > 60_000 || cue.BlendMilliseconds > cue.DurationMilliseconds ||
                string.IsNullOrWhiteSpace(cue.FallbackAnimationId) || string.IsNullOrWhiteSpace(cue.Provenance))
                diagnostics.Add(new("dialogue.animation.invalid",
                    "Animation, timing, blend, fallback, or provenance is invalid.", path));
        }

        foreach (var cue in profile.VoiceCues)
        {
            var path = $"{root}/voiceCues/{cue.Id}";
            AddUuid(cue.Id, path, uuids, diagnostics);
            CheckNode(cue.NodeId, nodes, path, diagnostics);
            var hasResource = !string.IsNullOrWhiteSpace(cue.Resource);
            if (string.IsNullOrWhiteSpace(cue.Locale) || string.IsNullOrWhiteSpace(cue.Take) ||
                string.IsNullOrWhiteSpace(cue.LipSyncProfileId) || string.IsNullOrWhiteSpace(cue.Emotion) ||
                cue.DurationMilliseconds is < 1 or > 3_600_000 || string.IsNullOrWhiteSpace(cue.Provenance) ||
                (hasResource && (!IsSafeResource(cue.Resource!) || !SpdxLicenseValidator.IsSupported(cue.LicenseSpdx) ||
                                 !IsSha256(cue.ContentSha256))) ||
                (!hasResource && !cue.AllowSubtitleFallback))
                diagnostics.Add(new(VoiceInvalidCode,
                    "Voice locale, take, lipsync, duration, licensed resource, hash, or subtitle fallback is invalid.", path));
        }

        foreach (var shot in profile.CameraShots)
        {
            var path = $"{root}/cameraShots/{shot.Id}";
            AddUuid(shot.Id, path, uuids, diagnostics);
            CheckNode(shot.NodeId, nodes, path, diagnostics);
            if (shot.Mode == DialogueCameraShotMode.Anchor &&
                (shot.CameraAnchorId is null || cameraAnchorIds is not null && !cameraAnchorIds.Contains(shot.CameraAnchorId.Value)))
                diagnostics.Add(new("dialogue.camera.anchor_missing",
                    "Anchored conversation shots require an existing camera anchor.", $"{path}/cameraAnchorId"));
            if (shot.Mode != DialogueCameraShotMode.Anchor && shot.CameraAnchorId is not null ||
                !float.IsFinite(shot.FieldOfViewDegrees) || shot.FieldOfViewDegrees is < 1 or > 179 ||
                shot.DurationMilliseconds is < 1 or > 3_600_000 || shot.BlendMilliseconds is < 0 or > 60_000 ||
                !float.IsFinite(shot.SafeAreaMargin) || shot.SafeAreaMargin is < 0 or > .45f ||
                shot.Priority is < -1000 or > 1000 || string.IsNullOrWhiteSpace(shot.Provenance))
                diagnostics.Add(new("dialogue.camera.invalid",
                    "Conversation camera mode, timing, framing, safe area, priority, or provenance is invalid.", path));
        }

        foreach (var bookmark in profile.Bookmarks)
        {
            var path = $"{root}/bookmarks/{bookmark.Id}";
            AddUuid(bookmark.Id, path, uuids, diagnostics);
            CheckNode(bookmark.NodeId, nodes, path, diagnostics);
            var expected = $"dialogue://{dialogue.Id}/{bookmark.NodeId}";
            if (string.IsNullOrWhiteSpace(bookmark.Name) || string.IsNullOrWhiteSpace(bookmark.AuthorId) ||
                string.IsNullOrWhiteSpace(bookmark.Provenance) ||
                !string.Equals(bookmark.DeepLink, expected, StringComparison.Ordinal) ||
                bookmark.Scope == DialogueBookmarkScope.Shared && string.IsNullOrWhiteSpace(bookmark.ReviewComment))
                diagnostics.Add(new("dialogue.bookmark.invalid",
                    "Bookmark name, author, shared review comment, deep link, or provenance is invalid.", path));
        }

        foreach (var rule in profile.InterjectionRules)
        {
            var path = $"{root}/interjectionRules/{rule.Id}";
            AddUuid(rule.Id, path, uuids, diagnostics);
            CheckNode(rule.TriggerNodeId, nodes, path, diagnostics);
            CheckNode(rule.InterjectionNodeId, nodes, path, diagnostics);
            if (!double.IsFinite(rule.MinimumRelation) || rule.MinimumRelation is < -100 or > 100 ||
                rule.Priority is < -1000 or > 1000 || rule.CooldownSeconds is < 0 or > 604_800 ||
                string.IsNullOrWhiteSpace(rule.ConflictGroup) || string.IsNullOrWhiteSpace(rule.Provenance) ||
                rule.PersonalityTags.Any(string.IsNullOrWhiteSpace))
                diagnostics.Add(new("dialogue.interjection.invalid",
                    "Interjection personality, relation, priority, cooldown, conflict, or provenance is invalid.", path));
        }

        foreach (var group in profile.TimedChoices.GroupBy(item => item.NodeId).Where(item => item.Count() > 1))
            diagnostics.Add(new("dialogue.timed_choice.node_duplicate",
                "A dialogue node can define only one timed-choice rule.", $"{root}/timedChoices/{group.Key}"));
        foreach (var rule in profile.TimedChoices)
        {
            var path = $"{root}/timedChoices/{rule.Id}";
            AddUuid(rule.Id, path, uuids, diagnostics);
            CheckNode(rule.NodeId, nodes, path, diagnostics);
            var node = dialogue.Nodes.SingleOrDefault(item => item.Id == rule.NodeId);
            if (node is not null && node.Choices.All(item => item.Id != rule.DefaultChoiceId))
                diagnostics.Add(new("dialogue.timed_choice.default_missing",
                    "Timed-choice default must reference a choice on the same node.", $"{path}/defaultChoiceId"));
            if (rule.DurationMilliseconds is < 500 or > 300_000 || rule.ServerRevision < 0 ||
                rule.AuthorityScope == DialogueRuntimeAuthorityScope.Server && rule.ServerRevision == 0 ||
                string.IsNullOrWhiteSpace(rule.AuditActorId) || string.IsNullOrWhiteSpace(rule.Provenance) ||
                rule.TimeoutScriptId is not null && !IsSafeIdentifier(rule.TimeoutScriptId))
                diagnostics.Add(new("dialogue.timed_choice.invalid",
                    "Timed-choice duration, authority revision, timeout script, audit identity, or provenance is invalid.", path));
        }

        foreach (var group in profile.VariantSets.GroupBy(item => item.NodeId).Where(item => item.Count() > 1))
            diagnostics.Add(new("dialogue.variant.node_duplicate",
                "A dialogue node can define only one procedural variant set.", $"{root}/variantSets/{group.Key}"));
        foreach (var set in profile.VariantSets)
        {
            var path = $"{root}/variantSets/{set.Id}";
            AddUuid(set.Id, path, uuids, diagnostics);
            CheckNode(set.NodeId, nodes, path, diagnostics);
            if (string.IsNullOrWhiteSpace(set.Key) || set.SeedOffset is < -1_000_000 or > 1_000_000 ||
                set.AntiRepeatWindow is < 0 or > 16 || set.Variants.Count is < 1 or > 64 ||
                string.IsNullOrWhiteSpace(set.Provenance))
                diagnostics.Add(new("dialogue.variant_set.invalid",
                    "Variant key, seed offset, anti-repeat window, variants, or provenance is invalid.", path));
            var localizationKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var variant in set.Variants)
            {
                var variantPath = $"{path}/variants/{variant.Id}";
                AddUuid(variant.Id, variantPath, uuids, diagnostics);
                if (variant.Weight is < 1 or > 10_000 || string.IsNullOrWhiteSpace(variant.Provenance) ||
                    !localizationKeys.Add(variant.Text.Key) || variant.Conditions.Any(item =>
                        string.IsNullOrWhiteSpace(item.Key) || item.ExpectedValue is null))
                    diagnostics.Add(new("dialogue.variant.invalid",
                        "Variant weight, localization key, typed conditions, or provenance is invalid.", variantPath));
            }
        }

        return diagnostics;
    }

    private static void CheckNode(DialogueNodeId id, IReadOnlySet<DialogueNodeId> nodes, string path,
        ICollection<DomainDiagnostic> diagnostics)
    {
        if (!nodes.Contains(id))
            diagnostics.Add(new("dialogue.presentation.node_missing",
                $"Presentation item references missing dialogue node '{id}'.", $"{path}/nodeId"));
    }

    private static void AddUuid(Guid id, string path, ISet<Guid> ids, ICollection<DomainDiagnostic> diagnostics)
    {
        if (id == Guid.Empty)
            diagnostics.Add(new("dialogue.presentation.uuid_empty", "Presentation UUID cannot be empty.", path));
        else if (!ids.Add(id))
            diagnostics.Add(new("dialogue.presentation.uuid_duplicate",
                "Presentation UUIDs must be unique within the dialogue.", path));
    }

    private static bool IsSafeResource(string value) => !Path.IsPathRooted(value) && !value.Contains("..", StringComparison.Ordinal) &&
        !value.Contains('\\') && !Uri.TryCreate(value, UriKind.Absolute, out _);

    private static bool IsSha256(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static bool IsSafeIdentifier(string value) => value.Length <= 160 &&
        value.All(character => char.IsLetterOrDigit(character) || character is '.' or '_' or '-' or '/' or ':');
}
