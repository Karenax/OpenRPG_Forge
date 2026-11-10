"""Transport the two verified zero-thickness Unity BoxColliders as surfaces."""
import argparse
import json
from pathlib import Path
import re
import struct

from bind_witch_urp_frames import require, sha, split

SOURCE = (
    ("Plane_1", "e99311922b92a62a0aa25c073184dc53b76d2f92f0b94560505d7ebd71c32fe1",
     "witch-urp-frame-bindings-v1/r1/chat-mask-uvfix.glb",
     "ae7cc739d17f5052b798fc060c1ffe7a88dccd7549385cf34ff0080a0607646f"),
    ("Plane_2", "2c92f909bce7d04ee8185e4d465145709cde56f2922703055c1bf67747891f1b",
     "witch-feather-specular-linear-v1/r1/plume-specular-openrpg.glb",
     "1f2d22203d4ab9645dbff0ae5a575287a0b0d0917687b0ad6d23d8a7da7af7b5"),
)


def parse_source(raw):
    chunks = re.split(r"(?m)^--- !u!(\d+) &(\d+)\n", raw.decode("utf-8").replace("\r\n", "\n"))
    records = {int(key): (int(kind), body) for kind, key, body in zip(chunks[1::3], chunks[2::3], chunks[3::3])}

    def field(body, name):
        match = re.search(r"(?m)^  " + name + r": (.+)$", body)
        require(match, f"Missing {name}")
        return match[1]

    def vector(body, name):
        return [float(x.split(": ", 1)[1]) for x in field(body, name).strip("{}").split(", ")]

    def ref(body, name):
        return int(re.fullmatch(r"\{fileID: (\d+)\}", field(body, name))[1])

    [(collider_id, body)] = [(key, body) for key, (kind, body) in records.items() if kind == 65]
    owner = ref(body, "m_GameObject")
    require(
        field(body, "m_Enabled") == "1" and field(body, "m_IsTrigger") == "0"
        and ref(body, "m_Material") == 0
        and field(records[owner][1], "m_IsActive") == "1"
        and vector(body, "m_Size") == [1, 1, 0] and vector(body, "m_Center") == [0, 0, 0],
        "Outside the validated flat box profile",
    )
    [node] = [node for kind, node in records.values() if kind == 4 and ref(node, "m_GameObject") == owner]
    parent_kind, parent = records[ref(node, "m_Father")]
    require(parent_kind == 4 and ref(parent, "m_Father") == 0, "Expected one child of root")
    require(
        vector(parent, "m_LocalRotation") == [0, 0, 0, 1]
        and field(records[ref(parent, "m_GameObject")][1], "m_IsActive") == "1"
        and all(vector(n, "m_LocalPosition") == [0, 0, 0] and vector(n, "m_LocalScale") == [1, 1, 1]
                for n in (node, parent)),
        "Outside the verified parent transform",
    )
    x, y, z, w = vector(node, "m_LocalRotation")
    require([x, y, z, w] == [-0.5, -0.5, -0.5, 0.5], "Outside the verified collider orientation")
    return str(collider_id), [-x, -y, z, w]


def bind(raw, prefab_name, prefab_sha, source):
    document, original_bin = split(raw)
    require(
        document["extras"]["source_prefab"].endswith(f"/{prefab_name}.prefab")
        and document.get("scene", 0) == 0
        and len(document["scenes"]) == 1,
        "Mismatched carrier",
    )
    binary = bytearray(original_bin)
    original_hash = sha(original_bin)
    component_id, rotation = source
    collision_name = f'UTM_{document["extras"]["asset_id"]}_{component_id}'
    def accessor(payload, kind, count, shape, target, bounds=None):
        view = len(document["bufferViews"])
        document["bufferViews"].append(
            dict(buffer=0, byteOffset=len(binary), byteLength=len(payload), target=target)
        )
        binary.extend(payload)
        item = dict(bufferView=view, componentType=kind, count=count, type=shape)
        if bounds:
            item.update(min=bounds[0], max=bounds[1])
        document["accessors"].append(item)
        return len(document["accessors"]) - 1
    positions = accessor(
        struct.pack("<12f", -.5,-.5,0, .5,-.5,0, .5,.5,0, -.5,.5,0),
        5126, 4, "VEC3", 34962, ([-.5,-.5,0],[.5,.5,0]),
    )
    # Preserve the index buffer through Bevy's GLB loader. Missing normals
    # trigger vertex duplication even for a collision-only triangle list.
    normals = accessor(
        struct.pack("<12f", *([0.0, 0.0, -1.0] * 4)), 5126, 4, "VEC3", 34962,
    )
    indices = accessor(struct.pack("<6H", 0,2,1,0,3,2), 5123, 6, "SCALAR", 34963)
    document["scenes"][0]["nodes"].append(len(document["nodes"]))
    document["nodes"].append(dict(
        name=collision_name, mesh=len(document["meshes"]), rotation=rotation,
        extras=dict(source_role="collision", source_prefab_sha256=prefab_sha,
                    source_component_id=component_id, source_collider_type="BoxCollider",
                    openrpg_collision=dict(schema="source-collider.v1", kind="flat_box",
                                           source_dimensions_m=[1, 1, 0], target_geometry="triangle_mesh")),
    ))
    document["meshes"].append(dict(
        name=collision_name, primitives=[dict(attributes=dict(POSITION=positions, NORMAL=normals), indices=indices, mode=4)],
    ))
    require(sha(memoryview(binary)[:len(original_bin)]) == original_hash, "Original BIN changed")
    document["buffers"][0]["byteLength"] = len(binary)
    js = json.dumps(document, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    js += b" " * (-len(js) % 4)
    output = (struct.pack("<4sIIII", b"glTF", 2, 28 + len(js) + len(binary), len(js), 0x4E4F534A)
              + js + struct.pack("<II", len(binary), 0x004E4942) + binary)
    return output, document["extras"]["asset_id"], original_hash


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--jobs", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    results = []
    for name, expected, path, expected_carrier in SOURCE:
        prefab = (args.source / (name + ".prefab")).read_bytes()
        require(sha(prefab) == expected, f"Prefab SHA drift: {name}")
        carrier = (args.jobs / path).read_bytes()
        require(sha(carrier) == expected_carrier, f"Carrier SHA drift: {name}")
        output, asset_id, preserved = bind(carrier, name, expected, parse_source(prefab))
        results.append((name, output, dict(id=asset_id, sha256=sha(output),
                        original_carrier_sha256=sha(carrier), preserved_bin_prefix_sha256=preserved,
                        prefab_sha256=expected)))
    args.output.mkdir(parents=True, exist_ok=False)
    for name, output, row in results:
        path = args.output / f"{name}.glb"
        path.write_bytes(output)
        row["path"] = str(path.resolve())
        row["bytes"] = len(output)
    (args.output / "RESULT.v1.json").write_text(
        json.dumps(dict(status="FLAT_BOX_CARRIERS_NOT_STUDIO_QUALIFIED",
                        models=[row for _, _, row in results]), indent=2) + "\n", encoding="utf-8"
    )


if __name__ == "__main__":
    main()
