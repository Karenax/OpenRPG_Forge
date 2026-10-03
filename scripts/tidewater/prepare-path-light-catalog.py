"""Prepare isolated AssetCatalog metadata; never install or mutate a zone.

Usage: PACKAGE EXPECTED_PACKAGE_SHA256 NEW_OUTPUT
Each content revision identifies the complete immutable source package.
"""
from pathlib import Path
import hashlib
import json
import sys
import uuid


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def main():
    package_arg, expected, output_arg = sys.argv[1:]
    package, output = Path(package_arg), Path(output_arg)
    assert not output.exists()
    manifest_raw = (package / "package.json").read_bytes()
    assert digest(manifest_raw) == expected
    manifest = json.loads(manifest_raw)
    assert manifest["schema"] == "forge.tidewater.path-light-package"
    assert manifest["version"] == 1
    assert {p.name for p in package.iterdir()} == set(manifest["files"]) | {"package.json"}
    for name, pin in manifest["files"].items():
        assert Path(name).name == name
        raw = (package / name).read_bytes()
        assert len(raw) == pin["bytes"] and digest(raw) == pin["sha256"]
    description = json.loads((package / manifest["props"]["file"]).read_bytes())
    assert len(description["props"]) == 3
    assert description["source_commit"] == manifest["source_commit"]
    namespace = uuid.UUID("9e65f3d9-f8e2-5f10-8d1e-3e0b6e4d7c21")
    assets, bindings = [], []
    for prop in description["props"]:
        source_id = prop["source_id"]
        asset_id = str(uuid.uuid5(namespace, "openrpg-forge:asset:" + source_id))
        assets.append({"id": asset_id, "logical_name": source_id, "kind": "mesh",
                       "source": {"origin": "external", "declared_license": "MIT",
                                  "uri": "https://github.com/dgreenheck/tidewater/tree/" + manifest["source_commit"]},
                       "content_hash": expected})
        bindings.append({"asset_id": asset_id, "asset_hash": expected,
                         "source_object_id": source_id, "source_frame": prop["source_frame"],
                         "local_bounds": prop["local_bounds"],
                         "description_fr": "Petite lampe de chemin sur un poteau en bois, lanterne en verre chaud et métal.",
                         "description_en": "Small path light on a wooden post, with a warm glass lantern and metal fittings.",
                         "description_basis": "source_declared",
                         "bounds_basis": "measured_from_converted_vertices",
                         "studio_visible": False, "runtime_qualified": False})
    assert len({row["id"] for row in assets}) == len(assets)
    output.mkdir()
    (output / "catalog.json").write_text(json.dumps({"schema_version": 1, "assets": assets}, indent=2) + "\n", encoding="utf-8")
    (output / "bindings.json").write_text(json.dumps({"schema": "forge.imported-source-catalog-preparation.v1",
        "package_sha256": expected, "bindings": bindings,
        "qualification": "metadata preparation only; no installed asset or authored zone"}, indent=2) + "\n", encoding="utf-8")
    receipt = {"schema": "forge.path-light-catalog-preparation.v1",
               "script_sha256": digest(Path(__file__).read_bytes()), "package_sha256": expected,
               "files": {p.name: {"sha256": digest(p.read_bytes()), "bytes": p.stat().st_size}
                         for p in sorted(output.iterdir())},
               "assets": len(assets), "studio_executed": False, "runtime_qualified": False}
    (output / "receipt.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(receipt))


if __name__ == "__main__":
    main()
