using Forge.Domain.Dialogues;

namespace Forge.Application.Dialogues;

public sealed record DialoguePresentationHandle(Guid Id, int SchemaVersion, DialogueId DialogueId);

public sealed record DialoguePresentationScriptSnapshot(
    DialoguePresentationHandle Handle,
    string Provenance,
    bool RequiredAtRuntime,
    bool ServerAuthoritativeInterjections,
    int AnimationCueCount,
    int VoiceCueCount,
    int CameraShotCount,
    int BookmarkCount,
    int InterjectionRuleCount);

/// <summary>Typed, read-first ForgeScript boundary for dialogue presentation.</summary>
public sealed class DialoguePresentationScriptService
{
    private readonly DialogueDefinition _dialogue;

    public DialoguePresentationScriptService(DialogueDefinition dialogue)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        var diagnostics = DialoguePresentationValidator.Validate(dialogue);
        if (diagnostics.Count > 0)
            throw new ArgumentException(string.Join(';', diagnostics.Select(item => item.Code)), nameof(dialogue));
        if (dialogue.Presentation is null)
            throw new ArgumentException("DIALOGUE_PRESENTATION_PROFILE_MISSING", nameof(dialogue));
        _dialogue = dialogue;
    }

    public DialoguePresentationHandle Handle => new(_dialogue.Presentation!.Id,
        _dialogue.Presentation.SchemaVersion, _dialogue.Id);

    public DialoguePresentationScriptSnapshot Read(DialoguePresentationHandle handle)
    {
        var profile = EnsureHandle(handle);
        return new(handle, profile.Provenance, profile.RequiredAtRuntime,
            profile.ServerAuthoritativeInterjections, profile.AnimationCues.Count, profile.VoiceCues.Count,
            profile.CameraShots.Count, profile.Bookmarks.Count, profile.InterjectionRules.Count);
    }

    public DialoguePresentationResolution Resolve(
        DialoguePresentationHandle handle, DialogueNodeId nodeId, DialoguePresentationRuntimeContext context)
    {
        EnsureHandle(handle);
        return DialoguePresentationRuntimeResolver.Resolve(_dialogue, nodeId, context);
    }

    private DialoguePresentationProfile EnsureHandle(DialoguePresentationHandle handle)
    {
        var profile = _dialogue.Presentation!;
        if (handle.Id != profile.Id || handle.SchemaVersion != profile.SchemaVersion || handle.DialogueId != _dialogue.Id)
            throw new InvalidOperationException("DIALOGUE_PRESENTATION_HANDLE_STALE");
        return profile;
    }
}
