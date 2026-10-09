"""Inventory all active Vault assets and prepare a detached client-compatible transfer.

Normalized payloads and their authoritative metadata are delivered together.
Original archives remain inventoried source material, never silently called runtime assets.
No files in the Vault are modified. --pack streams directly without a second library copy.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import os
import stat
import time
from pathlib import Path
import subprocess


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def inventory(root, folder):
    entries = []
    stack = [folder]
    updated = time.monotonic()
    print(f"Inventory: {folder.name}", flush=True)
    while stack:
        current = stack.pop()
        with os.scandir(current) as listing:
            for entry in listing:
                metadata = entry.stat(follow_symlinks=False)
                path = Path(entry.path)
                if stat.S_ISLNK(metadata.st_mode) or getattr(metadata, "st_file_attributes", 0) & 0x400:
                    raise ValueError(f"Vault link/reparse point refused: {path}")
                if stat.S_ISDIR(metadata.st_mode):
                    stack.append(path)
                elif stat.S_ISREG(metadata.st_mode):
                    entries.append(dict(path=path.relative_to(root).as_posix(), bytes=metadata.st_size))
        if time.monotonic() - updated > 30:
            print(f"{folder.name}: {len(entries)} files inventoried", flush=True)
            updated = time.monotonic()
    return sorted(entries, key=lambda entry: entry["path"])


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--vault", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--delivery-cli", type=Path)
    parser.add_argument("--pack", action="store_true")
    parser.add_argument("--reuse-index", action="store_true")
    parser.add_argument("--export-shard", help="Export just one planned shard ID with the native CLI")
    parser.add_argument("--shard-gib", type=int, default=8)
    args = parser.parse_args()
    registry = args.vault / "metadata/asset-import-registry.v1.json"
    initial_revision = digest(registry)
    previous = None
    args.output.mkdir(parents=True, exist_ok=True)
    client_folders = [name for name in ("normalized", "metadata", "licenses", "animation") if (args.vault / name).is_dir()]
    index_path = args.output / "asset-library-transfer.json"
    if args.reuse_index:
        previous = json.loads(index_path.read_bytes())
        if previous["registry_sha256"] != initial_revision:
            raise ValueError("Registry changed; refresh the inventory before export")
        client_files, originals = previous["client_files"], previous["original_sources"]
    else:
        client_files = [entry for folder in client_folders for entry in inventory(args.vault, args.vault / folder)]
        originals = inventory(args.vault, args.vault / "originals") if (args.vault / "originals").is_dir() else []
    content_bytes = sum(entry["bytes"] for entry in client_files)
    plan = dict(schema="forge.delivery.asset-transfer.v1", package_id="asset-library", rights="private_local_only",
                registry_sha256=initial_revision, client_layout=client_folders,
                client_files=client_files, client_file_count=len(client_files), client_bytes=content_bytes,
                original_sources=originals, original_source_bytes=sum(e["bytes"] for e in originals),
                excluded_folders=["cache", "logs", "inbox", "quarantine"],
                status="inventoried_export_ready", archive=None,
                qualification="Existing asset profiles and provenance preserved; no new Studio/runtime or redistribution qualification.")
    if not 1 <= args.shard_gib <= 64:
        raise ValueError("Shard size must be between 1 and 64 GiB")
    shard_limit = args.shard_gib * 1024**3
    shards = []
    for entry in client_files:
        if entry["bytes"] > 16 * 1024**3:
            raise ValueError(f"File requires dedicated large-file transport: {entry['path']}")
        if not shards or (shards[-1]["files"] and (shards[-1]["bytes"] + entry["bytes"] > shard_limit or len(shards[-1]["files"]) >= 50000)):
            shards.append(dict(id=f"asset-library-part-{len(shards)+1:04d}", bytes=0, files=[]))
        shards[-1]["files"].append(entry["path"])
        shards[-1]["bytes"] += entry["bytes"]
    shard_folder = args.output / "shards"
    shard_folder.mkdir(exist_ok=True)
    licenses = [e["path"] for e in client_files if e["path"].startswith("licenses/")][:100]
    for shard in shards:
        selection = list(dict.fromkeys(shard.pop("files") + licenses))
        selection_path = shard_folder / f"{shard['id']}.paths.json"
        selection_path.write_text(json.dumps(selection, ensure_ascii=False) + "\n", encoding="utf-8")
        shard.update(selection_file=selection_path.relative_to(args.output).as_posix(), file_count=len(selection),
                     status="planned_not_exported", archive=None)
        if previous:
            old = next((s for s in previous.get("shards", []) if s["id"] == shard["id"]), None)
            if old and old.get("archive") and old["bytes"] == shard["bytes"] and old["file_count"] == shard["file_count"]:
                archive_path = args.output / old["archive"]["file"]
                if archive_path.is_file() and digest(archive_path) == old["archive"]["archive_sha256"]:
                    shard.update(status=old["status"], archive=old["archive"])
    plan.update(shards=shards, shard_target_bytes=shard_limit, status="inventoried_sharded_transfer_planned",
                client_merge="Preserve relative paths; identical shared license files may overlap. Compose the installed shards into a separate Vault root.")
    if args.export_shard:
        if not args.delivery_cli or not licenses:
            raise ValueError("Shard export requires the native CLI and license evidence")
        shard = next((s for s in shards if s["id"] == args.export_shard), None)
        if not shard:
            raise ValueError("Unknown planned shard ID")
        descriptor = dict(schema="forge.delivery.package.v1", id=shard["id"], label="Bibliothèque privée : " + shard["id"],
                          version="0.1.0-" + initial_revision[:12], kind="assets", platform="any", source_revision=initial_revision,
                          rights="private_local_only", license_paths=licenses, files=[], product=None)
        descriptor_path = shard_folder / f"{shard['id']}.descriptor.json"
        descriptor_path.write_text(json.dumps(descriptor, ensure_ascii=False), encoding="utf-8")
        archive = shard_folder / f"{shard['id']}.forgepack"
        proof_path = shard_folder / f"{shard['id']}.pack-proof.json"
        with proof_path.open("w", encoding="utf-8") as stdout:
            result = subprocess.run([str(args.delivery_cli), "pack-list", str(args.vault), str(descriptor_path), str(archive.absolute()),
                                     str(args.output / shard["selection_file"])], stdout=stdout, stderr=subprocess.PIPE, text=True)
        if result.returncode:
            raise RuntimeError(result.stderr[-5000:])
        receipt = json.loads(proof_path.read_bytes())
        if digest(registry) != initial_revision:
            raise ValueError("Registry changed during shard export; archive retained but not promoted")
        shard.update(status="private_local_package_ready", archive=dict(file=archive.relative_to(args.output).as_posix(),
                     archive_sha256=receipt["archive_sha256"], manifest_sha256=receipt["manifest_sha256"], bytes=archive.stat().st_size))
    if digest(registry) != initial_revision:
        raise ValueError("Vault registry changed during inventory; retry with stable sources")
    if args.pack:
        if content_bytes > 256 * 1024**3:
            raise ValueError("Library exceeds single-package limits; use the planned shard exports")
        if not args.delivery_cli:
            raise ValueError("--pack requires --delivery-cli")
        licenses = [e["path"] for e in client_files if e["path"].startswith("licenses/")][:100]
        if not licenses:
            raise ValueError("No existing license evidence found")
        descriptor = dict(schema="forge.delivery.package.v1", id="asset-library", label="Bibliothèque privée d’assets Forge",
                          version="0.1.0-" + initial_revision[:12], kind="assets", platform="any", source_revision=initial_revision,
                          rights="private_local_only", license_paths=licenses, files=[], product=None)
        descriptor_file = args.output / "asset-library.descriptor.json"
        descriptor_file.write_text(json.dumps(descriptor, ensure_ascii=False), encoding="utf-8")
        archive = args.output / "asset-library.forgepack"
        output_file = args.output / "asset-library.pack-proof.json"
        with output_file.open("w", encoding="utf-8") as stdout:
            process = subprocess.run([str(args.delivery_cli), "pack-selected", str(args.vault), str(descriptor_file),
                                     str(archive.absolute()), *client_folders], stdout=stdout, stderr=subprocess.PIPE, text=True)
        if process.returncode:
            raise RuntimeError(process.stderr[-5000:])
        receipt = json.loads(output_file.read_bytes())
        if digest(registry) != initial_revision:
            raise ValueError("Vault registry changed during export; archive retained but not promoted")
        plan["archive"] = dict(file=archive.name, archive_sha256=receipt["archive_sha256"],
                               manifest_sha256=receipt["manifest_sha256"], bytes=archive.stat().st_size,
                               files=len(receipt["descriptor"]["files"]))
        plan["status"] = "private_local_package_ready"
    path = args.output / "asset-library-transfer.json"
    path.write_text(json.dumps(plan, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"index":str(path), "status":plan["status"], "client_files":len(client_files),
                      "client_bytes":content_bytes, "original_sources":len(originals),
                      "original_source_bytes":plan["original_source_bytes"]}), flush=True)


if __name__ == "__main__":
    main()
