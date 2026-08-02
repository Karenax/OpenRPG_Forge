#!/usr/bin/env python3
"""Download reproducible CC0 3D source files into an external Asset Vault.

The repository keeps only manifests and tooling. Large immutable originals live in
the vault selected with --vault or OPENRPG_ASSET_VAULT.
"""

from __future__ import annotations

import argparse
import concurrent.futures
import hashlib
import json
import os
from pathlib import Path
import shutil
import sys
import tempfile
import threading
import time
from typing import Any, Iterable
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode
from urllib.request import Request, urlopen
import zipfile


USER_AGENT = "OpenRPG-Forge-AssetVault/0.1 (+https://github.com/Karenax/OpenRPG_Forge)"
POLYHAVEN_ASSETS_URL = "https://api.polyhaven.com/assets?t=models"
POLYHAVEN_FILES_URL = "https://api.polyhaven.com/files/{asset_id}"
AMBIENTCG_ASSETS_URL = "https://ambientcg.com/api/v3/assets"
CC0_ID = "CC0-1.0"
PRINT_LOCK = threading.Lock()


def request_bytes(url: str, attempts: int = 4) -> bytes:
    request = Request(url, headers={"User-Agent": USER_AGENT, "Referer": "https://github.com/Karenax/OpenRPG_Forge"})
    last_error: Exception | None = None
    for attempt in range(attempts):
        try:
            with urlopen(request, timeout=90) as response:
                return response.read()
        except (HTTPError, URLError, TimeoutError) as error:
            last_error = error
            if attempt + 1 < attempts:
                time.sleep(2**attempt)
    raise RuntimeError(f"Unable to download {url}: {last_error}")


def request_json(url: str) -> Any:
    return json.loads(request_bytes(url).decode("utf-8"))


def file_digest(path: Path, algorithm: str = "sha256") -> str:
    digest = hashlib.new(algorithm)
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def atomic_write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    text = json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    with tempfile.NamedTemporaryFile("w", encoding="utf-8", newline="\n", dir=path.parent, delete=False) as stream:
        stream.write(text)
        temporary = Path(stream.name)
    temporary.replace(path)


def relativize_vault_paths(value: Any, vault: Path) -> Any:
    prefix = vault.resolve().as_posix().rstrip("/") + "/"
    if isinstance(value, dict):
        return {key: relativize_vault_paths(item, vault) for key, item in value.items()}
    if isinstance(value, list):
        return [relativize_vault_paths(item, vault) for item in value]
    if isinstance(value, str) and value.startswith(prefix):
        return value[len(prefix) :]
    return value


def download_file(url: str, destination: Path, expected_md5: str | None = None) -> dict[str, Any]:
    destination.parent.mkdir(parents=True, exist_ok=True)
    if destination.exists() and (expected_md5 is None or file_digest(destination, "md5") == expected_md5):
        return describe_file(destination, url, expected_md5)

    payload = request_bytes(url)
    with tempfile.NamedTemporaryFile("wb", dir=destination.parent, delete=False) as stream:
        stream.write(payload)
        temporary = Path(stream.name)
    if expected_md5 and file_digest(temporary, "md5") != expected_md5:
        temporary.unlink(missing_ok=True)
        raise RuntimeError(f"MD5 mismatch for {url}")
    temporary.replace(destination)
    return describe_file(destination, url, expected_md5)


def describe_file(path: Path, url: str, expected_md5: str | None) -> dict[str, Any]:
    return {
        "path": path.as_posix(),
        "url": url,
        "size": path.stat().st_size,
        "sha256": file_digest(path),
        "source_md5": expected_md5,
    }


def safe_extract_zip(archive: Path, destination: Path) -> list[str]:
    destination.mkdir(parents=True, exist_ok=True)
    root = destination.resolve()
    extracted: list[str] = []
    with zipfile.ZipFile(archive) as bundle:
        members = bundle.infolist()
        if len(members) > 10_000:
            raise RuntimeError(f"Archive contains too many entries: {archive}")
        total = sum(member.file_size for member in members)
        if total > 8 * 1024 * 1024 * 1024:
            raise RuntimeError(f"Archive expands beyond the 8 GiB safety limit: {archive}")
        for member in members:
            target = (destination / member.filename).resolve()
            if root != target and root not in target.parents:
                raise RuntimeError(f"Unsafe archive path {member.filename!r} in {archive}")
            if member.is_dir():
                target.mkdir(parents=True, exist_ok=True)
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            with bundle.open(member) as source, target.open("wb") as output:
                shutil.copyfileobj(source, output)
            extracted.append(target.relative_to(destination).as_posix())
    return sorted(extracted)


