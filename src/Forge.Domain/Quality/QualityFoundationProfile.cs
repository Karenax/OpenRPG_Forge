using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Validation;

namespace Forge.Domain.Quality;

public enum StructuredLogSeverity
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical,
}

public enum PlayerPrivacyMode
{
    Redact,
    Hash,
}

public sealed record StructuredLogPolicy(
    StructuredLogSeverity MinimumSeverity,
    IReadOnlyList<string> Categories,
    bool IncludeObjectIds,
    bool IncludeSourceLines,
    bool IncludeTransactions,
    PlayerPrivacyMode PlayerPrivacy,
    int RetentionDays,
    int MaximumEntries);

public sealed record LicenseValidationPolicy(
    IReadOnlyList<string> ApprovedSpdxIdentifiers,
    IReadOnlyList<string> RequiredReviewerRoles,
    bool RequireProvenance,
    bool GenerateNotice,
    bool BlockUnknowns);

public sealed record QualityTestInventory(
    IReadOnlyList<string> DomainAreas,
    IReadOnlyList<string> HeadlessScenarios,
    string ReportRelativePath);

public sealed record SaveVerificationPolicy(
    bool RoundTrip,
    bool OldVersions,
    bool PartialFailure,
    bool MissingPackage,
    bool Rollback,
    bool DeterministicState);

public sealed record ValidationCategoryPolicy(
    bool Enabled,
    bool RequiredBeforeBuild,
    bool RequiredAtRuntime);

public sealed record CanonicalValidationPolicy(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    ValidationCategoryPolicy Schemas,
    ValidationCategoryPolicy References,
    ValidationCategoryPolicy Graphs,
    ValidationCategoryPolicy Geometry,
    ValidationCategoryPolicy Navigation,
    bool ClickableDiagnostics,
    bool StructuredCli,
    bool StructuredMcp)
{
    public const int CurrentSchemaVersion = 1;

    public static CanonicalValidationPolicy CreateDefault(Guid ownerId)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("Validation policy owner UUID cannot be empty.", nameof(ownerId));
        var required = new ValidationCategoryPolicy(true, true, true);
        var optional = new ValidationCategoryPolicy(true, false, false);
        return new(Stable(ownerId, "canonical-validation"), CurrentSchemaVersion,
            "openrpg-forge:qa-r027", required, optional, optional, optional, optional, true, true, true);
    }

    public IReadOnlyDictionary<string, ValidationCategoryPolicy> Categories =>
        new Dictionary<string, ValidationCategoryPolicy>(StringComparer.Ordinal)
        {
            ["geometry"] = Geometry,
            ["graphs"] = Graphs,
            ["navigation"] = Navigation,
            ["references"] = References,
            ["schemas"] = Schemas,
        };

    private static Guid Stable(Guid ownerId, string suffix)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:{ownerId:D}:{suffix}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }
}

public sealed record QualityFoundationProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    LicenseValidationPolicy Licenses,
    QualityTestInventory Tests,
    SaveVerificationPolicy Saves,
    StructuredLogPolicy Logs,
    CanonicalValidationPolicy? Validation = null)
{
    public const int CurrentSchemaVersion = 1;

    public static QualityFoundationProfile CreateDefault(Guid ownerId)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("Quality profile owner UUID cannot be empty.", nameof(ownerId));
        return new(
            Stable(ownerId, "quality-foundation"), CurrentSchemaVersion, "openrpg-forge:qa-default",
            new(["Apache-2.0", "BSD-2-Clause", "BSD-3-Clause", "CC0-1.0", "CC-BY-4.0", "MIT"],
                ["content", "legal"], true, true, true),
            new(["ids", "serializers", "migrations", "commands", "validation", "rules", "diffs"],
                ["crypt-of-ashes", "generic-core"], ".forge/quality/reports"),
            new(true, true, true, true, true, true),
            new(StructuredLogSeverity.Information,
                ["build", "content", "runtime", "save", "script", "security"], true, true, true,
                PlayerPrivacyMode.Redact, 14, 10_000),
            CanonicalValidationPolicy.CreateDefault(ownerId));
    }

    public CanonicalValidationPolicy EffectiveValidation => Validation ?? CanonicalValidationPolicy.CreateDefault(Id);

    private static Guid Stable(Guid ownerId, string suffix)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:{ownerId:D}:{suffix}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }
}

