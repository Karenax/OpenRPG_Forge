"""Reconcile native HDRP material snapshot identities and compiled-pass evidence.

This checks captured API data. It cannot qualify images, GPU render parity,
original authoring-version parity, or availability in the editor.
"""
import argparse
import copy
import hashlib
import json
import math
from pathlib import Path
import re


def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def finite(value):
    return type(value) in [float, int] and math.isfinite(value)


def identity(value):
    return isinstance(value, str) and re.fullmatch(r"[a-f0-9]{32}", value) is not None


def reconcile(manifest, native):
    requests = manifest["cases"]
    rows = native["cases"]
    if not requests or len(rows) != len(requests) or native["requested"] != len(requests) or native["captured"] != len(requests) or native["failed"] != 0:
        raise ValueError("Complete native material coverage required")
    if native["selected_pipeline_type"] != "UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset" or native["graphics_api"] == "Null":
        raise ValueError("Actual selected HDRP/native graphics compiler context absent")
    if len({request["material_guid"] for request in requests}) != len(requests):
        raise ValueError("Duplicate requested material identity")
    verified = []
    for request, row in zip(requests, rows):
        if not identity(request["material_guid"]) or not identity(request["expected_shader_guid"]):
            raise ValueError("Malformed source GUID")
        shader_id = request["expected_shader_file_id"]
        if not isinstance(shader_id, str) or re.fullmatch(r"-?\d+", shader_id) is None or not -(2**63) <= int(shader_id) < 2**63:
            raise ValueError("Exact signed source fileID string required")
        if row["material_guid"] != request["material_guid"] or row["actual_material_file_id"] != "2100000" or row["shader_guid"] != request["expected_shader_guid"] or row["shader_file_id"] != shader_id:
            raise ValueError("Original material/shader identity differs")
        if row["active_render_pipeline_tag"] != "HDRenderPipeline" or row["error"] or row["shader_has_errors"] is not False or row["shader_supported"] is not True or row["compile_passes_executed"] is not True or not row["passes"]:
            raise ValueError("Original named HDRP pass coverage unverified")
        if type(row["render_queue"]) is not int or row["render_queue"] < 0:
            raise ValueError("Effective native render queue absent")
        if not isinstance(row["keywords"], list) or any(not isinstance(key, str) for key in row["keywords"]):
            raise ValueError("Captured native keyword list absent")
        for source_pass in row["passes"]:
            if not isinstance(source_pass["name"], str) or not source_pass["name"] or source_pass["name"].startswith("<Unnamed") or source_pass["compiled"] is not True or type(source_pass["enabled"]) is not bool:
                raise ValueError("Unnamed/uncompiled source pass or missing enable state")
        if any(message["severity"] == "Error" for message in row["messages"]):
            raise ValueError("Native shader compilation error retained")
        serialized = json.loads(row["native_material_json"])
        if not isinstance(serialized, dict):
            raise ValueError("Full serialized native material object required")
        properties = {}
        for prop in row["properties"]:
            name = prop["name"]
            if not name or name in properties:
                raise ValueError("Duplicate or empty native property")
            properties[name] = prop
            if prop["type"] not in ["Float", "Range", "Vector", "Color", "Texture", "Int"]:
                raise ValueError("Unknown source property representation")
            for key in ["native_float", "default_float"]:
                if not finite(prop[key]):
                    raise ValueError("Nonfinite native material scalar")
            for key in ["native_vector", "default_vector"]:
                if set(prop[key]) != {"x", "y", "z", "w"} or not all(finite(component) for component in prop[key].values()):
                    raise ValueError("Incomplete/nonfinite native material vector")
            if prop["type"] == "Texture" and (type(prop["texture_null"]) is not bool or type(prop["texture_exact_identity"]) is not bool):
                raise ValueError("Native texture observation state absent")
        for texture in request.get("required_textures", []):
            prop = properties[texture["property"]]
            if prop["type"] != "Texture" or prop["texture_null"] or not prop["texture_exact_identity"] or prop["texture_guid"] != texture["guid"] or prop["texture_file_id"] != texture["file_id"]:
                raise ValueError("Original authored texture binding differs")
        verified.append({"material_guid": row["material_guid"], "shader_guid": row["shader_guid"], "shader_file_id": shader_id, "passes": row["passes"], "property_count": len(properties), "render_queue": row["render_queue"], "keywords": row["keywords"], "null_texture_properties": sorted(name for name, prop in properties.items() if prop["type"] == "Texture" and prop["texture_null"]), "native_serialized_material_sha256": hashlib.sha256(row["native_material_json"].encode()).hexdigest()})
    return verified


