namespace Forge.Domain.Creatures;

public static class CreaturePresentationRuntimeResolver
{
    public static Resolution Resolve(CreaturePresentationProfile? profile, bool required)
    {
        if (profile is null)
        {
            return new Resolution(null, UseModelFallback: true,
                required
                    ? [new CreaturePresentationDiagnostic("creature.presentation.required_missing", "creaturePresentation", "A required creature presentation is missing.")]
                    : []);
        }

        var diagnostics = CreaturePresentationValidator.Validate(profile);
        return new Resolution(diagnostics.Count == 0 ? profile : null, profile.UseModelFallback, diagnostics);
    }

    public sealed record Resolution(
        CreaturePresentationProfile? Profile,
        bool UseModelFallback,
        IReadOnlyList<CreaturePresentationDiagnostic> Diagnostics)
    {
        public bool Succeeded => Diagnostics.Count == 0;
    }
}
