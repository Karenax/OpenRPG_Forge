#!/usr/bin/env python3
"""Download the audited KayKit CC0 medieval-fantasy packs from official GitHub repositories."""

from __future__ import annotations

import argparse
import json
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
CC0_MARKERS = ("Creative Commons Zero", "creativecommons.org/publicdomain/zero/1.0")

# Commits are intentionally pinned. The matching LICENSE.txt is checked again before download.
PACKS: tuple[dict[str, Any], ...] = (
    {
        "slug": "character-pack-skeletons-1.0",
        "name": "KayKit Character Pack: Skeletons",
        "version": "1.0",
        "repository": "KayKit-Character-Pack-Skeletons-1.0",
        "commit": "15b62b9bad122f72926c10fb14d622c73819fa54",
        "source_page": "https://kaylousberg.itch.io/kaykit-skeletons",
        "categories": ["Characters", "Creatures", "Animations", "Weapons"],
        "tags": ["skeleton", "undead", "character", "animated", "fantasy"],
    },
    {
        "slug": "dungeon-remastered-1.0",
        "name": "KayKit Dungeon Pack (Dungeon Remastered)",
        "version": "1.0",
        "repository": "KayKit-Dungeon-Remastered-1.0",
        "commit": "b0ca9bd96a8072ab36a3a5464f00ed1e06a16d07",
        "source_page": "https://kaylousberg.itch.io/kaykit-dungeon-pack",
        "categories": ["Dungeon", "Architecture", "Props", "Weapons", "Materials"],
        "tags": ["dungeon", "modular", "architecture", "prop", "fantasy"],
    },
)


def github_urls(pack: dict[str, Any]) -> tuple[str, str, str]:
    repository = pack["repository"]
    commit = pack["commit"]
    repository_url = f"https://github.com/KayKit-Game-Assets/{repository}"
    archive_url = f"https://codeload.github.com/KayKit-Game-Assets/{repository}/zip/{commit}"
    license_url = f"https://raw.githubusercontent.com/KayKit-Game-Assets/{repository}/{commit}/LICENSE.txt"
    return repository_url, archive_url, license_url


def verify_cc0(license_url: str) -> None:
    text = request_bytes(license_url).decode("utf-8-sig", errors="strict")
    if not all(marker.casefold() in text.casefold() for marker in CC0_MARKERS):
        raise RuntimeError(f"Pinned license does not explicitly declare CC0: {license_url}")


def download_pack(vault: Path, pack: dict[str, Any], *, refresh: bool = False) -> dict[str, Any]:
    repository_url, archive_url, license_url = github_urls(pack)
    verify_cc0(license_url)
    archive_name = f"{pack['repository']}-{pack['commit']}.zip"
    archive = vault / "originals" / "kaykit" / pack["slug"] / archive_name
    downloaded = download_file(archive_url, archive, refresh=refresh)
    extracted_root = vault / "normalized" / "kaykit" / pack["slug"]
    downloaded_archive = Path(downloaded["path"])
    if downloaded_archive.resolve() != archive.resolve():
        extracted_root = extracted_root / "versions" / downloaded["sha256"][:16]
    extracted = safe_extract_zip(downloaded_archive, extracted_root)
    model_files = sorted(
        value for value in extracted if Path(value).suffix.casefold() in MODEL_EXTENSIONS
    )
    preview_files = sorted(
        value for value in extracted
        if Path(value).suffix.casefold() in IMAGE_EXTENSIONS
        and any(token in Path(value).stem.casefold() for token in ("preview", "thumbnail", "render"))
    )
    if not model_files:
        raise RuntimeError(f"No supported 3D model found in {archive}")

    metadata = {
        "schema_version": 1,
        "pack_id": f"kaykit:{pack['slug']}",
        "source": "kaykit",
        "name": pack["name"],
        "author": "Kay Lousberg",
        "version": pack["version"],
        "source_page": pack["source_page"],
        "repository_url": repository_url,
        "source_commit": pack["commit"],
        "catalog_date": time.strftime("%Y-%m-%d", time.gmtime()),
        "retrieved_at": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "license": "CC0-1.0",
        "license_url": license_url.replace("raw.githubusercontent.com", "github.com").replace(
            f"/{pack['commit']}/LICENSE.txt", f"/blob/{pack['commit']}/LICENSE.txt"
        ),
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
    atomic_write_json(vault / "metadata" / "kaykit" / f"{pack['slug']}.json", metadata)
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
            print(f"[KayKit {index}/{len(selected)}] {pack['slug']}", flush=True)
        except Exception as error:  # Continue the remaining audited packs.
            errors.append({"pack_id": f"kaykit:{pack['slug']}", "error": str(error)})
            print(f"[KayKit ERROR {index}/{len(selected)}] {pack['slug']}: {error}", flush=True)
    packs.sort(key=lambda item: item["pack_id"])
    catalog = {
        "schema_version": 1,
        "catalog_date": time.strftime("%Y-%m-%d", time.gmtime()),
        "vault_root": "${OPENRPG_ASSET_VAULT}",
        "source": "kaykit",
        "license_policy": "cc0-only",
        "pack_count": len(packs),
        "model_file_count": sum(item["model_file_count"] for item in packs),
        "unique_model_name_count": sum(item["unique_model_name_count"] for item in packs),
        "total_bytes": sum(item["total_bytes"] for item in packs),
        "error_count": len(errors),
        "errors": errors,
        "packs": packs,
    }
    output = args.catalog_output or repo_root / "asset_manifests" / "catalogs" / "kaykit_medieval_fantasy.json"
    if not output.is_absolute():
        output = repo_root / output
    atomic_write_json(output, catalog)
    atomic_write_json(vault / "logs" / "latest-kaykit-medieval-fantasy.json", catalog)
    print(f"Catalog: {output}")
    print(f"Packs: {len(packs)}; models: {catalog['model_file_count']}; errors: {len(errors)}")
    return 0 if not errors else 2


if __name__ == "__main__":
    raise SystemExit(main())