def controls():
    request = {"material_guid": "a" * 32, "expected_shader_guid": "b" * 32, "expected_shader_file_id": "-6465566751694194690"}
    row = {"material_guid": request["material_guid"], "actual_material_file_id": "2100000", "shader_guid": request["expected_shader_guid"], "shader_file_id": request["expected_shader_file_id"], "active_render_pipeline_tag": "HDRenderPipeline", "error": "", "shader_has_errors": False, "shader_supported": True, "compile_passes_executed": True, "render_queue": 2000, "keywords": [], "passes": [{"name": "Forward", "compiled": True, "enabled": True}], "properties": [], "messages": [], "native_material_json": "{}"}
    zero = {axis: 0.0 for axis in ["x", "y", "z", "w"]}
    request["required_textures"] = [{"property": "_BaseMap", "guid": "d" * 32, "file_id": "2800000"}]
    row["properties"] = [{"name": "_BaseMap", "type": "Texture", "native_float": 0.0, "default_float": 0.0, "native_vector": dict(zero), "default_vector": dict(zero), "texture_null": False, "texture_exact_identity": True, "texture_guid": "d" * 32, "texture_file_id": "2800000"}]
    native = {"cases": [row], "requested": 1, "captured": 1, "failed": 0, "selected_pipeline_type": "UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset", "graphics_api": "Direct3D11"}
    manifest = {"cases": [request]}
    assert len(reconcile(manifest, native)) == 1
    mutations = [lambda r: r.update(shader_file_id=str(int(float(request["expected_shader_file_id"])))), lambda r: r.update(shader_guid="c" * 32), lambda r: r.update(active_render_pipeline_tag="UniversalPipeline"), lambda r: r.update(shader_has_errors=True), lambda r: r["passes"][0].update(name="<Unnamed Pass 0>"), lambda r: r["passes"][0].update(compiled=False), lambda r: r.update(render_queue=float("nan")), lambda r: r.update(messages=[{"severity": "Error"}])]
    mutations += [lambda r: r["properties"][0].update(texture_guid="e" * 32), lambda r: r["properties"].append(copy.deepcopy(r["properties"][0])), lambda r: r["properties"][0].update(native_float=float("nan")), lambda r: r["properties"][0]["native_vector"].pop("w")]
    for mutation in mutations:
        altered = copy.deepcopy(native)
        mutation(altered["cases"][0])
        try:
            reconcile(manifest, altered)
        except ValueError:
            continue
        raise AssertionError("Malformed native evidence accepted")
    return {"positive_fixture_controls": 1, "negative_fixture_controls": len(mutations), "native_materials_verified": 0, "full_import": False}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--self-test", action="store_true")
    parser.add_argument("--manifest", type=Path)
    parser.add_argument("--native", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    if args.self_test:
        print(json.dumps(controls()))
        return
    if not args.manifest or not args.native or not args.output:
        parser.error("manifest, native and output required")
    pins = {str(path.resolve()): sha(path) for path in [args.manifest, args.native, Path(__file__)]}
    rows = reconcile(json.loads(args.manifest.read_text("utf-8-sig")), json.loads(args.native.read_text("utf-8-sig")))
    for path, expected in pins.items():
        if sha(path) != expected:
            raise ValueError("Snapshot changed during independent reopening")
    report = {"status": "native_material_snapshot_reconciled", "native_materials_verified": len(rows), "rows": rows, "input_pins": pins, "original_source_files_reopened": False, "gpu_render_parity_qualified": False, "original_authoring_version_qualified": False, "studio_verified": False, "full_import": False}
    with args.output.open("x", encoding="utf-8") as file:
        json.dump(report, file, indent=2, allow_nan=False)
    print(json.dumps({"native_materials_verified": len(rows), "gpu_render_parity_qualified": False, "full_import": False}))


if __name__ == "__main__":
    main()
