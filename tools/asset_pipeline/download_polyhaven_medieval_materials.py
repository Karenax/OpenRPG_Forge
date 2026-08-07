#!/usr/bin/env python3
"""Download a bounded, reproducible Poly Haven CC0 medieval material and HDRI collection."""

from __future__ import annotations

import argparse
from pathlib import Path
import sys
import time
from typing import Any

sys.path.insert(0, str(Path(__file__).resolve().parent))
from download_cc0_3d import (  # noqa: E402
    atomic_write_json,
    default_vault,
    download_file,
    relativize_vault_paths,
    request_json,
)


ASSETS_URL = "https://api.polyhaven.com/assets?t={kind}"
FILES_URL = "https://api.polyhaven.com/files/{asset_id}"
KEYWORDS = {
    "bark", "beam", "brick", "castle", "cliff", "cobble", "dirt", "fabric", "forest",
    "ground", "leather", "metal", "moss", "plaster", "rock", "roof", "ruin", "stone",
    "tile", "wood", "wooden", "path", "road", "medieval", "church", "courtyard",
}
MATERIAL_CHANNELS = ("Diffuse", "nor_gl", "Rough", "Displacement")


def text_values(metadata: dict[str, Any]) -> set[str]:
    values = [metadata.get("name", ""), *(metadata.get("categories") or []), *(metadata.get("tags") or [])]
    return {token.casefold() for value in values for token in str(value).replace("_", " ").split()}


def select_assets(kind: str, maximum: int) -> list[tuple[str, dict[str, Any]]]:
    payload = request_json(ASSETS_URL.format(kind=kind))
    scored = []
    for asset_id, metadata in payload.items():
        score = len(text_values(metadata) & KEYWORDS)
        if score:
            scored.append((score, asset_id, metadata))
    scored.sort(key=lambda value: (-value[0], value[1]))
    return [(asset_id, metadata) for _, asset_id, metadata in scored[:maximum]]


def pick_variant(data: dict[str, Any], channel: str, resolution: str, extension: str) -> dict[str, Any] | None:
    channel_data = data.get(channel) or data.get(channel.casefold()) or {}
    return ((channel_data.get(resolution) or {}).get(extension))


def download_material(vault: Path, asset_id: str, metadata: dict[str, Any], resolution: str) -> dict[str, Any]:
    source = request_json(FILES_URL.format(asset_id=asset_id))
    base = vault / "originals" / "polyhaven" / "materials" / asset_id / resolution
    files: list[dict[str, Any]] = []
    channels: dict[str, str] = {}
    for channel in MATERIAL_CHANNELS:
        variant = pick_variant(source, channel, resolution, "jpg")
        if not variant:
            continue
        downloaded = download_file(variant["url"], base / Path(variant["url"]).name, variant.get("md5"))
        files.append(downloaded)
        channels[channel] = downloaded["path"]
    if "Diffuse" not in channels:
        raise RuntimeError(f"No {resolution} JPEG diffuse map for {asset_id}")
    preview_url = f"https://cdn.polyhaven.com/asset_img/thumbs/{asset_id}.png?width=320&height=240"
    preview = download_file(preview_url, base / f"{asset_id}.preview.png")
    files.append(preview)
    return {
        "schema_version": 1,
        "asset_id": f"polyhaven-material:{asset_id}",
        "source": "polyhaven",
        "source_asset_id": asset_id,
        "name": metadata.get("name") or asset_id,
        "author": "Poly Haven contributors",
        "kind": "material",
        "version": resolution,
        "source_page": f"https://polyhaven.com/a/{asset_id}",
        "catalog_date": time.strftime("%Y-%m-%d", time.gmtime()),
        "retrieved_at": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "license": "CC0-1.0",
        "license_url": "https://polyhaven.com/license",
        "license_verified": True,
        "attribution_required": False,
        "categories": ["Materials", *(metadata.get("categories") or [])],
        "tags": metadata.get("tags") or [],
        "variant": resolution,
        "primary_file": channels["Diffuse"],
        "preview_file": preview["path"],
        "material_channels": channels,
        "files": files,
        "total_bytes": sum(item["size"] for item in files),
        "status": "downloaded-original",
    }