def select_ids(records: Iterable[dict[str, Any]], max_assets: int | None) -> list[dict[str, Any]]:
    ordered = sorted(records, key=lambda item: str(item["id"]).lower())
    return ordered if max_assets is None else ordered[:max_assets]


def polyhaven_index(max_assets: int | None) -> list[dict[str, Any]]:
    payload = request_json(POLYHAVEN_ASSETS_URL)
    records = [
        {
            "id": asset_id,
            "title": metadata.get("name") or asset_id,
            "categories": sorted(metadata.get("categories") or []),
            "tags": sorted(metadata.get("tags") or []),
            "source_updated_at": metadata.get("date_published"),
        }
        for asset_id, metadata in payload.items()
    ]
    return select_ids(records, max_assets)


def ambientcg_index(max_assets: int | None) -> list[dict[str, Any]]:
    query = urlencode(
        {
            "type": "3d-model",
            "sort": "alphabet",
            "limit": 500,
            "include": "type,title,url,tags,downloads,thumbnails,releaseDate",
        }
    )
    payload = request_json(f"{AMBIENTCG_ASSETS_URL}?{query}")
    records = [
        {
            "id": item["id"],
            "title": item.get("title") or item["id"],
            "url": item.get("url"),
            "tags": sorted(item.get("tags") or []),
            "downloads": item.get("downloads") or [],
            "thumbnail": (item.get("thumbnails") or {}).get("256-WEBP"),
            "source_updated_at": item.get("releaseDate"),
        }
        for item in payload["assets"]
    ]
    return select_ids(records, max_assets)


def download_polyhaven_asset(vault: Path, record: dict[str, Any], resolution: str) -> dict[str, Any]:
    asset_id = record["id"]
    source_metadata = request_json(POLYHAVEN_FILES_URL.format(asset_id=asset_id))
    model_format = "gltf"
    variant = (((source_metadata.get(model_format) or {}).get(resolution) or {}).get(model_format))
    if not variant:
        model_format = "fbx"
        variant = (((source_metadata.get(model_format) or {}).get(resolution) or {}).get(model_format))
    if not variant:
        raise RuntimeError(f"No glTF or FBX {resolution} variant for Poly Haven asset {asset_id}")

    base = vault / "originals" / "polyhaven" / asset_id / f"{model_format}-{resolution}"
    downloads: list[dict[str, Any]] = []
    main_name = Path(variant["url"]).name
    downloads.append(download_file(variant["url"], base / main_name, variant.get("md5")))
    for relative_name, dependency in sorted((variant.get("include") or {}).items()):
        downloads.append(download_file(dependency["url"], base / relative_name, dependency.get("md5")))

    metadata = {
        "schema_version": 1,
        "asset_id": f"polyhaven:{asset_id}",
        "source": "polyhaven",
        "source_asset_id": asset_id,
        "name": record["title"],
        "source_page": f"https://polyhaven.com/a/{asset_id}",
        "catalog_date": time.strftime("%Y-%m-%d", time.gmtime()),
        "license": CC0_ID,
        "license_url": "https://polyhaven.com/license",
        "license_verified": True,
        "categories": record["categories"],
        "tags": record["tags"],
        "variant": f"{model_format}-{resolution}",
        "model_format": model_format,
        "files": downloads,
        "total_bytes": sum(item["size"] for item in downloads),
        "status": "downloaded-original",
    }
    metadata = relativize_vault_paths(metadata, vault)
    atomic_write_json(vault / "metadata" / "polyhaven" / f"{asset_id}.json", metadata)
    return metadata


def download_ambientcg_asset(vault: Path, record: dict[str, Any], quality: str) -> dict[str, Any]:
    asset_id = record["id"]
    variant = next((item for item in record["downloads"] if item.get("attributes") == quality), None)
    if not variant:
        raise RuntimeError(f"No {quality} variant for ambientCG asset {asset_id}")

    archive = vault / "originals" / "ambientcg" / asset_id / f"{asset_id}_{quality}.zip"
    downloaded = download_file(variant["url"], archive)
    extracted_root = vault / "normalized" / "ambientcg" / asset_id / quality.lower()
    extracted = safe_extract_zip(archive, extracted_root)
    model_files = [name for name in extracted if Path(name).suffix.lower() in {".gltf", ".glb", ".fbx", ".obj"}]
    if not model_files:
        raise RuntimeError(f"No supported 3D model found in {archive}")

    metadata = {
        "schema_version": 1,
        "asset_id": f"ambientcg:{asset_id}",
        "source": "ambientcg",
        "source_asset_id": asset_id,
        "name": record["title"],
        "source_page": record["url"],
        "catalog_date": time.strftime("%Y-%m-%d", time.gmtime()),
        "license": CC0_ID,
        "license_url": "https://docs.ambientcg.com/license/",
        "license_verified": True,
        "categories": ["3d-model"],
        "tags": record["tags"],
        "thumbnail_url": record["thumbnail"],
        "variant": quality,
        "files": [downloaded],
        "extracted_root": extracted_root.as_posix(),
        "extracted_files": extracted,
        "model_files": model_files,
        "total_bytes": downloaded["size"],
        "status": "downloaded-and-extracted",
    }
    metadata = relativize_vault_paths(metadata, vault)
    atomic_write_json(vault / "metadata" / "ambientcg" / f"{asset_id}.json", metadata)
    return metadata


