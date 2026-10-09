"""Export a separate private catalogue before the much larger normalized asset shards."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--vault", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--delivery-cli", type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    registry = args.vault / "metadata/asset-import-registry.v1.json"
    def revision():
        with registry.open("rb") as stream:
            return hashlib.file_digest(stream, "sha256").hexdigest()
    pin = revision()
    licenses = [p.relative_to(args.vault).as_posix() for p in sorted((args.vault / "licenses").rglob("*")) if p.is_file()]
    descriptor = dict(schema="forge.delivery.package.v1", id="asset-library-catalog", label="Catalogue privé des assets Forge",
                      version="0.1.0-"+pin[:12], kind="assets", platform="any", source_revision=pin,
                      rights="private_local_only", license_paths=licenses, files=[], product=None)
    description = args.output / "asset-library-catalog.descriptor.json"
    description.write_text(json.dumps(descriptor, ensure_ascii=False), encoding="utf-8")
    archive = args.output / "asset-library-catalog.forgepack"
    proof = args.output / "asset-library-catalog.pack-proof.json"
    with proof.open("w", encoding="utf-8") as stdout:
        process = subprocess.run([str(args.delivery_cli), "pack-selected", str(args.vault), str(description),
                                  str(archive.absolute()), "metadata", "licenses", "animation"], stdout=stdout, stderr=subprocess.PIPE, text=True)
    if process.returncode:
        raise RuntimeError(process.stderr[-4000:])
    if revision() != pin:
        raise ValueError("Registry changed while exporting; retain archive without promoting")
    receipt = json.loads(proof.read_bytes())
    index = dict(schema="forge.delivery.catalog-export.v1", status="PRIVATE_LOCAL_CATALOG_READY", archive=archive.name,
                 archive_sha256=receipt["archive_sha256"], manifest_sha256=receipt["manifest_sha256"], bytes=archive.stat().st_size,
                 files=len(receipt["descriptor"]["files"]), registry_sha256=pin, rights="private_local_only",
                 normalized_payload_included=False, full_library_qualified=False)
    (args.output / "asset-library-catalog.json").write_text(json.dumps(index, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(index), flush=True)

if __name__ == "__main__":
    main()
