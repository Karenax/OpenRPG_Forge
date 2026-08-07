#!/usr/bin/env python3
"""Materialize per-entry vault metadata from an existing verified catalog."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import re

from download_cc0_3d import atomic_write_json, default_vault


def safe_name(value: str) -> str:
    return re.sub(r"[^a-zA-Z0-9._-]+", "-", value.split(":", 1)[-1]).strip("-")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("catalog", type=Path)
    parser.add_argument("--vault", type=Path)
    args = parser.parse_args()
    repo_root = Path(__file__).resolve().parents[2]
    catalog_path = args.catalog if args.catalog.is_absolute() else repo_root / args.catalog
    catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
    vault = (args.vault or default_vault(repo_root)).resolve()
    entries = catalog.get("assets") or catalog.get("packs") or []
    for entry in entries:
        external_id = entry.get("asset_id") or entry.get("pack_id")
        source = entry["source"]
        atomic_write_json(vault / "metadata" / source / f"{safe_name(external_id)}.json", entry)
    print(f"Metadata: {len(entries)} entries from {catalog_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
