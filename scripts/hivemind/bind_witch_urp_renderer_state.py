"""Bind a fixed Witch House LOD to its original Unity MeshRenderer."""
import argparse
import json
import re
import struct
import subprocess
from pathlib import Path

from bind_witch_urp_frames import require, sha, split


def field(block, key, pattern, optional=False):
    values = re.findall(rf"(?m)^  {key}: {pattern}$", block)
    require(len(values) == 1 or optional and not values, f"Ambiguous or absent {key}")
    return values[0] if values else None


def guid(meta):
    values = re.findall(r"(?m)^guid: ([0-9a-f]{32})\r?$", meta.decode("utf-8-sig"))
    require(len(values) == 1, "Expected one prefab GUID")
    return values[0]


def state(prefab, lod, node_name, slot_count):
    text = prefab.decode("utf-8-sig").replace("\r\n", "\n")
    blocks = re.split(r"(?m)^--- !u!(\d+) &(-?\d+)\n", text)[1:]
    records = dict(zip(blocks[1::3], zip(blocks[::3], blocks[2::3])))
    require(len(records) * 3 == len(blocks) and records, "Invalid Unity YAML document")
    require(all(kind != "1001" for kind, _ in records.values()), "Nested prefab not supported")

    def one(kind):
        values = [(key, data) for key, (tag, data) in records.items() if kind == tag]
        require(len(values) == 1, f"Expected one Unity class {kind}")
        return values[0]

    def obj(key, kind):
        tag, body = records[key]
        require(tag == kind, f"Expected Unity class {kind} for {key}")
        return body

    def link(body, key):
        return field(body, key, r"\{fileID: (-?\d+)\}")

    group_id, group = one("205")
    levels = re.split(r"(?m)^  - screenRelativeHeight: ", group)[1:]
    require(type(lod) is int and 0 <= lod < len(levels), "Missing authored LOD")
    ids = re.findall(r"(?m)^    - renderer: \{fileID: (-?\d+)\}$", levels[lod])
    require(len(ids) == 1, "Expected exactly one renderer in the selected LOD")
    renderer_id = ids[0]
    renderer = obj(renderer_id, "23")
    go_id = link(renderer, "m_GameObject")
    go = obj(go_id, "1")
    name = field(go, "m_Name", r"(.+)")
    require(node_name == f"{name}[{lod}]", "Unity name or LOD differs from carrier")
    require(len(re.findall(r"(?m)^  - \{fileID: -?\d+, guid: [0-9a-f]{32}, type: 2\}$",
                           renderer)) == slot_count, "Renderer slots differ from carrier")
    transforms = {key: value for key, (kind, value) in records.items() if kind == "4"}
    current = [key for key, t in transforms.items() if link(t, "m_GameObject") == go_id]
    require(len(current) == 1, "Ambiguous renderer Transform")
    visited = set()
    original_path = []
    current = current[0]
    while current != "0":
        require(current not in visited, "Cyclic source hierarchy")
        visited.add(current)
        t = transforms[current]
        ancestor = obj(link(t, "m_GameObject"), "1")
        require(field(ancestor, "m_IsActive", r"([01])") == "1", "Inactive source hierarchy")
        original_path.append(field(ancestor, "m_Name", r"(.+)"))
        current = link(t, "m_Father")
    force_off = field(renderer, "m_ForceRenderingOff", r"([01])", optional=True)
    return {
        "renderer_id": renderer_id,
        "lod_group_id": group_id,
        "source_node_path": original_path[::-1],
        "source_force_rendering_off_serialized": force_off is not None,
        "enabled": field(renderer, "m_Enabled", r"([01])") == "1",
        "force_rendering_off": force_off == "1",
        "shadow_casting_mode": int(field(renderer, "m_CastShadows", r"([01])")),
        "receive_shadows": field(renderer, "m_ReceiveShadows", r"([01])") == "1",
    }


