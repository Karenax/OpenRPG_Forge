"""Stage a pinned Rust notice candidate with the module-game application.

This preserves candidate notices, not a legal qualification or a linked SBOM.
All inputs are read and checked before the caller creates a delivery output.
"""
from __future__ import annotations

from dataclasses import dataclass
import hashlib
import importlib.util
import json
import ntpath
import os
from pathlib import Path, PurePosixPath
import re
import stat
import subprocess
import sys
import types
from urllib.parse import unquote, urlsplit

import application_sources
import linux_build_evidence
from application_sources import ordinary, read_regular
from complete_rebuild import shared_source_inputs, source_inputs_equal

PREFIX = "licenses/rust-runtime-candidate/"
INDEX_NAMES = ("INDEX.json", "SUPPLEMENT-NOTICES.json", "FORGE-ORIGINAL-NOTICES.json", "ASSET-NOTICES.json")
MAX_FILE = 16 * 1024 * 1024
MAX_BUNDLE = 64 * 1024 * 1024
MAX_FILES = 4096
WINDOWS_TARGET = "x86_64-pc-windows-msvc"
LINUX_TARGET = "x86_64-unknown-linux-gnu"
RUNTIME_TARGETS = frozenset((WINDOWS_TARGET, LINUX_TARGET))


@dataclass(frozen=True)
class PreparedNotices:
    files: tuple[tuple[str, bytes], ...]
    project_notice: bytes
    project_links: tuple[tuple[str, bytes], ...]
    receipt: dict


def sha(data):
    return hashlib.sha256(data).hexdigest()


def document(data, label):
    try:
        value = json.loads(data)
    except (UnicodeError, json.JSONDecodeError) as error:
        raise ValueError(f"Invalid {label}: {error}") from error
    if not isinstance(value, dict):
        raise ValueError(f"Invalid {label}")
    return value


def cargo_inputs(source_manifest):
    files = source_manifest.get("files")
    if not isinstance(files, dict):
        raise ValueError("Build source inventory is missing")
    roots = ("apps/", "crates/", "tools/", "third_party/")
    return {name: pin.get("sha256") for name, pin in files.items()
            if isinstance(name, str) and isinstance(pin, dict)
            and (name in {"Cargo.toml", "Cargo.lock"}
                 or (name.startswith(roots) and name.rsplit("/", 1)[-1] in {"Cargo.toml", "Cargo.lock"}))}


def text_reference(value, texts):
    if not isinstance(value, str) or not re.fullmatch(r"texts/[0-9a-f]{64}\.txt", value):
        raise ValueError("Invalid candidate notice text path")
    digest = value[6:-4]
    if value not in texts or sha(texts[value]) != digest:
        raise ValueError("Candidate notice text differs from its path")
    return digest


