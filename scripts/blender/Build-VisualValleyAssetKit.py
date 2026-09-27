#!/usr/bin/env python3
"""Build the deterministic, governed R1-063 Visual Valley asset-kit candidate.

The worker creates original procedural geometry only. It does not read Cadle, game assets,
network resources or runtime sources. Generated .blend, GLB and PNG files are derived Gate 1
evidence and are never promoted into the product implicitly.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import random
import re
import sys
from pathlib import Path
from typing import Iterable

import bpy
import numpy as np
from mathutils import Vector


ASSET_ID_PATTERN = re.compile(r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
EXPECTED_SCHEMA = "forge.asset-kit"
EXPECTED_SCHEMA_VERSION = 1
BLENDER_MAJOR_MINOR_PATTERN = re.compile(r"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$")


def parse_arguments() -> argparse.Namespace:
    separator = sys.argv.index("--") if "--" in sys.argv else len(sys.argv)
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--authored-source", type=Path)
    return parser.parse_args(sys.argv[separator + 1 :])


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while chunk := stream.read(1024 * 1024):
            digest.update(chunk)
    return digest.hexdigest()


def blender_major_minor(value: str, *, field: str) -> tuple[int, int]:
    """Parse a manifest Blender version written exactly as major.minor."""
    if not isinstance(value, str) or not BLENDER_MAJOR_MINOR_PATTERN.fullmatch(value):
        raise RuntimeError(f"{field} must use major.minor syntax, found {value!r}")
    major, minor = value.split(".", 1)
    return int(major), int(minor)


def require_running_blender_major_minor(expected: tuple[int, int], *, context: str) -> None:
    actual = tuple(bpy.app.version[:2])
    if actual != expected:
        required = ".".join(str(part) for part in expected)
        raise RuntimeError(f"{context} requires Blender {required}.x, found {bpy.app.version_string}")


def use_eevee(scene: bpy.types.Scene) -> None:
    """Select the Eevee enum exposed by the running Blender version."""
    engines = {item.identifier for item in scene.render.bl_rna.properties["engine"].enum_items}
    for engine in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        if engine in engines:
            scene.render.engine = engine
            return
    raise RuntimeError(f"Blender {bpy.app.version_string} does not expose an Eevee render engine")


def configure_sky_type(sky: bpy.types.ShaderNodeTexSky) -> str:
    """Use the closest supported physical-sky model across supported Blender releases."""
    supported = {item.identifier for item in sky.bl_rna.properties["sky_type"].enum_items}
    for candidate in ("NISHITA", "MULTIPLE_SCATTERING"):
        if candidate in supported:
            sky.sky_type = candidate
            return candidate
    raise RuntimeError(
        f"Blender {bpy.app.version_string} exposes sky types {sorted(supported)!r}; "
        "the presentation requires NISHITA or MULTIPLE_SCATTERING"
    )


def require_new_output_directory(path: Path) -> None:
    if path.exists():
        raise RuntimeError(f"output directory already exists: {path}")
    path.mkdir(parents=True)


def load_manifest(path: Path) -> dict:
    if not path.is_file():
        raise RuntimeError(f"asset-kit manifest does not exist: {path}")
    data = json.loads(path.read_text(encoding="utf-8"))
    if data.get("schema") != EXPECTED_SCHEMA:
        raise RuntimeError(f"unexpected schema: {data.get('schema')!r}")
    if data.get("schema_version") != EXPECTED_SCHEMA_VERSION:
        raise RuntimeError(f"unexpected schema version: {data.get('schema_version')!r}")
    if data.get("coordinate_system") != "right_handed_y_up" or data.get("unit") != "metre":
        raise RuntimeError("asset-kit coordinate system must be right_handed_y_up in metres")
    generator = data.get("generator")
    if not isinstance(generator, dict):
        raise RuntimeError("asset-kit must define generator")
    blender_major_minor(generator.get("blender_version"), field="generator.blender_version")
    assets = data.get("assets")
    if not isinstance(assets, list) or not assets:
        raise RuntimeError("asset-kit must contain at least one asset")
    seen: set[str] = set()
    for asset in assets:
        asset_id = asset.get("id")
        if not isinstance(asset_id, str) or not ASSET_ID_PATTERN.fullmatch(asset_id):
            raise RuntimeError(f"invalid asset id: {asset_id!r}")
        if asset_id in seen:
            raise RuntimeError(f"duplicate asset id: {asset_id}")
        seen.add(asset_id)
        if asset.get("kind") not in {"rock", "tree", "architecture"}:
            raise RuntimeError(f"unsupported asset kind for {asset_id}")
        dimensions = asset.get("dimensions_m")
        if (
            not isinstance(dimensions, list)
            or len(dimensions) != 3
            or any(not isinstance(value, (int, float)) or value <= 0 for value in dimensions)
        ):
            raise RuntimeError(f"invalid dimensions for {asset_id}")
        budgets = asset.get("lod_triangle_budgets")
        if (
            not isinstance(budgets, list)
            or len(budgets) != 3
            or any(not isinstance(value, int) or value <= 0 for value in budgets)
            or not (budgets[0] > budgets[1] > budgets[2])
        ):
            raise RuntimeError(f"invalid descending LOD budgets for {asset_id}")
    if data.get("provenance", {}).get("cadle_content_used") is not False:
        raise RuntimeError("R1-063 proof must explicitly exclude Cadle content")
    if data.get("provenance", {}).get("commercial_game_content_used") is not False:
        raise RuntimeError("R1-063 proof must explicitly exclude commercial game content")
    texture_policy = data.get("validation", {}).get("texture_policy")
    if not isinstance(texture_policy, dict):
        raise RuntimeError("asset-kit validation must define texture_policy")
    texture_resolution = texture_policy.get("resolution")
    if (
        not isinstance(texture_resolution, int)
        or texture_resolution < 256
        or texture_resolution > 2048
        or texture_resolution & (texture_resolution - 1)
    ):
        raise RuntimeError("texture resolution must be a power of two between 256 and 2048")
    if texture_policy.get("required_maps") != ["base_color", "roughness", "normal"]:
        raise RuntimeError("texture policy must require base_color, roughness and normal maps")
    if texture_policy.get("foliage_alpha_required") is not True:
        raise RuntimeError("texture policy must require foliage alpha")
    if texture_policy.get("foliage_alpha_mode") not in ("blend", "mask"):
        raise RuntimeError("texture policy must explicitly require foliage blend or mask")
    if texture_policy.get("embed_in_glb") is not True:
        raise RuntimeError("texture policy must require textures embedded in GLB")
    return data


def reset_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in list(bpy.data.collections):
        bpy.data.collections.remove(collection)
    for datablocks in (
        bpy.data.meshes,
        bpy.data.curves,
        bpy.data.materials,
        bpy.data.cameras,
        bpy.data.lights,
    ):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    use_eevee(scene)


def new_collection(name: str, parent: bpy.types.Collection | None = None) -> bpy.types.Collection:
    collection = bpy.data.collections.new(name)
    (parent or bpy.context.scene.collection).children.link(collection)
    return collection


def move_to_collection(obj: bpy.types.Object, collection: bpy.types.Collection) -> None:
    for current in list(obj.users_collection):
        current.objects.unlink(obj)
    collection.objects.link(obj)


def make_principled_material(
    name: str,
    base_color: tuple[float, float, float, float],
    roughness: float,
    metallic: float = 0.0,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = base_color
    material.metallic = metallic
    material.roughness = roughness
    nodes = material.node_tree.nodes
    principled = next((node for node in nodes if node.type == "BSDF_PRINCIPLED"), None)
    if principled is None:
        raise RuntimeError(f"Principled BSDF missing from material {name}")
    principled.inputs["Base Color"].default_value = base_color
    principled.inputs["Roughness"].default_value = roughness
    principled.inputs["Metallic"].default_value = metallic
    material["forge_material_model"] = "principled_pbr"
    material["forge_procedural_original"] = True
    return material


def normalized_field(field: np.ndarray) -> np.ndarray:
    minimum = float(field.min())
    maximum = float(field.max())
    if maximum - minimum <= 1e-8:
        return np.zeros_like(field, dtype=np.float32)
    return ((field - minimum) / (maximum - minimum)).astype(np.float32)


def deterministic_fbm(
    u: np.ndarray,
    v: np.ndarray,
    seed: int,
    base_frequency: int,
    octaves: int = 5,
) -> np.ndarray:
    rng = random.Random(seed)
    field = np.zeros_like(u, dtype=np.float32)
    amplitude = 1.0
    amplitude_sum = 0.0
    for octave in range(octaves):
        frequency = base_frequency * (2**octave)
        phase_a = rng.uniform(-math.pi, math.pi)
        phase_b = rng.uniform(-math.pi, math.pi)
        phase_c = rng.uniform(-math.pi, math.pi)
        layer = (
            np.sin(math.tau * frequency * u + phase_a)
            + np.cos(math.tau * (frequency + 1) * v + phase_b)
            + np.sin(math.tau * (frequency * u + (frequency + 2) * v) + phase_c)
        ) / 3.0
        field += layer.astype(np.float32) * amplitude
        amplitude_sum += amplitude
        amplitude *= 0.52
    return normalized_field(field / amplitude_sum)


def smoothstep(edge0: float, edge1: float, value: np.ndarray) -> np.ndarray:
    scaled = np.clip((value - edge0) / (edge1 - edge0), 0.0, 1.0)
    return (scaled * scaled * (3.0 - 2.0 * scaled)).astype(np.float32)


def normal_map_from_height(height: np.ndarray, strength: float) -> np.ndarray:
    derivative_x = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * strength
    derivative_y = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * strength
    normal = np.stack((-derivative_x, -derivative_y, np.ones_like(height)), axis=-1)
    normal /= np.maximum(np.linalg.norm(normal, axis=-1, keepdims=True), 1e-8)
    return (normal * 0.5 + 0.5).astype(np.float32)


def foliage_mask_and_veins(
    u: np.ndarray,
    v: np.ndarray,
    profile: str,
) -> tuple[np.ndarray, np.ndarray]:
    x = u * 2.0 - 1.0
    y = np.clip(v, 0.0, 1.0)
    if profile == "oak_leaf":
        envelope = np.power(np.maximum(np.sin(math.pi * y), 0.0), 0.58)
        lobes = 0.76 + 0.2 * np.cos(math.tau * (3.0 * y + 0.08))
        half_width = np.maximum(0.035, envelope * lobes)
        signed_edge = half_width - np.abs(x)
        alpha = smoothstep(-0.025, 0.018, signed_edge)
        stem = np.exp(-((x / 0.035) ** 2)) * smoothstep(0.0, 0.18, 0.2 - y)
        alpha = np.maximum(alpha, stem).astype(np.float32)
        midrib = np.exp(-((x / 0.027) ** 2)) * smoothstep(0.02, 0.15, y) * smoothstep(1.0, 0.83, y)
        veins = np.zeros_like(u, dtype=np.float32)
        for origin in (0.19, 0.31, 0.44, 0.57, 0.7):
            active = smoothstep(origin - 0.025, origin + 0.02, y) * smoothstep(origin + 0.2, origin + 0.13, y)
            reach = np.maximum(0.0, (y - origin) * 2.25)
            veins += np.exp(-((np.abs(x) - reach) / 0.025) ** 2) * active
        detail = np.clip(midrib + veins * 0.62, 0.0, 1.0) * alpha
    elif profile == "pine_needle":
        envelope = np.power(np.maximum(np.sin(math.pi * y), 0.0), 0.48)
        serration = 0.82 + 0.16 * np.cos(math.tau * (11.0 * y + 0.05))
        half_width = np.maximum(0.025, envelope * serration * 0.84)
        signed_edge = half_width - np.abs(x)
        alpha = smoothstep(-0.022, 0.012, signed_edge)
        central = np.exp(-((x / 0.045) ** 2))
        diagonal = np.exp(-((np.abs(x) - (0.18 + 0.42 * np.mod(y * 10.0, 1.0))) / 0.045) ** 2)
        detail = np.clip(central * 0.78 + diagonal * 0.34, 0.0, 1.0) * alpha
    else:
        raise RuntimeError(f"unsupported foliage texture profile: {profile}")
    return alpha.astype(np.float32), detail.astype(np.float32)


def build_material_texture_set(
    resolution: int,
    seed: int,
    profile: str,
    base_color: tuple[float, float, float, float],
    roughness: float,
) -> dict[str, np.ndarray]:
    axis = np.linspace(0.0, 1.0, resolution, endpoint=False, dtype=np.float32)
    u, v = np.meshgrid(axis, axis)
    broad = deterministic_fbm(u, v, seed, 2, 5)
    fine = deterministic_fbm(u, v, seed + 101, 7, 4)
    alpha = np.ones_like(u, dtype=np.float32)
    base = np.array(base_color[:3], dtype=np.float32)
    dark = np.clip(base * 0.43, 0.0, 1.0)
    light = np.clip(base * 1.7 + 0.025, 0.0, 1.0)

    if profile == "rock":
        strata = 0.5 + 0.5 * np.sin(math.tau * (v * 5.0 + broad * 0.34))
        color_field = np.clip(broad * 0.68 + fine * 0.2 + strata * 0.12, 0.0, 1.0)
        height = broad * 0.72 + fine * 0.28
    elif profile == "moss":
        clusters = smoothstep(0.33, 0.78, broad * 0.78 + fine * 0.22)
        color_field = np.clip(clusters * 0.7 + fine * 0.3, 0.0, 1.0)
        height = clusters * 0.65 + fine * 0.35
    elif profile == "bark":
        ridges = 0.5 + 0.5 * np.sin(math.tau * (u * 13.0 + broad * 1.35))
        fissures = smoothstep(0.47, 0.57, np.abs(ridges - 0.5) * 2.0)
        color_field = np.clip(ridges * 0.56 + broad * 0.24 + fine * 0.2, 0.0, 1.0)
        height = ridges * 0.62 + fissures * 0.2 + fine * 0.18
    elif profile == "limestone":
        sediment = 0.5 + 0.5 * np.sin(math.tau * (v * 8.0 + broad * 0.28))
        pores = smoothstep(0.72, 0.88, fine)
        color_field = np.clip(broad * 0.57 + sediment * 0.31 - pores * 0.12, 0.0, 1.0)
        height = broad * 0.58 + sediment * 0.29 - pores * 0.13
    elif profile == "mortar":
        grains = deterministic_fbm(u, v, seed + 211, 11, 3)
        color_field = np.clip(broad * 0.5 + grains * 0.5, 0.0, 1.0)
        height = broad * 0.43 + grains * 0.57
    elif profile in {"oak_leaf", "pine_needle"}:
        alpha, veins = foliage_mask_and_veins(u, v, profile)
        chlorophyll = np.clip(broad * 0.48 + fine * 0.18 + veins * 0.34, 0.0, 1.0)
        color_field = chlorophyll
        height = (broad * 0.18 + fine * 0.09 + veins * 0.73) * alpha
    else:
        raise RuntimeError(f"unsupported texture profile: {profile}")

    color_rgb = dark + (light - dark) * color_field[..., np.newaxis]
    if profile in {"oak_leaf", "pine_needle"}:
        color_rgb *= (0.83 + 0.17 * alpha)[..., np.newaxis]
    base_color_rgba = np.concatenate((np.clip(color_rgb, 0.0, 1.0), alpha[..., np.newaxis]), axis=-1)
    roughness_field = np.clip(roughness + (broad - 0.5) * 0.12 + (fine - 0.5) * 0.06, 0.45, 1.0)
    roughness_rgba = np.stack(
        (roughness_field, roughness_field, roughness_field, np.ones_like(roughness_field)),
        axis=-1,
    ).astype(np.float32)
    normal_strength = 5.8 if profile == "bark" else (4.2 if profile in {"rock", "limestone"} else 3.1)
    normal_rgb = normal_map_from_height(height.astype(np.float32), normal_strength)
    normal_rgba = np.concatenate((normal_rgb, np.ones((*normal_rgb.shape[:2], 1), dtype=np.float32)), axis=-1)
    return {
        "base_color": base_color_rgba.astype(np.float32),
        "roughness": roughness_rgba,
        "normal": normal_rgba.astype(np.float32),
    }


def save_texture_image(
    output_directory: Path,
    material_key: str,
    material_name: str,
    map_name: str,
    pixels: np.ndarray,
) -> tuple[bpy.types.Image, dict]:
    height, width, channels = pixels.shape
    if channels != 4:
        raise RuntimeError(f"texture {material_key}/{map_name} must have four channels")
    filename = f"r1063-{material_key.replace('_', '-')}-{map_name.replace('_', '-')}.png"
    path = output_directory / "textures" / filename
    image = bpy.data.images.new(f"R1063_{material_key}_{map_name}", width=width, height=height, alpha=True, float_buffer=False)
    colorspace = "sRGB" if map_name == "base_color" else "Non-Color"
    image.colorspace_settings.name = colorspace
    image.alpha_mode = "STRAIGHT"
    # Procedural colors share the scene-linear contract of Principled inputs.
    # Blender's byte-image pixel buffer stores encoded samples: naming its color
    # space does not encode foreach_set values. Encode RGB once for an sRGB PNG;
    # alpha, roughness and tangent normals remain linear data. Keep source arrays.
    encoded = pixels.copy()
    if map_name == "base_color":
        rgb = np.clip(encoded[..., :3], 0.0, 1.0)
        encoded[..., :3] = np.where(
            rgb <= 0.0031308,
            rgb * 12.92,
            1.055 * np.power(rgb, 1.0 / 2.4) - 0.055,
        )
    image.pixels.foreach_set(np.ascontiguousarray(encoded).ravel())
    image.filepath_raw = str(path)
    image.file_format = "PNG"
    image.save()
    image.pack()
    return image, {
        "material_key": material_key,
        "material_name": material_name,
        "map": map_name,
        "path": path.relative_to(output_directory).as_posix(),
        "sha256": sha256(path),
        "bytes": path.stat().st_size,
        "resolution": [width, height],
        "channels": channels,
        "colorspace": colorspace,
        "packed_in_blend": bool(image.packed_file),
        "embed_in_glb": True,
    }


def bind_pbr_texture_set(
    material: bpy.types.Material,
    material_key: str,
    images: dict[str, bpy.types.Image],
    foliage: bool,
    foliage_alpha_mode: str = "blend",
) -> None:
    if foliage_alpha_mode not in ("blend", "mask"):
        raise ValueError("foliage alpha mode must be blend or mask")
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    output.location = (760.0, 0.0)
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    principled.location = (470.0, 0.0)
    principled.inputs["Metallic"].default_value = 0.0
    base_node = nodes.new("ShaderNodeTexImage")
    base_node.name = f"R1063_{material_key}_BaseColor"
    base_node.label = "Base Color + Alpha"
    base_node.location = (-420.0, 180.0)
    base_node.image = images["base_color"]
    roughness_node = nodes.new("ShaderNodeTexImage")
    roughness_node.name = f"R1063_{material_key}_Roughness"
    roughness_node.label = "Roughness"
    roughness_node.location = (-420.0, -70.0)
    roughness_node.image = images["roughness"]
    normal_node = nodes.new("ShaderNodeTexImage")
    normal_node.name = f"R1063_{material_key}_Normal"
    normal_node.label = "Tangent Normal"
    normal_node.location = (-420.0, -330.0)
    normal_node.image = images["normal"]
    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.location = (160.0, -260.0)
    normal_map.inputs["Strength"].default_value = 0.82
    links.new(base_node.outputs["Color"], principled.inputs["Base Color"])
    links.new(roughness_node.outputs["Color"], principled.inputs["Roughness"])
    links.new(normal_node.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], principled.inputs["Normal"])
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    if foliage and foliage_alpha_mode == "mask":
        # These generated leaves use alpha for silhouette coverage, not glass-like
        # transmission. Blender's glTF exporter recognizes Round as MASK at 0.5.
        # Preserve the source texture, including its feathered edge texels.
        cutoff = nodes.new("ShaderNodeMath")
        cutoff.name = "ForgeFoliageAlphaCutout"
        cutoff.operation = "ROUND"
        cutoff.location = (160.0, 160.0)
        links.new(base_node.outputs["Alpha"], cutoff.inputs[0])
        links.new(cutoff.outputs[0], principled.inputs["Alpha"])
        material.use_backface_culling = False
        material.surface_render_method = "DITHERED"
    elif foliage:
        links.new(base_node.outputs["Alpha"], principled.inputs["Alpha"])
        material.use_backface_culling = False
        material.surface_render_method = "BLENDED"
    material["forge_material_model"] = "principled_pbr"
    material["forge_texture_maps"] = "base_color,roughness,normal"
    material["forge_foliage_alpha"] = foliage
    material["forge_foliage_alpha_mode"] = foliage_alpha_mode if foliage else "opaque"
    material["forge_glb_embedded_textures"] = True


def create_materials(
    output_directory: Path,
    texture_resolution: int,
    generator_seed: int,
    foliage_alpha_mode: str = "blend",
) -> tuple[dict[str, bpy.types.Material], list[dict]]:
    textured_specs = {
        "rock": ("R1063_Rock", (0.24, 0.25, 0.22, 1.0), 0.88, "rock", False),
        "moss": ("R1063_Moss", (0.12, 0.25, 0.055, 1.0), 0.92, "moss", False),
        "bark": ("R1063_Bark", (0.12, 0.055, 0.018, 1.0), 0.94, "bark", False),
        "oak_leaf": ("R1063_OakLeaf", (0.06, 0.26, 0.035, 1.0), 0.76, "oak_leaf", True),
        "oak_leaf_light": ("R1063_OakLeafLight", (0.13, 0.38, 0.07, 1.0), 0.74, "oak_leaf", True),
        "oak_leaf_dark": ("R1063_OakLeafDark", (0.025, 0.12, 0.018, 1.0), 0.82, "oak_leaf", True),
        "pine": ("R1063_PineNeedle", (0.025, 0.16, 0.07, 1.0), 0.81, "pine_needle", True),
        "pine_light": ("R1063_PineNeedleLight", (0.055, 0.26, 0.12, 1.0), 0.79, "pine_needle", True),
        "pine_dark": ("R1063_PineNeedleDark", (0.012, 0.07, 0.03, 1.0), 0.86, "pine_needle", True),
        "limestone": ("R1063_Limestone", (0.39, 0.31, 0.19, 1.0), 0.86, "limestone", False),
        "mortar": ("R1063_Mortar", (0.18, 0.17, 0.135, 1.0), 0.98, "mortar", False),
    }
    materials: dict[str, bpy.types.Material] = {}
    texture_records: list[dict] = []
    for index, (key, spec) in enumerate(textured_specs.items()):
        name, base_color, roughness, profile, foliage = spec
        material = make_principled_material(name, base_color, roughness)
        generated = build_material_texture_set(
            texture_resolution,
            generator_seed + (index + 1) * 1009,
            profile,
            base_color,
            roughness,
        )
        images: dict[str, bpy.types.Image] = {}
        for map_name in ("base_color", "roughness", "normal"):
            image, record = save_texture_image(output_directory, key, name, map_name, generated[map_name])
            images[map_name] = image
            texture_records.append(record)
        bind_pbr_texture_set(material, key, images, foliage, foliage_alpha_mode)
        materials[key] = material
    materials.update(
        {
            "wood": make_principled_material("R1063_Wood", (0.24, 0.105, 0.035, 1.0), 0.9),
            "collision": make_principled_material("R1063_Collision", (0.12, 0.32, 0.55, 1.0), 1.0),
            "ground": make_principled_material("R1063_Ground", (0.18, 0.24, 0.15, 1.0), 0.96),
            "scale": make_principled_material("R1063_ScaleMarker", (0.62, 0.27, 0.08, 1.0), 0.72),
        }
    )
    return materials, texture_records


def mesh_object(
    name: str,
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, ...]],
    collection: bpy.types.Collection,
    materials: Iterable[bpy.types.Material],
    material_indices: list[int] | None = None,
    smooth: bool = False,
) -> bpy.types.Object:
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.validate(clean_customdata=False)
    mesh.update(calc_edges=True)
    for material in materials:
        mesh.materials.append(material)
    if material_indices is not None:
        if len(material_indices) != len(mesh.polygons):
            raise RuntimeError(f"material index mismatch for {name}")
        for polygon, material_index in zip(mesh.polygons, material_indices, strict=True):
            polygon.material_index = material_index
    for polygon in mesh.polygons:
        polygon.use_smooth = smooth
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    return obj


def mark_asset_object(
    obj: bpy.types.Object,
    asset_id: str,
    role: str,
    lod: int | None = None,
) -> None:
    obj["forge_asset_id"] = asset_id
    obj["forge_role"] = role
    obj["forge_source_kind"] = "procedural_original"
    obj["forge_license"] = "LicenseRef-OpenRPG-Forge-Project-Generated"
    if lod is not None:
        obj["forge_lod"] = lod


def make_active(obj: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.hide_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def apply_modifier(obj: bpy.types.Object, modifier_name: str) -> None:
    make_active(obj)
    bpy.ops.object.modifier_apply(modifier=modifier_name)


def smart_uv(obj: bpy.types.Object) -> None:
    make_active(obj)
    if obj.data.uv_layers.get("UVMap") is None:
        uv_layer = obj.data.uv_layers.new(name="UVMap")
    else:
        uv_layer = obj.data.uv_layers.get("UVMap")
    obj.data.uv_layers.active = uv_layer
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(62.0), island_margin=0.025)
    bpy.ops.object.mode_set(mode="OBJECT")


def append_box(
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, ...]],
    center: tuple[float, float, float],
    size: tuple[float, float, float],
    rotation_z: float = 0.0,
) -> None:
    cx, cy, cz = center
    sx, sy, sz = (value * 0.5 for value in size)
    base = len(vertices)
    local = [
        (-sx, -sy, -sz),
        (sx, -sy, -sz),
        (sx, sy, -sz),
        (-sx, sy, -sz),
        (-sx, -sy, sz),
        (sx, -sy, sz),
        (sx, sy, sz),
        (-sx, sy, sz),
    ]
    cosine = math.cos(rotation_z)
    sine = math.sin(rotation_z)
    for x, y, z in local:
        vertices.append((cx + x * cosine - y * sine, cy + x * sine + y * cosine, cz + z))
    faces.extend(
        [
            (base + 0, base + 1, base + 2, base + 3),
            (base + 4, base + 7, base + 6, base + 5),
            (base + 0, base + 4, base + 5, base + 1),
            (base + 1, base + 5, base + 6, base + 2),
            (base + 2, base + 6, base + 7, base + 3),
            (base + 4, base + 0, base + 3, base + 7),
        ]
    )


def append_wedge_prism(
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, ...]],
    center_z: float,
    inner_radius: float,
    outer_radius: float,
    angle_a: float,
    angle_b: float,
    depth: float,
) -> None:
    base = len(vertices)
    half_depth = depth * 0.5
    ring = [
        (inner_radius * math.cos(angle_a), -half_depth, center_z + inner_radius * math.sin(angle_a)),
        (outer_radius * math.cos(angle_a), -half_depth, center_z + outer_radius * math.sin(angle_a)),
        (outer_radius * math.cos(angle_b), -half_depth, center_z + outer_radius * math.sin(angle_b)),
        (inner_radius * math.cos(angle_b), -half_depth, center_z + inner_radius * math.sin(angle_b)),
        (inner_radius * math.cos(angle_a), half_depth, center_z + inner_radius * math.sin(angle_a)),
        (outer_radius * math.cos(angle_a), half_depth, center_z + outer_radius * math.sin(angle_a)),
        (outer_radius * math.cos(angle_b), half_depth, center_z + outer_radius * math.sin(angle_b)),
        (inner_radius * math.cos(angle_b), half_depth, center_z + inner_radius * math.sin(angle_b)),
    ]
    vertices.extend(ring)
    faces.extend(
        [
            (base + 0, base + 1, base + 2, base + 3),
            (base + 4, base + 7, base + 6, base + 5),
            (base + 0, base + 4, base + 5, base + 1),
            (base + 1, base + 5, base + 6, base + 2),
            (base + 2, base + 6, base + 7, base + 3),
            (base + 3, base + 7, base + 4, base + 0),
        ]
    )


def append_tube(
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, ...]],
    points: list[Vector],
    radii: list[float],
    sides: int,
) -> None:
    if len(points) != len(radii) or len(points) < 2:
        raise RuntimeError("tube requires matching points and radii")
    base = len(vertices)
    for index, point in enumerate(points):
        previous = points[max(0, index - 1)]
        following = points[min(len(points) - 1, index + 1)]
        tangent = (following - previous).normalized()
        reference = Vector((0.0, 0.0, 1.0))
        if abs(tangent.dot(reference)) > 0.92:
            reference = Vector((1.0, 0.0, 0.0))
        basis_a = tangent.cross(reference).normalized()
        basis_b = tangent.cross(basis_a).normalized()
        for side in range(sides):
            angle = math.tau * side / sides
            bark_relief = (
                1.0
                + 0.035 * math.sin(side * 4.73 + index * 1.91 + point.z * 1.37)
                + 0.018 * math.cos(side * 8.21 - index * 0.83 + point.x * 2.4)
            )
            offset = (
                basis_a * math.cos(angle) + basis_b * math.sin(angle)
            ) * radii[index] * bark_relief
            value = point + offset
            vertices.append((value.x, value.y, value.z))
    for ring in range(len(points) - 1):
        ring_a = base + ring * sides
        ring_b = ring_a + sides
        for side in range(sides):
            next_side = (side + 1) % sides
            faces.append((ring_a + side, ring_a + next_side, ring_b + next_side, ring_b + side))
    faces.append(tuple(base + side for side in reversed(range(sides))))
    end = base + (len(points) - 1) * sides
    faces.append(tuple(end + side for side in range(sides)))


def add_bevel(obj: bpy.types.Object, width: float, segments: int) -> None:
    modifier = obj.modifiers.new("ForgeBevel", "BEVEL")
    modifier.width = width
    modifier.segments = segments
    modifier.limit_method = "ANGLE"
    apply_modifier(obj, modifier.name)


def create_deformed_icosphere(
    name: str,
    collection: bpy.types.Collection,
    material: bpy.types.Material,
    seed: int,
    dimensions: tuple[float, float, float],
    subdivisions: int,
    lod: int,
    collision: bool = False,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1.0)
    obj = bpy.context.object
    obj.name = name
    move_to_collection(obj, collection)
    rng = random.Random(seed)
    phase_a = rng.uniform(-math.pi, math.pi)
    phase_b = rng.uniform(-math.pi, math.pi)
    phase_c = rng.uniform(-math.pi, math.pi)
    sx, sy, sz = (dimensions[0] * 0.5, dimensions[1] * 0.5, dimensions[2] * 0.5)
    for vertex in obj.data.vertices:
        direction = vertex.co.normalized()
        coarse = (
            math.sin((direction.x * 1.7 + direction.y * 2.3 + direction.z * 2.9) * 2.6 + phase_a)
            + math.sin((-direction.x * 2.8 + direction.y * 1.3 + direction.z * 2.1) * 2.1 + phase_b)
            + math.cos((direction.x * 3.5 - direction.y * 2.7 + direction.z * 1.1) * 1.9 + phase_c)
        ) / 3.0
        fine = (
            math.sin((direction.x * 5.2 + direction.y * 8.1 - direction.z * 4.6) * 1.7 + phase_b)
            + math.cos((direction.x * 9.3 - direction.y * 3.4 + direction.z * 6.2) * 1.3 + phase_c)
        ) * 0.5
        displacement = 1.0 + coarse * (0.18 if not collision else 0.07) + fine * (0.045 if not collision else 0.0)
        shaped_x = direction.x * sx * displacement + direction.z * sx * 0.075
        shaped_y = direction.y * sy * displacement - direction.x * sy * 0.035
        shaped_z = direction.z * sz * displacement + sz
        vertex.co = (shaped_x, shaped_y, shaped_z)
        if vertex.co.z < 0.08:
            vertex.co.z *= 0.35
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.use_smooth = not collision
    smart_uv(obj)
    mark_asset_object(obj, name.split("_LOD")[0].lower().replace("_", "-"), "collision" if collision else "render", None if collision else lod)
    return obj


def apply_conforming_moss(
    asset_id: str,
    obj: bpy.types.Object,
    material: bpy.types.Material,
    dimensions: tuple[float, float, float],
    seed: int,
    lod: int,
) -> None:
    obj.data.materials.append(material)
    phase = random.Random(seed + 97).uniform(-math.pi, math.pi)
    moss_faces = 0
    for polygon in obj.data.polygons:
        center = sum(
            (obj.data.vertices[index].co for index in polygon.vertices),
            Vector((0.0, 0.0, 0.0)),
        ) / len(polygon.vertices)
        patch_field = (
            math.sin(center.x * 2.4 + center.y * 1.3 + phase)
            + math.cos(center.y * 3.1 - center.x * 0.8 - phase * 0.6)
            + math.sin((center.x - center.y) * 4.7 + phase * 1.4)
        ) / 3.0
        height_ratio = center.z / dimensions[2]
        if polygon.normal.z > 0.36 and height_ratio > 0.43 and patch_field > (-0.18 + lod * 0.08):
            polygon.material_index = 1
            moss_faces += 1
    if moss_faces == 0:
        raise RuntimeError(f"{asset_id} LOD{lod} generated no conforming moss faces")
    obj["forge_conforming_moss_faces"] = moss_faces


def build_rock_asset(asset: dict, collection: bpy.types.Collection, materials: dict[str, bpy.types.Material]) -> dict:
    asset_id = asset["id"]
    dimensions = tuple(float(value) for value in asset["dimensions_m"])
    seed = int(asset["seed"])
    lod_objects: dict[int, list[bpy.types.Object]] = {}
    for lod, subdivisions in enumerate((5, 4, 3)):
        rock = create_deformed_icosphere(
            f"{asset_id}_LOD{lod}",
            collection,
            materials["rock"],
            seed,
            dimensions,
            subdivisions,
            lod,
        )
        apply_conforming_moss(asset_id, rock, materials["moss"], dimensions, seed, lod)
        lod_objects[lod] = [rock]
    collision = create_deformed_icosphere(
        f"UCX_{asset_id}_01",
        collection,
        materials["collision"],
        seed,
        dimensions,
        2,
        2,
        collision=True,
    )
    collision.display_type = "WIRE"
    collision.hide_render = True
    mark_asset_object(collision, asset_id, "collision")
    return {"lods": lod_objects, "collisions": [collision]}


def tree_skeleton(
    seed: int,
    height: float,
    crown_radius: float,
    profile: str,
    lod: int,
) -> tuple[list[tuple[float, float, float]], list[tuple[int, ...]], list[Vector]]:
    rng = random.Random(seed + lod * 1009)
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    endpoints: list[Vector] = []
    sides = (18, 12, 8)[lod]
    trunk_rings = (28, 18, 11)[lod]
    trunk_points: list[Vector] = []
    trunk_radii: list[float] = []
    base_radius = height * (0.052 if profile == "broadleaf_oak" else 0.036)
    for index in range(trunk_rings):
        ratio = index / (trunk_rings - 1)
        sway = math.sin(ratio * math.pi * 1.7 + seed * 0.013) * height * (0.018 if profile == "broadleaf_oak" else 0.009)
        trunk_points.append(Vector((sway, math.cos(ratio * math.pi * 1.3 + seed) * sway * 0.42, ratio * height)))
        trunk_radii.append(base_radius * (1.0 - ratio * 0.78) * (1.0 + 0.08 * math.sin(ratio * 21.0)))
    append_tube(vertices, faces, trunk_points, trunk_radii, sides)
    root_count = 7 if lod == 0 else (5 if lod == 1 else 3)
    for root in range(root_count):
        angle = math.tau * root / root_count + rng.uniform(-0.14, 0.14)
        length = base_radius * rng.uniform(2.2, 3.6)
        points = [
            Vector((0.0, 0.0, base_radius * 0.38)),
            Vector((math.cos(angle) * length * 0.42, math.sin(angle) * length * 0.42, base_radius * 0.16)),
            Vector((math.cos(angle) * length, math.sin(angle) * length, 0.025)),
        ]
        append_tube(vertices, faces, points, [base_radius * 0.46, base_radius * 0.19, base_radius * 0.035], max(5, sides - 3))
    if profile == "broadleaf_oak":
        branch_count = (13, 9, 6)[lod]
        for index in range(branch_count):
            ratio = 0.28 + 0.6 * index / max(1, branch_count - 1)
            origin = trunk_points[min(trunk_rings - 2, int(ratio * (trunk_rings - 1)))]
            angle = index * 2.399963 + rng.uniform(-0.24, 0.24)
            length = crown_radius * (1.0 - 0.28 * ratio) * rng.uniform(0.78, 1.12)
            lift = height * rng.uniform(0.12, 0.23)
            direction = Vector((math.cos(angle), math.sin(angle), 0.0))
            points = [
                origin,
                origin + direction * length * 0.32 + Vector((0.0, 0.0, lift * 0.24)),
                origin + direction * length * 0.7 + Vector((0.0, 0.0, lift * 0.72)),
                origin + direction * length + Vector((0.0, 0.0, lift)),
            ]
            radius = base_radius * (0.36 + 0.18 * (1.0 - ratio))
            append_tube(vertices, faces, points, [radius, radius * 0.62, radius * 0.3, radius * 0.055], max(5, sides - 3))
            endpoints.extend(points[1:])
            if lod < 2:
                for split in (-1.0, 1.0):
                    split_angle = angle + split * rng.uniform(0.38, 0.72)
                    split_direction = Vector((math.cos(split_angle), math.sin(split_angle), 0.0))
                    start = points[2]
                    child_end = start + split_direction * length * rng.uniform(0.28, 0.46) + Vector((0.0, 0.0, lift * rng.uniform(0.22, 0.48)))
                    child_points = [
                        start,
                        (start + child_end) * 0.5 + Vector((0.0, 0.0, lift * 0.08)),
                        child_end,
                    ]
                    append_tube(
                        vertices,
                        faces,
                        child_points,
                        [radius * 0.24, radius * 0.13, radius * 0.025],
                        max(5, sides - 5),
                    )
                    endpoints.extend(child_points[1:])
    else:
        tier_count = (21, 14, 9)[lod]
        for tier in range(tier_count):
            ratio = 0.2 + 0.71 * tier / max(1, tier_count - 1)
            origin = trunk_points[min(trunk_rings - 2, int(ratio * (trunk_rings - 1)))]
            tier_radius = crown_radius * max(0.16, (1.0 - ratio) ** 0.72)
            arms = 5 if lod == 0 else (4 if lod == 1 else 3)
            for arm in range(arms):
                angle = math.tau * arm / arms + tier * 0.83 + rng.uniform(-0.11, 0.11)
                direction = Vector((math.cos(angle), math.sin(angle), 0.0))
                length = tier_radius * rng.uniform(0.78, 1.07)
                lateral = Vector((-math.sin(angle), math.cos(angle), 0.0))
                end = origin + direction * length + Vector((0.0, 0.0, -length * 0.09 + height * 0.022))
                points = [
                    origin,
                    origin + direction * length * 0.24 + lateral * length * 0.018 + Vector((0.0, 0.0, height * 0.018)),
                    origin + direction * length * 0.5 + lateral * length * 0.028 + Vector((0.0, 0.0, height * 0.026)),
                    origin + direction * length * 0.76 + lateral * length * 0.018 + Vector((0.0, 0.0, height * 0.012)),
                    end,
                ]
                append_tube(
                    vertices,
                    faces,
                    points,
                    [base_radius * 0.16, base_radius * 0.12, base_radius * 0.075, base_radius * 0.038, base_radius * 0.012],
                    max(5, sides - 5),
                )
                endpoints.extend((points[2], points[-1]))
        endpoints.append(trunk_points[-1])
    return vertices, faces, endpoints


def leaf_cloud(
    name: str,
    asset_id: str,
    collection: bpy.types.Collection,
    materials: list[bpy.types.Material],
    endpoints: list[Vector],
    seed: int,
    profile: str,
    lod: int,
    crown_radius: float,
) -> bpy.types.Object:
    rng = random.Random(seed + 7001 + lod * 131)
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    material_indices: list[int] = []
    if profile == "broadleaf_oak":
        per_endpoint = (80, 30, 10)[lod]
        leaf_length = (0.29, 0.42, 0.64)[lod]
        leaf_width = leaf_length * 0.48
        spread = crown_radius * (0.18 if lod == 0 else (0.23 if lod == 1 else 0.31))
    else:
        per_endpoint = (38, 14, 5)[lod]
        leaf_length = (0.42, 0.62, 0.96)[lod]
        leaf_width = leaf_length * 0.1
        spread = crown_radius * (0.13 if lod == 0 else (0.17 if lod == 1 else 0.22))
    for endpoint in endpoints:
        for _ in range(per_endpoint):
            radial = Vector((rng.uniform(-1.0, 1.0), rng.uniform(-1.0, 1.0), rng.uniform(-0.55, 0.75)))
            if radial.length_squared < 0.01:
                radial = Vector((1.0, 0.0, 0.0))
            center = endpoint + radial.normalized() * spread * (rng.random() ** 0.55)
            yaw = rng.random() * math.tau
            pitch = rng.uniform(-0.7, 0.7)
            forward = Vector((math.cos(yaw), math.sin(yaw), math.sin(pitch))).normalized()
            side = Vector((-math.sin(yaw), math.cos(yaw), rng.uniform(-0.18, 0.18))).normalized()
            normal = forward.cross(side).normalized()
            fold = leaf_width * rng.uniform(0.12, 0.28)
            base = len(vertices)
            vertices.extend(
                [
                    tuple(center - forward * leaf_length * 0.52),
                    tuple(center - forward * leaf_length * 0.16 + side * leaf_width * 0.56),
                    tuple(center + forward * leaf_length * 0.18 + side * leaf_width),
                    tuple(center + forward * leaf_length * 0.58),
                    tuple(center + forward * leaf_length * 0.18 - side * leaf_width),
                    tuple(center - forward * leaf_length * 0.16 - side * leaf_width * 0.56),
                    tuple(center + normal * fold),
                ]
            )
            faces.extend(
                [
                    (base + 6, base + 0, base + 1),
                    (base + 6, base + 1, base + 2),
                    (base + 6, base + 2, base + 3),
                    (base + 6, base + 3, base + 4),
                    (base + 6, base + 4, base + 5),
                    (base + 6, base + 5, base + 0),
                ]
            )
            tone = rng.random()
            material_index = 1 if tone < 0.56 else (0 if tone < 0.78 else 2)
            material_indices.extend([material_index] * 6)
    obj = mesh_object(name, vertices, faces, collection, materials, material_indices, smooth=False)
    uv_layer = obj.data.uv_layers.new(name="UVMap")
    uv_pattern = ((0.5, 0.0), (0.86, 0.24), (1.0, 0.68), (0.5, 1.0), (0.0, 0.68), (0.14, 0.24), (0.5, 0.5))
    for polygon in obj.data.polygons:
        for vertex_index, loop_index in zip(polygon.vertices, polygon.loop_indices, strict=True):
            uv_layer.data[loop_index].uv = uv_pattern[vertex_index % 7]
    mark_asset_object(obj, asset_id, "render", lod)
    return obj


def build_tree_asset(asset: dict, collection: bpy.types.Collection, materials: dict[str, bpy.types.Material]) -> dict:
    asset_id = asset["id"]
    profile = str(asset["generator_profile"])
    dimensions = tuple(float(value) for value in asset["dimensions_m"])
    height = dimensions[2]
    crown_radius = max(dimensions[0], dimensions[1]) * 0.5
    seed = int(asset["seed"])
    lod_objects: dict[int, list[bpy.types.Object]] = {}
    for lod in range(3):
        vertices, faces, endpoints = tree_skeleton(seed, height, crown_radius, profile, lod)
        wood = mesh_object(f"{asset_id}_LOD{lod}_Wood", vertices, faces, collection, [materials["bark"]], smooth=True)
        smart_uv(wood)
        mark_asset_object(wood, asset_id, "render", lod)
        foliage_materials = (
            [materials["oak_leaf_dark"], materials["oak_leaf"], materials["oak_leaf_light"]]
            if profile == "broadleaf_oak"
            else [materials["pine_dark"], materials["pine"], materials["pine_light"]]
        )
        leaves = leaf_cloud(
            f"{asset_id}_LOD{lod}_Foliage",
            asset_id,
            collection,
            foliage_materials,
            endpoints,
            seed,
            profile,
            lod,
            crown_radius,
        )
        lod_objects[lod] = [wood, leaves]
    collisions: list[bpy.types.Object] = []
    trunk_radius = height * (0.052 if profile == "broadleaf_oak" else 0.036)
    bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=trunk_radius * 1.08, depth=height * 0.58, location=(0.0, 0.0, height * 0.29))
    trunk_collision = bpy.context.object
    trunk_collision.name = f"UCX_{asset_id}_Trunk"
    move_to_collection(trunk_collision, collection)
    trunk_collision.data.materials.append(materials["collision"])
    smart_uv(trunk_collision)
    mark_asset_object(trunk_collision, asset_id, "collision")
    trunk_collision.hide_render = True
    trunk_collision.display_type = "WIRE"
    collisions.append(trunk_collision)
    if profile == "broadleaf_oak":
        for index, angle in enumerate((0.4, 2.5, 4.6), start=1):
            length = crown_radius * 0.74
            bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=trunk_radius * 0.28, depth=length)
            branch = bpy.context.object
            branch.name = f"UCX_{asset_id}_Branch{index:02d}"
            move_to_collection(branch, collection)
            branch.rotation_euler = (0.0, math.radians(68.0), angle)
            branch.location = (math.cos(angle) * length * 0.25, math.sin(angle) * length * 0.25, height * 0.58)
            branch.data.materials.append(materials["collision"])
            smart_uv(branch)
            mark_asset_object(branch, asset_id, "collision")
            branch.hide_render = True
            branch.display_type = "WIRE"
            collisions.append(branch)
    return {"lods": lod_objects, "collisions": collisions}


def limestone_course_blocks(
    length: float,
    height: float,
    lod: int,
    rng: random.Random,
):
    """Yield the shared limestone coursing used by straight and corner walls.

    A corner is not a separate, slab-like material language.  Its two arms
    deliberately reuse the straight wall's course height, stagger, span
    jitter, depth jitter, vertical jitter and tiny yaw.  Callers only choose
    the horizontal axis on which that common profile is laid out.
    """
    course_height = 0.34 if lod == 0 else 0.56
    courses = max(3, round(height / course_height))
    nominal = 0.58 if lod == 0 else 0.9
    for row in range(courses):
        z = (row + 0.5) * height / courses
        offset = -(nominal * 0.5 if row % 2 else 0.0)
        while offset < length:
            block_length = nominal * rng.uniform(0.78, 1.18)
            center_length = offset + block_length * 0.5
            if center_length - block_length * 0.5 < 0.0:
                center_length = block_length * 0.5
            if center_length + block_length * 0.5 > length:
                block_length = length - (center_length - block_length * 0.5)
                center_length = length - block_length * 0.5
            if block_length > 0.12:
                # Preserve the random draw order used by limestone_wall so a
                # given asset seed remains deterministic across this refactor.
                yield (
                    center_length,
                    z,
                    block_length,
                    rng.uniform(-0.025, 0.025),
                    rng.uniform(0.94, 1.04),
                    rng.uniform(-0.018, 0.018),
                    rng.uniform(-0.018, 0.018),
                )
            offset += max(block_length, nominal * 0.7)


def architecture_wall_geometry(
    asset_id: str,
    collection: bpy.types.Collection,
    material: bpy.types.Material,
    mortar: bpy.types.Material,
    dimensions: tuple[float, float, float],
    seed: int,
    lod: int,
) -> list[bpy.types.Object]:
    rng = random.Random(seed + lod * 419)
    width, depth, height = dimensions
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    material_indices: list[int] = []
    if lod < 2:
        courses = max(3, round(height / (0.34 if lod == 0 else 0.56)))
        for center_x, z, block_width, center_y, depth_scale, z_jitter, yaw in limestone_course_blocks(width, height, lod, rng):
            before = len(faces)
            append_box(
                vertices,
                faces,
                (center_x - width * 0.5, center_y, z + z_jitter),
                (block_width * 0.96, depth * depth_scale, height / courses * 0.9),
                yaw,
            )
            material_indices.extend([0] * (len(faces) - before))
        # Modular walls meet at +/- width/2. Insetting the backing in X/Z opens
        # a through-gap between panels even though their collision boxes touch.
        # Keep relief in depth only; joints at the module boundary retain mortar.
        append_box(vertices, faces, (0.0, 0.0, height * 0.5), (width, depth * 0.78, height))
        material_indices.extend([1] * 6)
    else:
        append_box(vertices, faces, (0.0, 0.0, height * 0.5), (width, depth, height))
        material_indices.extend([0] * 6)
    obj = mesh_object(f"{asset_id}_LOD{lod}_Masonry", vertices, faces, collection, [material, mortar], material_indices)
    if lod < 2:
        add_bevel(obj, 0.028 if lod == 0 else 0.018, 2 if lod == 0 else 1)
    smart_uv(obj)
    mark_asset_object(obj, asset_id, "render", lod)
    return [obj]


def architecture_wall_corner_geometry(
    asset_id: str,
    collection: bpy.types.Collection,
    material: bpy.types.Material,
    mortar: bpy.types.Material,
    dimensions: tuple[float, float, float],
    seed: int,
    lod: int,
) -> list[bpy.types.Object]:
    """Build an L-shaped, ground-pivoted modular wall corner.

    The pivot is the exterior ground corner at (0, 0, 0).  Its two 4 m arms
    extend along +X and +Y, with their 0.72 m thickness facing inward.  The
    full-size mortar backing deliberately overlaps at the elbow, so a module
    attached to either arm cannot reveal a through-gap at its boundary.
    """
    width_x, width_y, height = dimensions
    if abs(width_x - width_y) > 1e-6:
        raise RuntimeError("limestone_wall_corner requires equal arm lengths")
    thickness = 0.72
    rng = random.Random(seed + lod * 631)
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    material_indices: list[int] = []

    def add_box(
        center: tuple[float, float, float],
        size: tuple[float, float, float],
        material_index: int,
        rotation_z: float = 0.0,
    ) -> None:
        before = len(faces)
        append_box(vertices, faces, center, size, rotation_z)
        material_indices.extend([material_index] * (len(faces) - before))

    # Each backing reaches the exterior corner.  Their overlap is intentional:
    # it keeps both arm boundaries closed after independent GLB reimport.
    add_box((width_x * 0.5, thickness * 0.5, height * 0.5), (width_x, thickness, height), 1)
    add_box((thickness * 0.5, width_y * 0.5, height * 0.5), (thickness, width_y, height), 1)
    if lod < 2:
        courses = max(3, round(height / (0.34 if lod == 0 else 0.56)))
        # The grounded pivot is the *outside* elbow.  The renderable facing
        # must therefore project out of the X=0/Y=0 exterior planes, just as
        # the straight limestone-wall relief projects out of both faces of its
        # mortar backing.  Keeping it on the inward faces made the two public
        # faces of the corner read as plain mortar while its attached straight
        # modules visibly carried stone courses.
        for arm in ("x", "y"):
            length = width_x if arm == "x" else width_y
            for center_length, z, block_length, depth_offset, depth_scale, z_jitter, yaw in limestone_course_blocks(length, height, lod, rng):
                block_height = height / courses * 0.9
                if arm == "x":
                    center = (center_length, depth_offset, z + z_jitter)
                    size = (block_length * 0.96, thickness * depth_scale, block_height)
                    rotation = yaw
                else:
                    # This is the same straight-wall box rotated into +Y.
                    # The sign maps its local depth jitter to the +Y arm's
                    # X-axis while retaining the exterior X=0 facing.
                    center = (-depth_offset, center_length, z + z_jitter)
                    size = (block_length * 0.96, thickness * depth_scale, block_height)
                    rotation = math.pi * 0.5 + yaw
                add_box(center, size, 0, rotation)
    obj = mesh_object(
        f"{asset_id}_LOD{lod}_Masonry",
        vertices,
        faces,
        collection,
        [material, mortar],
        material_indices,
    )
    if lod < 2:
        add_bevel(obj, 0.028 if lod == 0 else 0.018, 2 if lod == 0 else 1)
    smart_uv(obj)
    mark_asset_object(obj, asset_id, "render", lod)
    return [obj]


def architecture_arch_geometry(
    asset_id: str,
    collection: bpy.types.Collection,
    material: bpy.types.Material,
    mortar: bpy.types.Material,
    dimensions: tuple[float, float, float],
    seed: int,
    lod: int,
) -> list[bpy.types.Object]:
    rng = random.Random(seed + lod * 811)
    width, depth, height = dimensions
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    material_indices: list[int] = []
    opening_half = width * 0.28
    spring_z = height * 0.47
    outer_radius = opening_half + (0.52 if lod == 0 else 0.42)
    pier_width = (width - opening_half * 2.0) * 0.5
    courses = 7 if lod == 0 else (4 if lod == 1 else 1)
    if lod < 2:
        for side in (-1.0, 1.0):
            for row in range(courses):
                z = spring_z * (row + 0.5) / courses
                x = side * (opening_half + pier_width * 0.5)
                before = len(faces)
                append_box(
                    vertices,
                    faces,
                    (x + rng.uniform(-0.018, 0.018), rng.uniform(-0.018, 0.018), z),
                    (pier_width * 0.94, depth * rng.uniform(0.96, 1.03), spring_z / courses * 0.9),
                    rng.uniform(-0.014, 0.014),
                )
                material_indices.extend([0] * (len(faces) - before))
            for z, width_scale, cap_height in (
                (0.11, 1.08, 0.22),
                (spring_z - 0.12, 1.04, 0.24),
            ):
                before = len(faces)
                append_box(
                    vertices,
                    faces,
                    (side * (opening_half + pier_width * 0.5), -0.01, z),
                    (pier_width * width_scale, depth * 1.055, cap_height),
                )
                material_indices.extend([0] * (len(faces) - before))
        segments = 15 if lod == 0 else 9
        for index in range(segments):
            angle_a = math.pi * index / segments
            angle_b = math.pi * (index + 1) / segments
            before = len(faces)
            append_wedge_prism(vertices, faces, spring_z, opening_half, outer_radius, angle_a, angle_b, depth * 1.04)
            material_indices.extend([0] * (len(faces) - before))
    else:
        side_width = (width - opening_half * 2.0) * 0.5
        for side in (-1.0, 1.0):
            append_box(vertices, faces, (side * (opening_half + side_width * 0.5), 0.0, spring_z * 0.5), (side_width, depth, spring_z))
            material_indices.extend([0] * 6)
        segments = 6
        for index in range(segments):
            before = len(faces)
            append_wedge_prism(vertices, faces, spring_z, opening_half, outer_radius, math.pi * index / segments, math.pi * (index + 1) / segments, depth)
            material_indices.extend([0] * (len(faces) - before))
    obj = mesh_object(f"{asset_id}_LOD{lod}_Masonry", vertices, faces, collection, [material, mortar], material_indices)
    if lod < 2:
        add_bevel(obj, 0.032 if lod == 0 else 0.018, 2 if lod == 0 else 1)
    smart_uv(obj)
    mark_asset_object(obj, asset_id, "render", lod)
    return [obj]


def architecture_collisions(
    asset: dict,
    collection: bpy.types.Collection,
    material: bpy.types.Material,
) -> list[bpy.types.Object]:
    asset_id = asset["id"]
    width, depth, height = (float(value) for value in asset["dimensions_m"])
    profile = asset["generator_profile"]
    specs: list[tuple[str, tuple[float, float, float], tuple[float, float, float]]]
    if profile == "limestone_wall":
        specs = [("01", (0.0, 0.0, height * 0.5), (width, depth, height))]
    elif profile == "limestone_wall_corner":
        thickness = 0.72
        if abs(width - depth) > 1e-6:
            raise RuntimeError("limestone_wall_corner requires equal arm lengths")
        specs = [
            ("ArmX", (width * 0.5, thickness * 0.5, height * 0.5), (width, thickness, height)),
            ("ArmY", (thickness * 0.5, depth * 0.5, height * 0.5), (thickness, depth, height)),
        ]
    else:
        opening_half = width * 0.28
        spring_z = height * 0.47
        side_width = (width - opening_half * 2.0) * 0.5
        specs = [
            ("Left", (-(opening_half + side_width * 0.5), 0.0, spring_z * 0.5), (side_width, depth, spring_z)),
            ("Right", ((opening_half + side_width * 0.5), 0.0, spring_z * 0.5), (side_width, depth, spring_z)),
            ("Top", (0.0, 0.0, height - (height - spring_z) * 0.25), (width, depth, (height - spring_z) * 0.5)),
        ]
    collisions: list[bpy.types.Object] = []
    for suffix, center, size in specs:
        vertices: list[tuple[float, float, float]] = []
        faces: list[tuple[int, ...]] = []
        append_box(vertices, faces, center, size)
        obj = mesh_object(f"UCX_{asset_id}_{suffix}", vertices, faces, collection, [material])
        smart_uv(obj)
        mark_asset_object(obj, asset_id, "collision")
        obj.hide_render = True
        obj.display_type = "WIRE"
        collisions.append(obj)
    return collisions


def build_architecture_asset(asset: dict, collection: bpy.types.Collection, materials: dict[str, bpy.types.Material]) -> dict:
    profile = str(asset["generator_profile"])
    dimensions = tuple(float(value) for value in asset["dimensions_m"])
    seed = int(asset["seed"])
    lod_objects: dict[int, list[bpy.types.Object]] = {}
    for lod in range(3):
        if profile == "limestone_wall":
            lod_objects[lod] = architecture_wall_geometry(asset["id"], collection, materials["limestone"], materials["mortar"], dimensions, seed, lod)
        elif profile == "limestone_wall_corner":
            lod_objects[lod] = architecture_wall_corner_geometry(asset["id"], collection, materials["limestone"], materials["mortar"], dimensions, seed, lod)
        elif profile == "limestone_arch":
            lod_objects[lod] = architecture_arch_geometry(asset["id"], collection, materials["limestone"], materials["mortar"], dimensions, seed, lod)
        else:
            raise RuntimeError(f"unsupported architecture profile: {profile}")
    collisions = architecture_collisions(asset, collection, materials["collision"])
    return {"lods": lod_objects, "collisions": collisions}


def object_stats(objects: list[bpy.types.Object]) -> dict:
    vertices = sum(len(obj.data.vertices) for obj in objects if obj.type == "MESH")
    polygons = sum(len(obj.data.polygons) for obj in objects if obj.type == "MESH")
    triangles = sum(
        sum(max(0, len(polygon.vertices) - 2) for polygon in obj.data.polygons)
        for obj in objects
        if obj.type == "MESH"
    )
    return {"objects": len(objects), "vertices": vertices, "polygons": polygons, "triangles": triangles}


def validate_asset(asset: dict, built: dict) -> dict:
    budgets = asset["lod_triangle_budgets"]
    lod_stats: dict[str, dict] = {}
    previous_triangles: int | None = None
    for lod in range(3):
        objects = built["lods"][lod]
        stats = object_stats(objects)
        if stats["triangles"] > budgets[lod]:
            raise RuntimeError(
                f"{asset['id']} LOD{lod} exceeds triangle budget: {stats['triangles']} > {budgets[lod]}"
            )
        if previous_triangles is not None and stats["triangles"] >= previous_triangles:
            raise RuntimeError(f"{asset['id']} LOD triangle counts must strictly decrease")
        previous_triangles = stats["triangles"]
        missing_uv = [
            obj.name
            for obj in objects
            if obj.type == "MESH" and obj.data.uv_layers.get("UVMap") is None
        ]
        if missing_uv:
            raise RuntimeError(f"{asset['id']} LOD{lod} missing UVMap: {missing_uv}")
        lod_stats[f"LOD{lod}"] = {**stats, "triangle_budget": budgets[lod], "objects_named": sorted(obj.name for obj in objects)}
    if not built["collisions"]:
        raise RuntimeError(f"{asset['id']} has no collision object")
    return {
        "lods": lod_stats,
        "collisions": object_stats(built["collisions"]),
        "collision_objects": sorted(obj.name for obj in built["collisions"]),
        "uv_layer": "UVMap",
    }


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def configure_presentation(materials: dict[str, bpy.types.Material], width: int, height: int) -> dict:
    scene = bpy.context.scene
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.dither_intensity = 0.0
    scene.render.film_transparent = False
    scene.render.use_file_extension = True
    use_eevee(scene)
    scene.eevee.taa_render_samples = 64
    scene.world.use_nodes = True
    world_nodes = scene.world.node_tree.nodes
    world_links = scene.world.node_tree.links
    world_nodes.clear()
    world_output = world_nodes.new("ShaderNodeOutputWorld")
    background = world_nodes.new("ShaderNodeBackground")
    sky = world_nodes.new("ShaderNodeTexSky")
    configure_sky_type(sky)
    sky.sun_elevation = math.radians(31.0)
    sky.sun_rotation = math.radians(118.0)
    sky.altitude = 0.35
    sky.air_density = 0.92
    # Blender 5.2's MULTIPLE_SCATTERING sky API removed these Nishita controls.
    if hasattr(sky, "dust_density"):
        sky.dust_density = 1.35
    if hasattr(sky, "ozone_density"):
        sky.ozone_density = 1.05
    background.inputs["Strength"].default_value = 0.32
    world_links.new(sky.outputs["Color"], background.inputs["Color"])
    world_links.new(background.outputs["Background"], world_output.inputs["Surface"])
    scene.view_settings.exposure = -0.12
    try:
        scene.view_settings.look = "AgX - Medium High Contrast"
    except TypeError:
        pass
    presentation = new_collection("R1063_Presentation")
    bpy.ops.mesh.primitive_plane_add(size=160.0, location=(0.0, 0.0, -0.025))
    ground = bpy.context.object
    ground.name = "R1063_PresentationGround"
    move_to_collection(ground, presentation)
    ground.data.materials.append(materials["ground"])
    bpy.ops.object.light_add(type="SUN", location=(0.0, -8.0, 18.0))
    sun = bpy.context.object
    sun.name = "R1063_Sun"
    move_to_collection(sun, presentation)
    sun.rotation_euler = (math.radians(34.0), math.radians(-22.0), math.radians(-32.0))
    sun.data.energy = 2.0
    sun.data.color = (1.0, 0.78, 0.52)
    sun.data.angle = math.radians(5.0)
    bpy.ops.object.light_add(type="AREA", location=(-7.0, -9.0, 11.0))
    key = bpy.context.object
    key.name = "R1063_Key"
    move_to_collection(key, presentation)
    key.data.energy = 950.0
    key.data.shape = "DISK"
    key.data.size = 7.0
    key.data.color = (1.0, 0.89, 0.72)
    look_at(key, Vector((0.0, 0.0, 4.0)))
    bpy.ops.object.light_add(type="AREA", location=(8.0, 4.0, 7.0))
    rim = bpy.context.object
    rim.name = "R1063_Rim"
    move_to_collection(rim, presentation)
    rim.data.energy = 420.0
    rim.data.size = 6.0
    rim.data.color = (0.48, 0.58, 0.72)
    look_at(rim, Vector((0.0, 0.0, 5.0)))
    camera_data = bpy.data.cameras.new("R1063_TurntableCamera")
    camera_data.lens = 56.0
    camera_data.sensor_width = 36.0
    camera_data.clip_start = 0.1
    camera_data.clip_end = 300.0
    camera = bpy.data.objects.new("R1063_TurntableCamera", camera_data)
    presentation.objects.link(camera)
    scene.camera = camera
    return {"collection": presentation, "ground": ground, "lights": [sun, key, rim], "camera": camera}


def set_asset_render_visibility(all_built: dict[str, dict], selected_asset_id: str | None) -> None:
    for asset_id, built in all_built.items():
        for lod, objects in built["lods"].items():
            visible = asset_id == selected_asset_id and lod == 0
            for obj in objects:
                obj.hide_render = not visible
        for obj in built["collisions"]:
            obj.hide_render = True


def camera_for_asset(camera: bpy.types.Object, objects: list[bpy.types.Object], angles: list[int]) -> None:
    """Fit all turntable poses, including authored transforms, with 8% image margins.

    Recipe dimensions are not bounds after authoring. Use the actual render meshes
    and the camera frame (including render/pixel aspect), not a fixed distance.
    One camera for the whole sequence avoids zoom pumping between poses.
    """
    if not objects or not angles or camera.data.type != "PERSP":
        raise RuntimeError("turntable framing requires meshes, angles and a perspective camera")
    scene = bpy.context.scene
    rotations = {obj.name: obj.rotation_euler.copy() for obj in objects}
    points: list[Vector] = []
    try:
        for angle in angles:
            for obj in objects:
                obj.rotation_euler.z = rotations[obj.name].z + math.radians(angle)
            bpy.context.view_layer.update()
            points.extend(obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box)
    finally:
        for obj in objects:
            obj.rotation_euler = rotations[obj.name]
        bpy.context.view_layer.update()
    target = Vector(tuple((min(p[i] for p in points) + max(p[i] for p in points)) * 0.5 for i in range(3)))
    direction = Vector((0.82, -1.0, 0.35)).normalized()
    camera.location = target + direction
    look_at(camera, target)
    rotation_inverse = camera.rotation_euler.to_matrix().transposed()
    frame = camera.data.view_frame(scene=scene)
    # The worker creates a centered lens; shifted lenses need asymmetric fitting.
    if camera.data.shift_x != 0.0 or camera.data.shift_y != 0.0:
        raise RuntimeError("turntable framing requires a centered lens")
    tan_x = min(abs(p.x / p.z) for p in frame) * 0.84
    tan_y = min(abs(p.y / p.z) for p in frame) * 0.84
    local = [rotation_inverse @ (p - target) for p in points]
    distance = max(max(p.z + abs(p.x) / tan_x, p.z + abs(p.y) / tan_y, p.z + camera.data.clip_start * 2.0) for p in local)
    camera.location = target + direction * distance
    camera.data.clip_end = max(camera.data.clip_end, max(distance - p.z for p in local) * 1.1)
    bpy.context.view_layer.update()


def render_turntable(
    asset: dict,
    built: dict,
    all_built: dict[str, dict],
    camera: bpy.types.Object,
    angles: list[int],
    output_directory: Path,
) -> list[Path]:
    asset_id = asset["id"]
    set_asset_render_visibility(all_built, asset_id)
    camera_for_asset(camera, built["lods"][0], angles)
    paths: list[Path] = []
    lod0 = built["lods"][0]
    rotations = {obj.name: obj.rotation_euler.copy() for obj in lod0}
    try:
        for angle in angles:
            radians = math.radians(angle)
            for obj in lod0:
                obj.rotation_euler.z = rotations[obj.name].z + radians
            path = output_directory / f"turntable-{angle:03d}.png"
            bpy.context.scene.render.filepath = str(path)
            bpy.ops.render.render(write_still=True)
            paths.append(path)
    finally:
        for obj in lod0:
            obj.rotation_euler = rotations[obj.name]
        bpy.context.view_layer.update()
    return paths


def duplicate_for_lineup(
    source_objects: list[bpy.types.Object],
    asset_id: str,
    offset_x: float,
    collection: bpy.types.Collection,
) -> list[bpy.types.Object]:
    duplicates: list[bpy.types.Object] = []
    for source in source_objects:
        duplicate = source.copy()
        duplicate.data = source.data.copy()
        duplicate.name = f"Preview_{asset_id}_{source.name}"
        collection.objects.link(duplicate)
        duplicate.location.x += offset_x
        duplicate.hide_render = False
        duplicates.append(duplicate)
    return duplicates


def render_lineup(
    assets: list[dict],
    all_built: dict[str, dict],
    presentation: dict,
    material: bpy.types.Material,
    output_path: Path,
) -> Path:
    set_asset_render_visibility(all_built, None)
    preview_collection = new_collection("R1063_Lineup", presentation["collection"])
    offsets = (-19.0, -13.0, -6.5, 1.5, 9.0, 15.0)
    marker_x = -1.8
    if len(assets) != len(offsets):
        # Preserve the historical six-asset framing, but allow focused derivative
        # kits. Space their actual mesh bounds, including any authored edits.
        bpy.context.view_layer.update()
        cursor = 0.0
        positions = []
        for asset in assets:
            points = [
                obj.matrix_world @ Vector(corner)
                for obj in all_built[asset["id"]]["lods"][0]
                for corner in obj.bound_box
            ]
            minimum = min(point.x for point in points)
            maximum = max(point.x for point in points)
            positions.append(cursor - minimum)
            cursor += maximum - minimum + 2.0
        centre = (cursor - 2.0) * 0.5
        offsets = tuple(position - centre for position in positions)
        marker_x = -centre - 1.0
    for asset, offset in zip(assets, offsets, strict=True):
        duplicate_for_lineup(all_built[asset["id"]]["lods"][0], asset["id"], offset, preview_collection)
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=0.18, depth=1.8, location=(marker_x, 0.0, 0.9))
    marker = bpy.context.object
    marker.name = "R1063_HumanScale_1_8m"
    move_to_collection(marker, preview_collection)
    marker.data.materials.append(material)
    camera = presentation["camera"]
    try:
        # Fit the complete arrangement and its scale marker using actual bounds.
        # A fixed camera cropped taller trees and subsequent authored edits.
        camera_for_asset(camera, list(preview_collection.objects), [0])
        bpy.context.scene.render.filepath = str(output_path)
        bpy.ops.render.render(write_still=True)
    finally:
        for obj in list(preview_collection.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.collections.remove(preview_collection)
    return output_path


def export_asset_glb(asset_id: str, built: dict, path: Path) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    selected = [obj for lod in range(3) for obj in built["lods"][lod]] + list(built["collisions"])
    previous_hide: dict[str, bool] = {}
    for obj in selected:
        previous_hide[obj.name] = obj.hide_get()
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = selected[0]
    bpy.ops.export_scene.gltf(
        filepath=str(path),
        export_format="GLB",
        use_selection=True,
        export_yup=True,
        export_apply=True,
        export_cameras=False,
        export_lights=False,
        export_animations=False,
        export_extras=True,
    )
    for obj in selected:
        obj.select_set(False)
        obj.hide_set(previous_hide[obj.name])


def build_asset(asset: dict, collection: bpy.types.Collection, materials: dict[str, bpy.types.Material]) -> dict:
    if asset["kind"] == "rock":
        return build_rock_asset(asset, collection, materials)
    if asset["kind"] == "tree":
        return build_tree_asset(asset, collection, materials)
    if asset["kind"] == "architecture":
        return build_architecture_asset(asset, collection, materials)
    raise RuntimeError(f"unsupported asset kind: {asset['kind']}")


def load_authored_source(path: Path | None, manifest_path: Path, manifest: dict) -> dict | None:
    """Bind an explicit, immutable authored asset source to its generating recipe.

    This is a DCC worker input, not a Studio promotion command. Never rebase an authored
    mesh silently when the generating recipe changes.
    """
    if path is None:
        return None
    path = path.resolve(strict=True)
    data = json.loads(path.read_text(encoding="utf-8"))
    fields = {"schema", "schema_version", "base_manifest_sha256", "blend", "assets", "provenance"}
    if set(data) != fields or data["schema"] != "forge.blender-authored-source" or type(data["schema_version"]) is not int or data["schema_version"] != 1:
        raise RuntimeError("invalid authored-source schema or fields")
    if data["base_manifest_sha256"] != sha256(manifest_path):
        raise RuntimeError("authored-source conflict: generating manifest changed; explicit rebase required")
    if data["provenance"] != manifest["provenance"]:
        raise RuntimeError("authored-source provenance must match the original kit")
    record = data["blend"]
    if not isinstance(record, dict) or set(record) != {"path", "sha256"}:
        raise RuntimeError("invalid authored blend record")
    relative = Path(record["path"])
    if relative.is_absolute() or relative.drive or ".." in relative.parts:
        raise RuntimeError("authored blend path must remain relative to its source directory")
    blend_path = (path.parent / relative).resolve(strict=True)
    if not blend_path.is_relative_to(path.parent) or blend_path.suffix.lower() != ".blend":
        raise RuntimeError("authored blend resolves outside its source directory or is not a blend")
    if sha256(blend_path) != record["sha256"]:
        raise RuntimeError("authored blend SHA-256 mismatch")
    assets = data["assets"]
    known = {asset["id"] for asset in manifest["assets"]}
    if not isinstance(assets, dict) or not assets or not set(assets).issubset(known):
        raise RuntimeError("authored source must select known asset IDs")
    names: set[str] = set()
    for asset_id, objects in assets.items():
        if not isinstance(objects, list) or not objects:
            raise RuntimeError(f"authored source has no objects for {asset_id}")
        for name in objects:
            if not isinstance(name, str) or not name or name in names:
                raise RuntimeError("authored source has invalid or duplicate object names")
            names.add(name)
    return {"document": data, "blend_path": blend_path, "descriptor_sha256": sha256(path)}


def import_authored_assets(source: dict | None) -> dict:
    if source is None:
        return {}
    selected = source["document"]["assets"]
    names = [name for objects in selected.values() for name in objects]
    # Library append reads datablocks only; it does not open/execute the source scene.
    with bpy.data.libraries.load(str(source["blend_path"]), link=False) as (available, requested):
        if not set(names).issubset(available.objects):
            raise RuntimeError("authored source references missing objects")
        requested.objects = list(names)
    by_name = {obj.name: obj for obj in requested.objects if obj is not None}
    if set(by_name) != set(names):
        raise RuntimeError(f"authored object names changed during import: expected={names}, actual={sorted(by_name)}")
    result = {}
    for asset_id, object_names in selected.items():
        built = {"lods": {0: [], 1: [], 2: []}, "collisions": []}
        for name in object_names:
            obj = by_name[name]
            if obj.type != "MESH" or obj.parent or obj.constraints or obj.modifiers or obj.animation_data or obj.data.shape_keys:
                raise RuntimeError(f"authored object must be a baked standalone mesh: {name}")
            if obj.get("forge_asset_id") != asset_id:
                raise RuntimeError(f"authored object asset identity mismatch: {name}")
            if obj.get("forge_role") == "collision" and name.startswith("UCX_"):
                built["collisions"].append(obj)
            elif obj.get("forge_role") == "render" and obj.get("forge_lod") in (0, 1, 2):
                built["lods"][obj["forge_lod"]].append(obj)
            else:
                raise RuntimeError(f"authored object role or LOD mismatch: {name}")
            for material in obj.data.materials:
                if material is None or not material.use_nodes or material.animation_data or material.node_tree.animation_data:
                    raise RuntimeError(f"authored mesh requires static node materials: {name}")
                for node in material.node_tree.nodes:
                    if node.type == "TEX_IMAGE" and (node.image is None or not node.image.packed_file):
                        raise RuntimeError(f"authored image must be packed in source blend: {name}")
        if any(not objects for objects in built["lods"].values()) or not built["collisions"]:
            raise RuntimeError(f"authored asset requires three LODs and collisions: {asset_id}")
        result[asset_id] = built
    return result


def main() -> None:
    args = parse_arguments()
    manifest_path = args.manifest.resolve()
    output_directory = args.output_dir.resolve()
    manifest = load_manifest(manifest_path)
    require_running_blender_major_minor(
        blender_major_minor(manifest["generator"]["blender_version"], field="generator.blender_version"),
        context=f"asset-kit manifest {manifest_path.name}",
    )
    authored_source = load_authored_source(args.authored_source, manifest_path, manifest)
    require_new_output_directory(output_directory)
    assets_directory = output_directory / "assets"
    turntables_directory = output_directory / "turntables"
    textures_directory = output_directory / "textures"
    assets_directory.mkdir()
    turntables_directory.mkdir()
    textures_directory.mkdir()
    reset_scene()
    authored_assets = import_authored_assets(authored_source)
    texture_policy = manifest["validation"]["texture_policy"]
    materials, texture_records = create_materials(
        output_directory,
        int(texture_policy["resolution"]),
        int(manifest["generator"]["seed"]),
        texture_policy["foliage_alpha_mode"],
    )
    root_collection = new_collection("R1063_Assets")
    all_built: dict[str, dict] = {}
    validation: dict[str, dict] = {}
    for asset in manifest["assets"]:
        asset_collection = new_collection(f"Asset_{asset['id']}", root_collection)
        if asset["id"] in authored_assets:
            built = authored_assets[asset["id"]]
            for obj in [obj for objects in built["lods"].values() for obj in objects] + built["collisions"]:
                move_to_collection(obj, asset_collection)
        else:
            built = build_asset(asset, asset_collection, materials)
        all_built[asset["id"]] = built
        validation[asset["id"]] = validate_asset(asset, built)
    render_width, render_height = (int(value) for value in manifest["validation"]["render_resolution"])
    presentation = configure_presentation(materials, render_width, render_height)
    turntable_angles = [int(value) for value in manifest["validation"]["turntable_angles_degrees"]]
    asset_reports: list[dict] = []
    for asset in manifest["assets"]:
        asset_id = asset["id"]
        glb_path = assets_directory / f"{asset_id}.glb"
        export_asset_glb(asset_id, all_built[asset_id], glb_path)
        asset_turntable_dir = turntables_directory / asset_id
        asset_turntable_dir.mkdir()
        render_paths = render_turntable(
            asset,
            all_built[asset_id],
            all_built,
            presentation["camera"],
            turntable_angles,
            asset_turntable_dir,
        )
        asset_reports.append(
            {
                "id": asset_id,
                "kind": asset["kind"],
                "generator_profile": asset["generator_profile"],
                "seed": asset["seed"],
                "dimensions_m": asset["dimensions_m"],
                "validation": validation[asset_id],
                "glb": {
                    "path": glb_path.relative_to(output_directory).as_posix(),
                    "sha256": sha256(glb_path),
                    "bytes": glb_path.stat().st_size,
                },
                "turntable": [
                    {
                        "angle_degrees": angle,
                        "path": path.relative_to(output_directory).as_posix(),
                        "sha256": sha256(path),
                        "bytes": path.stat().st_size,
                    }
                    for angle, path in zip(turntable_angles, render_paths, strict=True)
                ],
            }
        )
    lineup_path = render_lineup(
        manifest["assets"],
        all_built,
        presentation,
        materials["scale"],
        output_directory / "asset-kit-lineup.png",
    )
    set_asset_render_visibility(all_built, None)
    blend_path = output_directory / "visual-valley-r1-063-asset-kit.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)
    report = {
        "schema": "forge.blender-asset-kit-build",
        "schema_version": 1,
        "asset_kit_id": manifest["id"],
        "asset_kit_revision": manifest["revision"],
        "generator": {
            "blender_version": bpy.app.version_string,
            "script": Path(__file__).name,
            "script_sha256": sha256(Path(__file__)),
            "input_manifest": manifest_path.name,
            "input_manifest_sha256": sha256(manifest_path),
            "seed": manifest["generator"]["seed"],
        },
        "provenance": manifest["provenance"],
        "render_resolution": [render_width, render_height],
        "texture_policy": texture_policy,
        "textures": texture_records,
        "assets": asset_reports,
        "lineup": {
            "path": lineup_path.relative_to(output_directory).as_posix(),
            "sha256": sha256(lineup_path),
            "bytes": lineup_path.stat().st_size,
            "human_scale_marker_m": 1.8,
        },
        "blend": {
            "path": blend_path.relative_to(output_directory).as_posix(),
            "sha256": sha256(blend_path),
            "bytes": blend_path.stat().st_size,
            "deterministic_hash_required": False,
        },
        "qualification": {
            "blender_asset_candidate": True,
            "technical_export_validated": True,
            "pbr_texture_maps_recorded": True,
            "foliage_alpha_authored": True,
            "runtime_bevy_integrated": False,
            "runtime_performance_qualified": False,
            "human_art_review": "pending",
            "publication_approved": False,
            "r1_063_complete": False,
        },
        "limitations": [
            "Procedural clean topology and controlled LOD generation are provided; a corrective human sculpt/retopology pass remains required before publication.",
            "Deterministic procedural base-color, roughness and tangent-normal PNG maps are packed into the Blend and embedded in GLB; they remain a generated baseline, not scanned or hand-authored AAA surfaces.",
            "Foliage uses generated alpha silhouettes and veins; a human botanical pass and production card clustering remain required before publication.",
            "Collision meshes are authored and named but are not yet consumed or qualified by Bevy/Rapier.",
            "The kit is not integrated into Visual Valley and does not qualify R1-064, R1-065 or Gate 1.",
        ],
    }
    report_path = output_directory / "asset-kit-build.v1.json"
    if authored_source is not None:
        report["authored_source"] = {
            "descriptor_sha256": authored_source["descriptor_sha256"],
            "blend_sha256": authored_source["document"]["blend"]["sha256"],
            "base_manifest_sha256": authored_source["document"]["base_manifest_sha256"],
            "assets": sorted(authored_assets),
            "human_review": "pending",
        }
        report["provenance"] = {
            **manifest["provenance"],
            "source_kind": "procedural_original_with_authored_edits",
        }
        report["limitations"].append("Authored meshes are preserved from a hashed source blend; Studio authoring and runtime acceptance of this provenance remain unqualified.")
    report_path.write_text(json.dumps(report, indent=2, sort_keys=True, ensure_ascii=False) + "\n", encoding="utf-8")
    print(
        "OPENRPG_FORGE_R1_063_ASSET_KIT_BUILD_PASS "
        f"assets={len(asset_reports)} report={report_path} sha256={sha256(report_path)}"
    )


if __name__ == "__main__":
    main()
