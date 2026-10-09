"""Read preserved material dependencies; never execute purchased scripts."""
import datetime
import hashlib
import json
from pathlib import Path
import re

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "nested-prefab-material-dependency-inventory-v1"
REF = re.compile(r'\bguid\s*:\s*([0-9a-fA-F]{32})|"guid"\s*:\s*"([0-9a-fA-F]{32})"')

def sha(path):
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()

def read(path):
    return json.loads(path.read_text("utf-8-sig"))

def main():
    if ROOT.exists():
        raise RuntimeError("Existing material inventory preserved")
    plan_path = W / "nested-prefab-scene-bindings-v1/RESULT.v1.json"
    reopen_path = plan_path.parent / "SOURCE-REOPEN.v1.json"
    plan, reopen = read(plan_path), read(reopen_path)
    if reopen["status"] != "selected_prefab_hierarchy_and_geometry_bindings_reopened":
        raise RuntimeError("Native binding reopening required")
    pins = {str(p): sha(p) for p in [plan_path, reopen_path, Path(__file__).resolve()]}
    selected = {}
    for prefab in plan["rows"]:
        for renderer in prefab["static_renderer_bindings"]:
            for slot_index, ref in enumerate(renderer["original_material_slots"]):
                if ref.get("is_null"):
                    continue
                if not ref.get("exact_asset_identity_available"):
                    raise RuntimeError("Native material identity absent")
                key = (prefab["pack_id"], ref["guid"], str(ref["file_id"]))
                item = selected.setdefault(key, {"pack_id": key[0], "material": ref, "uses": []})
                if item["material"] != ref:
                    raise RuntimeError("Conflicting native material identity")
                item["uses"].append({"asset_id": prefab["asset_id"], "node_path": renderer["node_path"],
                                     "renderer_identity": renderer["renderer_identity"], "slot_index": slot_index})
    manifests, checked, rows = {}, {}, []
    for key, item in sorted(selected.items()):
        pack = key[0]
        base = W / "hivemind-run" / pack.removeprefix("hivemind.")
        if pack not in manifests:
            manifest_path = base / "source.v1.json"
            manifest = read(manifest_path)
            pins[str(manifest_path)] = sha(manifest_path)
            manifests[pack] = (manifest, {f["stored_path"].replace("\\", "/"): f for f in manifest["files"]})
        manifest, files = manifests[pack]
        pending = [key[1]]
        visited, dependencies, unresolved = set(), [], []
        while pending:
            guid = pending.pop()
            if guid in visited:
                continue
            visited.add(guid)
            asset_path = manifest["guid_paths"].get(guid)
            if asset_path is None:
                unresolved.append({"guid": guid, "reason": "not_in_preserved_pack_manifest"})
                continue
            if guid == key[1] and asset_path != item["material"]["asset_path"]:
                raise RuntimeError("Native material path differs from source manifest")
            verified_files = []
            for suffix in ["", ".meta"]:
                relative = "source/" + asset_path + suffix
                record = files.get(relative)
                if record is None:
                    unresolved.append({"guid": guid, "asset_path": asset_path, "reason": "preserved_payload_or_meta_absent", "suffix": suffix})
                    continue
                path = (base / relative).resolve()
                if not path.is_relative_to((base / "source").resolve()):
                    raise RuntimeError("Source path escapes preserved pack")
                if str(path) not in checked:
                    if path.stat().st_size != record["bytes"] or sha(path) != record["sha256"]:
                        raise RuntimeError(f"Preserved source mismatch: {path}")
                    checked[str(path)] = record["sha256"]
                pins[str(path)] = record["sha256"]
                verified_files.append({"path": str(path), "sha256": record["sha256"], "bytes": record["bytes"]})
                if suffix == "" and path.suffix.lower() in {".mat", ".shadergraph", ".shadersubgraph", ".shader", ".hlsl", ".cginc"}:
                    text = path.read_text("utf-8-sig")
                    pending.extend((a or b).lower() for a, b in REF.findall(text) if (a or b) != "0" * 32)
            dependencies.append({"guid": guid, "asset_path": asset_path, "verified_files": verified_files})
        rows.append({**item, "dependencies": dependencies, "unresolved_references": unresolved,
                     "program_qualified": False, "render_qualified": False})
    for path, expected in pins.items():
        if sha(Path(path)) != expected:
            raise RuntimeError(f"Input changed during inventory: {path}")
    result = {"status": "preserved_material_dependency_inventory_reopened", "checked_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "materials": len(rows), "unique_payloads_and_meta_reopened": len(checked),
              "materials_with_unresolved_references": sum(bool(row["unresolved_references"]) for row in rows),
              "rows": rows, "source_pins": pins, "all_input_pins_unchanged": True,
              "scope": "serialized GUID dependencies only; builtin/package GUIDs, HLSL include names and render parity require separate qualification",
              "material_programs_qualified": False, "glb_created": False, "studio_verified": False, "full_import": False}
    ROOT.mkdir()
    (ROOT / "RESULT.v1.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), "utf-8")
    print(json.dumps({k: v for k, v in result.items() if k not in {"rows", "source_pins"}}))

if __name__ == "__main__":
    main()
