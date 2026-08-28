using Forge.Domain.Areas;

namespace Forge.Runtime;

internal sealed record RuntimeModuleExperienceProfile(
    bool ConstrainCamera,
    bool CompleteOnBossExit,
    bool SystemicGameplayEnabled,
    bool EmberAtmosphereEnabled,
    string? ArtKitId)
{
    internal static RuntimeModuleExperienceProfile Default { get; } = new(true, false, false, false, null);

    internal static RuntimeModuleExperienceProfile From(AreaDefinition area)
    {
        ArgumentNullException.ThrowIfNull(area);
        var tags = area.Environment.Tags;
        return new RuntimeModuleExperienceProfile(
            !Has(tags, "camera:unbounded"),
            Has(tags, "completion:boss-exit"),
            Has(tags, "systemic:world-as-weapon"),
            Has(tags, "atmosphere:ember"),
            Value(tags, "art-kit:"));
    }

    private static bool Has(IReadOnlyList<string> tags, string value) =>
        tags.Contains(value, StringComparer.OrdinalIgnoreCase);

    private static string? Value(IReadOnlyList<string> tags, string prefix) =>
        tags.FirstOrDefault(tag => tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?[prefix.Length..];
}
