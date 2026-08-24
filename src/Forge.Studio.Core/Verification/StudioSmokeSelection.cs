using System.Text.RegularExpressions;

namespace Forge.Studio.Core.Verification;

public enum StudioSmokeScope
{
    Route,
    Wave,
    Full,
}

public sealed record StudioSmokeSelection(
    StudioSmokeScope Scope,
    string Route,
    string Wave)
{
    private static readonly HashSet<string> KnownRoutes = BuildKnownRoutes();

    private static readonly Regex StandardRoute = new(
        "^ux(?<wave>0|1|3|4|6|7|8|9)-(?<route>[a-z]{1,2})(?:-|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Ux2Route = new(
        "^ux2-(?:(?<family>lw|env|nav)-)?(?<route>[a-z])(?:-|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static StudioSmokeSelection Full { get; } = new(StudioSmokeScope.Full, "all", "all");

    public static int KnownRouteCount => KnownRoutes.Count;

    public bool Includes(string route)
    {
        if (Scope == StudioSmokeScope.Full)
        {
            return true;
        }

        var canonical = CanonicalizeRoute(route);
        if (canonical == "edit")
        {
            return true;
        }

        return Scope switch
        {
            StudioSmokeScope.Route => string.Equals(canonical, Route, StringComparison.Ordinal),
            StudioSmokeScope.Wave => string.Equals(WaveFor(canonical), Wave, StringComparison.Ordinal),
            _ => false,
        };
    }

    public static bool TryCreate(
        string? scope,
        string? evidence,
        string? page,
        out StudioSmokeSelection selection,
        out string error)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            selection = Full;
            error = string.Empty;
            return true;
        }

        if (!Enum.TryParse<StudioSmokeScope>(scope, ignoreCase: true, out var parsedScope))
        {
            selection = Full;
            error = $"unknown smoke scope '{scope}'";
            return false;
        }

        if (parsedScope == StudioSmokeScope.Full)
        {
            selection = Full;
            error = string.Empty;
            return true;
        }

        var requested = !string.IsNullOrWhiteSpace(evidence) ? evidence : page;
        var route = CanonicalizeRoute(requested);
        if (route.Length == 0)
        {
            selection = Full;
            error = $"unknown Studio smoke route '{requested}'";
            return false;
        }

        selection = new StudioSmokeSelection(parsedScope, route, WaveFor(route));
        error = string.Empty;
        return true;
    }

    public static string CanonicalizeRoute(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "edit";
        }

        var normalized = value.Trim().ToLowerInvariant().Replace('_', '-');
        if (!normalized.StartsWith("ux", StringComparison.Ordinal))
        {
            return "edit";
        }

        if (normalized.StartsWith("ux10-", StringComparison.Ordinal) || normalized == "ux10")
        {
            return "ux10";
        }

        if (normalized.StartsWith("ux5-", StringComparison.Ordinal))
        {
            return normalized.StartsWith("ux5-a", StringComparison.Ordinal) ? "ux5-a" : "ux5-b";
        }

        var ux2Alias = normalized switch
        {
            "ux2-area-034-lod" => "ux2-env-j",
            "ux2-area-033-visibility" => "ux2-nav-j",
            "ux2-wpt-010" => "ux2-nav-k",
            "ux2-wpt-012" => "ux2-nav-l",
            "ux2-wpt-013" => "ux2-nav-m",
            "ux2-encounter-008-010-014" => "ux2-lw-i",
            "ux2-encounter-016-017-019" => "ux2-lw-j",
            "ux2-encounter-018" => "ux2-lw-k",
            "ux2-encounter-020" => "ux2-lw-l",
            _ => string.Empty,
        };
        if (ux2Alias.Length > 0)
        {
            return KnownRoutes.Contains(ux2Alias) ? ux2Alias : string.Empty;
        }

        var ux2 = Ux2Route.Match(normalized);
        if (ux2.Success)
        {
            var family = ux2.Groups["family"].Value;
            var route = ux2.Groups["route"].Value;
            var candidate = family.Length == 0 ? $"ux2-{route}" : $"ux2-{family}-{route}";
            return KnownRoutes.Contains(candidate) ? candidate : string.Empty;
        }

        var standard = StandardRoute.Match(normalized);
        if (standard.Success)
        {
            var candidate = $"ux{standard.Groups["wave"].Value}-{standard.Groups["route"].Value}";
            return KnownRoutes.Contains(candidate) ? candidate : string.Empty;
        }

        return string.Empty;
    }

    private static string WaveFor(string route)
    {
        if (route == "edit")
        {
            return "edit";
        }

        var separator = route.IndexOf('-', StringComparison.Ordinal);
        return separator > 0 ? route[..separator] : route;
    }

    private static HashSet<string> BuildKnownRoutes()
    {
        var routes = new HashSet<string>(StringComparer.Ordinal) { "edit", "ux10" };
        AddLetterRoutes(routes, "ux0", 'a', 'j');
        AddLetterRoutes(routes, "ux1", 'a', 'z');
        foreach (var suffix in new[] { "aa", "ab", "ac", "ad", "ae", "af", "ag", "ah", "ai", "aj" })
        {
            routes.Add($"ux1-{suffix}");
        }
        foreach (var suffix in new[] { "a", "b", "c" })
        {
            routes.Add($"ux2-{suffix}");
        }
        AddLetterRoutes(routes, "ux2-lw", 'b', 'l');
        AddLetterRoutes(routes, "ux2-env", 'b', 'k');
        AddLetterRoutes(routes, "ux2-nav", 'b', 'm');
        AddLetterRoutes(routes, "ux3", 'a', 'i');
        AddLetterRoutes(routes, "ux4", 'a', 'u');
        AddLetterRoutes(routes, "ux5", 'a', 'b');
        AddLetterRoutes(routes, "ux6", 'a', 'z');
        AddLetterRoutes(routes, "ux7", 'a', 'i');
        AddLetterRoutes(routes, "ux8", 'a', 'n');
        AddLetterRoutes(routes, "ux9", 'a', 'o');
        return routes;
    }

    private static void AddLetterRoutes(HashSet<string> routes, string prefix, char first, char last)
    {
        for (var suffix = first; suffix <= last; suffix++)
        {
            routes.Add($"{prefix}-{suffix}");
        }
    }
}