def download_hdri(vault: Path, asset_id: str, metadata: dict[str, Any], resolution: str) -> dict[str, Any]:
    source = request_json(FILES_URL.format(asset_id=asset_id))
    variant = (((source.get("hdri") or {}).get(resolution) or {}).get("hdr"))
    if not variant:
        raise RuntimeError(f"No {resolution} HDR variant for {asset_id}")
    base = vault / "originals" / "polyhaven" / "hdri" / asset_id / resolution
    hdri = download_file(variant["url"], base / Path(variant["url"]).name, variant.get("md5"))
    preview_url = f"https://cdn.polyhaven.com/asset_img/thumbs/{asset_id}.png?width=320&height=160"
    preview = download_file(preview_url, base / f"{asset_id}.preview.png")
    return {
        "schema_version": 1,
        "asset_id": f"polyhaven-hdri:{asset_id}",
        "source": "polyhaven",
        "source_asset_id": asset_id,
        "name": metadata.get("name") or asset_id,
        "author": "Poly Haven contributors",
        "kind": "hdri",
        "version": resolution,
        "source_page": f"https://polyhaven.com/a/{asset_id}",
        "catalog_date": time.strftime("%Y-%m-%d", time.gmtime()),
        "retrieved_at": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "license": "CC0-1.0",
        "license_url": "https://polyhaven.com/license",
        "license_verified": True,
        "attribution_required": False,
        "categories": ["HDRI", *(metadata.get("categories") or [])],
        "tags": metadata.get("tags") or [],
        "variant": resolution,
        "primary_file": hdri["path"],
        "preview_file": preview["path"],
        "files": [hdri, preview],
        "total_bytes": hdri["size"] + preview["size"],
        "status": "downloaded-original",
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--vault", type=Path)
    parser.add_argument("--catalog-output", type=Path)
    parser.add_argument("--max-materials", type=int, default=24)
    parser.add_argument("--max-hdris", type=int, default=8)
    parser.add_argument("--material-resolution", choices=("1k", "2k"), default="2k")
    parser.add_argument("--hdri-resolution", choices=("1k", "2k"), default="2k")
    args = parser.parse_args(sys.argv[1:] if argv is None else argv)
    repo_root = Path(__file__).resolve().parents[2]
    vault = (args.vault or default_vault(repo_root)).resolve()
    assets: list[dict[str, Any]] = []
    errors: list[dict[str, str]] = []
    tasks = [
        ("material", asset_id, metadata)
        for asset_id, metadata in select_assets("textures", args.max_materials)
    ] + [
        ("hdri", asset_id, metadata)
        for asset_id, metadata in select_assets("hdris", args.max_hdris)
    ]
    for index, (kind, asset_id, metadata) in enumerate(tasks, start=1):
        try:
            asset = (
                download_material(vault, asset_id, metadata, args.material_resolution)
                if kind == "material"
                else download_hdri(vault, asset_id, metadata, args.hdri_resolution)
            )
            asset = relativize_vault_paths(asset, vault)
            assets.append(asset)
            atomic_write_json(vault / "metadata" / "polyhaven" / kind / f"{asset_id}.json", asset)
            print(f"[Poly Haven {index}/{len(tasks)}] {kind}:{asset_id}", flush=True)
        except Exception as error:
            errors.append({"asset_id": f"polyhaven-{kind}:{asset_id}", "error": str(error)})
            print(f"[Poly Haven ERROR {index}/{len(tasks)}] {kind}:{asset_id}: {error}", flush=True)
    assets.sort(key=lambda item: item["asset_id"])
    catalog = {
        "schema_version": 1,
        "catalog_date": time.strftime("%Y-%m-%d", time.gmtime()),
        "vault_root": "${OPENRPG_ASSET_VAULT}",
        "source": "polyhaven",
        "license_policy": "cc0-only",
        "asset_count": len(assets),
        "total_bytes": sum(item["total_bytes"] for item in assets),
        "error_count": len(errors),
        "errors": errors,
        "assets": assets,
    }
    output = args.catalog_output or repo_root / "asset_manifests" / "catalogs" / "polyhaven_medieval_materials_hdri.json"
    if not output.is_absolute():
        output = repo_root / output
    atomic_write_json(output, catalog)
    atomic_write_json(vault / "logs" / "latest-polyhaven-medieval-materials.json", catalog)
    print(f"Catalog: {output}")
    print(f"Assets: {len(assets)}; errors: {len(errors)}; bytes: {catalog['total_bytes']}")
    return 0 if not errors else 2


if __name__ == "__main__":
    raise SystemExit(main())
