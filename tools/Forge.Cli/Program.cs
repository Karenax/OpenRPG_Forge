using System.Text.Json;
using Forge.ModuleFormat.Packages;

return Run(args);

static int Run(IReadOnlyList<string> arguments)
{
    try
    {
        if (arguments.Count >= 1 && arguments[0] == "package")
        {
            return RunPackage(arguments.Skip(1).ToArray());
        }

        Console.Error.WriteLine("Usage: forge package <export|validate|load> ...");
        return 2;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

static int RunPackage(IReadOnlyList<string> arguments)
{
    if (arguments.Count == 3 && arguments[0] == "export")
    {
        var result = ModulePackager.Export(arguments[1], arguments[2]);
        Console.WriteLine(
            $"OPENRPG_FORGE_MODULE_EXPORT_PASS slug={result.Manifest.Slug} files={result.FileCount} " +
            $"hash={result.Manifest.ContentSha256}");
        return 0;
    }

    if (arguments.Count == 2 && arguments[0] == "validate")
    {
        var result = ModulePackageValidator.Validate(arguments[1]);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            valid = result.IsValid,
            module = result.Manifest?.Slug,
            diagnostics = result.Diagnostics,
        }));
        if (result.IsValid)
        {
            Console.WriteLine($"OPENRPG_FORGE_MODULE_VALIDATION_PASS slug={result.Manifest!.Slug}");
        }

        return result.IsValid ? 0 : 1;
    }

    if (arguments.Count == 3 && arguments[0] == "load")
    {
        var result = ModulePackageLoader.Load(arguments[1], arguments[2]);
        Console.WriteLine(
            $"OPENRPG_FORGE_MODULE_LOAD_PASS slug={result.Manifest.Slug} " +
            $"cache_hit={result.CacheHit.ToString().ToLowerInvariant()}");
        return 0;
    }

    Console.Error.WriteLine(
        "Usage: forge package export <workspace> <destination.orpmod> | " +
        "validate <module.orpmod> | load <module.orpmod> <cache>");
    return 2;
}
