using Forge.Domain.Validation;

namespace Forge.Domain.Audio;

public static class AudioEmitterCatalogValidator
{
    public const string SchemaUnsupportedCode = "audio.catalog.schema_unsupported";
    public const string DefinitionInvalidCode = "audio.emitter.definition_invalid";
    public const string DefinitionDuplicateCode = "audio.emitter.definition_duplicate";
    public const string ClipSetInvalidCode = "audio.clip_set.invalid";
    public const string ClipInvalidCode = "audio.clip.invalid";
    public const string ClipDuplicateCode = "audio.clip.uuid_duplicate";
    public const string LoopInvalidCode = "audio.loop.invalid";
    public const string SpatialInvalidCode = "audio.spatial.invalid";
    public const string InstanceInvalidCode = "audio.emitter.instance_invalid";
    public const string InstanceDuplicateCode = "audio.emitter.instance_duplicate";
    public const string DefinitionMissingCode = "audio.emitter.definition_missing";
    public const string MixBusMissingCode = "audio.emitter.mix_bus_missing";
    public const string MemoryBudgetCode = "audio.memory.budget_exceeded";

    public static IReadOnlyList<DomainDiagnostic> Validate(
        AudioEmitterCatalog catalog,
        IReadOnlySet<Guid>? mixBusIds = null,
        long maximumMemoryBytes = 256L * 1024 * 1024,
        string path = "audioEmitters")
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var diagnostics = new List<DomainDiagnostic>();
        if (catalog.SchemaVersion != AudioEmitterCatalog.CurrentSchemaVersion)
            Add(SchemaUnsupportedCode, "Audio emitter catalog schema must be 1.", $"{path}/schemaVersion");

        var definitionIds = new HashSet<Guid>();
        var clipIds = new HashSet<Guid>();
        long totalBytes = 0;
        for (var definitionIndex = 0; definitionIndex < catalog.Definitions.Count; definitionIndex++)
        {
            var definition = catalog.Definitions[definitionIndex];
            var definitionPath = $"{path}/definitions/{definitionIndex}";
            if (definition.Id == Guid.Empty || definition.SchemaVersion != AudioEmitterDefinition.CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(definition.Name) || string.IsNullOrWhiteSpace(definition.Provenance) ||
                string.IsNullOrWhiteSpace(definition.PackageManifest) || !ValidSpdx(definition.LicenseSpdx) ||
                !ValidSha256(definition.ContentSha256) || !float.IsFinite(definition.StartDelaySeconds) ||
                definition.StartDelaySeconds < 0)
                Add(DefinitionInvalidCode,
                    "Audio emitter requires a stable UUID, supported schema, name, provenance, package manifest, SPDX license and SHA-256.",
                    definitionPath);
            if (!definitionIds.Add(definition.Id))
                Add(DefinitionDuplicateCode, $"Audio emitter definition '{definition.Id}' is duplicated.", $"{definitionPath}/id");
            if (definition.MixBusId is { } mixBusId && (mixBusIds is null || !mixBusIds.Contains(mixBusId)))
                Add(MixBusMissingCode, $"Audio mix bus '{mixBusId}' does not exist in the area.", $"{definitionPath}/mixBusId");

            ValidateClipSet(definition.ClipSet, definitionPath, clipIds, diagnostics, ref totalBytes);
            ValidateLoop(definition.Loop, definitionPath, diagnostics);
            ValidateSpatial(definition.Spatial, definitionPath, diagnostics);
        }

        var instanceIds = new HashSet<Guid>();
        for (var instanceIndex = 0; instanceIndex < catalog.Instances.Count; instanceIndex++)
        {
            var instance = catalog.Instances[instanceIndex];
            var instancePath = $"{path}/instances/{instanceIndex}";
            if (instance.Id == Guid.Empty || instance.DefinitionId == Guid.Empty || string.IsNullOrWhiteSpace(instance.Provenance))
                Add(InstanceInvalidCode, "Audio emitter instance requires UUID, definition and provenance.", instancePath);
            if (!instanceIds.Add(instance.Id))
                Add(InstanceDuplicateCode, $"Audio emitter instance '{instance.Id}' is duplicated.", $"{instancePath}/id");
            if (!definitionIds.Contains(instance.DefinitionId))
                Add(DefinitionMissingCode, $"Audio emitter definition '{instance.DefinitionId}' does not exist.", $"{instancePath}/definitionId");
        }

        if (totalBytes > maximumMemoryBytes)
            Add(MemoryBudgetCode, $"Audio clips declare {totalBytes} bytes; budget is {maximumMemoryBytes} bytes.", path);
        return diagnostics;

