"""Reconcile captured normals for the exact single-joint Medieval cloth rig.

No general claim about nonuniform-scale or multiple-influence rigs is made.
"""
import json
import math
from pathlib import Path
import struct

import verify_medieval_skin_positions as skin


def main():
    output = Path(r"F:\OpenRPG_ImportWork\medieval-skin-normal-parity-v1")
    if output.exists():
        raise ValueError("Previous normal comparison must be preserved")
    root = skin.ROOT
    reopened = root / "REOPEN.v1.json"
    pins = dict(json.loads(reopened.read_text("utf-8-sig"))["input_pins"])
    for path in [reopened, Path(__file__).resolve(), Path(skin.__file__).resolve()]:
        pins[str(path)] = skin.sha(path)
    for path, expected in pins.items():
        if skin.sha(path) != expected:
            raise ValueError("Changed capture/source: " + path)
    source = root / "export"
    result = json.loads((source / "RESULT.json").read_text("utf-8-sig"))
    count = result["vertices"]
    if result["bind_poses"] != 1 or result["normals"] != count or result["tangents"] != 0:
        raise ValueError("This qualification is restricted to the observed single-joint rig")
    if (source / "bones-per-vertex.u8").read_bytes() != bytes([1]) * count:
        raise ValueError("Single influence per vertex required")
    if (source / "boneweights.i32-f32").read_bytes() != struct.pack("<if", 0, 1.0) * count:
        raise ValueError("Exact source joint/weight required")
    normals = skin.floats(source / "normals.f32")
    bind = skin.floats(source / "bindposes.f32")
    bind_matrix = [[bind[c * 4 + r] for c in range(4)] for r in range(4)]
    reports = []
    for index, pose in enumerate(result["probe_poses"]):
        transform = skin.multiply(skin.multiply(skin.inverse(skin.matrix(pose["mesh_world"])), skin.matrix(pose["joint_world"][0])), bind_matrix)
        # The observed probes must have a rotation-only palette. Refuse shear/scale
        # rather than extending this result to a different normal-skinning rule.
        for a in range(3):
            for b in range(3):
                product = sum(transform[k][a] * transform[k][b] for k in range(3))
                if abs(product - float(a == b)) > 2e-5:
                    raise ValueError("Non-orthonormal palette is outside this proof")
        observed = skin.floats(source / f"pose-{index}-normals.f32")
        maximum = 0.0
        for vertex in range(count):
            for axis in range(3):
                predicted = sum(transform[axis][k] * normals[vertex * 3 + k] for k in range(3))
                error = abs(predicted - observed[vertex * 3 + axis])
                if not math.isfinite(error) or error > 2e-5:
                    raise ValueError(f"Normal mismatch pose {index}, vertex {vertex}, axis {axis}")
                maximum = max(maximum, error)
        reports.append({"pose": pose["name"], "normals_compared": count, "maximum_absolute_normal_component_error": maximum})
    for path, expected in pins.items():
        if skin.sha(path) != expected:
            raise ValueError("Capture/source changed during comparison")
    report = {"status": "two_native_single_joint_normal_poses_reconciled", "source_guid": result["guid"], "source_file_id": result["file_id"], "poses": reports, "input_pins": pins, "source_normal_deformation_verified": True, "tangents": "absent_in_original_capture", "normalization_applied": False, "skin_glb_converted": False, "gpu_verified": False, "studio_verified": False, "full_import": False}
    output.mkdir()
    with (output / "RESULT.v1.json").open("x", encoding="utf-8") as file:
        json.dump(report, file, indent=2, allow_nan=False)
    print(json.dumps({key: value for key, value in report.items() if key != "input_pins"}))


if __name__ == "__main__":
    main()
