#!/usr/bin/env python3
"""Download selected Quaternius CC0 packs from the author's public OpenGameArt pages."""

from __future__ import annotations

import argparse
from html import unescape
from pathlib import Path
import re
import sys
import time
from typing import Any
from urllib.parse import quote

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
PACKS: tuple[dict[str, Any], ...] = (
    {
        "slug": "lowpoly-modular-dungeon-pack",
        "name": "LowPoly Modular Dungeon Pack",
        "version": "May-2019",
        "page": "https://opengameart.org/content/lowpoly-modular-dungeon-pack",
        "archive": "Updated Modular Dungeon - May 2019.zip",
        "categories": ["Dungeon", "Architecture", "Props"],
        "tags": ["dungeon", "modular", "medieval", "roguelike"],
    },
    {
        "slug": "lowpoly-animated-monsters",
        "name": "LowPoly Animated Monsters",
        "version": "2018-08-16",
        "page": "https://opengameart.org/content/lowpoly-animated-monsters",
        "archive": "Animated Monster Pack by @Quaternius.zip",
        "categories": ["Creatures", "Animations", "Characters"],
        "tags": ["monster", "slime", "skeleton", "dragon", "bat", "animated"],
    },
    {
        "slug": "lowpoly-animated-knight",
        "name": "LowPoly Animated Knight",
        "version": "2018-07-15",
        "page": "https://opengameart.org/content/lowpoly-animated-knight",
        "archive": "Knight Character by @Quaternius.zip",
        "categories": ["Characters", "Animations", "Weapons"],
        "tags": ["knight", "warrior", "medieval", "fantasy", "animated"],
    },
    {
        "slug": "lowpoly-medieval-weapons",
        "name": "LowPoly Medieval Weapons",
        "version": "September-2018",
        "page": "https://opengameart.org/content/lowpoly-medieval-weapons",
        "archive": "Medieval Weapons Pack - Sept 2018.zip",
        "categories": ["Weapons", "Props"],
        "tags": ["weapon", "sword", "shield", "dagger", "bow", "axe"],
    },
    {
        "slug": "universal-animation-library-2-standard",
        "name": "Universal Animation Library 2",
        "version": "Standard-2026-01-23",
        "page": "https://opengameart.org/content/universal-animation-library-2",
        "archive": "universal_animation_library_2standard.zip",
        "categories": ["Animations", "Characters", "Weapons"],
        "tags": ["animation", "humanoid", "combat", "locomotion", "parkour", "farming"],
    },
)


def parse_download(pack: dict[str, Any]) -> tuple[str, str]:
    html = request_bytes(pack["page"]).decode("utf-8", errors="replace")
    plain = re.sub(r"\s+", " ", re.sub(r"<[^>]+>", " ", unescape(html)))
    if not re.search(r"Author:\s*quaternius", plain, re.IGNORECASE):
        raise RuntimeError(f"OpenGameArt page author is not Quaternius: {pack['page']}")
    if not re.search(r"License\(s\):\s*CC0", plain, re.IGNORECASE):
        raise RuntimeError(f"OpenGameArt page does not explicitly declare CC0: {pack['page']}")
    expected = re.escape(quote(pack["archive"]))
    match = re.search(rf'href="(https://opengameart\.org/sites/default/files/{expected})"', html, re.IGNORECASE)
    if not match:
        raise RuntimeError(f"Expected public archive is missing: {pack['archive']}")
    preview = re.search(r'<meta[^>]+property="og:image"[^>]+content="([^"]+)"', html, re.IGNORECASE)
    if not preview:
        raise RuntimeError(f"OpenGameArt page has no public preview: {pack['page']}")
    return unescape(match.group(1)), unescape(preview.group(1))


def download_pack(vault: Path, pack: dict[str, Any], *, refresh: bool = False) -> dict[str, Any]:
    url, preview_url = parse_download(pack)
    archive = vault / "originals" / "quaternius" / pack["slug"] / pack["archive"]
    downloaded = download_file(url, archive, refresh=refresh)
    extracted_root = vault / "normalized" / "quaternius" / pack["slug"]
    downloaded_archive = Path(downloaded["path"])
    if downloaded_archive.resolve() != archive.resolve():
        extracted_root = extracted_root / "versions" / downloaded["sha256"][:16]
    extracted = safe_extract_zip(downloaded_archive, extracted_root)
    preview_extension = Path(preview_url.split("?", 1)[0]).suffix.casefold() or ".png"
    preview_name = f"{pack['slug']}.preview{preview_extension}"
    preview = download_file(preview_url, extracted_root / preview_name)
    model_files = sorted(value for value in extracted if Path(value).suffix.casefold() in MODEL_EXTENSIONS)
    if not model_files:
        raise RuntimeError(f"No supported 3D model found in {archive}")
    metadata = {
        "schema_version": 1,
        "pack_id": f"quaternius:{pack['slug']}",
        "source": "quaternius",
        "name": pack["name"],
        "author": "Quaternius",
        "version": pack["version"],
        "source_page": pack["page"],
        "catalog_date": time.strftime("%Y-%m-%d", time.gmtime()),
        "retrieved_at": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "license": "CC0-1.0",
        "license_url": pack["page"],
        "license_verified": True,
        "attribution_required": False,
        "categories": pack["categories"],
        "tags": pack["tags"],
        "files": [downloaded, preview],
        "archive_sha256": downloaded["sha256"],
        "extracted_root": extracted_root.as_posix(),
        "extracted_files": extracted,
        "model_files": model_files,
        "preview_files": [preview_name],
        "model_file_count": len(model_files),
        "unique_model_name_count": len({Path(value).stem.casefold() for value in model_files}),
        "total_bytes": downloaded["size"] + preview["size"],
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
            print(f"[OpenGameArt {index}/{len(selected)}] {pack['slug']}", flush=True)
        except Exception as error:
            errors.append({"pack_id": f"quaternius:{pack['slug']}", "error": str(error)})
            print(f"[OpenGameArt ERROR {index}/{len(selected)}] {pack['slug']}: {error}", flush=True)
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
    output = args.catalog_output or repo_root / "asset_manifests" / "catalogs" / "quaternius_opengameart_packs.json"
    if not output.is_absolute():
        output = repo_root / output
    atomic_write_json(output, catalog)
    atomic_write_json(vault / "logs" / "latest-quaternius-opengameart.json", catalog)
    print(f"Catalog: {output}")
    print(f"Packs: {len(packs)}; models: {catalog['model_file_count']}; errors: {len(errors)}")
    return 0 if not errors else 2


if __name__ == "__main__":
    raise SystemExit(main())
