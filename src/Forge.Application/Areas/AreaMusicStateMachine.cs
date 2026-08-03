using Forge.Domain.Areas;
namespace Forge.Application.Areas;

public sealed record MusicTransition(MusicState From, MusicState To, string Track,
    IReadOnlyList<string> Stems, float FadeSeconds, float CooldownSeconds);
public static class AreaMusicStateMachine
{
    public static MusicTransition? Transition(AreaAudioOrchestration orchestration, MusicState from,
        MusicState to, float elapsedSinceLastTransition)
    {
        var target = orchestration.MusicStates.SingleOrDefault(item => item.State == to); if (target is null) return null;
        if (elapsedSinceLastTransition < target.CooldownSeconds) return null;
        return new(from, to, target.Track, target.Stems, target.TransitionSeconds, target.CooldownSeconds);
    }
    public static string? ResolveCaption(AreaAudioOrchestration orchestration, string resource, string locale,
        string fallbackLocale = "fr")
    {
        var caption = orchestration.Captions.SingleOrDefault(item => item.Resource == resource); if (caption is null) return null;
        return caption.Localizations.FirstOrDefault(item => item.Locale.Equals(locale, StringComparison.OrdinalIgnoreCase))?.Text ??
            caption.Localizations.FirstOrDefault(item => item.Locale.Equals(fallbackLocale, StringComparison.OrdinalIgnoreCase))?.Text ??
            caption.Localizations.FirstOrDefault()?.Text;
    }
}
