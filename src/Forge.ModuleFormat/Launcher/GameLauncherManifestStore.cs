using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Domain.Modules;

namespace Forge.ModuleFormat.Launcher;

public static class GameLauncherManifestStore
{
    public const string FileName = "game-launcher.json";
    public const string DistributionFileName = "game-distribution-manifest.json";

    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static GameLauncherManifest Load(
        string rootDirectory,
        bool verifyHashes = true,
        string? manifestFileName = null)
    {
        var root = RequireDirectory(rootDirectory);
        var fileName = RequireManifestFileName(manifestFileName ?? FileName);
        var path = Path.Combine(root, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Game launcher manifest is missing: {fileName}", path);
        }

        var manifest = JsonSerializer.Deserialize<GameLauncherManifest>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException("The game launcher manifest is empty.");
        Verify(root, manifest, verifyHashes);
        return manifest;
    }

    public static GameLauncherVerification Verify(
        string rootDirectory,
        GameLauncherManifest manifest,
        bool verifyHashes = true)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var root = RequireDirectory(rootDirectory);
        var diagnostics = Validate(manifest);
        if (diagnostics.Count > 0)
        {
            throw new InvalidDataException("Game launcher manifest is invalid: " + string.Join("; ", diagnostics));
        }

        var launcher = ResolveRequiredFile(root, manifest.ExecutableFile, "Launcher executable");
        var runtime = ResolveRequiredFile(root, manifest.RuntimeExecutable, "Runtime executable");
        var module = ResolveRequiredFile(root, manifest.ModulePackage, "Module package");
        var background = manifest.BackgroundImage is null
            ? null
            : ResolveRequiredFile(root, manifest.BackgroundImage, "Launcher background image");
        var music = manifest.Music is null
            ? null
            : ResolveRequiredFile(root, manifest.Music, "Launcher music");
        if (verifyHashes)
        {
            RequireHash(launcher, manifest.LauncherSha256, "Launcher executable");
            RequireHash(runtime, manifest.RuntimeSha256, "Runtime executable");
            RequireHash(module, manifest.ModuleSha256, "Module package");
        }