def prepare(bundle, candidate_receipt, graph_receipt, source_manifest, project_notice,
            target=WINDOWS_TARGET):
    if not isinstance(target, str) or target not in RUNTIME_TARGETS:
        raise ValueError("Unsupported module-game notice target")
    if not all((bundle, candidate_receipt, graph_receipt)):
        raise ValueError("Module-game requires an explicit Rust notice bundle, candidate receipt and graph receipt")
    bundle = Path(bundle)
    ordinary(bundle)
    if not bundle.is_dir():
        raise ValueError("Rust notice bundle must be a physical directory")
    candidate_data = read_regular(candidate_receipt, MAX_FILE, "candidate notice receipt")
    graph_data = read_regular(graph_receipt, MAX_FILE, "notice graph receipt")
    candidate = document(candidate_data, "candidate notice receipt")
    graph = document(graph_data, "notice graph receipt")
    if (candidate.get("schema") != "forge.gate7.runtime-notice-asset-candidate.v2"
            or candidate.get("passed") is not True
            or candidate.get("distribution_qualified") is not False
            or candidate.get("graph_evidence_sha256") != sha(graph_data)
            or graph.get("schema") != "forge.gate7.current-delivery-five-unresolved-graph.v1"
            or graph.get("failed") is not False or graph.get("paths_changed_during_run") != []):
        raise ValueError("Candidate notice and Cargo graph receipts do not match")
    graph_pins = graph.get("manifests_and_locks_sha256")
    build_pins = cargo_inputs(source_manifest)
    if (not isinstance(graph_pins, dict) or not graph_pins
            or candidate.get("manifest_snapshot_sha256") != sha(json.dumps(graph_pins, sort_keys=True).encode())
            or any(build_pins.get(name) != digest for name, digest in graph_pins.items())):
        raise ValueError("Notice candidate does not match the build Cargo manifests and locks")
    # The physical build inventory may also contain ignored locks for unrelated
    # tools. The current runtime closure is still compared in prepare_for_assembly.
    extra_build_pins = {name: pin for name, pin in build_pins.items() if name not in graph_pins}
    if any(not isinstance(pin, str) or not re.fullmatch(r"[0-9a-f]{64}", pin)
           for pin in extra_build_pins.values()):
        raise ValueError("Invalid additional Cargo manifest or lock pin")
    scopes = graph.get("scopes", [])
    if not any(scope.get("name") == "module-game"
               and scope.get("target") == target
               and scope.get("graphs", {}).get("all_nondev", {}).get("exit_code") == 0
               for scope in scopes if isinstance(scope, dict)):
        raise ValueError(f"Candidate graph does not cover the {target} module-game runtime")

    names = {entry.name for entry in bundle.iterdir()}
    if names != {*INDEX_NAMES, "texts"}:
        raise ValueError("Unexpected or missing candidate notice bundle entries")
    indices = {name: read_regular(bundle / name, MAX_FILE, "candidate notice index") for name in INDEX_NAMES}
    base, supplements, forge, assets = (document(indices[name], name) for name in INDEX_NAMES)
    if (sha(indices["INDEX.json"]) != candidate.get("base_index_sha256")
            or sha(indices["SUPPLEMENT-NOTICES.json"]) != candidate.get("supplement_index_sha256")
            or sha(indices["FORGE-ORIGINAL-NOTICES.json"]) != candidate.get("forge_index_sha256")
            or sha(indices["ASSET-NOTICES.json"]) != candidate.get("asset_index_sha256")
            or supplements.get("base_index_sha256") != sha(indices["INDEX.json"])
            or forge.get("base_index_sha256") != sha(indices["INDEX.json"])
            or forge.get("supplement_index_sha256") != sha(indices["SUPPLEMENT-NOTICES.json"])
            or assets.get("schema") != "forge.asset-notice-supplements.v1"
            or assets.get("base_index_sha256") != sha(indices["INDEX.json"])
            or assets.get("release_qualified") is not False):
        raise ValueError("Candidate notice index chain differs from its receipt")
    requests = base.get("requests", [])
    if not any(request.get("package") == "forge_runtime"
               and request.get("target") == target
               and request.get("no_default_features") is True
               and "forge_runtime/module-game" in request.get("features", [])
               for request in requests if isinstance(request, dict)):
        raise ValueError("Candidate notice inventory does not cover the module-game runtime")

    text_dir = bundle / "texts"
    ordinary(text_dir)
    if not text_dir.is_dir():
        raise ValueError("Candidate notice text directory is missing")
    texts = {}
    for path in sorted(text_dir.iterdir()):
        if len(texts) >= MAX_FILES or not re.fullmatch(r"[0-9a-f]{64}\.txt", path.name):
            raise ValueError("Invalid or oversized candidate notice text inventory")
        data = read_regular(path, MAX_FILE, "candidate notice text")
        if sha(data) != path.stem:
            raise ValueError("Candidate notice text SHA-256 differs from its filename")
        texts[f"texts/{path.name}"] = data
    for item in base.get("packages", []):
        for value in item.get("notices", []):
            text_reference(value, texts)
    for index in (supplements, forge):
        for item in index.get("packages", []):
            for notice in item.get("notices", []):
                if text_reference(notice.get("path"), texts) != notice.get("sha256"):
                    raise ValueError("Candidate notice reference SHA-256 differs")
    asset_entries = assets.get("assets")
    if not isinstance(asset_entries, list) or not asset_entries:
        raise ValueError("Candidate asset notice inventory is empty")
    package_ids = {item.get("package_id") for item in base.get("packages", [])}
    for item in asset_entries:
        if item.get("package_id") not in package_ids or not item.get("notices"):
            raise ValueError("Candidate asset notice is not tied to an indexed package")
        for notice in item["notices"]:
            path = notice.get("path")
            if text_reference(path, texts) != notice.get("sha256") or len(texts[path]) != notice.get("bytes"):
                raise ValueError("Candidate asset notice text differs")
    files = {**indices, **texts}
    total = sum(map(len, files.values()))
    if (len(files) != candidate.get("bundle_files") or total != candidate.get("bundle_bytes")
            or len(files) > MAX_FILES or total > MAX_BUNDLE):
        raise ValueError("Candidate notice bundle size or file count differs from its receipt")
    project_data = read_regular(project_notice, MAX_FILE, "project third-party notices")
    try:
        link_names = sorted(set(re.findall(r"\]\((third_party/[^)]+)\)", project_data.decode("utf-8"))))
    except UnicodeError as error:
        raise ValueError("Project notice is not UTF-8") from error
    if len(link_names) > 256:
        raise ValueError("Too many project notice references")
    links = []
    linked_bytes = 0
    for name in link_names:
        if (not re.fullmatch(r"third_party/(?:[A-Za-z0-9_.-]+/)*[A-Za-z0-9_.-]+", name)
                or any(part in {".", ".."} for part in name.split("/"))):
            raise ValueError("Invalid project notice reference")
        data = read_regular(Path(project_notice).parent / name, MAX_FILE, "project notice reference")
        if source_manifest.get("files", {}).get(name, {}).get("sha256") != sha(data):
            raise ValueError(f"Project notice reference differs from build inputs: {name}")
        linked_bytes += len(data)
        if total + len(project_data) + linked_bytes > MAX_BUNDLE:
            raise ValueError("Runtime notice candidate exceeds the staging budget")
        links.append((name, data))
    receipt = {"schema": "forge.delivery.runtime-notice-candidate.v1",
               "candidate_receipt_sha256": sha(candidate_data), "graph_receipt_sha256": sha(graph_data),
               "base_index_sha256": sha(indices["INDEX.json"]),
               "supplement_index_sha256": sha(indices["SUPPLEMENT-NOTICES.json"]),
               "forge_index_sha256": sha(indices["FORGE-ORIGINAL-NOTICES.json"]),
               "asset_index_sha256": sha(indices["ASSET-NOTICES.json"]),
               "additional_build_cargo_inputs": extra_build_pins,
               "project_notice_sha256": sha(project_data), "candidate_files": len(files),
               "candidate_bytes": total, "project_reference_bytes": linked_bytes,
               "project_reference_sha256": {name:sha(data) for name, data in links},
               "actual_linked_contents_qualified": False,
               "asset_rights_qualified": False, "distribution_qualified": False}
    if target != WINDOWS_TARGET:
        receipt["runtime_target"] = target
    return PreparedNotices(tuple((PREFIX + name, data) for name, data in sorted(files.items())),
                           project_data, tuple(links), receipt)