def bind(row, args, target):
    raw = Path(row["path"]).read_bytes()
    require(len(raw) == row["bytes"] and sha(raw) == row["sha256"], "Input carrier drift")
    doc, binary = split(raw)
    extras = doc["extras"]
    require(extras["asset_id"] == row["id"] and "source_frame_binding" in extras, "Wrong carrier")
    nodes = doc["nodes"]
    require(len(nodes) == 1 and len(doc["scenes"]) == 1 and doc["scenes"][0]["nodes"] == [0]
            and not nodes[0].get("children"), "Expected flattened single-renderer LOD")
    node = nodes[0]
    require("unity_source_node" not in node.get("extras", {}) and "original_lod_groups" not in extras,
            "Refusing to overwrite existing Unity renderer or LOD state")
    reference = Path(extras["source_prefab"])
    path = args.source_project / reference
    original = args.original_prefabs / reference.name
    imported_meta = path.with_suffix(".prefab.meta").read_bytes()
    original_meta = original.with_suffix(".prefab.meta").read_bytes()
    require(guid(imported_meta) == extras["source_guid"], "Imported prefab GUID changed")
    source_guid = guid(original_meta)
    require(re.search(rf"-{source_guid}(?:-lod\d+)?$", row["id"]), "Original prefab identity changed")
    source, imported = original.read_bytes(), path.read_bytes()
    lod = extras["source_lod"]
    slots = len(doc["meshes"][node["mesh"]]["primitives"])
    authored = state(source, lod, node["name"], slots)
    require(authored == state(imported, lod, node["name"], slots),
            "Original prefab and Unity import disagree on renderer state")
    renderer = {k: authored[k] for k in
                ("enabled", "force_rendering_off", "shadow_casting_mode", "receive_shadows")}
    renderer["component"] = {
        "guid": source_guid, "file_id": authored["renderer_id"],
        "type": "UnityEngine.MeshRenderer", "exact_asset_identity_available": True,
    }
    node.setdefault("extras", {})["unity_source_node"] = {
        "path": "/".join(authored["source_node_path"]), "parent_path": None,
        "active_self": True, "active_source_parent_chain": True, "renderers": [renderer],
    }
    # The carrier is the already-selected LOD, not an instance of the full LODGroup.
    extras["original_lod_groups"] = []
    extras["fixed_lod_renderer_binding"] = {
        "unity_source": authored, "source_prefab_sha256": sha(source),
        "source_meta_sha256": sha(original_meta), "imported_prefab_sha256": sha(imported),
        "imported_meta_sha256": sha(imported_meta),
    }
    js = json.dumps(doc, ensure_ascii=False, separators=(",", ":")).encode()
    js += b" " * (-len(js) % 4)
    with target.open("xb") as output:
        output.write(struct.pack("<4sIIII", b"glTF", 2, 28 + len(js) + len(binary), len(js), 0x4E4F534A))
        output.write(js)
        output.write(struct.pack("<II", len(binary), 0x004E4942))
        output.write(binary)
    result = target.read_bytes()
    actual, payload = split(result)
    require(actual == doc and payload == binary, "Renderer binding changed GLB payload")
    digest = sha(result)
    report = target.with_suffix(".reopen.json")
    subprocess.run([str(args.verifier), str(target), digest, str(report), "--large-desktop"], check=True)
    require(json.loads(report.read_bytes())["carrier_sha256"] == digest, "Native reader pin differs")
    return {
        "id": row["id"], "group": row["group"], "path": str(target), "bytes": len(result),
        "sha256": digest, "input_sha256": row["sha256"], "unchanged_bin_sha256": sha(payload),
        "renderer": renderer, "fixed_lod": lod, "native_report_sha256": sha(report.read_bytes()),
        **extras["fixed_lod_renderer_binding"],
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for key in ("frames", "source-project", "original-prefabs", "output", "verifier"):
        parser.add_argument("--" + key, type=lambda p: Path(p).resolve(), required=True)
    args = parser.parse_args()
    receipt = args.frames.read_bytes()
    manifest = json.loads(receipt)
    require(sha(args.verifier.read_bytes()) == manifest["verifier_sha256"], "Native verifier drift")
    rows = manifest["models"]
    require(len(rows) == len({row["id"] for row in rows}) == 34, "Expected 34 distinct carriers")
    args.output.mkdir(parents=True, exist_ok=False)
    result = [bind(row, args, args.output / f"{i:02d}.glb") for i, row in enumerate(rows)]
    (args.output / "RESULT.v1.json").write_text(json.dumps({
        "status": "FIXED_LOD_RENDERER_STATE_REOPENED_NOT_STUDIO_QUALIFIED",
        "input_receipt_sha256": sha(receipt), "verifier_sha256": manifest["verifier_sha256"],
        "models": result,
    }, indent=2) + "\n", encoding="utf-8")
    print(f"{len(result)} models reopened with original renderer state")


if __name__ == "__main__":
    main()
