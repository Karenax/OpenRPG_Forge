using Forge.Domain.Areas;
using Forge.Domain.Validation;

namespace Forge.Domain.Audio;

public static class AudioCaptionValidator
{
    public const string SchemaUnsupportedCode = "audio.caption.schema_unsupported";
    public const string IdentityInvalidCode = "audio.caption.identity_invalid";
    public const string LocalizationInvalidCode = "audio.caption.localization_invalid";
    public const string ResourceMissingCode = "audio.caption.resource_missing";
    public const string GovernanceMissingCode = "audio.caption.governance_missing";
    public const string BudgetExceededCode = "audio.caption.budget_exceeded";

    public static IReadOnlyList<DomainDiagnostic> Validate(AreaAudioProfile profile,
        string path = "audio/captions")
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<DomainDiagnostic>();
        if (profile.Orchestration.Captions.Count > 512)
            diagnostics.Add(new(BudgetExceededCode, "Audio captions are limited to 512 per area.", path));
        var governed = profile.Emitters.Definitions.SelectMany(item => item.ClipSet.Clips)
            .Select(item => item.ResourcePath).ToHashSet(StringComparer.Ordinal);
        for (var index = 0; index < profile.Orchestration.Captions.Count; index++)
        {
            var caption = profile.Orchestration.Captions[index];
            var captionPath = $"{path}/{index}";
            var migrationDefault = string.Equals(caption.Provenance, "migration-default",
                StringComparison.Ordinal);
            if (caption.SchemaVersion != SoundCaption.CurrentSchemaVersion)
                diagnostics.Add(new(SchemaUnsupportedCode, "Audio caption schema must be 1.",
                    $"{captionPath}/schemaVersion"));
            if ((!migrationDefault && caption.Id == Guid.Empty) ||
                string.IsNullOrWhiteSpace(caption.Provenance) ||
                string.IsNullOrWhiteSpace(caption.PackageManifest) || !ValidSpdx(caption.LicenseSpdx) ||
                (!migrationDefault && !ValidSha256(caption.ContentSha256)))
                diagnostics.Add(new(IdentityInvalidCode,
                    "Audio caption requires UUID, provenance, manifest, SPDX license and SHA-256.", captionPath));
            if (string.IsNullOrWhiteSpace(caption.Resource) ||
                caption.Localizations.Count == 0 || caption.Localizations.Any(item =>
                    string.IsNullOrWhiteSpace(item.Locale) || string.IsNullOrWhiteSpace(item.Text) ||
                    item.Text.Length > 512) ||
                caption.Localizations.Select(item => item.Locale).Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                caption.Localizations.Count ||
                caption.Importance is not ("informational" or "important" or "critical") ||
                caption.CueKind is not ("dialogue" or "effect" or "ambient") ||
                !float.IsFinite(caption.MinimumDisplaySeconds) ||
                !float.IsFinite(caption.MaximumDisplaySeconds) ||
                caption.MinimumDisplaySeconds is < .25f or > 30 ||
                caption.MaximumDisplaySeconds < caption.MinimumDisplaySeconds ||
                caption.MaximumDisplaySeconds > 60)
                diagnostics.Add(new(LocalizationInvalidCode,
                    "Caption requires unique localized text, known cue kind and bounded display duration.",
                    captionPath));
            if (!string.IsNullOrWhiteSpace(caption.Resource) &&
                !caption.Resource.StartsWith("builtin://", StringComparison.Ordinal) &&
                !profile.Playlist.Contains(caption.Resource, StringComparer.Ordinal) &&
                !governed.Contains(caption.Resource))
                diagnostics.Add(new(ResourceMissingCode,
                    $"Caption resource '{caption.Resource}' is not referenced by the area audio profile.",
                    $"{captionPath}/resource"));
            if (!migrationDefault && !caption.Resource.StartsWith("builtin://", StringComparison.Ordinal) &&
                !governed.Contains(caption.Resource))
                diagnostics.Add(new(GovernanceMissingCode,
                    $"Caption resource '{caption.Resource}' has no governed clip license and provenance.",
                    $"{captionPath}/resource"));
        }
        return diagnostics;
    }

    private static bool ValidSpdx(string value) => !string.IsNullOrWhiteSpace(value) &&
        !string.Equals(value.Trim(), "UNLICENSED", StringComparison.OrdinalIgnoreCase) &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or '+' or '(' or ')' or ' ');

    private static bool ValidSha256(string value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
}
