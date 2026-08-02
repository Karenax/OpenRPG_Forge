#!/usr/bin/env python3
"""Build and query OpenRPG Forge's deterministic architecture graph.

The generator is deliberately local and dependency-free. It uses conservative
static extraction: uncertain constructs are ignored instead of being guessed.
"""

from __future__ import annotations

import argparse
import ast
import hashlib
import json
import os
import posixpath
import re
import sys
import tempfile
from collections import Counter, defaultdict, deque
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Any, Iterable, Sequence


SCHEMA_VERSION = 1
DEFAULT_GRAPH_PATH = Path("docs/architecture/graph.json")
DEFAULT_OVERVIEW_NAME = "overview.mmd"
MAX_OVERVIEW_KINDS = 14
MAX_OVERVIEW_EDGES = 40

ANALYZED_SUFFIXES = frozenset(
    {
        ".cc",
        ".cpp",
        ".cs",
        ".csproj",
        ".cfg",
        ".cxx",
        ".gd",
        ".godot",
        ".h",
        ".hpp",
        ".json",
        ".md",
        ".props",
        ".py",
        ".sln",
        ".targets",
        ".tres",
        ".tscn",
        ".toml",
        ".yaml",
        ".yml",
    }
)

EXCLUDED_DIRECTORIES = frozenset(
    {
        ".git",
        ".godot",
        ".idea",
        ".mypy_cache",
        ".pytest_cache",
        ".ruff_cache",
        ".tmp",
        ".venv",
        ".vs",
        ".vscode",
        "__pycache__",
        "artifacts",
        "bin",
        "build",
        "coverage",
        "dist",
        "generated",
        "node_modules",
        "obj",
        "old",
        "target",
        "tmp",
        "vendor",
        "venv",
    }
)

EXCLUDED_SUFFIXES = frozenset(
    {
        ".bin",
        ".db",
        ".key",
        ".model",
        ".onnx",
        ".p12",
        ".pem",
        ".sqlite",
        ".sqlite3",
    }
)

CSHARP_TYPE_PATTERN = re.compile(
    r"(?m)^\s*(?:\[[^\]\n]+\]\s*)*"
    r"(?:(?:public|internal|private|protected|abstract|sealed|static|partial|"
    r"readonly|ref|new)\s+)*"
    r"(?P<declaration>record\s+(?:class\s+|struct\s+)?|class\s+|"
    r"interface\s+|struct\s+|enum\s+)"
    r"(?P<name>[A-Za-z_]\w*)"
)
CSHARP_NAMESPACE_PATTERN = re.compile(
    r"(?m)^\s*namespace\s+(?P<name>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*[;{]"
)
CSHARP_USING_PATTERN = re.compile(
    r"(?m)^\s*using\s+(?:static\s+)?(?:[A-Za-z_]\w*\s*=\s*)?"
    r"(?P<name>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*;"
)
CSHARP_MAP_ROUTE_PATTERN = re.compile(
    r"\bMap(?P<verb>Get|Post|Put|Delete|Patch)\s*\(\s*"
    r'"(?P<path>/[^"\r\n]*)"\s*,\s*'
    r"(?P<handler>[A-Za-z_]\w*)\s*\.\s*[A-Za-z_]\w*"
)
CSHARP_ATTRIBUTE_ROUTE_PATTERN = re.compile(
    r"\[Http(?P<verb>Get|Post|Put|Delete|Patch)"
    r"(?:\s*\(\s*\"(?P<path>[^\"\r\n]*)\"\s*\))?\s*\]"
)
CSHARP_CONTROLLER_ROUTE_PATTERN = re.compile(
    r"\[Route\s*\(\s*\"(?P<path>[^\"\r\n]*)\"\s*\)\s*\]"
)
CSHARP_CLIENT_CALL_PATTERN = re.compile(
    r"\b(?P<method>GetFromJsonAsync|GetAsync|PostAsJsonAsync|PostAsync|"
    r"PutAsJsonAsync|PutAsync|DeleteAsync|PatchAsync)"
    r"(?:\s*<[^()\r\n]*>)?\s*\(\s*"
    r'"(?P<path>/[^"\r\n]*)"'
)

HTTP_METHODS = {
    "DeleteAsync": "DELETE",
    "GetAsync": "GET",
    "GetFromJsonAsync": "GET",
    "PatchAsync": "PATCH",
    "PostAsJsonAsync": "POST",
    "PostAsync": "POST",
    "PutAsJsonAsync": "PUT",
    "PutAsync": "PUT",
}


@dataclass(frozen=True)
class SourceFile:
    path: str
    raw: bytes
    text: str

    @property
    def suffix(self) -> str:
        return PurePosixPath(self.path).suffix.lower()


@dataclass(frozen=True)
class CSharpType:
    id: str
    kind: str
    name: str
    namespace: str
    path: str
    line: int
    start: int
    end: int


@dataclass(frozen=True)
class PythonModule:
    id: str
    name: str
    path: str


