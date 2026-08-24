using System.Collections.Immutable;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Forge.ArchitectureGraph;

public sealed class RoslynGraphAnalyzer
{
    private static readonly SymbolDisplayFormat StableSymbolFormat =
        SymbolDisplayFormat.FullyQualifiedFormat
            .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)
            .WithGenericsOptions(SymbolDisplayGenericsOptions.IncludeTypeParameters)
            .WithMiscellaneousOptions(
                SymbolDisplayMiscellaneousOptions.UseSpecialTypes
                | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private readonly SupplementalExtractor _supplementalExtractor = new();

    public GraphDocument Analyze(
        string root,
        IReadOnlyList<SourceDocument> sources)
    {
        var builder = new GraphBuilder();
        AddFileNodes(builder, sources);

        var projects = DiscoverProjects(root, sources, builder);
        var compilations = BuildCompilations(projects, sources, builder);
        var typeNodeIds = AddDeclaredTypes(compilations, builder);
        AddSemanticRelations(compilations, typeNodeIds, builder);
        _supplementalExtractor.Analyze(sources, builder);

        var manifest = SourceScanner.CreateManifest(sources);
        var roslynVersion = typeof(CSharpCompilation).Assembly
            .GetName()
            .Version?
            .ToString() ?? "unknown";
        var metadata = new GraphMetadata(
            SchemaVersion: 2,
            Engine: "roslyn",
            EngineVersion: roslynVersion,
            SourceDigest: SourceScanner.ComputeManifestDigest(manifest),
            SourceFileCount: manifest.Count,
            Sources: manifest);
        return builder.Build(metadata);
    }

    private static void AddFileNodes(
        GraphBuilder builder,
        IReadOnlyList<SourceDocument> sources)
    {
        foreach (var source in sources)
        {
            builder.AddNode(new GraphNode(
                $"file:{source.Path}",
                ClassifyFile(source.Path),
                Path.GetFileName(source.Path),
                source.Path,
                1,
                new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["language"] = LanguageForSuffix(source.Suffix),
                }));

            if (source.HasInvalidUtf8)
            {
                builder.AddDiagnostic(new GraphDiagnostic(
                    source.Path,
                    1,
                    "warning",
                    "Invalid UTF-8 bytes were replaced during analysis."));
            }
        }
    }

    private static IReadOnlyList<ProjectInfo> DiscoverProjects(
        string root,
        IReadOnlyList<SourceDocument> sources,
        GraphBuilder builder)
    {
        var sourcePaths = sources.Select(source => source.Path).ToHashSet(StringComparer.Ordinal);
        var projects = new List<ProjectInfo>();

        foreach (var source in sources.Where(item => item.Suffix == ".csproj"))
        {
            var projectId = $"project:{SourceScanner.NormalizePath(Path.ChangeExtension(source.Path, null))}";
            var name = Path.GetFileNameWithoutExtension(source.Path);
            var directory = SourceScanner.NormalizePath(Path.GetDirectoryName(source.Path) ?? string.Empty);
            var references = new List<string>();
            var constants = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                using var stringReader = new StringReader(source.Text);
                using var xmlReader = XmlReader.Create(stringReader, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                });
                var document = XDocument.Load(xmlReader, LoadOptions.SetLineInfo);
                foreach (var reference in document.Descendants()
                             .Where(element => element.Name.LocalName == "ProjectReference"))
                {
                    var include = reference.Attribute("Include")?.Value;
                    if (string.IsNullOrWhiteSpace(include))
                    {
                        continue;
                    }

                    var normalized = ResolveRelativePath(directory, include);
                    if (sourcePaths.Contains(normalized))
                    {
                        references.Add(normalized);
                    }
                    else
                    {
                        var line = (reference as IXmlLineInfo)?.LineNumber ?? 1;
                        builder.AddDiagnostic(new GraphDiagnostic(
                            source.Path,
                            line,
                            "warning",
                            $"Project reference not found: {normalized}"));
                    }
                }

                foreach (var defineConstants in document.Descendants()
                             .Where(element => element.Name.LocalName == "DefineConstants"))
                {
                    foreach (var constant in defineConstants.Value.Split(
                                 [';', ','],
                                 StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (!constant.Contains("$(", StringComparison.Ordinal))
                        {
                            constants.Add(constant);
                        }
                    }
                }
            }
            catch (XmlException exception)
            {
                builder.AddDiagnostic(new GraphDiagnostic(
                    source.Path,
                    exception.LineNumber,
                    "error",
                    $"Invalid project XML: {exception.Message}"));
            }

            if (name.Contains("Toolset", StringComparison.OrdinalIgnoreCase))
            {
                constants.Add("TOOLS");
            }

            var project = new ProjectInfo(
                source.Path,
                projectId,
                name,
                directory,
                references.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                constants.Order(StringComparer.Ordinal).ToArray());
            projects.Add(project);
            builder.AddNode(new GraphNode(projectId, "project", name, source.Path, 1));
            builder.AddEdge(projectId, $"file:{source.Path}", "defined_in", "syntactic", source.Path, 1);
        }

        var byPath = projects.ToDictionary(project => project.Path, StringComparer.Ordinal);
        foreach (var project in projects)
        {
            foreach (var reference in project.ProjectReferences)
            {
                if (byPath.TryGetValue(reference, out var target))
                {
                    builder.AddEdge(
                        project.Id,
                        target.Id,
                        "imports",
                        "syntactic",
                        project.Path,
                        FindProjectReferenceLine(sources, project.Path, reference));
                }
            }
        }

        return projects.OrderBy(project => project.Path, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<ProjectCompilation> BuildCompilations(
        IReadOnlyList<ProjectInfo> projects,
        IReadOnlyList<SourceDocument> sources,
        GraphBuilder builder)
    {
        var csharpSources = sources.Where(source => source.Suffix == ".cs").ToArray();
        var owners = AssignSourcesToProjects(projects, csharpSources);
        var projectByPath = projects.ToDictionary(project => project.Path, StringComparer.Ordinal);
        var built = new Dictionary<string, ProjectCompilation>(StringComparer.Ordinal);
        var active = new HashSet<string>(StringComparer.Ordinal);
        var platformReferences = CreatePlatformReferences();

        foreach (var project in projects)
        {
            if (!owners.TryGetValue(project.Path, out var projectSources))
            {
                continue;
            }

            foreach (var source in projectSources)
            {
                builder.AddEdge(
                    project.Id,
                    $"file:{source.Path}",
                    "contains",
                    "syntactic",
                    project.Path,
                    1);
            }
        }

        ProjectCompilation Build(ProjectInfo project)
        {
            if (built.TryGetValue(project.Path, out var cached))
            {
                return cached;
            }

            if (!active.Add(project.Path))
            {
                builder.AddDiagnostic(new GraphDiagnostic(
                    project.Path,
                    1,
                    "error",
                    "Cyclic project reference detected."));
                return CreateCompilation(project, [], [], platformReferences);
            }

            var dependencies = project.ProjectReferences
                .Where(projectByPath.ContainsKey)
                .Select(reference => Build(projectByPath[reference]))
                .ToArray();
            var ownedSources = owners.TryGetValue(project.Path, out var projectSources)
                ? projectSources
                : [];
            var result = CreateCompilation(project, ownedSources, dependencies, platformReferences);
            active.Remove(project.Path);
            built[project.Path] = result;
            return result;
        }

        foreach (var project in projects)
        {
            Build(project);
        }

        var unowned = owners.TryGetValue(string.Empty, out var unownedSources)
            ? unownedSources
            : [];
        if (unowned.Count > 0)
        {
            var looseProject = new ProjectInfo(
                "<loose>",
                "project:loose-csharp",
                "Loose.CSharp",
                string.Empty,
                [],
                []);
            builder.AddNode(new GraphNode(looseProject.Id, "project", looseProject.Name));
            built[looseProject.Path] = CreateCompilation(
                looseProject,
                unowned,
                [],
                platformReferences);
        }

        foreach (var compilation in built.Values)
        {
            foreach (var tree in compilation.Compilation.SyntaxTrees)
            {
                foreach (var diagnostic in tree.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error))
                {
                    var span = diagnostic.Location.GetLineSpan();
                    builder.AddDiagnostic(new GraphDiagnostic(
                        SourceScanner.NormalizePath(tree.FilePath),
                        span.StartLinePosition.Line + 1,
                        "error",
                        $"Roslyn parse error {diagnostic.Id}: {diagnostic.GetMessage()}"));
                }
            }
        }

        return built.Values.OrderBy(item => item.Project.Path, StringComparer.Ordinal).ToArray();
    }

    private static ProjectCompilation CreateCompilation(
        ProjectInfo project,
        IReadOnlyList<SourceDocument> sources,
        IReadOnlyList<ProjectCompilation> dependencies,
        ImmutableArray<MetadataReference> platformReferences)
    {
        var parseOptions = new CSharpParseOptions(
            LanguageVersion.CSharp12,
            DocumentationMode.Parse,
            SourceCodeKind.Regular,
            project.DefineConstants);
        var trees = sources
            .OrderBy(source => source.Path, StringComparer.Ordinal)
            .Select(source => CSharpSyntaxTree.ParseText(
                source.Text,
                parseOptions,
                source.Path,
                System.Text.Encoding.UTF8))
            .ToArray();
        var references = platformReferences
            .Concat(dependencies.Select(item => item.Compilation.ToMetadataReference()))
            .ToArray();
        var compilation = CSharpCompilation.Create(
            project.Name,
            trees,
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable,
                deterministic: true,
                allowUnsafe: true));
        return new ProjectCompilation(project, compilation);
    }

    private static Dictionary<string, string> AddDeclaredTypes(
        IReadOnlyList<ProjectCompilation> compilations,
        GraphBuilder builder)
    {
        var typeNodeIds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var projectCompilation in compilations)
        {
            foreach (var tree in projectCompilation.Compilation.SyntaxTrees.OrderBy(item => item.FilePath, StringComparer.Ordinal))
            {
                var model = projectCompilation.Compilation.GetSemanticModel(tree, ignoreAccessibility: true);
                var root = tree.GetRoot();
                foreach (var declaration in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
                {
                    if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol)
                    {
                        continue;
                    }

                    AddTypeNode(builder, typeNodeIds, projectCompilation.Project, symbol, declaration);
                }

                foreach (var declaration in root.DescendantNodes().OfType<DelegateDeclarationSyntax>())
                {
                    if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol)
                    {
                        continue;
                    }

                    AddTypeNode(builder, typeNodeIds, projectCompilation.Project, symbol, declaration);
                }
            }
        }

        return typeNodeIds;
    }

    private static void AddTypeNode(
        GraphBuilder builder,
        IDictionary<string, string> typeNodeIds,
        ProjectInfo project,
        INamedTypeSymbol symbol,
        SyntaxNode declaration)
    {
        var path = SourceScanner.NormalizePath(declaration.SyntaxTree.FilePath);
        var line = declaration.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        var kind = ClassifyType(symbol.Name, path);
        var symbolKey = StableTypeName(symbol);
        var nodeId = $"{kind}:{symbolKey}";
        typeNodeIds[symbolKey] = nodeId;
        var isPartialType = symbol.DeclaringSyntaxReferences.Length > 1
            && symbol.DeclaringSyntaxReferences.All(reference =>
                reference.GetSyntax() is TypeDeclarationSyntax typeDeclaration
                && typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword));
        var isAnotherDeclarationInSameProject =
            isPartialType
            && builder.Nodes.TryGetValue(nodeId, out var existingNode)
            && existingNode.Kind == kind
            && existingNode.Name == symbol.Name
            && existingNode.Metadata?.TryGetValue("project", out var existingProject) == true
            && existingProject == project.Name;
        if (!isAnotherDeclarationInSameProject)
        {
            builder.AddNode(new GraphNode(
                nodeId,
                kind,
                symbol.Name,
                path,
                line,
                new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["namespace"] = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
                    ["project"] = project.Name,
                    ["symbol_kind"] = symbol.TypeKind.ToString(),
                }));
        }
        builder.AddEdge(nodeId, $"file:{path}", "defined_in", "semantic", path, line);
        builder.AddEdge(nodeId, project.Id, "belongs_to", "semantic", path, line);

        var containingNamespace = symbol.ContainingNamespace;
        if (containingNamespace is not null && !containingNamespace.IsGlobalNamespace)
        {
            var namespaceName = containingNamespace.ToDisplayString();
            var namespaceId = $"namespace:{namespaceName}";
            builder.AddNode(new GraphNode(namespaceId, "namespace", namespaceName));
            builder.AddEdge(namespaceId, $"file:{path}", "defined_in", "semantic", path, line);
        }
    }

    private static void AddSemanticRelations(
        IReadOnlyList<ProjectCompilation> compilations,
        IReadOnlyDictionary<string, string> typeNodeIds,
        GraphBuilder builder)
    {
        var shortNameIndex = typeNodeIds.Keys
            .GroupBy(key => key.Split('.').Last().Split('<')[0], StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var internalMemberNames = compilations
            .SelectMany(item => item.Compilation.SyntaxTrees)
            .SelectMany(tree => tree.GetRoot().DescendantNodes())
            .SelectMany(node => node switch
            {
                MethodDeclarationSyntax method => [method.Identifier.ValueText],
                PropertyDeclarationSyntax property => [property.Identifier.ValueText],
                EventDeclarationSyntax eventDeclaration => [eventDeclaration.Identifier.ValueText],
                VariableDeclaratorSyntax variable => [variable.Identifier.ValueText],
                EnumMemberDeclarationSyntax enumMember => [enumMember.Identifier.ValueText],
                _ => Array.Empty<string>(),
            })
            .ToHashSet(StringComparer.Ordinal);
        foreach (var projectCompilation in compilations)
        {
            foreach (var tree in projectCompilation.Compilation.SyntaxTrees.OrderBy(item => item.FilePath, StringComparer.Ordinal))
            {
                var model = projectCompilation.Compilation.GetSemanticModel(tree, ignoreAccessibility: true);
                var root = tree.GetRoot();
                var declarations = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().ToArray();
                foreach (var declaration in declarations)
                {
                    if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol sourceSymbol)
                    {
                        continue;
                    }

                    var sourceKey = StableTypeName(sourceSymbol);
                    if (!typeNodeIds.TryGetValue(sourceKey, out var sourceNodeId))
                    {
                        continue;
                    }

                    AddBaseTypeRelations(
                        declaration,
                        model,
                        sourceNodeId,
                        typeNodeIds,
                        builder);

                    var ownNodes = declaration.DescendantNodes(
                        child => child == declaration
                            || child is not BaseTypeDeclarationSyntax
                            && child is not DelegateDeclarationSyntax);
                    foreach (var nameSyntax in ownNodes.OfType<SimpleNameSyntax>())
                    {
                        var identifier = nameSyntax.Identifier.ValueText;
                        if (!shortNameIndex.ContainsKey(identifier)
                            && !internalMemberNames.Contains(identifier))
                        {
                            continue;
                        }

                        var symbol = ResolveSymbol(model, nameSyntax);
                        var targetType = ContainingType(symbol);
                        if (targetType is null)
                        {
                            continue;
                        }

                        var targetKey = StableTypeName(targetType);
                        if (!typeNodeIds.TryGetValue(targetKey, out var targetNodeId))
                        {
                            continue;
                        }

                        if (targetNodeId == sourceNodeId)
                        {
                            continue;
                        }

                        var sourceNode = builder.Nodes[sourceNodeId];
                        var targetNode = builder.Nodes[targetNodeId];
                        var evidencePath = SourceScanner.NormalizePath(tree.FilePath);
                        var evidenceLine = nameSyntax.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        builder.AddEdge(
                            sourceNodeId,
                            targetNodeId,
                            RelationshipForTypes(sourceNode.Kind, targetNode.Kind),
                            "semantic",
                            evidencePath,
                            evidenceLine);

                        if (!string.IsNullOrWhiteSpace(targetNode.Path)
                            && targetNode.Path != evidencePath)
                        {
                            builder.AddEdge(
                                $"file:{evidencePath}",
                                $"file:{targetNode.Path}",
                                "imports",
                                "semantic",
                                evidencePath,
                                evidenceLine);
                        }
                    }

                    AddHttpRelations(
                        declaration,
                        model,
                        sourceNodeId,
                        typeNodeIds,
                        builder);
                }
            }
        }

        LinkHttpOperationsToRoutes(builder);
    }

    private static void AddBaseTypeRelations(
        BaseTypeDeclarationSyntax declaration,
        SemanticModel model,
        string sourceNodeId,
        IReadOnlyDictionary<string, string> typeNodeIds,
        GraphBuilder builder)
    {
        if (declaration.BaseList is null)
        {
            return;
        }

        foreach (var baseType in declaration.BaseList.Types)
        {
            if (model.GetTypeInfo(baseType.Type).Type is not INamedTypeSymbol targetType)
            {
                continue;
            }

            if (!typeNodeIds.TryGetValue(StableTypeName(targetType), out var targetNodeId))
            {
                continue;
            }

            var path = SourceScanner.NormalizePath(declaration.SyntaxTree.FilePath);
            var line = baseType.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            builder.AddEdge(
                sourceNodeId,
                targetNodeId,
                targetType.TypeKind == TypeKind.Interface ? "implements" : "inherits",
                "semantic",
                path,
                line);
        }
    }

    private static void AddHttpRelations(
        BaseTypeDeclarationSyntax declaration,
        SemanticModel model,
        string sourceNodeId,
        IReadOnlyDictionary<string, string> typeNodeIds,
        GraphBuilder builder)
    {
        var sourceNode = builder.Nodes[sourceNodeId];
        foreach (var invocation in declaration.DescendantNodes(
                     child => child == declaration
                         || child is not BaseTypeDeclarationSyntax).OfType<InvocationExpressionSyntax>())
        {
            var methodName = invocation.Expression switch
            {
                MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                _ => string.Empty,
            };
            var verb = HttpVerb(methodName);
            if (verb is null || invocation.ArgumentList.Arguments.Count == 0)
            {
                continue;
            }

            var routeValue = model.GetConstantValue(invocation.ArgumentList.Arguments[0].Expression);
            if (!routeValue.HasValue || routeValue.Value is not string route || !route.StartsWith('/'))
            {
                continue;
            }

            var normalizedRoute = NormalizeRoute(route);
            var path = SourceScanner.NormalizePath(invocation.SyntaxTree.FilePath);
            var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            if (methodName.StartsWith("Map", StringComparison.Ordinal))
            {
                var routeId = $"route:{verb}:{normalizedRoute}";
                builder.AddNode(new GraphNode(
                    routeId,
                    "route",
                    $"{verb} {normalizedRoute}",
                    path,
                    line,
                    HttpMetadata(verb, normalizedRoute)));
                builder.AddEdge(routeId, $"file:{path}", "defined_in", "syntactic", path, line);

                if (invocation.ArgumentList.Arguments.Count > 1)
                {
                    var handlerSymbol = ResolveSymbol(
                        model,
                        invocation.ArgumentList.Arguments[1].Expression);
                    var handlerType = ContainingType(handlerSymbol);
                    if (handlerType is not null
                        && typeNodeIds.TryGetValue(StableTypeName(handlerType), out var handlerNodeId))
                    {
                        builder.AddEdge(routeId, handlerNodeId, "handled_by", "semantic", path, line);
                    }
                }
            }
            else if (sourceNode.Kind == "client")
            {
                var operationId = $"api_operation:{sourceNodeId}:{verb}:{normalizedRoute}";
                builder.AddNode(new GraphNode(
                    operationId,
                    "api_operation",
                    $"{verb} {normalizedRoute}",
                    path,
                    line,
                    HttpMetadata(verb, normalizedRoute)));
                builder.AddEdge(sourceNodeId, operationId, "calls_api", "syntactic", path, line);
                builder.AddEdge(operationId, $"file:{path}", "defined_in", "syntactic", path, line);
            }
        }
    }

    private static void LinkHttpOperationsToRoutes(GraphBuilder builder)
    {
        var routes = builder.Nodes.Values
            .Where(node => node.Kind == "route")
            .ToDictionary(
                node => HttpKey(node.Metadata),
                node => node.Id,
                StringComparer.Ordinal);
        foreach (var operation in builder.Nodes.Values.Where(node => node.Kind == "api_operation"))
        {
            if (routes.TryGetValue(HttpKey(operation.Metadata), out var routeId))
            {
                builder.AddEdge(
                    operation.Id,
                    routeId,
                    "calls_route",
                    "syntactic",
                    operation.Path ?? string.Empty,
                    operation.Line ?? 1);
            }
        }
    }

    private static Dictionary<string, IReadOnlyList<SourceDocument>> AssignSourcesToProjects(
        IReadOnlyList<ProjectInfo> projects,
        IReadOnlyList<SourceDocument> sources)
    {
        var result = new Dictionary<string, List<SourceDocument>>(StringComparer.Ordinal);
        var orderedProjects = projects
            .OrderByDescending(project => project.Directory.Length)
            .ThenBy(project => project.Path, StringComparer.Ordinal)
            .ToArray();
        foreach (var source in sources)
        {
            var owner = orderedProjects.FirstOrDefault(project => IsUnderDirectory(source.Path, project.Directory));
            var key = owner?.Path ?? string.Empty;
            if (!result.TryGetValue(key, out var ownedSources))
            {
                ownedSources = [];
                result[key] = ownedSources;
            }

            ownedSources.Add(source);
        }

        return result.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<SourceDocument>)pair.Value
                .OrderBy(source => source.Path, StringComparer.Ordinal)
                .ToArray(),
            StringComparer.Ordinal);
    }

    private static ImmutableArray<MetadataReference> CreatePlatformReferences()
    {
        var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrWhiteSpace(trustedAssemblies))
        {
            return [];
        }

        return trustedAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }

    private static ISymbol? ResolveSymbol(SemanticModel model, SyntaxNode syntax)
    {
        var info = model.GetSymbolInfo(syntax);
        return info.Symbol ?? (info.CandidateSymbols.Length == 1 ? info.CandidateSymbols[0] : null);
    }

    private static INamedTypeSymbol? ContainingType(ISymbol? symbol)
    {
        return symbol switch
        {
            INamedTypeSymbol type => type.OriginalDefinition,
            IMethodSymbol method => method.ContainingType?.OriginalDefinition,
            IPropertySymbol property => property.ContainingType?.OriginalDefinition,
            IFieldSymbol field => field.ContainingType?.OriginalDefinition,
            IEventSymbol eventSymbol => eventSymbol.ContainingType?.OriginalDefinition,
            IParameterSymbol parameter => parameter.Type as INamedTypeSymbol,
            ILocalSymbol local => local.Type as INamedTypeSymbol,
            IAliasSymbol alias => alias.Target as INamedTypeSymbol,
            _ => symbol?.ContainingType?.OriginalDefinition,
        };
    }

    private static string StableTypeName(INamedTypeSymbol symbol) =>
        symbol.OriginalDefinition.ToDisplayString(StableSymbolFormat);

    private static string ClassifyType(string name, string path)
    {
        if (path.Split('/').Any(part => part.Equals("tests", StringComparison.OrdinalIgnoreCase))
            || name.EndsWith("Test", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Tests", StringComparison.OrdinalIgnoreCase))
        {
            return "test";
        }

        if (name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Endpoint", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Endpoints", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Handler", StringComparison.OrdinalIgnoreCase))
        {
            return "handler";
        }

        if (name.StartsWith('I') && EndsWithAny(name, "Service", "Repository", "Runtime", "Provider"))
        {
            return "interface";
        }

        if (name.EndsWith("Service", StringComparison.OrdinalIgnoreCase)) return "service";
        if (name.EndsWith("Validator", StringComparison.OrdinalIgnoreCase)) return "validator";
        if (name.EndsWith("Repository", StringComparison.OrdinalIgnoreCase)) return "repository";
        if (EndsWithAny(name, "Serializer", "Converter", "Adapter")) return "adapter";
        if (EndsWithAny(name, "ApiClient", "Client")) return "client";
        if (EndsWithAny(name, "Screen", "View", "Page", "Component", "Dock", "Toolset", "Plugin", "Window")) return "component";
        if (EndsWithAny(
                name,
                "Definition", "Model", "Entity", "Dto", "Command", "Event", "Value", "Id",
                "Instance", "Kind", "Document", "Diagnostic", "State", "Patch", "Result", "Entry",
                "Profile", "Descriptor", "Contract", "Snapshot", "Manifest", "Request", "Response"))
        {
            return "model";
        }

        return "type";
    }

    private static string RelationshipForTypes(string sourceKind, string targetKind)
    {
        if (sourceKind == "test" && targetKind != "test") return "tests";
        if (sourceKind == "component" && targetKind == "client") return "calls_api";
        if (sourceKind is "handler" or "component" or "type" && targetKind == "service") return "uses_service";
        if (sourceKind == "adapter" && targetKind == "validator") return "uses_service";
        if (sourceKind == "service" && targetKind == "repository") return "uses_repository";
        if (sourceKind is "repository" or "service" && targetKind == "model") return "accesses_model";
        if (sourceKind == "adapter" && targetKind == "model") return "serializes";
        if (sourceKind == "validator" && targetKind == "model") return "validates";
        if (sourceKind == "model" && targetKind == "model") return "relationship";
        if (sourceKind is "handler" or "client" && targetKind == "model") return "uses_model";
        return "references";
    }

    private static string ClassifyFile(string path)
    {
        var suffix = Path.GetExtension(path);
        if (suffix.Equals(".md", StringComparison.OrdinalIgnoreCase))
        {
            return path.StartsWith("docs/ADR/", StringComparison.OrdinalIgnoreCase)
                ? "decision"
                : "document";
        }

        return suffix.Equals(".cs", StringComparison.OrdinalIgnoreCase) ? "file" : "configuration";
    }

    private static string LanguageForSuffix(string suffix) => suffix switch
    {
        ".cs" => "csharp",
        ".py" => "python",
        ".gd" => "gdscript",
        ".cc" or ".cpp" or ".cxx" or ".h" or ".hpp" => "cpp",
        ".md" => "markdown",
        _ => "configuration",
    };

    private static bool EndsWithAny(string value, params string[] suffixes) =>
        suffixes.Any(suffix => value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

    private static bool IsUnderDirectory(string path, string directory)
    {
        return string.IsNullOrEmpty(directory)
            || path.StartsWith($"{directory}/", StringComparison.Ordinal);
    }

    private static string ResolveRelativePath(string directory, string target)
        => SupplementalExtractor.NormalizeRelativePath(directory, target);

    private static int FindProjectReferenceLine(
        IReadOnlyList<SourceDocument> sources,
        string projectPath,
        string referencePath)
    {
        var source = sources.First(item => item.Path == projectPath);
        var referenceName = Path.GetFileName(referencePath);
        var lines = source.Text.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (lines[index].Contains(referenceName, StringComparison.OrdinalIgnoreCase))
            {
                return index + 1;
            }
        }

        return 1;
    }

    private static string? HttpVerb(string methodName)
    {
        if (methodName.StartsWith("Map", StringComparison.Ordinal)) methodName = methodName[3..];
        if (methodName.EndsWith("Async", StringComparison.Ordinal)) methodName = methodName[..^5];
        if (methodName.StartsWith("GetFromJson", StringComparison.Ordinal)) return "GET";
        if (methodName.StartsWith("PostAsJson", StringComparison.Ordinal)) return "POST";
        if (methodName.StartsWith("PutAsJson", StringComparison.Ordinal)) return "PUT";
        return methodName switch
        {
            "Get" => "GET",
            "Post" => "POST",
            "Put" => "PUT",
            "Delete" => "DELETE",
            "Patch" => "PATCH",
            _ => null,
        };
    }

    private static string NormalizeRoute(string route)
    {
        var withoutQuery = route.Split(['?', '#'], 2)[0];
        var segments = withoutQuery
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment =>
                (segment.StartsWith('{') && segment.EndsWith('}'))
                || segment.StartsWith(':')
                    ? "{}"
                    : segment);
        return $"/{string.Join('/', segments)}";
    }

    private static IReadOnlyDictionary<string, string> HttpMetadata(string verb, string route) =>
        new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["http_method"] = verb,
            ["route"] = route,
        };

    private static string HttpKey(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null
            || !metadata.TryGetValue("http_method", out var method)
            || !metadata.TryGetValue("route", out var route))
        {
            return string.Empty;
        }

        return $"{method}:{route}";
    }

    private sealed record ProjectInfo(
        string Path,
        string Id,
        string Name,
        string Directory,
        IReadOnlyList<string> ProjectReferences,
        IReadOnlyList<string> DefineConstants);

    private sealed record ProjectCompilation(
        ProjectInfo Project,
        CSharpCompilation Compilation);
}
