"""Extract a fresh Windows candidate and attest its manifest and executable pins."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import stat
import zipfile


def digest(path):
    result = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            result.update(block)
    return result.hexdigest()


def relative(value):
    value = value.replace("\\", "/")
    path = PurePosixPath(value)
    if path.is_absolute() or ":" in value or any(p in ("", ".", "..") for p in value.rstrip("/").split("/")):
        raise ValueError("Unsafe package path")
    return path


def verify(archive, destination, receipt, expected):
    if destination.exists() or receipt.exists():
        raise ValueError("Fresh extraction and receipt paths required")
    archive_hash = digest(archive)
    if archive_hash != expected.lower():
        raise ValueError("Archive pin differs")
    with zipfile.ZipFile(archive) as bundle:
        entries = bundle.infolist()
        flat = any(e.filename == "manifest.json" for e in entries)
        if len(entries) > 12000 or sum(e.file_size for e in entries) > 2 * 1024**3:
            raise ValueError("Package quota exceeded")
        seen = set()
        for entry in entries:
            name = relative(entry.filename)
            if (not flat and name.parts[0] != archive.stem) or name.as_posix().casefold() in seen:
                raise ValueError("Unexpected package root or duplicate")
            if stat.S_ISLNK(entry.external_attr >> 16):
                raise ValueError("Package links are forbidden")
            seen.add(name.as_posix().casefold())
        destination.mkdir()
        bundle.extractall(destination)
    root = destination if flat else destination / archive.stem
    manifest = json.loads((root / "manifest.json").read_text(encoding="utf-8-sig"))
    if manifest["schema"] != "forge.tidewater.windows-playtest.v1" or manifest["source_commit"] != "1438b1abfcaee3267092b75573014f4d9b4a983c":
        raise ValueError("Unexpected candidate contract")
    expected_files = {"manifest.json"}
    total = 0
    for entry in manifest["files"]:
        name = relative(entry["path"]).as_posix()
        if name in expected_files:
            raise ValueError("Duplicate manifest entry")
        expected_files.add(name)
        path = root / name
        if path.stat().st_size != entry["bytes"] or digest(path) != entry["sha256"]:
            raise ValueError("Extracted payload differs: " + name)
        total += entry["bytes"]
    actual_files = {p.relative_to(root).as_posix() for p in root.rglob("*") if p.is_file()}
    if actual_files != expected_files:
        raise ValueError("Extracted inventory differs")
    pins = {
        "OpenRPG-Forge-Tidewater.exe": "25724b8407cc0d0f77cf9ee8d707c59a01e131e7496954fdefa3574c70ba0478",
        "runtime/tidewater.exe": "b8990c25c58a9a5fe27f4ece81da3e7c8cf7813d3289df67d4c782b77e12a174",
        "content/debris/procedural/debris-native.json": "63e9abf5736fa043765366c78e7eea3688b62ce641b43c7a9db3bed34f528216",
        "content/debris/scanned/package.json": "f7faf1c052d59edae78420f22372daa2f00369c05f2bb60846e965bcd86c4ad4",
        "content/debris/scanned.wgsl": "3d54ba12b6546b79d220ab9912ec871237cd1b9a25d6043cc375f5cb1c9503c3",
    }
    for name, pin in pins.items():
        if digest(root / name) != pin:
            raise ValueError("Candidate component pin differs: " + name)
    result = {"schema": "forge.tidewater.windows-extraction.v1", "pass": True,
              "archive_sha256": archive_hash, "root": str(root), "flat_archive": flat, "manifest_files": len(manifest["files"]),
              "bytes": total, "component_pins": pins,
              "limits": ["integrity only; no game or launcher UI execution", "no fresh-machine qualification"]}
    receipt.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("archive", type=Path)
    parser.add_argument("destination", type=Path)
    parser.add_argument("receipt", type=Path)
    parser.add_argument("--expected-sha256", required=True)
    args = parser.parse_args()
    verify(args.archive, args.destination, args.receipt, args.expected_sha256)
