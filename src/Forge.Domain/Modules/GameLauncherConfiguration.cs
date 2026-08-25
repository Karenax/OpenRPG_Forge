using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Forge.Domain.Modules;

public enum GameLauncherMultiplayerMode
{
    Disabled,
    LocalHost,
    DirectConnectPreview,
}

public sealed record GameLauncherConfiguration
{
    public const int CurrentSchemaVersion = 1;

    private static readonly HashSet<string> ReservedWindowsNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public GameLauncherConfigurationId Id { get; }

    public int SchemaVersion { get; }

    public bool Enabled { get; }

    public string DisplayName { get; }

    public string Tagline { get; }

    public string ExecutableName { get; }

    public string AccentColor { get; }

    public string? BackgroundImagePath { get; }

    public string? MusicPath { get; }

    public string? NewsUrl { get; }

    public string? SupportUrl { get; }

    public bool SettingsEnabled { get; }

    public GameLauncherMultiplayerMode Multiplayer { get; }

    public int DefaultServerPort { get; }

    public string Provenance { get; }

    public GameLauncherConfiguration(
        GameLauncherConfigurationId id,
        bool enabled,
        string displayName,
        string tagline,
        string executableName,
        string accentColor,
        string? backgroundImagePath,
        string? newsUrl,
        string? supportUrl,
        bool settingsEnabled,
        GameLauncherMultiplayerMode multiplayer,
        int defaultServerPort,
        string provenance,
        string? musicPath = null,
        int schemaVersion = CurrentSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(tagline);
        ArgumentException.ThrowIfNullOrWhiteSpace(executableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(accentColor);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                $"Game launcher schema '{schemaVersion}' is not supported.");
        }

        var normalizedDisplayName = displayName.Trim();
        var normalizedTagline = tagline.Trim();
        var normalizedExecutableName = executableName.Trim();
        if (normalizedDisplayName.Length > 80)
        {
            throw new ArgumentException("Game launcher display names cannot exceed 80 characters.", nameof(displayName));
        }

        if (normalizedTagline.Length > 180)
        {
            throw new ArgumentException("Game launcher taglines cannot exceed 180 characters.", nameof(tagline));
        }

        if (!IsSafeExecutableName(normalizedExecutableName))
        {
            throw new ArgumentException(
                "The game launcher executable name must be a safe Windows file name without an extension.",
                nameof(executableName));
        }

        var normalizedAccent = accentColor.Trim().ToUpperInvariant();
        if (!IsRgbColor(normalizedAccent))
        {
            throw new ArgumentException("The game launcher accent color must use #RRGGBB.", nameof(accentColor));
        }

        var normalizedBackground = NormalizeOptional(backgroundImagePath);
        if (normalizedBackground is not null &&
            (!IsSafeRelativePath(normalizedBackground) || !IsSupportedImageExtension(normalizedBackground)))
        {
            throw new ArgumentException(
                "The launcher background must be a safe relative PNG or JPEG project path.",
                nameof(backgroundImagePath));
        }

        var normalizedMusic = NormalizeOptional(musicPath);
        if (normalizedMusic is not null &&
            (!IsSafeRelativePath(normalizedMusic) || !IsSupportedMusicExtension(normalizedMusic)))
        {
            throw new ArgumentException(
                "The launcher music must be a safe relative WAV project path.",
                nameof(musicPath));
        }

