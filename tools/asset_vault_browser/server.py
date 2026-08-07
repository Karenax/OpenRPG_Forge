#!/usr/bin/env python3
"""Local, read-only browser for the OpenRPG Asset Vault."""

from __future__ import annotations

import argparse
import hashlib
import json
import mimetypes
import os
import re
import threading
import urllib.parse
import webbrowser
from collections import Counter
from dataclasses import dataclass, field
from datetime import datetime, timezone
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path, PurePosixPath
from typing import Any, Iterable


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
STATIC_ROOT = Path(__file__).resolve().parent / "static"
DEFAULT_MAIN_CATALOG = REPOSITORY_ROOT / "asset_manifests" / "catalogs" / "cc0_3d_all.json"
DEFAULT_KENNEY_CATALOG = REPOSITORY_ROOT / "asset_manifests" / "catalogs" / "kenney_3d_packs.json"
DEFAULT_PACK_CATALOGS = tuple(
    path for path in sorted((REPOSITORY_ROOT / "asset_manifests" / "catalogs").glob("*.json"))
    if path not in {DEFAULT_MAIN_CATALOG, DEFAULT_KENNEY_CATALOG}
)
MODEL_EXTENSIONS = {".glb", ".gltf", ".obj", ".fbx"}
FORMAT_PRIORITY = {"glb": 0, "gltf": 1, "fbx": 2, "obj": 3}
VIEWER_FORMATS = {"glb", "gltf"}
MAX_REQUEST_BYTES = 1_000_000


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def default_vault_root() -> Path:
    configured = os.environ.get("OPENRPG_ASSET_VAULT")
    return Path(configured) if configured else Path(r"E:\OpenRPG_AssetVault")


def default_selection_path() -> Path:
    base = Path(os.environ.get("LOCALAPPDATA", str(Path.home())))
    return base / "OpenRPG Forge" / "Asset Vault Browser" / "selection.v1.json"


def slug(value: str) -> str:
    normalized = re.sub(r"[^a-z0-9]+", "-", value.casefold()).strip("-")
    return normalized or "model"


def read_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        value = json.load(handle)
    if not isinstance(value, dict):
        raise ValueError(f"Expected a JSON object in {path}")
    return value


def relative_url(prefix: str, relative_path: str) -> str:
    clean = str(PurePosixPath(relative_path.replace("\\", "/")))
    return f"{prefix}/{urllib.parse.quote(clean, safe='/')}"


def safe_vault_path(vault_root: Path, relative_path: str) -> Path:
    relative = PurePosixPath(urllib.parse.unquote(relative_path))
    if relative.is_absolute() or ".." in relative.parts:
        raise ValueError("Unsafe Vault path")
    root = vault_root.resolve()
    candidate = root.joinpath(*relative.parts).resolve()
    if candidate != root and root not in candidate.parents:
        raise ValueError("Vault path escapes configured root")
    return candidate


@dataclass(slots=True)
class AssetItem:
    asset_id: str
    source: str
    name: str
    pack_id: str | None
    pack_name: str | None
    kind: str
    format: str
    model_relative_path: str
    preview_url: str | None
    source_page: str | None
    license: str
    tags: list[str] = field(default_factory=list)
    categories: list[str] = field(default_factory=list)
    variants: list[dict[str, str]] = field(default_factory=list)
    file_size: int = 0
    logical_bytes: int = 0
    search_text: str = ""

    @property
    def model_url(self) -> str:
        return relative_url("/vault", self.model_relative_path)

    @property
    def viewer_supported(self) -> bool:
        return self.format in VIEWER_FORMATS and self.kind != "animation"

    def summary(self) -> dict[str, Any]:
        return {
            "id": self.asset_id,
            "source": self.source,
            "name": self.name,
            "packId": self.pack_id,
            "packName": self.pack_name,
            "kind": self.kind,
            "format": self.format,
            "modelUrl": self.model_url,
            "modelRelativePath": self.model_relative_path,
            "previewUrl": self.preview_url,
            "sourcePage": self.source_page,
            "license": self.license,
            "tags": self.tags,
            "categories": self.categories,
            "variants": self.variants,
            "fileSize": self.file_size,
            "logicalBytes": self.logical_bytes,
            "viewerSupported": self.viewer_supported,
        }


