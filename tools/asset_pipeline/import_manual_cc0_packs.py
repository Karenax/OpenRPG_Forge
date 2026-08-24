#!/usr/bin/env python3
"""Import the audited CC0 archives manually downloaded from official Itch.io pages."""

from __future__ import annotations

import argparse
import os
from pathlib import Path, PurePosixPath
import shutil
import sys
import time
from typing import Any
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parent))
from download_cc0_3d import (  # noqa: E402
    atomic_write_json,
    default_vault,
    ensure_download_cache,
    file_digest,
    relativize_vault_paths,
    safe_extract_zip,
)


MODEL_EXTENSIONS = {".glb", ".gltf", ".fbx", ".obj"}
IMAGE_EXTENSIONS = {".png", ".jpg", ".jpeg", ".webp"}
DANGEROUS_EXTENSIONS = {".bat", ".cmd", ".com", ".dll", ".exe", ".js", ".msi", ".ps1", ".scr", ".vbs"}
CC0_NAME_MARKERS = ("creative commons zero", "cc0 1.0 universal")
CC0_URL_MARKER = "creativecommons.org/publicdomain/zero/1.0"


PACKS: tuple[dict[str, Any], ...] = (
    {
        "source": "quaternius",
        "slug": "universal-base-characters-standard",
        "name": "Universal Base Characters",
        "author": "Quaternius",
        "version": "Standard",
        "archive": "Universal Base Characters[Standard].zip",
        "sha256": "fdbf1804c90dfc1ea03e992bff7da2dfd1a79318e13270a660180f9308455f40",
        "source_page": "https://quaternius.itch.io/universal-base-characters",
        "license_path": "Universal Base Characters[Standard]/License_Standard.txt",
        "gltf_uri_repairs": {
            "T_Eye_Normal_png.png": "T_Eye_Normal.png",
            "T_Hair_1_Normal_png.png": "T_Hair_1_Normal.png",
        },
        "categories": ["Characters", "Animations"],
        "tags": ["character", "humanoid", "rigged", "retargeting", "fantasy"],
    },
    {
        "source": "quaternius",
        "slug": "modular-character-outfits-fantasy-standard",
        "name": "Modular Character Outfits - Fantasy",
        "author": "Quaternius",
        "version": "Standard-2.1",
        "archive": "Modular Character Outfits - Fantasy[Standard].zip",
        "sha256": "c3468b18871cc8c8f05ab14df7712baf22cb9f389cbd870babf130e595187f70",
        "source_page": "https://quaternius.itch.io/modular-character-outfits-fantasy",
        "license_path": "Modular Character Outfits - Fantasy[Standard]/License_Standard.txt",
        "categories": ["Characters", "Animations", "Props"],
        "tags": ["character", "outfit", "modular", "rigged", "fantasy", "medieval"],
    },
    {
        "source": "quaternius",
        "slug": "lowpoly-animated-easy-enemies",
        "name": "LowPoly Animated Easy Enemies",
        "author": "Quaternius",
        "version": "January-2019",
        "archive": "Easy Animated Enemy Pack - Jan 2019.zip",
        "sha256": "a97f38b981fec2f42b263fe92828a7bf73f9da1228d5aac906fe354cd2b21004",
        "source_page": "https://quaternius.itch.io/animated-easy-enemies",
        "license_path": None,
        "categories": ["Creatures", "Characters", "Animations"],
        "tags": ["enemy", "animated", "frog", "rat", "snake", "spider", "wasp"],
    },
    {
        "source": "kaykit",
        "slug": "forest-nature-pack-1.0-free",
        "name": "KayKit Forest Nature Pack",
        "author": "Kay Lousberg",
        "version": "1.0 FREE",
        "archive": "KayKit_Forest_Nature_Pack_1.0_FREE.zip",
        "sha256": "2ee83e63bb7695f2d884ec27ddf6fce020789a452e7d5c5b0bbdfc4f6ea1fc8c",
        "source_page": "https://kaylousberg.itch.io/kaykit-forest",
        "license_path": "KayKit_Forest_Nature_Pack_1.0_FREE/License.txt",
        "categories": ["Nature", "Props", "Materials"],
        "tags": ["forest", "nature", "tree", "rock", "plant", "stylized"],
    },
    {
        "source": "kaykit",
        "slug": "character-animations-1.1-free",
        "name": "KayKit Character Animations",
        "author": "Kay Lousberg",
        "version": "1.1 FREE",
        "archive": "KayKit_Character_Animations_1.1.zip",
        "sha256": "65882f31f905ad2e953819648a59287cdeab8f623908d5ef701971d3758be20f",
        "source_page": "https://kaylousberg.itch.io/kaykit-character-animations",
        "license_path": "KayKit_Character_Animations_1.1/License.txt",
        "categories": ["Animations", "Characters"],
        "tags": ["animation", "humanoid", "combat", "locomotion", "retargeting"],
    },
    {
        "source": "kaykit",
        "slug": "fantasy-weapons-bits-1.0-free",
        "name": "KayKit Fantasy Weapons Bits",
        "author": "Kay Lousberg",
        "version": "1.0 FREE",
        "archive": "KayKit_FantasyWeaponsBits_1.0_FREE.zip",
        "sha256": "1017144f85a994f405b731befa71ced360f5b60ed0b9c00c72e711c1633ce2d2",
        "source_page": "https://kaylousberg.itch.io/fantasy-weapons-bits",
        "license_path": "KayKit_FantasyWeaponsBits_1.0_FREE/License.txt",
        "categories": ["Weapons", "Props"],
        "tags": ["weapon", "fantasy", "sword", "axe", "bow", "shield"],
    },
    {
        "source": "kaykit",
        "slug": "rpg-tools-bits-1.0-free",
        "name": "KayKit RPG Tools Bits",
        "author": "Kay Lousberg",
        "version": "1.0 FREE",
        "archive": "KayKit_RPGToolsBits_1.0_FREE.zip",
        "sha256": "a17c2a54df93d525e90d960baf3c68638defb4f424e204ceead6ea1f89e9c7dd",
        "source_page": "https://kaylousberg.itch.io/rpg-tools-bits",
        "license_path": "KayKit_RPGToolsBits_1.0_FREE/License.txt",
        "categories": ["Props", "Weapons"],
        "tags": ["rpg", "tool", "crafting", "medieval", "prop"],
    },
    {
        "source": "kaykit",
        "slug": "resource-bits-1.0-free",
        "name": "KayKit Resource Bits",
        "author": "Kay Lousberg",
        "version": "1.0 FREE",
        "archive": "KayKit_ResourceBits_1.0_FREE.zip",
        "sha256": "7056f1310896a4612a67703fd6d5af389fcccdeb46df0a89e2322aa8e3bcfcf7",
        "source_page": "https://kaylousberg.itch.io/resource-bits",
        "license_path": "KayKit_ResourceBits_1.0_FREE/License.txt",
        "categories": ["Props", "Nature", "Materials"],
        "tags": ["resource", "ore", "wood", "stone", "crafting", "prop"],
    },
)


