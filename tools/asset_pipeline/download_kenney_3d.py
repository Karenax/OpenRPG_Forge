#!/usr/bin/env python3
"""Download every free CC0 3D pack currently listed by Kenney."""

from __future__ import annotations

import argparse
import concurrent.futures
from html import unescape
import json
import os
from pathlib import Path
import re
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


CATEGORY_URL = "https://kenney.nl/assets/category:3D"
ASSET_LINK = re.compile(r'href=["\'](https://kenney\.nl/assets/[^"\']+)["\']', re.IGNORECASE)
ZIP_LINK = re.compile(r'https://kenney\.nl/media/pages/assets/[^"\'<> ]+\.zip', re.IGNORECASE)
TAG = re.compile(r"<[^>]+>")
MODEL_EXTENSIONS = {".glb", ".gltf", ".fbx", ".obj"}


def text_content(html: str) -> str:
    return re.sub(r"\s+", " ", unescape(TAG.sub(" ", html))).strip()


def discover_pack_pages() -> list[str]:
    pages: list[str] = []
    for page_number in range(1, 21):
        url = CATEGORY_URL if page_number == 1 else f"{CATEGORY_URL}/page:{page_number}"
        try:
            html = request_bytes(url).decode("utf-8")
        except RuntimeError:
            if page_number == 1:
                raise
            break
        found = []
        for match in ASSET_LINK.finditer(html):
            link = match.group(1).split("?", 1)[0].rstrip("/")
            suffix = link.removeprefix("https://kenney.nl/assets/")
            if "/" in suffix or ":" in suffix:
                continue
            found.append(link)
        if not found:
            break
        pages.extend(found)
    return sorted(set(pages))


def parse_pack_page(url: str) -> dict[str, Any]:
    html = request_bytes(url).decode("utf-8")
    text = text_content(html)
    if "License Creative Commons CC0" not in text:
        raise RuntimeError(f"Page does not declare Creative Commons CC0: {url}")
    zip_match = ZIP_LINK.search(html)
    if not zip_match:
        raise RuntimeError(f"No official ZIP download found: {url}")
    slug = url.rstrip("/").rsplit("/", 1)[-1]
    title_match = re.search(r"<title>(.*?)</title>", html, re.IGNORECASE | re.DOTALL)
    title = text_content(title_match.group(1)) if title_match else slug.replace("-", " ").title()
    title = re.sub(r"\s*[·•|-]\s*Kenney\s*$", "", title, flags=re.IGNORECASE)
    count_match = re.search(r"Files\s+([0-9,]+)\s*[×x]", text)
    return {
        "id": slug,
        "title": title,
        "source_page": url,
        "download_url": zip_match.group(0),
        "advertised_file_count": int(count_match.group(1).replace(",", "")) if count_match else None,
    }


def download_pack(vault: Path, record: dict[str, Any]) -> dict[str, Any]:
    pack_id = record["id"]
    archive = vault / "originals" / "kenney" / pack_id / Path(record["download_url"]).name
    downloaded = download_file(record["download_url"], archive)
    extracted_root = vault / "normalized" / "kenney" / pack_id
    extracted = safe_extract_zip(archive, extracted_root)
    model_files = sorted(name for name in extracted if Path(name).suffix.lower() in MODEL_EXTENSIONS)
    if not model_files:
        raise RuntimeError(f"No supported 3D file found in {archive}")
    unique_models = sorted({Path(name).stem.lower() for name in model_files})
    metadata = {
        "schema_version": 1,
        "pack_id": f"kenney:{pack_id}",
        "source": "kenney",
        "name": record["title"],
        "source_page": record["source_page"],
        "catalog_date": time.strftime("%Y-%m-%d", time.gmtime()),
        "license": "CC0-1.0",
        "license_url": record["source_page"],
        "license_verified": True,
        "advertised_file_count": record["advertised_file_count"],
        "files": [downloaded],
        "extracted_root": extracted_root.as_posix(),
        "extracted_files": extracted,
        "model_files": model_files,
        "model_file_count": len(model_files),
        "unique_model_name_count": len(unique_models),
        "total_bytes": downloaded["size"],
        "status": "downloaded-and-extracted",
    }
    metadata = relativize_vault_paths(metadata, vault)
    atomic_write_json(vault / "metadata" / "kenney" / f"{pack_id}.json", metadata)
    return metadata


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--vault", type=Path)
    parser.add_argument("--jobs", type=int, default=4)
    parser.add_argument("--max-packs", type=int)
    parser.add_argument("--catalog-output", type=Path)
    args = parser.parse_args(sys.argv[1:] if argv is None else argv)
    repo_root = Path(__file__).resolve().parents[2]
    configured = os.environ.get("OPENRPG_ASSET_VAULT")
    vault = (args.vault or (Path(configured) if configured else default_vault(repo_root))).resolve()
    pages = discover_pack_pages()
    if args.max_packs:
        pages = pages[: args.max_packs]

    records: list[dict[str, Any]] = []
    page_errors: list[dict[str, str]] = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=args.jobs) as executor:
        futures = {executor.submit(parse_pack_page, page): page for page in pages}
        for future in concurrent.futures.as_completed(futures):
            page = futures[future]
            try:
                records.append(future.result())
            except Exception as error:
                page_errors.append({"page": page, "error": str(error)})

    packs: list[dict[str, Any]] = []
    errors: list[dict[str, str]] = list(page_errors)
    with concurrent.futures.ThreadPoolExecutor(max_workers=args.jobs) as executor:
        futures = {executor.submit(download_pack, vault, record): record for record in records}
        for index, future in enumerate(concurrent.futures.as_completed(futures), start=1):
            record = futures[future]
            try:
                packs.append(future.result())
                print(f"[Kenney {index}/{len(records)}] {record['id']}", flush=True)
            except Exception as error:
                errors.append({"pack_id": record["id"], "error": str(error)})
                print(f"[Kenney ERROR {index}/{len(records)}] {record['id']}: {error}", flush=True)

    packs.sort(key=lambda item: item["pack_id"])
    catalog = {
        "schema_version": 1,
        "catalog_date": time.strftime("%Y-%m-%d", time.gmtime()),
        "vault_root": "${OPENRPG_ASSET_VAULT}",
        "license_policy": "cc0-only",
        "pack_count": len(packs),
        "model_file_count": sum(item["model_file_count"] for item in packs),
        "unique_model_name_count": sum(item["unique_model_name_count"] for item in packs),
        "total_bytes": sum(item["total_bytes"] for item in packs),
        "error_count": len(errors),
        "errors": errors,
        "packs": packs,
    }
    output = args.catalog_output or vault / "metadata" / "kenney-3d-catalog.json"
    if not output.is_absolute():
        output = repo_root / output
    atomic_write_json(output, catalog)
    print(f"Catalog: {output}")
    print(
        f"Packs: {len(packs)}; model files: {catalog['model_file_count']}; "
        f"errors: {len(errors)}; bytes: {catalog['total_bytes']}"
    )
    return 0 if not errors else 2


if __name__ == "__main__":
    raise SystemExit(main())
