#!/usr/bin/env python3
"""Validate files and model dependencies referenced by a CC0 Asset Vault catalog."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import sys
from typing import Any
from urllib.parse import unquote, urlparse


def digest(path: Path) -> str:
    sha256 = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            sha256.update(chunk)
    return sha256.hexdigest()


def local_gltf_dependencies(path: Path) -> list[Path]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    uris = [item.get("uri") for item in payload.get("buffers", [])]
    uris.extend(item.get("uri") for item in payload.get("images", []))
    dependencies: list[Path] = []
    for uri in uris:
        if not uri or uri.startswith("data:"):
            continue
        parsed = urlparse(uri)
        if parsed.scheme or parsed.netloc:
            raise RuntimeError(f"External URI is not allowed in {path}: {uri}")
        dependency = (path.parent / unquote(parsed.path)).resolve()
        if path.parent.resolve() not in dependency.parents and dependency != path.parent.resolve():
            raise RuntimeError(f"glTF dependency escapes its asset folder in {path}: {uri}")
        dependencies.append(dependency)
    return dependencies


def validate(catalog_path: Path, vault: Path) -> dict[str, Any]:
    catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
    errors: list[str] = []
    checked_files = 0
    checked_dependencies = 0
    entries = catalog.get("assets") or catalog.get("packs") or []
    for asset in entries:
        asset_id = asset.get("asset_id") or asset["pack_id"]
        if asset.get("license") != "CC0-1.0" or not asset.get("license_verified"):
            errors.append(f"{asset_id}: license is not verified CC0-1.0")
        for item in asset.get("files", []):
            path = vault / item["path"]
            if not path.is_file():
                errors.append(f"{asset_id}: missing {item['path']}")
                continue
            checked_files += 1
            if path.stat().st_size != item["size"]:
                errors.append(f"{asset_id}: size mismatch for {item['path']}")
            elif digest(path) != item["sha256"]:
                errors.append(f"{asset_id}: SHA-256 mismatch for {item['path']}")
            if path.suffix.lower() == ".gltf":
                try:
                    for dependency in local_gltf_dependencies(path):
                        checked_dependencies += 1
                        if not dependency.is_file():
                            errors.append(f"{asset_id}: missing glTF dependency {dependency}")
                except (json.JSONDecodeError, RuntimeError) as error:
                    errors.append(f"{asset_id}: {error}")
        extracted_root = asset.get("extracted_root")
        for model_file in asset.get("model_files", []):
            path = vault / extracted_root / model_file
            if not path.is_file():
                errors.append(f"{asset_id}: missing extracted model {model_file}")
            elif path.suffix.lower() == ".gltf":
                try:
                    for dependency in local_gltf_dependencies(path):
                        checked_dependencies += 1
                        if not dependency.is_file():
                            errors.append(f"{asset_id}: missing glTF dependency {dependency}")
                except (json.JSONDecodeError, RuntimeError) as error:
                    errors.append(f"{asset_id}: {error}")

    return {
        "asset_count": len(entries),
        "checked_files": checked_files,
        "checked_gltf_dependencies": checked_dependencies,
        "error_count": len(errors),
        "errors": errors,
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("catalog", type=Path)
    parser.add_argument("--vault", type=Path)
    args = parser.parse_args(sys.argv[1:] if argv is None else argv)
    configured = os.environ.get("OPENRPG_ASSET_VAULT")
    vault = (args.vault or (Path(configured) if configured else None))
    if vault is None:
        raise SystemExit("Pass --vault or set OPENRPG_ASSET_VAULT")
    result = validate(args.catalog.resolve(), vault.resolve())
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if not result["errors"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