class GraphBuilder:
    def __init__(self) -> None:
        self._nodes: dict[str, dict[str, Any]] = {}
        self._edges: dict[tuple[Any, ...], dict[str, Any]] = {}
        self._diagnostics: dict[tuple[Any, ...], dict[str, Any]] = {}

    @property
    def nodes(self) -> dict[str, dict[str, Any]]:
        return self._nodes

    def add_node(
        self,
        node_id: str,
        kind: str,
        name: str,
        path: str | None = None,
        line: int | None = None,
        metadata: dict[str, Any] | None = None,
    ) -> str:
        node: dict[str, Any] = {"id": node_id, "kind": kind, "name": name}
        if path is not None:
            node["path"] = path
        if line is not None:
            node["line"] = line
        if metadata:
            node["metadata"] = dict(sorted(metadata.items()))

        existing = self._nodes.get(node_id)
        if existing is None:
            self._nodes[node_id] = node
        elif existing != node:
            self.add_diagnostic(
                path or existing.get("path", ""),
                line or existing.get("line", 1),
                f"Identifiant de nœud ambigu ignoré : {node_id}",
            )
        return node_id

    def add_edge(
        self,
        source: str,
        target: str,
        kind: str,
        evidence_path: str,
        evidence_line: int,
    ) -> None:
        if source not in self._nodes or target not in self._nodes:
            return
        evidence = {"line": evidence_line, "path": evidence_path}
        key = (source, target, kind, evidence_path, evidence_line)
        self._edges[key] = {
            "source": source,
            "target": target,
            "kind": kind,
            "evidence": evidence,
        }

    def add_diagnostic(self, path: str, line: int, message: str) -> None:
        key = (path, line, message)
        self._diagnostics[key] = {
            "path": path,
            "line": line,
            "message": message,
        }

    def finish(self, metadata: dict[str, Any]) -> dict[str, Any]:
        nodes = sorted(self._nodes.values(), key=lambda node: node["id"])
        edges = sorted(
            self._edges.values(),
            key=lambda edge: (
                edge["source"],
                edge["target"],
                edge["kind"],
                edge["evidence"]["path"],
                edge["evidence"]["line"],
            ),
        )
        diagnostics = sorted(
            self._diagnostics.values(),
            key=lambda item: (item["path"], item["line"], item["message"]),
        )
        return {
            "metadata": dict(sorted(metadata.items())),
            "nodes": nodes,
            "edges": edges,
            "diagnostics": diagnostics,
        }


def normalize_relative_path(path: Path) -> str:
    # ``path`` already comes from Path.relative_to(). Removing leading dots
    # would corrupt legitimate repository directories such as ``.github``.
    return path.as_posix()


def contains_excluded_directory(parts: tuple[str, ...]) -> bool:
    for index, part in enumerate(parts):
        if part not in EXCLUDED_DIRECTORIES:
            continue
        if (
            part == "build"
            and index == 2
            and len(parts) >= 3
            and parts[0] in {"src", "tests"}
        ):
            continue
        return True
    return False


def is_excluded_file(
    relative_path: str, extra_excluded_paths: frozenset[str] = frozenset()
) -> bool:
    path = PurePosixPath(relative_path)
    lower_parts = tuple(part.lower() for part in path.parts)
    name = path.name.lower()
    suffix = path.suffix.lower()

    if contains_excluded_directory(lower_parts[:-1]):
        return True
    if name == ".env" or name.startswith(".env."):
        return True
    if suffix in EXCLUDED_SUFFIXES or name.endswith(".sqlite-journal"):
        return True
    if relative_path in extra_excluded_paths or relative_path in {
        DEFAULT_GRAPH_PATH.as_posix(),
        f"{DEFAULT_GRAPH_PATH.parent.as_posix()}/{DEFAULT_OVERVIEW_NAME}",
    }:
        return True
    return False


def discover_sources(
    root: Path, extra_excluded_paths: frozenset[str] = frozenset()
) -> list[SourceFile]:
    sources: list[SourceFile] = []
    for current_root, directory_names, file_names in os.walk(root):
        current = Path(current_root)
        directory_names[:] = sorted(
            name
            for name in directory_names
            if not contains_excluded_directory(
                tuple(
                    part.lower()
                    for part in (current / name).relative_to(root).parts
                )
            )
        )
        for file_name in sorted(file_names):
            absolute_path = current / file_name
            relative_path = normalize_relative_path(absolute_path.relative_to(root))
            if is_excluded_file(relative_path, extra_excluded_paths):
                continue
            if absolute_path.suffix.lower() not in ANALYZED_SUFFIXES:
                continue
            raw = absolute_path.read_bytes()
            text = raw.decode("utf-8", errors="replace")
            sources.append(SourceFile(relative_path, raw, text))
    return sorted(sources, key=lambda source: source.path)


def source_digest(sources: Iterable[SourceFile]) -> str:
    digest = hashlib.sha256()
    for source in sources:
        digest.update(source.path.encode("utf-8"))
        digest.update(b"\0")
        # Git may materialize the same text with LF or CRLF depending on the
        # checkout platform. The graph describes source semantics, so line
        # endings must not make an otherwise identical graph stale.
        normalized_raw = source.raw.replace(b"\r\n", b"\n").replace(b"\r", b"\n")
        digest.update(normalized_raw)
        digest.update(b"\0")
    return f"sha256:{digest.hexdigest()}"


def line_number(text: str, offset: int) -> int:
    return text.count("\n", 0, offset) + 1


def file_kind(source: SourceFile) -> str:
    path = PurePosixPath(source.path)
    if source.suffix == ".md":
        return "decision" if tuple(part.lower() for part in path.parts[:2]) == (
            "docs",
            "adr",
        ) else "document"
    if source.suffix in {
        ".cfg",
        ".csproj",
        ".godot",
        ".json",
        ".props",
        ".sln",
        ".targets",
        ".toml",
        ".tres",
        ".tscn",
        ".yaml",
        ".yml",
    }:
        return "configuration"
    return "file"


def add_file_nodes(builder: GraphBuilder, sources: Sequence[SourceFile]) -> None:
    for source in sources:
        builder.add_node(
            f"file:{source.path}",
            file_kind(source),
            PurePosixPath(source.path).name,
            source.path,
            1,
            {"language": language_for_suffix(source.suffix)},
        )
        if "\ufffd" in source.text:
            builder.add_diagnostic(
                source.path,
                1,
                "Le fichier contient des octets UTF-8 invalides remplacés pendant l’analyse.",
            )


