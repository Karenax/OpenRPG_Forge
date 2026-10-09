"""Reuse the existing topology compiler for the 16 pinned source materials."""
import datetime
import hashlib
import json
from pathlib import Path
import subprocess
import sys

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "nested-prefab-material-active-ir-v1"
COMPILER = W / "dungeon-original-material-family-closure-v1/compile_source_material_ir.py"

def read(p):
    return json.loads(Path(p).read_text("utf-8-sig"))

def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()

def documents(text):
    decoder, result, offset = json.JSONDecoder(), [], 0
    while offset < len(text):
        while offset < len(text) and text[offset].isspace():
            offset += 1
        if offset == len(text):
            break
        value, offset = decoder.raw_decode(text, offset)
        result.append(value)
    return result

def main():
    if ROOT.exists():
        raise RuntimeError("Existing material topology evidence preserved")
    binding_path = W / "nested-prefab-graph-property-bindings-v1/RESULT.v1.json"
    source_path = W / "nested-prefab-material-source-ir-v1/RESULT.v1.json"
    bindings, sources = read(binding_path), read(source_path)
    materials = {(r["pack_id"], r["material_identity"]["guid"]): r for r in sources["rows"]}
    pins = {str(p): sha(p) for p in [binding_path, source_path, COMPILER, Path(__file__).resolve()]}
    original = COMPILER.read_text("utf-8-sig")
    old_header = "B=Path(__file__).resolve().parent/sys.argv[2];Q=Path('F:/OpenRPG_ImportWork/queue/dungeon-builder/prefabs.json')"
    new_header = "B=Path(sys.argv[3]);Q=Path(sys.argv[4])"
    if original.count(old_header) != 1:
        raise RuntimeError("Existing compiler configuration header changed")
    adapted = original.replace(old_header, new_header)
    if adapted.replace(new_header, old_header) != original:
        raise RuntimeError("Compiler body changed")
    ROOT.mkdir()
    compiler_path = ROOT / "compile_source_material_ir.configured.v1.py"
    compiler_path.write_text(adapted, "utf-8")
    pins[str(compiler_path)] = sha(compiler_path)
    rows, contexts = [], {}
    for binding in bindings["rows"]:
        pack = binding["pack_id"]
        guid = binding["material_identity"]["guid"]
        source = materials[(pack, guid)]
        material_source = Path(source["source_material"]["path"])
        if sha(material_source) != source["source_material"]["sha256"]:
            raise RuntimeError("Material changed before topology compilation")
        pins[str(material_source)] = source["source_material"]["sha256"]
        graph_guid = binding["graph"]["source_shader_guid"]
        context_key = (pack, graph_guid)
        base = W / "hivemind-run" / pack.removeprefix("hivemind.")
        if context_key not in contexts:
            manifest_path = base / "source.v1.json"
            manifest = read(manifest_path)
            pins[str(manifest_path)] = sha(manifest_path)
            files = {f["stored_path"].replace("\\", "/"): f for f in manifest["files"]}
            pending, seen, graphs = [graph_guid], set(), []
            while pending:
                current = pending.pop(0)
                if current in seen:
                    continue
                seen.add(current)
                asset = manifest["guid_paths"].get(current)
                if not asset or not asset.endswith((".shadergraph", ".shadersubgraph")):
                    raise RuntimeError("Referenced shader subgraph source unavailable")
                for suffix in ["", ".meta"]:
                    relative = "source/" + asset + suffix
                    record = files[relative]
                    path = (base / relative).resolve()
                    if not path.is_relative_to((base / "source").resolve()) or sha(path) != record["sha256"]:
                        raise RuntimeError("Source shader graph or metadata changed")
                    pins[str(path)] = record["sha256"]
                graph_path = base / "source" / asset
                objects = documents(graph_path.read_text("utf-8-sig"))
                graphs.append({"guid": current, "path": str(graph_path), "sha256": sha(graph_path), "objects": objects})
                for obj in objects:
                    if obj.get("m_Type", "").endswith(".SubGraphNode"):
                        pending.append(json.loads(obj["m_SerializedSubGraph"])["subGraph"]["guid"])
            destination = ROOT / pack / graph_guid
            destination.mkdir(parents=True)
            (destination / "material-family-active-ir-v1").mkdir()
            closure_path = destination / "SHADER-SUBGRAPH-CLOSURE.v1.json"
            closure_path.write_text(json.dumps({"graphs": graphs}, indent=2), "utf-8")
            pins[str(closure_path)] = sha(closure_path)
            contexts[context_key] = destination
        destination = contexts[context_key]
        props = source["source_material_properties"]
        saved = props["m_SavedProperties"]
        floats, colors, textures = {}, {}, {}
        for entries, target in [(saved.get("m_Floats", []), floats), (saved.get("m_Ints", []), floats)]:
            for entry in entries:
                for name, value in entry.items():
                    if name in target:
                        raise RuntimeError("Duplicate scalar material property")
                    target[name] = value
        for entry in saved.get("m_Colors", []):
            for name, value in entry.items():
                colors[name] = [value[c] for c in ["r", "g", "b", "a"]]
        for entry in saved.get("m_TexEnvs", []):
            for name, value in entry.items():
                textures[name] = {"guid": value["m_Texture"].get("guid"),
                                  "scale": [value["m_Scale"][c] for c in ["x", "y"]],
                                  "offset": [value["m_Offset"][c] for c in ["x", "y"]]}
        queue = {"source_root": str(base / "source"), "materials": {guid: {
            "name": props["m_Name"], "source": source["material_identity"]["asset_path"],
            "sha256": source["source_material"]["sha256"], "shader_guid": graph_guid,
            "floats": floats, "colors": colors, "textures": textures,
            "keywords": props.get("m_ValidKeywords", [])}},
            "source_material_file_id": source["material_identity"]["file_id"],
            "null_texture_bindings_are_explicit_and_require_native_default_adapter": True}
        queue_path = destination / f"{guid}.queue.v1.json"
        queue_path.write_text(json.dumps(queue, indent=2, allow_nan=False), "utf-8")
        pins[str(queue_path)] = sha(queue_path)
        process = subprocess.run([sys.executable, str(compiler_path), guid, graph_guid, str(destination), str(queue_path)],
                                 capture_output=True, text=True, creationflags=subprocess.CREATE_NO_WINDOW)
        (destination / f"{guid}.stdout.log").write_text(process.stdout, "utf-8")
        (destination / f"{guid}.stderr.log").write_text(process.stderr, "utf-8")
        output_path = destination / "material-family-active-ir-v1" / f"{guid}.json"
        row = {"pack_id": pack, "material_guid": guid, "actual_compiler_exit_code": process.returncode,
               "output": str(output_path) if output_path.exists() else None,
               "material_conversion_qualified": False, "render_qualified": False}
        if process.returncode == 0:
            report = read(output_path)
            if report["source_material_sha256"] != queue["materials"][guid]["sha256"]:
                raise RuntimeError("Compiled material source pin differs")
            row.update(nodes=len(report["nodes"]), terminals=len(report["terminals"]), opcode_counts=report["opcode_counts"], output_sha256=sha(output_path))
        rows.append(row)
        print(json.dumps(row), flush=True)
    for path, expected in pins.items():
        if sha(path) != expected:
            raise RuntimeError("Input changed during source topology compilation")
    result = {"status": "material_active_topology_compiled" if all(r["actual_compiler_exit_code"] == 0 for r in rows) else "material_active_topology_partial_refusals",
              "checked_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "materials": len(rows),
              "compiled_materials": sum(r["actual_compiler_exit_code"] == 0 for r in rows), "rows": rows,
              "source_pins": pins, "compiler_body_unchanged_configuration_header_only": True,
              "original_source_compiler_sha256": pins[str(COMPILER)], "all_input_pins_unchanged": True,
              "source_program_semantics_gpu_qualified": False, "glb_created": False, "studio_verified": False, "full_import": False}
    (ROOT / "RESULT.v1.json").write_text(json.dumps(result, indent=2), "utf-8")
    print(json.dumps({k: v for k, v in result.items() if k not in {"rows", "source_pins"}}))

if __name__ == "__main__":
    main()