def prepare_for_assembly(args, repo, source_manifest):
    repo = Path(repo).resolve(strict=True)
    if (source_manifest.get("schema") != "forge.delivery.shared-source-inputs.v1"
            or Path(source_manifest.get("source_root", "")).resolve() != repo
            or not source_inputs_equal(shared_source_inputs(repo), source_manifest)):
        raise ValueError("Complete current delivery build source inventory is required")
    target = getattr(args, "runtime_notice_target", WINDOWS_TARGET)
    prepared = prepare(getattr(args, "runtime_notice_bundle", None),
                       getattr(args, "runtime_notice_receipt", None),
                       getattr(args, "runtime_notice_graph", None),
                       source_manifest, repo / "THIRD_PARTY_NOTICES.md", target)
    # Cargo configuration can affect resolution without changing Cargo.toml. Recheck
    # the current offline closure rather than treating an old graph as a build SBOM.
    cache = getattr(args, "cargo_cache", None)
    if cache is None:
        raise ValueError("Cargo registry is required to check the runtime notice scope")
    registry = Path(cache).resolve()
    if registry.name.casefold() != "registry":
        raise ValueError("Cargo cache must be the registry directory inside CARGO_HOME")
    env = dict(os.environ, CARGO_HOME=str(registry.parent), CARGO_NET_OFFLINE="true")
    command = ["cargo", "metadata", "--manifest-path", str(repo / "Cargo.toml"),
               "--format-version", "1", "--locked", "--offline", "--filter-platform",
               target, "--features", "forge_runtime/module-game", "--no-default-features"]
    result = subprocess.run(command, cwd=repo, env=env, capture_output=True, check=False)
    if result.returncode:
        raise ValueError("Cannot verify current module-game Cargo closure offline")
    collector_path = repo / "scripts/tidewater/collect_rust_notices.py"
    spec = importlib.util.spec_from_file_location("forge_delivery_notice_collector", collector_path)
    if spec is None or spec.loader is None:
        raise ValueError("Runtime notice collector is unavailable")
    collector = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(collector)
    current = set(collector.dependencies(document(result.stdout, "Cargo metadata"), "forge_runtime"))
    index = document(dict(prepared.files)[PREFIX + "INDEX.json"], "candidate notice index")
    request = next(request for request in index["requests"]
                   if request.get("package") == "forge_runtime"
                   and request.get("target") == target
                   and request.get("no_default_features") is True
                   and "forge_runtime/module-game" in request.get("features", []))
    if current != set(request.get("package_ids", [])):
        raise ValueError("Current runtime Cargo closure differs from the notice candidate")
    receipt = {**prepared.receipt, "current_runtime_package_count": len(current),
               "current_runtime_package_ids_sha256": sha(json.dumps(sorted(current)).encode())}
    return PreparedNotices(prepared.files, prepared.project_notice, prepared.project_links, receipt)


