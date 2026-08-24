using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Localization;
using Forge.Application.Quality;
using Forge.Domain.Projects;
using Forge.Domain.Quality;
using Forge.Scripting;

namespace Forge.ModuleFormat.Quality;

public sealed record WorkspaceQualityLabOptions(
    int MaximumLocalizationCharacters,
    double LocalizationExpansionFactor,
    int MaximumScripts,
    ForgeScriptQuotas ScriptQuotas,
    bool WritePseudoLocalizationSnapshot)
{
    public static WorkspaceQualityLabOptions Default { get; } = new(96, 1.35, 256,
        new ForgeScriptQuotas(25_000, 250, 32, 32), true);
}

public sealed record WorkspaceQualityLabReport(
    bool Passed,
    string Fingerprint,
    LocalizationQualityReport Localization,
    IReadOnlyList<DialogueQualityReport> Dialogues,
    IReadOnlyList<QuestQualityReport> Quests,
    EncounterQualityReport Encounters,
    ForgeScriptSandboxSuiteReport Scripts,
    string? PseudoLocalizationSnapshotPath,
    WorkspaceCanonicalValidationReport CanonicalValidation);

public sealed class WorkspaceQualityLabService(IForgeScriptRuntime scriptRuntime)
{
    public const string PseudoSnapshotRelativePath = ".forge/quality/pseudo-localization.svg";
    private static readonly IReadOnlySet<string> AllowedScriptEvents = new HashSet<string>(
        ["health", "variable", "say", "cast", "spawn", "quest", "unlock", "world.area", "world.query"],
        StringComparer.Ordinal);
    private readonly IForgeScriptRuntime _scriptRuntime = scriptRuntime ?? throw new ArgumentNullException(nameof(scriptRuntime));

    public WorkspaceQualityLabReport Run(
        string workspaceRoot,
        ProjectDefinition project,
        WorkspaceQualityLabOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(project);
        options ??= WorkspaceQualityLabOptions.Default;
        if (options.MaximumScripts is < 0 or > 10_000) throw new ArgumentOutOfRangeException(nameof(options));
        var root = Path.GetFullPath(workspaceRoot);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);

        var extraction = LocalizationCatalogExtractor.Extract(project);
        var localization = LocalizationQualityService.Analyze(extraction.Catalog,
            options.MaximumLocalizationCharacters, options.LocalizationExpansionFactor);
        var dialogues = project.Dialogues.OrderBy(dialogue => dialogue.Slug, StringComparer.Ordinal)
            .Select(dialogue => NarrativeQualityService.TestDialogue(dialogue)).ToArray();
        var quests = project.Quests.OrderBy(quest => quest.Slug, StringComparer.Ordinal)
            .Select(quest => NarrativeQualityService.TestQuest(quest)).ToArray();
        var encounters = EncounterQualityService.Simulate(project);
        var scripts = RunScripts(root, options);
        var qualityProfile = QualityFoundationProfileStore.Load(root, project.Id.Value).Profile;
        var canonicalValidation = WorkspaceCanonicalValidationService.Run(root, project, qualityProfile);
        var canonicalBlocking = WorkspaceCanonicalValidationService.BlockingDiagnostics(
            project, qualityProfile, canonicalValidation).Count > 0;
        var pseudoPath = options.WritePseudoLocalizationSnapshot
            ? WritePseudoSnapshot(root, LocalizationQualityService.RenderPseudoSnapshotSvg(localization))
            : null;
        var passed = extraction.Diagnostics.Count == 0 && localization.Passed && dialogues.All(report => report.Passed) &&
            quests.All(report => report.Passed) && encounters.Passed && scripts.Passed && !canonicalBlocking;
        var fingerprint = Fingerprint(new
        {
            passed,
            localization = new { localization.Passed, issues = localization.Issues.Select(issue => issue.Code) },
            dialogues = dialogues.Select(report => new { id = report.DialogueId.ToString(), report.Passed, report.CoveredNodes, report.CoveredChoices }),
            quests = quests.Select(report => new { id = report.QuestId.ToString(), report.Passed, report.CoveredStates, report.CoveredTransitions }),
            encounters = encounters.Areas.Select(area => new { id = area.AreaId.ToString(), area.Passed, area.CreatureCount, runs = area.Runs.Count }),
            scripts = scripts.Tests.Select(test => new { test.Id, test.Passed, test.EventCoverage }),
            canonicalValidation = new
            {
                canonicalValidation.Passed,
                canonicalValidation.Fingerprint,
                canonicalValidation.CategoryCounts,
            },
        });
        return new(passed, fingerprint, localization, dialogues, quests, encounters, scripts, pseudoPath,
            canonicalValidation);
    }

    private ForgeScriptSandboxSuiteReport RunScripts(string root, WorkspaceQualityLabOptions options)
    {
        var scriptsRoot = Path.Combine(root, "scripts");
        if (!Directory.Exists(scriptsRoot)) return new(true, 0, 0, 1, []);
        var paths = Directory.EnumerateFiles(scriptsRoot, "*.forge", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase).Take(options.MaximumScripts + 1).ToArray();
        if (paths.Length > options.MaximumScripts)
            return new(false, 0, 0, 0, [new ForgeScriptSandboxTestResult("script-quota", false,
                ForgeScriptExecutionStatus.Failed, false, 0, [], [], ["script-count-quota"], [])]);
        var mock = new ForgeScriptMockWorld(
            new Dictionary<string, object?> { ["player"] = "mock:player", ["area"] = "mock:area" },
            new Dictionary<string, object?> { ["clock"] = "deterministic", ["random"] = "seeded" },
            AllowedScriptEvents);
        var tests = paths.Select(path =>
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            return new ForgeScriptSandboxTestCase(relative, relative, File.ReadAllText(path), options.ScriptQuotas,
                mock, new HashSet<string>(StringComparer.Ordinal));
        }).ToArray();
        return new ForgeScriptSandboxTestService(_scriptRuntime).RunSuite(tests);
    }

    private static string WritePseudoSnapshot(string root, string svg)
    {
        var output = Path.GetFullPath(Path.Combine(root, PseudoSnapshotRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!output.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Pseudo-localization snapshot path escapes the workspace.");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var temporary = output + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, svg, new UTF8Encoding(false));
            File.Move(temporary, output, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return PseudoSnapshotRelativePath;
    }

    private static string Fingerprint(object value) => Convert.ToHexStringLower(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })));
}