        var normalizedNewsUrl = NormalizeOptionalUrl(newsUrl, nameof(newsUrl));
        var normalizedSupportUrl = NormalizeOptionalUrl(supportUrl, nameof(supportUrl));
        if (!Enum.IsDefined(multiplayer))
        {
            throw new ArgumentOutOfRangeException(nameof(multiplayer), "The game launcher multiplayer mode is invalid.");
        }
        if (defaultServerPort is < 1024 or > 65_535)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultServerPort), "The launcher server port must be between 1024 and 65535.");
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Enabled = enabled;
        DisplayName = normalizedDisplayName;
        Tagline = normalizedTagline;
        ExecutableName = normalizedExecutableName;
        AccentColor = normalizedAccent;
        BackgroundImagePath = normalizedBackground;
        MusicPath = normalizedMusic;
        NewsUrl = normalizedNewsUrl;
        SupportUrl = normalizedSupportUrl;
        SettingsEnabled = settingsEnabled;
        Multiplayer = multiplayer;
        DefaultServerPort = defaultServerPort;
        Provenance = provenance.Trim();
    }

    public static GameLauncherConfiguration CreateDefault(ModuleId moduleId, string displayName)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        return new GameLauncherConfiguration(
            GameLauncherConfigurationId.From(CreateStableGuid(moduleId.ToString(), "game-launcher")),
            enabled: false,
            displayName,
            "Une aventure créée avec OpenRPG Forge",
            NormalizeExecutableName(displayName),
            "#C77A45",
            backgroundImagePath: null,
            newsUrl: null,
            supportUrl: null,
            settingsEnabled: true,
            GameLauncherMultiplayerMode.DirectConnectPreview,
            defaultServerPort: 5121,
            "migration-default");
    }

    public static GameLauncherConfiguration CreateDisabled(ModuleDistributionConfigurationId distributionId)
    {
        ArgumentNullException.ThrowIfNull(distributionId);
        return new GameLauncherConfiguration(
            GameLauncherConfigurationId.From(CreateStableGuid(distributionId.ToString(), "game-launcher")),
            enabled: false,
            "Jeu OpenRPG",
            "Une aventure créée avec OpenRPG Forge",
            "Jeu_OpenRPG",
            "#C77A45",
            backgroundImagePath: null,
            newsUrl: null,
            supportUrl: null,
            settingsEnabled: true,
            GameLauncherMultiplayerMode.DirectConnectPreview,
            defaultServerPort: 5121,
            "migration-default");
    }

    private static string NormalizeExecutableName(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var previousSeparator = false;
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousSeparator = false;
            }
            else if (!previousSeparator && builder.Length > 0)
            {
                builder.Append('_');
                previousSeparator = true;
            }
        }

        var result = builder.ToString().Trim('_');
        if (result.Length > 48)
        {
            result = result[..48].TrimEnd('_');
        }

        return string.IsNullOrWhiteSpace(result) || ReservedWindowsNames.Contains(result)
            ? "Jeu_OpenRPG"
            : result;
    }

    private static bool IsSafeExecutableName(string value)
    {
        if (value.Length is < 1 or > 64 || value.EndsWith(' ') || value.EndsWith('.') ||
            value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            value.Any(character => char.IsControl(character) || "<>:\"/\\|?*".Contains(character)))
        {
            return false;
        }

        var stem = value.Split('.', 2)[0];
        return !ReservedWindowsNames.Contains(stem);
    }

    private static bool IsRgbColor(string value) =>
        value.Length == 7 && value[0] == '#' && value.AsSpan(1).IndexOfAnyExcept("0123456789ABCDEF") < 0;

    private static bool IsSupportedImageExtension(string path) =>
        Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(path).Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(path).Equals(".jpeg", StringComparison.OrdinalIgnoreCase);

    private static bool IsSupportedMusicExtension(string path) =>
        Path.GetExtension(path).Equals(".wav", StringComparison.OrdinalIgnoreCase);

    private static bool IsSafeRelativePath(string path) =>
        !Path.IsPathRooted(path) &&
        !path.Contains('\\') &&
        path.Split('/').All(segment => segment is not "" and not "." and not "..");

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeOptionalUrl(string? value, string parameterName)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is null)
        {
            return null;
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("Launcher links must use an absolute HTTP or HTTPS URL.", parameterName);
        }

        return uri.AbsoluteUri;
    }

    private static Guid CreateStableGuid(string owner, string discriminator)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:{owner}:{discriminator}"));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[7] = (byte)((guidBytes[7] & 0x0f) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3f) | 0x80);
        return new Guid(guidBytes);
    }
}
