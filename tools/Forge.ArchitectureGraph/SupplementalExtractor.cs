using System.Text.RegularExpressions;

namespace Forge.ArchitectureGraph;

public sealed partial class SupplementalExtractor
{
    public void Analyze(
        IReadOnlyList<SourceDocument> sources,
        GraphBuilder builder)
    {
        var knownPaths = sources.Select(source => source.Path).ToHashSet(StringComparer.Ordinal);
        AnalyzeMarkdown(sources, knownPaths, builder);
        AnalyzeCpp(sources, knownPaths, builder);
        AnalyzeGodot(sources, knownPaths, builder);
        AnalyzePython(sources, builder);
    }

    private static void AnalyzeMarkdown(
        IReadOnlyList<SourceDocument> sources,
        IReadOnlySet<string> knownPaths,
        GraphBuilder builder)
    {
        foreach (var source in sources.Where(item => item.Suffix == ".md"))
        {
            foreach (Match match in MarkdownLinkPattern().Matches(source.Text))
            {
                AddRelativeReference(
                    source,
                    match.Groups["target"].Value,
                    match.Index,
                    "references",
                    knownPaths,
                    builder);
            }

            foreach (Match match in InlineCodePattern().Matches(source.Text))
            {
                var target = match.Groups["target"].Value;
                if (target.Contains('/') || Path.HasExtension(target))
                {
                    AddRelativeReference(
                        source,
                        target,
                        match.Index,
                        "references",
                        knownPaths,
                        builder);
                }
            }
        }
    }

