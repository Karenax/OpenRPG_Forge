using System.Text.RegularExpressions;

namespace Forge.Domain.Creatures;

public sealed record CreaturePresentationProfile
{
    public const int CurrentSchemaVersion = 1;

    private static readonly Regex IdentifierPattern = new(
        "^[a-z0-9]+(?:[._-][a-z0-9]+)*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public Guid Id { get; }

    public int SchemaVersion { get; }

    public string Provenance { get; }

    public string GenderId { get; }

    public string BodyTypeId { get; }

    public PronounSet Pronouns { get; }

    public IReadOnlyList<PortraitVariant> Portraits { get; }

    public bool UseModelFallback { get; }

    public string RigId { get; }

    public IReadOnlyList<BodySlot> BodySlots { get; }

    public IReadOnlyList<MaterialColor> MaterialColors { get; }

    public IReadOnlyList<Attachment> Attachments { get; }

    public VoiceProfile? Voice { get; }

    public CreaturePresentationProfile(
        Guid id,
        int schemaVersion,
        string provenance,
        string genderId,
        string bodyTypeId,
        PronounSet pronouns,
        IReadOnlyList<PortraitVariant>? portraits,
        bool useModelFallback,
        string rigId,
        IReadOnlyList<BodySlot>? bodySlots,
        IReadOnlyList<MaterialColor>? materialColors,
        IReadOnlyList<Attachment>? attachments,
        VoiceProfile? voice)
    {
        ArgumentNullException.ThrowIfNull(pronouns);
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance?.Trim() ?? string.Empty;
        GenderId = NormalizeIdentifier(genderId);
        BodyTypeId = NormalizeIdentifier(bodyTypeId);
        Pronouns = pronouns.Normalize();
        Portraits = (portraits ?? []).Select(item => item with
        {
            Expression = NormalizeIdentifier(item.Expression),
            AssetPath = item.AssetPath?.Trim().Replace('\\', '/') ?? string.Empty,
            LicenseSpdx = item.LicenseSpdx?.Trim() ?? string.Empty,
            ContentSha256 = item.ContentSha256?.Trim().ToLowerInvariant() ?? string.Empty,
            AltText = item.AltText?.Trim() ?? string.Empty,
        }).OrderBy(item => item.Expression, StringComparer.Ordinal)
            .ThenBy(item => item.Width).ThenBy(item => item.Height).ToArray();
        UseModelFallback = useModelFallback;
        RigId = NormalizeIdentifier(rigId);
        BodySlots = (bodySlots ?? []).Select(item => item with
        {
            SlotId = NormalizeIdentifier(item.SlotId),
            AssetId = item.AssetId?.Trim() ?? string.Empty,
            RequiredRigId = NormalizeIdentifier(item.RequiredRigId),
            HiddenSlots = (item.HiddenSlots ?? []).Select(NormalizeIdentifier).Order(StringComparer.Ordinal).ToArray(),
            ClippingRule = NormalizeIdentifier(item.ClippingRule),
        }).OrderBy(item => item.SlotId, StringComparer.Ordinal).ToArray();
        MaterialColors = (materialColors ?? []).Select(item => item with
        {
            ParameterId = NormalizeIdentifier(item.ParameterId),
            HexColor = item.HexColor?.Trim().ToUpperInvariant() ?? string.Empty,
        }).OrderBy(item => item.ParameterId, StringComparer.Ordinal).ToArray();
        Attachments = (attachments ?? []).Select(item => item with
        {
            SlotId = NormalizeIdentifier(item.SlotId),
            Kind = NormalizeIdentifier(item.Kind),
            AssetId = item.AssetId?.Trim() ?? string.Empty,
            RequiredRigId = NormalizeIdentifier(item.RequiredRigId),
        }).OrderBy(item => item.SlotId, StringComparer.Ordinal).ToArray();
        Voice = voice?.Normalize();
    }

    public static CreaturePresentationProfile CreateDefault(Guid id, string provenance) => new(
        id,
        CurrentSchemaVersion,
        provenance,
        "unspecified",
        "humanoid-medium",
        new PronounSet("iel", "iel", "son", "soi"),
        [],
        useModelFallback: true,
        "rig.humanoid",
        [],
        [new MaterialColor("primary", "#D68A3A")],
        [],
        null);

    internal static string NormalizeIdentifier(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;

    internal static bool IsCanonicalIdentifier(string value) => IdentifierPattern.IsMatch(value);

    public sealed record PronounSet(string Subject, string Object, string Possessive, string Reflexive)
    {
        internal PronounSet Normalize() => new(
            Subject?.Trim() ?? string.Empty,
            Object?.Trim() ?? string.Empty,
            Possessive?.Trim() ?? string.Empty,
            Reflexive?.Trim() ?? string.Empty);
    }

    public sealed record PortraitVariant(
        string Expression,
        string AssetPath,
        int Width,
        int Height,
        string LicenseSpdx,
        string ContentSha256,
        string AltText);

    public sealed record BodySlot(
        string SlotId,
        string AssetId,
        string RequiredRigId,
        int Layer,
        IReadOnlyList<string> HiddenSlots,
        string ClippingRule);

    public sealed record MaterialColor(string ParameterId, string HexColor);

    public sealed record Attachment(
        string SlotId,
        string Kind,
        string AssetId,
        string RequiredRigId);

    public sealed record VoiceProfile(
        string ProfileId,
        string Language,
        IReadOnlyList<string> AudioBankIds,
        IReadOnlyList<string> EmotionIds,
        string? LipSyncProfileId,
        IReadOnlyList<string> VariantIds,
        string LicenseSpdx,
        string RightsProvenance)
    {
        internal VoiceProfile Normalize() => new(
            NormalizeIdentifier(ProfileId),
            Language?.Trim() ?? string.Empty,
            (AudioBankIds ?? []).Order(StringComparer.Ordinal).ToArray(),
            (EmotionIds ?? []).Order(StringComparer.Ordinal).ToArray(),
            string.IsNullOrWhiteSpace(LipSyncProfileId) ? null : NormalizeIdentifier(LipSyncProfileId),
            (VariantIds ?? []).Order(StringComparer.Ordinal).ToArray(),
            LicenseSpdx?.Trim() ?? string.Empty,
            RightsProvenance?.Trim() ?? string.Empty);
    }
}
