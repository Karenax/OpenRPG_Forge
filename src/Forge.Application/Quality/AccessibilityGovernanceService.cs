using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Forge.Domain.Authoring;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;

namespace Forge.Application.Quality;

public sealed record QualityGovernanceCheck(string Code, string Path, bool Passed, string Message);

public sealed record AccessibilityGovernanceReport(
    bool Passed,
    string Fingerprint,
    IReadOnlyList<QualityGovernanceCheck> EditorChecks,
    IReadOnlyList<QualityGovernanceCheck> RuntimeChecks);

public sealed record DependencyGovernanceReport(
    bool Passed,
    string Fingerprint,
    int DependencyCount,
    IReadOnlyList<QualityGovernanceCheck> Checks);

public static class AccessibilityGovernanceService
{
    public static AccessibilityGovernanceReport Evaluate(EditorWorkspaceProfile editor, ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(project);
        var access = project.Module.RuntimePolicies.Extensions.ModernTooling.QualityAssurance.Accessibility;
        var editorChecks = new[]
        {
            Check("access.editor.keyboard", "module/runtimePolicies/extensions/modernTooling/qualityAssurance/accessibility/editorKeyboardOnly",
                access.EditorKeyboardOnly, "Keyboard-only authoring is required."),
            Check("access.editor.labels", "module/runtimePolicies/extensions/modernTooling/qualityAssurance/accessibility/editorScreenReaderLabels",
                access.EditorScreenReaderLabels, "Screen-reader labels are required where supported."),
            Check("access.editor.status", "module/runtimePolicies/extensions/modernTooling/qualityAssurance/accessibility/editorColorIndependentStatus",
                access.EditorColorIndependentStatus, "Status must use text or symbols in addition to color."),
            Check("access.editor.shortcuts", "module/runtimePolicies/extensions/modernTooling/qualityAssurance/accessibility/editorShortcutReference",
                access.EditorShortcutReference, "A keyboard shortcut reference is required."),
            Check("access.editor.contrast", "module/runtimePolicies/extensions/modernTooling/qualityAssurance/accessibility/minimumContrastRatio",
                access.MinimumContrastRatio >= 4.5, "Normal editor text requires at least 4.5:1 contrast."),
            Check("access.editor.scale", ".forge/editor-workspace.json/textScale",
                editor.TextScale is >= EditorWorkspaceProfile.MinimumTextScale and <= EditorWorkspaceProfile.MaximumTextScale &&
                access.EditorTextScale is >= EditorWorkspaceProfile.MinimumTextScale and <= EditorWorkspaceProfile.MaximumTextScale,
                "Editor and module text scales must remain within supported bounds."),
            Check("access.editor.font", ".forge/editor-workspace.json/primaryFontFamily",
                !string.IsNullOrWhiteSpace(editor.PrimaryFontFamily) && editor.FallbackFontFamilies.Count > 0,
                "Editor typography requires a primary font and at least one fallback."),
        };

        var gameplay = project.Module.RuntimePolicies.Gameplay;
        var difficulty = project.Module.RuntimePolicies.Rules.ActiveDifficulty.Accessibility;
        var runtimeChecks = new[]
        {
            Check("access.runtime.subtitles", "module/runtimePolicies/extensions/modernTooling/qualityAssurance/accessibility/runtimeSubtitles",
                access.RuntimeSubtitles, "Runtime subtitles are required."),
            Check("access.runtime.captions", "module/runtimePolicies/extensions/modernTooling/qualityAssurance/accessibility/runtimeCaptions",
                access.RuntimeCaptions, "Runtime sound captions are required."),
            Check("access.runtime.text", "module/runtimePolicies/extensions/modernTooling/qualityAssurance/accessibility/runtimeTextScale",
                access.RuntimeTextScale is >= .8 and <= 2, "Runtime text scale is outside supported bounds."),
            Check("access.runtime.contrast", "module/runtimePolicies/extensions/modernTooling/qualityAssurance/accessibility/runtimeHighContrast",
                access.RuntimeHighContrast && access.RuntimeColorVisionMode != RuntimeColorVisionMode.Standard,
                "High contrast and a color-vision-safe mode are required."),
            Check("access.runtime.input", "module/runtimePolicies/gameplay/radialMenu",
                access.RuntimeInputAssists && gameplay.RadialMenu.KeyboardNavigation && gameplay.RadialMenu.ListFallback &&
                gameplay.RadialMenu.ScreenReaderLabels && gameplay.RadialMenu.Actions.All(action => !string.IsNullOrWhiteSpace(action.AccessibleLabel)),
                "Runtime actions require keyboard navigation, list fallback and accessible labels."),
            Check("access.runtime.reduced_motion", "module/runtimePolicies/extensions/modernTooling/qualityAssurance/accessibility/runtimeReducedMotion",
                access.SchemaVersion == AccessibilityQualityProfile.CurrentSchemaVersion,
                $"Reduced-motion option is configured to {access.RuntimeReducedMotion.ToString().ToLowerInvariant()}."),
            Check("access.runtime.difficulty", "module/runtimePolicies/rules/activeDifficulty/accessibility",
                access.RuntimeDifficultyAssists && difficulty.PauseAssist && difficulty.TelegraphAssist &&
                difficulty.TimingWindowMultiplier >= 1 && difficulty.PerceptionCueMultiplier >= 1,
                "Difficulty assists require pause, telegraph, timing and perception support."),
        };
        var passed = editorChecks.Concat(runtimeChecks).All(check => check.Passed);
        return new AccessibilityGovernanceReport(passed, Fingerprint(new
        {
            passed,
            editor = editorChecks.Select(check => new { check.Code, check.Path, check.Passed }),
            runtime = runtimeChecks.Select(check => new { check.Code, check.Path, check.Passed }),
        }), editorChecks, runtimeChecks);
    }

