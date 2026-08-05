using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Quality;
using Forge.Domain.Assets;
using Forge.Domain.Authoring;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;
using Forge.ModuleFormat.Assets;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Configuration;
using Forge.Scripting;

namespace Forge.ModuleFormat.Quality;

public sealed record SecurityFuzzBoundaryReport(string Boundary, int Cases, int Accepted, int Rejected,
    int UnexpectedFailures, int PathEscapes, long ElapsedMilliseconds);

public sealed record SecurityFuzzReport(bool Passed, string Fingerprint, int TotalCases,
    IReadOnlyList<SecurityFuzzBoundaryReport> Boundaries);

public sealed record WorkspaceGovernanceLabOptions(bool WriteArtifact)
{
    public static WorkspaceGovernanceLabOptions Default { get; } = new(true);
}

public sealed record WorkspaceGovernanceLabReport(bool Passed, string Fingerprint,
    AccessibilityGovernanceReport Accessibility, SecurityFuzzReport SecurityFuzz,
    DependencyGovernanceReport Dependencies, string? ReportPath);

public sealed class WorkspaceGovernanceLabService(IForgeScriptRuntime scriptRuntime)
{
    public const string ReportRelativePath = ".forge/quality/governance-report.json";
    private readonly IForgeScriptRuntime _scriptRuntime = scriptRuntime ?? throw new ArgumentNullException(nameof(scriptRuntime));

    public WorkspaceGovernanceLabReport Run(string workspaceRoot, ProjectDefinition project,
        WorkspaceGovernanceLabOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(project);
        options ??= WorkspaceGovernanceLabOptions.Default;
        var root = Path.GetFullPath(workspaceRoot);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        var editor = EditorWorkspaceProfileStore.TryLoad(root) ??
            EditorWorkspaceProfile.CreateDefault(EditorRole.Builder, project.Module.EntryAreaId);
        var accessibility = AccessibilityGovernanceService.Evaluate(editor, project);
        var fuzz = RunFuzz(project.Module.RuntimePolicies.Extensions.ModernTooling.QualityAssurance.SecurityFuzz);
        var dependencies = DependencyGovernanceService.Evaluate(project);
        var passed = accessibility.Passed && fuzz.Passed && dependencies.Passed;
        var fingerprint = AccessibilityGovernanceService.Fingerprint(new
        {
            passed,
            accessibility = accessibility.Fingerprint,
            fuzz = fuzz.Fingerprint,
            dependencies = dependencies.Fingerprint,
        });
        var path = options.WriteArtifact ? WriteJson(root, new
        {
            schemaVersion = 1,
            passed,
            fingerprint,
            accessibility,
            securityFuzz = fuzz,
            dependencies,
        }) : null;
        return new WorkspaceGovernanceLabReport(passed, fingerprint, accessibility, fuzz, dependencies, path);
    }

    private SecurityFuzzReport RunFuzz(SecurityFuzzProfile profile)
    {
        var reports = new List<SecurityFuzzBoundaryReport>();
        var totalTimer = Stopwatch.StartNew();
        foreach (var boundary in profile.Boundaries)
        {
            var timer = Stopwatch.StartNew();
            var accepted = 0;
            var rejected = 0;
            var failures = 0;
            var escapes = 0;
            var random = new Random(unchecked(profile.Seed * 397 ^ StableBoundarySeed(boundary)));
            for (var index = 0; index < profile.CasesPerBoundary; index++)
            {
                var payload = Payload(random, profile.MaximumInputBytes);
                try
                {
                    var outcome = Exercise(boundary, payload, index, profile.MaximumInputBytes);
                    if (outcome.Accepted) accepted++; else rejected++;
                    if (outcome.PathEscape) escapes++;
                }
                catch (Exception exception) when (IsExpectedRejection(exception))
                {
                    rejected++;
                }
                catch
                {
                    failures++;
                }
            }
            timer.Stop();
            reports.Add(new SecurityFuzzBoundaryReport(boundary, profile.CasesPerBoundary, accepted, rejected,
                failures, escapes, timer.ElapsedMilliseconds));
        }
        totalTimer.Stop();
        var passed = (!profile.RequireNoCrash || reports.All(report => report.UnexpectedFailures == 0)) &&
            (!profile.RequireNoPathEscape || reports.All(report => report.PathEscapes == 0)) &&
            totalTimer.ElapsedMilliseconds <= profile.TimeBudgetMilliseconds;
        var fingerprint = AccessibilityGovernanceService.Fingerprint(new
        {
            passed,
            profile.Seed,
            profile.CasesPerBoundary,
            reports = reports.Select(report => new
            {
                report.Boundary,
                report.Cases,
                report.Accepted,
                report.Rejected,
                report.UnexpectedFailures,
                report.PathEscapes,
            }),
        });
        return new SecurityFuzzReport(passed, fingerprint, reports.Sum(report => report.Cases), reports);
    }

    private (bool Accepted, bool PathEscape) Exercise(string boundary, byte[] payload, int index, int maximumBytes)
    {
        var text = Encoding.UTF8.GetString(payload);
        return boundary switch
        {
            "project-json" => (ProjectJsonSerializer.Deserialize(text) is not null, false),
            "package-archive" => (ValidatePackageBytes(payload), false),
            "forgescript" => (_scriptRuntime.Compile($"void main() {{\n{text}\n}}").Success, false),
            "network-json" => (new NetworkConfigurationStore().Deserialize(text) is not null, false),
            "image-header" => (UntrustedAssetHeaderValidator.IsSupportedImage(payload, maximumBytes), false),
            "model-header" => (UntrustedAssetHeaderValidator.IsSupportedModel(payload, maximumBytes), false),
            "path-sandbox" => ValidatePath(index),
            _ => throw new ArgumentOutOfRangeException(nameof(boundary), boundary, "Unsupported fuzz boundary."),
        };
    }

    private static (bool Accepted, bool PathEscape) ValidatePath(int index)
    {
        var malicious = index % 2 == 0;
        var path = (index % 4) switch
        {
            0 => "../outside/model.glb",
            1 => "assets/props/model.glb",
            2 => "C:/outside/model.glb",
            _ => "assets/textures/diffuse.png",
        };
        var safe = AssetCatalogValidator.IsSafeRelativePath(path);
        return (safe, malicious && safe);
    }

    private static bool ValidatePackageBytes(byte[] payload)
    {
        var temporary = Path.Combine(Path.GetTempPath(), $"openrpg-forge-fuzz-{Guid.NewGuid():N}.orpmod");
        try
        {
            File.WriteAllBytes(temporary, payload);
            return ModulePackageValidator.Validate(temporary).IsValid;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static byte[] Payload(Random random, int maximumBytes)
    {
        var length = random.Next(64, Math.Min(maximumBytes, 2048) + 1);
        var bytes = new byte[length];
        random.NextBytes(bytes);
        return bytes;
    }

    private static int StableBoundarySeed(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return BitConverter.ToInt32(hash, 0);
    }

    private static bool IsExpectedRejection(Exception exception) => exception is ArgumentException or JsonException or
        InvalidDataException or IOException or OverflowException or DecoderFallbackException or NotSupportedException;

    private static string WriteJson(string root, object value)
    {
        var output = Path.GetFullPath(Path.Combine(root,
            ReportRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!output.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Governance report path escapes the workspace.");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var temporary = output + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(value, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
            }) + Environment.NewLine, new UTF8Encoding(false));
            File.Move(temporary, output, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        return ReportRelativePath;
    }
}
