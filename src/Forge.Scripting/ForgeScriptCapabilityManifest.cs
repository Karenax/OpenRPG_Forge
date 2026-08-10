using System.Text;
using System.Text.RegularExpressions;

namespace Forge.Scripting;

public sealed record ForgeScriptCapability(
    string Id,
    string DisplayName,
    IReadOnlyList<string> ApiNames,
    string Authority,
    string Quota);

public static partial class ForgeScriptCapabilityManifest
{
    public const int SchemaVersion = 1;
    public const string ApiVersion = "1.0.0";
    public const int MaximumQueryResults = 128;
    public const int MaximumStringBytes = 16_384;
    public const int MaximumRegexPatternBytes = 256;
    public const int MaximumRegexInputBytes = 4_096;

    public static IReadOnlyList<ForgeScriptCapability> Capabilities { get; } =
    [
        new("world", "Zones et objets",
            ["current_area", "find_entity", "entity_position", "spawn_at", "destroy_entity"],
            "server-read/write", $"query<={MaximumQueryResults}"),
        new("movement", "Mouvement",
            ["move_to", "face_towards", "follow", "teleport", "set_velocity", "cancel_movement"],
            "server-write", "task and spawn quotas"),
        new("utilities", "Math et chaînes",
            ["vec3", "vec_length", "vec_dot", "vec_normalize", "quat", "quat_normalize",
                "format_int", "format_float", "unicode_length", "regex_match"],
            "pure", $"string<={MaximumStringBytes};regex={MaximumRegexPatternBytes}/{MaximumRegexInputBytes}"),
        new("blackboard", "Variables locales typées",
            ["local_set_int", "local_get_int", "local_set_float", "local_get_float", "local_set_string",
                "local_get_string", "local_set_entity", "local_get_entity", "local_set_vector",
                "local_get_vector"],
            "server-write/read", $"string<={MaximumStringBytes}"),
        new("opaque-handles", "Handles opaques typés",
            ["resolve_asset", "create_effect", "release_effect"],
            "server-read/write", "kind+generation validated on every call"),
        new("determinism", "Aléatoire et horloge de simulation",
            ["random_int", "simulation_tick", "simulation_time_ms"],
            "server-authoritative", "seeded RNG and monotonic simulation clock"),
    ];

    public static IReadOnlyList<string> ExplicitlyForbiddenCapabilities { get; } =
    [
        "filesystem",
        "network",
        "process",
        "native-pointer",
        "reflection",
        "godot",
        "dotnet",
    ];

    public static IReadOnlyList<ForgeScriptSourceDiagnostic> ValidateSource(string source, Guid? subjectId = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var diagnostics = new List<ForgeScriptSourceDiagnostic>();
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            foreach (Match match in ForbiddenApiPattern().Matches(lines[lineIndex]))
            {
                diagnostics.Add(new("script.sandbox.capability_forbidden",
                    $"script/source:{lineIndex + 1}:{match.Index + 1}", subjectId, "error",
                    $"Capability '{match.Value.Trim()}' is outside the ForgeScript allowlist."));
            }

            if (Encoding.UTF8.GetByteCount(lines[lineIndex]) > MaximumStringBytes)
            {
                diagnostics.Add(new("script.sandbox.string_quota_exceeded",
                    $"script/source:{lineIndex + 1}:1", subjectId, "error",
                    $"A source line exceeds the {MaximumStringBytes}-byte string safety quota."));
            }
        }

        return diagnostics;
    }

    public static string Summary() =>
        $"allowlist={string.Join(',', Capabilities.Select(item => item.Id))};" +
        $"denied={string.Join(',', ExplicitlyForbiddenCapabilities)};" +
        $"query<={MaximumQueryResults};regex={MaximumRegexPatternBytes}/{MaximumRegexInputBytes}";

    [GeneratedRegex(
        @"\b(?:file(?:_open|_read|_write)?|directory|http(?:_client)?|socket|network|process|shell|dllimport|native_library|native_pointer|intptr|reflection|godot|dotnet|system\s*::)\s*(?:\(|\.|::)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForbiddenApiPattern();
}