def audit_archive(archive: Path, pack: dict[str, Any]) -> dict[str, Any]:
    if file_digest(archive) != pack["sha256"]:
        raise RuntimeError(f"SHA-256 mismatch for {archive.name}")
    license_paths: list[str] = []
    with zipfile.ZipFile(archive) as bundle:
        members = bundle.infolist()
        if len(members) > 10_000:
            raise RuntimeError(f"Archive contains too many entries: {archive}")
        if sum(member.file_size for member in members) > 8 * 1024 * 1024 * 1024:
            raise RuntimeError(f"Archive expands beyond the 8 GiB safety limit: {archive}")
        root = Path("C:/openrpg-archive-audit")
        for member in members:
            member_path = PurePosixPath(member.filename.replace("\\", "/"))
            target = (root / Path(*member_path.parts)).resolve()
            if root.resolve() != target and root.resolve() not in target.parents:
                raise RuntimeError(f"Unsafe archive path {member.filename!r} in {archive}")
            if member.flag_bits & 1:
                raise RuntimeError(f"Encrypted archive member {member.filename!r} in {archive}")
            if ((member.external_attr >> 16) & 0o170000) == 0o120000:
                raise RuntimeError(f"Symbolic link {member.filename!r} in {archive}")
            if Path(member.filename).suffix.casefold() in DANGEROUS_EXTENSIONS:
                raise RuntimeError(f"Executable content {member.filename!r} in {archive}")
        expected_license = pack.get("license_path")
        if expected_license:
            try:
                license_text = bundle.read(expected_license).decode("utf-8-sig", errors="replace").casefold()
            except KeyError as error:
                raise RuntimeError(f"Expected license is missing from {archive}: {expected_license}") from error
            if CC0_URL_MARKER not in license_text or not any(marker in license_text for marker in CC0_NAME_MARKERS):
                raise RuntimeError(f"Expected license does not explicitly declare CC0: {expected_license}")
            license_paths.append(expected_license)
    return {
        "archive_entry_count": len(members),
        "archive_uncompressed_bytes": sum(member.file_size for member in members),
        "license_evidence_files": license_paths,
    }


