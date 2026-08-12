using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Areas;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.Application.Areas;

public enum AudioCaptionDirection
{
    Unknown,
    Front,
    Right,
    Rear,
    Left,
}

public sealed record AudioCaptionPresentation(
    Guid Id,
    string Resource,
    string Text,
    string Locale,
    string CueKind,
    string Importance,
    bool ShowDirection,
    AudioCaptionDirection Direction,
    float DisplaySeconds,
    string AccessibleText);

public sealed record AudioCaptionRuntimeResolution(
    AudioCaptionPresentation? Presentation,
    IReadOnlyList<DomainDiagnostic> Diagnostics)
{
    public bool Available => Presentation is not null;
}

public static class AudioCaptionRuntimeService
{
    public const string RequiredMissingCode = "runtime.audio_caption.required_missing";

    public static AudioCaptionRuntimeResolution Resolve(AreaAudioProfile profile, string resource,
        string locale, string fallbackLocale, Vector3Value listenerPosition, Vector3Value listenerForward,
        Vector3Value sourcePosition, bool required = false)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var caption = profile.Orchestration.Captions.SingleOrDefault(item =>
            item.Resource.Equals(resource, StringComparison.Ordinal));
        if (caption is null)
            return required
                ? new(null, [new(RequiredMissingCode, $"Required caption for '{resource}' is missing.",
                    "environment/audio/orchestration/captions")])
                : new(null, []);
        var localization = caption.Localizations.FirstOrDefault(item =>
                               item.Locale.Equals(locale, StringComparison.OrdinalIgnoreCase)) ??
                           caption.Localizations.FirstOrDefault(item =>
                               item.Locale.Equals(fallbackLocale, StringComparison.OrdinalIgnoreCase)) ??
                           caption.Localizations.First();
        var direction = caption.ShowDirection
            ? ResolveDirection(listenerPosition, listenerForward, sourcePosition)
            : AudioCaptionDirection.Unknown;
        var text = localization.Text.Trim();
        var readingSeconds = Math.Max(.25f, text.Length / 15f);
        var seconds = Math.Clamp(readingSeconds, caption.MinimumDisplaySeconds,
            caption.MaximumDisplaySeconds);
        var id = caption.Id == Guid.Empty ? StableId(resource) : caption.Id;
        var directionText = direction == AudioCaptionDirection.Unknown
            ? string.Empty
            : $"{direction.ToString().ToLowerInvariant()}: ";
        return new(new(id, caption.Resource, text, localization.Locale, caption.CueKind,
            caption.Importance, caption.ShowDirection, direction, seconds,
            $"{directionText}{text}"), []);
    }

    private static AudioCaptionDirection ResolveDirection(Vector3Value listenerPosition,
        Vector3Value listenerForward, Vector3Value sourcePosition)
    {
        var dxRaw = sourcePosition.X - listenerPosition.X;
        var dzRaw = sourcePosition.Z - listenerPosition.Z;
        var horizontalLength = MathF.Sqrt(dxRaw * dxRaw + dzRaw * dzRaw);
        var forwardLength = MathF.Sqrt(listenerForward.X * listenerForward.X +
                                       listenerForward.Z * listenerForward.Z);
        if (horizontalLength <= .0001f || forwardLength <= .0001f) return AudioCaptionDirection.Front;
        var dx = dxRaw / horizontalLength;
        var dz = dzRaw / horizontalLength;
        var fx = listenerForward.X / forwardLength;
        var fz = listenerForward.Z / forwardLength;
        var dot = dx * fx + dz * fz;
        var cross = fx * dz - fz * dx;
        if (dot >= .5f) return AudioCaptionDirection.Front;
        if (dot <= -.5f) return AudioCaptionDirection.Rear;
        return cross <= 0 ? AudioCaptionDirection.Left : AudioCaptionDirection.Right;
    }

    private static Guid StableId(string resource)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:audio-caption:{resource}"));
        return new(bytes[..16]);
    }
}
