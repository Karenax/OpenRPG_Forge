#!/usr/bin/env python3
"""Download audited Quaternius Standard CC0 packs from the official Godot Asset Store."""

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
    request_bytes,
    safe_extract_zip,
)


MODEL_EXTENSIONS = {".glb", ".gltf", ".fbx", ".obj"}
IMAGE_EXTENSIONS = {".png", ".jpg", ".jpeg", ".webp"}

PACKS: tuple[dict[str, Any], ...] = (
    {
        "slug": "medieval-village-megakit-standard-1.0",
        "name": "Medieval Village MegaKit",
        "version": "Standard-1.0",
        "store_slug": "medieval-village-megakit",
        "download_id": 142,
        "archive": "Medieval_Village_MegaKitStandard.zip",
        "itch_page": "https://quaternius.itch.io/medieval-village-megakit",
        "categories": ["Village", "Architecture", "Props", "Materials"],
        "tags": ["medieval", "village", "modular", "building", "roof", "wall"],
    },
    {
        "slug": "fantasy-props-megakit-standard-1.0",
        "name": "Fantasy Props MegaKit",
        "version": "Standard-1.0",
        "store_slug": "fantasy-props-megakit",
        "download_id": 141,
        "archive": "Fantasy_Props_MegaKitStandard.zip",
        "itch_page": "https://quaternius.itch.io/fantasy-props-megakit",
        "categories": ["Props", "Weapons", "Village", "Materials"],
        "tags": ["fantasy", "medieval", "prop", "weapon", "furniture", "market"],
    },
    {
        "slug": "stylized-nature-megakit-standard-1.0",
        "name": "Stylized Nature MegaKit",
        "version": "Standard-1.0",
        "store_slug": "stylized-nature-megakit",
        "download_id": 31,
        "archive": "Stylized_Nature_MegaKitStandard.zip",
        "itch_page": "https://quaternius.itch.io/stylized-nature-megakit",
        "categories": ["Nature", "Props", "Materials"],
        "tags": ["nature", "forest", "tree", "rock", "plant", "grass"],
    },
    {
        "slug": "universal-animation-library-standard-1.0",
        "name": "Universal Animation Library",
        "version": "Standard-1.0",
        "store_slug": "universal-animation-library",
        "download_id": 44,
        "archive": "Universal_Animation_LibraryStandard.zip",
        "itch_page": "https://quaternius.itch.io/universal-animation-library",
        "categories": ["Animations", "Characters", "Weapons"],
        "tags": ["animation", "humanoid", "retarget", "locomotion", "combat"],
    },
)


def store_page(pack: dict[str, Any]) -> str:
    return f"https://store.godotengine.org/asset/quaternius/{pack['store_slug']}/"


def download_url(pack: dict[str, Any]) -> str:
    return f"{store_page(pack)}download/{pack['download_id']}/"


def verify_store_record(pack: dict[str, Any]) -> None:
    text = request_bytes(store_page(pack)).decode("utf-8", errors="replace").casefold()
    required = ("quaternius", "cc0 1.0 universal", pack["version"].casefold())
    if not all(marker in text for marker in required):
        raise RuntimeError(f"Godot Asset Store record is missing its expected author, version or CC0 license: {store_page(pack)}")


