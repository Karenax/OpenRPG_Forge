namespace Forge.Domain.WorldFeatures;

public sealed record TriggerPortalTransitionResolution(
    bool Requested,
    bool Authorized,
    TriggerWorldProfile.PortalPolicy? Portal,
    IReadOnlyList<string> MissingPermissions,
    string DiagnosticCode,
    string Reason);

public static class TriggerPortalTransitionService
{
    public const string FoundationRequiredCode = "WORLD_TRIGGER_FOUNDATION_REQUIRED_MISSING";
    public const string PermissionDeniedCode = "WORLD_TRIGGER_PORTAL_PERMISSION_DENIED";

    public static TriggerPortalTransitionResolution Resolve(
        TriggerWorldProfile profile,
        WorldFeatureRuntimeResolver.TriggerEnterResult entered,
        IReadOnlySet<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(entered);
        ArgumentNullException.ThrowIfNull(permissions);

        if (profile.Foundation is null)
        {
            return new(false, false, null, [],
                profile.FoundationRequiredAtRuntime ? FoundationRequiredCode : string.Empty,
                profile.FoundationRequiredAtRuntime ? "foundation-required-missing" : "foundation-optional-fallback");
        }

        if (!entered.Fired || entered.Event is null)
            return new(false, false, null, [], string.Empty, "trigger-not-fired");

        var portal = profile.Foundation.Portal;
        if (portal is null)
            return new(false, false, null, [], string.Empty, "portal-optional-fallback");

        var missing = portal.Permissions
            .Where(permission => !permissions.Contains(permission))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (missing.Length > 0)
            return new(true, false, null, missing, PermissionDeniedCode, "permission-denied");

        return new(true, true, portal, [], string.Empty, "authorized");
    }
}
