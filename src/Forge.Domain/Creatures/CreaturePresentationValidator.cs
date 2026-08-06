using System.Text.RegularExpressions;

namespace Forge.Domain.Creatures;

public static partial class CreaturePresentationValidator
{
    public static IReadOnlyList<CreaturePresentationDiagnostic> Validate(
        CreaturePresentationProfile profile,
        string path = "creaturePresentation")
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<CreaturePresentationDiagnostic>();
        Add(profile.Id == Guid.Empty, "creature.presentation.uuid_required", path, "A stable presentation UUID is required.");
        Add(profile.SchemaVersion != CreaturePresentationProfile.CurrentSchemaVersion,
            "creature.presentation.schema_unsupported", path, "The creature presentation schema is unsupported.");
        Add(string.IsNullOrWhiteSpace(profile.Provenance), "creature.presentation.provenance_required", path,
            "Presentation provenance is required.");
        Add(!CreaturePresentationProfile.IsCanonicalIdentifier(profile.GenderId),
            "creature.presentation.gender_invalid", $"{path}/genderId", "Gender must use a canonical extensible identifier.");
        Add(!CreaturePresentationProfile.IsCanonicalIdentifier(profile.BodyTypeId),
            "creature.presentation.body_type_invalid", $"{path}/bodyTypeId", "Body type must use a canonical extensible identifier.");
        Add(new[] { profile.Pronouns.Subject, profile.Pronouns.Object, profile.Pronouns.Possessive, profile.Pronouns.Reflexive }
                .Any(string.IsNullOrWhiteSpace),
            "creature.presentation.pronouns_incomplete", $"{path}/pronouns", "Subject, object, possessive and reflexive pronouns are required.");
        Add(profile.Portraits.Count == 0 && !profile.UseModelFallback,
            "creature.presentation.portrait_or_fallback_required", $"{path}/portraits", "Provide a portrait or enable the 3D model fallback.");
        Add(!CreaturePresentationProfile.IsCanonicalIdentifier(profile.RigId),
            "creature.presentation.rig_invalid", $"{path}/rigId", "A canonical rig identifier is required.");

        ValidatePortraits(profile, path, diagnostics);
        ValidateBodySlots(profile, path, diagnostics);
        ValidateMaterialColors(profile, path, diagnostics);
        ValidateAttachments(profile, path, diagnostics);
        ValidateVoice(profile, path, diagnostics);
        return diagnostics;

