using Forge.Domain.Areas;
using Forge.Domain.Validation;

namespace Forge.Domain.WorldFeatures;

public sealed record CompanionValidationRuntimeResult(
    bool Available,
    bool OptionalFallback,
    bool DestinationAllowed,
    bool UseSafeFallback,
    string OwnershipKind,
    IReadOnlyList<string> Commands,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class CompanionValidationRuntimeResolver
{
    public static CompanionValidationRuntimeResult Resolve(
        CompanionWorldProfile profile,
        AreaId? destinationAreaId = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Validation is null)
        {
            IReadOnlyList<DomainDiagnostic> diagnostics = profile.ValidationRequiredAtRuntime
                ? new[] { new DomainDiagnostic(
                    "world.companion.validation_required",
                    "Required companion validation policy is missing at runtime.",
                    $"module/worldFeatures/companions/{profile.Id}/validation") }
                : [];
            return new(false, !profile.ValidationRequiredAtRuntime, false, false, "legacy", [], diagnostics);
        }

        var policy = profile.Validation;
        var navigation = policy.CrossZoneNavigation;
        var destinationAllowed = !navigation.Enabled || destinationAreaId is null ||
                                 navigation.AllowedAreaIds.Contains(destinationAreaId);
        return new(true, false, destinationAllowed,
            !destinationAllowed && navigation.SafeFallback,
            policy.Ownership.Kind,
            policy.CommandIds.Order(StringComparer.Ordinal).ToArray(),
            []);
    }
}