    private static void AnalyzeCpp(
        IReadOnlyList<SourceDocument> sources,
        IReadOnlySet<string> knownPaths,
        GraphBuilder builder)
    {
        var suffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cc", ".cpp", ".cxx", ".h", ".hpp",
        };
        foreach (var source in sources.Where(item => suffixes.Contains(item.Suffix)))
        {
            foreach (Match match in CppIncludePattern().Matches(source.Text))
            {
                AddRelativeReference(
                    source,
                    match.Groups["target"].Value,
                    match.Index,
                    "imports",
                    knownPaths,
                    builder);
            }
        }
    }

    private static void AnalyzeGodot(
        IReadOnlyList<SourceDocument> sources,
        IReadOnlySet<string> knownPaths,
        GraphBuilder builder)
    {
        var suffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cfg", ".gd", ".godot", ".tres", ".tscn",
        };
        foreach (var source in sources.Where(item => suffixes.Contains(item.Suffix)))
        {
            foreach (Match match in GodotResourcePattern().Matches(source.Text))
            {
                var projectDirectory = FindGodotProjectDirectory(source.Path, knownPaths);
                var target = NormalizeRelativePath(projectDirectory, match.Groups["target"].Value);
                if (knownPaths.Contains(target))
                {
                    builder.AddEdge(
                        $"file:{source.Path}",
                        $"file:{target}",
                        "imports",
                        "syntactic",
                        source.Path,
                        LineNumber(source.Text, match.Index));
                }
            }

            if (source.Suffix == ".cfg")
            {
                foreach (Match match in GodotPluginScriptPattern().Matches(source.Text))
                {
                    AddRelativeReference(
                        source,
                        match.Groups["target"].Value,
                        match.Index,
                        "imports",
                        knownPaths,
                        builder);
                }
            }
        }
    }

    private static void AnalyzePython(
        IReadOnlyList<SourceDocument> sources,
        GraphBuilder builder)
    {
        var pythonSources = sources.Where(item => item.Suffix == ".py").ToArray();
        var modules = pythonSources.ToDictionary(
            source => ModuleName(source.Path),
            source => source,
            StringComparer.Ordinal);
        foreach (var (moduleName, source) in modules.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var moduleId = $"module:{moduleName}";
            builder.AddNode(new GraphNode(moduleId, "module", moduleName, source.Path, 1));
            builder.AddEdge(moduleId, $"file:{source.Path}", "defined_in", "syntactic", source.Path, 1);

            foreach (Match match in PythonImportPattern().Matches(source.Text))
            {
                var imported = match.Groups["module"].Value;
                var target = modules
                    .Where(pair => imported == pair.Key || imported.StartsWith($"{pair.Key}.", StringComparison.Ordinal))
                    .OrderByDescending(pair => pair.Key.Length)
                    .Select(pair => pair.Value)
                    .FirstOrDefault();
                if (target is null)
                {
                    continue;
                }

                builder.AddEdge(
                    $"file:{source.Path}",
                    $"file:{target.Path}",
                    "imports",
                    "syntactic",
                    source.Path,
                    LineNumber(source.Text, match.Index));
            }
        }
    }

    private static void AddRelativeReference(
        SourceDocument source,
        string rawTarget,
        int offset,
        string kind,
        IReadOnlySet<string> knownPaths,
        GraphBuilder builder)
    {
        var cleanTarget = rawTarget
            .Trim()
            .Trim('<', '>')
            .Split(['#', '?'], 2)[0];
        if (string.IsNullOrWhiteSpace(cleanTarget)
            || Uri.TryCreate(cleanTarget, UriKind.Absolute, out _)
            || cleanTarget.IndexOfAny(['*', '{', '}', '[', ']']) >= 0)
        {
            return;
        }

        var directory = SourceScanner.NormalizePath(Path.GetDirectoryName(source.Path) ?? string.Empty);
        var target = NormalizeRelativePath(directory, cleanTarget);
        if (!knownPaths.Contains(target))
        {
            return;
        }

        builder.AddEdge(
            $"file:{source.Path}",
            $"file:{target}",
            kind,
            "syntactic",
            source.Path,
            LineNumber(source.Text, offset));
    }

    internal static string NormalizeRelativePath(string directory, string target)
    {
        var segments = new List<string>();
        if (!string.IsNullOrWhiteSpace(directory))
        {
            segments.AddRange(directory.Split('/', StringSplitOptions.RemoveEmptyEntries));
        }

        foreach (var segment in target.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count > 0)
                {
                    segments.RemoveAt(segments.Count - 1);
                }

                continue;
            }

            segments.Add(segment);
        }

        return string.Join('/', segments);
    }

    private static string FindGodotProjectDirectory(
        string sourcePath,
        IReadOnlySet<string> knownPaths)
    {
        var directory = SourceScanner.NormalizePath(Path.GetDirectoryName(sourcePath) ?? string.Empty);
        while (true)
        {
            var candidate = string.IsNullOrEmpty(directory)
                ? "project.godot"
                : $"{directory}/project.godot";
            if (knownPaths.Contains(candidate))
            {
                return directory;
            }

            var slash = directory.LastIndexOf('/');
            if (slash < 0)
            {
                return string.Empty;
            }

            directory = directory[..slash];
        }
    }

    private static int LineNumber(string text, int offset)
    {
        var line = 1;
        for (var index = 0; index < offset && index < text.Length; index++)
        {
            if (text[index] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    private static string ModuleName(string path)
    {
        var withoutSuffix = path[..^Path.GetExtension(path).Length].Replace('/', '.');
        return withoutSuffix.EndsWith(".__init__", StringComparison.Ordinal)
            ? withoutSuffix[..^9]
            : withoutSuffix;
    }

    [GeneratedRegex(@"\[[^\]]*\]\((?<target>[^)]+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownLinkPattern();

    [GeneratedRegex(@"`(?<target>[^`\r\n]+)`", RegexOptions.CultureInvariant)]
    private static partial Regex InlineCodePattern();

    [GeneratedRegex("^\\s*#\\s*include\\s*\"(?<target>[^\"\\r\\n]+)\"", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex CppIncludePattern();

    [GeneratedRegex("\"res://(?<target>[^\"\\r\\n]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex GodotResourcePattern();

    [GeneratedRegex("^\\s*script\\s*=\\s*\"(?<target>[^\"\\r\\n]+)\"\\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex GodotPluginScriptPattern();

    [GeneratedRegex("^\\s*(?:from\\s+(?<module>[A-Za-z_]\\w*(?:\\.[A-Za-z_]\\w*)*)\\s+import|import\\s+(?<module>[A-Za-z_]\\w*(?:\\.[A-Za-z_]\\w*)*))", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex PythonImportPattern();
}
