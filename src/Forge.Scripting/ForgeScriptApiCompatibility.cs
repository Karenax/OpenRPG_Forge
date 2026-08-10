namespace Forge.Scripting;

public readonly record struct ForgeScriptSemanticVersion(int Major, int Minor, int Patch) : IComparable<ForgeScriptSemanticVersion>
{
    public static ForgeScriptSemanticVersion Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var parts = value.Split('.');
        var patch = 0;
        if (parts.Length is < 2 or > 3 || !int.TryParse(parts[0], out var major) ||
            !int.TryParse(parts[1], out var minor) || (parts.Length == 3 && !int.TryParse(parts[2], out patch)) ||
            major < 0 || minor < 0 || patch < 0)
            throw new FormatException($"ForgeScript API version '{value}' is not semantic.");
        return new(major, minor, patch);
    }
    public int CompareTo(ForgeScriptSemanticVersion other) =>
        Major != other.Major ? Major.CompareTo(other.Major) : Minor != other.Minor ? Minor.CompareTo(other.Minor) : Patch.CompareTo(other.Patch);
    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

public sealed record ForgeScriptApiDeprecation(string Symbol, string Since, string Replacement, string Removal);
public sealed record ForgeScriptApiMigration(string From, string To, IReadOnlyList<string> Steps);
public sealed record ForgeScriptApiNegotiation(bool Succeeded, string Requested, string Selected,
    IReadOnlyList<string> MissingCapabilities, IReadOnlyList<ForgeScriptApiDeprecation> Deprecations,
    string DiagnosticCode);

public static class ForgeScriptApiCompatibility
{
    public const string Current = "1.0.0";
    public static IReadOnlyList<string> SupportedVersions { get; } = ["1.0.0"];
    public static IReadOnlyList<ForgeScriptApiDeprecation> Deprecations { get; } =
    [new("variable", "1.0.0", "local_set_string", "2.0.0")];
    public static IReadOnlyList<ForgeScriptApiMigration> Migrations { get; } =
    [new("1.0", "1.0.0", ["No source rewrite required.", "Prefer typed local_set_* functions over variable()."])];

    public static bool IsSupported(string version)
    {
        try { return ForgeScriptSemanticVersion.Parse(version) is var parsed && parsed.Major == 1 && parsed.Minor == 0; }
        catch (FormatException) { return false; }
    }

    public static ForgeScriptApiNegotiation Negotiate(string requestedVersion,
        IReadOnlySet<string>? requiredCapabilities = null)
    {
        var available = ForgeScriptCapabilityManifest.Capabilities.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var missing = (requiredCapabilities ?? new HashSet<string>()).Where(item => !available.Contains(item))
            .Order(StringComparer.Ordinal).ToArray();
        if (!IsSupported(requestedVersion))
            return new(false, requestedVersion, string.Empty, missing, [], "script.api.version_incompatible");
        if (missing.Length > 0)
            return new(false, requestedVersion, Current, missing, [], "script.api.capability_missing");
        return new(true, requestedVersion, Current, [], Deprecations, "ok");
    }
}
