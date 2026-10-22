"""Prepare checksum-pinned Cargo sources on exFAT without applying tar timestamps.

Archives stay byte-for-byte intact. Only extraction metadata differs. Cargo's
version-1 completion marker is written after every extracted file is verified.
Run while Cargo is stopped; use the regular Cargo home/target on F afterwards.
"""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import tarfile
import tomllib
import urllib.request
import uuid

REGISTRY = "index.crates.io-1949cf8c6b5b557f"
SOURCE = "registry+https://github.com/rust-lang/crates.io-index"


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def closure(packages, roots):
    def resolve(spec):
        fields = spec.split()
        matches = [p for p in packages if p["name"] == fields[0]
                   and (len(fields) == 1 or p["version"] == fields[1])]
        if len(matches) != 1:
            raise ValueError(f"Ambiguous or absent locked dependency: {spec}")
        return matches[0]

    queue = [resolve(root) for root in roots]
    found = {}
    while queue:
        package = queue.pop()
        key = (package["name"], package["version"])
        if key in found:
            continue
        found[key] = package
        queue.extend(resolve(dep) for dep in package.get("dependencies", []))
    return [p for _, p in sorted(found.items()) if p.get("source") == SOURCE]


def members(archive, slug):
    seen = set()
    for member in archive:
        name = PurePosixPath(member.name)
        if (not name.parts or name.parts[0] != slug or name.is_absolute()
                or ".." in name.parts or "\\" in member.name or ":" in member.name
                or member.issym() or member.islnk()
                or not (member.isdir() or member.isfile())
                or name.name.rstrip(" .").casefold() == ".cargo-ok"
                or member.name.casefold() in seen):
            raise ValueError(f"Unsupported archive member: {member.name}")
        seen.add(member.name.casefold())
        yield member, Path(*name.parts[1:])


def verify_sources(archive_path, directory, slug):
    count = 0
    with tarfile.open(archive_path, "r:gz") as archive:
        for member, relative in members(archive, slug):
            if member.isdir():
                continue
            target = directory / relative
            if target.is_symlink() or target.stat().st_size != member.size:
                raise ValueError(f"Extracted file differs: {target}")
            with archive.extractfile(member) as stream:
                expected = hashlib.file_digest(stream, "sha256").hexdigest()
            if digest(target) != expected:
                raise ValueError(f"Extracted file checksum differs: {target}")
            count += 1
    return count


def prepare(package, home, read_cache):
    slug = f'{package["name"]}-{package["version"]}'
    cache = home / "registry" / "cache" / REGISTRY
    cache.mkdir(parents=True, exist_ok=True)
    archive_path = cache / f"{slug}.crate"
    if not archive_path.exists():
        candidate = read_cache / "registry" / "cache" / REGISTRY / archive_path.name
        temporary = cache / f".{slug}-{uuid.uuid4().hex}.partial"
        if candidate.is_file():
            if digest(candidate) != package["checksum"]:
                raise ValueError(f"Read-only cache checksum differs: {candidate}")
            with candidate.open("rb") as source, temporary.open("xb") as output:
                shutil.copyfileobj(source, output)
        else:
            url = f'https://static.crates.io/crates/{package["name"]}/{slug}.crate'
            with urllib.request.urlopen(url, timeout=30) as source, temporary.open("xb") as output:
                shutil.copyfileobj(source, output)
        if digest(temporary) != package["checksum"]:
            raise ValueError(f"Archive checksum differs; partial retained: {temporary}")
        temporary.rename(archive_path)
    if digest(archive_path) != package["checksum"]:
        raise ValueError(f"Locked archive checksum differs: {archive_path}")

    source_root = home / "registry" / "src" / REGISTRY
    source_root.mkdir(parents=True, exist_ok=True)
    destination = source_root / slug
    marker = destination / ".cargo-ok"
    if marker.is_file() and json.loads(marker.read_text()) == {"v": 1}:
        return {"package": slug, "archive_sha256": package["checksum"],
                "files": verify_sources(archive_path, destination, slug), "status": "VERIFIED_EXISTING"}
    staging = home / "registry" / ".gate5-staging" / f"{slug}-{uuid.uuid4().hex}"
    staging.mkdir(parents=True)
    with tarfile.open(archive_path, "r:gz") as archive:
        for member, relative in members(archive, slug):
            target = staging / relative
            if member.isdir():
                target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with archive.extractfile(member) as source, target.open("xb") as output:
                    shutil.copyfileobj(source, output)
    count = verify_sources(archive_path, staging, slug)
    manifest = tomllib.loads((staging / "Cargo.toml").read_text(encoding="utf-8"))
    if (manifest["package"]["name"], manifest["package"]["version"]) != (package["name"], package["version"]):
        raise ValueError(f"Package identity differs: {slug}")
    (staging / ".cargo-ok").write_text('{"v":1}', encoding="utf-8")
    if destination.exists():
        if destination.is_symlink() or any(p.is_symlink() for p in destination.rglob("*")):
            raise ValueError(f"Unexpected source link; preserved: {destination}")
        preserved = home / "registry" / ".gate5-interrupted" / f"{slug}-{uuid.uuid4().hex}"
        preserved.parent.mkdir(exist_ok=True)
        # Both absolute paths are confined to this dedicated registry.
        destination.resolve().relative_to(home)
        preserved.resolve().relative_to(home)
        os.rename(destination, preserved)
    os.rename(staging, destination)
    return {"package": slug, "archive_sha256": package["checksum"],
            "files": count, "status": "EXTRACTED_CONTENT_VERIFIED_NO_TAR_MTIME"}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--lock", type=Path, required=True)
    parser.add_argument("--home", type=Path, required=True)
    parser.add_argument("--read-cache", type=Path, required=True)
    parser.add_argument("--root", action="append", required=True)
    parser.add_argument("--receipt", type=Path, required=True)
    args = parser.parse_args()
    home = args.home.resolve()
    if home != Path("E:/OpenRPG_Forge/.build/cargo-home").resolve():
        raise ValueError("Only the dedicated E: Cargo home is writable")
    if args.receipt.exists():
        raise ValueError("Existing receipt preserved")
    args.receipt.resolve().relative_to(Path("F:/OpenRPG_Forge").resolve())
    packages = tomllib.loads(args.lock.read_text(encoding="utf-8"))["package"]
    results = []
    for package in closure(packages, args.root):
        result = prepare(package, home, args.read_cache)
        results.append(result)
        print(json.dumps(result), flush=True)
    args.receipt.parent.mkdir(parents=True, exist_ok=True)
    with args.receipt.open("x", encoding="utf-8") as output:
        json.dump({"utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
                   "lock_sha256": digest(args.lock), "roots": args.root,
                   "packages": results}, output, indent=2)


if __name__ == "__main__":
    main()
