"""Collect the pinned native launcher/game dependency notices for a Windows playtest."""

from __future__ import annotations

import argparse
import json
import re
import shutil
import subprocess
from pathlib import Path


NOTICE_FILE_RE = re.compile(r"^(license|licence|copying|notice)(?:[._-].*)?$", re.I)


def metadata(manifest: Path) -> dict:
    completed = subprocess.run(
        [
            "cargo",
            "metadata",
            "--manifest-path",
            str(manifest),
            "--format-version",
            "1",
            "--locked",
            "--offline",
            "--filter-platform",
            "x86_64-pc-windows-msvc",
        ],
        check=True,
        capture_output=True,
    )
    return json.loads(completed.stdout)


def dependencies(document: dict, root_name: str) -> dict[str, dict]:
    packages = {package["id"]: package for package in document["packages"]}
    nodes = {node["id"]: node for node in document["resolve"]["nodes"]}
    roots = [package["id"] for package in packages.values() if package["name"] == root_name]
    if len(roots) != 1:
        raise ValueError(f"Expected one Cargo package named {root_name}, found {len(roots)}")
    pending = roots[:]
    seen: set[str] = set()
    while pending:
        package_id = pending.pop()
        if package_id in seen:
            continue
        seen.add(package_id)
        for dependency in nodes[package_id]["deps"]:
            if any(kind.get("kind") != "dev" for kind in dependency["dep_kinds"]):
                pending.append(dependency["pkg"])
    return {package_id: packages[package_id] for package_id in seen}


def notice_relative_path(root: Path, source: Path) -> Path:
    try:
        return source.relative_to(root)
    except ValueError:
        # Keep the historical basename behavior for an absolute license_file.
        return Path(source.name)


def notice_files(package: dict) -> list[Path]:
    root = Path(package["manifest_path"]).parent
    # Registry/git archives can carry component notices below their package root;
    # path packages may contain unrelated frontend/assets trees.
    candidates = root.rglob("*") if package.get("source") else root.iterdir()
    names = {
        path
        for path in candidates
        if path.is_file() and NOTICE_FILE_RE.match(path.name)
    }
    if package.get("license_file"):
        path = root / package["license_file"]
        if path.is_file():
            names.add(path)
    return sorted(names, key=lambda path: notice_relative_path(root, path).as_posix().casefold())


def collect(forge_manifest: Path, launcher_manifest: Path, output: Path) -> dict:
    forge = dependencies(metadata(forge_manifest), "forge_engine")
    launcher = dependencies(metadata(launcher_manifest), "forge_launcher_prototype")
    packages = {**forge, **launcher}
    output.mkdir(parents=True, exist_ok=False)
    entries = []
    for package in sorted(packages.values(), key=lambda item: (item["name"], item["version"], item["id"])):
        folder = re.sub(r"[^A-Za-z0-9._-]", "_", f"{package['name']}-{package['version']}")
        sources = notice_files(package)
        copied = []
        if sources:
            destination = output / folder
            destination.mkdir()
            for source in sources:
                relative = notice_relative_path(Path(package["manifest_path"]).parent, source)
                target = destination / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(source, target)
                copied.append((Path(folder) / relative).as_posix())
        entries.append(
            {
                "name": package["name"],
                "version": package["version"],
                "license": package.get("license"),
                "authors": package.get("authors", []),
                "repository": package.get("repository"),
                "source": package.get("source"),
                "notices": copied,
            }
        )
    index = {
        "schema": "forge.windows-playtest.rust-dependency-notices.v1",
        "target": "x86_64-pc-windows-msvc",
        "root_packages": ["forge_engine", "forge_launcher_prototype"],
        "package_count": len(entries),
        "packages_without_bundled_notice": sum(not entry["notices"] for entry in entries),
        "packages": entries,
    }
    (output / "INDEX.json").write_text(json.dumps(index, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return index


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("forge_manifest", type=Path)
    parser.add_argument("launcher_manifest", type=Path)
    parser.add_argument("output", type=Path)
    arguments = parser.parse_args()
    result = collect(arguments.forge_manifest, arguments.launcher_manifest, arguments.output)
    print(
        "RUST_NOTICES_COLLECTED"
        f" packages={result['package_count']}"
        f" missing_text={result['packages_without_bundled_notice']}"
    )
