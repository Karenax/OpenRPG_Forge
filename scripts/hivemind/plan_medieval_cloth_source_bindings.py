"""Resolve declared cloth prefab renderers without confusing them with the FBX skin.

This is a source dependency plan, not a native material or rendered qualification.
"""
import hashlib
import json
from pathlib import Path
import re

SOURCE = Path(r"F:\OpenRPG_ImportWork\hivemind-run\medieval-kingdom\source")
ART = SOURCE / "Assets/Hivemind/MedievalKingdom/HDRP(Default)/Art"
OUT = Path(r"F:\OpenRPG_ImportWork\medieval-cloth-source-bindings-plan-v1")
MESH_GUID = "abc00000000011112206424264823986"


def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def documents(text):
    headers = list(re.finditer(r"^--- !u!(\d+) &(-?\d+)\s*$", text, re.M))
    return [(int(h.group(1)), h.group(2), text[h.end():headers[i + 1].start() if i + 1 < len(headers) else len(text)]) for i, h in enumerate(headers)]


def main():
    if OUT.exists():
        raise ValueError("Preserve prior source plan")
    index = {}
    for meta in SOURCE.rglob("*.meta"):
        if not meta.is_file():
            continue
        match = re.search(r"^guid:\s*([a-f0-9]{32})\s*$", meta.read_text("utf-8-sig"), re.M)
        if match:
            asset = Path(str(meta)[:-5])
            if asset.is_file():
                index.setdefault(match.group(1), []).append(asset)
    pins = {str(Path(__file__).resolve()): sha(__file__)}

    def retain(path):
        pins[str(path)] = sha(path)
        if Path(str(path) + ".meta").is_file():
            pins[str(path) + ".meta"] = sha(str(path) + ".meta")

    def resolve(guid):
        candidates = index.get(guid, [])
        if len(candidates) > 1:
            raise ValueError("Ambiguous GUID in source pack: " + guid)
        if not candidates:
            return {"guid": guid, "status": "unresolved_in_this_source_pack"}
        retain(candidates[0])
        return {"guid": guid, "status": "source_path_resolved", "path": str(candidates[0])}

    mesh = resolve(MESH_GUID)
    if mesh["status"] != "source_path_resolved":
        raise ValueError("Original cloth mesh source absent")
    prefabs = []
    for name in ["SKM_MarketCloth_01.prefab", "SKM_MarketCloth_01_1.prefab", "SKM_MarketCloth_01_2.prefab"]:
        path = ART / "Prefabs" / name
        retain(path)
        docs = documents(path.read_text("utf-8-sig"))
        if any(kind in [1001, 137] for kind, _, _ in docs):
            raise ValueError("Unexpected instance or skinned renderer requires its own resolver")
        renderers = [(identity, body) for kind, identity, body in docs if kind == 23]
        filters = [(identity, body) for kind, identity, body in docs if kind == 33]
        if len(renderers) != 1 or len(filters) != 1:
            raise ValueError("Unexpected cloth renderer/filter coverage")
        renderer_id, renderer = renderers[0]
        filter_id, mesh_filter = filters[0]
        gameobject = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", renderer).group(1)
        if re.search(r"m_GameObject: \{fileID: (-?\d+)\}", mesh_filter).group(1) != gameobject:
            raise ValueError("Filter/renderer owner mismatch")
        if f"m_Mesh: {{fileID: 4300000, guid: {MESH_GUID}, type: 3}}" not in mesh_filter:
            raise ValueError("Different native mesh source")
        materials = re.search(r"  m_Materials:\n((?:  - .*\n)+)", renderer)
        refs = re.findall(r"fileID: (-?\d+), guid: ([a-f0-9]{32}), type: (\d+)", materials.group(1))
        if len(refs) != 1 or refs[0][0] != "2100000":
            raise ValueError("Unexpected material slots")
        material = resolve(refs[0][1])
        if material["status"] != "source_path_resolved":
            raise ValueError("Declared cloth material absent")
        material_text = Path(material["path"]).read_text("utf-8-sig")
        shader = re.search(r"m_Shader: \{fileID: (-?\d+), guid: ([a-f0-9]{32}),\s*type: (\d+)\}", material_text)
        if not shader:
            raise ValueError("Incomplete source shader identity")
        shader_dependency = resolve(shader.group(2))
        shader_dependency["file_id"] = shader.group(1)
        texture_refs = []
        for prop, file_id, guid in re.findall(r"    - ([^:\n]+):\n        m_Texture: \{fileID: (-?\d+), guid: ([a-f0-9]{32}), type: \d+\}", material_text):
            # The regex emits property, fileID, GUID, retaining every serialized
            # reference even if an eventual native shader does not read it.
            dependency = resolve(guid)
            dependency.update(property=prop, file_id=file_id, active_native_read_verified=False)
            texture_refs.append(dependency)
        material.update(file_id="2100000", shader=shader_dependency, serialized_texture_references=texture_refs)
        prefabs.append({"path": str(path), "renderer_class": "MeshRenderer", "renderer_file_id": renderer_id, "mesh_filter_file_id": filter_id, "gameobject_file_id": gameobject, "mesh_guid": MESH_GUID, "mesh_file_id": "4300000", "material_slots": [material], "skin_applied_by_prefab_renderer": False, "collider_components": sum(kind == 64 for kind, _, _ in docs), "original_yaml_retained": True, "native_binding_verified": False})
    for path, expected in pins.items():
        if sha(path) != expected:
            raise ValueError("Source changed while planning")
    report = {"status": "three_static_cloth_prefab_source_bindings_resolved", "prefabs": prefabs, "fbx_mesh_source": mesh, "fbx_skin_capture_retained_separately": True, "input_pins": pins, "native_material_verified": False, "geometry_converted": False, "gpu_verified": False, "studio_verified": False, "full_import": False}
    OUT.mkdir()
    with (OUT / "PLAN.v1.json").open("x", encoding="utf-8") as file:
        json.dump(report, file, indent=2)
    print(json.dumps({"prefabs": len(prefabs), "materials": len({row['material_slots'][0]['guid'] for row in prefabs}), "unresolved_shader_references": sum(row['material_slots'][0]['shader']['status'] != 'source_path_resolved' for row in prefabs), "unresolved_serialized_texture_references": sum(ref['status'] != 'source_path_resolved' for row in prefabs for ref in row['material_slots'][0]['serialized_texture_references']), "native_verified": False, "full_import": False}))


if __name__ == "__main__":
    main()
