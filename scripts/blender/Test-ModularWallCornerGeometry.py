#!/usr/bin/env python3
"""Blender regression: inward input, reflected arm and complementary miter."""
import importlib.util
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

path = Path(__file__).with_name("Derive-ModularWallCornerFromJoint.py")
spec = importlib.util.spec_from_file_location("corner_derivation", path)
worker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(worker)
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.mesh.primitive_cube_add(size=1)
source = bpy.context.object
source.dimensions = (4, 0.72, 3.2)
source.location.z = 1.6
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
source.data.flip_normals()
oriented = worker.oriented_world_mesh(source)
bm = bmesh.new()
bm.from_mesh(oriented)
assert bm.calc_volume(signed=True) > 9.2, "inside-out source was not repaired"
bm.free()
center = Vector((0, 0, 1.6))
arm_x = Matrix.Translation(Vector((2, 0.36, 0)))
arm_y = Matrix(((0, 1, 0, 0.36), (1, 0, 0, 2), (0, 0, 1, 0), (0, 0, 0, 1)))
for arm, transform, axis in (("ArmX", arm_x, 1), ("ArmY", arm_y, 0)):
    for interior in (False, True):
        obj = worker.bake_mitered_half(source, f"fixture_LOD0_{arm}", transform, center, interior)
        points = [v.co for v in obj.data.vertices]
        assert all((p.x >= p.y - 1e-6 if arm == "ArmX" else p.y >= p.x - 1e-6) for p in points)
        assert all((p[axis] >= 0.36 - 1e-6 if interior else p[axis] <= 0.36 + 1e-6) for p in points)
        tree = BVHTree.FromPolygons(points, [tuple(p.vertices) for p in obj.data.polygons])
        origin = Vector((2, 2, 1.6))
        origin[axis] = 10 if interior else -10
        direction = Vector((0, 0, 0))
        direction[axis] = -1 if interior else 1
        hit, normal, _, _ = tree.ray_cast(origin, direction)
        assert hit is not None
        assert abs(hit[axis] - (0.72 if interior else 0)) < 1e-6
        assert normal[axis] > 0.999 if interior else normal[axis] < -0.999
print("PASS: inward source repaired; both determinant signs, both faces and complementary miters verified")
