namespace Forge.ArchitectureGraph.Tests;

public sealed class RoslynArchitectureGraphTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"forge-architecture-graph-{Guid.NewGuid():N}");

    public RoslynArchitectureGraphTests()
    {
        Directory.CreateDirectory(_root);
        WriteFixture();
    }

    [Fact]
    public void Analyze_ResolvesSemanticCrossProjectRelations()
    {
        var graph = BuildGraph();
        var edges = graph.Edges
            .Select(edge => (edge.Source, edge.Target, edge.Kind, edge.Confidence))
            .ToHashSet();

        Assert.True(
            edges.Contains((
                "service:Forge.Application.AreaService",
                "repository:Forge.Persistence.AreaRepository",
                "uses_repository",
                "semantic")),
            string.Join(Environment.NewLine, graph.Diagnostics.Select(item => $"DIAG {item.Path}:{item.Line} {item.Message}").Concat(edges.Where(edge =>
                edge.Source.Contains("Area", StringComparison.Ordinal)
                || edge.Target.Contains("Area", StringComparison.Ordinal)
                || edge.Source.StartsWith("project:", StringComparison.Ordinal)).Select(edge => edge.ToString()))));
        Assert.Contains(
            (
                "repository:Forge.Persistence.AreaRepository",
                "model:Forge.Domain.AreaDefinition",
                "accesses_model",
                "semantic"),
            edges);
        Assert.Contains(
            (
                "test:Forge.Tests.AreaServiceTests",
                "service:Forge.Application.AreaService",
                "tests",
                "semantic"),
            edges);
        Assert.Contains(
            (
                "file:src/Application/AreaService.cs",
                "file:src/Persistence/AreaRepository.cs",
                "imports",
                "semantic"),
            edges);
    }

    [Fact]
    public void Analyze_ProducesStableBytes()
    {
        var first = GraphStore.Serialize(BuildGraph());
        var second = GraphStore.Serialize(BuildGraph());

        Assert.Equal(first, second);
    }

    [Fact]
    public void Analyze_MergesPartialTypeDeclarationsAndKeepsEveryDefinitionEdge()
    {
        Write(
            "src/Application/PartialAreaService.cs",
            "namespace Forge.Application; public sealed partial class PartialAreaService { }\n");
        Write(
            "src/Application/PartialAreaService.Commands.cs",
            "namespace Forge.Application; public sealed partial class PartialAreaService { public void Run() { } }\n");

        var graph = BuildGraph();
        var nodeId = "service:Forge.Application.PartialAreaService";
        var definitionPaths = graph.Edges
            .Where(edge => edge.Source == nodeId && edge.Kind == "defined_in")
            .Select(edge => edge.Target)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Single(graph.Nodes, node => node.Id == nodeId);
        Assert.Contains("file:src/Application/PartialAreaService.cs", definitionPaths);
        Assert.Contains("file:src/Application/PartialAreaService.Commands.cs", definitionPaths);
        Assert.DoesNotContain(
            graph.Diagnostics,
            diagnostic => diagnostic.Message.Contains(nodeId, StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_StillReportsConflictingNonPartialDeclarations()
    {
        Write(
            "src/Application/ConflictingAreaService.cs",
            "namespace Forge.Application; public sealed class ConflictingAreaService { }\n");
        Write(
            "src/Application/ConflictingAreaService.Duplicate.cs",
            "namespace Forge.Application; public sealed class ConflictingAreaService { }\n");

        var graph = BuildGraph();
        const string nodeId = "service:Forge.Application.ConflictingAreaService";

        Assert.Contains(
            graph.Diagnostics,
            diagnostic => diagnostic.Message.Contains(nodeId, StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_IsIndependentOfCheckoutLineEndings()
    {
        var first = GraphStore.Serialize(BuildGraph());
        var path = Path.Combine(_root, "src", "Application", "AreaService.cs");
        var content = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        File.WriteAllText(
            path,
            content.Replace("\n", "\r\n", StringComparison.Ordinal),
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var second = GraphStore.Serialize(BuildGraph());

        Assert.Equal(first, second);
    }

    [Fact]
    public void Analyze_AcceptsUtf8BomInProjectFiles()
    {
        var path = Path.Combine(_root, "src", "Domain", "Domain.csproj");
        File.WriteAllText(
            path,
            "<Project Sdk=\"Microsoft.NET.Sdk\" />\n",
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var graph = BuildGraph();

        Assert.DoesNotContain(
            graph.Diagnostics,
            diagnostic => diagnostic.Message.StartsWith(
                "Invalid project XML",
                StringComparison.Ordinal));
    }

    [Fact]
    public void FastCheck_DetectsOnlyChangedManifestWithoutFullRebuild()
    {
        var service = new ArchitectureGraphService();
        var output = "artifacts/architecture-graph/roslyn/graph.json";
        service.Generate(_root, output);

        var fresh = service.Check(_root, output, full: false);
        Assert.True(fresh.IsFresh, string.Join(Environment.NewLine, fresh.Messages));

        Write(
            "src/Domain/AreaDefinition.cs",
            "namespace Forge.Domain; public sealed record AreaDefinition(string Id, string Name);\n");
        var stale = service.Check(_root, output, full: false);

        Assert.False(stale.IsFresh);
        Assert.Contains(
            stale.Messages,
            message => message == "Changed indexed source: src/Domain/AreaDefinition.cs");
    }

    [Fact]
    public void Generate_DoesNotIndexOfficialArtifacts()
    {
        var service = new ArchitectureGraphService();
        const string output = "docs/architecture/graph.json";

        service.Generate(_root, output);
        var first = File.ReadAllBytes(Path.Combine(_root, output.Replace('/', Path.DirectorySeparatorChar)));
        service.Generate(_root, output);
        var second = File.ReadAllBytes(Path.Combine(_root, output.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Equal(first, second);
        Assert.DoesNotContain(
            service.Load(_root, output).Nodes,
            node => node.Path is "docs/architecture/graph.json" or "docs/architecture/overview.mmd");
    }

    [Fact]
    public void Scanner_ExcludesDependenciesSecretsBuildsAndDatabases()
    {
        Write("node_modules/package/ignored.cs", "public sealed class Ignored {}\n");
        Write("src/Domain/bin/ignored.cs", "public sealed class IgnoredBuild {}\n");
        Write(".artifacts/local/editor-settings.json", "{}\n");
        Write("src/Domain/.forge/generated.json", "{}\n");
        Write("src/Domain/secrets.sqlite", "not a database fixture");
        Write(".env", "SECRET=ignored");
        Write("apps/Forge.Runtime/content/solaren/project.json", "{\"module\":\"content, not architecture\"}\n");

        var paths = new SourceScanner().Discover(_root).Select(source => source.Path).ToArray();

        Assert.DoesNotContain(paths, path => path.Contains("node_modules", StringComparison.Ordinal));
        Assert.DoesNotContain(paths, path => path.Contains("/bin/", StringComparison.Ordinal));
        Assert.DoesNotContain(paths, path => path.Contains(".artifacts", StringComparison.Ordinal));
        Assert.DoesNotContain(paths, path => path.Contains("/.forge/", StringComparison.Ordinal));
        Assert.DoesNotContain(paths, path => path.EndsWith(".sqlite", StringComparison.Ordinal));
        Assert.DoesNotContain(".env", paths);
        Assert.DoesNotContain(paths, path => path.StartsWith(
            "apps/Forge.Runtime/content/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_LinksMatchingHttpVerbAndNormalizedRouteOnly()
    {
        Write(
            "src/Application/AreaHttp.cs",
            """
            namespace Forge.Application;
            public sealed class AreaApiClient
            {
                private readonly System.Net.Http.HttpClient _http = new();
                public async System.Threading.Tasks.Task Load()
                {
                    await _http.GetAsync("/areas/{areaId}");
                    await _http.PostAsync("/areas/{areaId}", null);
                }
            }
            public static class AreaRoutes
            {
                public static void Map(dynamic app) => app.MapGet("/areas/{id}", Handle);
                public static string Handle() => "ok";
            }
            """);

        var graph = BuildGraph();
        var callsRoute = graph.Edges.Where(edge => edge.Kind == "calls_route").ToArray();

        Assert.Contains(callsRoute, edge => edge.Target == "route:GET:/areas/{}");
        Assert.DoesNotContain(callsRoute, edge => edge.Target == "route:POST:/areas/{}");
    }

    [Fact]
    public void Analyze_AddsExplicitMarkdownCppGodotAndPythonLinks()
    {
        Write("docs/feature.md", "See [area](../src/Domain/AreaDefinition.cs).\n");
        Write("native/include/area.hpp", "#pragma once\n");
        Write("native/area.cpp", "#include \"include/area.hpp\"\n");
        Write("game/project.godot", "[application]\n");
        Write("game/scripts/area.gd", "extends Node\n");
        Write("game/main.gd", "const Area = preload(\"res://scripts/area.gd\")\n");
        Write("scripts/package.py", "VALUE = 1\n");
        Write("scripts/consumer.py", "import scripts.package\n");

        var edges = BuildGraph().Edges
            .Select(edge => (edge.Source, edge.Target, edge.Kind))
            .ToHashSet();

        Assert.Contains(("file:docs/feature.md", "file:src/Domain/AreaDefinition.cs", "references"), edges);
        Assert.Contains(("file:native/area.cpp", "file:native/include/area.hpp", "imports"), edges);
        Assert.Contains(("file:game/main.gd", "file:game/scripts/area.gd", "imports"), edges);
        Assert.Contains(("file:scripts/consumer.py", "file:scripts/package.py", "imports"), edges);
    }

    [Fact]
    public void ImpactQuery_ReturnsDependenciesAndTestsAsBoundedContext()
    {
        var graph = BuildGraph();
        var engine = new GraphQueryEngine();

        var result = engine.Query(
            graph,
            "AreaService",
            QueryMode.Impact,
            depth: 2,
            maxNodes: 12);
        var context = engine.Format(result, QueryFormat.Context);

        Assert.True(result.Nodes.Count <= 12);
        Assert.Contains(result.Nodes, node => node.Name == "AreaServiceTests");
        Assert.Contains("TESTS:", context, StringComparison.Ordinal);
        Assert.Contains("AreaServiceTests", context, StringComparison.Ordinal);
    }

    [Fact]
    public void Query_SupportsEveryBoundedFormatAndEmptyResults()
    {
        var engine = new GraphQueryEngine();
        var graph = BuildGraph();
        var result = engine.Query(graph, "AreaService", QueryMode.Impact, depth: 2, maxNodes: 8);

        Assert.True(result.Nodes.Count <= 8);
        Assert.NotEmpty(engine.Format(result, QueryFormat.Text));
        Assert.NotEmpty(engine.Format(result, QueryFormat.Paths));
        Assert.StartsWith("{", engine.Format(result, QueryFormat.Json), StringComparison.Ordinal);
        Assert.StartsWith("flowchart", engine.Format(result, QueryFormat.Mermaid), StringComparison.Ordinal);
        Assert.Contains("FOCUS: AreaService", engine.Format(result, QueryFormat.Context), StringComparison.Ordinal);

        var empty = engine.Query(graph, "SymbolThatDoesNotExist", QueryMode.Impact, depth: 1, maxNodes: 4);
        Assert.Empty(empty.Nodes);
        Assert.Contains("no matching architecture evidence", engine.Format(empty, QueryFormat.Context), StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private GraphDocument BuildGraph()
    {
        var scanner = new SourceScanner();
        var sources = scanner.Discover(_root);
        return new RoslynGraphAnalyzer().Analyze(_root, sources);
    }

    private void WriteFixture()
    {
        Write("src/Domain/Domain.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        Write(
            "src/Domain/AreaDefinition.cs",
            "namespace Forge.Domain; public sealed record AreaDefinition(string Id);\n");
        Write(
            "src/Persistence/Persistence.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\Domain\Domain.csproj" />
              </ItemGroup>
            </Project>
            """);
        Write(
            "src/Persistence/AreaRepository.cs",
            """
            using Forge.Domain;
            namespace Forge.Persistence;
            public sealed class AreaRepository
            {
                public AreaDefinition Load() => new("crypt");
            }
            """);
        Write(
            "src/Application/Application.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\Domain\Domain.csproj" />
                <ProjectReference Include="..\Persistence\Persistence.csproj" />
              </ItemGroup>
            </Project>
            """);
        Write(
            "src/Application/AreaService.cs",
            """
            using Forge.Domain;
            using Forge.Persistence;
            namespace Forge.Application;
            public sealed class AreaService
            {
                private readonly AreaRepository _repository = new();
                public AreaDefinition Load() => _repository.Load();
            }
            """);
        Write(
            "tests/Tests.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\src\Application\Application.csproj" />
              </ItemGroup>
            </Project>
            """);
        Write(
            "tests/AreaServiceTests.cs",
            """
            using Forge.Application;
            namespace Forge.Tests;
            public sealed class AreaServiceTests
            {
                private readonly AreaService _service = new();
            }
            """);
    }

    private void Write(string relativePath, string content)
    {
        var destination = Path.Combine(
            _root,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.WriteAllText(destination, content.Replace("\r\n", "\n", StringComparison.Ordinal));
    }
}
