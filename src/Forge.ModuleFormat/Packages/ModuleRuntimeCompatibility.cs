using System.Text.RegularExpressions;

namespace Forge.ModuleFormat.Packages;

public static partial class ModuleRuntimeCompatibility
{
    public const string CurrentRuntimeVersion = "0.6.34";
    public const string IncompatibleRuntimeCode = "module.runtime.minimum_version_unsupported";
    public const string DiagnosticPath = "manifest.json/runtimeMinimumVersion";

    public static ModulePackageDiagnostic? Evaluate(ModuleManifest manifest, string availableRuntimeVersion)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(availableRuntimeVersion);
        if (!TryParse(manifest.RuntimeMinimumVersion, out var required) ||
            !TryParse(availableRuntimeVersion, out var available) ||
            !manifest.RuntimeCompatibilityRequired || available.CompareTo(required) >= 0)
            return null;
        return new(IncompatibleRuntimeCode,
            $"Module requires runtime {manifest.RuntimeMinimumVersion} or newer; available runtime is {availableRuntimeVersion}.",
            DiagnosticPath);
    }

    private static bool TryParse(string value, out Version version)
    {
        var match = SemanticVersionRegex().Match(value ?? string.Empty);
        if (!match.Success)
        {
            version = new Version();
            return false;
        }
        version = new Version(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value),
            int.Parse(match.Groups[3].Value));
        return true;
    }

    [GeneratedRegex(@"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:[-+].*)?$")]
    private static partial Regex SemanticVersionRegex();
}
