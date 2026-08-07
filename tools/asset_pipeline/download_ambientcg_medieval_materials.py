#!/usr/bin/env python3
"""Download a bounded CC0 ambientCG medieval material collection at 2K or below."""

from __future__ import annotations

import argparse
from pathlib import Path
import sys
import time
from typing import Any
from urllib.parse import urlencode

sys.path.insert(0, str(Path(__file__).resolve().parent))
from download_cc0_3d import (  # noqa: E402
    AMBIENTCG_ASSETS_URL,
    atomic_write_json,
    default_vault,
    download_file,
    relativize_vault_paths,
    request_json,
    safe_extract_zip,
)


KEYWORDS = {
    "bark", "brick", "castle", "cliff", "cobble", "dirt", "fabric", "forest", "ground",
    "leather", "metal", "moss", "plaster", "rock", "roof", "ruin", "stone", "tile",
    "wood", "wooden", "path", "medieval", "mud", "gravel",
}
IMAGE_EXTENSIONS = {".png", ".jpg", ".jpeg", ".webp"}


def select_assets(maximum: int, quality: str) -> list[dict[str, Any]]:
    query = urlencode({
        "type": "material",
        "sort": "alphabet",
        "limit": 500,
        "include": "type,title,url,tags,downloads,thumbnails,releaseDate",
    })
    payload = request_json(f"{AMBIENTCG_ASSETS_URL}?{query}")
    scored: list[tuple[int, str, dict[str, Any]]] = []
    for item in payload["assets"]:
        words = {token.casefold() for token in str(item.get("title", "")).replace("_", " ").split()}
        words.update(str(token).casefold() for token in item.get("tags") or [])
        variant = next((value for value in item.get("downloads") or [] if value.get("attributes") == quality), None)
        score = len(words & KEYWORDS)
        if score and variant:
            scored.append((score, str(item["id"]), item | {"selected_download": variant}))
    scored.sort(key=lambda value: (-value[0], value[1]))
    return [item for _, _, item in scored[:maximum]]


def download_asset(vault: Path, item: dict[str, Any], quality: str) -> dict[str, Any]:
    asset_id = str(item["id"])
    variant = item["selected_download"]
    archive = vault / "originals" / "ambientcg" / "materials" / asset_id / f"{asset_id}_{quality}.zip"
    downloaded = download_file(variant["url"], archive)
    extracted_root = vault / "normalized" / "ambientcg" / "materials" / asset_id / quality.casefold()
    downloaded_archive = Path(downloaded["path"])
    if downloaded_archive.resolve() != archive.resolve():
        extracted_root = extracted_root / "versions" / downloaded["sha256"][:16]
    extracted = safe_extract_zip(downloaded_archive, extracted_root)
    images = [value for value in extracted if Path(value).suffix.casefold() in IMAGE_EXTENSIONS]
    primary = next((value for value in images if "color" in Path(value).stem.casefold()), None)
    if primary is None:
        raise RuntimeError(f"No color texture found in {archive}")
    thumbnail_url = (item.get("thumbnails") or {}).get("256-WEBP")
    if not thumbnail_url:
        raise RuntimeError(f"No official thumbnail for {asset_id}")
    preview = download_file(thumbnail_url, extracted_root / f"{asset_id}.preview.webp")
    return {
        "schema_version": 1,
        "asset_id": f"ambientcg-material:{asset_id}",
        "source": "ambientcg",
        "source_asset_id": asset_id,
        "name": item.get("title") or asset_id,
        "author": "ambientCG",
        "kind": "material",
        "version": quality,
        "source_page": item.get("url") or f"https://ambientcg.com/view?id={asset_id}",
        "catalog_date": time.strftime("%Y-%m-%d", time.gmtime()),
        "retrieved_at": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "license": "CC0-1.0",
        "license_url": "https://docs.ambientcg.com/license/",
        "license_verified": True,
        "attribution_required": False,
        "categories": ["Materials"],
        "tags": item.get("tags") or [],
        "variant": quality,
        "primary_file": str((extracted_root / primary).relative_to(vault)).replace("\\", "/"),
        "preview_file": str(Path(preview["path"]).relative_to(vault)).replace("\\", "/"),
        "files": [downloaded, preview],
        "extracted_root": extracted_root.as_posix(),
        "extracted_files": extracted,
        "total_bytes": downloaded["size"] + preview["size"],
        "status": "downloaded-and-extracted",
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--vault", type=Path)
    parser.add_argument("--catalog-output", type=Path)
    parser.add_argument("--max-assets", type=int, default=16)
    parser.add_argument("--quality", choices=("1K-JPG", "2K-JPG"), default="2K-JPG")
    args = parser.parse_args(sys.argv[1:] if argv is None else argv)
    repo_root = Path(__file__).resolve().parents[2]
    vault = (args.vault or default_vault(repo_root)).resolve()
    assets: list[dict[str, Any]] = []
    errors: list[dict[str, str]] = []
    selected = select_assets(args.max_assets, args.quality)
    for index, item in enumerate(selected, start=1):
        try:
            asset = relativize_vault_paths(download_asset(vault, item, args.quality), vault)
            assets.append(asset)
            atomic_write_json(vault / "metadata" / "ambientcg" / "materials" / f"{item['id']}.json", asset)
            print(f"[ambientCG {index}/{len(selected)}] {item['id']}", flush=True)
        except Exception as error:
            errors.append({"asset_id": f"ambientcg-material:{item['id']}", "error": str(error)})
            print(f"[ambientCG ERROR {index}/{len(selected)}] {item['id']}: {error}", flush=True)
    assets.sort(key=lambda item: item["asset_id"])
    catalog = {
        "schema_version": 1,
        "catalog_date": time.strftime("%Y-%m-%d", time.gmtime()),
        "vault_root": "${OPENRPG_ASSET_VAULT}",
        "source": "ambientcg",
        "license_policy": "cc0-only",
        "asset_count": len(assets),
        "total_bytes": sum(item["total_bytes"] for item in assets),
        "error_count": len(errors),
        "errors": errors,
        "assets": assets,
    }
    output = args.catalog_output or repo_root / "asset_manifests" / "catalogs" / "ambientcg_medieval_materials.json"
    if not output.is_absolute():
        output = repo_root / output
    atomic_write_json(output, catalog)
    atomic_write_json(vault / "logs" / "latest-ambientcg-medieval-materials.json", catalog)
    print(f"Catalog: {output}")
    print(f"Assets: {len(assets)}; errors: {len(errors)}; bytes: {catalog['total_bytes']}")
    return 0 if not errors else 2


if __name__ == "__main__":
    raise SystemExit(main())
