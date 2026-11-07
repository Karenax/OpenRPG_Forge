"""Immutable qualification snapshot of the common checkout, not another development checkout."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import zipfile


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--repository", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    root = args.repository.resolve(strict=True)
    spec = importlib.util.spec_from_file_location("bounded_inventory", root / "scripts/gate6/capture_inputs.py")
    reader = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(reader)
    reader.ROOTS = ("apps", "crates", "tools", "third_party", "examples", ".cargo", "scripts", "docs")
    reader.EXPLICIT = tuple(sorted({path.name for pattern in ("*.toml", "*.lock", "*.md", ".gitignore", ".gitattributes") for path in root.glob(pattern) if path.is_file()}))
    files = reader.capture(root)
    # A VM-native build requires a frozen source transport and its own OS-native cache.
    # Changes remain authored only in the Windows common checkout.
    manifest = {"schema":"forge.gate7.vm-source-snapshot.v1", "repository":str(root), "files":files,
                "head":subprocess.check_output(["git", "-C", str(root), "rev-parse", "HEAD"], encoding="utf-8").strip(),
                "roots":reader.ROOTS,"bytes":sum(item["bytes"] for item in files.values()),"development_checkout":False}
    with zipfile.ZipFile(args.output, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=2, allowZip64=True) as archive:
        for name, expected in files.items():
            data = (root / name).read_bytes()
            if len(data) != expected["bytes"] or hashlib.sha256(data).hexdigest() != expected["sha256"]:
                raise ValueError(f"Input changed while packing: {name}")
            archive.writestr(name, data)
        archive.writestr("__forge_vm_source_manifest__.json", json.dumps(manifest, ensure_ascii=False))
    if reader.capture(root) != files:
        raise ValueError("Common checkout changed during source transport construction")
    with zipfile.ZipFile(args.output) as archive:
        for name, expected in files.items():
            if hashlib.sha256(archive.read(name)).hexdigest() != expected["sha256"]:
                raise ValueError(f"Source transport verification failed: {name}")
    with args.output.open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256").hexdigest()
    receipt = {"archive":str(args.output), "sha256":digest,"files":len(files),"source_bytes":manifest["bytes"],"archive_bytes":args.output.stat().st_size,"full_read_verified":True}
    with args.output.with_suffix(".receipt.json").open("x", encoding="utf-8") as stream:
        json.dump(receipt, stream, indent=2)
    print(json.dumps(receipt))


if __name__ == "__main__":
    main()