        void Add(bool condition, string code, string diagnosticPath, string message)
        {
            if (condition) diagnostics.Add(new(code, diagnosticPath, message));
        }
    }

    private static void ValidatePortraits(
        CreaturePresentationProfile profile,
        string path,
        ICollection<CreaturePresentationDiagnostic> diagnostics)
    {
        if (profile.Portraits.Select(item => $"{item.Expression}:{item.Width}x{item.Height}")
            .Distinct(StringComparer.Ordinal).Count() != profile.Portraits.Count)
        {
            diagnostics.Add(new("creature.presentation.portrait_duplicate", $"{path}/portraits",
                "Portrait expression and resolution pairs must be unique."));
        }

        for (var index = 0; index < profile.Portraits.Count; index++)
        {
            var portrait = profile.Portraits[index];
            var itemPath = $"{path}/portraits/{index}";
            if (!CreaturePresentationProfile.IsCanonicalIdentifier(portrait.Expression) ||
                !IsSafeRelativeAssetPath(portrait.AssetPath) || portrait.Width < 32 || portrait.Height < 32 ||
                string.IsNullOrWhiteSpace(portrait.LicenseSpdx) || !Sha256Pattern().IsMatch(portrait.ContentSha256) ||
                string.IsNullOrWhiteSpace(portrait.AltText))
            {
                diagnostics.Add(new("creature.presentation.portrait_invalid", itemPath,
                    "Portrait expression, safe asset path, dimensions, licence, SHA-256 and alternative text are required."));
            }
        }
    }

    private static void ValidateBodySlots(
        CreaturePresentationProfile profile,
        string path,
        ICollection<CreaturePresentationDiagnostic> diagnostics)
    {
        if (profile.BodySlots.Select(item => item.SlotId).Distinct(StringComparer.Ordinal).Count() != profile.BodySlots.Count)
        {
            diagnostics.Add(new("creature.presentation.body_slot_duplicate", $"{path}/bodySlots",
                "Body slots must be unique."));
        }

        if (profile.BodySlots.Any(item =>
                !CreaturePresentationProfile.IsCanonicalIdentifier(item.SlotId) ||
                string.IsNullOrWhiteSpace(item.AssetId) ||
                !string.Equals(item.RequiredRigId, profile.RigId, StringComparison.Ordinal) ||
                item.Layer is < 0 or > 64 || string.IsNullOrWhiteSpace(item.ClippingRule)))
        {
            diagnostics.Add(new("creature.presentation.body_slot_invalid", $"{path}/bodySlots",
                "Body slots require a compatible rig, asset, bounded layer and clipping rule."));
        }
    }

    private static void ValidateMaterialColors(
        CreaturePresentationProfile profile,
        string path,
        ICollection<CreaturePresentationDiagnostic> diagnostics)
    {
        if (profile.MaterialColors.Select(item => item.ParameterId).Distinct(StringComparer.Ordinal).Count() != profile.MaterialColors.Count ||
            profile.MaterialColors.Any(item =>
                !CreaturePresentationProfile.IsCanonicalIdentifier(item.ParameterId) ||
                !HexColorPattern().IsMatch(item.HexColor)))
        {
            diagnostics.Add(new("creature.presentation.material_color_invalid", $"{path}/materialColors",
                "Material parameters must be unique and use #RRGGBB or #RRGGBBAA colours."));
        }
    }

    private static void ValidateAttachments(
        CreaturePresentationProfile profile,
        string path,
        ICollection<CreaturePresentationDiagnostic> diagnostics)
    {
        if (profile.Attachments.Select(item => item.SlotId).Distinct(StringComparer.Ordinal).Count() != profile.Attachments.Count ||
            profile.Attachments.Any(item => string.IsNullOrWhiteSpace(item.AssetId) ||
                !CreaturePresentationProfile.IsCanonicalIdentifier(item.SlotId) ||
                !CreaturePresentationProfile.IsCanonicalIdentifier(item.Kind) ||
                !string.Equals(item.RequiredRigId, profile.RigId, StringComparison.Ordinal)))
        {
            diagnostics.Add(new("creature.presentation.attachment_invalid", $"{path}/attachments",
                "Attachments require a unique slot, generic kind, asset and compatible rig."));
        }
    }

    private static void ValidateVoice(
        CreaturePresentationProfile profile,
        string path,
        ICollection<CreaturePresentationDiagnostic> diagnostics)
    {
        var voice = profile.Voice;
        if (voice is null) return;
        if (!CreaturePresentationProfile.IsCanonicalIdentifier(voice.ProfileId) ||
            string.IsNullOrWhiteSpace(voice.Language) || voice.AudioBankIds.Count == 0 ||
            voice.AudioBankIds.Any(string.IsNullOrWhiteSpace) || voice.EmotionIds.Any(string.IsNullOrWhiteSpace) ||
            voice.AudioBankIds.Distinct(StringComparer.Ordinal).Count() != voice.AudioBankIds.Count ||
            voice.EmotionIds.Distinct(StringComparer.Ordinal).Count() != voice.EmotionIds.Count ||
            voice.VariantIds.Any(string.IsNullOrWhiteSpace) ||
            voice.VariantIds.Distinct(StringComparer.Ordinal).Count() != voice.VariantIds.Count ||
            voice.LipSyncProfileId is not null && !CreaturePresentationProfile.IsCanonicalIdentifier(voice.LipSyncProfileId) ||
            string.IsNullOrWhiteSpace(voice.LicenseSpdx) || string.IsNullOrWhiteSpace(voice.RightsProvenance))
        {
            diagnostics.Add(new("creature.presentation.voice_invalid", $"{path}/voice",
                "Voice profiles require language, unique banks and emotions, licence and rights provenance."));
        }
    }

    private static bool IsSafeRelativeAssetPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\')) return false;
        return path.Split('/').All(segment => segment is not "" and not "." and not "..");
    }

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();

    [GeneratedRegex("^#[0-9A-Fa-f]{6}(?:[0-9A-Fa-f]{2})?$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColorPattern();
}
