using System.Text;
using System.Text.Json.Nodes;
using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Workspaces;

public static class EditorWorkspaceProfileStore
{
    public const string RelativePath = ".forge/editor-profile.json";
    public const string CatalogRelativePath = ".forge/editor-profiles.json";

    public static void Save(string workspaceRoot, EditorWorkspaceProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(profile);
        var path = Path.Combine(Path.GetFullPath(workspaceRoot), RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporary, EditorWorkspaceProfileJsonSerializer.Serialize(profile) + Environment.NewLine,
            new UTF8Encoding(false));
        File.Move(temporary, path, overwrite: true);
        var catalog = TryLoadCatalog(workspaceRoot);
        var profiles = catalog?.Profiles.Where(candidate => candidate.Id != profile.Id).Append(profile).ToArray() ??
            Enum.GetValues<EditorRole>().Select(role =>
                role == profile.Role ? profile : EditorWorkspaceProfile.CreateDefault(role, profile.ActiveAreaId)).ToArray();
        SaveCatalog(workspaceRoot, profile.Id, profiles);
    }

    public static EditorWorkspaceProfile? TryLoad(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var path = Path.Combine(Path.GetFullPath(workspaceRoot), RelativePath);
        return File.Exists(path)
            ? EditorWorkspaceProfileJsonSerializer.Deserialize(File.ReadAllText(path, Encoding.UTF8))
            : null;
    }

    public static EditorWorkspaceProfileCatalog? TryLoadCatalog(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var path = Path.Combine(Path.GetFullPath(workspaceRoot), CatalogRelativePath);
        if (!File.Exists(path)) return null;
        var root = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8))?.AsObject() ??
                   throw new InvalidDataException("Editor profile catalog is empty.");
        var schemaVersion = root["schemaVersion"]?.GetValue<int>() ?? 0;
        if (schemaVersion != EditorWorkspaceProfileCatalog.CurrentSchemaVersion)
            throw new InvalidDataException($"Editor profile catalog schema '{schemaVersion}' is unsupported.");
        var activeId = Guid.Parse(root["activeProfileId"]?.GetValue<string>() ??
                                  throw new InvalidDataException("Editor profile catalog active UUID is missing."));
        var profiles = root["profiles"]?.AsArray().Select(node =>
            EditorWorkspaceProfileJsonSerializer.Deserialize(node?.ToJsonString() ??
                throw new InvalidDataException("Editor profile catalog contains an empty profile."))).ToArray() ?? [];
        return new(EditorWorkspaceProfileId.From(activeId), profiles);
    }

    public static void SaveCatalog(string workspaceRoot, EditorWorkspaceProfileId activeProfileId,
        IReadOnlyList<EditorWorkspaceProfile> profiles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(activeProfileId);
        ArgumentNullException.ThrowIfNull(profiles);
        if (profiles.Count is < 1 or > 32 || profiles.Select(profile => profile.Id).Distinct().Count() != profiles.Count ||
            profiles.All(profile => profile.Id != activeProfileId))
            throw new ArgumentException("Editor profile catalog must contain unique profiles and its active UUID.", nameof(profiles));
        var root = new JsonObject
        {
            ["schemaVersion"] = EditorWorkspaceProfileCatalog.CurrentSchemaVersion,
            ["activeProfileId"] = activeProfileId.Value.ToString("D"),
            ["profiles"] = new JsonArray(profiles.OrderBy(profile => profile.Role).ThenBy(profile => profile.Name,
                StringComparer.Ordinal).Select(profile => JsonNode.Parse(
                    EditorWorkspaceProfileJsonSerializer.Serialize(profile))).ToArray()),
        };
        var path = Path.Combine(Path.GetFullPath(workspaceRoot), CatalogRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporary, root.ToJsonString(new() { WriteIndented = true }) + Environment.NewLine,
            new UTF8Encoding(false));
        File.Move(temporary, path, overwrite: true);
    }
}

public sealed record EditorWorkspaceProfileCatalog
{
    public const int CurrentSchemaVersion = 1;

    public EditorWorkspaceProfileId ActiveProfileId { get; }
    public IReadOnlyList<EditorWorkspaceProfile> Profiles { get; }

    public EditorWorkspaceProfileCatalog(EditorWorkspaceProfileId activeProfileId,
        IReadOnlyList<EditorWorkspaceProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(activeProfileId);
        ArgumentNullException.ThrowIfNull(profiles);
        if (profiles.Count is < 1 or > 32 || profiles.Select(profile => profile.Id).Distinct().Count() != profiles.Count ||
            profiles.All(profile => profile.Id != activeProfileId))
            throw new ArgumentException("Editor profile catalog is invalid.", nameof(profiles));
        ActiveProfileId = activeProfileId;
        Profiles = profiles.OrderBy(profile => profile.Role).ThenBy(profile => profile.Name, StringComparer.Ordinal).ToArray();
    }
}