def materialize_archive(vault: Path, source: Path, pack: dict[str, Any]) -> Path:
    content_hash = pack["sha256"]
    destination = vault / "originals" / pack["source"] / pack["slug"] / source.name
    if destination.exists() and file_digest(destination) != content_hash:
        destination = destination.parent / "versions" / content_hash[:16] / source.name
    if destination.exists():
        if file_digest(destination) != content_hash:
            raise RuntimeError(f"Immutable archive path contains different bytes: {destination}")
        ensure_download_cache(destination, content_hash)
        return destination
    destination.parent.mkdir(parents=True, exist_ok=True)
    try:
        os.link(source, destination)
    except OSError:
        shutil.copy2(source, destination)
    ensure_download_cache(destination, content_hash)
    return destination


def apply_gltf_uri_repairs(extracted_root: Path, model_files: list[str], repairs: dict[str, str]) -> list[dict[str, str]]:
    applied: list[dict[str, str]] = []
    if not repairs:
        return applied
    for relative in model_files:
        if Path(relative).suffix.casefold() != ".gltf":
            continue
        path = extracted_root / relative
        content = path.read_bytes()
        updated = content
        for broken_uri, repaired_uri in repairs.items():
            broken = f'"uri":"{broken_uri}"'.encode("utf-8")
            if broken not in updated:
                continue
            dependency = path.parent / repaired_uri
            if not dependency.is_file():
                raise RuntimeError(f"Cannot repair {relative}: replacement dependency is missing: {repaired_uri}")
            updated = updated.replace(broken, f'"uri":"{repaired_uri}"'.encode("utf-8"))
            applied.append({"file": relative, "from": broken_uri, "to": repaired_uri})
        if updated != content:
            temporary = path.with_suffix(path.suffix + ".tmp")
            temporary.write_bytes(updated)
            os.replace(temporary, path)
    return applied