def _snapshot_file(root, manifest, name, *, label="Linux snapshot"):
    pin = manifest.get("files", {}).get(name)
    path = root / name
    if (not isinstance(pin, dict) or type(pin.get("bytes")) is not int or
            not linux_build_evidence.physical(path)):
        raise ValueError(f"{label} input is missing or unsafe: {name}")
    data = read_regular(path, MAX_FILE, f"{label} input")
    if len(data) != pin["bytes"] or sha(data) != pin.get("sha256"):
        raise ValueError(f"{label} input differs: {name}")
    return data


def _snapshot_code(root, manifest):
    """Authenticate the running reader and import only pinned snapshot helpers."""
    running = {"scripts/delivery/runtime_notices.py": Path(__file__),
               "scripts/delivery/application_sources.py": Path(application_sources.__file__),
               "scripts/delivery/linux_build_evidence.py": Path(linux_build_evidence.__file__)}
    for name, loaded in running.items():
        snapshot_data = _snapshot_file(root, manifest, name)
        if not linux_build_evidence.physical(loaded) or read_regular(loaded, MAX_FILE, "running reader") != snapshot_data:
            raise ValueError(f"Running Linux snapshot reader differs: {name}")
    for name in ("scripts/gate6/capture_inputs.py", "scripts/tidewater/collect_rust_notices.py"):
        _snapshot_file(root, manifest, name)


def _snapshot_module(root, manifest, name, label, *, input_label="Linux snapshot"):
    path = root / name
    data = _snapshot_file(root, manifest, name, label=input_label)
    module = types.ModuleType(label)
    module.__file__ = str(path)
    exec(compile(data, str(path), "exec"), module.__dict__)
    return module


def windows_delivery_cli_scope(repo, source_manifest, registry, index_path):
    """Recompute the Windows delivery CLI closure against the staged notice index."""
    repo = Path(repo)
    registry = Path(registry)
    index_path = Path(index_path)
    ordinary(repo)
    ordinary(registry)
    if (not repo.is_dir() or not registry.is_dir() or registry.name.casefold() != "registry"
            or source_manifest.get("schema") != "forge.delivery.shared-source-inputs.v1"
            or Path(source_manifest.get("source_root", "")).resolve() != repo.resolve()):
        raise ValueError("Windows delivery notices require the pinned checkout and Cargo registry")
    repo = repo.resolve(strict=True)
    registry = registry.resolve(strict=True)
    names = (".cargo/config.toml", "Cargo.toml", "Cargo.lock", "tools/forge_delivery/Cargo.toml",
             "tools/forge_delivery/Cargo.lock", "scripts/tidewater/collect_rust_notices.py")
    before = {name: _snapshot_file(repo, source_manifest, name, label="Windows delivery") for name in names}
    index_data = read_regular(index_path, MAX_FILE, "staged Windows notice index")
    index = document(index_data, "staged Windows notice index")
    packages = index.get("packages")
    if index.get("schema") != "forge.rust-dependency-notices.v2" or not isinstance(packages, list):
        raise ValueError("Windows delivery notice package inventory is invalid")
    indexed = [item.get("package_id") for item in packages if isinstance(item, dict)]
    if (len(indexed) != len(packages) or any(not isinstance(value, str) or not value for value in indexed)
            or len(indexed) != len(set(indexed))):
        raise ValueError("Windows delivery notice package identities are invalid")
    collector = _snapshot_module(repo, source_manifest, names[-1], "forge_windows_delivery_notice_collector",
                                 input_label="Windows delivery")
    command = ["cargo", "metadata", "--manifest-path", str(repo / "tools/forge_delivery/Cargo.toml"),
               "--format-version", "1", "--locked", "--offline", "--filter-platform", WINDOWS_TARGET]
    env = dict(os.environ, CARGO_HOME=str(registry.parent), CARGO_NET_OFFLINE="true")
    for key in ("CARGO_ENCODED_RUSTFLAGS", "RUSTUP_TOOLCHAIN"):
        env.pop(key, None)
    result = subprocess.run(command, cwd=repo, env=env, capture_output=True, check=False)
    if result.returncode:
        raise ValueError("Cannot verify the Windows delivery CLI Cargo closure offline")
    current = collector.dependencies(document(result.stdout, "Windows delivery Cargo metadata"), "forge_delivery")
    if not current or set(current) - set(indexed):
        raise ValueError("Windows delivery CLI Cargo closure is not covered by the notice index")
    package_map = {item["package_id"]: item for item in packages}
    windows_origin = bool(ntpath.splitdrive(str(repo))[0])
    recorded = _canonical_packages({identity:package_map[identity] for identity in current}, str(repo),
                                   source_manifest, windows_origin=windows_origin)
    resolved = _canonical_packages(current, str(repo), source_manifest,
                                   windows_origin=windows_origin, current=True)
    if set(resolved) != set(recorded):
        raise ValueError("Windows delivery CLI Cargo metadata differs from the notice index")
    if ({name: _snapshot_file(repo, source_manifest, name, label="Windows delivery") for name in names} != before
            or read_regular(index_path, MAX_FILE, "staged Windows notice index") != index_data):
        raise ValueError("Windows delivery notice inputs changed during Cargo metadata")
    identities = [list(item) for item in sorted(resolved)]
    return {"target": WINDOWS_TARGET, "package": "forge_delivery", "metadata_command": command,
            "cargo_home": str(registry.parent), "index_sha256": sha(index_data),
            "package_count": len(current), "package_ids_sha256": sha(json.dumps(sorted(current)).encode()),
            "normalized_package_ids_sha256": sha(json.dumps(identities, ensure_ascii=False,
                                                 separators=(",", ":")).encode())}


