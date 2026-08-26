using Forge.Application.Gameplay;
using Forge.Domain.Dialogues;
using Godot;

namespace Forge.Runtime;

internal sealed partial class RuntimeDialogueVoicePlayer : Node
{
    private AudioStreamPlayer? _activePlayer;
    private DialogueId? _activeDialogueId;
    private DialogueNodeId? _activeNodeId;

    internal bool IsPlaying => _activePlayer?.Playing == true;
    internal int StartedCount { get; private set; }
    internal int CompletedCount { get; private set; }
    internal int InterruptedCount { get; private set; }
    internal int FallbackCount { get; private set; }

    internal bool Play(
        string? moduleContentRoot,
        DialogueId dialogueId,
        DialogueVoiceCue cue)
    {
        ArgumentNullException.ThrowIfNull(dialogueId);
        ArgumentNullException.ThrowIfNull(cue);
        Stop("replaced");
        var verified = DialogueVoiceResourceVerifier.Verify(moduleContentRoot, cue);
        if (!verified.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_DIALOGUE_VOICE_DIAGNOSTIC code={verified.DiagnosticCode} " +
                        $"dialogue={dialogueId} node={cue.NodeId}");
            if (verified.UseSubtitleFallback)
            {
                FallbackCount++;
                GD.Print($"OPENRPG_FORGE_DIALOGUE_VOICE event=fallback dialogue={dialogueId} node={cue.NodeId}");
            }
            return false;
        }
        var verifiedBytes = verified.VerifiedBytes!;
        var extension = verified.Extension!;
        AudioStream? stream = null;
        if (extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase))
            stream = AudioStreamMP3.LoadFromBuffer(verifiedBytes);
        else if (extension.Equals(".wav", StringComparison.OrdinalIgnoreCase))
            stream = AudioStreamWav.LoadFromBuffer(verifiedBytes, new Godot.Collections.Dictionary());
        if (stream is null)
        {
            GD.PrintErr($"OPENRPG_FORGE_DIALOGUE_VOICE_DIAGNOSTIC code=dialogue.voice.runtime.load_failed " +
                        $"dialogue={dialogueId} node={cue.NodeId}");
            if (cue.AllowSubtitleFallback)
            {
                FallbackCount++;
                GD.Print($"OPENRPG_FORGE_DIALOGUE_VOICE event=fallback dialogue={dialogueId} node={cue.NodeId}");
            }
            return false;
        }

        var player = new AudioStreamPlayer
        {
            Stream = stream,
            Bus = AudioServer.GetBusIndex("Voice") >= 0 ? "Voice" : "Master",
        };
        _activePlayer = player;
        _activeDialogueId = dialogueId;
        _activeNodeId = cue.NodeId;
        player.Finished += () => Complete(player, dialogueId, cue.NodeId);
        AddChild(player);
        player.Play();
        StartedCount++;
        GD.Print($"OPENRPG_FORGE_DIALOGUE_VOICE event=started dialogue={dialogueId} node={cue.NodeId}");
        return true;
    }

    internal void Stop(string reason)
    {
        if (_activePlayer is null) return;
        var player = _activePlayer;
        var dialogue = _activeDialogueId;
        var node = _activeNodeId;
        _activePlayer = null;
        _activeDialogueId = null;
        _activeNodeId = null;
        player.Stop();
        player.QueueFree();
        InterruptedCount++;
        GD.Print($"OPENRPG_FORGE_DIALOGUE_VOICE event=interrupted dialogue={dialogue} node={node} reason={reason}");
    }

    private void Complete(AudioStreamPlayer player, DialogueId dialogueId, DialogueNodeId nodeId)
    {
        if (!ReferenceEquals(_activePlayer, player)) return;
        _activePlayer = null;
        _activeDialogueId = null;
        _activeNodeId = null;
        player.QueueFree();
        CompletedCount++;
        GD.Print($"OPENRPG_FORGE_DIALOGUE_VOICE event=completed dialogue={dialogueId} node={nodeId}");
    }
}