def language_for_suffix(suffix: str) -> str:
    if suffix == ".cs":
        return "csharp"
    if suffix == ".py":
        return "python"
    if suffix == ".gd":
        return "gdscript"
    if suffix in {".cc", ".cpp", ".cxx", ".h", ".hpp"}:
        return "cpp"
    if suffix == ".md":
        return "markdown"
    return "configuration"


def mask_c_like_comments(text: str) -> str:
    output = list(text)
    state = "normal"
    index = 0
    while index < len(text):
        current = text[index]
        following = text[index + 1] if index + 1 < len(text) else ""
        if state == "normal":
            if current == '"':
                state = "string"
            elif current == "'":
                state = "character"
            elif current == "/" and following == "/":
                output[index] = output[index + 1] = " "
                state = "line_comment"
                index += 1
            elif current == "/" and following == "*":
                output[index] = output[index + 1] = " "
                state = "block_comment"
                index += 1
        elif state == "string":
            if current == "\\":
                index += 1
            elif current == '"':
                state = "normal"
        elif state == "character":
            if current == "\\":
                index += 1
            elif current == "'":
                state = "normal"
        elif state == "line_comment":
            if current == "\n":
                state = "normal"
            else:
                output[index] = " "
        elif state == "block_comment":
            if current == "*" and following == "/":
                output[index] = output[index + 1] = " "
                state = "normal"
                index += 1
            elif current != "\n":
                output[index] = " "
        index += 1
    return "".join(output)


def classify_type(name: str, path: str) -> str:
    lower_name = name.lower()
    lower_parts = tuple(part.lower() for part in PurePosixPath(path).parts)
    if "tests" in lower_parts or lower_name.endswith(("test", "tests")):
        return "test"
    if lower_name.endswith(("controller", "endpoint", "endpoints", "handler")):
        return "handler"
    if lower_name.startswith("i") and lower_name.endswith(
        ("service", "repository", "runtime", "provider")
    ):
        return "interface"
    if lower_name.endswith("service"):
        return "service"
    if lower_name.endswith("validator"):
        return "validator"
    if lower_name.endswith("repository"):
        return "repository"
    if lower_name.endswith(("serializer", "converter", "adapter")):
        return "adapter"
    if lower_name.endswith(("apiclient", "client")):
        return "client"
    if lower_name.endswith(
        ("screen", "view", "page", "component", "dock", "toolset", "plugin")
    ):
        return "component"
    if lower_name.endswith(
        (
            "definition",
            "model",
            "entity",
            "dto",
            "command",
            "event",
            "value",
            "id",
            "instance",
            "kind",
            "document",
            "diagnostic",
            "state",
            "patch",
            "result",
            "entry",
        )
    ):
        return "model"
    return "type"


