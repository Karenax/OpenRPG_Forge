"""Join verified native hierarchy/LOD/material slots to exact mesh derivatives."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import sys

W = Path(r"F:\OpenRPG_ImportWork")
GEOMETRY = W / "nested-prefab-captured-geometry-derivatives-v1"
HIERARCHY = W / "hivemind-prefab-instance-source-closure-v1"
ROOT = W / "nested-prefab-scene-bindings-v1"

def read(p):
    return json.loads(p.read_text("utf-8-sig"))

def sha(p):
    digest = hashlib.sha256()
    with Path(p).open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()

def identity(ref):
    if ref.get("is_null") or not ref.get("exact_asset_identity_available"):
        raise ValueError("Exact native identity unavailable")
    if not isinstance(ref.get("guid"), str) or len(ref["guid"]) != 32:
        raise ValueError("Native GUID absent")
    return ref["guid"], str(ref["file_id"])

def bind_renderer(asset_id, node, renderer, derivative):
    if not renderer.get("static_mesh_adapter_applicable"):
        raise ValueError("Nonstatic renderer requires its own adapter")
    key = identity(renderer["mesh"]["source"])
    expected = derivative["source_mesh_identity"]
    if key != (expected["guid"], expected["file_id"]):
        raise ValueError("Different mesh GUID/fileID")
    aliases = [a for a in derivative["prefab_instance_aliases"]
               if a["asset_id"] == asset_id and a["node_path"] == node["path"]
               and identity(a["renderer"]) == identity(renderer["component"])]
    if len(aliases) != 1:
        raise ValueError("Native renderer identity/instance alias unresolved")
    alias = aliases[0]
    if alias["original_material_slots"] != renderer["original_material_slots"]:
        raise ValueError("Original material slot order changed")
    for field in ["enabled", "shadow_casting_mode", "receive_shadows"]:
        if alias[field] != renderer[field]:
            raise ValueError(f"Native renderer state differs: {field}")
    if renderer["mesh"]["vertices"] != derivative["vertices"]:
        raise ValueError("Native vertex count differs")
    return {"node_path": node["path"], "renderer_identity": renderer["component"],
            "source_mesh_identity": renderer["mesh"]["source"],
            "geometry_derivative": derivative["derivative"],
            "geometry_receipt_sha256": derivative["geometry_receipt_sha256"],
            "original_material_slots": renderer["original_material_slots"],
            "source_submesh_count": derivative["submeshes"],
            "material_draw_mapping_requires_native_qualification": len(renderer["original_material_slots"]) != derivative["submeshes"],
            "material_program_qualified": False, "render_qualified": False}

def reflected_column_major(matrix):
    if len(matrix) != 16:
        raise ValueError("Native transform matrix width differs")
    # C M C, where C is the X reflection proved by the geometry converter.
    # Only sign changes and storage order change; no decomposition or tolerance.
    return [(-matrix[row * 4 + column] if (row == 0) != (column == 0)
             else matrix[row * 4 + column]) for column in range(4) for row in range(4)]

def run():
    if ROOT.exists():
        raise RuntimeError("Existing prefab assembly evidence preserved")
    geometry_path = GEOMETRY / "RUN-RESULT.v1.json"
    geometry = read(geometry_path)
    if geometry["status"] != "captured_geometry_derivatives_reopened" or not geometry["original_sources_unchanged"]:
        raise RuntimeError("Complete derivative bank and final source guards required")
    proof_path = HIERARCHY / "HIERARCHY-REOPEN.v1.json"
    proof = read(proof_path)
    if not proof["source_and_capture_pins_unchanged_before_after"]:
        raise RuntimeError("Verified native hierarchy absent")
    packs = sorted({row["pack_id"] for row in geometry["rows"]})
    pins = {str(p): sha(p) for p in [geometry_path, proof_path, Path(__file__).resolve()]}
    for path, expected in geometry["source_pins"].items():
        if sha(path) != expected:
            raise RuntimeError(f"Closed geometry source changed: {path}")
        pins[path] = expected
    index = {}
    for row in geometry["rows"]:
        expected = row["source_mesh_identity"]
        key = (row["pack_id"], expected["guid"], expected["file_id"])
        if key in index:
            raise RuntimeError("Duplicate mesh identity")
        index[key] = row
        path = Path(row["derivative"]) / "GEOMETRY.v1.json"
        if sha(path) != row["geometry_receipt_sha256"]:
            raise RuntimeError("Derivative receipt changed")
        pins[str(path)] = row["geometry_receipt_sha256"]
        receipt = read(path)
        for name, details in receipt["derived_streams"].items():
            path = Path(row["derivative"]) / name
            if sha(path) != details["sha256"]:
                raise RuntimeError("Derivative stream changed")
            pins[str(path)] = details["sha256"]
    rows, errors = [], []
    for pack in packs:
        slug = pack.removeprefix("hivemind.")
        capture_path = HIERARCHY / "projects" / f"{slug}.HIERARCHY-RESULT.v1.json"
        expected = proof["proof_pins"].get(str(capture_path))
        if not expected or sha(capture_path) != expected:
            raise RuntimeError("Native hierarchy snapshot not pinned")
        pins[str(capture_path)] = expected
        captured = read(capture_path)
        if captured["failed"] or captured["purchased_scripts_copied_or_executed"]:
            raise RuntimeError("Native prefab capture refused")
        for case in captured["cases"]:
            bindings, deferred = [], []
            for node in case["nodes"]:
                for renderer in node["renderers"]:
                    if not renderer.get("static_mesh_adapter_applicable"):
                        deferred.append({"node_path": node["path"], "renderer": renderer,
                                         "reason": "nonstatic_renderer_requires_L05_or_L06"})
                        continue
                    try:
                        key = (pack, *identity(renderer["mesh"]["source"]))
                        if key not in index:
                            raise ValueError("Native mesh has no closed geometry derivative yet")
                        bindings.append(bind_renderer(case["asset_id"], node, renderer, index[key]))
                    except ValueError as error:
                        errors.append({"asset_id": case["asset_id"], "node_path": node["path"], "error": str(error)})
            nodes = [{"source_node": node,
                      "target_local_matrix_column_major": reflected_column_major(node["source_local_matrix_row_major"]),
                      "target_source_world_matrix_column_major": reflected_column_major(node["source_local_to_world_matrix_row_major"])}
                     for node in case["nodes"]]
            rows.append({"asset_id": case["asset_id"], "pack_id": pack, "nodes": nodes,
                         "static_renderer_bindings": bindings, "deferred_renderers": deferred,
                         "original_lod_groups": case["lod_groups"], "original_root": case["root"],
                         "original_components_colliders_and_inactive_nodes_preserved": True,
                         "glb_created": False, "full_import": False})
    for path, expected in pins.items():
        if sha(path) != expected:
            raise RuntimeError(f"Input changed during assembly: {path}")
    ROOT.mkdir()
    result = {"schema": "openrpg.native-prefab-scene-bindings.v1", "checked_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "status": "source_scene_bindings_prepared" if not errors else "partial_source_scene_bindings_with_refusals",
              "prefabs": len(rows), "static_renderer_bindings": sum(len(row["static_renderer_bindings"]) for row in rows),
              "deferred_nonstatic_renderers": sum(len(row["deferred_renderers"]) for row in rows),
              "rows": rows, "errors": errors, "source_pins": pins,
              "source_and_derivative_pins_unchanged": True, "material_programs_qualified": False,
              "glb_created": False, "studio_verified": False, "full_import": False}
    (ROOT / "RESULT.v1.json").write_text(json.dumps(result, indent=2, ensure_ascii=False, allow_nan=False), "utf-8")
    print(json.dumps({k: v for k, v in result.items() if k not in ["rows", "source_pins", "errors"]}))

def self_test():
    import copy
    mesh = {"is_null": False, "exact_asset_identity_available": True, "guid": "a" * 32, "file_id": "-42"}
    component = {**mesh, "guid": "b" * 32, "file_id": "17"}
    materials = [{**mesh, "guid": "c" * 32}, {**mesh, "guid": "d" * 32}]
    renderer = {"static_mesh_adapter_applicable": True, "mesh": {"source": mesh, "vertices": 3},
                "component": component, "original_material_slots": materials,
                "enabled": True, "shadow_casting_mode": 1, "receive_shadows": True}
    node = {"path": "root/0000"}
    derivative = {"source_mesh_identity": {"guid": "a" * 32, "file_id": "-42"},
                  "prefab_instance_aliases": [{"asset_id": "owned.fixture", "node_path": node["path"],
                    "renderer": component, "original_material_slots": materials,
                    "enabled": True, "shadow_casting_mode": 1, "receive_shadows": True}],
                  "vertices": 3, "submeshes": 2, "derivative": "owned-control", "geometry_receipt_sha256": "0" * 64}
    assert not bind_renderer("owned.fixture", node, renderer, derivative)["material_draw_mapping_requires_native_qualification"]
    cases = []
    bad = copy.deepcopy(renderer); bad["mesh"]["source"]["file_id"] = "42"
    cases.append(("signed_mesh_file_id_changed", bad, derivative))
    bad = copy.deepcopy(renderer); bad["component"]["file_id"] = "18"
    cases.append(("renderer_identity_changed", bad, derivative))
    bad = copy.deepcopy(renderer); bad["original_material_slots"].reverse()
    cases.append(("original_material_order_changed", bad, derivative))
    bad = copy.deepcopy(renderer); bad["static_mesh_adapter_applicable"] = False
    cases.append(("rig_or_effect_not_flattened", bad, derivative))
    bad = copy.deepcopy(derivative); bad["prefab_instance_aliases"] *= 2
    cases.append(("duplicate_instance_alias", renderer, bad))
    controls = ["exact_mesh_renderer_and_material_order"]
    for label, candidate_renderer, candidate_geometry in cases:
        try:
            bind_renderer("owned.fixture", node, candidate_renderer, candidate_geometry)
        except ValueError:
            controls.append(label)
        else:
            raise RuntimeError(f"Bad binding accepted: {label}")
    translated = [1, 0, 0, 2, 0, 1, 0, 3, 0, 0, 1, 4, 0, 0, 0, 1]
    target = reflected_column_major(translated)
    assert target[12:15] == [-2, 3, 4]
    assert [target[i] for i in [0, 5, 10, 15]] == [1, 1, 1, 1]
    controls.append("native_pose_reflection_and_column_major_order")
    print(json.dumps({"assembly_controls_passed": controls, "full_import": False}))

if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "self-test":
        self_test()
    else:
        run()
