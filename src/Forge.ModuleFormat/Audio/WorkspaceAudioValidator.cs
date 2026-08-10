using System.Security.Cryptography;
using Forge.Domain.Audio;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.ModuleFormat.Audio;

public static class WorkspaceAudioValidator
{
    public const string ResourceUnsafeCode = "audio.resource.path_unsafe";
    public const string ResourceMissingCode = "audio.resource.missing";
    public const string ResourceSizeCode = "audio.resource.size_mismatch";
    public const string ResourceHashCode = "audio.resource.hash_mismatch";

    public static IReadOnlyList<DomainDiagnostic> Validate(string workspacePath, ProjectDefinition project)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentNullException.ThrowIfNull(project);
        var root = Path.GetFullPath(workspacePath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var diagnostics = new List<DomainDiagnostic>();
        foreach (var area in project.Areas)
        {
            diagnostics.AddRange(AudioEmitterCatalogValidator.Validate(
                area.Environment.Audio.Emitters,
                area.Environment.Audio.Orchestration.MixBuses.Select(item => item.Id).ToHashSet(),
                path: $"areas/{area.Id}/environment/audio/emitters"));
            for (var definitionIndex = 0; definitionIndex < area.Environment.Audio.Emitters.Definitions.Count; definitionIndex++)
            {
                var definition = area.Environment.Audio.Emitters.Definitions[definitionIndex];
                for (var clipIndex = 0; clipIndex < definition.ClipSet.Clips.Count; clipIndex++)
                {
                    var clip = definition.ClipSet.Clips[clipIndex];
                    if (clip.ResourcePath.StartsWith("builtin://", StringComparison.Ordinal)) continue;
                    var path = $"areas/{area.Id}/environment/audio/emitters/definitions/{definitionIndex}/clipSet/clips/{clipIndex}/resourcePath";
                    var absolute = Path.GetFullPath(Path.Combine(workspacePath,
                        clip.ResourcePath.Replace('/', Path.DirectorySeparatorChar)));
                    if (!absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    {
                        diagnostics.Add(new DomainDiagnostic(ResourceUnsafeCode,
                            $"Audio resource '{clip.ResourcePath}' escapes the workspace.", path));
                        continue;
                    }
                    if (!File.Exists(absolute))
                    {
                        diagnostics.Add(new DomainDiagnostic(ResourceMissingCode,
                            $"Audio resource '{clip.ResourcePath}' is missing.", path));
                        continue;
                    }
                    var bytes = File.ReadAllBytes(absolute);
                    if (bytes.LongLength != clip.MemoryBytes)
                        diagnostics.Add(new DomainDiagnostic(ResourceSizeCode,
                            $"Audio resource '{clip.ResourcePath}' size does not match declared memory bytes.", path));
                    var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                    if (!string.Equals(hash, clip.ContentSha256, StringComparison.OrdinalIgnoreCase))
                        diagnostics.Add(new DomainDiagnostic(ResourceHashCode,
                            $"Audio resource '{clip.ResourcePath}' does not match its SHA-256.", path));
                }
            }
        }
        return diagnostics;
    }
}