def analyze_csharp(
    builder: GraphBuilder, sources: Sequence[SourceFile]
) -> tuple[list[CSharpType], dict[tuple[str, str], str]]:
    csharp_sources = [source for source in sources if source.suffix == ".cs"]
    types: list[CSharpType] = []
    namespaces: dict[str, str] = {}
    masked_by_path: dict[str, str] = {}

    for source in csharp_sources:
        masked = mask_c_like_comments(source.text)
        masked_by_path[source.path] = masked
        namespace_match = CSHARP_NAMESPACE_PATTERN.search(masked)
        namespace = namespace_match.group("name") if namespace_match else ""
        if namespace:
            namespace_id = f"namespace:{namespace}"
            namespaces[namespace] = builder.add_node(
                namespace_id,
                "namespace",
                namespace,
            )
            builder.add_edge(
                namespace_id,
                f"file:{source.path}",
                "defined_in",
                source.path,
                line_number(masked, namespace_match.start()),
            )

        type_matches = list(CSHARP_TYPE_PATTERN.finditer(masked))
        for match_index, match in enumerate(type_matches):
            name = match.group("name")
            kind = classify_type(name, source.path)
            qualified_name = f"{namespace}.{name}" if namespace else name
            node_id = f"{kind}:{qualified_name}"
            definition_line = line_number(masked, match.start("name"))
            builder.add_node(
                node_id,
                kind,
                name,
                source.path,
                definition_line,
                {"namespace": namespace} if namespace else None,
            )
            builder.add_edge(
                node_id,
                f"file:{source.path}",
                "defined_in",
                source.path,
                definition_line,
            )
            types.append(
                CSharpType(
                    node_id,
                    kind,
                    name,
                    namespace,
                    source.path,
                    definition_line,
                    match.start(),
                    (
                        type_matches[match_index + 1].start()
                        if match_index + 1 < len(type_matches)
                        else len(masked)
                    ),
                )
            )

    namespace_names = sorted(namespaces, key=lambda item: (-len(item), item))
    for source in csharp_sources:
        masked = masked_by_path[source.path]
        for match in CSHARP_USING_PATTERN.finditer(masked):
            imported = match.group("name")
            matched_namespace = next(
                (
                    namespace
                    for namespace in namespace_names
                    if imported == namespace or imported.startswith(f"{namespace}.")
                ),
                None,
            )
            if matched_namespace:
                builder.add_edge(
                    f"file:{source.path}",
                    namespaces[matched_namespace],
                    "imports",
                    source.path,
                    line_number(masked, match.start()),
                )

    type_names: dict[str, list[CSharpType]] = defaultdict(list)
    for type_info in types:
        type_names[type_info.name].append(type_info)

    for source_type in types:
        full_masked = masked_by_path[source_type.path]
        masked = full_masked[source_type.start : source_type.end]
        for target_name, candidates in sorted(type_names.items()):
            if target_name == source_type.name or len(candidates) != 1:
                continue
            match = re.search(rf"\b{re.escape(target_name)}\b", masked)
            if match is None:
                continue
            target = candidates[0]
            relationship = relationship_for_types(source_type.kind, target.kind)
            if relationship is not None:
                builder.add_edge(
                    source_type.id,
                    target.id,
                    relationship,
                    source_type.path,
                    line_number(full_masked, source_type.start + match.start()),
                )

    routes: dict[tuple[str, str], str] = {}
    for source in csharp_sources:
        masked = masked_by_path[source.path]
        source_types = [type_info for type_info in types if type_info.path == source.path]
        for match in CSHARP_MAP_ROUTE_PATTERN.finditer(masked):
            verb = match.group("verb").upper()
            route_path = normalize_route(match.group("path"))
            route_id = add_route_node(
                builder,
                routes,
                verb,
                route_path,
                source.path,
                line_number(masked, match.start()),
            )
            handler_name = match.group("handler")
            candidates = type_names.get(handler_name, [])
            if len(candidates) == 1:
                builder.add_edge(
                    route_id,
                    candidates[0].id,
                    "handled_by",
                    source.path,
                    line_number(masked, match.start("handler")),
                )

        controller_prefix_match = CSHARP_CONTROLLER_ROUTE_PATTERN.search(masked)
        controller_prefix = (
            controller_prefix_match.group("path") if controller_prefix_match else ""
        )
        handler_type = next(
            (type_info for type_info in source_types if type_info.kind == "handler"),
            source_types[0] if len(source_types) == 1 else None,
        )
        for match in CSHARP_ATTRIBUTE_ROUTE_PATTERN.finditer(masked):
            verb = match.group("verb").upper()
            suffix = match.group("path") or ""
            prefix = controller_prefix
            if handler_type:
                controller_name = re.sub(r"Controller$", "", handler_type.name)
                prefix = prefix.replace("[controller]", controller_name.lower())
            route_path = normalize_route(f"{prefix}/{suffix}")
            route_id = add_route_node(
                builder,
                routes,
                verb,
                route_path,
                source.path,
                line_number(masked, match.start()),
            )
            if handler_type:
                builder.add_edge(
                    route_id,
                    handler_type.id,
                    "handled_by",
                    source.path,
                    line_number(masked, match.start()),
                )

    operations: list[tuple[str, str, str]] = []
    for client_type in (item for item in types if item.kind == "client"):
        masked = masked_by_path[client_type.path]
        for match in CSHARP_CLIENT_CALL_PATTERN.finditer(masked):
            verb = HTTP_METHODS[match.group("method")]
            route_path = normalize_route(match.group("path"))
            operation_id = (
                f"api_operation:{client_type.namespace}.{client_type.name}:"
                f"{verb}:{route_path}"
                if client_type.namespace
                else f"api_operation:{client_type.name}:{verb}:{route_path}"
            )
            operation_line = line_number(masked, match.start())
            builder.add_node(
                operation_id,
                "api_operation",
                f"{verb} {route_path}",
                client_type.path,
                operation_line,
                {"http_method": verb, "route": route_path},
            )
            builder.add_edge(
                operation_id,
                f"file:{client_type.path}",
                "defined_in",
                client_type.path,
                operation_line,
            )
            builder.add_edge(
                client_type.id,
                operation_id,
                "calls_api",
                client_type.path,
                operation_line,
            )
            operations.append((operation_id, verb, route_path))

    for operation_id, verb, route_path in operations:
        route_id = routes.get((verb, route_path))
        if route_id:
            operation_node = builder.nodes[operation_id]
            builder.add_edge(
                operation_id,
                route_id,
                "calls_route",
                operation_node["path"],
                operation_node["line"],
            )

    return types, routes


def relationship_for_types(source_kind: str, target_kind: str) -> str | None:
    if source_kind == "test" and target_kind != "test":
        return "tests"
    if source_kind == "component" and target_kind == "client":
        return "calls_api"
    if source_kind in {"handler", "component", "type"} and target_kind == "service":
        return "uses_service"
    if source_kind == "handler" and target_kind == "model":
        return "uses_model"
    if source_kind == "adapter" and target_kind == "validator":
        return "uses_service"
    if source_kind == "service" and target_kind == "repository":
        return "uses_repository"
    if source_kind in {"repository", "service"} and target_kind == "model":
        return "accesses_model"
    if source_kind == "adapter" and target_kind == "model":
        return "serializes"
    if source_kind == "validator" and target_kind == "model":
        return "validates"
    if source_kind == "model" and target_kind == "model":
        return "relationship"
    if source_kind == "client" and target_kind == "model":
        return "uses_model"
    return None


def normalize_route(route: str) -> str:
    route = route.strip().split("?", 1)[0].split("#", 1)[0]
    route = re.sub(r"\$?\{[^{}]+\}", "{}", route)
    route = re.sub(r"(?<=/):[A-Za-z_]\w*", "{}", route)
    route = re.sub(r"/+", "/", f"/{route.lstrip('/')}")
    if len(route) > 1:
        route = route.rstrip("/")
    return route


def add_route_node(
    builder: GraphBuilder,
    routes: dict[tuple[str, str], str],
    verb: str,
    route_path: str,
    source_path: str,
    source_line: int,
) -> str:
    route_id = f"route:{verb}:{route_path}"
    builder.add_node(
        route_id,
        "route",
        f"{verb} {route_path}",
        source_path,
        source_line,
        {"http_method": verb, "route": route_path},
    )
    builder.add_edge(
        route_id,
        f"file:{source_path}",
        "defined_in",
        source_path,
        source_line,
    )
    routes[(verb, route_path)] = route_id
    return route_id


def module_name_for_path(path: str) -> str:
    pure_path = PurePosixPath(path)
    without_suffix = pure_path.with_suffix("")
    parts = list(without_suffix.parts)
    if parts and parts[-1] == "__init__":
        parts.pop()
    return ".".join(parts)