class CatalogIndex:
    def __init__(
        self,
        vault_root: Path,
        main_catalog: Path,
        kenney_catalog: Path,
        pack_catalogs: Iterable[Path] = (),
    ) -> None:
        self.vault_root = vault_root.resolve()
        self.main_catalog_path = main_catalog.resolve()
        self.kenney_catalog_path = kenney_catalog.resolve()
        self.pack_catalog_paths = tuple(path.resolve() for path in pack_catalogs)
        self.items: list[AssetItem] = []
        self.by_id: dict[str, AssetItem] = {}
        self.catalog_entry_count = 0
        self.scanned_at = utc_now()
        self._load()

    def _load(self) -> None:
        if not self.vault_root.is_dir():
            raise FileNotFoundError(f"Asset Vault not found: {self.vault_root}")
        main = read_json(self.main_catalog_path)
        main_assets = main.get("assets", [])
        if not isinstance(main_assets, list):
            raise ValueError("Catalog asset collections must be arrays")

        for entry in main_assets:
            if isinstance(entry, dict):
                item = self._main_item(entry)
                if item is not None:
                    self._add(item)
        extra_asset_count = 0
        pack_count = 0
        for catalog_path in (self.kenney_catalog_path, *self.pack_catalog_paths):
            catalog = read_json(catalog_path)
            extra_assets = catalog.get("assets", [])
            packs = catalog.get("packs", [])
            if not isinstance(extra_assets, list) or not isinstance(packs, list):
                raise ValueError(f"Catalog asset collections must be arrays: {catalog_path}")
            extra_asset_count += len(extra_assets)
            for entry in extra_assets:
                if isinstance(entry, dict):
                    item = self._main_item(entry)
                    if item is not None:
                        self._add(item)
            pack_count += len(packs)
            for pack in packs:
                if isinstance(pack, dict):
                    for item in self._pack_items(pack):
                        self._add(item)
        self.catalog_entry_count = len(main_assets) + extra_asset_count + pack_count
        self.items.sort(key=lambda item: (item.source.casefold(), (item.pack_name or "").casefold(), item.name.casefold(), item.asset_id))

    def _add(self, item: AssetItem) -> None:
        if item.asset_id in self.by_id:
            digest = hashlib.sha1(item.model_relative_path.encode("utf-8"), usedforsecurity=False).hexdigest()[:8]
            item.asset_id = f"{item.asset_id}--{digest}"
        item.search_text = " ".join(
            [
                item.asset_id,
                item.source,
                item.name,
                item.pack_id or "",
                item.pack_name or "",
                item.kind,
                item.format,
                *item.tags,
                *item.categories,
            ]
        ).casefold()
        self.by_id[item.asset_id] = item
        self.items.append(item)

    def _main_item(self, entry: dict[str, Any]) -> AssetItem | None:
        source = str(entry.get("source", "")).strip()
        asset_id = str(entry.get("asset_id", "")).strip()
        if source not in {"polyhaven", "ambientcg"} or not asset_id:
            return None

        kind = str(entry.get("kind", "model")).strip().casefold()
        candidates: list[tuple[str, str, int]] = []
        if kind in {"material", "hdri"}:
            primary = str(entry.get("primary_file", "")).replace("\\", "/")
            matching = next(
                (value for value in entry.get("files", []) if isinstance(value, dict) and value.get("path") == primary),
                {},
            )
            if primary:
                candidates.append((primary, PurePosixPath(primary).suffix.casefold().removeprefix("."), int(matching.get("size", 0) or 0)))
        elif source == "polyhaven":
            for file_info in entry.get("files", []):
                if not isinstance(file_info, dict):
                    continue
                relative = str(file_info.get("path", "")).replace("\\", "/")
                extension = PurePosixPath(relative).suffix.casefold()
                if extension in MODEL_EXTENSIONS:
                    candidates.append((relative, extension[1:], int(file_info.get("size", 0) or 0)))
        else:
            extracted_root = str(entry.get("extracted_root", "")).replace("\\", "/")
            for model_file in entry.get("model_files", []):
                relative = str(PurePosixPath(extracted_root) / str(model_file).replace("\\", "/"))
                extension = PurePosixPath(relative).suffix.casefold()
                if extension in MODEL_EXTENSIONS:
                    full_path = safe_vault_path(self.vault_root, relative)
                    candidates.append((relative, extension[1:], full_path.stat().st_size if full_path.is_file() else 0))
        if not candidates:
            return None

        candidates.sort(key=lambda value: (FORMAT_PRIORITY.get(value[1], 99), value[0].casefold()))
        relative, selected_format, file_size = candidates[0]
        source_asset_id = str(entry.get("source_asset_id", asset_id.split(":", 1)[-1]))
        local_preview = str(entry.get("preview_file", "")).replace("\\", "/")
        if local_preview and safe_vault_path(self.vault_root, local_preview).is_file():
            preview_url = relative_url("/vault", local_preview)
            source_label = "Poly Haven" if source == "polyhaven" else "ambientCG"
        elif source == "ambientcg":
            extracted_root = str(entry.get("extracted_root", "")).replace("\\", "/")
            local_preview = str(PurePosixPath(extracted_root) / f"{source_asset_id}.png")
            preview_url = relative_url("/vault", local_preview) if safe_vault_path(self.vault_root, local_preview).is_file() else entry.get("thumbnail_url")
            source_label = "ambientCG"
        else:
            preview_url = entry.get("thumbnail_url") or f"https://cdn.polyhaven.com/asset_img/thumbs/{urllib.parse.quote(source_asset_id)}.png?width=320&height=240"
            source_label = "Poly Haven"

        variants = [{"format": model_format, "path": path} for path, model_format, _ in candidates]
        return AssetItem(
            asset_id=asset_id,
            source=source_label,
            name=str(entry.get("name", source_asset_id)),
            pack_id=None,
            pack_name=None,
            kind=kind,
            format=selected_format,
            model_relative_path=relative,
            preview_url=str(preview_url) if preview_url else None,
            source_page=str(entry.get("source_page")) if entry.get("source_page") else None,
            license=str(entry.get("license", "unknown")),
            tags=[str(value) for value in entry.get("tags", [])],
            categories=[str(value) for value in entry.get("categories", [])],
            variants=variants,
            file_size=file_size,
            logical_bytes=int(entry.get("total_bytes", 0) or 0),
        )

    def _pack_items(self, pack: dict[str, Any]) -> Iterable[AssetItem]:
        pack_id = str(pack.get("pack_id", "")).strip()
        if not pack_id:
            return []
        pack_slug = pack_id.split(":", 1)[-1]
        pack_name = str(pack.get("name", pack_slug))
        source_id = str(pack.get("source", pack_id.split(":", 1)[0])).strip().casefold()
        source_label = {
            "ambientcg": "ambientCG",
            "kaykit": "KayKit",
            "kenney": "Kenney",
            "quaternius": "Quaternius",
        }.get(source_id, source_id.title())
        extracted_root = str(pack.get("extracted_root", f"normalized/{source_id}/{pack_slug}")).replace("\\", "/")
        pack_categories = [str(value) for value in pack.get("categories", [])]
        pack_tags = [str(value) for value in pack.get("tags", [])]
        grouped: dict[str, list[tuple[str, str, str]]] = {}
        for value in pack.get("model_files", []):
            model_file = str(value).replace("\\", "/")
            path = PurePosixPath(model_file)
            extension = path.suffix.casefold()
            if extension not in MODEL_EXTENSIONS:
                continue
            logical_name = path.stem
            grouped.setdefault(logical_name.casefold(), []).append((model_file, extension[1:], logical_name))

        preview_names: dict[str, Path] = {}
        preview_candidates = [str(value).replace("\\", "/") for value in pack.get("preview_files", [])]
        preview_directory = safe_vault_path(self.vault_root, str(PurePosixPath(extracted_root) / "Previews"))
        if preview_directory.is_dir():
            preview_candidates.extend(
                preview.relative_to(safe_vault_path(self.vault_root, extracted_root)).as_posix()
                for preview in preview_directory.iterdir()
                if preview.is_file() and preview.suffix.casefold() in {".png", ".jpg", ".jpeg", ".webp"}
            )
        for value in preview_candidates:
            preview = safe_vault_path(self.vault_root, str(PurePosixPath(extracted_root) / value))
            if preview.is_file():
                preview_names[preview.stem.casefold()] = preview
        fallback_preview = None
        for preview in preview_candidates:
            relative = str(PurePosixPath(extracted_root) / preview)
            if safe_vault_path(self.vault_root, relative).is_file():
                fallback_preview = relative_url("/vault", relative)
                break
        for name in ("Preview.png", "Sample.png"):
            relative = str(PurePosixPath(extracted_root) / name)
            if safe_vault_path(self.vault_root, relative).is_file():
                fallback_preview = relative_url("/vault", relative)
                break

        items: list[AssetItem] = []
        for logical_key, variants_data in grouped.items():
            variants_data.sort(key=lambda value: (FORMAT_PRIORITY.get(value[1], 99), value[0].casefold()))
            selected_file, selected_format, logical_name = variants_data[0]
            relative = str(PurePosixPath(extracted_root) / selected_file)
            selected_path = safe_vault_path(self.vault_root, relative)
            sidecar_relative = str(PurePosixPath(relative).with_name(f"{PurePosixPath(relative).stem}.preview.png"))
            sidecar_path = safe_vault_path(self.vault_root, sidecar_relative)
            is_animation = (
                "animation library" in pack_name.casefold()
                or any(part.casefold().startswith("animation") for part in PurePosixPath(selected_file).parts)
            )
            preview_path = preview_names.get(logical_key)
            if sidecar_path.is_file():
                preview_url = relative_url("/vault", sidecar_relative)
            elif preview_path is not None:
                preview_relative = preview_path.relative_to(self.vault_root).as_posix()
                preview_url = relative_url("/vault", preview_relative)
            else:
                preview_url = fallback_preview
            items.append(
                AssetItem(
                    asset_id=f"{pack_id}:{slug(logical_name)}",
                    source=source_label,
                    name=logical_name.replace("_", " ").replace("-", " ").strip().title(),
                    pack_id=pack_id,
                    pack_name=pack_name,
                    kind="animation" if is_animation else "model",
                    format=selected_format,
                    model_relative_path=relative,
                    preview_url=preview_url,
                    source_page=str(pack.get("source_page")) if pack.get("source_page") else None,
                    license=str(pack.get("license", "unknown")),
                    tags=list(dict.fromkeys([*pack_tags, pack_slug, logical_name])),
                    categories=list(dict.fromkeys([*pack_categories, "Animations" if is_animation else "3d-model"])),
                    variants=[{"format": model_format, "path": str(PurePosixPath(extracted_root) / model_path)} for model_path, model_format, _ in variants_data],
                    file_size=selected_path.stat().st_size if selected_path.is_file() else 0,
                    logical_bytes=int(pack.get("total_bytes", 0) or 0),
                )
            )
        return items

    def metadata(self) -> dict[str, Any]:
        sources = Counter(item.source for item in self.items)
        formats = Counter(item.format for item in self.items)
        packs = Counter(item.pack_id for item in self.items if item.pack_id)
        return {
            "schemaVersion": 1,
            "scannedAt": self.scanned_at,
            "catalogEntryCount": self.catalog_entry_count,
            "logicalModelCount": len(self.items),
            "viewerReadyCount": sum(1 for item in self.items if item.viewer_supported),
            "sourceCounts": dict(sorted(sources.items())),
            "formatCounts": dict(sorted(formats.items())),
            "packs": [
                {
                    "id": pack_id,
                    "name": next(item.pack_name for item in self.items if item.pack_id == pack_id),
                    "count": count,
                }
                for pack_id, count in sorted(packs.items(), key=lambda pair: pair[0] or "")
            ],
        }

    def query(
        self,
        query: str = "",
        source: str = "",
        pack_id: str = "",
        model_format: str = "",
        kind: str = "",
        selected_ids: set[str] | None = None,
        selected_only: bool = False,
        offset: int = 0,
        limit: int = 96,
    ) -> dict[str, Any]:
        terms = [term.casefold() for term in query.split() if term.strip()]
        selected_ids = selected_ids or set()

        def matches(item: AssetItem) -> bool:
            if source and item.source.casefold() != source.casefold():
                return False
            if pack_id and item.pack_id != pack_id:
                return False
            if model_format and item.format != model_format.casefold():
                return False
            if kind and item.kind != kind.casefold():
                return False
            if selected_only and item.asset_id not in selected_ids:
                return False
            return all(term in item.search_text for term in terms)

        filtered = [item for item in self.items if matches(item)]
        page = filtered[offset : offset + limit]
        return {
            "schemaVersion": 1,
            "total": len(filtered),
            "offset": offset,
            "limit": limit,
            "items": [item.summary() | {"selected": item.asset_id in selected_ids} for item in page],
        }