def import_pack(vault: Path, inbox: Path, pack: dict[str, Any], *, catalog_date: str, retrieved_at: str) -> dict[str, Any]:
    inbox_archive = inbox / pack["archive"]
    if not inbox_archive.is_file():
        raise RuntimeError(f"Expected manual archive is missing: {inbox_archive}")
    audit = audit_archive(inbox_archive, pack)
    archive = materialize_archive(vault, inbox_archive, pack)
    extracted_root = vault / "normalized" / pack["source"] / pack["slug"]
    extracted = safe_extract_zip(archive, extracted_root)
    model_files = sorted(value for value in extracted if Path(value).suffix.casefold() in MODEL_EXTENSIONS)
    if not model_files:
        raise RuntimeError(f"No supported 3D model found in {archive}")
    normalized_repairs = apply_gltf_uri_repairs(extracted_root, model_files, pack.get("gltf_uri_repairs", {}))
    preview_files = sorted(
        value for value in extracted
        if Path(value).suffix.casefold() in IMAGE_EXTENSIONS
        and any(token in Path(value).stem.casefold() for token in ("contents", "preview", "render", "sample", "thumbnail"))
    )
    metadata = {
        "schema_version": 1,
        "pack_id": f"{pack['source']}:{pack['slug']}",
        "source": pack["source"],
        "name": pack["name"],
        "author": pack["author"],
        "version": pack["version"],
        "source_page": pack["source_page"],
        "catalog_date": catalog_date,
        "retrieved_at": retrieved_at,
        "license": "CC0-1.0",
        "license_url": pack["source_page"],
        "license_verified": True,
        "license_verification": "archive-and-official-page" if pack.get("license_path") else "official-page-and-pinned-archive",
        "attribution_required": False,
        "categories": pack["categories"],
        "tags": pack["tags"],
        "files": [{
            "path": archive.as_posix(),
            "url": pack["source_page"],
            "size": archive.stat().st_size,
            "sha256": pack["sha256"],
            "source_md5": None,
        }],
        "archive_sha256": pack["sha256"],
        "extracted_root": extracted_root.as_posix(),
        "extracted_files": extracted,
        "model_files": model_files,
        "preview_files": preview_files,
        "model_file_count": len(model_files),
        "unique_model_name_count": len({Path(value).stem.casefold() for value in model_files}),
        "total_bytes": archive.stat().st_size,
        "status": "downloaded-and-extracted",
        "normalized_repairs": normalized_repairs,
        **audit,
    }
    metadata = relativize_vault_paths(metadata, vault)
    atomic_write_json(vault / "metadata" / pack["source"] / f"{pack['slug']}.json", metadata)
    return metadata


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--vault", type=Path)
    parser.add_argument("--inbox", type=Path)
    parser.add_argument("--catalog-output", type=Path)
    parser.add_argument("--pack", action="append", choices=[pack["slug"] for pack in PACKS])
    args = parser.parse_args(sys.argv[1:] if argv is None else argv)
    repo_root = Path(__file__).resolve().parents[2]
    vault = (args.vault or default_vault(repo_root)).resolve()
    inbox = (args.inbox or vault / "inbox" / "manual").resolve()
    selected = [pack for pack in PACKS if not args.pack or pack["slug"] in args.pack]
    catalog_date = time.strftime("%Y-%m-%d", time.gmtime())
    retrieved_at = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    packs: list[dict[str, Any]] = []
    errors: list[dict[str, str]] = []
    for index, pack in enumerate(selected, start=1):
        try:
            packs.append(import_pack(vault, inbox, pack, catalog_date=catalog_date, retrieved_at=retrieved_at))
            print(f"[Manual {index}/{len(selected)}] {pack['slug']}", flush=True)
        except Exception as error:  # Continue independently audited packs.
            errors.append({"pack_id": f"{pack['source']}:{pack['slug']}", "error": str(error)})
            print(f"[Manual ERROR {index}/{len(selected)}] {pack['slug']}: {error}", flush=True)
    packs.sort(key=lambda item: item["pack_id"])
    catalog = {
        "schema_version": 1,
        "catalog_date": catalog_date,
        "vault_root": "${OPENRPG_ASSET_VAULT}",
        "source": "manual-itch-cc0",
        "license_policy": "cc0-only",
        "pack_count": len(packs),
        "model_file_count": sum(item["model_file_count"] for item in packs),
        "unique_model_name_count": sum(item["unique_model_name_count"] for item in packs),
        "total_bytes": sum(item["total_bytes"] for item in packs),
        "error_count": len(errors),
        "errors": errors,
        "packs": packs,
    }
    output = args.catalog_output or repo_root / "asset_manifests" / "catalogs" / "manual_itch_cc0_packs.json"
    if not output.is_absolute():
        output = repo_root / output
    atomic_write_json(output, catalog)
    atomic_write_json(vault / "logs" / "latest-manual-itch-cc0.json", catalog)
    print(f"Catalog: {output}")
    print(f"Packs: {len(packs)}; models: {catalog['model_file_count']}; errors: {len(errors)}")
    return 0 if not errors else 2


if __name__ == "__main__":
    raise SystemExit(main())