def download_pack(vault: Path, pack: dict[str, Any], *, refresh: bool = False) -> dict[str, Any]:
    verify_store_record(pack)
    archive = vault / "originals" / "quaternius" / pack["slug"] / pack["archive"]
    downloaded = download_file(download_url(pack), archive, refresh=refresh)
    extracted_root = vault / "normalized" / "quaternius" / pack["slug"]
    downloaded_archive = Path(downloaded["path"])
    if downloaded_archive.resolve() != archive.resolve():
        extracted_root = extracted_root / "versions" / downloaded["sha256"][:16]
    extracted = safe_extract_zip(downloaded_archive, extracted_root)
    model_files = sorted(value for value in extracted if Path(value).suffix.casefold() in MODEL_EXTENSIONS)
    preview_files = sorted(
        value for value in extracted
        if Path(value).suffix.casefold() in IMAGE_EXTENSIONS
        and any(token in Path(value).stem.casefold() for token in ("preview", "thumbnail", "render", "sample"))
    )
    if not model_files:
        raise RuntimeError(f"No supported 3D model found in {archive}")
    metadata = {
        "schema_version": 1,
        "pack_id": f"quaternius:{pack['slug']}",
        "source": "quaternius",
        "name": pack["name"],
        "author": "Quaternius",
        "version": pack["version"],
        "source_page": pack["itch_page"],
        "official_distribution_page": store_page(pack),
        "catalog_date": time.strftime("%Y-%m-%d", time.gmtime()),
        "retrieved_at": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "license": "CC0-1.0",
        "license_url": store_page(pack),
        "license_verified": True,
        "attribution_required": False,
        "categories": pack["categories"],
        "tags": pack["tags"],
        "files": [downloaded],
        "archive_sha256": downloaded["sha256"],
        "extracted_root": extracted_root.as_posix(),
        "extracted_files": extracted,
        "model_files": model_files,
        "preview_files": preview_files,
        "model_file_count": len(model_files),
        "unique_model_name_count": len({Path(value).stem.casefold() for value in model_files}),
        "total_bytes": downloaded["size"],
        "status": "downloaded-and-extracted",
    }
    metadata = relativize_vault_paths(metadata, vault)
    atomic_write_json(vault / "metadata" / "quaternius" / f"{pack['slug']}.json", metadata)
    return metadata


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--vault", type=Path)
    parser.add_argument("--catalog-output", type=Path)
    parser.add_argument("--pack", action="append", choices=[pack["slug"] for pack in PACKS])
    parser.add_argument("--update", action="store_true")
    args = parser.parse_args(sys.argv[1:] if argv is None else argv)
    repo_root = Path(__file__).resolve().parents[2]
    vault = (args.vault or default_vault(repo_root)).resolve()
    selected = [pack for pack in PACKS if not args.pack or pack["slug"] in args.pack]
    packs: list[dict[str, Any]] = []
    errors: list[dict[str, str]] = []
    for index, pack in enumerate(selected, start=1):
        try:
            packs.append(download_pack(vault, pack, refresh=args.update))
            print(f"[Quaternius {index}/{len(selected)}] {pack['slug']}", flush=True)
        except Exception as error:
            errors.append({"pack_id": f"quaternius:{pack['slug']}", "error": str(error)})
            print(f"[Quaternius ERROR {index}/{len(selected)}] {pack['slug']}: {error}", flush=True)
    packs.sort(key=lambda item: item["pack_id"])
    catalog = {
        "schema_version": 1,
        "catalog_date": time.strftime("%Y-%m-%d", time.gmtime()),
        "vault_root": "${OPENRPG_ASSET_VAULT}",
        "source": "quaternius",
        "license_policy": "cc0-only",
        "pack_count": len(packs),
        "model_file_count": sum(item["model_file_count"] for item in packs),
        "unique_model_name_count": sum(item["unique_model_name_count"] for item in packs),
        "total_bytes": sum(item["total_bytes"] for item in packs),
        "error_count": len(errors),
        "errors": errors,
        "packs": packs,
    }
    output = args.catalog_output or repo_root / "asset_manifests" / "catalogs" / "quaternius_standard_packs.json"
    if not output.is_absolute():
        output = repo_root / output
    atomic_write_json(output, catalog)
    atomic_write_json(vault / "logs" / "latest-quaternius-standard.json", catalog)
    print(f"Catalog: {output}")
    print(f"Packs: {len(packs)}; models: {catalog['model_file_count']}; errors: {len(errors)}")
    return 0 if not errors else 2


if __name__ == "__main__":
    raise SystemExit(main())
