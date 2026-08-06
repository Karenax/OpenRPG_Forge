#!/usr/bin/env python3
"""Plan catalog updates and safely uninstall acquired Asset Vault source content."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import sys
from typing import Any, Iterable

from download_cc0_3d import atomic_write_json, default_vault, file_digest


SCHEMA_VERSION = 1


def load_catalog(path: Path) -> dict[str, Any]:
    catalog = json.loads(path.read_text(encoding="utf-8"))
    if catalog.get("schema_version") != SCHEMA_VERSION:
        raise RuntimeError(f"Unsupported catalog schema in {path}")
    if not isinstance(catalog.get("assets") or catalog.get("packs") or [], list):
        raise RuntimeError(f"Catalog entries are invalid in {path}")
    return catalog


def catalog_entries(catalog: dict[str, Any]) -> list[dict[str, Any]]:
    return list(catalog.get("assets") or catalog.get("packs") or [])


def entry_id(entry: dict[str, Any]) -> str:
    value = entry.get("asset_id") or entry.get("pack_id")
    if not isinstance(value, str) or not value.strip():
        raise RuntimeError("Catalog entry has no stable asset or pack ID")
    return value.strip()


def entry_signature(entry: dict[str, Any]) -> str:
    source_files = []
    for item in entry.get("files") or []:
        source_files.append(
            {
                "url": item.get("url"),
                "source_md5": item.get("source_md5"),
                "sha256": item.get("sha256"),
                "size": item.get("size"),
            }
        )
    value = {
        "id": entry_id(entry),
        "source_updated_at": entry.get("source_updated_at"),
        "variant": entry.get("variant"),
        "files": sorted(source_files, key=lambda item: str(item.get("url"))),
    }
    payload = json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    return hashlib.sha256(payload.encode("utf-8")).hexdigest()


def plan_catalog_update(
    current: dict[str, Any],
    incoming: dict[str, Any],
) -> dict[str, Any]:
    current_by_id = {entry_id(item): item for item in catalog_entries(current)}
    incoming_by_id = {entry_id(item): item for item in catalog_entries(incoming)}
    current_ids = set(current_by_id)
    incoming_ids = set(incoming_by_id)
    added = sorted(incoming_ids - current_ids)
    removed = sorted(current_ids - incoming_ids)
    updated = sorted(
        identifier
        for identifier in current_ids & incoming_ids
        if entry_signature(current_by_id[identifier]) != entry_signature(incoming_by_id[identifier])
    )
    unchanged = sorted((current_ids & incoming_ids) - set(updated))
    fingerprint_source = json.dumps(
        {
            "added": added,
            "updated": updated,
            "removed": removed,
            "unchanged": unchanged,
        },
        sort_keys=True,
        separators=(",", ":"),
    )
    return {
        "schema_version": SCHEMA_VERSION,
        "plan_id": hashlib.sha256(fingerprint_source.encode("utf-8")).hexdigest(),
        "added": added,
        "updated": updated,
        "removed": removed,
        "unchanged": unchanged,
        "requires_refresh": added + updated,
    }


def safe_relative_path(value: str) -> Path:
    path = Path(value.replace("\\", "/"))
    if path.is_absolute() or not path.parts or ".." in path.parts:
        raise RuntimeError(f"Unsafe Asset Vault path: {value}")
    return path


def resolve_contained(vault: Path, value: str) -> tuple[Path, Path]:
    relative = safe_relative_path(value)
    root = vault.resolve()
    resolved = (root / relative).resolve()
    if resolved != root and root not in resolved.parents:
        raise RuntimeError(f"Asset Vault path escapes root: {value}")
    return relative, resolved


def directory_size(path: Path) -> int:
    return sum(item.stat().st_size for item in path.rglob("*") if item.is_file())


def path_fingerprint(path: Path) -> str:
    if path.is_file():
        return file_digest(path)
    values = []
    for item in sorted((candidate for candidate in path.rglob("*") if candidate.is_file())):
        values.append(f"{item.relative_to(path).as_posix()}:{file_digest(item)}")
    return hashlib.sha256("\n".join(values).encode("utf-8")).hexdigest()


def acquisition_roots(vault: Path, entry: dict[str, Any]) -> list[tuple[Path, Path]]:
    candidates: list[str] = []
    for item in entry.get("files") or []:
        value = item.get("path")
        if not isinstance(value, str):
            continue
        relative = safe_relative_path(value)
        if len(relative.parts) >= 3 and relative.parts[0].lower() == "originals":
            candidates.append(Path(*relative.parts[:3]).as_posix())
        else:
            candidates.append(relative.as_posix())
    extracted_root = entry.get("extracted_root")
    if isinstance(extracted_root, str) and extracted_root:
        candidates.append(safe_relative_path(extracted_root).as_posix())
    source = entry.get("source")
    source_asset_id = entry.get("source_asset_id")
    if not source_asset_id:
        source_asset_id = entry_id(entry).split(":", 1)[-1]
    if isinstance(source, str) and source and isinstance(source_asset_id, str) and source_asset_id:
        candidates.append(f"metadata/{source}/{source_asset_id}.json")

    roots = []
    for value in sorted(set(candidates)):
        relative, resolved = resolve_contained(vault, value)
        if resolved.exists():
            roots.append((relative, resolved))
    return roots


def plan_uninstall(vault: Path, catalog: dict[str, Any], asset_id: str) -> dict[str, Any]:
    matches = [item for item in catalog_entries(catalog) if entry_id(item).lower() == asset_id.lower()]
    if len(matches) != 1:
        raise RuntimeError(f"Catalog entry {asset_id!r} was not found exactly once")
    paths = []
    for relative, resolved in acquisition_roots(vault, matches[0]):
        paths.append(
            {
                "path": relative.as_posix(),
                "kind": "directory" if resolved.is_dir() else "file",
                "bytes": directory_size(resolved) if resolved.is_dir() else resolved.stat().st_size,
                "sha256": path_fingerprint(resolved),
            }
        )
    identifier = entry_id(matches[0])
    return {
        "schema_version": SCHEMA_VERSION,
        "plan_id": uninstall_plan_id(identifier, paths),
        "operation": "uninstall",
        "asset_id": identifier,
        "paths": paths,
        "total_bytes": sum(item["bytes"] for item in paths),
    }


def uninstall_plan_id(asset_id: str, paths: Iterable[dict[str, Any]]) -> str:
    source = json.dumps(
        {"asset_id": asset_id, "paths": list(paths)},
        sort_keys=True,
        separators=(",", ":"),
    )
    return hashlib.sha256(source.encode("utf-8")).hexdigest()


def apply_uninstall(vault: Path, plan: dict[str, Any]) -> dict[str, Any]:
    if plan.get("schema_version") != SCHEMA_VERSION or plan.get("operation") != "uninstall":
        raise RuntimeError("Invalid Asset Vault uninstall plan")
    expected_plan_id = uninstall_plan_id(plan.get("asset_id", ""), plan.get("paths") or [])
    if plan.get("plan_id") != expected_plan_id:
        raise RuntimeError("Asset Vault uninstall plan fingerprint is invalid")
    backup_root = vault / "cache" / "uninstalled" / plan["plan_id"]
    moved: list[tuple[Path, Path]] = []
    receipt_path: Path | None = None
    try:
        for item in plan["paths"]:
            relative, source = resolve_contained(vault, item["path"])
            if not source.exists() or path_fingerprint(source) != item["sha256"]:
                raise RuntimeError(f"Asset Vault source changed after planning: {item['path']}")
            backup = backup_root / relative
            backup.parent.mkdir(parents=True, exist_ok=True)
            if backup.exists():
                raise RuntimeError(f"Uninstall backup already exists: {backup}")
            shutil.move(str(source), str(backup))
            moved.append((source, backup))

        receipt = {
            **plan,
            "applied": True,
            "backup_root": backup_root.relative_to(vault).as_posix(),
        }
        receipt_path = vault / "metadata" / "receipts" / f"acquisition-uninstall-{plan['plan_id']}.json"
        atomic_write_json(receipt_path, receipt)
        update_uninstall_registry(vault, receipt)
        return {**receipt, "receipt": receipt_path.relative_to(vault).as_posix()}
    except Exception:
        if receipt_path and receipt_path.exists():
            receipt_path.unlink()
        for source, backup in reversed(moved):
            if backup.exists():
                source.parent.mkdir(parents=True, exist_ok=True)
                shutil.move(str(backup), str(source))
        raise


def update_uninstall_registry(vault: Path, receipt: dict[str, Any]) -> None:
    path = vault / "metadata" / "uninstalled-assets.v1.json"
    if path.exists():
        registry = json.loads(path.read_text(encoding="utf-8"))
        if registry.get("schema_version") != SCHEMA_VERSION:
            raise RuntimeError("Unsupported uninstalled asset registry schema")
    else:
        registry = {"schema_version": SCHEMA_VERSION, "assets": []}
    assets = [item for item in registry.get("assets") or [] if item.get("asset_id") != receipt["asset_id"]]
    assets.append(
        {
            "asset_id": receipt["asset_id"],
            "plan_id": receipt["plan_id"],
            "backup_root": receipt["backup_root"],
            "total_bytes": receipt["total_bytes"],
        }
    )
    registry["assets"] = sorted(assets, key=lambda item: item["asset_id"])
    atomic_write_json(path, registry)


def cache_report(vault: Path) -> dict[str, Any]:
    cache_root = vault / "cache" / "downloads" / "sha256"
    objects = [item for item in cache_root.rglob("*") if item.is_file()] if cache_root.exists() else []
    partials = [item for item in (vault / "originals").rglob("*.part")] if (vault / "originals").exists() else []
    registry_path = vault / "metadata" / "uninstalled-assets.v1.json"
    registry = json.loads(registry_path.read_text(encoding="utf-8")) if registry_path.exists() else {"assets": []}
    return {
        "schema_version": SCHEMA_VERSION,
        "cached_objects": len(objects),
        "cached_bytes": sum(item.stat().st_size for item in objects),
        "resumable_partials": len(partials),
        "resumable_bytes": sum(item.stat().st_size for item in partials),
        "uninstalled_assets": len(registry.get("assets") or []),
    }


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    update = subparsers.add_parser("update-plan")
    update.add_argument("--current", required=True, type=Path)
    update.add_argument("--incoming", required=True, type=Path)
    uninstall = subparsers.add_parser("uninstall")
    uninstall.add_argument("--vault", type=Path)
    uninstall.add_argument("--catalog", required=True, type=Path)
    uninstall.add_argument("--id", required=True)
    uninstall.add_argument("--apply", action="store_true")
    report = subparsers.add_parser("report")
    report.add_argument("--vault", type=Path)
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(sys.argv[1:] if argv is None else argv)
    repo_root = Path(__file__).resolve().parents[2]
    if args.command == "update-plan":
        result = plan_catalog_update(load_catalog(args.current), load_catalog(args.incoming))
        print(json.dumps(result, ensure_ascii=False, sort_keys=True))
        print(
            "OPENRPG_FORGE_ASSET_ACQUISITION_UPDATE_PLAN_PASS "
            f"added={len(result['added'])} updated={len(result['updated'])} "
            f"removed={len(result['removed'])} unchanged={len(result['unchanged'])}"
        )
        return 0

    vault = (args.vault or default_vault(repo_root)).resolve()
    if args.command == "uninstall":
        plan = plan_uninstall(vault, load_catalog(args.catalog), args.id)
        result = apply_uninstall(vault, plan) if args.apply else {**plan, "applied": False}
        print(json.dumps(result, ensure_ascii=False, sort_keys=True))
        print(
            f"OPENRPG_FORGE_ASSET_ACQUISITION_UNINSTALL_{'APPLY' if args.apply else 'PLAN'}_PASS "
            f"asset={plan['asset_id']} paths={len(plan['paths'])} bytes={plan['total_bytes']}"
        )
        return 0

    result = cache_report(vault)
    print(json.dumps(result, ensure_ascii=False, sort_keys=True))
    print(
        "OPENRPG_FORGE_ASSET_ACQUISITION_REPORT_PASS "
        f"objects={result['cached_objects']} partials={result['resumable_partials']} "
        f"uninstalled={result['uninstalled_assets']}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
