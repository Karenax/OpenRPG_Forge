"""Additional controls for nondegenerate indices and independent reopening."""
import importlib.util
import json
from pathlib import Path
import struct

path = Path(__file__).with_name("convert_closed_nested_geometry.py")
spec = importlib.util.spec_from_file_location("nested_geometry", path)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
source = {"positions.f32": struct.pack("<9f", 1., 0., 0., 0., 1., 0., 0., 0., 1.),
          "normals.f32": b"", "tangents.f32": b"", "colors.f32": b"",
          "submesh-0.i32": struct.pack("<3i", 0, 1, 2),
          "submesh-1.i32": b""}
metadata = {"normals_count": 0, "tangents_count": 0, "colors_count": 0,
            "uv_counts": [0] * 8, "submeshes": 2}
good = {"positions.target.f32": struct.pack("<9f", -1., 0., 0., -0., 1., 0., -0., 0., 1.),
        "submesh-0.target.u32": struct.pack("<3I", 0, 2, 1), "submesh-1.target.u32": b""}
module.reopen_streams(source, good, metadata)
controls = ["nondegenerate_winding_and_empty_submesh_preserved"]
for name, broken in [
    ("original_winding_refused", {**good, "submesh-0.target.u32": struct.pack("<3I", 0, 1, 2)}),
    ("original_empty_submesh_dropped_refused", {k: v for k, v in good.items() if k != "submesh-1.target.u32"}),
    ("extra_submesh_refused", {**good, "submesh-2.target.u32": b""}),
]:
    try:
        module.reopen_streams(source, broken, metadata)
    except ValueError:
        controls.append(name)
    else:
        raise RuntimeError(f"Invalid geometry accepted: {name}")
print(json.dumps({"additional_reopening_controls_passed": controls}))