        return new GameLauncherVerification(manifest, root, launcher, runtime, module, background, music);
    }

    public static void Save(
        string rootDirectory,
        GameLauncherManifest manifest,
        string? manifestFileName = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var root = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(root);
        var diagnostics = Validate(manifest);
        if (diagnostics.Count > 0)
        {
            throw new InvalidDataException("Game launcher manifest is invalid: " + string.Join("; ", diagnostics));
        }

        var fileName = RequireManifestFileName(manifestFileName ?? FileName);
        WriteAtomic(Path.Combine(root, fileName), JsonSerializer.Serialize(manifest, Options) + Environment.NewLine);
    }

    public static void SaveDistribution(
        string rootDirectory,
        GameDistributionManifest manifest,
        string? distributionFileName = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.SchemaVersion != GameDistributionManifest.CurrentSchemaVersion || manifest.GameId == Guid.Empty ||
            string.IsNullOrWhiteSpace(manifest.Product) || string.IsNullOrWhiteSpace(manifest.Version) ||
            !string.Equals(manifest.Platform, "windows-x64", StringComparison.Ordinal) || manifest.Files.Count == 0 ||
            manifest.Files.Any(file => !IsSafeRelativePath(file.Path) || file.Bytes < 0 || !IsSha256(file.Sha256)))
        {
            throw new InvalidDataException("The game distribution manifest is invalid.");
        }

        var root = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(root);
        var fileName = RequireDistributionFileName(distributionFileName ?? DistributionFileName);
        WriteAtomic(
            Path.Combine(root, fileName),
            JsonSerializer.Serialize(manifest, Options) + Environment.NewLine);
    }

    public static GameDistributionManifest LoadAndVerifyDistribution(
        string rootDirectory,
        string? manifestFileName = null,
        string? distributionFileName = null)
    {
        var root = RequireDirectory(rootDirectory);
        var launcherFileName = RequireManifestFileName(manifestFileName ?? FileName);
        var distributionName = RequireDistributionFileName(distributionFileName ?? DistributionFileName);
        var path = Path.Combine(root, distributionName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Game distribution manifest is missing: {distributionName}", path);
        var manifest = JsonSerializer.Deserialize<GameDistributionManifest>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException("The game distribution manifest is empty.");
        if (manifest.SchemaVersion != GameDistributionManifest.CurrentSchemaVersion || manifest.GameId == Guid.Empty ||
            string.IsNullOrWhiteSpace(manifest.Product) || string.IsNullOrWhiteSpace(manifest.Version) ||
            !string.Equals(manifest.Platform, "windows-x64", StringComparison.Ordinal) || manifest.Files.Count == 0)
            throw new InvalidDataException("The game distribution manifest is invalid.");
        foreach (var file in manifest.Files)
        {
            if (!IsSafeRelativePath(file.Path) || file.Bytes < 0 || !IsSha256(file.Sha256))
                throw new InvalidDataException($"Game distribution entry is invalid: {file.Path}");
            var resolved = ResolveRequiredFile(root, file.Path, "Game distribution file");
            if (new FileInfo(resolved).Length != file.Bytes)
                throw new InvalidDataException($"Game distribution file size does not match: {file.Path}");
            RequireHash(resolved, file.Sha256, $"Game distribution file '{file.Path}'");
        }

        var launcher = Load(root, verifyHashes: false, launcherFileName);
        var indexedPaths = manifest.Files.Select(file => file.Path).ToHashSet(StringComparer.Ordinal);
        var requiredPaths = new List<string>
        {
            launcherFileName,
            launcher.ExecutableFile,
            launcher.RuntimeExecutable,
            "runtime/OpenRPG_Forge_Runtime.pck",
            launcher.ModulePackage,
        };
        if (launcher.BackgroundImage is not null) requiredPaths.Add(launcher.BackgroundImage);
        if (launcher.Music is not null) requiredPaths.Add(launcher.Music);
        var missingRequiredPath = requiredPaths.FirstOrDefault(path => !indexedPaths.Contains(path));
        if (missingRequiredPath is not null)
            throw new InvalidDataException(
                $"Game distribution manifest does not cover required file: {missingRequiredPath}");
        return manifest;
    }

    public static GameLauncherManifestFileSet ForExecutable(string executableFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executableFileName);
        if (!string.Equals(Path.GetFileName(executableFileName), executableFileName, StringComparison.Ordinal) ||
            !Path.GetExtension(executableFileName).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Launcher executable name must be a simple .exe file name.",
                nameof(executableFileName));
        var stem = Path.GetFileNameWithoutExtension(executableFileName);
        return new($"{stem}.game-launcher.json", $"{stem}.game-distribution-manifest.json");
    }

    public static GameLauncherManifestFileSet ResolveFileSet(string rootDirectory, string executableFileName)
    {
        var root = RequireDirectory(rootDirectory);
        var specific = ForExecutable(executableFileName);
        var hasManifest = File.Exists(Path.Combine(root, specific.ManifestFileName));
        var hasDistribution = File.Exists(Path.Combine(root, specific.DistributionFileName));
        if (hasManifest != hasDistribution)
            throw new InvalidDataException(
                $"Executable-specific launcher manifests are incomplete for '{executableFileName}'.");
        if (hasManifest) return specific;

        var fallback = new GameLauncherManifestFileSet(FileName, DistributionFileName);
        var fallbackManifest = Load(root, verifyHashes: false, fallback.ManifestFileName);
        if (!string.Equals(fallbackManifest.ExecutableFile, executableFileName,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                $"No launcher manifest pair matches executable '{executableFileName}'.");
        return fallback;
    }

    public static IReadOnlyList<string> Validate(GameLauncherManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var diagnostics = new List<string>();
        if (manifest.SchemaVersion != GameLauncherManifest.CurrentSchemaVersion)
            diagnostics.Add("launcher.schema_unsupported");
        if (manifest.GameId == Guid.Empty) diagnostics.Add("launcher.game_id_invalid");
        if (!IsSlug(manifest.Slug)) diagnostics.Add("launcher.slug_invalid");
        if (string.IsNullOrWhiteSpace(manifest.DisplayName) || manifest.DisplayName.Length > 80)
            diagnostics.Add("launcher.display_name_invalid");
        if (string.IsNullOrWhiteSpace(manifest.Tagline) || manifest.Tagline.Length > 180)
            diagnostics.Add("launcher.tagline_invalid");
        if (string.IsNullOrWhiteSpace(manifest.Version)) diagnostics.Add("launcher.version_invalid");
        if (!IsRgbColor(manifest.AccentColor)) diagnostics.Add("launcher.accent_invalid");
        foreach (var path in new[] { manifest.ExecutableFile, manifest.RuntimeExecutable, manifest.ModulePackage })
            if (!IsSafeRelativePath(path)) diagnostics.Add("launcher.path_unsafe");
        if (manifest.BackgroundImage is not null && !IsSafeRelativePath(manifest.BackgroundImage))
            diagnostics.Add("launcher.background_unsafe");
        if (manifest.Music is not null &&
            (!IsSafeRelativePath(manifest.Music) ||
             !Path.GetExtension(manifest.Music).Equals(".wav", StringComparison.OrdinalIgnoreCase)))
            diagnostics.Add("launcher.music_unsafe");
        foreach (var hash in new[] { manifest.LauncherSha256, manifest.RuntimeSha256, manifest.ModuleSha256 })
            if (!IsSha256(hash)) diagnostics.Add("launcher.hash_invalid");
        if (!IsOptionalHttpUrl(manifest.NewsUrl) || !IsOptionalHttpUrl(manifest.SupportUrl))
            diagnostics.Add("launcher.url_invalid");
        if (!Enum.IsDefined(manifest.Multiplayer)) diagnostics.Add("launcher.multiplayer_invalid");
        if (manifest.DefaultServerPort is < 1024 or > 65_535) diagnostics.Add("launcher.port_invalid");
        return diagnostics.Distinct(StringComparer.Ordinal).ToArray();
    }

    internal static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    internal static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    internal static bool IsSafeRelativePath(string path) =>
        !string.IsNullOrWhiteSpace(path) &&
        !Path.IsPathRooted(path) &&
        !path.Contains('\\') &&
        path.Split('/').All(segment => segment is not "" and not "." and not "..");

    private static string RequireDirectory(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        var root = Path.GetFullPath(rootDirectory);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Game launcher root does not exist: {root}");
        }

        return root;
    }

    private static string RequireManifestFileName(string value) =>
        RequireJsonFileName(value, ".game-launcher.json", FileName);

    private static string RequireDistributionFileName(string value) =>
        RequireJsonFileName(value, ".game-distribution-manifest.json", DistributionFileName);

    private static string RequireJsonFileName(string value, string specificSuffix, string defaultName)
    {
        if (!string.Equals(Path.GetFileName(value), value, StringComparison.Ordinal) ||
            !value.Equals(defaultName, StringComparison.Ordinal) &&
            !value.EndsWith(specificSuffix, StringComparison.Ordinal))
            throw new ArgumentException($"Launcher manifest file name is invalid: {value}", nameof(value));
        return value;
    }

    private static string ResolveRequiredFile(string root, string relativePath, string label)
    {
        if (!IsSafeRelativePath(relativePath))
        {
            throw new InvalidDataException($"{label} path is unsafe: {relativePath}");
        }

        var candidate = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(candidate))
        {
            throw new FileNotFoundException($"{label} is missing: {relativePath}", candidate);
        }

        return candidate;
    }

    private static void RequireHash(string path, string expected, string label)
    {
        var actual = Sha256(path);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{label} SHA-256 does not match {FileName}.");
        }
    }

    private static bool IsSlug(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 80 && value.All(character =>
            char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '-');

    private static bool IsRgbColor(string value) =>
        value.Length == 7 && value[0] == '#' && value.AsSpan(1).IndexOfAnyExcept("0123456789ABCDEFabcdef") < 0;

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.AsSpan().IndexOfAnyExcept("0123456789ABCDEFabcdef") < 0;

    private static bool IsOptionalHttpUrl(string? value) =>
        value is null || Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    private static void WriteAtomic(string path, string content)
    {
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}

public sealed record GameLauncherManifestFileSet(
    string ManifestFileName,
    string DistributionFileName);
