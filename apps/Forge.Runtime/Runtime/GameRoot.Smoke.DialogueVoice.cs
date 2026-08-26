using System.Security.Cryptography;
using Forge.Domain.Dialogues;
using Godot;

namespace Forge.Runtime;

public partial class GameRoot
{
    private async Task RunDialogueVoiceCompletionSmokeTestAsync()
    {
        var project = _loadedProject ??
            throw new InvalidOperationException("A project must be loaded before the dialogue voice completion smoke.");
        var player = _dialogueVoicePlayer ??
            throw new InvalidOperationException("Dialogue voice player was not created.");
        DialogueDefinition? selectedDialogue = null;
        DialogueVoiceCue? selectedCue = null;
        Node3D? selectedParticipantNode = null;
        (Forge.Domain.Blueprints.BlueprintId BlueprintId, string DisplayName) selectedParticipant = default;

        foreach (var dialogue in project.Dialogues)
        {
            var cue = dialogue.Presentation?.VoiceCues.FirstOrDefault(item => item.NodeId == dialogue.EntryNodeId);
            if (cue is null ||
                !Forge.Application.Gameplay.DialogueVoiceResourceVerifier.Verify(_moduleContentRoot, cue).Succeeded)
                continue;
            var participant = _narrativeParticipants.FirstOrDefault(item =>
                item.Value.BlueprintId == cue.ParticipantBlueprintId);
            if (participant.Key is null) continue;
            selectedDialogue = dialogue;
            selectedCue = cue;
            selectedParticipantNode = participant.Key;
            selectedParticipant = participant.Value;
            break;
        }

        AssertSmoke(selectedDialogue is not null && selectedCue is not null && selectedParticipantNode is not null,
            "No reconstructed authored dialogue entry has a verified voice cue.");
        var completedBefore = player.CompletedCount;
        var fallbackBefore = player.FallbackCount;
        AssertSmoke(TryActivateNarrativeDialogue(selectedParticipantNode!, selectedParticipant),
            "The verified dialogue voice participant could not open its authored dialogue.");
        AssertSmoke(_activeDialogue?.Id == selectedDialogue!.Id,
            "The interactive dialogue resolver selected a different dialogue than the verified voice cue.");
        InteractWithGuide();
        AssertSmoke(player.IsPlaying,
            "The verified dialogue voice did not start through the interactive dialogue surface.");

        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (player.CompletedCount == completedBefore && timeout.Elapsed < TimeSpan.FromSeconds(30))
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        AssertSmoke(!player.IsPlaying && player.CompletedCount == completedBefore + 1 &&
                    player.FallbackCount == fallbackBefore,
            "The interactive dialogue voice did not complete naturally without subtitle fallback.");
        GD.Print("OPENRPG_FORGE_RUNTIME_DIALOGUE_VOICE_COMPLETION_PASS " +
                 $"dialogue={selectedDialogue.Id} node={selectedCue!.NodeId} " +
                 $"sha256={selectedCue.ContentSha256} fallback=false completed=true");
        GetTree().Quit(0);
    }

    private void RunDialogueVoicePlaybackSmokeTest()
    {
        var player = _dialogueVoicePlayer ??
            throw new InvalidOperationException("Dialogue voice player was not created.");
        var root = Path.Combine(Path.GetTempPath(), "openrpg-runtime-voice", Guid.NewGuid().ToString("N"));
        var relative = "audio/dialogues/smoke/node/speaker/take.wav";
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes = BuildSmokeWave();
        File.WriteAllBytes(path, bytes);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var dialogueId = DialogueId.From(Guid.Parse("d0050000-0000-4000-8000-000000000281"));
        var nodeId = DialogueNodeId.From(Guid.Parse("d0050000-0000-4000-8000-000000000282"));
        var participant = Forge.Domain.Blueprints.BlueprintId.From(
            Guid.Parse("d0050000-0000-4000-8000-000000000283"));
        var cue = new DialogueVoiceCue(Guid.Parse("d0050000-0000-4000-8000-000000000284"), nodeId,
            participant, "fr-FR", relative, "take-01", new("smoke.voice", "Voix de test"),
            "neutral", "neutral", 1000, "CC0-1.0", hash, true, "runtime:voice-smoke");
        try
        {
            var first = player.Play(root, dialogueId, cue);
            AssertSmoke(first && player.IsPlaying && player.StartedCount >= 1,
                "A verified dialogue voice did not start real AudioStreamPlayer playback.");
            var second = player.Play(root, dialogueId, cue with { Take = "take-02" });
            AssertSmoke(second && player.IsPlaying && player.StartedCount >= 2 && player.InterruptedCount >= 1,
                "A following dialogue node did not interrupt and replace the previous voice.");
            player.Stop("dialogue-aborted");
            AssertSmoke(!player.IsPlaying && player.InterruptedCount >= 2,
                "Dialogue close or abort did not interrupt voice playback.");
            var missing = player.Play(root, dialogueId,
                cue with { Resource = "audio/dialogues/smoke/node/speaker/missing.wav" });
            AssertSmoke(!missing && player.FallbackCount >= 1,
                "An allowed missing dialogue voice did not preserve subtitle fallback.");
            GD.Print("OPENRPG_FORGE_RUNTIME_DIALOGUE_VOICE_PLAYBACK_PASS started=true replaced=true " +
                     "abort_interrupted=true fallback=true bus=voice-or-master");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static byte[] BuildSmokeWave()
    {
        var bytes = new byte[48];
        "RIFF"u8.CopyTo(bytes);
        BitConverter.GetBytes(40).CopyTo(bytes, 4);
        "WAVEfmt "u8.CopyTo(bytes.AsSpan(8));
        BitConverter.GetBytes(16).CopyTo(bytes, 16);
        BitConverter.GetBytes((short)1).CopyTo(bytes, 20);
        BitConverter.GetBytes((short)1).CopyTo(bytes, 22);
        BitConverter.GetBytes(8000).CopyTo(bytes, 24);
        BitConverter.GetBytes(16000).CopyTo(bytes, 28);
        BitConverter.GetBytes((short)2).CopyTo(bytes, 32);
        BitConverter.GetBytes((short)16).CopyTo(bytes, 34);
        "data"u8.CopyTo(bytes.AsSpan(36));
        BitConverter.GetBytes(4).CopyTo(bytes, 40);
        return bytes;
    }
}
