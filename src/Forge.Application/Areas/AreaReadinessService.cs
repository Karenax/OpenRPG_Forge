using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Domain.Areas;
using Forge.Domain.Audio;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Areas;

public sealed record AreaReadinessDiagnostic(
    Guid Id,
    string Category,
    string Code,
    string Message,
    string Path,
    Guid SubjectId);

public sealed record AreaReadinessReport(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    AreaId AreaId,
    bool Passed,
    string Fingerprint,
    IReadOnlyList<string> Checks,
    IReadOnlyDictionary<string, int> CategoryCounts,
    IReadOnlyList<AreaReadinessDiagnostic> Diagnostics)
{
    public const int CurrentSchemaVersion = 1;
}

public static class AreaReadinessService
{
    public static readonly IReadOnlyList<string> RequiredChecks =
    [
        "tile-connections", "collisions", "surface-navigation", "transitions", "references",
        "player-spawns", "lighting", "audio", "budgets",
    ];

    public static AreaReadinessReport Run(ProjectDefinition project, AreaId areaId,
        AreaPerformanceBudget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(areaId);
        var area = project.Areas.SingleOrDefault(item => item.Id == areaId)
            ?? throw new ArgumentException("Area does not exist.", nameof(areaId));
        var entries = new List<(string Category, DomainDiagnostic Diagnostic)>();
        entries.AddRange(AreaFoundationValidator.Validate(area, area.Foundation).Select(item => ("tile-connections", item)));
        entries.AddRange(AreaPlaytestProfileValidator.Validate(project, area, area.Playtest).Select(item => ("player-spawns", item)));
        entries.AddRange(AreaGeometryValidator.Validate(project, area).Select(item => ("collisions", item)));
        entries.AddRange(AreaWorldValidator.Validate(area, area.World).Select(item => ("references", item)));
        entries.AddRange(AnchorCatalogValidator.Validate(project, area).Select(item => ("transitions", item)));
        entries.AddRange(area.Portals.SelectMany(item => PortalLinkValidator.Validate(area, item))
            .Select(item => ("transitions", item)));
        entries.AddRange(ProjectValidator.Validate(project).Where(item =>
                item.Path.StartsWith($"areas/{area.Id}/", StringComparison.Ordinal))
            .Select(item => (item.Code.StartsWith("area.portal.", StringComparison.Ordinal)
                ? "transitions" : "references", item)));
        try
        {
            entries.AddRange(AreaNavigationValidator.AnalyzeAll(project, area)
                .SelectMany(item => item.Diagnostics).Select(item =>
                    (item.Code == AreaNavigationValidator.MissingSpawnCode ? "player-spawns" : "surface-navigation", item)));
        }
        catch (InvalidOperationException exception)
        {
            entries.Add(("surface-navigation", new DomainDiagnostic("area.navigation.bake_too_dense",
                exception.Message, $"areas/{area.Id}/navigation/cellSize")));
        }
        entries.AddRange(AudioEmitterCatalogValidator.Validate(area.Environment.Audio.Emitters,
                area.Environment.Audio.Orchestration.MixBuses.Select(item => item.Id).ToHashSet(),
                path: $"areas/{area.Id}/environment/audio/emitters")
            .Select(item => ("audio", item)));

        var performance = AreaPerformanceAnalyzer.Analyze(project, area.Id, budget);
        entries.AddRange(performance.Diagnostics.Select((message, index) =>
            ("budgets", new DomainDiagnostic("area.performance.budget_exceeded", message,
                $"areas/{area.Id}/performance/{index}"))));
        if (area.Environment.Rendering.LightBudgets.Count == 0 ||
            !float.IsFinite(area.Environment.Lighting.AmbientIntensity) ||
            !float.IsFinite(area.Environment.Lighting.DirectionalIntensity))
            entries.Add(("lighting", new DomainDiagnostic("area.lighting.profile_invalid",
                "Area lighting and platform light budgets are required.", $"areas/{area.Id}/environment/lighting")));

        var diagnostics = entries.DistinctBy(item => (item.Category, item.Diagnostic.Code, item.Diagnostic.Path))
            .Select(item => new AreaReadinessDiagnostic(Stable($"{area.Id}:{item.Category}:{item.Diagnostic.Code}:{item.Diagnostic.Path}"),
                item.Category, item.Diagnostic.Code, item.Diagnostic.Message, item.Diagnostic.Path,
                Subject(item.Diagnostic.Path) ?? area.Id.Value))
            .OrderBy(item => item.Category, StringComparer.Ordinal).ThenBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.Path, StringComparer.Ordinal).ToArray();
        var counts = RequiredChecks.ToDictionary(check => check,
            check => diagnostics.Count(item => item.Category == check), StringComparer.Ordinal);
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            area = area.Id.ToString(),
            checks = RequiredChecks,
            diagnostics = diagnostics.Select(item => new { item.Category, item.Code, item.Path }),
        })));
        return new(Stable($"{area.Id}:{fingerprint}"), AreaReadinessReport.CurrentSchemaVersion,
            "derived:area-readiness-r029", area.Id, diagnostics.Length == 0, fingerprint,
            RequiredChecks, counts, diagnostics);
    }

    private static Guid? Subject(string path) => path.Split('/', StringSplitOptions.RemoveEmptyEntries)
        .Select(item => Guid.TryParse(item, out var id) ? id : (Guid?)null).FirstOrDefault(item => item is not null);

    private static Guid Stable(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:{value}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }
}