def _verify_extracted_snapshot(root, manifest, reader):
    """Reuse the source capture's physical walk, including its explicit exclusions."""
    roots = tuple(manifest.get("roots", ()))
    files = manifest.get("files", {})
    if roots != linux_build_evidence.SOURCE_ROOTS or not isinstance(files, dict):
        raise ValueError("Linux snapshot roots or manifest differ")
    expected_top_files = {name for name in files if "/" not in name}
    actual_top_files = set()
    actual_roots = set()
    with os.scandir(root) as entries:
        for entry in entries:
            info = entry.stat(follow_symlinks=False)
            if stat.S_ISLNK(info.st_mode) or getattr(info, "st_file_attributes", 0) & 0x400:
                raise ValueError("Linux snapshot contains a root link or junction")
            if stat.S_ISDIR(info.st_mode):
                actual_roots.add(entry.name)
            elif stat.S_ISREG(info.st_mode):
                actual_top_files.add(entry.name)
            else:
                raise ValueError("Linux snapshot contains a non-file root entry")
    if actual_roots != set(roots) or actual_top_files != expected_top_files:
        raise ValueError("Linux snapshot root inventory differs")
    reader.ROOTS = roots
    reader.EXPLICIT = tuple(sorted(expected_top_files))
    if reader.capture(root) != files:
        raise ValueError("Linux snapshot files changed or differ from the verified ZIP")


def _local_cargo_id(package_id, origin, files, *, windows_origin):
    """Rebase only a file URL below its verified source root; keep its fragment."""
    if not isinstance(package_id, str) or not package_id.startswith("path+file:"):
        raise ValueError("Local Cargo package identity is invalid")
    value = urlsplit(package_id[len("path+"):])
    if value.scheme != "file" or value.netloc or value.query or not value.fragment or not value.path.startswith("/"):
        raise ValueError("Local Cargo package file URL is invalid")
    try:
        path = unquote(value.path, encoding="utf-8", errors="strict")
    except UnicodeError as error:
        raise ValueError("Local Cargo package file URL is invalid") from error
    if ("\\" in path or "\0" in path or re.search(r"%(?:2f|5c|00)", value.path, re.I) or
            any(part in {"", ".", ".."} for part in path.split("/")[1:])):
        raise ValueError("Local Cargo package path is unsafe")
    if windows_origin:
        if not re.match(r"^/[A-Za-z]:/", path):
            raise ValueError("Local Cargo package is outside the captured source root")
        try:
            relative = ntpath.relpath(path[1:], origin).replace("\\", "/")
        except ValueError as error:
            raise ValueError("Local Cargo package is outside the captured source root") from error
    else:
        try:
            relative = PurePosixPath(path).relative_to(PurePosixPath(origin)).as_posix()
        except ValueError as error:
            raise ValueError("Local Cargo package is outside the Linux snapshot") from error
    if relative == ".":
        relative = ""
    if relative.startswith("../") or relative == ".." or relative.startswith("/"):
        raise ValueError("Local Cargo package is outside the verified source root")
    manifest_name = (relative + "/" if relative else "") + "Cargo.toml"
    matches = [name for name in files if name.casefold() == manifest_name.casefold()] if windows_origin else [
        name for name in files if name == manifest_name]
    if len(matches) != 1:
        raise ValueError("Local Cargo package manifest is absent or ambiguous in the snapshot")
    canonical_manifest = matches[0]
    canonical_directory = canonical_manifest.removesuffix("Cargo.toml").rstrip("/")
    return canonical_directory, value.fragment, canonical_manifest


