using System.IO.Compression;

namespace Forge.ModuleFormat.Packages;

public static class ModulePackageLoader
{
    public static ModuleLoadResult Load(
        string packagePath,
        string cacheRoot,
        ModulePackageLimits? limits = null,
        string availableRuntimeVersion = ModuleRuntimeCompatibility.CurrentRuntimeVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheRoot);
        var validation = ModulePackageValidator.Validate(packagePath, limits, availableRuntimeVersion);
        if (!validation.IsValid || validation.Manifest is null || validation.Project is null)
        {
            throw new InvalidDataException(
                $"Module package validation failed: {string.Join("; ", validation.Diagnostics.Select(d => d.Message))}");
        }

        var root = Path.GetFullPath(cacheRoot);
        Directory.CreateDirectory(root);
        var cachePath = Path.Combine(
            root,
            validation.Manifest.ModuleId.ToString(),
            validation.Manifest.ContentSha256);
        var projectPath = Path.Combine(cachePath, "content", "project.json");
        if (File.Exists(projectPath))
        {
            return new ModuleLoadResult(validation.Manifest, validation.Project, cachePath, true);
        }

        var moduleCacheRoot = Path.GetDirectoryName(cachePath)
            ?? throw new InvalidOperationException("Module cache path has no parent.");
        Directory.CreateDirectory(moduleCacheRoot);
        var temporaryPath = Path.Combine(moduleCacheRoot, $".{validation.Manifest.ContentSha256}.{Guid.NewGuid():N}.tmp");
        Directory.CreateDirectory(temporaryPath);
        try
        {
            using var archive = ZipFile.OpenRead(packagePath);
            foreach (var entry in archive.Entries.Where(entry => !entry.FullName.EndsWith("/", StringComparison.Ordinal)))
            {
                var target = Path.GetFullPath(Path.Combine(temporaryPath, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                if (!target.StartsWith(temporaryPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Archive entry '{entry.FullName}' escapes the cache root.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using var source = entry.Open();
                using var destination = File.Create(target);
                source.CopyTo(destination);
            }

            Directory.Move(temporaryPath, cachePath);
        }
        catch
        {
            if (Directory.Exists(temporaryPath))
            {
                Directory.Delete(temporaryPath, true);
            }

            throw;
        }

        return new ModuleLoadResult(validation.Manifest, validation.Project, cachePath, false);
    }
}
