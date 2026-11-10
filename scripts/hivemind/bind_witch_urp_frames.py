"""Bind audited Witch House URP carriers to the Forge authoring frame."""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import subprocess

REFLECT_Z = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, -1, 0, 0, 0, 0, 1]
CONVENTION = (
    "C=diag(1,1,-1,1); positions/normals/tangentZ reflected; "
    "tangentW negated; triangle winding reversed; node=C*M*C"
)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def require(ok, reason):
    if not ok:
        raise ValueError(reason)


def split(raw):
    require(len(raw) >= 28, "Truncated GLB")
    magic, version, total, size, kind = struct.unpack_from("<4sIIII", raw)
    require(
        (magic, version, total, kind) == (b"glTF", 2, len(raw), 0x4E4F534A)
        and 0 < size <= 16 * 1024 * 1024
        and size % 4 == 0
        and size + 28 <= len(raw),
        "Invalid GLB JSON chunk",
    )
    end = size + 20
    size, kind = struct.unpack_from("<II", raw, end)
    binary = memoryview(raw)[end + 8:]
    require(kind == 0x004E4942 and size == len(binary), "Invalid GLB BIN chunk")
    return json.loads(raw[20:end]), binary


def attribute(doc, binary, index, width):
    a = doc["accessors"][index]
    view = doc["bufferViews"][a["bufferView"]]
    require(
        a["componentType"] == 5126 and a["type"] == f"VEC{width}"
        and not a.get("normalized") and "sparse" not in a
        and view["buffer"] == 0 and view.get("byteStride", 4 * width) == 4 * width,
        "Unexpected vertex stream format",
    )
    count = a["count"]
    size = count * width * 4
    offset = a.get("byteOffset", 0)
    start = view.get("byteOffset", 0) + offset
    require(
        count > 0 and offset >= 0 and start >= 0
        and offset + size <= view["byteLength"] and start + size <= len(binary),
        "Vertex stream out of bounds",
    )
    return binary[start:start + size]


def bind(row, path, verifier):
    raw = Path(row["path"]).read_bytes()
    require(len(raw) == row["bytes"] and sha(raw) == row["sha256"], "Input carrier drift")
    doc, binary = split(raw)
    extras = doc["extras"]
    require(extras["asset_id"] == row["id"], "Wrong asset ID")
    require(extras["source_to_candidate"] == CONVENTION, "Unsupported geometry convention")
    require("source_frame_binding" not in extras, "Refusing to replace an existing frame")
    require(doc["extensionsRequired"] == ["OPENRPG_source_graph"], "Expected source graph carrier")
    for mesh in doc["meshes"]:
        for primitive in mesh["primitives"]:
            attrs = primitive["attributes"]
            for name, width in [("NORMAL", 3), ("TANGENT", 4)]:
                target = attribute(doc, binary, attrs[name], width)
                source = attribute(doc, binary, attrs[f"_OPENRPG_SOURCE_{name}"], width)
                reflected = bytearray(target)
                for i in range(0, len(target), 4 * width):
                    reflected[i + 11] ^= 0x80
                    if width == 4:
                        reflected[i + 15] ^= 0x80
                require(reflected == source, f"{name} does not match audited Z reflection")
    extras["source_frame_binding"] = {
        "schema": "openrpg.source-frame-binding.v1",
        "target_object_from_source_object": REFLECT_Z,
        "source_object_from_target_object": REFLECT_Z,
        # The Unity equivalent of a placement in Forge has the same origin,
        # meters and up axis, with its world Z reflected just like the mesh.
        # Studio's temporary preview translation is handled by the host.
        "source_world_from_target_world": REFLECT_Z,
        "source_absolute_world_from_target_world": REFLECT_Z,
        "world_policy": "Witch House URP authoring world, coincident origins, no origin rebasing",
    }
    js = json.dumps(doc, ensure_ascii=False, separators=(",", ":")).encode()
    js += b" " * (-len(js) % 4)
    with path.open("xb") as output:
        output.write(struct.pack("<4sIIII", b"glTF", 2, 28 + len(js) + len(binary), len(js), 0x4E4F534A))
        output.write(js)
        output.write(struct.pack("<II", len(binary), 0x004E4942))
        output.write(binary)
    reopened = path.read_bytes()
    reopened_doc, reopened_bin = split(reopened)
    require(reopened_bin == binary and reopened_doc == doc, "GLB payload changed")
    reopened_doc["extras"].pop("source_frame_binding")
    require(reopened_doc == split(raw)[0], "Unrelated JSON changed")
    output_sha = sha(reopened)
    report_path = path.with_suffix(".reopen.json")
    native = subprocess.run(
        [str(verifier), str(path), output_sha, str(report_path), "--large-desktop"],
        capture_output=True, check=False,
    )
    require(native.returncode == 0, native.stderr.decode(errors="replace").strip())
    report = json.loads(report_path.read_bytes())
    require(report["carrier_sha256"] == output_sha, "Native report pin differs")
    return {
        "id": row["id"], "group": row["group"], "path": str(path),
        "bytes": len(reopened), "sha256": output_sha, "input_sha256": row["sha256"],
        "unchanged_bin_sha256": sha(binary), "native_report_sha256": sha(report_path.read_bytes()),
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--verifier", required=True, type=Path)
    parser.add_argument("--verifier-sha256", required=True)
    parser.add_argument("receipts", type=Path, nargs=2)
    args = parser.parse_args()
    require(sha(args.verifier.read_bytes()) == args.verifier_sha256, "Verifier pin differs")
    receipts = [path.read_bytes() for path in args.receipts]
    rows = [row for raw in receipts for row in json.loads(raw)["models"]]
    require(len(rows) == len({row["id"] for row in rows}) == 34, "Expected 34 distinct URP graph rows")
    require(sum(row["group"] == 14 for row in rows) == 3, "Expected three chain LODs")
    args.output.mkdir(parents=True, exist_ok=False)
    results = [bind(row, args.output / f"{i:02d}.glb", args.verifier) for i, row in enumerate(rows)]
    report = {
        "status": "EXPLICIT_FRAMES_REOPENED_NOT_STUDIO_QUALIFIED",
        "receipt_sha256": [sha(raw) for raw in receipts],
        "verifier_sha256": args.verifier_sha256,
        "models": results,
    }
    (args.output / "RESULT.v1.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"{len(results)} carriers reopened")


if __name__ == "__main__":
    main()
