#!/usr/bin/env python3
"""Compose a complete package-valid Tidewater bundle with PNG Joe substituted.

The input bundles are read-only.  The composed bundle is a full byte copy of
bundle 009 except for ``scene/joe.glb``, which comes from the additive PNG
fallback bundle 010.  Conversion provenance stays beside the package rather
than in it because the package manifest admits only runtime input groups.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import sys
from typing import Any


EXPECTED_SOURCE_JOE = "da3efce5a452d3c17f40b8d33bf449f565c0a6bef93602dc3e74364fb64081ca"
EXPECTED_PNG_JOE = "6a859d795aab742c39a92163a17687f93036973184455562d917dff3af7432e4"
JOE_PATH = "scene/joe.glb"


def digest_file(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(path: Path) -> dict[str, Any]:
    return {"bytes": path.stat().st_size, "sha256": digest_file(path)}


def canonical_json(value: Any) -> bytes:
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True).encode("ascii") + b"\n"


def read_manifest(root: Path) -> dict[str, Any]:
    path = root / "preview-scene.json"
    if not path.is_file():
        raise FileNotFoundError(f"manifest missing: {path}")
    return json.loads(path.read_text(encoding="utf-8"))


def require_regular(path: Path) -> None:
    if not path.is_file() or path.is_symlink() or path.is_junction():
        raise ValueError(f"source must be a regular non-link file: {path}")


def load_packager(script: Path) -> Any:
    specification = importlib.util.spec_from_file_location("tidewater_packager", script)
    if specification is None or specification.loader is None:
        raise RuntimeError(f"cannot load package verifier: {script}")
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


def compose(base: Path, additive: Path, output: Path, sidecar: Path, packager_script: Path) -> dict[str, Any]:
    base_manifest = read_manifest(base)
    expected_fields = {"schema", "version", "source_commit", "area_id", "building_package_sha256", "files", "scope"}
    if set(base_manifest) != expected_fields:
        raise ValueError("bundle 009 manifest has an unexpected schema")
    base_files = base_manifest["files"]
    if JOE_PATH not in base_files or base_files[JOE_PATH]["sha256"] != EXPECTED_SOURCE_JOE:
        raise ValueError("bundle 009 does not carry the pinned original Joe")

    additive_manifest = read_manifest(additive)
    derived_record = additive_manifest.get("files", {}).get(JOE_PATH)
    if derived_record != {"bytes": 9619968, "sha256": EXPECTED_PNG_JOE}:
        raise ValueError("bundle 010 does not carry the expected converted Joe")
    provenance = additive / "provenance" / "joe-jpeg-to-png.json"
    validation = additive / "validation" / "joe.gltf-validator.json"
    source_license = base / "fish-stand-content" / "source" / "public" / "models" / "characters" / "LICENSE-Rocketbox.md"
    for required in (provenance, validation, source_license):
        require_regular(required)
    if output.exists() or sidecar.exists():
        raise FileExistsError("refusing to mutate an existing output bundle or provenance sidecar")
    staging = output.with_name(output.name + ".partial")
    sidecar_staging = sidecar.with_name(sidecar.name + ".partial")
    if staging.exists() or sidecar_staging.exists():
        raise FileExistsError("refusing to reuse an incomplete staging path")

    try:
        entries: dict[str, dict[str, Any]] = {}
        staging.mkdir(parents=True)
        for relative, expected in sorted(base_files.items()):
            source = (additive / relative) if relative == JOE_PATH else (base / relative)
            require_regular(source)
            actual = record(source)
            if relative != JOE_PATH and actual != expected:
                raise ValueError(f"bundle 009 source hash changed: {relative}")
            if relative == JOE_PATH and actual != derived_record:
                raise ValueError("bundle 010 PNG Joe hash changed")
            target = staging / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            # copyfile creates independent bytes; it never makes a hardlink.
            shutil.copyfile(source, target)
            if record(target) != actual:
                raise ValueError(f"copied file hash mismatch: {relative}")
            entries[relative] = actual

        manifest = {key: base_manifest[key] for key in expected_fields if key != "files"}
        manifest["files"] = entries
        (staging / "preview-scene.json").write_bytes(canonical_json(manifest))
        manifest_hash = digest_file(staging / "preview-scene.json")
        packager = load_packager(packager_script)
        verification = packager.verify(staging, manifest_hash)
        if verification["files"] != len(entries):
            raise ValueError("package verifier reported a different file count")

        sidecar_staging.mkdir(parents=True)
        shutil.copyfile(provenance, sidecar_staging / "joe-jpeg-to-png.json")
        shutil.copyfile(validation, sidecar_staging / "joe.gltf-validator.json")
        shutil.copyfile(source_license, sidecar_staging / "LICENSE-Rocketbox.md")
        sidecar_record = {
            "schema": "forge.tidewater.composed-joe-png-bundle.v1",
            "base_bundle": {"path": base.name, "manifest_sha256": digest_file(base / "preview-scene.json")},
            "additive_bundle": {"path": additive.name, "manifest_sha256": digest_file(additive / "preview-scene.json")},
            "composed_bundle": {"path": output.name, "manifest_sha256": manifest_hash, "files": len(entries)},
            "source_joe_sha256": EXPECTED_SOURCE_JOE,
            "derived_joe_sha256": EXPECTED_PNG_JOE,
            "license": "LICENSE-Rocketbox.md",
            "conversion_provenance": "joe-jpeg-to-png.json",
            "khronos_validation": "joe.gltf-validator.json",
            "copy_policy": "All bundle bytes were copied with shutil.copyfile; no source or output hardlinks are used.",
        }
        (sidecar_staging / "composition.json").write_bytes(canonical_json(sidecar_record))
        sidecar_staging.rename(sidecar)
        staging.rename(output)
        return {"bundle": str(output), "manifest_sha256": manifest_hash, "files": len(entries), "bytes": verification["bytes"], "provenance": str(sidecar)}
    except Exception:
        if staging.exists():
            shutil.rmtree(staging)
        if sidecar_staging.exists():
            shutil.rmtree(sidecar_staging)
        raise


def main() -> int:
    integration_default = Path(__file__).resolve().parents[3] / ".tidewater-integration"
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--integration-root", type=Path, default=integration_default)
    parser.add_argument("--base", type=Path)
    parser.add_argument("--additive", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--provenance-output", type=Path)
    arguments = parser.parse_args()
    integration = arguments.integration_root.resolve()
    base = (arguments.base or integration / "preview-scene-bundle-fish-stand-content-fish-009").resolve()
    additive = (arguments.additive or integration / "preview-scene-bundle-fish-stand-content-fish-010").resolve()
    output = (arguments.output or integration / "preview-scene-bundle-fish-stand-content-fish-011").resolve()
    sidecar = (arguments.provenance_output or integration / "preview-scene-bundle-fish-stand-content-fish-011.provenance").resolve()
    packager = Path(__file__).with_name("package-preview-scene.py")
    print(json.dumps(compose(base, additive, output, sidecar, packager), sort_keys=True))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (FileNotFoundError, ValueError, OSError, RuntimeError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        raise SystemExit(1)
