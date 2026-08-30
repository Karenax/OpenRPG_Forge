using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Domain.Assets;

namespace Forge.ModuleFormat.Assets;

public static class AssetIntelligenceProfileAnalyzer
{
    public const string Generator = "openrpg-deterministic-asset-analyzer/1";

    private static readonly string[] GenericCategories =
        ["3d", "3d-model", "asset", "model", "pack", "props"];

    public static AssetIntelligenceProfile CreateCatalogProfile(AssetCatalogItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var classification = Classify(item);
        var useCases = UseCases(classification.SemanticType, classification.AssemblyRole);
        var semanticLabelFr = FrenchSemanticLabel(classification.SemanticType);
        var style = string.IsNullOrWhiteSpace(item.TechnicalMetadata.Style)
            ? string.Empty
            : $", de style {item.TechnicalMetadata.Style}";
        var useCaseFr = useCases.Count == 0 ? "usage à préciser" : string.Join(", ", useCases);
        var useCaseEn = useCases.Count == 0 ? "usage to be reviewed" : string.Join(", ", useCases);
        AssetGeometryIntelligence? geometry = null;
        if (item.TechnicalMetadata.Dimensions is { } dimensions)
        {
            geometry = new AssetGeometryIntelligence(
                null,
                new AssetMetadataVector3(dimensions.Width, dimensions.Height, dimensions.Depth),
                new AssetMetadataVector3(0, 0, 0),
                1,
                "unknown",
                "unknown",
                "unknown");
        }

        return Normalize(new AssetIntelligenceProfile(
            AssetIntelligenceProfile.CurrentSchemaVersion,
            item.ExternalId,
            string.Empty,
            string.Empty,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["en"] = $"{item.DisplayName} is a {classification.SemanticType} asset from {item.Source}; " +
                         $"recommended role: {classification.AssemblyRole}; intended uses: {useCaseEn}.",
                ["fr"] = $"{item.DisplayName} est un asset de type {semanticLabelFr} provenant de " +
                         $"{item.Source}{style} ; rôle conseillé : {classification.AssemblyRole} ; usages : {useCaseFr}.",
            },
            classification.SemanticType,
            classification.Family,
            classification.AssemblyRole,
            useCases,
            geometry,
            new AssetPlacementIntelligence(
                classification.Surface,
                classification.SnapMode,
                null,
                []),
            new AssetIntelligenceTechnicalProfile(
                null,
                null,
                item.TechnicalMetadata.TriangleCount,
                item.TechnicalMetadata.Materials.Count > 0 ? item.TechnicalMetadata.Materials.Count : null,
                null,
                string.IsNullOrWhiteSpace(item.TechnicalMetadata.Skeleton) ? null : 1,
                null,
                item.TechnicalMetadata.Animations.Count > 0 ? item.TechnicalMetadata.Animations.Count : null,
                item.TechnicalMetadata.Materials,
                item.TechnicalMetadata.Animations),
            geometry is null ? "semantic-enriched" : "geometry-declared",
            "generated-review-required",
            Generator,
            geometry is null ? 0.45 : 0.55,
            [
                new AssetMetadataEvidence(
                    "semantics",
                    "catalog-and-filename",
                    0.45,
                    "Semantic type, family, role and descriptions were derived deterministically from governed catalog fields."),
                new AssetMetadataEvidence(
                    "geometry.dimensionsMeters",
                    geometry is null ? "not-available" : "catalog",
                    geometry is null ? 0 : 0.6,
                    geometry is null
                        ? "No governed geometry measurement is available yet."
                        : "Dimensions were declared by the source catalog and require import-time verification."),
            ]));
    }

    public static AssetIntelligenceProfile Analyze(
        AssetCatalogItem item,
        string modelPath,
        IReadOnlyList<string>? dependencyPaths = null,
        IDictionary<string, string>? hashCache = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        modelPath = Path.GetFullPath(modelPath);
        var baseline = PreferProfile(CreateCatalogProfile(item), item.Intelligence);
        var modelHash = HashFile(modelPath, hashCache);
        JsonDocument? document = null;
        var discoveredDependencies = new List<(string Key, string Path)>();
        try
        {
            document = ReadGltfDocument(modelPath);
            discoveredDependencies.AddRange(DiscoverDependencies(modelPath, document.RootElement));
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException or
                                           UnauthorizedAccessException or ArgumentException)
        {
            var dependencies = NormalizeDependencies(modelPath, dependencyPaths, discoveredDependencies);
            return WithHashesAndEvidence(
                baseline,
                modelHash,
                DependencyClosureHash(modelPath, modelHash, dependencies, hashCache),
                new AssetMetadataEvidence(
                    "geometry",
                    "model-inspection",
                    0.2,
                    $"The model was hashed but deterministic glTF inspection was partial: {exception.GetType().Name}."));
        }

        using (document)
        {
            var measured = Measure(document.RootElement);
            var dependencies = NormalizeDependencies(modelPath, dependencyPaths, discoveredDependencies);
            var closureHash = DependencyClosureHash(modelPath, modelHash, dependencies, hashCache);
            var generated = BuildMeasuredProfile(baseline, measured, modelHash, closureHash);
            return MergeMeasuredProfile(baseline, generated);
        }
    }

    public static AssetIntelligenceProfile WithBlenderGeometry(
        AssetIntelligenceProfile profile,
        AssetMetadataBounds bounds,
        AssetIntelligenceTechnicalProfile technical,
        string pivotClass)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentNullException.ThrowIfNull(technical);
        var dimensions = new AssetMetadataVector3(
            bounds.Maximum.X - bounds.Minimum.X,
            bounds.Maximum.Y - bounds.Minimum.Y,
            bounds.Maximum.Z - bounds.Minimum.Z);
        var geometry = new AssetGeometryIntelligence(
            bounds,
            dimensions,
            new AssetMetadataVector3(0, 0, 0),
            1,
            "y",
            "-z",
            NormalizeToken(pivotClass, "unknown"));
        var placement = CreatePlacement(profile.SemanticType, profile.AssemblyRole, profile.Placement, geometry, 0.95,
            "blender-quality-report");
        return Normalize(profile with
        {
            Geometry = geometry,
            Placement = placement,
            Technical = technical,
            Readiness = "placement-suggested",
            ReviewStatus = profile.ReviewStatus == "human-reviewed"
                ? profile.ReviewStatus
                : "generated-review-required",
            GeneratedBy = "openrpg-blender-worker/1",
            Confidence = Math.Max(profile.Confidence, 0.95),
            Evidence = profile.Evidence.Concat([
                new AssetMetadataEvidence(
                    "geometry-and-placement",
                    "blender-quality-report",
                    0.95,
                    "World-space bounds, dimensions and pivot class were measured after normalization; placement points remain reviewable."),
            ]).ToArray(),
        });
    }

    public static AssetIntelligenceProfile PreferProfile(
        AssetIntelligenceProfile left,
        AssetIntelligenceProfile right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return ProfileScore(right) > ProfileScore(left) ? Normalize(right) : Normalize(left);
    }

    private static AssetIntelligenceProfile BuildMeasuredProfile(
        AssetIntelligenceProfile baseline,
        GltfMeasurement measured,
        string modelHash,
        string closureHash)
    {
        AssetGeometryIntelligence? geometry = null;
        if (measured.Bounds is { } bounds)
        {
            geometry = new AssetGeometryIntelligence(
                bounds,
                new AssetMetadataVector3(
                    bounds.Maximum.X - bounds.Minimum.X,
                    bounds.Maximum.Y - bounds.Minimum.Y,
                    bounds.Maximum.Z - bounds.Minimum.Z),
                new AssetMetadataVector3(0, 0, 0),
                1,
                "y",
                "-z",
                PivotClass(bounds));
        }

        var placement = CreatePlacement(
            baseline.SemanticType,
            baseline.AssemblyRole,
            baseline.Placement,
            geometry,
            geometry is null ? 0.45 : 0.72,
            "gltf-accessors");
        return Normalize(baseline with
        {
            ModelContentSha256 = modelHash,
            DependencyClosureSha256 = closureHash,
            Geometry = geometry ?? baseline.Geometry,
            Placement = placement,
            Technical = new AssetIntelligenceTechnicalProfile(
                measured.MeshCount,
                measured.VertexCount,
                measured.TriangleCount,
                measured.Materials.Count,
                measured.TextureCount,
                measured.ArmatureCount,
                null,
                measured.Animations.Count,
                measured.Materials,
                measured.Animations),
            Readiness = geometry is null ? "semantic-enriched" : "placement-suggested",
            ReviewStatus = "generated-review-required",
            GeneratedBy = Generator,
            Confidence = geometry is null ? Math.Max(baseline.Confidence, 0.5) : Math.Max(baseline.Confidence, 0.72),
            Evidence = baseline.Evidence.Concat([
                new AssetMetadataEvidence(
                    "identity.modelContentSha256",
                    "sha256",
                    1,
                    "The selected model has an individual SHA-256 independent from its source pack fingerprint."),
                new AssetMetadataEvidence(
                    "identity.dependencyClosureSha256",
                    "sha256-dependency-closure",
                    1,
                    "The model and its discovered local dependencies were hashed as a deterministic closure."),
                new AssetMetadataEvidence(
                    "geometry-and-technical",
                    "gltf-accessors",
                    geometry is null ? 0.45 : 0.72,
                    geometry is null
                        ? "glTF structure was inspected but no governed POSITION accessor bounds were declared."
                        : "Bounds and technical counts were extracted from glTF accessors; node transforms still require Blender or human review."),
            ]).ToArray(),
        });
    }

    private static AssetIntelligenceProfile MergeMeasuredProfile(
        AssetIntelligenceProfile baseline,
        AssetIntelligenceProfile measured)
    {
        var preserveReviewed = baseline.ReviewStatus == "human-reviewed" &&
            !string.IsNullOrWhiteSpace(baseline.ModelContentSha256) &&
            string.Equals(baseline.ModelContentSha256, measured.ModelContentSha256, StringComparison.Ordinal);
        var geometry = baseline.Geometry is { Bounds: not null } && ProfileScore(baseline) >= ProfileScore(measured)
            ? baseline.Geometry
            : measured.Geometry ?? baseline.Geometry;
        var placement = baseline.Placement.SuggestedPivot is not null && ProfileScore(baseline) >= ProfileScore(measured)
            ? baseline.Placement
            : measured.Placement;
        return Normalize(measured with
        {
            Descriptions = baseline.SemanticType == "unclassified" ? measured.Descriptions : baseline.Descriptions,
            SemanticType = baseline.SemanticType == "unclassified" ? measured.SemanticType : baseline.SemanticType,
            Family = baseline.Family == "unclassified" ? measured.Family : baseline.Family,
            AssemblyRole = baseline.AssemblyRole == "unspecified" ? measured.AssemblyRole : baseline.AssemblyRole,
            UseCases = baseline.UseCases.Count == 0 ? measured.UseCases : baseline.UseCases,
            Geometry = geometry,
            Placement = placement,
            ReviewStatus = preserveReviewed ? "human-reviewed" : "generated-review-required",
            Evidence = baseline.Evidence.Concat(measured.Evidence)
                .DistinctBy(item => $"{item.Field}|{item.Source}|{item.Detail}", StringComparer.Ordinal)
                .ToArray(),
        });
    }

    private static AssetIntelligenceProfile WithHashesAndEvidence(
        AssetIntelligenceProfile profile,
        string modelHash,
        string closureHash,
        AssetMetadataEvidence evidence) => Normalize(profile with
        {
            ModelContentSha256 = modelHash,
            DependencyClosureSha256 = closureHash,
            Readiness = "semantic-enriched",
            ReviewStatus = "generated-review-required",
            Evidence = profile.Evidence.Concat([evidence]).ToArray(),
        });

    private static AssetPlacementIntelligence CreatePlacement(
        string semanticType,
        string assemblyRole,
        AssetPlacementIntelligence baseline,
        AssetGeometryIntelligence? geometry,
        double confidence,
        string evidenceSource)
    {
        if (geometry?.Bounds is not { } bounds) return baseline;
        var center = new AssetMetadataVector3(
            (bounds.Minimum.X + bounds.Maximum.X) * 0.5,
            (bounds.Minimum.Y + bounds.Maximum.Y) * 0.5,
            (bounds.Minimum.Z + bounds.Maximum.Z) * 0.5);
        var kind = semanticType == "door" ? "hinge-side" : "base-center";
        var pivot = semanticType == "door"
            ? new AssetMetadataVector3(bounds.Minimum.X, bounds.Minimum.Y, center.Z)
            : new AssetMetadataVector3(center.X, bounds.Minimum.Y, center.Z);
        var suggested = new AssetPlacementPoint(
            "placement-pivot",
            kind,
            pivot,
            new AssetMetadataVector3(0, 0, 0),
            semanticType == "door" ? Math.Min(confidence, 0.6) : confidence,
            evidenceSource,
            true);
        var anchors = new List<AssetPlacementPoint>();
        if (assemblyRole.StartsWith("modular-", StringComparison.Ordinal))
        {
            var useX = bounds.Maximum.X - bounds.Minimum.X >= bounds.Maximum.Z - bounds.Minimum.Z;
            anchors.Add(new AssetPlacementPoint(
                "snap-start",
                "modular-snap",
                useX
                    ? new AssetMetadataVector3(bounds.Minimum.X, bounds.Minimum.Y, center.Z)
                    : new AssetMetadataVector3(center.X, bounds.Minimum.Y, bounds.Minimum.Z),
                new AssetMetadataVector3(0, 0, 0),
                Math.Min(confidence, 0.58),
                evidenceSource,
                true));
            anchors.Add(new AssetPlacementPoint(
                "snap-end",
                "modular-snap",
                useX
                    ? new AssetMetadataVector3(bounds.Maximum.X, bounds.Minimum.Y, center.Z)
                    : new AssetMetadataVector3(center.X, bounds.Minimum.Y, bounds.Maximum.Z),
                new AssetMetadataVector3(0, 180, 0),
                Math.Min(confidence, 0.58),
                evidenceSource,
                true));
        }

        return new AssetPlacementIntelligence(baseline.Surface, baseline.SnapMode, suggested, anchors);
    }

    private static GltfMeasurement Measure(JsonElement root)
    {
        var accessors = root.TryGetProperty("accessors", out var accessorArray) &&
                        accessorArray.ValueKind == JsonValueKind.Array
            ? accessorArray.EnumerateArray().Select(item => item.Clone()).ToArray()
            : [];
        var materials = root.TryGetProperty("materials", out var materialArray) &&
                        materialArray.ValueKind == JsonValueKind.Array
            ? materialArray.EnumerateArray().Select((item, index) =>
                    ReadString(item, "name") ?? $"material-{index}")
                .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray()
            : [];
        var animations = root.TryGetProperty("animations", out var animationArray) &&
                         animationArray.ValueKind == JsonValueKind.Array
            ? animationArray.EnumerateArray().Select((item, index) =>
                    ReadString(item, "name") ?? $"animation-{index}")
                .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray()
            : [];
        var meshCount = 0;
        long vertices = 0;
        long triangles = 0;
        AssetMetadataVector3? minimum = null;
        AssetMetadataVector3? maximum = null;
        if (root.TryGetProperty("meshes", out var meshes) && meshes.ValueKind == JsonValueKind.Array)
        {
            meshCount = meshes.GetArrayLength();
            foreach (var mesh in meshes.EnumerateArray())
            {
                if (!mesh.TryGetProperty("primitives", out var primitives) ||
                    primitives.ValueKind != JsonValueKind.Array) continue;
                foreach (var primitive in primitives.EnumerateArray())
                {
                    var positionAccessor = ReadPositionAccessor(primitive, accessors);
                    if (positionAccessor is { } position)
                    {
                        vertices += ReadCount(position);
                        if (TryBounds(position, out var accessorMinimum, out var accessorMaximum))
                        {
                            minimum = minimum is null ? accessorMinimum : Min(minimum, accessorMinimum);
                            maximum = maximum is null ? accessorMaximum : Max(maximum, accessorMaximum);
                        }
                    }

                    var count = ReadPrimitiveCount(primitive, accessors, positionAccessor);
                    var mode = primitive.TryGetProperty("mode", out var modeValue) && modeValue.TryGetInt32(out var parsedMode)
                        ? parsedMode
                        : 4;
                    triangles += mode switch
                    {
                        4 => count / 3,
                        5 or 6 => Math.Max(0, count - 2),
                        _ => 0,
                    };
                }
            }
        }

        var textureCount = root.TryGetProperty("images", out var images) && images.ValueKind == JsonValueKind.Array
            ? images.GetArrayLength()
            : 0;
        var armatures = root.TryGetProperty("skins", out var skins) && skins.ValueKind == JsonValueKind.Array
            ? skins.GetArrayLength()
            : 0;
        return new GltfMeasurement(
            meshCount,
            vertices,
            triangles,
            materials,
            textureCount,
            armatures,
            animations,
            minimum is null || maximum is null ? null : new AssetMetadataBounds(minimum, maximum));
    }

    private static JsonElement? ReadPositionAccessor(JsonElement primitive, IReadOnlyList<JsonElement> accessors)
    {
        if (!primitive.TryGetProperty("attributes", out var attributes) ||
            !attributes.TryGetProperty("POSITION", out var position) ||
            !position.TryGetInt32(out var index) || index < 0 || index >= accessors.Count)
        {
            return null;
        }

        return accessors[index];
    }

    private static long ReadPrimitiveCount(
        JsonElement primitive,
        IReadOnlyList<JsonElement> accessors,
        JsonElement? positionAccessor)
    {
        if (primitive.TryGetProperty("indices", out var indices) && indices.TryGetInt32(out var index) &&
            index >= 0 && index < accessors.Count)
        {
            return ReadCount(accessors[index]);
        }

        return positionAccessor is { } position ? ReadCount(position) : 0;
    }

    private static long ReadCount(JsonElement accessor) =>
        accessor.TryGetProperty("count", out var count) && count.TryGetInt64(out var value) ? value : 0;

    private static bool TryBounds(
        JsonElement accessor,
        out AssetMetadataVector3 minimum,
        out AssetMetadataVector3 maximum)
    {
        minimum = maximum = new AssetMetadataVector3(0, 0, 0);
        return accessor.TryGetProperty("min", out var min) && TryVector(min, out minimum) &&
               accessor.TryGetProperty("max", out var max) && TryVector(max, out maximum);
    }

    private static bool TryVector(JsonElement array, out AssetMetadataVector3 value)
    {
        value = new AssetMetadataVector3(0, 0, 0);
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() < 3) return false;
        var items = array.EnumerateArray().Take(3).Select(item => item.GetDouble()).ToArray();
        if (items.Any(item => !double.IsFinite(item))) return false;
        value = new AssetMetadataVector3(items[0], items[1], items[2]);
        return true;
    }

    private static IEnumerable<(string Key, string Path)> DiscoverDependencies(string modelPath, JsonElement root)
    {
        var parent = Path.GetDirectoryName(modelPath)!;
        foreach (var property in new[] { "buffers", "images" })
        {
            if (!root.TryGetProperty(property, out var values) || values.ValueKind != JsonValueKind.Array) continue;
            foreach (var item in values.EnumerateArray())
            {
                var uri = ReadString(item, "uri");
                if (string.IsNullOrWhiteSpace(uri) || uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                    Uri.TryCreate(uri, UriKind.Absolute, out _)) continue;
                var decoded = Uri.UnescapeDataString(uri);
                if (decoded.Contains('\\') || decoded.Split('/').Any(part => part is "" or "." or "..")) continue;
                var path = Path.GetFullPath(Path.Combine(parent, decoded.Replace('/', Path.DirectorySeparatorChar)));
                if (File.Exists(path)) yield return (decoded.Replace('\\', '/'), path);
            }
        }
    }

    private static IReadOnlyList<(string Key, string Path)> NormalizeDependencies(
        string modelPath,
        IReadOnlyList<string>? declared,
        IReadOnlyList<(string Key, string Path)> discovered)
    {
        var parent = Path.GetDirectoryName(modelPath)!;
        return discovered.Concat((declared ?? [])
                .Where(File.Exists)
                .Select(path => (
                    Key: Path.GetRelativePath(parent, Path.GetFullPath(path)).Replace('\\', '/'),
                    Path: Path.GetFullPath(path))))
            .Where(item => !string.Equals(item.Path, modelPath, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToArray();
    }

    private static string DependencyClosureHash(
        string modelPath,
        string modelHash,
        IReadOnlyList<(string Key, string Path)> dependencies,
        IDictionary<string, string>? hashCache)
    {
        var entries = new List<string> { $"{Path.GetFileName(modelPath)}:{modelHash}" };
        entries.AddRange(dependencies.Select(item => $"{item.Key}:{HashFile(item.Path, hashCache)}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', entries))))
            .ToLowerInvariant();
    }

    private static JsonDocument ReadGltfDocument(string modelPath)
    {
        if (Path.GetExtension(modelPath).Equals(".gltf", StringComparison.OrdinalIgnoreCase))
        {
            return JsonDocument.Parse(File.ReadAllText(modelPath));
        }

        using var stream = File.OpenRead(modelPath);
        Span<byte> header = stackalloc byte[12];
        stream.ReadExactly(header);
        if (BinaryPrimitives.ReadUInt32LittleEndian(header) != 0x46546C67 ||
            BinaryPrimitives.ReadUInt32LittleEndian(header[4..]) != 2)
        {
            throw new InvalidDataException("The model is not a glTF 2.0 binary container.");
        }

        Span<byte> chunkHeader = stackalloc byte[8];
        while (stream.Position + 8 <= stream.Length)
        {
            stream.ReadExactly(chunkHeader);
            var length = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader);
            var type = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader[4..]);
            if (length > int.MaxValue || stream.Position + length > stream.Length)
            {
                throw new InvalidDataException("The GLB chunk table is invalid.");
            }

            var bytes = new byte[(int)length];
            stream.ReadExactly(bytes);
            if (type == 0x4E4F534A)
            {
                var jsonLength = bytes.Length;
                while (jsonLength > 0 && bytes[jsonLength - 1] is 0x20 or 0x00) jsonLength--;
                return JsonDocument.Parse(bytes.AsMemory(0, jsonLength));
            }
        }

        throw new InvalidDataException("The GLB container has no JSON chunk.");
    }

    private static string HashFile(string path, IDictionary<string, string>? cache)
    {
        path = Path.GetFullPath(path);
        if (cache is not null && cache.TryGetValue(path, out var cached)) return cached;
        using var stream = File.OpenRead(path);
        var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (cache is not null) cache[path] = hash;
        return hash;
    }

    private static Classification Classify(AssetCatalogItem item)
    {
        var text = string.Join(' ', new[] { item.ExternalId, item.DisplayName, item.SourceAssetId }
                .Concat(item.Categories).Concat(item.Tags))
            .ToLowerInvariant();
        string semantic;
        string role;
        string surface;
        string snap;
        if (Contains(text, "material", "texture", "surface"))
        {
            semantic = "material"; role = "surface-material"; surface = "surface"; snap = "none";
        }
        else if (Contains(text, "hdri", "sky", "environment map"))
        {
            semantic = "environment-light"; role = "environment"; surface = "environment"; snap = "none";
        }
        else if (Contains(text, "door", "gate", "portcullis"))
        {
            semantic = "door"; role = "interactive-leaf"; surface = "opening"; snap = "hinge";
        }
        else if (Contains(text, "character", "humanoid", "villager", "warrior", "knight", "mage", "rogue", "creature", "monster", "animal"))
        {
            semantic = "character"; role = "actor"; surface = "ground"; snap = "character-origin";
        }
        else if (Contains(text, "sword", "axe", "bow", "shield", "weapon", "potion", "armor", "armour", "helmet", "item"))
        {
            semantic = "item"; role = "equipable-item"; surface = "free"; snap = "grip";
        }
        else if (Contains(text, "stair", "stairs", "ladder", "bridge", "ramp"))
        {
            semantic = "architecture"; role = "modular-circulation"; surface = "ground"; snap = "modular";
        }
        else if (Contains(text, "wall", "floor", "roof", "fence", "window", "pillar", "column", "arch", "building", "house"))
        {
            semantic = "architecture"; role = "modular-structure"; surface = "ground"; snap = "modular";
        }
        else if (Contains(text, "tree", "bush", "grass", "flower", "plant", "mushroom"))
        {
            semantic = "vegetation"; role = "terrain-scatter"; surface = "ground"; snap = "scatter";
        }
        else if (Contains(text, "rock", "stone", "cliff", "boulder"))
        {
            semantic = "natural-prop"; role = "terrain-scatter"; surface = "ground"; snap = "scatter";
        }
        else if (Contains(text, "chair", "table", "bed", "bench", "cabinet", "shelf", "furniture"))
        {
            semantic = "furniture"; role = "standalone-prop"; surface = "ground"; snap = "grid";
        }
        else
        {
            semantic = "prop"; role = "standalone-prop"; surface = "ground"; snap = "grid";
        }

        var family = item.Categories
            .Select(value => NormalizeToken(value))
            .FirstOrDefault(value => value.Any(char.IsLetter) &&
                                     !GenericCategories.Contains(value, StringComparer.Ordinal)) ??
            item.Tags.Select(value => NormalizeToken(value)).FirstOrDefault(value => value.Any(char.IsLetter)) ??
            item.Source;
        return new Classification(semantic, family, role, surface, snap);
    }

    private static IReadOnlyList<string> UseCases(string semanticType, string assemblyRole) =>
        (semanticType, assemblyRole) switch
        {
            ("door", _) => ["opening", "interaction", "navigation-blocker"],
            ("architecture", "modular-circulation") => ["modular-building", "navigation", "level-design"],
            ("architecture", _) => ["modular-building", "environment-construction"],
            ("vegetation" or "natural-prop", _) => ["biome-dressing", "terrain-scatter", "environment"],
            ("character", _) => ["actor", "creature-blueprint", "animation"],
            ("item", _) => ["inventory", "equipment", "world-pickup"],
            ("material", _) => ["surface-authoring", "environment-material"],
            ("environment-light", _) => ["lighting", "sky", "environment"],
            ("furniture", _) => ["interior-dressing", "interaction-candidate"],
            _ => ["scene-dressing", "placeable"],
        };

    private static string FrenchSemanticLabel(string semanticType) => semanticType switch
    {
        "door" => "porte",
        "architecture" => "architecture",
        "vegetation" => "végétation",
        "natural-prop" => "élément naturel",
        "character" => "personnage",
        "item" => "objet",
        "material" => "matériau",
        "environment-light" => "environnement lumineux",
        "furniture" => "mobilier",
        "prop" => "accessoire",
        _ => semanticType,
    };

    private static string PivotClass(AssetMetadataBounds bounds)
    {
        var dimensions = new AssetMetadataVector3(
            bounds.Maximum.X - bounds.Minimum.X,
            bounds.Maximum.Y - bounds.Minimum.Y,
            bounds.Maximum.Z - bounds.Minimum.Z);
        var tolerance = Math.Max(0.0001, Math.Max(dimensions.X, Math.Max(dimensions.Y, dimensions.Z)) * 0.01);
        var center = new AssetMetadataVector3(
            (bounds.Minimum.X + bounds.Maximum.X) * 0.5,
            (bounds.Minimum.Y + bounds.Maximum.Y) * 0.5,
            (bounds.Minimum.Z + bounds.Maximum.Z) * 0.5);
        var grounded = Math.Abs(bounds.Minimum.Y) <= tolerance;
        var centeredHorizontal = Math.Abs(center.X) <= tolerance && Math.Abs(center.Z) <= tolerance;
        if (grounded && centeredHorizontal) return "center-base";
        if (grounded) return "base-offset";
        if (centeredHorizontal && Math.Abs(center.Y) <= tolerance) return "bounds-center";
        return "offset";
    }

    private static int ProfileScore(AssetIntelligenceProfile profile)
    {
        var score = (int)Math.Round(profile.Confidence * 20);
        if (profile.ReviewStatus == "human-reviewed") score += 100;
        if (profile.Geometry?.Bounds is not null) score += 25;
        if (profile.Placement.SuggestedPivot is not null) score += 15;
        if (!string.IsNullOrWhiteSpace(profile.ModelContentSha256)) score += 10;
        if (profile.GeneratedBy.Contains("blender", StringComparison.OrdinalIgnoreCase) ||
            profile.GeneratedBy.Contains("inventory", StringComparison.OrdinalIgnoreCase)) score += 20;
        return score;
    }

    private static AssetIntelligenceProfile Normalize(AssetIntelligenceProfile profile) => profile with
    {
        ExternalId = profile.ExternalId.Trim(),
        ModelContentSha256 = profile.ModelContentSha256.Trim().ToLowerInvariant(),
        DependencyClosureSha256 = profile.DependencyClosureSha256.Trim().ToLowerInvariant(),
        Descriptions = profile.Descriptions
            .Where(item => !string.IsNullOrWhiteSpace(item.Key) && !string.IsNullOrWhiteSpace(item.Value))
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => item.Key.Trim().ToLowerInvariant(), item => item.Value.Trim(), StringComparer.Ordinal),
        SemanticType = NormalizeToken(profile.SemanticType, "unclassified"),
        Family = NormalizeToken(profile.Family, "unclassified"),
        AssemblyRole = NormalizeToken(profile.AssemblyRole, "unspecified"),
        UseCases = NormalizeTokens(profile.UseCases),
        Placement = profile.Placement with
        {
            Surface = NormalizeToken(profile.Placement.Surface, "unspecified"),
            SnapMode = NormalizeToken(profile.Placement.SnapMode, "none"),
            Anchors = profile.Placement.Anchors.OrderBy(item => item.Name, StringComparer.Ordinal).ToArray(),
        },
        Technical = profile.Technical with
        {
            Materials = NormalizeTokens(profile.Technical.Materials),
            Animations = NormalizeTokens(profile.Technical.Animations),
        },
        Readiness = NormalizeToken(profile.Readiness, "catalogued"),
        ReviewStatus = NormalizeToken(profile.ReviewStatus, "generated-review-required"),
        GeneratedBy = profile.GeneratedBy.Trim().ToLowerInvariant(),
        Confidence = Math.Clamp(profile.Confidence, 0, 1),
        Evidence = profile.Evidence
            .OrderBy(item => item.Field, StringComparer.Ordinal)
            .ThenBy(item => item.Source, StringComparer.Ordinal)
            .ToArray(),
    };

    private static IReadOnlyList<string> NormalizeTokens(IEnumerable<string> values) => values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => NormalizeToken(value))
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static string NormalizeToken(string value, string fallback = "") =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant().Replace(' ', '-');

    private static bool Contains(string text, params string[] values)
    {
        var tokens = text.Split(
            text.Where(character => !char.IsLetterOrDigit(character)).Distinct().ToArray(),
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return values.Any(value => value.Contains(' ')
            ? text.Contains(value, StringComparison.Ordinal)
            : tokens.Contains(value, StringComparer.Ordinal));
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static AssetMetadataVector3 Min(AssetMetadataVector3 left, AssetMetadataVector3 right) =>
        new(Math.Min(left.X, right.X), Math.Min(left.Y, right.Y), Math.Min(left.Z, right.Z));

    private static AssetMetadataVector3 Max(AssetMetadataVector3 left, AssetMetadataVector3 right) =>
        new(Math.Max(left.X, right.X), Math.Max(left.Y, right.Y), Math.Max(left.Z, right.Z));

    private sealed record Classification(
        string SemanticType,
        string Family,
        string AssemblyRole,
        string Surface,
        string SnapMode);

    private sealed record GltfMeasurement(
        int MeshCount,
        long VertexCount,
        long TriangleCount,
        IReadOnlyList<string> Materials,
        int TextureCount,
        int ArmatureCount,
        IReadOnlyList<string> Animations,
        AssetMetadataBounds? Bounds);
}