def _canonical_packages(packages, source_root, manifest, *, windows_origin, current=False):
    if not isinstance(packages, dict) or not packages:
        raise ValueError("Cargo dependency closure is empty")
    canonical = {}
    local_manifests = set()
    root = Path(source_root) if current else None
    for package_id, item in packages.items():
        if (not isinstance(package_id, str) or not isinstance(item, dict) or
                not isinstance(item.get("name"), str) or not item["name"] or
                not isinstance(item.get("version"), str) or not item["version"] or
                any(key not in item for key in ("license", "repository", "authors")) or
                any(item[key] is not None and not isinstance(item[key], str) for key in ("license", "repository")) or
                not isinstance(item["authors"], list) or not all(isinstance(value, str) for value in item["authors"])):
            raise ValueError("Cargo dependency identity is incomplete")
        details = (item["license"], item["repository"], tuple(item["authors"]))
        source = item.get("source")
        if package_id.startswith("path+"):
            if source is not None:
                raise ValueError("Local Cargo package source differs")
            relative, fragment, manifest_name = _local_cargo_id(package_id, source_root, manifest["files"],
                                                                  windows_origin=windows_origin)
            if manifest_name.casefold() in local_manifests:
                raise ValueError("Local Cargo package identities collide after source-root normalization")
            local_manifests.add(manifest_name.casefold())
            if current:
                actual = item.get("manifest_path")
                expected = root / manifest_name
                if (item.get("id") != package_id or not isinstance(actual, str) or not actual or
                        not Path(actual).is_absolute() or Path(actual) != expected or
                        not linux_build_evidence.physical(expected)):
                    raise ValueError("Local Cargo package manifest is outside or unsafe")
            identity = ("local", relative, fragment, item["name"], item["version"], details)
        else:
            if not isinstance(source, str) or not source:
                raise ValueError("External Cargo package source is missing")
            identity = ("external", package_id, source, item["name"], item["version"], details)
        if identity in canonical:
            raise ValueError("Cargo package identities collide after source-root normalization")
        canonical[identity] = package_id
    return canonical


def _linux_notice_scope(root, manifest, graph, index, original, windows_origin, env, *,
                        name, package, cargo_manifest, features, no_default):
    scopes = [scope for scope in graph.get("scopes", []) if isinstance(scope, dict) and
              scope.get("name") == name and scope.get("target") == LINUX_TARGET]
    if (len(scopes) != 1 or scopes[0].get("package") != package or scopes[0].get("features") != features or
            scopes[0].get("no_default_features") is not no_default or scopes[0].get("include_root_dev") is not False):
        raise ValueError(f"Linux {name} notice graph scope differs or is ambiguous")
    requests = [request for request in index.get("requests", []) if isinstance(request, dict) and
                request.get("package") == package and request.get("target") == LINUX_TARGET and
                request.get("features") == features and request.get("no_default_features") is no_default and
                request.get("include_root_dev") is False]
    if len(requests) != 1:
        raise ValueError(f"Linux {name} notice request differs or is ambiguous")
    request = requests[0]
    expected_manifest = (ntpath.join(original, *cargo_manifest.split("/")) if windows_origin else
                         str(PurePosixPath(original) / cargo_manifest))
    request_manifest = request.get("manifest")
    same_manifest = (isinstance(request_manifest, str) and
        (ntpath.normcase(ntpath.normpath(request_manifest)) == ntpath.normcase(ntpath.normpath(expected_manifest))
         if windows_origin else request_manifest == expected_manifest))
    ids = request.get("package_ids")
    if (not same_manifest or not isinstance(ids, list) or not ids or
            not all(isinstance(value, str) for value in ids) or len(ids) != len(set(ids))):
        raise ValueError("Linux notice request source or package identities differ")
    graph_scope = scopes[0].get("graphs", {}).get("all_nondev", {})
    if (graph_scope.get("exit_code") != 0 or graph_scope.get("package_count") != len(ids) or
            graph_scope.get("package_ids_sha256") != sha(json.dumps(sorted(ids)).encode())):
        raise ValueError("Linux notice graph does not bind the indexed dependency identities")
    indexed = index.get("packages", [])
    if (not isinstance(indexed, list) or any(not isinstance(item, dict) or
            not isinstance(item.get("package_id"), str) for item in indexed)):
        raise ValueError("Linux notice package inventory is invalid")
    package_map = {item["package_id"]:item for item in indexed}
    if len(package_map) != len(indexed) or any(value not in package_map for value in ids):
        raise ValueError("Linux notice package inventory is missing or duplicated")
    recorded = _canonical_packages({value:package_map[value] for value in ids}, original, manifest,
                                   windows_origin=windows_origin)
    local_manifests = {(identity[1] + "/" if identity[1] else "") + "Cargo.toml"
                       for identity in recorded if identity[0] == "local"}
    graph_pins = graph.get("manifests_and_locks_sha256", {})
    scope_lock = cargo_manifest.removesuffix("Cargo.toml") + "Cargo.lock"
    if not {"Cargo.toml", "Cargo.lock", cargo_manifest, scope_lock, *local_manifests}.issubset(graph_pins):
        raise ValueError("Linux local Cargo manifests are absent from the notice graph")
    command = ["cargo", "metadata", "--manifest-path", str(root / cargo_manifest), "--format-version", "1",
               "--locked", "--offline", "--filter-platform", LINUX_TARGET]
    if features:
        command.extend(("--features", ",".join(features)))
    if no_default:
        command.append("--no-default-features")
    completed = subprocess.run(command, cwd=root, env=env, capture_output=True, check=False, timeout=120)
    if completed.returncode or not isinstance(completed.stdout, bytes) or len(completed.stdout) > 128*1024*1024:
        raise ValueError(f"Cannot verify Linux {name} Cargo closure offline")
    collector = _snapshot_module(root, manifest, "scripts/tidewater/collect_rust_notices.py", "forge_linux_snapshot_notices")
    current_raw = collector.dependencies(document(completed.stdout, "Linux Cargo metadata"), package)
    current = _canonical_packages(current_raw, str(root), manifest,
                                  windows_origin=bool(ntpath.splitdrive(str(root))[0]), current=True)
    if set(current) != set(recorded):
        raise ValueError(f"Linux {name} Cargo closure differs from its notice candidate")
    identities = [list(item) for item in sorted(current)]
    return {"command":command, "metadata_sha256":sha(completed.stdout), "package_count":len(current),
            "current_package_ids_sha256":sha(json.dumps(sorted(current_raw)).encode()),
            "captured_package_ids_sha256":graph_scope["package_ids_sha256"],
            "normalized_package_ids_sha256":sha(json.dumps(identities, ensure_ascii=False,
                                                separators=(",", ":")).encode())}


