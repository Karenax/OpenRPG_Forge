using System.Security.Cryptography;
using Forge.Domain.Dialogues;

namespace Forge.Application.Gameplay;

public sealed record DialogueVoiceResourceVerification(
    bool Succeeded,
    byte[]? VerifiedBytes,
    string? Extension,
    string? DiagnosticCode,
    bool UseSubtitleFallback);

public static class DialogueVoiceResourceVerifier
{
    public const string MissingCode = "dialogue.voice.runtime.resource_missing";
    public const string UnsafeCode = "dialogue.voice.runtime.resource_unsafe";
    public const string HashMismatchCode = "dialogue.voice.runtime.hash_mismatch";
    public const string HashInvalidCode = "dialogue.voice.runtime.hash_invalid";
    public const long MaximumBytes = 64L * 1024 * 1024;

    public static DialogueVoiceResourceVerification Verify(string? moduleContentRoot, DialogueVoiceCue cue)
    {
        ArgumentNullException.ThrowIfNull(cue);
        if (string.IsNullOrWhiteSpace(cue.Resource))
            return Failure(MissingCode, cue.AllowSubtitleFallback);
        if (cue.ContentSha256 is not { Length: 64 } || !cue.ContentSha256.All(Uri.IsHexDigit))
            return Failure(HashInvalidCode, cue.AllowSubtitleFallback);
        var candidate = RuntimeModuleAssetPathResolver.ResolveVoiceAudioDestination(moduleContentRoot, cue.Resource);
        if (candidate is null) return Failure(UnsafeCode, cue.AllowSubtitleFallback);
        if (!File.Exists(candidate)) return Failure(MissingCode, cue.AllowSubtitleFallback);
        var verified = RuntimeModuleAssetPathResolver.ReadVoiceAudio(moduleContentRoot, cue.Resource, MaximumBytes);
        if (verified is null) return Failure(UnsafeCode, cue.AllowSubtitleFallback);
        try
        {
            var actual = Convert.ToHexString(SHA256.HashData(verified.Bytes)).ToLowerInvariant();
            return string.Equals(actual, cue.ContentSha256, StringComparison.OrdinalIgnoreCase)
                ? new(true, verified.Bytes, verified.Extension, null, false)
                : Failure(HashMismatchCode, cue.AllowSubtitleFallback);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failure(MissingCode, cue.AllowSubtitleFallback);
        }
    }

    private static DialogueVoiceResourceVerification Failure(string code, bool fallback) =>
        new(false, null, null, code, fallback);
}
