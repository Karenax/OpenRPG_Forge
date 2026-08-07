using System.Diagnostics;

namespace Forge.ArchitectureGraph;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        try
        {
            var options = CommandOptions.Parse(args);
            var service = new ArchitectureGraphService();
            return options.Command switch
            {
                "generate" => Generate(service, options),
                "check" => Check(service, options),
                "stats" => Stats(service, options),
                "query" => Query(service, options),
                _ => Usage($"Unknown command: {options.Command}"),
            };
        }
        catch (CommandLineException exception)
        {
            return Usage(exception.Message);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Architecture graph failed: {exception.Message}");
            return 1;
        }
    }

    private static int Generate(ArchitectureGraphService service, CommandOptions options)
    {
        var (graph, elapsed) = service.Generate(options.Root, options.Output);
        var paths = ArchitectureGraphService.ResolveOutputPaths(options.Root, options.Output);
        Console.WriteLine(
            $"Roslyn graph generated in {elapsed.TotalSeconds:F2}s: "
            + $"{DisplayPath(options.Root, paths.GraphPath)} "
            + $"({graph.Nodes.Count} nodes, {graph.Edges.Count} relations, "
            + $"{graph.Diagnostics.Count} diagnostics)");
        Console.WriteLine($"Overview generated: {DisplayPath(options.Root, paths.OverviewPath)}");
        return 0;
    }

    private static int Check(ArchitectureGraphService service, CommandOptions options)
    {
        var timer = Stopwatch.StartNew();
        var result = service.Check(options.Root, options.Output, options.Full);
        timer.Stop();
        foreach (var message in result.Messages)
        {
            var output = result.IsFresh ? Console.Out : Console.Error;
            output.WriteLine(message);
        }

        Console.WriteLine($"Check duration: {timer.Elapsed.TotalSeconds:F2}s");
        return result.IsFresh ? 0 : 1;
    }

    private static int Stats(ArchitectureGraphService service, CommandOptions options)
    {
        var graph = service.Load(options.Root, options.Output);
        Console.WriteLine($"schema_version: {graph.Metadata.SchemaVersion}");
        Console.WriteLine($"engine: {graph.Metadata.Engine} {graph.Metadata.EngineVersion}");
        Console.WriteLine($"source_digest: {graph.Metadata.SourceDigest}");
        Console.WriteLine($"source_files: {graph.Metadata.SourceFileCount}");
        Console.WriteLine($"nodes_total: {graph.Nodes.Count}");
        foreach (var group in graph.Nodes.GroupBy(node => node.Kind, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"nodes.{group.Key}: {group.Count()}");
        }

        Console.WriteLine($"edges_total: {graph.Edges.Count}");
        foreach (var group in graph.Edges.GroupBy(edge => edge.Kind, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"edges.{group.Key}: {group.Count()}");
        }

        Console.WriteLine($"diagnostics: {graph.Diagnostics.Count}");
        return 0;
    }

    private static int Query(ArchitectureGraphService service, CommandOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Query))
        {
            throw new CommandLineException("query requires a module, symbol, path, route, or concept.");
        }

        var timer = Stopwatch.StartNew();
        var graph = service.Load(options.Root, options.Output);
        var engine = new GraphQueryEngine();
        var result = engine.Query(
            graph,
            options.Query,
            options.Mode,
            options.Depth,
            options.MaxNodes);
        var formatted = engine.Format(result, options.Format);
        timer.Stop();
        Console.WriteLine(formatted);
        if (options.Format is not QueryFormat.Paths and not QueryFormat.Json)
        {
            Console.WriteLine($"Query duration: {timer.Elapsed.TotalMilliseconds:F0}ms");
        }

        return 0;
    }

    private static int Usage(string? error = null)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            Console.Error.WriteLine(error);
        }

        Console.Error.WriteLine(
            "Usage:\n"
            + "  dotnet run --project tools/Forge.ArchitectureGraph -- generate [--root PATH] [--output PATH]\n"
            + "  dotnet run --project tools/Forge.ArchitectureGraph -- check [--full] [--root PATH] [--output PATH]\n"
            + "  dotnet run --project tools/Forge.ArchitectureGraph -- stats [--root PATH] [--output PATH]\n"
            + "  dotnet run --project tools/Forge.ArchitectureGraph -- query \"SEARCH\" "
            + "[--mode impact|dependencies|dependents|tests] [--format text|paths|json|mermaid|context] "
            + "[--depth N] [--max-nodes N]");
        return 2;
    }

    private static string DisplayPath(string root, string path)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
        return relative.StartsWith("..", StringComparison.Ordinal)
            ? Path.GetFullPath(path)
            : SourceScanner.NormalizePath(relative);
    }
}

internal sealed record CommandOptions(
    string Command,
    string Root,
    string Output,
    string? Query,
    QueryMode Mode,
    QueryFormat Format,
    int Depth,
    int MaxNodes,
    bool Full)
{
    public static CommandOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            throw new CommandLineException("A command is required.");
        }

        var command = args[0].ToLowerInvariant();
        var root = Environment.CurrentDirectory;
        var output = ArchitectureGraphService.DefaultOutput;
        string? query = null;
        var mode = QueryMode.Impact;
        var format = QueryFormat.Text;
        var depth = 2;
        var maxNodes = 50;
        var full = false;

        for (var index = 1; index < args.Count; index++)
        {
            var argument = args[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                if (command == "query" && query is null)
                {
                    query = argument;
                    continue;
                }

                throw new CommandLineException($"Unexpected argument: {argument}");
            }

            switch (argument)
            {
                case "--root":
                    root = RequireValue(args, ref index, argument);
                    break;
                case "--output":
                    output = RequireValue(args, ref index, argument);
                    break;
                case "--depth":
                    depth = ParseNonNegative(RequireValue(args, ref index, argument), argument);
                    break;
                case "--max-nodes":
                    maxNodes = ParsePositive(RequireValue(args, ref index, argument), argument);
                    break;
                case "--mode":
                    mode = ParseEnum<QueryMode>(RequireValue(args, ref index, argument), argument);
                    break;
                case "--format":
                    format = ParseEnum<QueryFormat>(RequireValue(args, ref index, argument), argument);
                    break;
                case "--full":
                    full = true;
                    break;
                default:
                    throw new CommandLineException($"Unknown option: {argument}");
            }
        }

        root = Path.GetFullPath(root);
        if (!Directory.Exists(root))
        {
            throw new CommandLineException($"Repository root does not exist: {root}");
        }

        return new CommandOptions(
            command,
            root,
            output,
            query,
            mode,
            format,
            depth,
            maxNodes,
            full);
    }

    private static string RequireValue(IReadOnlyList<string> args, ref int index, string option)
    {
        index++;
        if (index >= args.Count)
        {
            throw new CommandLineException($"{option} requires a value.");
        }

        return args[index];
    }

    private static int ParseNonNegative(string value, string option)
    {
        if (!int.TryParse(value, out var parsed) || parsed < 0)
        {
            throw new CommandLineException($"{option} must be a non-negative integer.");
        }

        return parsed;
    }

    private static int ParsePositive(string value, string option)
    {
        var parsed = ParseNonNegative(value, option);
        if (parsed == 0)
        {
            throw new CommandLineException($"{option} must be greater than zero.");
        }

        return parsed;
    }

    private static T ParseEnum<T>(string value, string option)
        where T : struct, Enum
    {
        if (!Enum.TryParse<T>(value, ignoreCase: true, out var parsed))
        {
            throw new CommandLineException($"Invalid {option} value: {value}");
        }

        return parsed;
    }
}

internal sealed class CommandLineException(string message) : Exception(message);