def prepare_for_linux_snapshot(args, snapshot_root, source_zip, source_manifest_path,
                               source_sha256, source_manifest_sha256, capture_receipt_path, *,
                               include_launcher=False):
    """Check runtime notices against the actual immutable Linux source transport.

    This compares the offline Cargo closure, not installed bytes or a linked SBOM.
    It requires no Git checkout and never creates a kit or installation.
    """
    if sys.platform != "linux":
        raise ValueError("Linux snapshot notice verification requires a Linux host")
    if type(include_launcher) is not bool:
        raise ValueError("Linux launcher scope selection must be explicit")
    root = Path(snapshot_root)
    if not linux_build_evidence.physical(root) or not root.is_dir():
        raise ValueError("Linux snapshot root must be a physical directory")
    root = root.resolve(strict=True)
    receipt_path = Path(capture_receipt_path)
    if not linux_build_evidence.physical(receipt_path):
        raise ValueError("Linux capture receipt is unsafe")
    capture_data = read_regular(receipt_path, MAX_FILE, "Linux capture receipt")
    capture = document(capture_data, "Linux capture receipt")
    manifest, _ = linux_build_evidence.verify_snapshot(source_zip, source_sha256, source_manifest_sha256,
                                                       source_manifest_path, capture)
    original = manifest.get("repository")
    if (not isinstance(original, str) or not original or "\0" in original or
            any(part in {".", ".."} for part in original.replace("\\", "/").split("/"))):
        raise ValueError("Linux snapshot original source root is missing")
    windows_origin = bool(ntpath.splitdrive(original)[0])
    if (windows_origin and not ntpath.isabs(original)) or (not windows_origin and not PurePosixPath(original).is_absolute()):
        raise ValueError("Linux snapshot original source root is not absolute")
    _snapshot_code(root, manifest)
    reader = _snapshot_module(root, manifest, "scripts/gate6/capture_inputs.py", "forge_linux_snapshot_capture")
    _verify_extracted_snapshot(root, manifest, reader)
    for name in ("runtime_notice_bundle", "runtime_notice_receipt", "runtime_notice_graph"):
        path = getattr(args, name, None)
        if path is None or not linux_build_evidence.physical(Path(path)):
            raise ValueError(f"Linux notice input is missing or unsafe: {name}")
    project = root / "THIRD_PARTY_NOTICES.md"
    _snapshot_file(root, manifest, "THIRD_PARTY_NOTICES.md")
    prepared = prepare(getattr(args, "runtime_notice_bundle", None),
                       getattr(args, "runtime_notice_receipt", None),
                       getattr(args, "runtime_notice_graph", None), manifest, project, LINUX_TARGET)
    graph_data = read_regular(args.runtime_notice_graph, MAX_FILE, "Linux notice graph")
    graph = document(graph_data, "Linux notice graph")
    index = document(dict(prepared.files)[PREFIX + "INDEX.json"], "Linux candidate notice index")
    registry = getattr(args, "cargo_cache", None)
    target = getattr(args, "cargo_target", None) or os.environ.get("CARGO_TARGET_DIR")
    if registry is None or target is None:
        raise ValueError("Existing Cargo registry and target are required for Linux metadata")
    registry, target = Path(registry), Path(target)
    if (not registry.is_absolute() or not target.is_absolute() or not linux_build_evidence.physical(registry) or
            not registry.is_dir() or registry.name.casefold() != "registry" or
            not linux_build_evidence.physical(target) or not target.is_dir()):
        raise ValueError("Linux Cargo registry or target is not a physical directory")
    registry, target = registry.resolve(strict=True), target.resolve(strict=True)
    if (any(path.is_relative_to(root) or root.is_relative_to(path) for path in (registry, target)) or
            registry.is_relative_to(target) or target.is_relative_to(registry)):
        raise ValueError("Linux Cargo registry and target must be outside the snapshot")
    env = {key:value for key,value in os.environ.items() if not key.upper().startswith(
        ("CARGO_", "RUSTC", "RUSTDOC", "RUSTFLAGS", "RUSTUP_TOOLCHAIN"))}
    env.update(CARGO_HOME=str(registry.parent), CARGO_TARGET_DIR=str(target), CARGO_NET_OFFLINE="true")
    module_scope = _linux_notice_scope(root, manifest, graph, index, original, windows_origin, env,
        name="module-game", package="forge_runtime", cargo_manifest="Cargo.toml",
        features=["forge_runtime/module-game"], no_default=True)
    launcher_scope = (_linux_notice_scope(root, manifest, graph, index, original, windows_origin, env,
        name="launcher-standalone", package="forge_launcher_prototype",
        cargo_manifest="apps/forge_launcher/Cargo.toml", features=["standalone"], no_default=False)
        if include_launcher else None)
    _verify_extracted_snapshot(root, manifest, reader)
    _snapshot_code(root, manifest)
    if (linux_build_evidence.sha(source_zip) != source_sha256 or
            linux_build_evidence.sha(source_manifest_path) != source_manifest_sha256 or
            read_regular(receipt_path, MAX_FILE, "Linux capture receipt") != capture_data or
            read_regular(args.runtime_notice_graph, MAX_FILE, "Linux notice graph") != graph_data or
            prepare(args.runtime_notice_bundle, args.runtime_notice_receipt, args.runtime_notice_graph,
                    manifest, project, LINUX_TARGET) != prepared):
        raise ValueError("Linux snapshot or notice candidate changed during verification")
    receipt = {**prepared.receipt, "linux_source_zip_sha256":source_sha256,
               "linux_source_manifest_sha256":source_manifest_sha256,
               "linux_capture_receipt_sha256":sha(capture_data),
               "linux_snapshot_reverified_before_after":True, "linux_metadata_offline":True,
               "linux_metadata_command":module_scope["command"], "linux_metadata_sha256":module_scope["metadata_sha256"],
               "linux_cargo_home":str(registry.parent), "linux_cargo_target":str(target),
               "current_runtime_package_count":module_scope["package_count"],
               "current_runtime_package_ids_sha256":module_scope["current_package_ids_sha256"],
               "captured_runtime_package_ids_sha256":module_scope["captured_package_ids_sha256"],
               "normalized_runtime_package_ids_sha256":module_scope["normalized_package_ids_sha256"],
               "path_package_root_normalized":True, "distribution_qualified":False}
    if launcher_scope is not None:
        receipt["launcher_standalone_scope"] = launcher_scope
    return PreparedNotices(prepared.files, prepared.project_notice, prepared.project_links, receipt)


