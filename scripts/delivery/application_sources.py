"""Pinned, offline application dependency sources; never modify historical inputs.

Source archives and notices are read from one bounded, hash-checked buffer.
Archives are inspected in memory, never extracted or executed. Cache paths are
producer inputs only; recipients get the exact source archives inside the pack.
"""
import argparse
from dataclasses import dataclass
import gzip
import hashlib
import io
import json
import os
from pathlib import Path
import re
import tarfile
import tomllib

INVENTORY_PATH = Path(__file__).with_name("application_sources.json")
PREFIX = "licenses/native-media/"
MAX_ARCHIVE = 8 * 1024 * 1024
MAX_UNPACKED = 16 * 1024 * 1024
MAX_BUNDLE = 64 * 1024 * 1024
REGISTRY = "registry+https://github.com/rust-lang/crates.io-index"


@dataclass(frozen=True)
class PreparedSources:
    files: tuple[tuple[str, bytes], ...]
    receipt: dict


def sha(data):
    return hashlib.sha256(data).hexdigest()


def json_bytes(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode()


def keys(value, required, label):
    if not isinstance(value, dict) or set(value) != set(required):
        raise ValueError(f"Unknown or missing {label} fields")


def relative(value):
    if not isinstance(value, str) or not 1 <= len(value) <= 1024 or not value.isascii():
        raise ValueError("Invalid source member path")
    reserved = {"con", "prn", "aux", "nul", *(f"com{i}" for i in range(1, 10)),
                *(f"lpt{i}" for i in range(1, 10))}
    if any(not re.fullmatch(r"[A-Za-z0-9_.+-]+", part) or part in {".", ".."}
           or part.endswith(".") or part.split(".")[0].lower() in reserved
           for part in value.split("/")):
        raise ValueError(f"Unsafe source member path: {value!r}")
    return value


def ordinary(path):
    for item in (path, *path.parents):
        if item.exists() or item.is_symlink():
            stat = item.lstat()
            if item.is_symlink() or getattr(stat, "st_file_attributes", 0) & 0x400:
                raise ValueError(f"Link/reparse source path refused: {item}")


def read_regular(path, limit, label):
    path = Path(path)
    ordinary(path)
    try:
        if not path.is_file() or path.stat().st_size > limit:
            raise ValueError(f"Missing or oversized {label}: {path}")
        with path.open("rb") as stream:
            data = stream.read(limit + 1)
        if len(data) > limit:
            raise ValueError(f"Oversized {label}: {path}")
        return data
    except OSError as error:
        raise ValueError(f"Missing/unreadable {label}: {path}: {error}") from error


def pin(data):
    return {"bytes": len(data), "sha256": sha(data)}


def checked_pin(data, expected, label, member=False):
    expected_fields = {"member", "bytes", "sha256"} if member else {"bytes", "sha256"}
    keys(expected, expected_fields, f"{label} pin")
    if (type(expected["bytes"]) is not int or expected["bytes"] < 0
            or not isinstance(expected["sha256"], str)
            or not re.fullmatch(r"[0-9a-f]{64}", expected["sha256"])
            or pin(data) != {name: expected[name] for name in ("bytes", "sha256")}):
        raise ValueError(f"{label} SHA/size differs from inventory pin")


def archive_members(data, component, registry, cache_key):
    prefix = f"{component['name']}-{component['version']}"
    try:
        with gzip.GzipFile(fileobj=io.BytesIO(data)) as zipped:
            unpacked = zipped.read(MAX_UNPACKED + 1)
        if len(unpacked) > MAX_UNPACKED:
            raise ValueError("Source archive decompression budget exceeded")
        members, aliases = {}, set()
        with tarfile.open(fileobj=io.BytesIO(unpacked), mode="r:") as archive:
            for index, item in enumerate(archive):
                if index >= 4096:
                    raise ValueError("Source archive member count budget exceeded")
                name = relative(item.name.rstrip("/") if item.isdir() else item.name)
                if name != prefix and not name.startswith(prefix + "/"):
                    raise ValueError(f"Source member path outside crate: {name}")
                if name.lower() in aliases:
                    raise ValueError(f"Duplicate/aliased source member: {name}")
                aliases.add(name.lower())
                if item.isdir():
                    continue
                if not item.isfile() or not 0 <= item.size <= MAX_UNPACKED:
                    raise ValueError(f"Source member link/type/size refused: {name}")
                source = archive.extractfile(item)
                if source is None:
                    raise ValueError(f"Missing source member: {name}")
                content = source.read(MAX_UNPACKED + 1)
                if len(content) != item.size:
                    raise ValueError(f"Truncated source member: {name}")
                local = name[len(prefix) + 1:]
                members[local] = content
        if not members:
            raise ValueError("Empty source archive")
    except (OSError, EOFError, tarfile.TarError) as error:
        raise ValueError(f"Invalid source archive: {error}") from error
    cache_source = registry / "src" / cache_key / prefix
    ordinary(cache_source)
    for name, content in members.items():
        if read_regular(cache_source / name, MAX_UNPACKED, "cache source") != content:
            raise ValueError(f"Cache source differs from exact archive: {prefix}/{name}")
    present = set()
    for directory, directories, filenames in os.walk(cache_source, followlinks=False):
        for entry in directories + filenames:
            ordinary(Path(directory) / entry)
        present.update((Path(directory) / name).relative_to(cache_source).as_posix()
                       for name in filenames)
    if present - {".cargo-ok", ".cargo-checksum.json"} != set(members):
        raise ValueError(f"Cache source file inventory differs: {prefix}")
    return members


def component_files(component, data, registry, cache_key):
    members = archive_members(data, component, registry, cache_key)
    try:
        package = tomllib.loads(members["Cargo.toml"].decode())["package"]
    except (KeyError, UnicodeError, tomllib.TOMLDecodeError) as error:
        raise ValueError(f"Missing/invalid source Cargo.toml: {error}") from error
    for field in ("name", "version", "license", "repository", "authors"):
        if package.get(field) != component[field]:
            raise ValueError(f"Source Cargo.toml {field} differs from inventory")
    license_pin = component["license_file"]
    keys(license_pin, {"member", "bytes", "sha256"}, "license")
    license_data = members.get(relative(license_pin["member"]))
    if license_data is None:
        raise ValueError("Required LICENSE member is missing")
    checked_pin(license_data, license_pin, "LICENSE", member=True)
    prefix = f"{component['name']}-{component['version']}"
    files = {f"sources/{prefix}.crate": data, f"licenses/{prefix}.txt": license_data}
    if not isinstance(component["notices"], list) or not 1 <= len(component["notices"]) <= 32:
        raise ValueError("Required source notices are missing or oversized")
    for notice in component["notices"]:
        keys(notice, {"member", "first_line", "last_line", "bytes", "sha256", "output"}, "notice")
        content = members.get(relative(notice["member"]))
        first, last = notice["first_line"], notice["last_line"]
        if content is None or type(first) is not int or type(last) is not int or not 1 <= first <= last <= 100000:
            raise ValueError("Required notice member/line range is invalid")
        block = b"".join(content.splitlines(keepends=True)[first - 1:last])
        checked_pin(block, {name: notice[name] for name in ("bytes", "sha256")}, "notice")
        output = relative(notice["output"])
        if not output.startswith("notices/") or not output.endswith(".txt"):
            raise ValueError("Notice output path must be below notices/")
        if output in files and files[output] != block:
            raise ValueError("Conflicting source notice output")
        files[output] = block
    return files, {"files": len(members), "bytes": sum(map(len, members.values())),
                   "sha256": sha(json_bytes({name: pin(content) for name, content in members.items()}))}


def prepare_sources(repo, cargo_registry, inventory_path=INVENTORY_PATH):
    inventory_data = read_regular(inventory_path, 1024 * 1024, "source inventory")
    inventory = json.loads(inventory_data)
    keys(inventory, {"schema", "version", "name_prefix", "registry_cache_directory", "components"}, "source inventory")
    if (inventory["schema"] != "forge.delivery.application-source-inventory"
            or type(inventory["version"]) is not int or inventory["version"] != 1):
        raise ValueError("Unsupported source inventory version/schema")
    cache_key = relative(inventory["registry_cache_directory"])
    if "/" in cache_key or not re.fullmatch(r"[a-z0-9-]{1,64}", inventory["name_prefix"]):
        raise ValueError("Invalid source inventory namespace/cache directory")
    components = inventory["components"]
    if not isinstance(components, list) or not 1 <= len(components) <= 128:
        raise ValueError("Source component inventory count is invalid")
    lock_data = read_regular(Path(repo) / "Cargo.lock", 16 * 1024 * 1024, "Cargo.lock")
    locked = [item for item in tomllib.loads(lock_data.decode()).get("package", [])
              if item.get("name", "").startswith(inventory["name_prefix"])]
    identities = [(item.get("name"), item.get("version"), item.get("source"), item.get("checksum")) for item in locked]
    expected = []
    for component in components:
        keys(component, {"name", "version", "source", "sha256", "bytes", "license", "repository", "authors", "license_file", "notices"}, "component")
        if (not re.fullmatch(r"[a-z0-9-]{1,64}", component["name"])
                or not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", component["version"])
                or component["source"] != REGISTRY or component["license"] != "MPL-2.0"
                or not isinstance(component["authors"], list)
                or not all(isinstance(author, str) and len(author) <= 1024 for author in component["authors"])):
            raise ValueError("Invalid component identity/source/license")
        expected.append((component["name"], component["version"], component["source"], component["sha256"]))
    if len(set(expected)) != len(expected) or len(set(identities)) != len(identities) or set(expected) != set(identities):
        raise ValueError("Cargo.lock component set/pins differ from source inventory")
    files, aliases, summaries = {}, {}, []
    total = 0
    for component in components:
        filename = f"{component['name']}-{component['version']}.crate"
        archive = read_regular(Path(cargo_registry) / "cache" / cache_key / filename, MAX_ARCHIVE, "source archive")
        checked_pin(archive, {name: component[name] for name in ("bytes", "sha256")}, "source archive")
        payloads, source_summary = component_files(component, archive, Path(cargo_registry), cache_key)
        summaries.append({**component, "archive_path": PREFIX + "sources/" + filename, "source_files": source_summary})
        for name, content in payloads.items():
            name = PREFIX + relative(name)
            if name.lower() in aliases and aliases[name.lower()] != name:
                raise ValueError(f"Aliased source output path: {name}")
            aliases[name.lower()] = name
            if name in files and files[name] != content:
                raise ValueError(f"Conflicting source output pin: {name}")
            if name not in files:
                total += len(content)
                if total > MAX_BUNDLE:
                    raise ValueError("Application source bundle budget exceeded")
            files[name] = content
    notice = ["OpenRPG Forge — application dependency source notices", "",
              "These original dependency sources are distributed under MPL-2.0.",
              "Exact source archives are supplied locally, without a network service.",
              "The .crate files are gzip-compressed tar archives; use an archive reader to inspect them.",
              "Each source archive preserves its original license and source notices.",
              "The corresponding LICENSE text is also copied under licenses/.", "",
              "The original source notices below include third-party notices where present.",
              "This inventory is not a legal opinion or an audit of every application dependency.", ""]
    for component in summaries:
        notice.extend([f"{component['name']} {component['version']} — {component['license']}",
                       f"Authors: {', '.join(component['authors'])}", f"Repository: {component['repository']}",
                       f"Exact local source: sources/{component['name']}-{component['version']}.crate",
                       f"Source SHA-256: {component['sha256']}", ""])
    notice.extend(f"Original notice: {name.removeprefix(PREFIX)}" for name in sorted(files) if "/notices/" in name)
    files[PREFIX + "NOTICE.txt"] = ("\n".join(notice) + "\n").encode()
    manifest = {"schema": "forge.delivery.application-sources.v1", "inventory_sha256": sha(inventory_data),
                "cargo_lock_sha256": sha(lock_data), "components": summaries,
                "files": {name: pin(content) for name, content in sorted(files.items())}}
    files[PREFIX + "manifest.json"] = json_bytes(manifest)
    receipt = {"schema": "forge.delivery.application-sources-receipt.v1",
               "manifest_path": PREFIX + "manifest.json", "manifest_sha256": sha(files[PREFIX + "manifest.json"]),
               "inventory_sha256": sha(inventory_data), "cargo_lock_sha256": sha(lock_data),
               "source_archives": len(components), "source_archive_bytes": sum(c["bytes"] for c in components),
               "files": {name: pin(content) for name, content in sorted(files.items())}}
    return PreparedSources(tuple(sorted(files.items())), receipt)


def prepare_for_assembly(args, repo):
    if not args.shared_working_tree:
        return None
    cache = getattr(args, "cargo_cache", None)
    if cache is None:
        raise ValueError("Shared assembly requires an explicit --cargo-cache registry directory")
    return prepare_sources(repo, cache)


def stage_sources(prepared, applications_root):
    root = Path(applications_root)
    ordinary(root)
    if not root.is_dir():
        raise ValueError("Application staging destination must already exist")
    destination = root / PREFIX.rstrip("/")
    ordinary(destination)
    if destination.exists():
        raise ValueError("Application source destination already exists; never overwrite notices")
    destination.mkdir(parents=True, exist_ok=False)
    for name, content in prepared.files:
        relative(name)
        if not name.startswith(PREFIX) or pin(content) != prepared.receipt["files"].get(name):
            raise ValueError("Prepared application source output differs from receipt")
        path = root / name
        ordinary(path)
        path.parent.mkdir(parents=True, exist_ok=True)
        with path.open("xb") as stream:
            stream.write(content)
        if read_regular(path, MAX_BUNDLE, "staged source") != content:
            raise ValueError(f"Application source changed during staging: {name}")
    return json.loads(json_bytes(prepared.receipt))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--cargo-cache", type=Path, required=True, help="Explicit Cargo registry root containing cache/ and src/")
    action = parser.add_mutually_exclusive_group(required=True)
    action.add_argument("--verify-only", action="store_true")
    action.add_argument("--output", type=Path, help="Fresh application staging root, never an active installation")
    args = parser.parse_args()
    prepared = prepare_sources(args.repo, args.cargo_cache)
    if args.output:
        args.output.mkdir(parents=True, exist_ok=False)
        receipt = stage_sources(prepared, args.output)
    else:
        receipt = prepared.receipt
    print(json_bytes(receipt).decode(), end="")


if __name__ == "__main__":
    main()