def analyze_python(builder: GraphBuilder, sources: Sequence[SourceFile]) -> None:
    python_sources = [source for source in sources if source.suffix == ".py"]
    modules: dict[str, PythonModule] = {}
    trees: dict[str, ast.AST] = {}
    symbols: dict[tuple[str, str], str] = {}

    for source in python_sources:
        module_name = module_name_for_path(source.path)
        module_id = f"module:{module_name}"
        modules[module_name] = PythonModule(module_id, module_name, source.path)
        builder.add_node(module_id, "module", module_name, source.path, 1)
        builder.add_edge(module_id, f"file:{source.path}", "defined_in", source.path, 1)
        try:
            trees[source.path] = ast.parse(source.text, filename=source.path)
        except SyntaxError as error:
            builder.add_diagnostic(
                source.path,
                error.lineno or 1,
                f"Syntaxe Python ignorée : {error.msg}",
            )
            continue

        for node in ast.iter_child_nodes(trees[source.path]):
            if isinstance(node, (ast.ClassDef, ast.FunctionDef, ast.AsyncFunctionDef)):
                kind = (
                    classify_type(node.name, source.path)
                    if isinstance(node, ast.ClassDef)
                    else ("test" if node.name.startswith("test_") else "function")
                )
                symbol_id = f"{kind}:{module_name}.{node.name}"
                builder.add_node(
                    symbol_id,
                    kind,
                    node.name,
                    source.path,
                    node.lineno,
                    {"module": module_name},
                )
                builder.add_edge(
                    symbol_id,
                    f"file:{source.path}",
                    "defined_in",
                    source.path,
                    node.lineno,
                )
                symbols[(module_name, node.name)] = symbol_id

    for source in python_sources:
        tree = trees.get(source.path)
        if tree is None:
            continue
        source_module = modules[module_name_for_path(source.path)]
        is_test_module = "tests" in tuple(
            part.lower() for part in PurePosixPath(source.path).parts
        )
        for node in ast.walk(tree):
            imported_modules: list[str] = []
            imported_symbols: list[tuple[str, str]] = []
            if isinstance(node, ast.Import):
                imported_modules.extend(alias.name for alias in node.names)
            elif isinstance(node, ast.ImportFrom):
                base = resolve_python_import(source_module.name, node.module, node.level)
                if base:
                    imported_modules.append(base)
                    imported_symbols.extend((base, alias.name) for alias in node.names)
            else:
                continue

            for imported in imported_modules:
                target_name = longest_internal_module(imported, modules)
                if target_name:
                    target_module = modules[target_name]
                    builder.add_edge(
                        source_module.id,
                        target_module.id,
                        "imports",
                        source.path,
                        getattr(node, "lineno", 1),
                    )
                    if is_test_module:
                        builder.add_edge(
                            source_module.id,
                            target_module.id,
                            "tests",
                            source.path,
                            getattr(node, "lineno", 1),
                        )

            if is_test_module:
                for module_name, symbol_name in imported_symbols:
                    target_id = symbols.get((module_name, symbol_name))
                    if target_id:
                        builder.add_edge(
                            source_module.id,
                            target_id,
                            "tests",
                            source.path,
                            getattr(node, "lineno", 1),
                        )


def resolve_python_import(current: str, module: str | None, level: int) -> str:
    if level == 0:
        return module or ""
    current_parts = current.split(".")[:-1]
    keep = max(0, len(current_parts) - level + 1)
    parts = current_parts[:keep]
    if module:
        parts.extend(module.split("."))
    return ".".join(parts)


def longest_internal_module(
    imported: str, modules: dict[str, PythonModule]
) -> str | None:
    candidates = [
        name
        for name in modules
        if imported == name or imported.startswith(f"{name}.")
    ]
    return max(candidates, key=len) if candidates else None


def analyze_markdown(builder: GraphBuilder, sources: Sequence[SourceFile]) -> None:
    known_paths = {source.path for source in sources}
    markdown_sources = [source for source in sources if source.suffix == ".md"]
    markdown_link = re.compile(r"\[[^\]]*\]\((?P<target>[^)]+)\)")
    inline_code = re.compile(r"`(?P<target>[^`\r\n]+)`")

    for source in markdown_sources:
        candidates: list[tuple[str, int]] = []
        for pattern in (markdown_link, inline_code):
            for match in pattern.finditer(source.text):
                target = match.group("target").strip()
                candidates.append((target, match.start("target")))

        for target, offset in sorted(candidates, key=lambda item: (item[1], item[0])):
            resolved = resolve_markdown_target(source.path, target)
            if resolved in known_paths:
                builder.add_edge(
                    f"file:{source.path}",
                    f"file:{resolved}",
                    "references",
                    source.path,
                    line_number(source.text, offset),
                )


def resolve_markdown_target(source_path: str, target: str) -> str:
    target = target.strip().strip("<>")
    target = target.split("#", 1)[0].split("?", 1)[0]
    if not target or re.match(r"^[A-Za-z][A-Za-z0-9+.-]*:", target):
        return ""
    if any(character in target for character in ("*", "{", "}", "[", "]")):
        return ""
    base = str(PurePosixPath(source_path).parent)
    if base == ".":
        base = ""
    return posixpath.normpath(posixpath.join(base, target.replace("\\", "/")))


def analyze_cpp_includes(builder: GraphBuilder, sources: Sequence[SourceFile]) -> None:
    known_paths = {source.path for source in sources}
    cpp_suffixes = {".cc", ".cpp", ".cxx", ".h", ".hpp"}
    include_pattern = re.compile(r'(?m)^\s*#\s*include\s*"(?P<path>[^"\r\n]+)"')
    for source in (item for item in sources if item.suffix in cpp_suffixes):
        for match in include_pattern.finditer(mask_c_like_comments(source.text)):
            target = resolve_source_reference(source.path, match.group("path"), known_paths)
            if target:
                builder.add_edge(
                    f"file:{source.path}",
                    f"file:{target}",
                    "imports",
                    source.path,
                    line_number(source.text, match.start()),
                )


