"""Assemble governed local kit revisions without mutating the input kits.

The default is additive. Replacing a governed asset is deliberately opt-in:
it may only replace an existing id at its existing GLB path and records a
separate attestation so an authoring candidate can distinguish it from an
append-only catalog revision.
"""
import argparse
import copy
import hashlib
import json
from pathlib import Path


def sha(data):
    return hashlib.sha256(data).hexdigest()


def json_bytes(value):
    return (json.dumps(value, indent=2) + "\n").encode("utf-8")


def require_fresh_output(source, addition, output):
    source, addition, output = source.resolve(), addition.resolve(), output.resolve()
    if output.exists() or output.drive.lower() != "e:" or any(output.is_relative_to(p) for p in (source, addition)):
        raise ValueError("fresh external E: output required")
    return source, addition, output


class Inputs:
    def __init__(self):
        self.data = {}

    def read(self, root, relative):
        relative = Path(relative)
        if relative.is_absolute() or ".." in relative.parts:
            raise ValueError("unsafe input path")
        path = root / relative
        if any(p.is_symlink() or p.is_junction() for p in (path, *path.parents)):
            raise ValueError("linked inputs refused")
        if not path.resolve().is_relative_to(root):
            raise ValueError("input escapes source")
        data = path.read_bytes()
        self.data[str(path)] = data
        return data

    def unchanged(self):
        for path, data in self.data.items():
            if Path(path).read_bytes() != data:
                raise ValueError("source changed during assembly")


def validate_report(report):
    provenance = report["provenance"]
    if (report["schema"], report["schema_version"]) != ("forge.blender-asset-kit-build", 1):
        raise ValueError("unsupported build report")
    if (not report["qualification"]["technical_export_validated"]
        or provenance["license"] != "LicenseRef-OpenRPG-Forge-Project-Generated"
        or provenance["source_kind"] not in ("procedural_original", "procedural_original_with_authored_edits")
        or provenance["cadle_content_used"] or provenance["commercial_game_content_used"]):
        raise ValueError("requires technically exported original Forge assets")


def load(source, addition):
    inputs = Inputs()
    build = json.loads(inputs.read(source, "build.json"))
    extra = json.loads(inputs.read(addition, "asset-kit-build.v1.json"))
    documents = {name: json.loads(inputs.read(source, name)) for name in ("preview.json", "prototype-catalog.json")}
    validate_report(build)
    validate_report(extra)
    for document in documents.values():
        if any(document[key] != build[key] for key in ("asset_kit_id", "asset_kit_revision")):
            raise ValueError("source document identity mismatch")
    return inputs, build, extra, documents


def asset_files(inputs, root, report):
    files = {}
    paths = set()
    for asset in report["assets"]:
        name, relative = asset["id"], asset["glb"]["path"]
        if name in files or relative in paths:
            raise ValueError("duplicate asset id or GLB path")
        data = inputs.read(root, relative)
        if sha(data) != asset["glb"]["sha256"]:
            raise ValueError("GLB pin mismatch")
        files[name] = asset
        paths.add(relative)
    if not files or len(files) > 1024:
        raise ValueError("asset count outside kit contract")
    return files


def write_kit(output, files, documents, attestation=None):
    output.mkdir(parents=True, exist_ok=False)
    for relative, data in files.items():
        target = output / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
        if sha(target.read_bytes()) != sha(data):
            raise ValueError("output copy mismatch")
    for name, document in documents.items():
        (output / name).write_bytes(json_bytes(document))
    if attestation is not None:
        (output / "replacement-attestation.json").write_bytes(json_bytes(attestation))


def assemble(source, addition, output):
    """Append an asset build without allowing a collision with existing pins."""
    source, addition, output = require_fresh_output(source, addition, output)
    inputs, build, extra, documents = load(source, addition)
    old_assets = copy.deepcopy(build["assets"])
    source_assets, extra_assets = asset_files(inputs, source, build), asset_files(inputs, addition, extra)
    source_paths = {asset["glb"]["path"] for asset in source_assets.values()}
    if any(asset_id in source_assets or asset["glb"]["path"] in source_paths for asset_id, asset in extra_assets.items()):
        raise ValueError("addition collides with an existing name or path; use --replace-existing")
    files = {asset["glb"]["path"]: inputs.data[str(source / asset["glb"]["path"])] for asset in source_assets.values()}
    files.update({asset["glb"]["path"]: inputs.data[str(addition / asset["glb"]["path"])] for asset in extra_assets.values()})
    build["assets"].extend(extra["assets"])
    build["asset_kit_revision"] += 1
    for document in documents.values():
        document["asset_kit_revision"] = build["asset_kit_revision"]
    preview_assets = documents["preview.json"]["assets"]
    if any(asset["id"] in preview_assets for asset in extra["assets"]):
        raise ValueError("addition would duplicate preview asset")
    preview_assets.extend(asset["id"] for asset in extra["assets"])
    build["qualification"].update(human_art_review="pending", publication_approved=False)
    build["additive_assembly"] = {
        "source_report_sha256": sha(inputs.data[str(source / "build.json")]),
        "addition_report_sha256": sha(inputs.data[str(addition / "asset-kit-build.v1.json")]),
        "addition_report": extra, "existing_pins_preserved": True,
        "scope": "local additive catalog revision; no placement replacement or runtime qualification",
        "script_sha256": sha(Path(__file__).read_bytes()),
    }
    if build["assets"][:len(old_assets)] != old_assets:
        raise ValueError("existing asset record changed")
    inputs.unchanged()
    documents["build.json"] = build
    write_kit(output, files, documents)
    print(f"ADDITIVE_KIT_ASSEMBLED preserved={len(old_assets)} added={len(extra_assets)} revision={build['asset_kit_revision']} placements_changed=0")


