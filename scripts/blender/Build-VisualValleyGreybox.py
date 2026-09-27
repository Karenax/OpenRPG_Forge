#!/usr/bin/env python3
"""Build and render a deterministic Visual Valley greybox in Blender 4.5 LTS.

The input is a derived forge.world-sketch-blender-input JSON emitted by forge_cli. The generated
.blend, GLB and render are evidence/cache artifacts, never canonical Forge world sources.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import random
import shutil
import sys
from pathlib import Path

import bpy
from mathutils import Vector


SCHEMA = "forge.world-sketch-blender-input"
SCHEMA_VERSION = 4
WORKER_VERSION = 7


def parse_arguments() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--render-width", type=int, default=1920)
    parser.add_argument("--render-height", type=int, default=1080)
    parser.add_argument("--terrain-step", type=int, default=2)
    return parser.parse_args(argv)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def require_new_output_directory(path: Path) -> None:
    if path.exists():
        raise RuntimeError(f"output directory already exists: {path}")
    path.mkdir(parents=True, exist_ok=False)


def load_input(path: Path) -> dict:
    data = json.loads(path.read_text(encoding="utf-8"))
    if data.get("schema") != SCHEMA or data.get("schema_version") != SCHEMA_VERSION:
        raise RuntimeError("unsupported Blender input schema")
    grid = data["height_grid"]
    expected = int(grid["vertices_x"]) * int(grid["vertices_z"])
    if len(grid["samples_m"]) != expected:
        raise RuntimeError(
            f"height sample count mismatch: expected {expected}, got {len(grid['samples_m'])}"
        )
    semantic_masks = data["semantic_masks"]
    if semantic_masks.get("schema") != "forge.world-semantic-masks":
        raise RuntimeError("unsupported semantic-mask schema")
    for layer in semantic_masks["layers"]:
        if len(layer["samples_u8"]) != expected:
            raise RuntimeError(f"semantic-mask sample count mismatch for {layer['feature_id']}")
    carving = data["spline_carving"]
    if carving.get("schema") != "forge.world-spline-carving":
        raise RuntimeError("unsupported spline-carving schema")
    if len(carving["carved_height_grid"]["samples_m"]) != expected:
        raise RuntimeError("carved height sample count mismatch")
    for field in [
        "road_surface_mask_u8",
        "water_bed_mask_u8",
        "bank_mask_u8",
        "vegetation_exclusion_mask_u8",
    ]:
        if len(carving[field]) != expected:
            raise RuntimeError(f"carving-mask sample count mismatch for {field}")
    traversal = data["traversal_audit"]
    if traversal.get("schema") != "forge.world-traversal-audit":
        raise RuntimeError("unsupported traversal-audit schema")
    for field in [
        "slope_degrees_u8",
        "slope_walkable_mask_u8",
        "water_blocked_mask_u8",
        "land_walkable_mask_u8",
        "bridge_candidate_mask_u8",
    ]:
        if len(traversal[field]) != expected:
            raise RuntimeError(f"traversal-mask sample count mismatch for {field}")
    bridge_placements = data["bridge_placements"]
    if bridge_placements.get("schema") != "forge.world-bridge-placements":
        raise RuntimeError("unsupported bridge-placement schema")
    if not bridge_placements.get("placements"):
        raise RuntimeError("Blender greybox requires at least one bridge placement")
    return data


def reset_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in list(bpy.data.collections):
        if collection.name != "Collection":
            bpy.data.collections.remove(collection)
    root = bpy.data.collections.get("Collection")
    if root is not None:
        root.name = "Forge_Generated"


def new_collection(name: str) -> bpy.types.Collection:
    collection = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(collection)
    return collection


def make_material(
    name: str,
    color: tuple[float, float, float, float],
    roughness: float,
    metallic: float = 0.0,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = color
    node = next(
        (candidate for candidate in material.node_tree.nodes if candidate.type == "BSDF_PRINCIPLED"),
        None,
    )
    if node is None:
        raise RuntimeError(f"Principled BSDF node missing from material {name}")
    node.inputs["Base Color"].default_value = color
    node.inputs["Roughness"].default_value = roughness
    node.inputs["Metallic"].default_value = metallic
    return material


def add_procedural_detail(
    material: bpy.types.Material,
    shadow_color: tuple[float, float, float, float],
    highlight_color: tuple[float, float, float, float],
    scale: float,
    detail: float,
    bump_strength: float,
) -> None:
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    principled = next(node for node in nodes if node.type == "BSDF_PRINCIPLED")
    coordinates = nodes.new("ShaderNodeTexCoord")
    noise = nodes.new("ShaderNodeTexNoise")
    noise.noise_dimensions = "3D"
    noise.inputs["Scale"].default_value = scale
    noise.inputs["Detail"].default_value = detail
    noise.inputs["Roughness"].default_value = 0.68
    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position = 0.28
    ramp.color_ramp.elements[0].color = shadow_color
    ramp.color_ramp.elements[1].position = 0.78
    ramp.color_ramp.elements[1].color = highlight_color
    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = bump_strength
    bump.inputs["Distance"].default_value = 0.22
    links.new(coordinates.outputs["Generated"], noise.inputs["Vector"])
    links.new(noise.outputs["Fac"], ramp.inputs["Fac"])
    links.new(ramp.outputs["Color"], principled.inputs["Base Color"])
    links.new(noise.outputs["Fac"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], principled.inputs["Normal"])


def make_terrain_material() -> bpy.types.Material:
    material = make_material("M_TerrainBlend", (0.16, 0.42, 0.095, 1.0), 0.9)
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    principled = next(node for node in nodes if node.type == "BSDF_PRINCIPLED")
    coordinates = nodes.new("ShaderNodeTexCoord")
    noise = nodes.new("ShaderNodeTexNoise")
    noise.noise_dimensions = "3D"
    noise.inputs["Scale"].default_value = 0.045
    noise.inputs["Detail"].default_value = 7.0
    noise.inputs["Roughness"].default_value = 0.7
    vertex_color = nodes.new("ShaderNodeVertexColor")
    vertex_color.layer_name = "forge_semantic_color"
    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.25
    bump.inputs["Distance"].default_value = 0.36
    links.new(coordinates.outputs["Object"], noise.inputs["Vector"])
    links.new(vertex_color.outputs["Color"], principled.inputs["Base Color"])
    links.new(noise.outputs["Fac"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], principled.inputs["Normal"])
    return material


def create_mesh_object(
    name: str,
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, ...]],
    collection: bpy.types.Collection,
    materials: list[bpy.types.Material],
    material_indices: list[int] | None = None,
    smooth: bool = False,
) -> bpy.types.Object:
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update(calc_edges=True)
    for material in materials:
        mesh.materials.append(material)
    if material_indices is not None:
        if len(material_indices) != len(mesh.polygons):
            raise RuntimeError(f"material index count mismatch for {name}")
        for polygon, material_index in zip(mesh.polygons, material_indices, strict=True):
            polygon.material_index = material_index
    if smooth:
        for polygon in mesh.polygons:
            polygon.use_smooth = True
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    return obj


def add_box(
    name: str,
    dimensions: tuple[float, float, float],
    location: tuple[float, float, float],
    rotation_z: float,
    collection: bpy.types.Collection,
    material: bpy.types.Material,
) -> bpy.types.Object:
    x, y, z = (value * 0.5 for value in dimensions)
    vertices = [
        (-x, -y, -z),
        (x, -y, -z),
        (x, y, -z),
        (-x, y, -z),
        (-x, -y, z),
        (x, -y, z),
        (x, y, z),
        (-x, y, z),
    ]
    faces = [
        (0, 1, 2, 3),
        (4, 7, 6, 5),
        (0, 4, 5, 1),
        (1, 5, 6, 2),
        (2, 6, 7, 3),
        (4, 0, 3, 7),
    ]
    obj = create_mesh_object(name, vertices, faces, collection, [material])
    obj.location = location
    obj.rotation_euler[2] = rotation_z
    return obj


class GridField:
    def __init__(self, grid: dict, samples: list, scale: float = 1.0):
        self.vertices_x = int(grid["vertices_x"])
        self.vertices_z = int(grid["vertices_z"])
        self.spacing = float(grid["sample_spacing_m"])
        self.origin_x = float(grid["origin_x_m"])
        self.origin_z = float(grid["origin_z_m"])
        self.samples = [float(value) * scale for value in samples]

    def sample(self, x_m: float, z_m: float) -> float:
        gx = max(0.0, min(self.vertices_x - 1.0, (x_m - self.origin_x) / self.spacing))
        gz = max(0.0, min(self.vertices_z - 1.0, (z_m - self.origin_z) / self.spacing))
        x0 = min(self.vertices_x - 2, int(math.floor(gx)))
        z0 = min(self.vertices_z - 2, int(math.floor(gz)))
        tx = gx - x0
        tz = gz - z0
        index = lambda x, z: z * self.vertices_x + x
        h00 = self.samples[index(x0, z0)]
        h10 = self.samples[index(x0 + 1, z0)]
        h01 = self.samples[index(x0, z0 + 1)]
        h11 = self.samples[index(x0 + 1, z0 + 1)]
        return (
            h00 * (1.0 - tx) * (1.0 - tz)
            + h10 * tx * (1.0 - tz)
            + h01 * (1.0 - tx) * tz
            + h11 * tx * tz
        )


class HeightSampler(GridField):
    def __init__(self, grid: dict):
        super().__init__(grid, grid["samples_m"])

    def slope_at_index(self, x_index: int, z_index: int) -> float:
        x_previous = max(0, x_index - 1)
        x_following = min(self.vertices_x - 1, x_index + 1)
        z_previous = max(0, z_index - 1)
        z_following = min(self.vertices_z - 1, z_index + 1)
        index = lambda x, z: z * self.vertices_x + x
        dx = (
            self.samples[index(x_following, z_index)] - self.samples[index(x_previous, z_index)]
        ) / max(self.spacing, (x_following - x_previous) * self.spacing)
        dz = (
            self.samples[index(x_index, z_following)] - self.samples[index(x_index, z_previous)]
        ) / max(self.spacing, (z_following - z_previous) * self.spacing)
        return math.hypot(dx, dz)


class SemanticMaskSampler:
    def __init__(self, document: dict):
        grid = document["grid"]
        self.layers_by_kind: dict[str, list[GridField]] = {}
        for layer in document["layers"]:
            self.layers_by_kind.setdefault(str(layer["kind"]), []).append(
                GridField(grid, layer["samples_u8"], 1.0 / 255.0)
            )

    def sample(self, kind: str, x_m: float, z_m: float) -> float:
        return max((field.sample(x_m, z_m) for field in self.layers_by_kind.get(kind, [])), default=0.0)

    def value_at_index(self, kind: str, index: int) -> float:
        return max((field.samples[index] for field in self.layers_by_kind.get(kind, [])), default=0.0)


class CarvingMaskSampler:
    def __init__(self, document: dict):
        grid = document["carved_height_grid"]
        self.fields = {
            name: GridField(grid, document[name], 1.0 / 255.0)
            for name in [
                "road_surface_mask_u8",
                "water_bed_mask_u8",
                "bank_mask_u8",
                "vegetation_exclusion_mask_u8",
            ]
        }

    def sample(self, name: str, x_m: float, z_m: float) -> float:
        return self.fields[name].sample(x_m, z_m)

    def value_at_index(self, name: str, index: int) -> float:
        return self.fields[name].samples[index]


def mix_color(
    base: tuple[float, float, float],
    target: tuple[float, float, float],
    weight: float,
) -> tuple[float, float, float]:
    weight = max(0.0, min(1.0, weight))
    return tuple(base[index] * (1.0 - weight) + target[index] * weight for index in range(3))


def terrain_vertex_color(
    index: int,
    x_index: int,
    z_index: int,
    sampler: HeightSampler,
    semantic_masks: SemanticMaskSampler,
    carving_masks: CarvingMaskSampler,
) -> tuple[float, float, float, float]:
    color = (0.18, 0.42, 0.095)
    meadow = semantic_masks.value_at_index("meadow", index)
    forest = semantic_masks.value_at_index("forest", index)
    wetland = semantic_masks.value_at_index("wetland", index)
    buildable = semantic_masks.value_at_index("buildable", index)
    authored_rock = semantic_masks.value_at_index("rock", index)
    slope_rock = max(0.0, min(1.0, (sampler.slope_at_index(x_index, z_index) - 0.3) / 0.55))
    color = mix_color(color, (0.28, 0.56, 0.12), meadow * 0.6)
    color = mix_color(color, (0.055, 0.24, 0.055), forest * 0.88)
    color = mix_color(color, (0.08, 0.28, 0.17), wetland * 0.82)
    color = mix_color(color, (0.38, 0.28, 0.13), buildable * 0.7)
    color = mix_color(color, (0.34, 0.36, 0.33), max(authored_rock, slope_rock) * 0.9)
    color = mix_color(color, (0.28, 0.17, 0.07), carving_masks.value_at_index("bank_mask_u8", index) * 0.82)
    color = mix_color(color, (0.11, 0.16, 0.12), carving_masks.value_at_index("water_bed_mask_u8", index) * 0.72)
    color = mix_color(color, (0.31, 0.20, 0.09), carving_masks.value_at_index("road_surface_mask_u8", index) * 0.9)
    return (*color, 1.0)


def build_terrain(
    sampler: HeightSampler,
    semantic_masks: SemanticMaskSampler,
    carving_masks: CarvingMaskSampler,
    step: int,
    collection: bpy.types.Collection,
    material: bpy.types.Material,
) -> bpy.types.Object:
    if step <= 0 or (sampler.vertices_x - 1) % step != 0 or (sampler.vertices_z - 1) % step != 0:
        raise RuntimeError("terrain-step must divide both height-grid interval counts")
    x_indices = list(range(0, sampler.vertices_x, step))
    z_indices = list(range(0, sampler.vertices_z, step))
    vertices = []
    vertex_colors = []
    for z_index in z_indices:
        z_m = sampler.origin_z + z_index * sampler.spacing
        for x_index in x_indices:
            x_m = sampler.origin_x + x_index * sampler.spacing
            source_index = z_index * sampler.vertices_x + x_index
            vertices.append((x_m, z_m, sampler.samples[source_index]))
            vertex_colors.append(
                terrain_vertex_color(
                    source_index,
                    x_index,
                    z_index,
                    sampler,
                    semantic_masks,
                    carving_masks,
                )
            )

    width = len(x_indices)
    faces = []
    for z in range(len(z_indices) - 1):
        for x in range(width - 1):
            a = z * width + x
            faces.append((a, a + 1, a + 1 + width, a + width))

    terrain = create_mesh_object(
        "VV_Terrain",
        vertices,
        faces,
        collection,
        [material],
        smooth=True,
    )
    color_attribute = terrain.data.color_attributes.new(
        name="forge_semantic_color",
        type="FLOAT_COLOR",
        domain="POINT",
    )
    for attribute_value, color in zip(color_attribute.data, vertex_colors, strict=True):
        attribute_value.color = color
    terrain["forge_role"] = "derived_carved_terrain_preview"
    return terrain


def create_carved_ribbon(
    name: str,
    spline: dict,
    sampler: HeightSampler,
    collection: bpy.types.Collection,
    material: bpy.types.Material,
) -> bpy.types.Object:
    path = spline["centerline_samples"]
    width_m = float(spline["authored_width_m"])
    is_water = spline["kind"] in {"river", "creek"}
    vertices: list[tuple[float, float, float]] = []
    for index, sample in enumerate(path):
        x_m, z_m = float(sample["x_m"]), float(sample["z_m"])
        previous = path[max(0, index - 1)]
        following = path[min(len(path) - 1, index + 1)]
        tangent_x = float(following["x_m"]) - float(previous["x_m"])
        tangent_z = float(following["z_m"]) - float(previous["z_m"])
        length = math.hypot(tangent_x, tangent_z) or 1.0
        normal_x, normal_z = -tangent_z / length, tangent_x / length
        for sign in (-1.0, 1.0):
            vx = x_m + normal_x * width_m * 0.5 * sign
            vz = z_m + normal_z * width_m * 0.5 * sign
            height_m = (
                float(sample["surface_height_m"]) + 0.06
                if is_water
                else sampler.sample(vx, vz) + 0.12
            )
            vertices.append((vx, vz, height_m))
    faces = [(index * 2, index * 2 + 1, index * 2 + 3, index * 2 + 2) for index in range(len(path) - 1)]
    ribbon = create_mesh_object(name, vertices, faces, collection, [material], smooth=True)
    ribbon["forge_role"] = "derived_carved_spline_preview"
    ribbon["forge_feature_id"] = str(spline["feature_id"])
    return ribbon


def append_cone(
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, ...]],
    radius_bottom: float,
    radius_top: float,
    height: float,
    z0: float,
    segments: int,
) -> None:
    offset = len(vertices)
    for ring_radius, z in ((radius_bottom, z0), (radius_top, z0 + height)):
        for index in range(segments):
            angle = 2.0 * math.pi * index / segments
            vertices.append((math.cos(angle) * ring_radius, math.sin(angle) * ring_radius, z))
    for index in range(segments):
        following = (index + 1) % segments
        faces.append((offset + index, offset + following, offset + segments + following, offset + segments + index))
    faces.append(tuple(offset + index for index in reversed(range(segments))))
    faces.append(tuple(offset + segments + index for index in range(segments)))


def append_uv_sphere(
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, ...]],
    radius: tuple[float, float, float],
    center: tuple[float, float, float],
    rings: int = 6,
    segments: int = 12,
) -> None:
    offset = len(vertices)
    for ring in range(1, rings):
        phi = math.pi * ring / rings
        for segment in range(segments):
            theta = 2.0 * math.pi * segment / segments
            vertices.append(
                (
                    center[0] + radius[0] * math.sin(phi) * math.cos(theta),
                    center[1] + radius[1] * math.sin(phi) * math.sin(theta),
                    center[2] + radius[2] * math.cos(phi),
                )
            )
    bottom = len(vertices)
    vertices.append((center[0], center[1], center[2] - radius[2]))
    top = len(vertices)
    vertices.append((center[0], center[1], center[2] + radius[2]))
    for ring in range(rings - 2):
        for segment in range(segments):
            following = (segment + 1) % segments
            a = offset + ring * segments + segment
            b = offset + ring * segments + following
            c = offset + (ring + 1) * segments + following
            d = offset + (ring + 1) * segments + segment
            faces.append((a, b, c, d))
    first_ring = offset
    last_ring = offset + (rings - 2) * segments
    for segment in range(segments):
        following = (segment + 1) % segments
        faces.append((bottom, first_ring + following, first_ring + segment))
        faces.append((top, last_ring + segment, last_ring + following))


def create_tree_prototypes(
    collection: bpy.types.Collection,
    bark: bpy.types.Material,
    deciduous: bpy.types.Material,
    conifer: bpy.types.Material,
) -> list[bpy.types.Object]:
    prototypes: list[bpy.types.Object] = []
    for variant in range(3):
        vertices: list[tuple[float, float, float]] = []
        trunk_faces: list[tuple[int, ...]] = []
        append_cone(vertices, trunk_faces, 0.42 + variant * 0.05, 0.25, 5.5 + variant, 0.0, 12)
        canopy_faces: list[tuple[int, ...]] = []
        if variant == 0:
            for center, radius in [
                ((0.0, 0.0, 6.8), (2.7, 2.5, 2.4)),
                ((1.3, 0.2, 7.4), (2.0, 1.9, 1.9)),
                ((-1.2, -0.3, 7.2), (2.1, 1.8, 2.0)),
                ((0.0, 0.8, 8.4), (1.8, 1.7, 1.7)),
            ]:
                append_uv_sphere(vertices, canopy_faces, radius, center)
            foliage_material = deciduous
        elif variant == 1:
            for radius, height, z0 in [(2.8, 4.5, 3.5), (2.2, 4.0, 5.6), (1.5, 3.5, 7.3)]:
                append_cone(vertices, canopy_faces, radius, 0.15, height, z0, 14)
            foliage_material = conifer
        else:
            for center, radius in [
                ((0.0, 0.0, 7.0), (3.0, 2.5, 1.7)),
                ((0.8, 0.0, 8.1), (2.3, 2.0, 1.5)),
                ((-1.1, 0.5, 7.8), (2.0, 1.7, 1.5)),
            ]:
                append_uv_sphere(vertices, canopy_faces, radius, center)
            foliage_material = deciduous
        all_faces = trunk_faces + canopy_faces
        material_indices = [0] * len(trunk_faces) + [1] * len(canopy_faces)
        prototype = create_mesh_object(
            f"TreePrototype_{variant}",
            vertices,
            all_faces,
            collection,
            [bark, foliage_material],
            material_indices,
            smooth=True,
        )
        prototype.hide_render = True
        prototype.hide_set(True)
        prototypes.append(prototype)
    return prototypes


def scatter_trees(
    data: dict,
    sampler: HeightSampler,
    semantic_masks: SemanticMaskSampler,
    carving_masks: CarvingMaskSampler,
    prototypes: list[bpy.types.Object],
    collection: bpy.types.Collection,
) -> int:
    rng = random.Random(0x56565F5452454553)
    extent = data["extent_m"]
    half_x, half_z = float(extent["size_x_m"]) * 0.5, float(extent["size_z_m"]) * 0.5
    landmarks = [
        (
            float(item["position"]["x_m"]),
            float(item["position"]["z_m"]),
            float(item["influence_radius_m"]),
        )
        for item in data["landmarks"]
    ]
    count = 0
    attempts = 0
    while count < 260 and attempts < 12000:
        attempts += 1
        x_m = rng.uniform(-half_x, half_x)
        z_m = rng.uniform(-half_z, half_z)
        forest_weight = semantic_masks.sample("forest", x_m, z_m)
        if forest_weight < 0.12 or rng.random() > forest_weight:
            continue
        if carving_masks.sample("vegetation_exclusion_mask_u8", x_m, z_m) > 0.08:
            continue
        if any(math.hypot(x_m - lx, z_m - lz) < radius * 0.75 for lx, lz, radius in landmarks):
            continue
        prototype = prototypes[rng.randrange(len(prototypes))]
        instance = prototype.copy()
        instance.data = prototype.data
        instance.name = f"Tree_{count:04d}"
        collection.objects.link(instance)
        scale = rng.uniform(0.72, 1.42)
        instance.scale = (scale * rng.uniform(0.9, 1.1), scale * rng.uniform(0.9, 1.1), scale)
        instance.rotation_euler[2] = rng.uniform(0.0, 2.0 * math.pi)
        instance.location = (x_m, z_m, sampler.sample(x_m, z_m) - 0.15)
        instance.hide_render = False
        instance.hide_set(False)
        instance["forge_role"] = "derived_tree_greybox"
        count += 1
    return count


def create_rock_prototype(
    collection: bpy.types.Collection,
    material: bpy.types.Material,
) -> bpy.types.Object:
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    append_uv_sphere(vertices, faces, (1.0, 0.82, 0.68), (0.0, 0.0, 0.62), rings=5, segments=10)
    distorted = []
    for index, (x, y, z) in enumerate(vertices):
        factor = 0.84 + 0.24 * math.sin(index * 2.399963)
        distorted.append((x * factor, y * (1.08 - 0.12 * factor), z * (0.92 + 0.1 * factor)))
    prototype = create_mesh_object("RockPrototype", distorted, faces, collection, [material], smooth=False)
    prototype.hide_render = True
    prototype.hide_set(True)
    return prototype


def scatter_rocks(
    sampler: HeightSampler,
    semantic_masks: SemanticMaskSampler,
    carving_masks: CarvingMaskSampler,
    prototype: bpy.types.Object,
    collection: bpy.types.Collection,
) -> int:
    rng = random.Random(0x56565F524F434B53)
    count = 0
    attempts = 0
    while count < 140 and attempts < 6000:
        attempts += 1
        x_m = rng.uniform(-255.0, 255.0)
        z_m = rng.uniform(-255.0, 255.0)
        rock_weight = max(0.12, semantic_masks.sample("rock", x_m, z_m))
        if rng.random() > rock_weight:
            continue
        if carving_masks.sample("vegetation_exclusion_mask_u8", x_m, z_m) > 0.65:
            continue
        instance = prototype.copy()
        instance.data = prototype.data
        instance.name = f"Rock_{count:04d}"
        collection.objects.link(instance)
        scale = rng.uniform(0.5, 3.6)
        instance.scale = (scale * rng.uniform(0.8, 1.6), scale * rng.uniform(0.7, 1.3), scale * rng.uniform(0.65, 1.25))
        instance.rotation_euler = (rng.uniform(-0.18, 0.18), rng.uniform(-0.18, 0.18), rng.uniform(0.0, math.tau))
        instance.location = (x_m, z_m, sampler.sample(x_m, z_m) - 0.25)
        instance.hide_render = False
        instance.hide_set(False)
        instance["forge_role"] = "derived_rock_greybox"
        count += 1
    return count


def local_offset(x: float, y: float, angle: float) -> tuple[float, float]:
    return (
        x * math.cos(angle) - y * math.sin(angle),
        x * math.sin(angle) + y * math.cos(angle),
    )


def create_roof(
    name: str,
    width: float,
    depth: float,
    height: float,
    location: tuple[float, float, float],
    rotation_z: float,
    collection: bpy.types.Collection,
    material: bpy.types.Material,
) -> bpy.types.Object:
    w, d = width * 0.5, depth * 0.5
    vertices = [
        (-w, -d, 0.0),
        (w, -d, 0.0),
        (w, d, 0.0),
        (-w, d, 0.0),
        (0.0, -d, height),
        (0.0, d, height),
    ]
    faces = [(0, 1, 4), (3, 5, 2), (0, 4, 5, 3), (1, 2, 5, 4), (0, 3, 2, 1)]
    roof = create_mesh_object(name, vertices, faces, collection, [material])
    roof.location = location
    roof.rotation_euler[2] = rotation_z
    return roof


def create_house(
    index: int,
    x_m: float,
    z_m: float,
    angle: float,
    sampler: HeightSampler,
    collection: bpy.types.Collection,
    materials: dict[str, bpy.types.Material],
    rng: random.Random,
) -> None:
    width = rng.uniform(8.0, 13.0)
    depth = rng.uniform(6.5, 10.0)
    wall_height = rng.uniform(5.2, 7.4)
    ground = sampler.sample(x_m, z_m)
    add_box(
        f"House_{index:02d}_Walls",
        (width, depth, wall_height),
        (x_m, z_m, ground + wall_height * 0.5),
        angle,
        collection,
        materials["plaster"],
    )
    create_roof(
        f"House_{index:02d}_Roof",
        width + 1.4,
        depth + 1.2,
        rng.uniform(2.5, 3.8),
        (x_m, z_m, ground + wall_height),
        angle,
        collection,
        materials["roof"],
    )
    for beam_index, local_x in enumerate((-width * 0.42, width * 0.42)):
        dx, dy = local_offset(local_x, -depth * 0.505, angle)
        add_box(
            f"House_{index:02d}_Beam_{beam_index}",
            (0.35, 0.28, wall_height * 0.92),
            (x_m + dx, z_m + dy, ground + wall_height * 0.5),
            angle,
            collection,
            materials["wood"],
        )


def build_village(
    landmark: dict,
    sampler: HeightSampler,
    collection: bpy.types.Collection,
    materials: dict[str, bpy.types.Material],
) -> int:
    rng = random.Random(0x56565F56494C4C41)
    center_x = float(landmark["position"]["x_m"])
    center_z = float(landmark["position"]["z_m"])
    count = 14
    for index in range(count):
        angle = math.tau * index / count + rng.uniform(-0.16, 0.16)
        radius = rng.uniform(13.0, 47.0)
        x_m = center_x + math.cos(angle) * radius
        z_m = center_z + math.sin(angle) * radius * 0.62
        create_house(index, x_m, z_m, angle + math.pi * 0.5, sampler, collection, materials, rng)
    return count


def build_keep(
    landmark: dict,
    sampler: HeightSampler,
    collection: bpy.types.Collection,
    stone: bpy.types.Material,
) -> None:
    x_m = float(landmark["position"]["x_m"])
    z_m = float(landmark["position"]["z_m"])
    ground = sampler.sample(x_m, z_m)
    add_box("Keep_Platform", (34.0, 30.0, 3.0), (x_m, z_m, ground + 1.5), 0.0, collection, stone)
    add_box("Keep_Tower", (18.0, 18.0, 34.0), (x_m, z_m, ground + 20.0), 0.0, collection, stone)
    for side in (-1, 1):
        for index in range(-3, 4):
            add_box(
                f"Keep_Battlement_X_{side}_{index}",
                (2.1, 2.1, 2.8),
                (x_m + side * 8.1, z_m + index * 2.5, ground + 38.1),
                0.0,
                collection,
                stone,
            )
            add_box(
                f"Keep_Battlement_Y_{side}_{index}",
                (2.1, 2.1, 2.8),
                (x_m + index * 2.5, z_m + side * 8.1, ground + 38.1),
                0.0,
                collection,
                stone,
            )


def nearest_spline_angle(data: dict, kind: str, x_m: float, z_m: float) -> float:
    spline = next(item for item in data["splines"] if item["kind"] == kind)
    points = spline["points"]
    best_distance = float("inf")
    best_angle = 0.0
    for first, second in zip(points, points[1:]):
        mx = (float(first["x_m"]) + float(second["x_m"])) * 0.5
        mz = (float(first["z_m"]) + float(second["z_m"])) * 0.5
        distance = (mx - x_m) ** 2 + (mz - z_m) ** 2
        if distance < best_distance:
            best_distance = distance
            best_angle = math.atan2(float(second["z_m"]) - float(first["z_m"]), float(second["x_m"]) - float(first["x_m"]))
    return best_angle


def build_bridge(
    placement: dict,
    collection: bpy.types.Collection,
    stone: bpy.types.Material,
) -> None:
    start = tuple(float(value) for value in placement["deck_start_surface_m"])
    end = tuple(float(value) for value in placement["deck_end_surface_m"])
    delta_x = end[0] - start[0]
    delta_y = end[1] - start[1]
    delta_z = end[2] - start[2]
    horizontal_length = math.hypot(delta_x, delta_z)
    deck_length = math.sqrt(delta_x * delta_x + delta_y * delta_y + delta_z * delta_z)
    angle = math.atan2(delta_z, delta_x)
    slope = math.atan2(delta_y, horizontal_length)
    normal = Vector((-math.sin(slope) * math.cos(angle), -math.sin(slope) * math.sin(angle), math.cos(slope)))
    across = Vector((-math.sin(angle), math.cos(angle), 0.0))
    surface_midpoint = Vector(((start[0] + end[0]) * 0.5, (start[2] + end[2]) * 0.5, (start[1] + end[1]) * 0.5))
    deck_width = float(placement["deck_width_m"])
    deck_thickness = float(placement["deck_thickness_m"])
    deck = add_box(
        "Bridge_Deck",
        (deck_length, deck_width, deck_thickness),
        tuple(surface_midpoint - normal * (deck_thickness * 0.5)),
        angle,
        collection,
        stone,
    )
    deck.rotation_euler[1] = -slope

    paver_count = 16
    paver_length = deck_length / paver_count
    for index in range(paver_count):
        fraction = (index + 0.5) / paver_count
        surface = Vector((
            start[0] + delta_x * fraction,
            start[2] + delta_z * fraction,
            start[1] + delta_y * fraction,
        ))
        paver = add_box(
            f"Bridge_Paver_{index:02d}",
            (paver_length - 0.08, deck_width - 1.5, 0.12),
            tuple(surface + normal * 0.06),
            angle,
            collection,
            stone,
        )
        paver.rotation_euler[1] = -slope

    parapet_height = float(placement["parapet_height_m"])
    parapet_thickness = float(placement["parapet_thickness_m"])
    for sign in (-1.0, 1.0):
        parapet_center = surface_midpoint + across * sign * (deck_width - parapet_thickness) * 0.5 + normal * (parapet_height * 0.5)
        parapet = add_box(
            "Bridge_Parapet",
            (deck_length, parapet_thickness, parapet_height),
            tuple(parapet_center),
            angle,
            collection,
            stone,
        )
        parapet.rotation_euler[1] = -slope

    for index, support in enumerate(placement["supports"]):
        fraction = float(support["center_fraction"])
        x_m = start[0] + delta_x * fraction
        z_m = start[2] + delta_z * fraction
        deck_surface_y = start[1] + delta_y * fraction
        local_ground = float(support["ground_height_m"])
        height = max(0.5, deck_surface_y - deck_thickness - local_ground)
        add_box(
            f"Bridge_Pier_{index}",
            (
                float(support["width_along_bridge_m"]),
                float(support["depth_across_bridge_m"]),
                height,
            ),
            (x_m, z_m, local_ground + height * 0.5),
            angle,
            collection,
            stone,
        )


def build_waterfall(
    landmark: dict,
    sampler: HeightSampler,
    collection: bpy.types.Collection,
    water: bpy.types.Material,
) -> None:
    x_m = float(landmark["position"]["x_m"])
    z_m = float(landmark["position"]["z_m"])
    top = sampler.sample(x_m, z_m) + 28.0
    bottom = sampler.sample(x_m, z_m - 24.0) + 1.0
    width = 17.0
    vertices = [
        (x_m - width * 0.5, z_m, bottom),
        (x_m + width * 0.5, z_m, bottom),
        (x_m + width * 0.5, z_m, top),
        (x_m - width * 0.5, z_m, top),
    ]
    create_mesh_object("Waterfall", vertices, [(0, 1, 2, 3)], collection, [water], smooth=True)


def create_mountain(
    name: str,
    x: float,
    y: float,
    base_z: float,
    radius: float,
    height: float,
    seed: int,
    collection: bpy.types.Collection,
    material: bpy.types.Material,
) -> bpy.types.Object:
    rng = random.Random(seed)
    radial_segments, rings = 32, 12
    vertices: list[tuple[float, float, float]] = []
    for ring in range(rings + 1):
        t = ring / rings
        ring_radius = radius * (1.0 - t) ** 0.72
        for segment in range(radial_segments):
            angle = math.tau * segment / radial_segments
            noise = 0.8 + 0.24 * math.sin(segment * 2.7 + ring * 1.9) + rng.uniform(-0.07, 0.07)
            vertices.append(
                (
                    x + math.cos(angle) * ring_radius * noise,
                    y + math.sin(angle) * ring_radius * noise,
                    base_z + height * t + math.sin(angle * 3.0 + ring) * radius * 0.035,
                )
            )
    faces = []
    for ring in range(rings):
        for segment in range(radial_segments):
            following = (segment + 1) % radial_segments
            a = ring * radial_segments + segment
            b = ring * radial_segments + following
            c = (ring + 1) * radial_segments + following
            d = (ring + 1) * radial_segments + segment
            faces.append((a, b, c, d))
    return create_mesh_object(name, vertices, faces, collection, [material], smooth=True)


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def build_cameras(
    data: dict,
    sampler: HeightSampler,
    collection: bpy.types.Collection,
) -> list[bpy.types.Object]:
    cameras: list[bpy.types.Object] = []
    for camera_spec in data["validation_cameras"]:
        camera_id = str(camera_spec["id"])
        position = [float(value) for value in camera_spec["position_m"]]
        target = [float(value) for value in camera_spec["target_m"]]
        position[1] = max(position[1], sampler.sample(position[0], position[2]) + 2.2)
        target[1] = max(target[1], sampler.sample(target[0], target[2]) + 6.0)
        camera_data = bpy.data.cameras.new(f"VV_Camera_{camera_id}")
        camera_data.sensor_fit = "VERTICAL"
        camera_data.angle = math.radians(float(camera_spec["vertical_fov_degrees"]))
        camera_data.clip_start = float(camera_spec["near_m"])
        camera_data.clip_end = float(camera_spec["far_m"])
        camera = bpy.data.objects.new(f"VV_Camera_{camera_id}", camera_data)
        collection.objects.link(camera)
        camera.location = (position[0], position[2], position[1])
        look_at(camera, Vector((target[0], target[2], target[1])))
        camera["forge_camera_id"] = camera_id
        camera["forge_camera_role"] = str(camera_spec["role"])
        cameras.append(camera)
    return cameras


def render_validation_captures(
    cameras: list[bpy.types.Object],
    output_directory: Path,
    hero_render_path: Path,
) -> dict[str, Path]:
    captures_directory = output_directory / "captures"
    captures_directory.mkdir(parents=False, exist_ok=False)
    capture_paths: dict[str, Path] = {}
    hero_capture: Path | None = None
    for camera in cameras:
        camera_id = str(camera["forge_camera_id"])
        capture_path = captures_directory / f"{camera_id}.png"
        bpy.context.scene.camera = camera
        bpy.context.scene.render.filepath = str(capture_path)
        bpy.ops.render.render(write_still=True)
        capture_paths[camera_id] = capture_path
        if camera["forge_camera_role"] == "reference_match":
            hero_capture = capture_path
    if hero_capture is None:
        raise RuntimeError("reference_match validation camera is missing")
    shutil.copyfile(hero_capture, hero_render_path)
    return capture_paths


def configure_lighting(collection: bpy.types.Collection) -> None:
    world = bpy.context.scene.world or bpy.data.worlds.new("VisualValleyWorld")
    bpy.context.scene.world = world
    world.use_nodes = True
    nodes = world.node_tree.nodes
    links = world.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputWorld")
    background = nodes.new("ShaderNodeBackground")
    sky = nodes.new("ShaderNodeTexSky")
    sky.sky_type = "NISHITA"
    sky.sun_elevation = math.radians(36.0)
    sky.sun_rotation = math.radians(132.0)
    sky.altitude = 0.5
    sky.air_density = 0.9
    sky.dust_density = 1.1
    background.inputs["Strength"].default_value = 0.32
    links.new(sky.outputs["Color"], background.inputs["Color"])
    links.new(background.outputs["Background"], output.inputs["Surface"])

    sun_data = bpy.data.lights.new("VV_Sun", "SUN")
    sun_data.energy = 2.4
    sun_data.angle = math.radians(5.0)
    sun = bpy.data.objects.new("VV_Sun", sun_data)
    collection.objects.link(sun)
    sun.rotation_euler = (math.radians(42.0), math.radians(-18.0), math.radians(-36.0))

    fill_data = bpy.data.lights.new("VV_Fill", "AREA")
    fill_data.energy = 280.0
    fill_data.shape = "DISK"
    fill_data.size = 120.0
    fill = bpy.data.objects.new("VV_Fill", fill_data)
    collection.objects.link(fill)
    fill.location = (-80.0, -120.0, 160.0)
    look_at(fill, Vector((0.0, 40.0, 10.0)))


def configure_render(width: int, height: int, output_path: Path) -> None:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.render.filepath = str(output_path)
    scene.render.use_file_extension = True
    scene.render.image_settings.color_depth = "8"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = -0.25


def write_mask_atlas(
    path: Path,
    width: int,
    height: int,
    columns: int,
    panels: list[tuple[str, list, tuple[float, float, float]]],
) -> list[list[str]]:
    rows = math.ceil(len(panels) / columns)
    atlas_width = width * columns
    atlas_height = height * rows
    pixels: list[float] = []
    layout: list[list[str]] = []
    for row in range(rows):
        layout.append(
            [panels[index][0] for index in range(row * columns, min((row + 1) * columns, len(panels)))]
        )
    for atlas_y in range(atlas_height):
        panel_row = atlas_y // height
        source_y = atlas_y % height
        for atlas_x in range(atlas_width):
            panel_column = atlas_x // width
            source_x = atlas_x % width
            panel_index = panel_row * columns + panel_column
            if panel_index >= len(panels):
                pixels.extend((0.02, 0.025, 0.02, 1.0))
                continue
            _, samples, tint = panels[panel_index]
            weight = float(samples[source_y * width + source_x]) / 255.0
            background = 0.018 + weight * 0.035
            pixels.extend(
                (
                    background + tint[0] * weight * 0.9,
                    background + tint[1] * weight * 0.9,
                    background + tint[2] * weight * 0.9,
                    1.0,
                )
            )
    image = bpy.data.images.new(
        path.stem,
        width=atlas_width,
        height=atlas_height,
        alpha=True,
        float_buffer=False,
    )
    image.pixels.foreach_set(pixels)
    image.filepath_raw = str(path)
    image.file_format = "PNG"
    image.save()
    bpy.data.images.remove(image)
    return layout


def write_mask_previews(data: dict, output_directory: Path) -> dict:
    grid = data["semantic_masks"]["grid"]
    width, height = int(grid["vertices_x"]), int(grid["vertices_z"])
    semantic_tints = {
        "forest": (0.05, 0.56, 0.08),
        "meadow": (0.52, 0.78, 0.12),
        "rock": (0.62, 0.64, 0.60),
        "wetland": (0.05, 0.48, 0.46),
        "buildable": (0.82, 0.51, 0.12),
        "water": (0.04, 0.42, 0.82),
    }
    semantic_panels = [
        (
            str(layer["feature_id"]),
            layer["samples_u8"],
            semantic_tints[str(layer["kind"])],
        )
        for layer in data["semantic_masks"]["layers"]
    ]
    semantic_path = output_directory / "semantic-mask-atlas.png"
    semantic_layout = write_mask_atlas(
        semantic_path,
        width,
        height,
        3,
        semantic_panels,
    )

    carving = data["spline_carving"]
    carving_panels = [
        ("road_surface", carving["road_surface_mask_u8"], (0.94, 0.48, 0.08)),
        ("water_bed", carving["water_bed_mask_u8"], (0.02, 0.58, 0.95)),
        ("banks", carving["bank_mask_u8"], (0.78, 0.58, 0.18)),
        (
            "vegetation_exclusion",
            carving["vegetation_exclusion_mask_u8"],
            (0.82, 0.12, 0.56),
        ),
    ]
    carving_path = output_directory / "spline-carving-mask-atlas.png"
    carving_layout = write_mask_atlas(
        carving_path,
        width,
        height,
        2,
        carving_panels,
    )
    traversal = data["traversal_audit"]
    traversal_panels = [
        ("slope_degrees_0_to_90", traversal["slope_degrees_u8"], (0.96, 0.22, 0.08)),
        ("slope_walkable", traversal["slope_walkable_mask_u8"], (0.28, 0.78, 0.14)),
        ("water_blocked", traversal["water_blocked_mask_u8"], (0.02, 0.52, 0.98)),
        ("land_walkable", traversal["land_walkable_mask_u8"], (0.18, 0.88, 0.48)),
        (
            "bridge_collider_candidate",
            traversal["bridge_candidate_mask_u8"],
            (0.98, 0.68, 0.06),
        ),
    ]
    traversal_path = output_directory / "traversal-audit-mask-atlas.png"
    traversal_layout = write_mask_atlas(
        traversal_path,
        width,
        height,
        3,
        traversal_panels,
    )
    return {
        "semantic": {
            "path": semantic_path,
            "layout_rows_bottom_to_top": semantic_layout,
        },
        "carving": {
            "path": carving_path,
            "layout_rows_bottom_to_top": carving_layout,
        },
        "traversal": {
            "path": traversal_path,
            "layout_rows_bottom_to_top": traversal_layout,
        },
    }


def export_glb(path: Path, backdrop_collection: bpy.types.Collection) -> None:
    backdrop_collection.hide_viewport = True
    try:
        bpy.ops.export_scene.gltf(
            filepath=str(path),
            check_existing=False,
            export_format="GLB",
            export_yup=True,
            export_apply=True,
            export_cameras=True,
            export_lights=False,
            export_animations=False,
            use_visible=True,
        )
    finally:
        backdrop_collection.hide_viewport = False


def main() -> None:
    args = parse_arguments()
    if bpy.app.version < (4, 5, 0):
        raise RuntimeError(f"Blender 4.5 or newer required, found {bpy.app.version_string}")
    if args.render_width < 640 or args.render_height < 360:
        raise RuntimeError("render dimensions are too small for visual review")
    require_new_output_directory(args.output_dir)
    data = load_input(args.input)
    reset_scene()

    collections = {
        name: new_collection(name)
        for name in ["Terrain", "Splines", "Landmarks", "Vegetation", "Cameras", "Lighting", "Backdrop", "Prototypes"]
    }
    materials = {
        "terrain": make_terrain_material(),
        "grass": make_material("M_Grass", (0.16, 0.42, 0.095, 1.0), 0.88),
        "earth": make_material("M_Earth", (0.24, 0.13, 0.055, 1.0), 0.94),
        "rock": make_material("M_Rock", (0.31, 0.34, 0.32, 1.0), 0.82),
        "wetland": make_material("M_Wetland", (0.12, 0.28, 0.16, 1.0), 0.72),
        "water": make_material("M_Water", (0.025, 0.28, 0.34, 1.0), 0.14, 0.08),
        "road": make_material("M_Road", (0.30, 0.20, 0.11, 1.0), 0.96),
        "bark": make_material("M_Bark", (0.12, 0.055, 0.022, 1.0), 0.9),
        "leaf": make_material("M_Leaf", (0.055, 0.28, 0.06, 1.0), 0.82),
        "needle": make_material("M_Needle", (0.025, 0.18, 0.055, 1.0), 0.86),
        "stone": make_material("M_Stone", (0.46, 0.48, 0.45, 1.0), 0.84),
        "plaster": make_material("M_Plaster", (0.66, 0.57, 0.42, 1.0), 0.88),
        "roof": make_material("M_Roof", (0.24, 0.075, 0.03, 1.0), 0.88),
        "wood": make_material("M_Wood", (0.16, 0.07, 0.025, 1.0), 0.9),
        "backdrop": make_material("M_Backdrop", (0.055, 0.075, 0.05, 1.0), 0.96),
    }
    procedural_details = {
        "grass": ((0.025, 0.105, 0.018, 1.0), (0.16, 0.39, 0.07, 1.0), 22.0, 5.0, 0.18),
        "earth": ((0.07, 0.028, 0.008, 1.0), (0.26, 0.12, 0.035, 1.0), 18.0, 5.0, 0.26),
        "rock": ((0.075, 0.082, 0.078, 1.0), (0.32, 0.35, 0.33, 1.0), 12.0, 7.0, 0.38),
        "wetland": ((0.018, 0.065, 0.035, 1.0), (0.09, 0.25, 0.12, 1.0), 16.0, 4.0, 0.2),
        "water": ((0.008, 0.10, 0.16, 1.0), (0.035, 0.38, 0.48, 1.0), 32.0, 6.0, 0.12),
        "road": ((0.08, 0.04, 0.015, 1.0), (0.30, 0.16, 0.065, 1.0), 14.0, 5.0, 0.28),
        "bark": ((0.025, 0.008, 0.003, 1.0), (0.16, 0.055, 0.012, 1.0), 9.0, 4.0, 0.34),
        "leaf": ((0.008, 0.08, 0.012, 1.0), (0.08, 0.32, 0.035, 1.0), 7.0, 4.0, 0.16),
        "needle": ((0.004, 0.045, 0.015, 1.0), (0.035, 0.22, 0.07, 1.0), 8.0, 4.0, 0.15),
        "stone": ((0.11, 0.12, 0.105, 1.0), (0.42, 0.45, 0.40, 1.0), 10.0, 6.0, 0.34),
        "plaster": ((0.20, 0.14, 0.075, 1.0), (0.62, 0.46, 0.23, 1.0), 7.0, 4.0, 0.14),
        "roof": ((0.055, 0.01, 0.004, 1.0), (0.30, 0.045, 0.012, 1.0), 12.0, 5.0, 0.28),
        "wood": ((0.035, 0.012, 0.003, 1.0), (0.20, 0.065, 0.012, 1.0), 10.0, 5.0, 0.31),
    }
    for key, (shadow, highlight, scale, detail, bump_strength) in procedural_details.items():
        add_procedural_detail(materials[key], shadow, highlight, scale, detail, bump_strength)

    sampler = HeightSampler(data["spline_carving"]["carved_height_grid"])
    semantic_masks = SemanticMaskSampler(data["semantic_masks"])
    carving_masks = CarvingMaskSampler(data["spline_carving"])
    terrain = build_terrain(
        sampler,
        semantic_masks,
        carving_masks,
        args.terrain_step,
        collections["Terrain"],
        materials["terrain"],
    )
    spline_objects = []
    for spline in data["spline_carving"]["splines"]:
        role = spline["kind"]
        material = materials["water"] if role in {"river", "creek"} else materials["road"]
        spline_objects.append(
            create_carved_ribbon(
                f"Spline_{spline['feature_id']}",
                spline,
                sampler,
                collections["Splines"],
                material,
            )
        )

    landmarks = {landmark["id"]: landmark for landmark in data["landmarks"]}
    bridge_placement = next(
        placement
        for placement in data["bridge_placements"]["placements"]
        if placement["landmark_id"] == "bridge_south"
    )
    build_bridge(bridge_placement, collections["Landmarks"], materials["stone"])
    house_count = build_village(landmarks["village_west"], sampler, collections["Landmarks"], materials)
    build_keep(landmarks["watch_keep_north"], sampler, collections["Landmarks"], materials["stone"])
    build_waterfall(landmarks["waterfall_northeast"], sampler, collections["Landmarks"], materials["water"])

    tree_prototypes = create_tree_prototypes(collections["Prototypes"], materials["bark"], materials["leaf"], materials["needle"])
    tree_count = scatter_trees(
        data,
        sampler,
        semantic_masks,
        carving_masks,
        tree_prototypes,
        collections["Vegetation"],
    )
    rock_prototype = create_rock_prototype(collections["Prototypes"], materials["rock"])
    rock_count = scatter_rocks(
        sampler,
        semantic_masks,
        carving_masks,
        rock_prototype,
        collections["Vegetation"],
    )
    collections["Prototypes"].hide_render = True

    backdrop_top = min(sampler.samples) - 2.0
    add_box(
        "BackdropGround",
        (1600.0, 1600.0, 8.0),
        (0.0, 80.0, backdrop_top - 4.0),
        0.0,
        collections["Backdrop"],
        materials["backdrop"],
    )

    mountain_specs = [
        (-310.0, 120.0, 88.0, 145.0),
        (-250.0, 300.0, 115.0, 195.0),
        (-80.0, 350.0, 105.0, 175.0),
        (105.0, 350.0, 120.0, 205.0),
        (275.0, 285.0, 105.0, 185.0),
        (330.0, 80.0, 85.0, 150.0),
    ]
    for index, (x, y, radius, height) in enumerate(mountain_specs):
        create_mountain(
            f"BackdropMountain_{index}",
            x,
            y,
            -8.0,
            radius,
            height,
            1000 + index,
            collections["Backdrop"],
            materials["rock"],
        )

    cameras = build_cameras(data, sampler, collections["Cameras"])
    hero_camera = next(camera for camera in cameras if camera["forge_camera_role"] == "reference_match")
    configure_lighting(collections["Lighting"])
    render_path = args.output_dir / "visual-valley-greybox.png"
    blend_path = args.output_dir / "visual-valley-greybox.blend"
    glb_path = args.output_dir / "visual-valley-greybox.glb"
    configure_render(args.render_width, args.render_height, render_path)
    mask_previews = write_mask_previews(data, args.output_dir)

    bpy.context.scene["forge_world_sketch_sha256"] = data["source_world_sketch_sha256"]
    bpy.context.scene["forge_worker_version"] = WORKER_VERSION
    bpy.context.scene["forge_canonical"] = False
    bpy.context.scene["forge_semantic_mask_layers"] = len(data["semantic_masks"]["layers"])
    bpy.context.scene["forge_carved_samples"] = int(data["spline_carving"]["modified_sample_count"])
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)
    export_glb(glb_path, collections["Backdrop"])
    capture_paths = render_validation_captures(cameras, args.output_dir, render_path)
    bpy.context.scene.camera = hero_camera
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)

    manifest_path = args.output_dir / "visual-valley-greybox.manifest.json"
    manifest = {
        "schema": "forge.blender-greybox-evidence",
        "schema_version": 1,
        "worker_version": WORKER_VERSION,
        "blender_version": bpy.app.version_string,
        "canonical": False,
        "source_world_sketch_sha256": data["source_world_sketch_sha256"],
        "input_sha256": sha256(args.input),
        "terrain_vertices": len(terrain.data.vertices),
        "terrain_polygons": len(terrain.data.polygons),
        "spline_objects": len(spline_objects),
        "semantic_mask_layers": len(data["semantic_masks"]["layers"]),
        "carving": {
            "modified_samples": int(data["spline_carving"]["modified_sample_count"]),
            "vegetation_exclusion_samples": int(
                data["spline_carving"]["vegetation_exclusion_sample_count"]
            ),
            "minimum_height_delta_m": float(data["spline_carving"]["minimum_height_delta_m"]),
            "maximum_height_delta_m": float(data["spline_carving"]["maximum_height_delta_m"]),
            "splines": {
                str(spline["feature_id"]): {
                    "kind": str(spline["kind"]),
                    "affected_samples": int(spline["affected_sample_count"]),
                    "centerline_samples": len(spline["centerline_samples"]),
                    "shoulder_width_m": float(spline["shoulder_width_m"]),
                    "bed_depth_m": float(spline["bed_depth_m"]),
                }
                for spline in data["spline_carving"]["splines"]
            },
        },
        "traversal_audit": {
            "maximum_walkable_slope_degrees": float(
                data["traversal_audit"]["maximum_walkable_slope_degrees"]
            ),
            "maximum_observed_slope_degrees": float(
                data["traversal_audit"]["maximum_observed_slope_degrees"]
            ),
            "slope_walkable_samples": int(
                data["traversal_audit"]["slope_walkable_sample_count"]
            ),
            "water_blocked_samples": int(
                data["traversal_audit"]["water_blocked_sample_count"]
            ),
            "land_walkable_samples": int(
                data["traversal_audit"]["land_walkable_sample_count"]
            ),
            "connected_land_components": int(
                data["traversal_audit"]["connected_land_component_count"]
            ),
            "navigation_provider_status": str(
                data["traversal_audit"]["navigation_provider_status"]
            ),
            "routes": data["traversal_audit"]["routes"],
            "crossings": data["traversal_audit"]["crossings"],
        },
        "bridge_placements": {
            "schema": str(data["bridge_placements"]["schema"]),
            "schema_version": int(data["bridge_placements"]["schema_version"]),
            "source_carved_terrain_sha256": str(
                data["bridge_placements"]["source_carved_terrain_sha256"]
            ),
            "source_traversal_audit_sha256": str(
                data["bridge_placements"]["source_traversal_audit_sha256"]
            ),
            "placement_count": len(data["bridge_placements"]["placements"]),
            "placements": [
                {
                    "landmark_id": str(placement["landmark_id"]),
                    "road_feature_id": str(placement["road_feature_id"]),
                    "water_feature_id": str(placement["water_feature_id"]),
                    "deck_horizontal_length_m": math.dist(
                        (
                            placement["deck_start_surface_m"][0],
                            placement["deck_start_surface_m"][2],
                        ),
                        (
                            placement["deck_end_surface_m"][0],
                            placement["deck_end_surface_m"][2],
                        ),
                    ),
                    "deck_surface_length_m": math.dist(
                        placement["deck_start_surface_m"],
                        placement["deck_end_surface_m"],
                    ),
                    "deck_width_m": float(placement["deck_width_m"]),
                    "slope_degrees": float(placement["slope_degrees"]),
                    "support_count": len(placement["supports"]),
                }
                for placement in data["bridge_placements"]["placements"]
            ],
        },
        "houses": house_count,
        "trees": tree_count,
        "rocks": rock_count,
        "camera": hero_camera.name,
        "validation_cameras": {
            camera_id: {
                "path": str(path.relative_to(args.output_dir)).replace("\\", "/"),
                "sha256": sha256(path),
                "bytes": path.stat().st_size,
            }
            for camera_id, path in sorted(capture_paths.items())
        },
        "render_resolution": [args.render_width, args.render_height],
        "mask_previews": {
            name: {
                "path": str(preview["path"].relative_to(args.output_dir)).replace("\\", "/"),
                "sha256": sha256(preview["path"]),
                "bytes": preview["path"].stat().st_size,
                "layout_rows_bottom_to_top": preview["layout_rows_bottom_to_top"],
            }
            for name, preview in sorted(mask_previews.items())
        },
        "outputs": {
            "blend": {"path": blend_path.name, "sha256": sha256(blend_path), "bytes": blend_path.stat().st_size},
            "glb": {"path": glb_path.name, "sha256": sha256(glb_path), "bytes": glb_path.stat().st_size},
            "render": {"path": render_path.name, "sha256": sha256(render_path), "bytes": render_path.stat().st_size},
        },
        "limitations": [
            "Preview assets, masks and carved meshes are derived evidence; WorldSketch vectors remain canonical.",
            "The traversal atlas is an audit field, not a navmesh or pathfinder.",
            "The south bridge placement and collider contract are materialized; runtime crossing qualification remains external to Blender.",
            "Procedural vegetation is a composition preview and not the final governed asset kit.",
        ],
    }
    manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps({"status": "BLENDER_GREYBOX_PASS", "manifest": str(manifest_path), **manifest["outputs"]}))


if __name__ == "__main__":
    main()