        void Add(string code, string message, string diagnosticPath) =>
            diagnostics.Add(new DomainDiagnostic(code, message, diagnosticPath));
    }

    private static void ValidateClipSet(AudioClipSet clipSet, string parentPath, HashSet<Guid> clipIds,
        List<DomainDiagnostic> diagnostics, ref long totalBytes)
    {
        var path = $"{parentPath}/clipSet";
        if (clipSet.Id == Guid.Empty || clipSet.SchemaVersion != AudioClipSet.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(clipSet.Name) || string.IsNullOrWhiteSpace(clipSet.Provenance) || clipSet.Clips.Count == 0)
            diagnostics.Add(new DomainDiagnostic(ClipSetInvalidCode,
                "Audio clip set requires UUID, supported schema, name, provenance and at least one clip.", path));
        for (var index = 0; index < clipSet.Clips.Count; index++)
        {
            var clip = clipSet.Clips[index];
            var clipPath = $"{path}/clips/{index}";
            if (clip.Id == Guid.Empty || string.IsNullOrWhiteSpace(clip.ResourcePath) || !SafeRelativePath(clip.ResourcePath) ||
                clip.Weight <= 0 || !float.IsFinite(clip.Weight) || string.IsNullOrWhiteSpace(clip.Locale) ||
                !float.IsFinite(clip.LoudnessLufs) || clip.LoudnessLufs is < -120 or > 24 ||
                !clip.MimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) || clip.MemoryBytes <= 0 ||
                !ValidSpdx(clip.LicenseSpdx) || string.IsNullOrWhiteSpace(clip.Provenance) || !ValidSha256(clip.ContentSha256) ||
                (!string.IsNullOrWhiteSpace(clip.PreviewResourcePath) && !SafeRelativePath(clip.PreviewResourcePath)) ||
                !float.IsFinite(clip.DurationSeconds) || clip.DurationSeconds < 0 ||
                (clip.WaveformPeaks?.Any(peak => !float.IsFinite(peak) || peak is < 0 or > 1) ?? false))
                diagnostics.Add(new DomainDiagnostic(ClipInvalidCode,
                    "Audio clip requires a safe resource, positive weight and memory, locale, loudness, audio MIME, SPDX license, provenance and SHA-256.",
                    clipPath));
            if (!clipIds.Add(clip.Id))
                diagnostics.Add(new DomainDiagnostic(ClipDuplicateCode, $"Audio clip '{clip.Id}' is duplicated.", $"{clipPath}/id"));
            if (clip.MemoryBytes > 0)
                totalBytes = totalBytes > long.MaxValue - clip.MemoryBytes ? long.MaxValue : totalBytes + clip.MemoryBytes;
        }
    }

    private static void ValidateLoop(AudioLoopPolicy loop, string parentPath, List<DomainDiagnostic> diagnostics)
    {
        var regionMode = loop.Mode == AudioLoopMode.SeamlessRegion;
        var finiteMode = loop.Mode == AudioLoopMode.FiniteCount;
        var eventMode = loop.Mode == AudioLoopMode.EventDriven;
        if (loop.RepeatCount < 0 || (finiteMode && loop.RepeatCount < 1) ||
            !float.IsFinite(loop.RegionStartSeconds) || !float.IsFinite(loop.RegionEndSeconds) ||
            loop.RegionStartSeconds < 0 || loop.RegionEndSeconds < 0 ||
            (regionMode && loop.RegionEndSeconds <= loop.RegionStartSeconds) ||
            !float.IsFinite(loop.CrossfadeSeconds) || loop.CrossfadeSeconds < 0 ||
            (regionMode && loop.CrossfadeSeconds * 2 > loop.RegionEndSeconds - loop.RegionStartSeconds) ||
            (eventMode && string.IsNullOrWhiteSpace(loop.StopEvent)))
            diagnostics.Add(new DomainDiagnostic(LoopInvalidCode,
                "Loop policy has invalid count, region, crossfade or event trigger.", $"{parentPath}/loop"));
    }

    private static void ValidateSpatial(AudioEmitterSpatialProfile spatial, string parentPath,
        List<DomainDiagnostic> diagnostics)
    {
        if (!float.IsFinite(spatial.InnerRadius) || !float.IsFinite(spatial.OuterRadius) ||
            spatial.InnerRadius < 0 || spatial.OuterRadius <= spatial.InnerRadius ||
            string.IsNullOrWhiteSpace(spatial.AttenuationCurve) ||
            !float.IsFinite(spatial.ConeAngleDegrees) || spatial.ConeAngleDegrees is < 1 or > 360 ||
            !float.IsFinite(spatial.ConeAttenuationDb) || spatial.ConeAttenuationDb is < -80 or > 0 ||
            !float.IsFinite(spatial.Occlusion) || spatial.Occlusion is < 0 or > 1 ||
            !float.IsFinite(spatial.HeightOffset))
            diagnostics.Add(new DomainDiagnostic(SpatialInvalidCode,
                "Spatial audio requires valid range, cone, occlusion, height and attenuation curve.", $"{parentPath}/spatial"));
    }

    private static bool ValidSpdx(string value) => !string.IsNullOrWhiteSpace(value) &&
        !string.Equals(value.Trim(), "UNLICENSED", StringComparison.OrdinalIgnoreCase) &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or '+' or '(' or ')' or ' ');

    private static bool ValidSha256(string value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);

    private static bool SafeRelativePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (value.StartsWith("builtin://", StringComparison.Ordinal)) return value.Length > "builtin://".Length;
        return !value.Contains("://", StringComparison.Ordinal) && !Path.IsPathRooted(value) &&
            !value.Replace('\\', '/').Split('/').Any(segment => segment == "..");
    }
}
