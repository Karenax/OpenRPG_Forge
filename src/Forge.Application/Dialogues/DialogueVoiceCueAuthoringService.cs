using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Dialogues;

public sealed record DialogueVoiceCueAuthoringRequest(
    DialogueId DialogueId,
    DialogueNodeId NodeId,
    BlueprintId ParticipantBlueprintId,
    string Resource,
    string Locale,
    string Take,
    string SubtitleKey,
    string Subtitle,
    string LipSyncProfileId,
    string Emotion,
    int DurationMilliseconds,
    string LicenseSpdx,
    string ContentSha256,
    bool AllowSubtitleFallback,
    string Provenance);

public sealed record DialogueVoiceCueAuthoringResult(
    DialogueDefinition? Dialogue,
    DialogueVoiceCue? Cue,
    IReadOnlyList<DomainDiagnostic> Diagnostics)
{
    public bool Succeeded => Dialogue is not null && Cue is not null && Diagnostics.Count == 0;
}

public static class DialogueVoiceCueAuthoringService
{
    public const string DialogueMissingCode = "dialogue.voice.dialogue_missing";
    public const string NodeMissingCode = "dialogue.voice.node_missing";
    public const string ParticipantMissingCode = "dialogue.voice.participant_missing";
    public const string ParticipantMismatchCode = "dialogue.voice.participant_mismatch";

    public static DialogueVoiceCueAuthoringResult Build(ProjectDefinition project, DialogueVoiceCueAuthoringRequest request)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(request);
        var dialogue = project.Dialogues.SingleOrDefault(item => item.Id == request.DialogueId);
        if (dialogue is null) return Failure(DialogueMissingCode, "Dialogue does not exist.", "dialogueId");
        var node = dialogue.Nodes.SingleOrDefault(item => item.Id == request.NodeId);
        if (node is null) return Failure(NodeMissingCode, "Dialogue node does not exist.", "nodeId");
        if (project.Blueprints.All(item => item.Id != request.ParticipantBlueprintId))
            return Failure(ParticipantMissingCode, "Dialogue participant blueprint does not exist.", "participantBlueprintId");
        if (node.SpeakerBlueprintId != request.ParticipantBlueprintId)
            return Failure(ParticipantMismatchCode, "Dialogue participant must match the authored node speaker.",
                "participantBlueprintId");

        var profile = dialogue.Presentation ?? new DialoguePresentationProfile(
            StableGuid($"{dialogue.Id}:presentation"), 1, request.Provenance, false, true, [], [], [], [], []);
        var existing = profile.VoiceCues.SingleOrDefault(item => item.NodeId == request.NodeId &&
            item.ParticipantBlueprintId == request.ParticipantBlueprintId &&
            string.Equals(item.Locale, request.Locale, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Take, request.Take, StringComparison.Ordinal));
        var cue = new DialogueVoiceCue(
            existing?.Id ?? StableGuid($"{dialogue.Id}:{request.NodeId}:{request.ParticipantBlueprintId}:{request.Locale}:{request.Take}"),
            request.NodeId, request.ParticipantBlueprintId, request.Locale, request.Resource, request.Take,
            new LocalizedText(request.SubtitleKey, request.Subtitle, provenance: request.Provenance),
            request.LipSyncProfileId, request.Emotion, request.DurationMilliseconds, request.LicenseSpdx,
            request.ContentSha256, request.AllowSubtitleFallback, request.Provenance);
        var voices = profile.VoiceCues.Where(item => item.Id != cue.Id).Append(cue)
            .OrderBy(item => item.NodeId.ToString(), StringComparer.Ordinal)
            .ThenBy(item => item.Locale, StringComparer.Ordinal)
            .ThenBy(item => item.Take, StringComparer.Ordinal)
            .ToArray();
        var updatedProfile = new DialoguePresentationProfile(profile.Id, profile.SchemaVersion, profile.Provenance,
            profile.RequiredAtRuntime, profile.ServerAuthoritativeInterjections, profile.AnimationCues, voices,
            profile.CameraShots, profile.Bookmarks, profile.InterjectionRules, profile.TimedChoices, profile.VariantSets);
        var updatedDialogue = dialogue.WithPresentation(updatedProfile);
        var diagnostics = DialoguePresentationValidator.Validate(updatedDialogue);
        return diagnostics.Count == 0
            ? new(updatedDialogue, cue, [])
            : new(null, null, diagnostics);
    }

    public static string DestinationRelativePath(DialogueVoiceCueAuthoringRequest request, string extension)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ContentSha256 is not { Length: 64 } || !request.ContentSha256.All(Uri.IsHexDigit))
            throw new ArgumentException("Dialogue voice SHA-256 must contain exactly 64 hexadecimal characters.",
                nameof(request));
        var hash = request.ContentSha256.ToLowerInvariant();
        return $"audio/dialogues/blobs/sha256/{hash[..2]}/{hash}{extension.ToLowerInvariant()}";
    }

    private static DialogueVoiceCueAuthoringResult Failure(string code, string message, string path) =>
        new(null, null, [new DomainDiagnostic(code, message, path)]);

    private static Guid StableGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guid);
        guid[6] = (byte)((guid[6] & 0x0f) | 0x40);
        guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
        return new Guid(guid);
    }
}