    private static QualityGovernanceCheck Check(string code, string path, bool passed, string message) =>
        new(code, path, passed, message);

    public static string Fingerprint(object value) => Convert.ToHexStringLower(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(value, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        })));
}

public static class DependencyGovernanceService
{
    private static readonly Regex SemanticVersion = new(
        "^[0-9]+\\.[0-9]+\\.[0-9]+(?:-[0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant);

    public static DependencyGovernanceReport Evaluate(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var checks = new List<QualityGovernanceCheck>();
        for (var index = 0; index < project.Module.Distribution.Dependencies.Count; index++)
        {
            var dependency = project.Module.Distribution.Dependencies[index];
            var root = $"module/distribution/dependencies/{index}";
            checks.Add(Check(dependency, "identity", root, !string.IsNullOrWhiteSpace(dependency.Name) &&
                SemanticVersion.IsMatch(dependency.Version) && !string.IsNullOrWhiteSpace(dependency.LicenseSpdx) &&
                !string.IsNullOrWhiteSpace(dependency.Owner), "Identity, semantic version, SPDX license and owner are required."));
            checks.Add(Check(dependency, "lifecycle", root, !string.IsNullOrWhiteSpace(dependency.Rationale) &&
                !string.IsNullOrWhiteSpace(dependency.UpdateCadence) && !string.IsNullOrWhiteSpace(dependency.RemovalPlan),
                "Rationale, update cadence and removal plan are required."));
            checks.Add(Check(dependency, "approval", root, dependency.ApprovalState == DependencyApprovalState.Approved &&
                dependency.ApproverRoles.Count >= dependency.RequiredApprovals && dependency.RequiredApprovals >= 2,
                "Approved dependencies require at least two review roles."));
            checks.Add(Check(dependency, "security", root, !string.IsNullOrWhiteSpace(dependency.SecurityAuditReference) &&
                dependency.RiskLevel is >= DependencyRiskLevel.Low and <= DependencyRiskLevel.Critical,
                "Risk and security audit evidence are required."));
        }
        var passed = checks.All(check => check.Passed);
        return new DependencyGovernanceReport(passed, AccessibilityGovernanceService.Fingerprint(new
        {
            passed,
            dependencies = project.Module.Distribution.Dependencies.Select(dependency => new
            {
                id = dependency.Id.ToString(),
                dependency.Name,
                dependency.Version,
                dependency.ApprovalState,
                dependency.RiskLevel,
                dependency.RequiredApprovals,
                roles = dependency.ApproverRoles.Order(StringComparer.OrdinalIgnoreCase),
                dependency.SecurityAuditReference,
            }),
            checks = checks.Select(check => new { check.Code, check.Path, check.Passed }),
        }), project.Module.Distribution.Dependencies.Count, checks);
    }

    private static QualityGovernanceCheck Check(ThirdPartyDependencyApproval dependency, string suffix,
        string root, bool passed, string message) => new($"dependency.{suffix}", root, passed,
            $"{dependency.Name}: {message}");
}
