using System.Text;
using System.Text.Json;
using Forge.Domain.Quality;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Quality;

public static class QualityFoundationProfileStore
{
    public const string RelativePath = ".forge/quality/foundation.json";
    public const string ReadFailedCode = "qa.foundation.read_failed";
    public const string WriteFailedCode = "qa.foundation.write_failed";
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
    };

    public static QualityFoundationLoadResult Load(string workspaceRoot, Guid ownerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var path = Resolve(workspaceRoot);
        if (!File.Exists(path)) return new(QualityFoundationProfile.CreateDefault(ownerId), true, []);
        try
        {
            var profile = JsonSerializer.Deserialize<QualityFoundationProfile>(File.ReadAllText(path), Options)
                ?? throw new JsonException("Quality foundation profile is empty.");
            var diagnostics = QualityFoundationProfileValidator.Validate(profile);
            return diagnostics.Count == 0
                ? new(profile, false, [])
                : new(QualityFoundationProfile.CreateDefault(ownerId), false,
                    diagnostics.Select(item => new ProjectSessionDiagnostic(item.Code, item.Message, item.Path)).ToArray());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or
            ArgumentException or NotSupportedException)
        {
            return new(QualityFoundationProfile.CreateDefault(ownerId), false,
                [new ProjectSessionDiagnostic(ReadFailedCode, exception.Message, path)]);
        }
    }

    public static ProjectSessionSaveResult Save(string workspaceRoot, QualityFoundationProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = QualityFoundationProfileValidator.Validate(profile);
        if (diagnostics.Count > 0)
            return new(false, diagnostics.Select(item => new ProjectSessionDiagnostic(item.Code, item.Message, item.Path)).ToArray());
        var path = Resolve(workspaceRoot);
        try
        {
            var canonical = profile with
            {
                Licenses = profile.Licenses with
                {
                    ApprovedSpdxIdentifiers = Normalize(profile.Licenses.ApprovedSpdxIdentifiers),
                    RequiredReviewerRoles = Normalize(profile.Licenses.RequiredReviewerRoles),
                },
                Tests = profile.Tests with
                {
                    DomainAreas = Normalize(profile.Tests.DomainAreas),
                    HeadlessScenarios = Normalize(profile.Tests.HeadlessScenarios),
                },
                Logs = profile.Logs with { Categories = Normalize(profile.Logs.Categories) },
            };
            var directory = Path.GetDirectoryName(path) ?? throw new IOException("QA profile path has no parent.");
            Directory.CreateDirectory(directory);
            var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Path.GetRandomFileName()}.tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                           4096, FileOptions.WriteThrough))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
                {
                    writer.Write(JsonSerializer.Serialize(canonical, Options));
                    writer.Write('\n');
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }
                File.Move(temporary, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
            return new(true, []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or
            ArgumentException or NotSupportedException)
        {
            return new(false, [new ProjectSessionDiagnostic(WriteFailedCode, exception.Message, path)]);
        }
    }

    private static IReadOnlyList<string> Normalize(IReadOnlyList<string> values) => values
        .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim().ToLowerInvariant())
        .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();

    private static string Resolve(string root) => Path.Combine(Path.GetFullPath(root), ".forge", "quality", "foundation.json");
}

public sealed record QualityFoundationLoadResult(
    QualityFoundationProfile Profile,
    bool FileMissing,
    IReadOnlyList<ProjectSessionDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}