def replace_existing(source, replacement, output):
    """Replace only existing governed assets with exact id/path identity matches."""
    source, replacement, output = require_fresh_output(source, replacement, output)
    inputs, build, extra, documents = load(source, replacement)
    old_assets = copy.deepcopy(build["assets"])
    old_preview = copy.deepcopy(documents["preview.json"])
    old_catalog = copy.deepcopy(documents["prototype-catalog.json"])
    source_assets, replacement_assets = asset_files(inputs, source, build), asset_files(inputs, replacement, extra)
    replacements = {}
    for asset_id, candidate in replacement_assets.items():
        previous = source_assets.get(asset_id)
        if previous is None:
            raise ValueError("replacement asset id is absent from source kit")
        if previous["glb"]["path"] != candidate["glb"]["path"]:
            raise ValueError("replacement must keep the existing asset id and GLB path")
        replacements[asset_id] = candidate
    replacement_ids = set(replacements)
    if not replacement_ids:
        raise ValueError("at least one replacement asset is required")
    updated_assets = [copy.deepcopy(replacements.get(asset["id"], asset)) for asset in old_assets]
    if len({asset["id"] for asset in updated_assets}) != len(updated_assets):
        raise ValueError("replacement introduced duplicate asset id")
    if len({asset["glb"]["path"] for asset in updated_assets}) != len(updated_assets):
        raise ValueError("replacement introduced duplicate GLB path")
    preview_assets = documents["preview.json"]["assets"]
    if len(preview_assets) != len(set(preview_assets)):
        raise ValueError("source preview already contains duplicate asset ids")
    if any(asset_id not in preview_assets for asset_id in replacement_ids):
        raise ValueError("replacement asset is absent from source preview")
    files = {}
    for asset in updated_assets:
        asset_id, relative = asset["id"], asset["glb"]["path"]
        origin = replacement if asset_id in replacement_ids else source
        files[relative] = inputs.data[str(origin / relative)]
    build["assets"] = updated_assets
    source_revision = build["asset_kit_revision"]
    build["asset_kit_revision"] += 1
    for document in documents.values():
        document["asset_kit_revision"] = build["asset_kit_revision"]
    build["qualification"].update(human_art_review="pending", publication_approved=False)
    attestation = {
        "schema": "forge.governed-kit-replacement-attestation",
        "schema_version": 1,
        "source_report_sha256": sha(inputs.data[str(source / "build.json")]),
        "replacement_report_sha256": sha(inputs.data[str(replacement / "asset-kit-build.v1.json")]),
        "source_revision": source_revision,
        "output_revision": build["asset_kit_revision"],
        "replacements": [{
            "id": asset_id,
            "glb_path": replacements[asset_id]["glb"]["path"],
            "previous_glb_sha256": source_assets[asset_id]["glb"]["sha256"],
            "replacement_glb_sha256": replacements[asset_id]["glb"]["sha256"],
        } for asset_id in sorted(replacement_ids)],
        "non_replaced_asset_records_preserved": True,
        "preview_assets_preserved_without_duplicates": True,
        "preview_placements_preserved": True,
        "prototype_catalog_preserved": True,
        "human_art_review": "pending",
        "publication_approved": False,
        "scope": "local governed replacement; no placement mutation or runtime qualification",
        "script_sha256": sha(Path(__file__).read_bytes()),
    }
    build["governed_replacement"] = attestation
    if [asset for asset in build["assets"] if asset["id"] not in replacement_ids] != [asset for asset in old_assets if asset["id"] not in replacement_ids]:
        raise ValueError("non-replaced asset record changed")
    if documents["preview.json"]["assets"] != old_preview["assets"] or documents["preview.json"]["placements"] != old_preview["placements"]:
        raise ValueError("preview pins changed")
    old_catalog["asset_kit_revision"] = documents["prototype-catalog.json"]["asset_kit_revision"]
    if documents["prototype-catalog.json"] != old_catalog:
        raise ValueError("prototype catalog changed")
    inputs.unchanged()
    documents["build.json"] = build
    write_kit(output, files, documents, attestation)
    print(f"REPLACEMENT_KIT_ASSEMBLED preserved={len(old_assets) - len(replacement_ids)} replaced={len(replacement_ids)} revision={build['asset_kit_revision']} placements_changed=0")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-kit", type=Path, required=True)
    parser.add_argument("--addition-build", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--replace-existing", action="store_true", help="replace exact existing asset id/path pins instead of appending")
    args = parser.parse_args()
    (replace_existing if args.replace_existing else assemble)(args.source_kit, args.addition_build, args.output_dir)