def analyze_dotnet_projects(builder: GraphBuilder, sources: Sequence[SourceFile]) -> None:
    project_sources = [source for source in sources if source.suffix == ".csproj"]
    known_paths = {source.path for source in sources}
    projects: dict[str, str] = {}
    reference_pattern = re.compile(
        r'(?i)<ProjectReference\s+Include\s*=\s*"(?P<path>[^"\r\n]+)"'
    )
    for source in project_sources:
        project_id = f"project:{PurePosixPath(source.path).with_suffix('').as_posix()}"
        projects[source.path] = builder.add_node(
            project_id,
            "project",
            PurePosixPath(source.path).stem,
            source.path,
            1,
        )
        builder.add_edge(
            project_id,
            f"file:{source.path}",
            "defined_in",
            source.path,
            1,
        )

    for source in project_sources:
        for match in reference_pattern.finditer(source.text):
            target_path = resolve_source_reference(
                source.path, match.group("path"), known_paths
            )
            target_id = projects.get(target_path or "")
            if target_id:
                builder.add_edge(
                    projects[source.path],
                    target_id,
                    "imports",
                    source.path,
                    line_number(source.text, match.start()),
                )


def analyze_godot_references(
    builder: GraphBuilder, sources: Sequence[SourceFile]
) -> None:
    known_paths = {source.path for source in sources}
    reference_pattern = re.compile(
        r'"res://(?P<path>[^"\r\n]+)"'
    )
    relative_script_pattern = re.compile(
        r'(?m)^\s*script\s*=\s*"(?P<path>[^"\r\n]+)"\s*$'
    )
    godot_suffixes = {".cfg", ".gd", ".godot", ".tres", ".tscn"}
    for source in (item for item in sources if item.suffix in godot_suffixes):
        for match in reference_pattern.finditer(source.text):
            project_prefix = godot_project_prefix(source.path, known_paths)
            target = posixpath.normpath(
                posixpath.join(project_prefix, match.group("path"))
            )
            if target in known_paths:
                builder.add_edge(
                    f"file:{source.path}",
                    f"file:{target}",
                    "imports",
                    source.path,
                    line_number(source.text, match.start()),
                )
        if source.suffix == ".cfg":
            for match in relative_script_pattern.finditer(source.text):
                target = resolve_source_reference(
                    source.path, match.group("path"), known_paths
                )
                if target:
                    builder.add_edge(
                        f"file:{source.path}",
                        f"file:{target}",
                        "imports",
                        source.path,
                        line_number(source.text, match.start()),
                    )


def godot_project_prefix(source_path: str, known_paths: set[str]) -> str:
    parent = PurePosixPath(source_path).parent
    while True:
        candidate = (parent / "project.godot").as_posix()
        if candidate in known_paths:
            return "" if parent == PurePosixPath(".") else parent.as_posix()
        if parent == PurePosixPath("."):
            return ""
        parent = parent.parent


def resolve_source_reference(
    source_path: str, target: str, known_paths: set[str]
) -> str | None:
    target = target.replace("\\", "/")
    candidates = [
        posixpath.normpath(
            posixpath.join(str(PurePosixPath(source_path).parent), target)
        ),
        posixpath.normpath(target),
    ]
    return next((candidate for candidate in candidates if candidate in known_paths), None)


def build_graph(
    root: Path, extra_excluded_paths: frozenset[str] = frozenset()
) -> dict[str, Any]:
    root = root.resolve()
    sources = discover_sources(root, extra_excluded_paths)
    builder = GraphBuilder()
    add_file_nodes(builder, sources)
    analyze_csharp(builder, sources)
    analyze_python(builder, sources)
    analyze_markdown(builder, sources)
    analyze_cpp_includes(builder, sources)
    analyze_dotnet_projects(builder, sources)
    analyze_godot_references(builder, sources)
    metadata = {
        "analyzers": ["cpp", "csharp", "dotnet", "godot", "markdown", "python"],
        "schema_version": SCHEMA_VERSION,
        "source_digest": source_digest(sources),
        "source_file_count": len(sources),
    }
    return builder.finish(metadata)


