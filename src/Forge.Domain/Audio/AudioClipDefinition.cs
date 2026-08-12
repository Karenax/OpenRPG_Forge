namespace Forge.Domain.Audio;

public sealed record AudioClipDefinition(
    Guid Id,
    string ResourcePath,
    float Weight,
    IReadOnlyList<string> Tags,
    string Variant,
    string Locale,
    float LoudnessLufs,
    string PreviewResourcePath,
    string MimeType,
    long MemoryBytes,
    string LicenseSpdx,
    string Provenance,
    string ContentSha256,
    string Transcript = "",
    IReadOnlyList<float>? WaveformPeaks = null,
    float DurationSeconds = 0);
