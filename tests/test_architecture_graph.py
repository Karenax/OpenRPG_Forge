from __future__ import annotations

import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "scripts" / "architecture_graph.py"
SPEC = importlib.util.spec_from_file_location("architecture_graph", SCRIPT_PATH)
assert SPEC is not None and SPEC.loader is not None
architecture_graph = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = architecture_graph
SPEC.loader.exec_module(architecture_graph)


class ArchitectureGraphTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary_directory = tempfile.TemporaryDirectory(
            prefix="openrpg architecture graph "
        )
        self.root = Path(self.temporary_directory.name)

    def tearDown(self) -> None:
        self.temporary_directory.cleanup()

    def write(self, relative_path: str, content: str) -> None:
        destination = self.root / relative_path
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_text(content, encoding="utf-8", newline="\n")

    def build_vertical_fixture(self) -> dict:
        self.write(
            "src/Toolset/AreaScreen.cs",
            """using Forge.Client;
namespace Forge.Toolset;
public sealed class AreaScreen
{
    private readonly AreaApiClient _client;
}
""",
        )
        self.write(
            "src/Client/AreaApiClient.cs",
            """using Forge.Domain;
namespace Forge.Client;
public sealed class AreaApiClient
{
    public Task<AreaDefinition?> Get(HttpClient http) =>
        http.GetFromJsonAsync<AreaDefinition>(\"/api/areas/{areaId}\");
}
""",
        )
        self.write(
            "src/Server/AreaEndpoints.cs",
            """using Forge.Application;
namespace Forge.Server;
public sealed class AreaEndpoints
{
    public static void Map(WebApplication app) =>
        app.MapGet(\"/api/areas/{id}\", AreaService.GetArea);
}
""",
        )
        self.write(
            "src/Application/AreaService.cs",
            """using Forge.Domain;
using Forge.Persistence;
namespace Forge.Application;
public sealed class AreaService
{
    private readonly AreaRepository _repository;
    public static AreaDefinition GetArea() => new(\"crypt\");
}
""",
        )
        self.write(
            "src/Persistence/AreaRepository.cs",
            """using Forge.Domain;
namespace Forge.Persistence;
public sealed class AreaRepository
{
    public AreaDefinition Load() => new(\"crypt\");
}
""",
        )
        self.write(
            "src/Domain/AreaDefinition.cs",
            """namespace Forge.Domain;
public sealed record AreaDefinition(string Id);
""",
        )
        self.write(
            "tests/AreaServiceTests.cs",
            """using Forge.Application;
namespace Forge.Tests;
public sealed class AreaServiceTests
{
    private readonly AreaService _service;
}
""",
        )
        return architecture_graph.build_graph(self.root)

    def test_csharp_vertical_path_is_connected_with_evidence(self) -> None:
        graph = self.build_vertical_fixture()
        edges = {
            (edge["source"], edge["target"], edge["kind"]): edge
            for edge in graph["edges"]
        }
        operation = (
            "api_operation:Forge.Client.AreaApiClient:GET:/api/areas/{}"
        )
        route = "route:GET:/api/areas/{}"
        expected = {
            (
                "component:Forge.Toolset.AreaScreen",
                "client:Forge.Client.AreaApiClient",
                "calls_api",
            ),
            (
                "client:Forge.Client.AreaApiClient",
                operation,
                "calls_api",
            ),
            (operation, route, "calls_route"),
            (route, "service:Forge.Application.AreaService", "handled_by"),
            (
                "service:Forge.Application.AreaService",
                "repository:Forge.Persistence.AreaRepository",
                "uses_repository",
            ),
            (
                "repository:Forge.Persistence.AreaRepository",
                "model:Forge.Domain.AreaDefinition",
                "accesses_model",
            ),
            (
                "test:Forge.Tests.AreaServiceTests",
                "service:Forge.Application.AreaService",
                "tests",
            ),
        }
        self.assertTrue(expected.issubset(edges))
        for edge_key in expected:
            evidence = edges[edge_key]["evidence"]
            self.assertTrue(evidence["path"])
            self.assertGreaterEqual(evidence["line"], 1)

    def test_internal_csharp_using_targets_a_known_namespace(self) -> None:
        graph = self.build_vertical_fixture()
        self.assertIn(
            (
                "file:src/Toolset/AreaScreen.cs",
                "namespace:Forge.Client",
                "imports",
            ),
            {
                (edge["source"], edge["target"], edge["kind"])
                for edge in graph["edges"]
            },
        )

    def test_dotnet_project_references_normalize_windows_separators(self) -> None:
        self.write("src/Domain/Forge.Domain.csproj", "<Project />\n")
        self.write(
            "src/Application/Forge.Application.csproj",
            """<Project>
  <ItemGroup>
    <ProjectReference Include="..\\Domain\\Forge.Domain.csproj" />
  </ItemGroup>
</Project>
""",
        )
        graph = architecture_graph.build_graph(self.root)
        self.assertIn(
            (
                "project:src/Application/Forge.Application",
                "project:src/Domain/Forge.Domain",
                "imports",
            ),
            {
                (edge["source"], edge["target"], edge["kind"])
                for edge in graph["edges"]
            },
        )

    def test_csharp_references_do_not_leak_between_neighboring_types(self) -> None:
        self.write(
            "src/Domain/Ids.cs",
            """namespace Forge.Domain;
public sealed record AreaId(string Value);
public sealed record ProjectId(string Value);
""",
        )
        self.write(
            "src/Format/Converters.cs",
            """using Forge.Domain;
namespace Forge.Format;
public sealed class AreaIdJsonConverter
{
    public string Write(AreaId value) => value.Value;
}
public sealed class ProjectIdJsonConverter
{
    public string Write(ProjectId value) => value.Value;
}
""",
        )
        graph = architecture_graph.build_graph(self.root)
        serializes = {
            (edge["source"], edge["target"])
            for edge in graph["edges"]
            if edge["kind"] == "serializes"
        }
        self.assertEqual(
            {
                (
                    "adapter:Forge.Format.AreaIdJsonConverter",
                    "model:Forge.Domain.AreaId",
                ),
                (
                    "adapter:Forge.Format.ProjectIdJsonConverter",
                    "model:Forge.Domain.ProjectId",
                ),
            },
            serializes,
        )

    def test_routes_are_normalized_without_matching_the_wrong_verb(self) -> None:
        self.write(
            "src/Api.cs",
            """namespace Forge.Api;
public sealed class AreaEndpoints
{
    public static void Map(WebApplication app) =>
        app.MapGet(\"/api/areas/{id}/\", AreaService.Get);
}
public sealed class AreaService { public static object Get() => new(); }
public sealed class AreaApiClient
{
    public Task Post(HttpClient http) =>
        http.PostAsync(\"/api/areas/${areaId}\", null);
}
""",
        )
        graph = architecture_graph.build_graph(self.root)
        calls_route = [edge for edge in graph["edges"] if edge["kind"] == "calls_route"]
        self.assertEqual([], calls_route)
        routes = [node for node in graph["nodes"] if node["kind"] == "route"]
        self.assertEqual(["route:GET:/api/areas/{}"], [node["id"] for node in routes])

    def test_exclusions_omit_secrets_builds_databases_and_dependencies(self) -> None:
        self.write("src/Valid.cs", "public sealed class ValidService {}\n")
        self.write(".github/workflows/check.yml", "name: Check\n")
        self.write("node_modules/External.cs", "public sealed class ExternalService {}\n")
        self.write("build/Generated.cs", "public sealed class GeneratedService {}\n")
        self.write(".env", "SECRET=value\n")
        self.write("data.sqlite", "not a database fixture\n")
        graph = architecture_graph.build_graph(self.root)
        paths = {node.get("path") for node in graph["nodes"]}
        self.assertIn("src/Valid.cs", paths)
        self.assertIn(".github/workflows/check.yml", paths)
        self.assertNotIn("node_modules/External.cs", paths)
        self.assertNotIn("build/Generated.cs", paths)
        self.assertNotIn(".env", paths)
        self.assertNotIn("data.sqlite", paths)

    def test_serialization_is_byte_identical_and_stably_sorted(self) -> None:
        graph_one = self.build_vertical_fixture()
        graph_two = architecture_graph.build_graph(self.root)
        bytes_one = architecture_graph.serialize_graph(graph_one)
        bytes_two = architecture_graph.serialize_graph(graph_two)
        self.assertEqual(bytes_one, bytes_two)
        self.assertEqual(
            sorted(node["id"] for node in graph_one["nodes"]),
            [node["id"] for node in graph_one["nodes"]],
        )
        parsed = json.loads(bytes_one)
        self.assertEqual(architecture_graph.SCHEMA_VERSION, parsed["metadata"]["schema_version"])

    def test_graph_is_independent_of_the_absolute_checkout_path(self) -> None:
        self.write("src/service.py", "def run():\n    return True\n")
        with tempfile.TemporaryDirectory(prefix="second checkout ") as second_directory:
            second_root = Path(second_directory)
            destination = second_root / "src/service.py"
            destination.parent.mkdir(parents=True)
            destination.write_text(
                "def run():\n    return True\n", encoding="utf-8", newline="\n"
            )
            first = architecture_graph.serialize_graph(
                architecture_graph.build_graph(self.root)
            )
            second = architecture_graph.serialize_graph(
                architecture_graph.build_graph(second_root)
            )
            self.assertEqual(first, second)

    def test_load_rejects_an_unknown_schema_version(self) -> None:
        graph_path = self.root / architecture_graph.DEFAULT_GRAPH_PATH
        graph_path.parent.mkdir(parents=True)
        graph_path.write_text(
            json.dumps({"metadata": {"schema_version": 999}}),
            encoding="utf-8",
            newline="\n",
        )
        with self.assertRaisesRegex(ValueError, "Version de schéma incompatible"):
            architecture_graph.load_graph(
                self.root, architecture_graph.DEFAULT_GRAPH_PATH
            )

    def test_generate_and_check_detect_a_stale_source(self) -> None:
        self.write("src/Domain/AreaDefinition.cs", "public record AreaDefinition;\n")
        first = architecture_graph.generate(
            self.root, architecture_graph.DEFAULT_GRAPH_PATH
        )
        graph_path = self.root / architecture_graph.DEFAULT_GRAPH_PATH
        overview_path = graph_path.parent / architecture_graph.DEFAULT_OVERVIEW_NAME
        first_graph_bytes = graph_path.read_bytes()
        first_overview_bytes = overview_path.read_bytes()
        second = architecture_graph.generate(
            self.root, architecture_graph.DEFAULT_GRAPH_PATH
        )
        self.assertEqual(first, second)
        self.assertEqual(first_graph_bytes, graph_path.read_bytes())
        self.assertEqual(first_overview_bytes, overview_path.read_bytes())
        fresh, errors = architecture_graph.check(
            self.root, architecture_graph.DEFAULT_GRAPH_PATH
        )
        self.assertTrue(fresh, errors)
        self.write(
            "src/Domain/AreaDefinition.cs",
            "public record AreaDefinition;\npublic record ModuleDefinition;\n",
        )
        fresh, errors = architecture_graph.check(
            self.root, architecture_graph.DEFAULT_GRAPH_PATH
        )
        self.assertFalse(fresh)
        self.assertTrue(any("périmé" in error for error in errors))

    def test_custom_output_inside_root_does_not_index_itself(self) -> None:
        self.write("src/service.py", "def run():\n    return True\n")
        output = Path("artifacts-for-docs/custom-graph.json")
        architecture_graph.generate(self.root, output)
        first_bytes = (self.root / output).read_bytes()
        fresh, errors = architecture_graph.check(self.root, output)
        self.assertTrue(fresh, errors)
        architecture_graph.generate(self.root, output)
        self.assertEqual(first_bytes, (self.root / output).read_bytes())

    def test_query_is_bounded_and_supports_all_formats(self) -> None:
        graph = self.build_vertical_fixture()
        result = architecture_graph.select_subgraph(
            graph, "AreaService", depth=2, max_nodes=5
        )
        self.assertLessEqual(len(result["nodes"]), 5)
        paths = architecture_graph.format_query(result, "paths")
        self.assertEqual(sorted(set(paths.splitlines())), paths.splitlines())
        self.assertIsInstance(json.loads(architecture_graph.format_query(result, "json")), dict)
        self.assertTrue(
            architecture_graph.format_query(result, "mermaid").startswith("flowchart LR")
        )
        self.assertIn("Nœuds:", architecture_graph.format_query(result, "text"))

    def test_empty_query_result_is_valid_in_every_format(self) -> None:
        self.write("README.md", "# Fixture\n")
        graph = architecture_graph.build_graph(self.root)
        result = architecture_graph.select_subgraph(
            graph, "does-not-exist", depth=1, max_nodes=10
        )
        self.assertEqual([], result["nodes"])
        self.assertEqual("", architecture_graph.format_query(result, "paths"))
        self.assertEqual([], json.loads(architecture_graph.format_query(result, "json"))["nodes"])
        self.assertIn("Aucun résultat", architecture_graph.format_query(result, "text"))
        self.assertIn("Aucun résultat", architecture_graph.format_query(result, "mermaid"))

    def test_invalid_python_is_reported_without_aborting(self) -> None:
        self.write("scripts/valid.py", "def valid():\n    return 1\n")
        self.write("scripts/invalid.py", "def invalid(:\n    pass\n")
        graph = architecture_graph.build_graph(self.root)
        self.assertTrue(
            any(
                diagnostic["path"] == "scripts/invalid.py"
                and "Syntaxe Python" in diagnostic["message"]
                for diagnostic in graph["diagnostics"]
            )
        )
        self.assertIn(
            "function:scripts.valid.valid", {node["id"] for node in graph["nodes"]}
        )

    def test_markdown_cpp_and_gdscript_references_are_explicit(self) -> None:
        self.write("README.md", "Voir [`Architecture`](docs/ARCHITECTURE.md).\n")
        self.write("docs/ARCHITECTURE.md", "# Architecture\n")
        self.write("native/main.cpp", '#include "detail/api.hpp"\n')
        self.write("native/detail/api.hpp", "#pragma once\n")
        self.write("game/main.gd", 'const AREA = preload("res://game/area.gd")\n')
        self.write("game/area.gd", "extends Node\n")
        self.write("apps/toolset/project.godot", "config_version=5\n")
        self.write(
            "apps/toolset/addons/forge/plugin.cfg",
            '[plugin]\nscript="ForgePlugin.cs"\n',
        )
        self.write(
            "apps/toolset/addons/forge/ForgePlugin.cs",
            "public sealed class ForgePlugin {}\n",
        )
        self.write(
            "apps/toolset/main.tscn",
            '[ext_resource path="res://addons/forge/ForgePlugin.cs" type="Script" id="1"]\n',
        )
        graph = architecture_graph.build_graph(self.root)
        edges = {
            (edge["source"], edge["target"], edge["kind"])
            for edge in graph["edges"]
        }
        self.assertIn(
            (
                "file:README.md",
                "file:docs/ARCHITECTURE.md",
                "references",
            ),
            edges,
        )
        self.assertIn(
            (
                "file:native/main.cpp",
                "file:native/detail/api.hpp",
                "imports",
            ),
            edges,
        )
        self.assertIn(
            ("file:game/main.gd", "file:game/area.gd", "imports"), edges
        )
        self.assertIn(
            (
                "file:apps/toolset/addons/forge/plugin.cfg",
                "file:apps/toolset/addons/forge/ForgePlugin.cs",
                "imports",
            ),
            edges,
        )
        self.assertIn(
            (
                "file:apps/toolset/main.tscn",
                "file:apps/toolset/addons/forge/ForgePlugin.cs",
                "imports",
            ),
            edges,
        )

    def test_python_imports_and_tests_are_connected(self) -> None:
        self.write("src/service.py", "class AreaService:\n    pass\n")
        self.write(
            "tests/test_service.py",
            "from src.service import AreaService\n\ndef test_area():\n    assert AreaService\n",
        )
        graph = architecture_graph.build_graph(self.root)
        edges = {
            (edge["source"], edge["target"], edge["kind"])
            for edge in graph["edges"]
        }
        self.assertIn(
            ("module:tests.test_service", "module:src.service", "imports"), edges
        )
        self.assertIn(
            ("module:tests.test_service", "service:src.service.AreaService", "tests"),
            edges,
        )


if __name__ == "__main__":
    unittest.main()