def stage(prepared, core):
    core = Path(core)
    notice = core / "licenses/THIRD_PARTY_NOTICES.md"
    conflict = notice.exists() and read_regular(notice, MAX_FILE, "existing playtest notice") != prepared.project_notice
    for relative, data in prepared.project_links:
        path = notice.parent / relative
        if path.exists() and read_regular(path, MAX_FILE, "existing project reference") != data:
            conflict = True
    if conflict:
        # Preserve a distinct historical playtest notice alongside the current one.
        notice = core / "licenses/runtime-project/THIRD_PARTY_NOTICES.md"
    if not notice.exists():
        notice.parent.mkdir(parents=True, exist_ok=True)
        with notice.open("xb") as output:
            output.write(prepared.project_notice)
    if read_regular(notice, MAX_FILE, "staged project notice") != prepared.project_notice:
        raise ValueError("Staged project notice differs")
    for relative, data in prepared.project_links:
        destination = notice.parent / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        if not destination.exists():
            with destination.open("xb") as output:
                output.write(data)
        if read_regular(destination, MAX_FILE, "staged project reference") != data:
            raise ValueError(f"Staged project notice reference changed: {relative}")
    for relative, data in prepared.files:
        destination = core / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        with destination.open("xb") as output:
            output.write(data)
        if read_regular(destination, MAX_FILE, "staged candidate notice") != data:
            raise ValueError(f"Staged candidate notice changed: {relative}")
    return {**prepared.receipt, "project_notice_path": notice.relative_to(core).as_posix(),
            "candidate_index_path": PREFIX + "INDEX.json", "candidate_asset_index_path": PREFIX + "ASSET-NOTICES.json"}