def run_parallel(label: str, records: list[dict[str, Any]], worker: Any, jobs: int) -> tuple[list[dict[str, Any]], list[dict[str, str]]]:
    completed: list[dict[str, Any]] = []
    errors: list[dict[str, str]] = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=jobs) as executor:
        futures = {executor.submit(worker, record): record for record in records}
        for index, future in enumerate(concurrent.futures.as_completed(futures), start=1):
            record = futures[future]
            try:
                completed.append(future.result())
                message = f"[{label} {index}/{len(records)}] {record['id']}"
            except Exception as error:  # keep the bulk operation progressing
                errors.append({"asset_id": record["id"], "error": str(error)})
                message = f"[{label} ERROR {index}/{len(records)}] {record['id']}: {error}"
            with PRINT_LOCK:
                print(message, flush=True)
    return completed, errors


def default_vault(repo_root: Path) -> Path:
    configured = os.environ.get("OPENRPG_ASSET_VAULT")
    return Path(configured).expanduser() if configured else repo_root.parent / "OpenRPG_AssetVault"


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--vault", type=Path, help="External Asset Vault path")
    parser.add_argument("--source", choices=("all", "polyhaven", "ambientcg"), default="all")
    parser.add_argument("--max-assets", type=int, help="Maximum number per selected source")
    parser.add_argument("--jobs", type=int, default=4)
    parser.add_argument("--polyhaven-resolution", choices=("1k", "2k", "4k"), default="1k")
    parser.add_argument("--ambientcg-quality", default="LQ-1K-JPG")
    parser.add_argument("--catalog-output", type=Path)
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(sys.argv[1:] if argv is None else argv)
    repo_root = Path(__file__).resolve().parents[2]
    vault = (args.vault or default_vault(repo_root)).resolve()
    if args.max_assets is not None and args.max_assets < 1:
        raise SystemExit("--max-assets must be positive")
    if not 1 <= args.jobs <= 16:
        raise SystemExit("--jobs must be between 1 and 16")

    for folder in ("originals", "normalized", "metadata", "licenses", "logs", "quarantine"):
        (vault / folder).mkdir(parents=True, exist_ok=True)

    assets: list[dict[str, Any]] = []
    errors: list[dict[str, str]] = []
    if args.source in {"all", "polyhaven"}:
        records = polyhaven_index(args.max_assets)
        result, failures = run_parallel(
            "PolyHaven",
            records,
            lambda record: download_polyhaven_asset(vault, record, args.polyhaven_resolution),
            args.jobs,
        )
        assets.extend(result)
        errors.extend({"source": "polyhaven", **item} for item in failures)
    if args.source in {"all", "ambientcg"}:
        records = ambientcg_index(args.max_assets)
        result, failures = run_parallel(
            "ambientCG",
            records,
            lambda record: download_ambientcg_asset(vault, record, args.ambientcg_quality),
            args.jobs,
        )
        assets.extend(result)
        errors.extend({"source": "ambientcg", **item} for item in failures)

    assets.sort(key=lambda item: item["asset_id"])
    catalog = {
        "schema_version": 1,
        "catalog_date": time.strftime("%Y-%m-%d", time.gmtime()),
        "vault_root": "${OPENRPG_ASSET_VAULT}",
        "license_policy": "cc0-only",
        "asset_count": len(assets),
        "total_bytes": sum(item["total_bytes"] for item in assets),
        "error_count": len(errors),
        "errors": errors,
        "assets": assets,
    }
    catalog_output = args.catalog_output or vault / "metadata" / "catalog.json"
    if not catalog_output.is_absolute():
        catalog_output = repo_root / catalog_output
    atomic_write_json(catalog_output, catalog)
    atomic_write_json(vault / "logs" / "latest-download.json", catalog)
    print(f"Catalog: {catalog_output}")
    print(f"Assets: {len(assets)}; errors: {len(errors)}; bytes: {catalog['total_bytes']}")
    return 0 if not errors else 2


if __name__ == "__main__":
    raise SystemExit(main())