public static class QualityFoundationProfileValidator
{
    public const string IdentityInvalidCode = "qa.foundation.identity_invalid";
    public const string SchemaInvalidCode = "qa.foundation.schema_invalid";
    public const string LicensePolicyInvalidCode = "qa.foundation.license_policy_invalid";
    public const string TestInventoryInvalidCode = "qa.foundation.test_inventory_invalid";
    public const string SaveCoverageInvalidCode = "qa.foundation.save_coverage_invalid";
    public const string LogPolicyInvalidCode = "qa.foundation.log_policy_invalid";
    public const string ValidationPolicyInvalidCode = "qa.foundation.validation_policy_invalid";

    private static readonly string[] RequiredDomainAreas =
        ["ids", "serializers", "migrations", "commands", "validation", "rules", "diffs"];

    public static IReadOnlyList<DomainDiagnostic> Validate(QualityFoundationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var path = $"quality/foundation/{profile.Id:D}";
        var diagnostics = new List<DomainDiagnostic>();
        if (profile.Id == Guid.Empty || string.IsNullOrWhiteSpace(profile.Provenance))
            diagnostics.Add(new(IdentityInvalidCode, "Quality profile UUID and provenance are required.", path));
        if (profile.SchemaVersion != QualityFoundationProfile.CurrentSchemaVersion)
            diagnostics.Add(new(SchemaInvalidCode, "Quality foundation schema must be 1.", $"{path}/schemaVersion"));
        var licenses = Normalize(profile.Licenses.ApprovedSpdxIdentifiers);
        if (!profile.Licenses.BlockUnknowns || !profile.Licenses.GenerateNotice || !profile.Licenses.RequireProvenance ||
            licenses.Count == 0 || licenses.Any(item => !DistributionComplianceValidator.IsApprovedLicense(item)) ||
            Normalize(profile.Licenses.RequiredReviewerRoles).Count < 2)
            diagnostics.Add(new(LicensePolicyInvalidCode,
                "License validation requires approved SPDX IDs, provenance, NOTICE, unknown blocking and two reviewer roles.",
                $"{path}/licenses"));
        var areas = Normalize(profile.Tests.DomainAreas);
        if (RequiredDomainAreas.Any(required => !areas.Contains(required, StringComparer.Ordinal)) ||
            Normalize(profile.Tests.HeadlessScenarios).Count < 2 || !IsSafeRelativePath(profile.Tests.ReportRelativePath))
            diagnostics.Add(new(TestInventoryInvalidCode,
                "Domain areas, two headless scenarios and a safe report path are required.", $"{path}/tests"));
        if (!profile.Saves.RoundTrip || !profile.Saves.OldVersions || !profile.Saves.PartialFailure ||
            !profile.Saves.MissingPackage || !profile.Saves.Rollback || !profile.Saves.DeterministicState)
            diagnostics.Add(new(SaveCoverageInvalidCode,
                "Save verification must cover round-trip, old versions, partial failure, missing package, rollback and deterministic state.",
                $"{path}/saves"));
        if (Normalize(profile.Logs.Categories).Count == 0 || !profile.Logs.IncludeObjectIds ||
            !profile.Logs.IncludeSourceLines || !profile.Logs.IncludeTransactions ||
            profile.Logs.RetentionDays is < 1 or > 365 || profile.Logs.MaximumEntries is < 100 or > 1_000_000)
            diagnostics.Add(new(LogPolicyInvalidCode,
                "Structured logs require categories, object/source/transaction fields, privacy and bounded retention.",
                $"{path}/logs"));
        var validation = profile.EffectiveValidation;
        if (validation.Id == Guid.Empty || validation.SchemaVersion != CanonicalValidationPolicy.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(validation.Provenance) || !validation.ClickableDiagnostics ||
            !validation.StructuredCli || !validation.StructuredMcp ||
            !validation.Schemas.Enabled || !validation.Schemas.RequiredBeforeBuild ||
            validation.Categories.Values.Any(category =>
                (category.RequiredBeforeBuild || category.RequiredAtRuntime) && !category.Enabled))
            diagnostics.Add(new(ValidationPolicyInvalidCode,
                "Canonical schema validation must remain enabled before build; required categories must be enabled with clickable structured outputs.",
                $"{path}/validation"));
        return diagnostics;
    }

    private static IReadOnlyList<string> Normalize(IReadOnlyList<string>? values) => (values ?? [])
        .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim().ToLowerInvariant())
        .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();

    private static bool IsSafeRelativePath(string value) => !string.IsNullOrWhiteSpace(value) && !Path.IsPathRooted(value) &&
        value.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).All(segment => segment is not "." and not "..");
}