def serialize_graph(graph: dict[str, Any]) -> bytes:
    return (
        json.dumps(graph, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    ).encode("utf-8")


def mermaid_id(value: str) -> str:
    return f"k_{hashlib.sha1(value.encode('utf-8')).hexdigest()[:12]}"


def escape_mermaid_label(value: str) -> str:
    return value.replace("\\", "\\\\").replace('"', "&quot;")


def build_overview(graph: dict[str, Any]) -> bytes:
    node_by_id = {node["id"]: node for node in graph["nodes"]}
    kind_counts = Counter(node["kind"] for node in graph["nodes"])
    selected_kinds = {
        kind
        for kind, _count in sorted(
            kind_counts.items(), key=lambda item: (-item[1], item[0])
        )[:MAX_OVERVIEW_KINDS]
    }
    edge_counts: Counter[tuple[str, str, str]] = Counter()
    for edge in graph["edges"]:
        source = node_by_id.get(edge["source"])
        target = node_by_id.get(edge["target"])
        if not source or not target:
            continue
        if source["kind"] in selected_kinds and target["kind"] in selected_kinds:
            edge_counts[(source["kind"], target["kind"], edge["kind"])] += 1

    lines = [
        "%% Fichier généré par scripts/architecture_graph.py. Ne pas modifier.",
        "flowchart LR",
    ]
    for kind in sorted(selected_kinds):
        lines.append(
            f'  {mermaid_id(kind)}["{escape_mermaid_label(kind)} ({kind_counts[kind]})"]'
        )
    selected_edges = sorted(
        edge_counts.items(),
        key=lambda item: (-item[1], item[0][0], item[0][1], item[0][2]),
    )[:MAX_OVERVIEW_EDGES]
    for (source_kind, target_kind, edge_kind), count in selected_edges:
        label = escape_mermaid_label(f"{edge_kind} ({count})")
        lines.append(
            f'  {mermaid_id(source_kind)} -->|"{label}"| {mermaid_id(target_kind)}'
        )
    if not selected_kinds:
        lines.append("  empty[\"Aucun nœud analysable\"]")
    return ("\n".join(lines) + "\n").encode("utf-8")


def resolve_output_paths(root: Path, output: Path) -> tuple[Path, Path]:
    graph_path = output if output.is_absolute() else root / output
    overview_path = graph_path.parent / DEFAULT_OVERVIEW_NAME
    return graph_path, overview_path


def artifact_exclusions(root: Path, paths: Iterable[Path]) -> frozenset[str]:
    exclusions: set[str] = set()
    for path in paths:
        try:
            exclusions.add(normalize_relative_path(path.resolve().relative_to(root.resolve())))
        except ValueError:
            continue
    return frozenset(exclusions)


def display_path(root: Path, path: Path) -> str:
    try:
        return normalize_relative_path(path.resolve().relative_to(root.resolve()))
    except ValueError:
        return str(path.resolve())


def atomic_write_many(artifacts: Sequence[tuple[Path, bytes]]) -> None:
    staged: list[tuple[Path, Path]] = []
    try:
        for destination, content in artifacts:
            destination.parent.mkdir(parents=True, exist_ok=True)
            descriptor, temporary_name = tempfile.mkstemp(
                prefix=f".{destination.name}.",
                suffix=".tmp",
                dir=destination.parent,
            )
            temporary_path = Path(temporary_name)
            with os.fdopen(descriptor, "wb") as stream:
                stream.write(content)
                stream.flush()
                os.fsync(stream.fileno())
            staged.append((temporary_path, destination))
        for temporary_path, destination in staged:
            os.replace(temporary_path, destination)
    finally:
        for temporary_path, _destination in staged:
            if temporary_path.exists():
                temporary_path.unlink()


def generate(root: Path, output: Path) -> dict[str, Any]:
    graph_path, overview_path = resolve_output_paths(root, output)
    graph = build_graph(root, artifact_exclusions(root, (graph_path, overview_path)))
    atomic_write_many(
        [
            (graph_path, serialize_graph(graph)),
            (overview_path, build_overview(graph)),
        ]
    )
    return graph


def check(root: Path, output: Path) -> tuple[bool, list[str]]:
    graph_path, overview_path = resolve_output_paths(root, output)
    graph = build_graph(root, artifact_exclusions(root, (graph_path, overview_path)))
    expected = {
        graph_path: serialize_graph(graph),
        overview_path: build_overview(graph),
    }
    errors: list[str] = []
    for path, content in expected.items():
        if not path.exists():
            errors.append(f"Artefact absent : {display_path(root, path)}")
        elif path.read_bytes() != content:
            errors.append(f"Artefact périmé : {display_path(root, path)}")
    if errors:
        errors.append("Exécuter : python scripts/architecture_graph.py generate")
    return not errors, errors


def load_graph(root: Path, output: Path) -> dict[str, Any]:
    graph_path, _overview_path = resolve_output_paths(root, output)
    with graph_path.open("r", encoding="utf-8") as stream:
        graph = json.load(stream)
    if graph.get("metadata", {}).get("schema_version") != SCHEMA_VERSION:
        raise ValueError(
            "Version de schéma incompatible ; régénérez le graphe avec le script courant."
        )
    return graph


def graph_stats(graph: dict[str, Any]) -> str:
    node_counts = Counter(node["kind"] for node in graph["nodes"])
    edge_counts = Counter(edge["kind"] for edge in graph["edges"])
    lines = [
        f"schema_version: {graph['metadata']['schema_version']}",
        f"source_digest: {graph['metadata']['source_digest']}",
        f"source_files: {graph['metadata']['source_file_count']}",
        f"nodes_total: {len(graph['nodes'])}",
    ]
    lines.extend(f"nodes.{kind}: {node_counts[kind]}" for kind in sorted(node_counts))
    lines.append(f"edges_total: {len(graph['edges'])}")
    lines.extend(f"edges.{kind}: {edge_counts[kind]}" for kind in sorted(edge_counts))
    lines.append(f"diagnostics: {len(graph.get('diagnostics', []))}")
    return "\n".join(lines)


def select_subgraph(
    graph: dict[str, Any], query: str, depth: int, max_nodes: int
) -> dict[str, Any]:
    normalized_query = " ".join(query.casefold().replace(",", " ").split())
    tokens = normalized_query.split()
    scored: list[tuple[int, str]] = []
    for node in graph["nodes"]:
        fields = [
            str(node.get("id", "")),
            str(node.get("name", "")),
            str(node.get("path", "")),
            json.dumps(node.get("metadata", {}), ensure_ascii=False, sort_keys=True),
        ]
        haystack = " ".join(fields).casefold()
        if not normalized_query or not all(token in haystack for token in tokens):
            continue
        score = 10
        if node.get("name", "").casefold() == normalized_query:
            score += 100
        if node.get("id", "").casefold() == normalized_query:
            score += 120
        if normalized_query in node.get("name", "").casefold():
            score += 40
        if normalized_query in node.get("path", "").casefold():
            score += 20
        scored.append((score, node["id"]))

    seeds = [
        node_id
        for _score, node_id in sorted(scored, key=lambda item: (-item[0], item[1]))[
            :max_nodes
        ]
    ]
    if not seeds:
        return {"query": query, "nodes": [], "edges": []}

    adjacency: dict[str, list[tuple[str, dict[str, Any]]]] = defaultdict(list)
    for edge in graph["edges"]:
        adjacency[edge["source"]].append((edge["target"], edge))
        adjacency[edge["target"]].append((edge["source"], edge))
    for entries in adjacency.values():
        entries.sort(
            key=lambda item: (
                item[1]["kind"],
                item[0],
                item[1]["source"],
                item[1]["target"],
            )
        )

    selected: set[str] = set(seeds)
    queue = deque((node_id, 0) for node_id in seeds)
    while queue and len(selected) < max_nodes:
        node_id, current_depth = queue.popleft()
        if current_depth >= depth:
            continue
        for neighbor, _edge in adjacency.get(node_id, []):
            if neighbor in selected:
                continue
            selected.add(neighbor)
            queue.append((neighbor, current_depth + 1))
            if len(selected) >= max_nodes:
                break

    nodes = sorted(
        (node for node in graph["nodes"] if node["id"] in selected),
        key=lambda node: node["id"],
    )
    edges = sorted(
        (
            edge
            for edge in graph["edges"]
            if edge["source"] in selected and edge["target"] in selected
        ),
        key=lambda edge: (
            edge["source"],
            edge["target"],
            edge["kind"],
            edge["evidence"]["path"],
            edge["evidence"]["line"],
        ),
    )
    return {"query": query, "nodes": nodes, "edges": edges}


def format_query(result: dict[str, Any], output_format: str) -> str:
    if output_format == "json":
        return json.dumps(result, ensure_ascii=False, indent=2, sort_keys=True)
    if output_format == "paths":
        paths = {
            node["path"] for node in result["nodes"] if node.get("path")
        }
        paths.update(edge["evidence"]["path"] for edge in result["edges"])
        return "\n".join(sorted(paths))
    if output_format == "mermaid":
        return query_mermaid(result)
    if not result["nodes"]:
        return f'Aucun résultat pour « {result["query"]} ».\nNœuds: 0; relations: 0.'
    lines = [
        f'Requête: {result["query"]}',
        f'Nœuds: {len(result["nodes"])}; relations: {len(result["edges"])}',
    ]
    for node in result["nodes"]:
        location = f" — {node['path']}:{node.get('line', 1)}" if node.get("path") else ""
        lines.append(f"[{node['kind']}] {node['name']}{location}")
    for edge in result["edges"]:
        evidence = edge["evidence"]
        lines.append(
            f"{edge['source']} -{edge['kind']}-> {edge['target']} "
            f"({evidence['path']}:{evidence['line']})"
        )
    return "\n".join(lines)


def query_mermaid(result: dict[str, Any]) -> str:
    lines = ["flowchart LR"]
    if not result["nodes"]:
        lines.append('  empty["Aucun résultat"]')
        return "\n".join(lines)
    for node in result["nodes"]:
        label = escape_mermaid_label(f"{node['kind']}: {node['name']}")
        lines.append(f'  {mermaid_id(node["id"])}["{label}"]')
    for edge in result["edges"]:
        label = escape_mermaid_label(edge["kind"])
        lines.append(
            f'  {mermaid_id(edge["source"])} -->|"{label}"| '
            f'{mermaid_id(edge["target"])}'
        )
    return "\n".join(lines)


def positive_int(value: str) -> int:
    parsed = int(value)
    if parsed < 0:
        raise argparse.ArgumentTypeError("la valeur doit être positive")
    return parsed


def add_common_arguments(parser: argparse.ArgumentParser) -> None:
    parser.add_argument(
        "--root",
        type=Path,
        default=Path(__file__).resolve().parents[1],
        help="racine du dépôt (par défaut : racine déduite du script)",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=DEFAULT_GRAPH_PATH,
        help="chemin du graph.json, relatif à la racine par défaut",
    )


def create_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    for name in ("generate", "check", "stats"):
        command_parser = subparsers.add_parser(name)
        add_common_arguments(command_parser)
    query_parser = subparsers.add_parser("query")
    add_common_arguments(query_parser)
    query_parser.add_argument("query")
    query_parser.add_argument("--depth", type=positive_int, default=1)
    query_parser.add_argument("--max-nodes", type=positive_int, default=50)
    query_parser.add_argument(
        "--format",
        choices=("text", "paths", "json", "mermaid"),
        default="text",
    )
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    parser = create_parser()
    arguments = parser.parse_args(argv)
    root = arguments.root.resolve()
    if not root.is_dir():
        parser.error(f"racine introuvable : {root}")

    if arguments.command == "generate":
        graph = generate(root, arguments.output)
        graph_path, overview_path = resolve_output_paths(root, arguments.output)
        print(
            f"Graphe généré : {display_path(root, graph_path)} "
            f"({len(graph['nodes'])} nœuds, {len(graph['edges'])} relations)"
        )
        print(f"Vue générée : {display_path(root, overview_path)}")
        return 0

    if arguments.command == "check":
        fresh, errors = check(root, arguments.output)
        if fresh:
            print("Graphe d’architecture frais et déterministe.")
            return 0
        for error in errors:
            print(error, file=sys.stderr)
        return 1

    try:
        graph = load_graph(root, arguments.output)
    except (FileNotFoundError, json.JSONDecodeError, ValueError) as error:
        print(f"Impossible de lire le graphe : {error}", file=sys.stderr)
        print("Exécuter : python scripts/architecture_graph.py generate", file=sys.stderr)
        return 1

    if arguments.command == "stats":
        print(graph_stats(graph))
        return 0

    if arguments.max_nodes == 0:
        parser.error("--max-nodes doit être supérieur à zéro")
    result = select_subgraph(
        graph, arguments.query, arguments.depth, arguments.max_nodes
    )
    print(format_query(result, arguments.format))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
