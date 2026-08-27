using System.Security.Cryptography;
using Forge.Application.Gameplay;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;

namespace Forge.ModuleFormat.Tests.RuntimePresentation;

public sealed class DialogueVoiceResourceVerifierTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "openrpg-voice-verify", Guid.NewGuid().ToString("N"));

    [Fact]
    public void VerifiesContainedBytesAndReturnsStableFallbackDiagnostics()
    {
        var relative = "audio/dialogues/test/node/speaker/take.mp3";
        var path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes = FakeMp3("verified voice");
        File.WriteAllBytes(path, bytes);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var cue = Cue(relative, hash, fallback: true);

        var valid = DialogueVoiceResourceVerifier.Verify(_root, cue);
        var changed = DialogueVoiceResourceVerifier.Verify(_root, cue with { ContentSha256 = new string('a', 64) });
        var missing = DialogueVoiceResourceVerifier.Verify(_root, cue with { Resource = "audio/dialogues/missing.mp3" });

        Assert.True(valid.Succeeded);
        Assert.Equal(bytes, valid.VerifiedBytes);
        Assert.Equal(".mp3", valid.Extension);
        Assert.Equal(DialogueVoiceResourceVerifier.HashMismatchCode, changed.DiagnosticCode);
        Assert.True(changed.UseSubtitleFallback);
        Assert.Equal(DialogueVoiceResourceVerifier.MissingCode, missing.DiagnosticCode);
        Assert.DoesNotContain(_root, changed.DiagnosticCode ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsTruncatedMediaAfterHashVerificationAndKeepsSubtitleFallback()
    {
        var relative = "audio/dialogues/test/node/speaker/truncated.mp3";
        var path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes = new byte[] { (byte)'I', (byte)'D', (byte)'3', 0x04, 0, 0, 0, 0, 1, 0 };
        File.WriteAllBytes(path, bytes);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        var result = DialogueVoiceResourceVerifier.Verify(_root, Cue(relative, hash, fallback: true));

        Assert.False(result.Succeeded);
        Assert.Null(result.VerifiedBytes);
        Assert.Equal(DialogueVoiceResourceVerifier.StructureInvalidCode, result.DiagnosticCode);
        Assert.True(result.UseSubtitleFallback);
    }

    private static DialogueVoiceCue Cue(string resource, string hash, bool fallback) => new(
        Guid.NewGuid(), DialogueNodeId.From(Guid.NewGuid()), BlueprintId.From(Guid.NewGuid()), "fr-FR",
        resource, "take-01", new("voice.test", "Test"), "neutral", "neutral", 1000,
        "CC0-1.0", hash, fallback, "test:voice");

    private static byte[] FakeMp3(string payload)
    {
        var frame = new byte[417];
        frame[0] = 0xff;
        frame[1] = 0xfb;
        frame[2] = 0x90;
        frame[3] = 0x00;
        var text = System.Text.Encoding.UTF8.GetBytes(payload);
        text.AsSpan(0, Math.Min(text.Length, frame.Length - 4)).CopyTo(frame.AsSpan(4));
        return frame;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
