"""Print deterministic world-space bounds for GLTF/GLB files passed on argv."""

from __future__ import annotations

import os
import sys

import bpy
from mathutils import Vector


def inspect(path: str) -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=path)
    meshes = [item for item in bpy.context.scene.objects if item.type == "MESH"]
    points = [item.matrix_world @ Vector(corner) for item in meshes for corner in item.bound_box]
    if not points:
        raise ValueError(f"Model has no mesh bounds: {path}")
    minimum = Vector(tuple(min(point[index] for point in points) for index in range(3)))
    maximum = Vector(tuple(max(point[index] for point in points) for index in range(3)))
    dimensions = maximum - minimum
    print(
        "BOUNDS|"
        + os.path.basename(path)
        + "|"
        + ",".join(f"{value:.3f}" for value in dimensions)
    )


for model_path in sys.argv[sys.argv.index("--") + 1 :]:
    inspect(os.path.abspath(model_path))
