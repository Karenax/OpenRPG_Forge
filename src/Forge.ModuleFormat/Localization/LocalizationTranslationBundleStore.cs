using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Application.Localization;

namespace Forge.ModuleFormat.Localization;

public static class LocalizationTranslationBundleStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static void Save(string path, LocalizationTranslationBundle bundle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(bundle);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath) ?? throw new IOException("Translation bundle has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(bundle, Options) + Environment.NewLine,
                new UTF8Encoding(false));
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public static LocalizationTranslationBundle Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return JsonSerializer.Deserialize<LocalizationTranslationBundle>(File.ReadAllText(Path.GetFullPath(path)), Options)
            ?? throw new JsonException("Localization translation bundle is empty.");
    }
}
