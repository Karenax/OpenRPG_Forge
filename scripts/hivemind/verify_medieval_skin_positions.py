"""Compare every captured BakeMesh position against independent weighted skinning.

This proves the captured source position deformation only, not a GLB or GPU path.
"""
import hashlib
import json
import math
from pathlib import Path
import struct

ROOT = Path(r"F:\OpenRPG_ImportWork\unity-medieval-rig-source-capture-v1")
OUT = Path(r"F:\OpenRPG_ImportWork\medieval-skin-position-parity-v1")


def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def multiply(a, b):
    return [[sum(a[r][k] * b[k][c] for k in range(4)) for c in range(4)] for r in range(4)]


def inverse(a):
    rows = [list(row) + [float(r == c) for c in range(4)] for r, row in enumerate(a)]
    for c in range(4):
        pivot = max(range(c, 4), key=lambda r: abs(rows[r][c]))
        if abs(rows[pivot][c]) < 1e-12:
            raise ValueError("Singular captured matrix")
        rows[c], rows[pivot] = rows[pivot], rows[c]
        divisor = rows[c][c]
        rows[c] = [v / divisor for v in rows[c]]
        for r in range(4):
            if r != c:
                factor = rows[r][c]
                rows[r] = [v - factor * w for v, w in zip(rows[r], rows[c])]
    return [row[4:] for row in rows]


def matrix(row):
    return [[row[f"e{r}{c}"] for c in range(4)] for r in range(4)]


def floats(path):
    data = path.read_bytes()
    if len(data) % 4:
        raise ValueError("Truncated float buffer")
    return struct.unpack("<" + str(len(data) // 4) + "f", data)


def main():
    if OUT.exists():
        raise ValueError("Preserve existing parity receipt")
    receipt = ROOT / "REOPEN.v1.json"
    reopened = json.loads(receipt.read_text("utf-8-sig"))
    pins = dict(reopened["input_pins"])
    pins[str(receipt)] = sha(receipt)
    pins[str(Path(__file__).resolve())] = sha(__file__)
    for path, expected in pins.items():
        if sha(path) != expected:
            raise ValueError("Changed source/capture input: " + path)
    source = ROOT / "export"
    result = json.loads((source / "RESULT.json").read_text("utf-8-sig"))
    positions = floats(source / "positions.f32")
    binds = floats(source / "bindposes.f32")
    bind_matrices = [[[binds[i * 16 + c * 4 + r] for c in range(4)] for r in range(4)] for i in range(result["bind_poses"])]
    counts = (source / "bones-per-vertex.u8").read_bytes()
    weights = (source / "boneweights.i32-f32").read_bytes()
    influence_rows = []
    cursor = 0
    for count in counts:
        row = []
        for _ in range(count):
            row.append(struct.unpack_from("<if", weights, cursor))
            cursor += 8
        influence_rows.append(row)
    reports = []
    for index, pose in enumerate(result["probe_poses"]):
        mesh_inverse = inverse(matrix(pose["mesh_world"]))
        palettes = [multiply(multiply(mesh_inverse, matrix(joint)), bind) for joint, bind in zip(pose["joint_world"], bind_matrices)]
        baked = floats(source / f"pose-{index}-positions.f32")
        maximum = 0.0
        for vertex, influences in enumerate(influence_rows):
            point = [*positions[vertex * 3:vertex * 3 + 3], 1.0]
            for axis in range(3):
                predicted = sum(weight * sum(palettes[joint][axis][k] * point[k] for k in range(4)) for joint, weight in influences)
                observed = baked[vertex * 3 + axis]
                error = abs(predicted - observed)
                # Independent float64 evaluation vs Unity float32 multiply/accumulate.
                tolerance = 2e-5 * max(1.0, abs(predicted), abs(observed))
                if not math.isfinite(error) or error > tolerance:
                    raise ValueError(f"Skin mismatch pose {index}, vertex {vertex}, axis {axis}: {error}")
                maximum = max(maximum, error)
        reports.append({"pose": pose["name"], "vertices_compared": len(influence_rows), "maximum_absolute_position_error": maximum})
    for path, expected in pins.items():
        if sha(path) != expected:
            raise ValueError("Input changed during parity check")
    OUT.mkdir()
    report = {"status": "all_native_probe_skin_positions_independently_reconciled", "source_guid": result["guid"], "source_file_id": result["file_id"], "poses": reports, "input_pins": pins, "source_position_deformation_verified": True, "normals_deformation_verified": False, "skin_glb_converted": False, "gpu_verified": False, "studio_verified": False, "full_import": False}
    with (OUT / "RESULT.v1.json").open("x", encoding="utf-8") as output:
        json.dump(report, output, indent=2, allow_nan=False)
    print(json.dumps({key: value for key, value in report.items() if key != "input_pins"}))


if __name__ == "__main__":
    main()
