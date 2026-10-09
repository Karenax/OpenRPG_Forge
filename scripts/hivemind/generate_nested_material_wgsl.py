"""Generate checked WGSL kernels without guessing texture defaults or normals."""
import datetime
import hashlib
import json
import math
from pathlib import Path
import subprocess
import traceback

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "nested-prefab-material-wgsl-v1"
EMITTER = W / "dungeon-original-material-wgsl-v1/adapted-node-emitter.py.txt"
BASE = W / "native-hdrp-nine-source-programs-v1"
KERNEL = BASE / "house-on-hill-source-shader-kernel-v1/source_house_graph_kernels.wgsl"
EXTRA = BASE / "house-on-hill-extra-node-kernels-v2/source_extra_nodes.wgsl"
VALIDATOR = BASE / "rock-surface-kernel-port-v1/target/debug/openrpg-rock-kernel-validator.exe"

def read(p):
    return json.loads(Path(p).read_text("utf-8-sig"))

def sha(p):
    h = hashlib.sha256()
    with Path(p).open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()

def scalar(value):
    value = float(value)
    if not math.isfinite(value):
        raise ValueError("Nonfinite source scalar")
    return repr(value)

def vec(values, n=4):
    values = list(values)
    values += [0] * max(0, n - len(values))
    return f"vec{n}<f32>(" + ",".join(scalar(v) for v in values[:n]) + ")"

def main():
    if ROOT.exists():
        raise RuntimeError("Existing shader kernel evidence preserved")
    if sha(VALIDATOR) != "bb928cfed4c84b4918fc306a23fae8818e8d5a0fc85b0f9444edbc6be9c55e1b":
        raise RuntimeError("Pinned native shader validator changed")
    batch_path = W / "nested-prefab-material-active-ir-v1/RESULT.v1.json"
    batch = read(batch_path)
    if batch["status"] != "material_active_topology_compiled" or batch["compiled_materials"] != 16:
        raise RuntimeError("Closed sixteen-material active topology required")
    pins = {str(p): sha(p) for p in [batch_path, EMITTER, KERNEL, EXTRA, VALIDATOR, Path(__file__).resolve()]}
    for p, expected in batch["source_pins"].items():
        if sha(p) != expected:
            raise RuntimeError("Active topology input changed")
        pins[p] = expected
    emitter = EMITTER.read_text("utf-8-sig")
    prefix = KERNEL.read_text("utf-8-sig") + "\n" + EXTRA.read_text("utf-8-sig")
    ROOT.mkdir()
    rows, failures, manifests = [], [], {}
    for row in batch["rows"]:
        case = ROOT / row["pack_id"] / row["material_guid"]
        case.mkdir(parents=True)
        try:
            ir_path = Path(row["output"])
            if sha(ir_path) != row["output_sha256"]:
                raise RuntimeError("Active topology output changed")
            pins[str(ir_path)] = row["output_sha256"]
            ir = read(ir_path)
            pack = row["pack_id"]
            base = W / "hivemind-run" / pack.removeprefix("hivemind.")
            if pack not in manifests:
                manifest_path = base / "source.v1.json"
                manifest = read(manifest_path)
                pins[str(manifest_path)] = sha(manifest_path)
                manifests[pack] = (manifest, {f["stored_path"].replace("\\", "/"): f for f in manifest["files"]})
            manifest, files = manifests[pack]
            resources, normals = [], set()
            for node in ir["nodes"]:
                for entry in node["inputs"]:
                    literal = entry["value"].get("literal")
                    if not isinstance(literal, dict) or "guid" not in literal:
                        continue
                    guid = literal["guid"]
                    if not guid or guid not in manifest["guid_paths"]:
                        raise RuntimeError("Active texture null/default or source unresolved; native adapter required")
                    if guid not in resources:
                        resources.append(guid)
                    if node["opcode"] == "SampleTexture2DNode" and node["source_parameters"].get("m_TextureType") == 1:
                        normals.add(guid)
            bindings = []
            for guid in resources:
                asset_path = manifest["guid_paths"][guid]
                payloads = []
                for suffix in ["", ".meta"]:
                    relative = "source/" + asset_path + suffix
                    original = (base / relative).resolve()
                    if not original.is_relative_to((base / "source").resolve()):
                        raise RuntimeError("Texture path outside preserved source pack")
                    record = files[relative]
                    if original.stat().st_size != record["bytes"] or sha(original) != record["sha256"]:
                        raise RuntimeError("Original texture or importer metadata changed")
                    pins[str(original)] = record["sha256"]
                    payloads.append({"path": str(original), "sha256": record["sha256"]})
                bindings.append({"guid": guid, "source_files": payloads, "requires_normal_decode": guid in normals,
                                 "normal_decode_mode_qualified": False, "imported_storage_qualified": False})
            context = {"ir": ir, "indices": {guid: i for i, guid in enumerate(resources)},
                       "normal_guids": normals, "dims": {}, "body": [], "input_requirements": [],
                       "prefix": prefix, "case": case, "scalar": scalar, "vec": vec, "math": math}
            exec(compile(emitter, str(EMITTER), "exec"), context)
            output = case / "source_material.wgsl"
            with (case / "naga.stdout.log").open("xb") as stdout, (case / "naga.stderr.log").open("xb") as stderr:
                process = subprocess.run([str(VALIDATOR), str(output)], stdout=stdout, stderr=stderr,
                                         creationflags=subprocess.CREATE_NO_WINDOW)
            if process.returncode != 0:
                raise RuntimeError(f"Native Naga validation refused, exit {process.returncode}")
            rows.append({"pack_id": pack, "material_guid": row["material_guid"], "wgsl": str(output),
                         "wgsl_sha256": sha(output), "active_ir_sha256": row["output_sha256"],
                         "actual_naga_exit_code": process.returncode, "source_texture_bindings": bindings,
                         "input_requirements": context["input_requirements"], "terminal_fields": context["terminal_fields"],
                         "texture_count": len(resources), "generated_texture_defaults": False,
                         "source_normal_decode_uniforms_require_host_qualification": True,
                         "render_qualified": False})
        except Exception as error:
            (case / "failure.log").write_text(traceback.format_exc(), "utf-8")
            failures.append({"pack_id": row["pack_id"], "material_guid": row["material_guid"], "error": str(error)})
        print(json.dumps({"generated": len(rows), "failed": len(failures), "total": 16}), flush=True)
    for p, expected in pins.items():
        if sha(p) != expected:
            raise RuntimeError("Input changed during shader kernel generation")
    result = {"status": "source_material_wgsl_kernels_naga_validated" if not failures else "source_material_wgsl_partial_refusals",
              "finished_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "requested_materials": 16,
              "naga_validated_materials": len(rows), "failed_materials": len(failures), "rows": rows,
              "failures": failures, "source_pins": pins, "existing_node_emitter_unchanged": True,
              "original_sources_and_helpers_unchanged": True, "gpu_rendered": False,
              "source_render_parity": False, "studio_verified": False, "full_import": False}
    (ROOT / "RESULT.v1.json").write_text(json.dumps(result, indent=2), "utf-8")
    print(json.dumps({k: v for k, v in result.items() if k not in {"rows", "failures", "source_pins"}}))

if __name__ == "__main__":
    main()