class SelectionStore:
    def __init__(self, path: Path, index: CatalogIndex) -> None:
        self.path = path.resolve()
        self.index = index
        self._lock = threading.Lock()
        self._asset_ids: list[str] = []
        self._note = ""
        self._updated_at: str | None = None
        self._load()

    def _load(self) -> None:
        if not self.path.is_file():
            return
        try:
            payload = read_json(self.path)
            ids = payload.get("assetIds", [])
            if isinstance(ids, list):
                self._asset_ids = [str(value) for value in ids if str(value) in self.index.by_id]
            self._note = str(payload.get("note", ""))[:4000]
            self._updated_at = str(payload.get("updatedAt")) if payload.get("updatedAt") else None
        except (OSError, ValueError, json.JSONDecodeError):
            self._asset_ids = []
            self._note = ""
            self._updated_at = None

    @property
    def ids(self) -> set[str]:
        with self._lock:
            return set(self._asset_ids)

    def payload(self) -> dict[str, Any]:
        with self._lock:
            entries = [self.index.by_id[value].summary() for value in self._asset_ids if value in self.index.by_id]
            return {
                "schemaVersion": 1,
                "updatedAt": self._updated_at,
                "note": self._note,
                "assetIds": list(self._asset_ids),
                "entries": entries,
                "storagePath": str(self.path),
            }

    def save(self, asset_ids: list[str], note: str) -> dict[str, Any]:
        invalid = [value for value in asset_ids if value not in self.index.by_id]
        if invalid:
            raise ValueError(f"Unknown asset IDs: {', '.join(invalid[:5])}")
        unique = list(dict.fromkeys(asset_ids))
        if len(unique) > len(self.index.items):
            raise ValueError("Selection exceeds catalog size")
        with self._lock:
            self._asset_ids = unique
            self._note = note[:4000]
            self._updated_at = utc_now()
            self.path.parent.mkdir(parents=True, exist_ok=True)
            payload = {
                "schemaVersion": 1,
                "updatedAt": self._updated_at,
                "note": self._note,
                "assetIds": self._asset_ids,
                "entries": [
                    {
                        "id": self.index.by_id[value].asset_id,
                        "source": self.index.by_id[value].source,
                        "packId": self.index.by_id[value].pack_id,
                        "name": self.index.by_id[value].name,
                        "format": self.index.by_id[value].format,
                        "modelRelativePath": self.index.by_id[value].model_relative_path,
                    }
                    for value in self._asset_ids
                ],
            }
            temporary = self.path.with_suffix(self.path.suffix + ".tmp")
            temporary.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
            os.replace(temporary, self.path)
        return self.payload()


