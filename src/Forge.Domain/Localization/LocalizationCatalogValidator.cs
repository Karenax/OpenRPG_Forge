using System.Text.RegularExpressions;
using Forge.Domain.Validation;

namespace Forge.Domain.Localization;

public static partial class LocalizationCatalogValidator
{
    public const string IdentityCode = "localization.catalog.id_mismatch";
    public const string SchemaCode = "localization.catalog.schema_unsupported";
    public const string GovernanceCode = "localization.catalog.governance_invalid";
    public const string BudgetCode = "localization.catalog.platform_budget_invalid";
    public const string MessageFormatCode = "localization.message.icu_invalid";
    public const string BudgetExceededCode = "localization.catalog.platform_budget_exceeded";

    public static IReadOnlyList<DomainDiagnostic> Validate(LocalizationCatalog catalog, Guid expectedModuleId)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var diagnostics = new List<DomainDiagnostic>();
        if (catalog.Id.Value != expectedModuleId)
            diagnostics.Add(new(IdentityCode, "Localization catalog UUID must match the module UUID.", "module/localization/id"));
        if (catalog.SchemaVersion != LocalizationCatalog.CurrentSchemaVersion)
            diagnostics.Add(new(SchemaCode, "Localization catalog schema is unsupported.", "module/localization/schemaVersion"));
        if (!NamespacePattern().IsMatch(catalog.PackageNamespace) || !SpdxPattern().IsMatch(catalog.LicenseSpdx) ||
            string.IsNullOrWhiteSpace(catalog.Provenance) || !HashPattern().IsMatch(catalog.ContentSha256))
            diagnostics.Add(new(GovernanceCode, "Localization package namespace, SPDX licence, provenance and SHA-256 are required.", "module/localization/governance"));
        if (catalog.PlatformByteBudget <= 0)
            diagnostics.Add(new(BudgetCode, "Localization platform byte budget must be positive.", "module/localization/platformByteBudget"));
        var totalBytes = catalog.Messages.Sum(message => System.Text.Encoding.UTF8.GetByteCount(message.Fallback) +
            message.Translations.Sum(item => System.Text.Encoding.UTF8.GetByteCount(item.Key) + System.Text.Encoding.UTF8.GetByteCount(item.Value)));
        if (catalog.PlatformByteBudget > 0 && totalBytes > catalog.PlatformByteBudget)
            diagnostics.Add(new(BudgetExceededCode, "Localization catalog exceeds its platform byte budget.", "module/localization/messages"));
        foreach (var message in catalog.Messages)
        {
            if (!LocalizationMessageFormatter.IsValid(message.Fallback) ||
                message.Translations.Values.Any(value => !LocalizationMessageFormatter.IsValid(value)))
                diagnostics.Add(new(MessageFormatCode, "Localization message contains invalid ICU plural/select syntax.", $"module/localization/messages/{message.Key}"));
        }

        return diagnostics.OrderBy(item => item.Path, StringComparer.Ordinal).ThenBy(item => item.Code, StringComparer.Ordinal).ToArray();
    }

    [GeneratedRegex("^[a-z][a-z0-9]*(?:\\.[a-z][a-z0-9]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex NamespacePattern();

    [GeneratedRegex("^(?:CC0-1\\.0|MIT|Apache-2\\.0|BSD-2-Clause|BSD-3-Clause|ISC|Zlib|OFL-1\\.1)$", RegexOptions.CultureInvariant)]
    private static partial Regex SpdxPattern();

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex HashPattern();
}