class AssetBrowserServer(ThreadingHTTPServer):
    daemon_threads = True

    def __init__(self, address: tuple[str, int], index: CatalogIndex, selection: SelectionStore) -> None:
        super().__init__(address, AssetBrowserHandler)
        self.index = index
        self.selection = selection
        self.static_root = STATIC_ROOT.resolve()
        self.vault_root = index.vault_root


class AssetBrowserHandler(BaseHTTPRequestHandler):
    server: AssetBrowserServer

    def log_message(self, format_string: str, *args: object) -> None:
        print(f"[{self.log_date_time_string()}] {format_string % args}")

    def end_headers(self) -> None:
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Referrer-Policy", "no-referrer")
        self.send_header(
            "Content-Security-Policy",
            "default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; "
            "img-src 'self' https: data: blob:; connect-src 'self'; worker-src 'self' blob:; "
            "font-src 'self' data:; frame-ancestors 'none'",
        )
        super().end_headers()

    def do_GET(self) -> None:  # noqa: N802 - BaseHTTPRequestHandler contract
        parsed = urllib.parse.urlparse(self.path)
        if parsed.path == "/api/meta":
            self._send_json(self.server.index.metadata())
            return
        if parsed.path == "/api/assets":
            params = urllib.parse.parse_qs(parsed.query)
            try:
                offset = max(0, int(params.get("offset", ["0"])[0]))
                limit = min(192, max(1, int(params.get("limit", ["96"])[0])))
            except ValueError:
                self._send_error(HTTPStatus.BAD_REQUEST, "offset and limit must be integers")
                return
            payload = self.server.index.query(
                query=params.get("q", [""])[0],
                source=params.get("source", [""])[0],
                pack_id=params.get("pack", [""])[0],
                model_format=params.get("format", [""])[0],
                kind=params.get("kind", [""])[0],
                selected_ids=self.server.selection.ids,
                selected_only=params.get("selected", ["false"])[0].casefold() == "true",
                offset=offset,
                limit=limit,
            )
            self._send_json(payload)
            return
        if parsed.path == "/api/asset":
            asset_id = urllib.parse.parse_qs(parsed.query).get("id", [""])[0]
            item = self.server.index.by_id.get(asset_id)
            if item is None:
                self._send_error(HTTPStatus.NOT_FOUND, "Asset not found")
            else:
                self._send_json(item.summary() | {"selected": asset_id in self.server.selection.ids})
            return
        if parsed.path in {"/api/selection", "/api/export"}:
            self._send_json(self.server.selection.payload(), download=parsed.path == "/api/export")
            return
        if parsed.path.startswith("/vault/"):
            try:
                path = safe_vault_path(self.server.vault_root, parsed.path.removeprefix("/vault/"))
            except ValueError as error:
                self._send_error(HTTPStatus.BAD_REQUEST, str(error))
                return
            self._send_file(path, cache=True)
            return

        static_relative = "index.html" if parsed.path == "/" else parsed.path.removeprefix("/")
        candidate = (self.server.static_root / static_relative).resolve()
        if candidate != self.server.static_root and self.server.static_root not in candidate.parents:
            self._send_error(HTTPStatus.BAD_REQUEST, "Unsafe static path")
            return
        self._send_file(candidate, cache=static_relative != "index.html")

    def do_POST(self) -> None:  # noqa: N802 - BaseHTTPRequestHandler contract
        parsed = urllib.parse.urlparse(self.path)
        if parsed.path != "/api/selection":
            self._send_error(HTTPStatus.NOT_FOUND, "Endpoint not found")
            return
        try:
            length = int(self.headers.get("Content-Length", "0"))
        except ValueError:
            self._send_error(HTTPStatus.BAD_REQUEST, "Invalid Content-Length")
            return
        if length <= 0 or length > MAX_REQUEST_BYTES:
            self._send_error(HTTPStatus.BAD_REQUEST, "Invalid request size")
            return
        try:
            payload = json.loads(self.rfile.read(length).decode("utf-8"))
            asset_ids = payload.get("assetIds", [])
            note = payload.get("note", "")
            if not isinstance(asset_ids, list) or not all(isinstance(value, str) for value in asset_ids):
                raise ValueError("assetIds must be an array of strings")
            if not isinstance(note, str):
                raise ValueError("note must be a string")
            result = self.server.selection.save(asset_ids, note)
        except (UnicodeDecodeError, json.JSONDecodeError, ValueError) as error:
            self._send_error(HTTPStatus.BAD_REQUEST, str(error))
            return
        self._send_json(result)

    def _send_json(self, payload: dict[str, Any], *, download: bool = False) -> None:
        body = json.dumps(payload, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
        self.send_response(HTTPStatus.OK)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        if download:
            self.send_header("Content-Disposition", 'attachment; filename="openrpg-asset-selection.json"')
        self.end_headers()
        self.wfile.write(body)

    def _send_error(self, status: HTTPStatus, message: str) -> None:
        body = json.dumps({"error": message}, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(body)

    def _send_file(self, path: Path, *, cache: bool) -> None:
        if not path.is_file():
            self._send_error(HTTPStatus.NOT_FOUND, "File not found")
            return
        size = path.stat().st_size
        start = 0
        end = size - 1
        status = HTTPStatus.OK
        range_header = self.headers.get("Range")
        if range_header:
            match = re.fullmatch(r"bytes=(\d*)-(\d*)", range_header.strip())
            if not match:
                self.send_response(HTTPStatus.REQUESTED_RANGE_NOT_SATISFIABLE)
                self.send_header("Content-Range", f"bytes */{size}")
                self.end_headers()
                return
            first, last = match.groups()
            if first:
                start = int(first)
                end = int(last) if last else end
            elif last:
                suffix_length = int(last)
                start = max(0, size - suffix_length)
            if start >= size or end < start:
                self.send_response(HTTPStatus.REQUESTED_RANGE_NOT_SATISFIABLE)
                self.send_header("Content-Range", f"bytes */{size}")
                self.end_headers()
                return
            end = min(end, size - 1)
            status = HTTPStatus.PARTIAL_CONTENT

        content_type = mimetypes.guess_type(path.name)[0] or "application/octet-stream"
        if path.suffix.casefold() == ".glb":
            content_type = "model/gltf-binary"
        elif path.suffix.casefold() == ".gltf":
            content_type = "model/gltf+json"
        length = end - start + 1
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(length))
        self.send_header("Accept-Ranges", "bytes")
        self.send_header("Cache-Control", "public, max-age=86400" if cache else "no-store")
        if status == HTTPStatus.PARTIAL_CONTENT:
            self.send_header("Content-Range", f"bytes {start}-{end}/{size}")
        self.end_headers()
        with path.open("rb") as handle:
            handle.seek(start)
            remaining = length
            while remaining:
                chunk = handle.read(min(1024 * 1024, remaining))
                if not chunk:
                    break
                self.wfile.write(chunk)
                remaining -= len(chunk)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Browse and curate the OpenRPG Asset Vault locally.")
    parser.add_argument("--vault", type=Path, default=default_vault_root())
    parser.add_argument("--catalog", type=Path, default=DEFAULT_MAIN_CATALOG)
    parser.add_argument("--kenney-catalog", type=Path, default=DEFAULT_KENNEY_CATALOG)
    parser.add_argument("--pack-catalog", type=Path, action="append", default=list(DEFAULT_PACK_CATALOGS))
    parser.add_argument("--selection", type=Path, default=default_selection_path())
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--open", action="store_true", help="Open the browser after startup")
    parser.add_argument("--scan-only", action="store_true", help="Print catalog metadata and exit")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if args.host not in {"127.0.0.1", "localhost"}:
        raise SystemExit("Asset Vault Browser only binds to the local loopback interface.")
    index = CatalogIndex(args.vault, args.catalog, args.kenney_catalog, args.pack_catalog)
    if args.scan_only:
        print(json.dumps(index.metadata(), ensure_ascii=False, indent=2))
        return 0
    selection = SelectionStore(args.selection, index)
    server = AssetBrowserServer((args.host, args.port), index, selection)
    host, port = server.server_address
    url = f"http://{host}:{port}/"
    print(f"OPENRPG_ASSET_BROWSER_READY url={url} models={len(index.items)} selection={selection.path}")
    if args.open:
        timer = threading.Timer(0.35, lambda: webbrowser.open(url))
        timer.daemon = True
        timer.start()
    try:
        server.serve_forever(poll_interval=0.25)
    except KeyboardInterrupt:
        print("\nAsset Vault Browser stopped.")
    finally:
        server.server_close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
