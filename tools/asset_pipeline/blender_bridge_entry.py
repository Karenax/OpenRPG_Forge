"""Bundled Blender entry point. Invoked only by blender_worker.py."""

from __future__ import annotations

import base64
import bmesh
import json
import math
import pathlib
import sys

import bpy
from mathutils import Vector


def _decode_payload(value: str):
    return json.loads(base64.urlsafe_b64decode(value.encode("ascii")).decode("utf-8"))


def _arguments() -> tuple[pathlib.Path, pathlib.Path, pathlib.Path, pathlib.Path, list[str], dict]:
    separator = sys.argv.index("--")
    values = sys.argv[separator + 1 :]
    if len(values) != 6:
        raise ValueError("Expected source, output, preview, report, operations and options.")
    operations = _decode_payload(values[4])
    options = _decode_payload(values[5])
    if not isinstance(operations, list) or not isinstance(options, dict):
        raise ValueError("Expected typed operations and options payloads.")
    return (
        pathlib.Path(values[0]),
        pathlib.Path(values[1]),
        pathlib.Path(values[2]),
        pathlib.Path(values[3]),
        operations,
        options,
    )


def _reset() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def _import_model(path: pathlib.Path) -> None:
    extension = path.suffix.lower()
    if extension == ".blend":
        bpy.ops.wm.open_mainfile(filepath=str(path))
    elif extension == ".obj":
        bpy.ops.wm.obj_import(filepath=str(path))
    elif extension == ".fbx":
        bpy.ops.import_scene.fbx(filepath=str(path))
    elif extension == ".dae":
        bpy.ops.wm.collada_import(filepath=str(path))
    elif extension in {".glb", ".gltf"}:
        bpy.ops.import_scene.gltf(filepath=str(path))
    else:
        raise ValueError(f"Unsupported model format: {extension}")


def _create_primitives(recipe: dict) -> None:
    material_definition = recipe["material"]
    material = bpy.data.materials.new(material_definition["name"])
    material.use_nodes = True
    principled = next(node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
    color = material_definition["baseColor"]
    principled.inputs["Base Color"].default_value = (
        color["red"], color["green"], color["blue"], color["alpha"])
    principled.inputs["Metallic"].default_value = material_definition["metallic"]
    principled.inputs["Roughness"].default_value = material_definition["roughness"]
    projections = {
        "generated": lambda: bpy.ops.uv.smart_project(angle_limit=math.radians(66)),
        "cube": bpy.ops.uv.cube_project,
        "cylinder": bpy.ops.uv.cylinder_project,
        "sphere": bpy.ops.uv.sphere_project,
    }
    creators = {
        "cube": lambda: bpy.ops.mesh.primitive_cube_add(size=1),
        "cylinder": lambda: bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=0.5, depth=1),
        "sphere": lambda: bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, radius=0.5),
        "cone": lambda: bpy.ops.mesh.primitive_cone_add(vertices=32, radius1=0.5, radius2=0, depth=1),
    }
    for primitive in recipe["primitives"]:
        creators[primitive["kind"]]()
        item = bpy.context.object
        item.name = primitive["name"]
        position = primitive["positionMeters"]
        rotation = primitive["rotationDegrees"]
        scale = primitive["scaleMeters"]
        item.location = (position["x"], position["y"], position["z"])
        item.rotation_euler = tuple(math.radians(rotation[axis]) for axis in ("x", "y", "z"))
        item.scale = (scale["x"], scale["y"], scale["z"])
        item.data.materials.append(material)
        bpy.context.view_layer.objects.active = item
        item.select_set(True)
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        projections[recipe["uvProjection"]]()
        bpy.ops.object.mode_set(mode="OBJECT")
        item.select_set(False)


def _apply_material_textures(sources: list[dict]) -> None:
    for source in sources:
        if source.get("channel") != "base_color":
            raise ValueError(f"Unsupported material texture channel: {source.get('channel')}")
        material = bpy.data.materials.get(str(source.get("material", "")))
        if material is None:
            raise ValueError(f"Material texture target does not exist: {source.get('material')}")
        path = pathlib.Path(str(source.get("path", "")))
        if not path.is_file():
            raise ValueError(f"Material texture source does not exist: {path}")
        material.use_nodes = True
        tree = material.node_tree
        principled = next((node for node in tree.nodes if node.type == "BSDF_PRINCIPLED"), None)
        if principled is None:
            raise ValueError(f"Material has no Principled BSDF node: {material.name}")
        image = bpy.data.images.load(str(path), check_existing=False)
        image.colorspace_settings.name = "sRGB"
        texture = tree.nodes.new("ShaderNodeTexImage")
        texture.name = f"OpenRPG Forge {material.name} Base Color"
        texture.label = "OpenRPG Forge governed base color"
        texture.image = image
        tree.links.new(texture.outputs["Color"], principled.inputs["Base Color"])
        if "Alpha" in texture.outputs and "Alpha" in principled.inputs:
            tree.links.new(texture.outputs["Alpha"], principled.inputs["Alpha"])


def _skinned_meshes(armature) -> list:
    return [
        item for item in bpy.context.scene.objects
        if item.type == "MESH" and (
            item.parent == armature
            or any(modifier.type == "ARMATURE" and modifier.object == armature for modifier in item.modifiers)
        )
    ]


def _rig_height(armature) -> float:
    points = [coordinate for bone in armature.data.bones for coordinate in (bone.head_local, bone.tail_local)]
    if not points:
        return 1.0
    return max(0.0001, max(point.z for point in points) - min(point.z for point in points))


def _raw_mesh_diagonal(meshes: list) -> float:
    points = [item.matrix_world @ vertex.co for item in meshes for vertex in item.data.vertices]
    if not points:
        return 0.0
    minimum = Vector((min(point.x for point in points), min(point.y for point in points), min(point.z for point in points)))
    maximum = Vector((max(point.x for point in points), max(point.y for point in points), max(point.z for point in points)))
    return max(0.000001, (maximum - minimum).length)


def _reset_pose(armature) -> None:
    for bone in armature.pose.bones:
        bone.location = (0.0, 0.0, 0.0)
        bone.rotation_mode = "QUATERNION"
        bone.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        bone.scale = (1.0, 1.0, 1.0)


def _retarget_action(target, source_armature, source_action, clip_name: str, profile: dict):
    target_bones = {bone.name for bone in target.data.bones}
    source_bones = {bone.name for bone in source_armature.data.bones}
    if len(target_bones) != int(profile["targetBoneCount"]):
        raise ValueError(
            f"Retarget profile '{profile['profileId']}' expected {profile['targetBoneCount']} target bones, "
            f"found {len(target_bones)}.")
    if len(source_bones) != int(profile["sourceBoneCount"]):
        raise ValueError(
            f"Retarget profile '{profile['profileId']}' expected {profile['sourceBoneCount']} source bones, "
            f"found {len(source_bones)}.")
    mapping = profile["bones"]
    missing_targets = sorted(set(mapping) - target_bones)
    missing_sources = sorted(set(mapping.values()) - source_bones)
    if missing_targets or missing_sources:
        raise ValueError(
            f"Retarget profile '{profile['profileId']}' does not match imported rigs; "
            f"missing targets={missing_targets[:3]} sources={missing_sources[:3]}.")

    frame_start = int(math.floor(source_action.frame_range[0]))
    frame_end = int(math.ceil(source_action.frame_range[1]))
    if frame_end < frame_start:
        raise ValueError(f"Animation '{clip_name}' has an invalid frame range.")
    source_action.use_fake_user = True
    source_armature.animation_data_clear()
    source_armature.animation_data_create()
    source_armature.animation_data.action = source_action
    if len(source_action.slots) == 1:
        source_armature.animation_data.action_slot = source_action.slots[0]
    target.animation_data_create()
    action = bpy.data.actions.new(clip_name)
    action.use_fake_user = True
    target.animation_data.action = action
    translation_scale = _rig_height(target) / _rig_height(source_armature)
    root_motion_target = profile["rootMotionTarget"]

    for frame in range(frame_start, frame_end + 1):
        bpy.context.scene.frame_set(frame)
        _reset_pose(target)
        bpy.context.view_layer.update()
        for target_name, source_name in mapping.items():
            source_pose = source_armature.pose.bones[source_name]
            target_pose = target.pose.bones[target_name]
            source_basis = source_pose.matrix_basis.copy()
            target_pose.rotation_mode = "QUATERNION"
            target_pose.rotation_quaternion = source_basis.to_quaternion().normalized()
            if target_name == root_motion_target:
                target_pose.location = source_basis.to_translation() * translation_scale
                target_pose.keyframe_insert(data_path="location", frame=frame, group=target_name)
            target_pose.keyframe_insert(data_path="rotation_quaternion", frame=frame, group=target_name)
        bpy.context.view_layer.update()

    source_armature.animation_data.action = None
    target.animation_data.action = None
    return action, frame_start, frame_end, translation_scale


def _audit_retarget_deformation(
    target,
    clip_name: str,
    frame_start: int,
    frame_end: int,
    reference_diagonal: float,
    profile: dict,
    translation_scale: float,
) -> dict:
    meshes = _skinned_meshes(target)
    sample_count = min(9, max(1, frame_end - frame_start + 1))
    if sample_count == 1:
        frames = [frame_start]
    else:
        frames = sorted({
            round(frame_start + index * (frame_end - frame_start) / (sample_count - 1))
            for index in range(sample_count)
        })
    invalid_coordinates = 0
    sampled_vertices = 0
    maximum_expansion = 0.0
    minimum_expansion = float("inf")
    maximum_displacement = 0.0
    maximum_temporal_displacement = 0.0
    first_sample_points = None
    rest_positions = [item.matrix_world @ vertex.co for item in meshes for vertex in item.data.vertices]
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for frame in frames:
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        points = []
        for item in meshes:
            evaluated = item.evaluated_get(depsgraph)
            evaluated_mesh = evaluated.to_mesh()
            try:
                points.extend(evaluated.matrix_world @ vertex.co for vertex in evaluated_mesh.vertices)
            finally:
                evaluated.to_mesh_clear()
        sampled_vertices += len(points)
        invalid_coordinates += sum(
            1 for point in points if any(not math.isfinite(component) for component in point)
        )
        finite_points = [
            point for point in points if all(math.isfinite(component) for component in point)
        ]
        if finite_points and reference_diagonal > 0:
            minimum = Vector((
                min(point.x for point in finite_points),
                min(point.y for point in finite_points),
                min(point.z for point in finite_points),
            ))
            maximum = Vector((
                max(point.x for point in finite_points),
                max(point.y for point in finite_points),
                max(point.z for point in finite_points),
            ))
            ratio = (maximum - minimum).length / reference_diagonal
            maximum_expansion = max(maximum_expansion, ratio)
            minimum_expansion = min(minimum_expansion, ratio)
            if len(rest_positions) == len(points):
                maximum_displacement = max(
                    maximum_displacement,
                    max((point - rest).length / reference_diagonal for point, rest in zip(points, rest_positions)),
                )
            if first_sample_points is None:
                first_sample_points = [point.copy() for point in points]
            elif len(first_sample_points) == len(points):
                maximum_temporal_displacement = max(
                    maximum_temporal_displacement,
                    max((point - first).length / reference_diagonal for point, first in zip(points, first_sample_points)),
                )
    if minimum_expansion == float("inf"):
        minimum_expansion = 0.0
    target_bone_count = len(target.data.bones)
    mapped_ratio = len(profile["bones"]) / max(1, target_bone_count)
    return {
        "profileId": profile["profileId"],
        "clip": clip_name,
        "sourceRig": profile["sourceRig"],
        "targetRig": profile["targetRig"],
        "sourceBoneCount": int(profile["sourceBoneCount"]),
        "targetBoneCount": target_bone_count,
        "mappedBoneCount": len(profile["bones"]),
        "mappedTargetRatio": round(mapped_ratio, 6),
        "frameStart": frame_start,
        "frameEnd": frame_end,
        "sampledFrames": frames,
        "sampledVertexCount": sampled_vertices,
        "invalidCoordinateCount": invalid_coordinates,
        "rootMotionScale": round(translation_scale, 6),
        "minimumBoundsRatio": round(minimum_expansion, 6),
        "maximumBoundsExpansion": round(maximum_expansion, 6),
        "maximumNormalizedVertexDisplacement": round(maximum_displacement, 6),
        "maximumNormalizedTemporalDisplacement": round(maximum_temporal_displacement, 6),
    }


def _merge_animation_sources(sources: list[dict], retarget_profile: dict | None = None) -> tuple[list[str], list[dict]]:
    if not sources:
        return [], []
    targets = [item for item in bpy.context.scene.objects if item.type == "ARMATURE"]
    if len(targets) != 1:
        raise ValueError("Animated model must contain exactly one target armature.")
    target = targets[0]
    target_bones = {bone.name for bone in target.data.bones}
    if not target_bones:
        raise ValueError("Animated model target armature has no bones.")

    target_meshes = _skinned_meshes(target)
    reference_diagonal = _raw_mesh_diagonal(target_meshes)
    selected_actions = []
    retarget_jobs = []
    target.animation_data_clear()
    _reset_pose(target)
    for source in sources:
        clip_name = str(source.get("name", "")).strip().lower()
        path = pathlib.Path(str(source.get("path", "")))
        before_objects = set(bpy.data.objects)
        before_actions = set(bpy.data.actions)
        bpy.ops.import_scene.fbx(filepath=str(path), use_anim=True)
        imported_objects = [item for item in bpy.data.objects if item not in before_objects]
        imported_armatures = [item for item in imported_objects if item.type == "ARMATURE"]
        if len(imported_armatures) != 1:
            raise ValueError(f"Animation '{clip_name}' must contain exactly one armature.")
        source_bones = {bone.name for bone in imported_armatures[0].data.bones}
        if source_bones != target_bones and retarget_profile is None:
            missing = sorted(target_bones - source_bones)[:3]
            extra = sorted(source_bones - target_bones)[:3]
            raise ValueError(f"Animation '{clip_name}' rig differs from target; missing={missing} extra={extra}.")
        imported_actions = [action for action in bpy.data.actions if action not in before_actions]
        candidates = [
            action for action in imported_actions
            if _blender_base_name(action.name).lower() == clip_name
            or _blender_base_name(action.name).lower().endswith(f"|{clip_name}")
        ]
        if len(candidates) != 1:
            raise ValueError(
                f"Animation '{clip_name}' did not expose one matching action: {[action.name for action in imported_actions]}")
        source_action = candidates[0]
        if source_bones != target_bones:
            action, frame_start, frame_end, translation_scale = _retarget_action(
                target,
                imported_armatures[0],
                source_action,
                clip_name,
                retarget_profile,
            )
            retarget_jobs.append((clip_name, frame_start, frame_end, translation_scale))
        else:
            action = source_action
            action.name = clip_name
            action.use_fake_user = True
        selected_actions.append(action)
        for item in imported_objects:
            bpy.data.objects.remove(item, do_unlink=True)
        for imported_action in imported_actions:
            if imported_action != action and imported_action.users == 0:
                bpy.data.actions.remove(imported_action)

    target.animation_data_clear()
    target.animation_data_create()
    for action in selected_actions:
        track = target.animation_data.nla_tracks.new()
        track.name = action.name
        strip = track.strips.new(action.name, int(action.frame_range[0]), action)
        strip.extrapolation = "NOTHING"
    retarget_audits = [
        _audit_retarget_deformation(
            target,
            clip_name,
            frame_start,
            frame_end,
            reference_diagonal,
            retarget_profile,
            translation_scale,
        )
        for clip_name, frame_start, frame_end, translation_scale in retarget_jobs
    ]
    return [action.name for action in selected_actions], retarget_audits


def _blender_base_name(value: str) -> str:
    """Ignore Blender's collision suffix when the same multi-take FBX is imported repeatedly."""
    stem, separator, suffix = value.rpartition(".")
    return stem if separator and len(suffix) == 3 and suffix.isdigit() else value


def _materialize_embedded_actions_as_nla() -> list[str]:
    """Preserve multi-take FBX actions as named GLB animation clips.

    Blender's FBX importer exposes every take as an Action but assigns only the first one
    to the armature. The glTF exporter therefore loses the remaining takes unless they are
    materialized as NLA tracks. Existing GLB NLA tracks remain authoritative.
    """
    armatures = [item for item in bpy.context.scene.objects if item.type == "ARMATURE"]
    existing = sorted({
        track.name
        for armature in armatures
        if armature.animation_data is not None
        for track in armature.animation_data.nla_tracks
    })
    if existing:
        return existing

    assignments = []
    used_names = set()
    for action in sorted(bpy.data.actions, key=lambda item: item.name):
        candidates = [
            armature for armature in armatures
            if action.name.lower().startswith(f"{armature.name.lower()}|")
        ]
        if not candidates and len(armatures) == 1:
            candidates = armatures
        if len(candidates) != 1:
            continue
        clip_name = action.name.rsplit("|", 1)[-1].strip()
        if not clip_name or clip_name in used_names:
            continue
        used_names.add(clip_name)
        assignments.append((candidates[0], action, clip_name))

    for armature in armatures:
        armature.animation_data_create()
        armature.animation_data.action = None
    for armature, action, clip_name in assignments:
        action.name = clip_name
        action.use_fake_user = True
        track = armature.animation_data.nla_tracks.new()
        track.name = clip_name
        strip = track.strips.new(clip_name, int(action.frame_range[0]), action)
        strip.extrapolation = "NOTHING"
    return sorted(used_names)


def _bind_unanimated_auxiliary_eye_armatures() -> None:
    armatures = [item for item in bpy.context.scene.objects if item.type == "ARMATURE"]
    animated = [
        item for item in armatures
        if item.animation_data is not None and len(item.animation_data.nla_tracks) > 0
    ]
    if len(animated) != 1:
        return
    primary = animated[0]
    attachment_bone = next(
        (name for name in ("Face", "Head") if primary.data.bones.get(name) is not None),
        None,
    )
    if attachment_bone is None:
        return
    for auxiliary in armatures:
        if auxiliary == primary or "eye" not in auxiliary.name.lower():
            continue
        if auxiliary.animation_data is not None and len(auxiliary.animation_data.nla_tracks) > 0:
            continue
        # Some legacy FBX creature packs export the eye rig as a separate static armature.
        # Attach it to the semantic facial bone while preserving its authored rest transform so
        # eyes follow the animated head instead of remaining behind in world space.
        world_transform = auxiliary.matrix_world.copy()
        auxiliary.parent = primary
        auxiliary.parent_type = "BONE"
        auxiliary.parent_bone = attachment_bone
        auxiliary.matrix_world = world_transform
    bpy.context.view_layer.update()


def _normalize_legacy_fbx_material_alpha() -> None:
    for material in bpy.data.materials:
        if not material.use_nodes or material.node_tree is None:
            if material.diffuse_color[3] <= 0.001:
                material.diffuse_color = (*material.diffuse_color[:3], 1.0)
            continue
        principled = next((node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"), None)
        if principled is None:
            continue
        alpha = principled.inputs.get("Alpha")
        if alpha is not None and not alpha.is_linked and float(alpha.default_value) <= 0.001:
            alpha.default_value = 1.0
            material.diffuse_color = (*material.diffuse_color[:3], 1.0)


def _normalize(options: dict, source: pathlib.Path) -> None:
    # Blender imports several legacy FBX packs with an unlinked zero Alpha value even though
    # their materials are authored as opaque. Preserve linked transparency while making those
    # otherwise fully invisible materials render consistently in glTF/Godot.
    if source.suffix.lower() == ".fbx":
        _normalize_legacy_fbx_material_alpha()
    scale = float(options["scaleMeters"])
    animated_scene = any(
        item.type == "ARMATURE"
        and item.animation_data is not None
        and (item.animation_data.action is not None or len(item.animation_data.nla_tracks) > 0)
        for item in bpy.context.scene.objects
    )
    normalization_root = None
    if animated_scene:
        # Apply one stable parent transform to the whole rig hierarchy. Scaling armatures and
        # their child meshes independently double-scales skins, and keyed FBX object transforms
        # can overwrite normalization on the armature itself during animation evaluation.
        roots = [candidate for candidate in bpy.context.scene.objects if candidate.parent is None]
        normalization_root = bpy.data.objects.new("ForgeNormalizationRoot", None)
        bpy.context.collection.objects.link(normalization_root)
        for item in roots:
            world_transform = item.matrix_world.copy()
            item.parent = normalization_root
            item.matrix_world = world_transform
        normalization_root.scale = Vector((scale, scale, scale))
        bpy.context.view_layer.update()
    else:
        for item in bpy.context.scene.objects:
            if item.type not in {"MESH", "ARMATURE", "EMPTY"}:
                continue
            item.scale = tuple(component * scale for component in item.scale)
            item.select_set(True)
        if options["applyTransforms"] and bpy.context.selected_objects:
            bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if options["pivot"] in {"ground_center", "object_center"}:
        meshes = [item for item in bpy.context.scene.objects if item.type == "MESH"]
        if meshes:
            minimum, maximum = _bounds(meshes)
            center = (minimum + maximum) * 0.5
            offset = -center if options["pivot"] == "object_center" else Vector((-center.x, -center.y, -minimum.z))
            if normalization_root is not None:
                normalization_root.location += offset
            else:
                for item in [candidate for candidate in bpy.context.scene.objects if candidate.parent is None]:
                    item.location += offset
            bpy.context.view_layer.update()


def _select_meshes(included_names: list[str]) -> None:
    if not included_names:
        return
    included = set(included_names)
    meshes = [item for item in bpy.context.scene.objects if item.type == "MESH"]
    available = {item.name for item in meshes}
    missing = sorted(included - available)
    if missing:
        raise ValueError(f"Requested mesh is absent from the imported model: {missing[0]}")
    for item in meshes:
        if item.name not in included:
            bpy.data.objects.remove(item, do_unlink=True)
    if not any(item.type == "MESH" for item in bpy.context.scene.objects):
        raise ValueError("Mesh selection removed every renderable object.")


def _merge_supplemental_mesh_sources(sources: list[dict]) -> None:
    """Attach governed skinned mesh fragments to the source model's existing rig.

    Modular character packs commonly distribute the body, face, hair and outfit as separate
    glTF files that share bone names. The worker keeps one authoritative armature, imports only
    explicitly allow-listed meshes, trims them at a declared model-space height and rebinds their
    armature modifiers. This avoids exporting a second static rig or an unintended full base body.
    """
    if not sources:
        return
    targets = [item for item in bpy.context.scene.objects if item.type == "ARMATURE"]
    if len(targets) != 1:
        raise ValueError("Supplemental skinned meshes require exactly one target armature.")
    target = targets[0]
    target_bones = {bone.name for bone in target.data.bones}
    if not target_bones:
        raise ValueError("Supplemental skinned meshes require a target armature with bones.")

    for source in sources:
        path = pathlib.Path(str(source.get("path", "")))
        included_names = set(source.get("includedMeshNames", []))
        minimum_vertex_z = float(source.get("minimumVertexZ", -1000.0))
        if not path.is_file():
            raise ValueError(f"Supplemental mesh source does not exist: {path}")
        before_objects = set(bpy.data.objects)
        _import_model(path)
        imported = [item for item in bpy.data.objects if item not in before_objects]
        selected = [
            item for item in imported
            if item.type == "MESH" and _blender_base_name(item.name) in included_names
        ]
        available = {
            _blender_base_name(item.name) for item in imported if item.type == "MESH"
        }
        missing = sorted(included_names - available)
        if missing:
            raise ValueError(f"Supplemental mesh is absent from the imported model: {missing[0]}")

        for item in selected:
            mesh = bmesh.new()
            try:
                mesh.from_mesh(item.data)
                discarded = [vertex for vertex in mesh.verts if vertex.co.z < minimum_vertex_z]
                if discarded:
                    bmesh.ops.delete(mesh, geom=discarded, context="VERTS")
                if not mesh.verts:
                    raise ValueError(
                        f"Supplemental mesh height filter removed every vertex from '{item.name}'.")
                mesh.to_mesh(item.data)
                item.data.update()
            finally:
                mesh.free()

            weighted_bones = {
                group.name for group in item.vertex_groups if group.name in target_bones
            }
            if not weighted_bones:
                raise ValueError(
                    f"Supplemental mesh '{item.name}' has no vertex groups compatible with the target rig.")
            armature_modifiers = [modifier for modifier in item.modifiers if modifier.type == "ARMATURE"]
            if not armature_modifiers:
                modifier = item.modifiers.new(name="OpenRPG Forge Supplemental Rig", type="ARMATURE")
                armature_modifiers = [modifier]
            for modifier in armature_modifiers:
                modifier.object = target
            world_transform = item.matrix_world.copy()
            item.parent = target
            item.matrix_world = world_transform

        for item in imported:
            if item not in selected:
                bpy.data.objects.remove(item, do_unlink=True)
        bpy.context.view_layer.update()


def _is_generated_lod(item) -> bool:
    return bool(item.get("openrpg_forge_generated_lod", False))


def _decimate_base_meshes(ratio: float) -> None:
    if ratio >= 0.999999:
        return
    sources = [
        item for item in bpy.context.scene.objects
        if item.type == "MESH" and not _is_generated_lod(item) and not item.name.endswith("-col")
    ]
    for source in sources:
        modifier = source.modifiers.new(name="OpenRPG Forge Base Decimation", type="DECIMATE")
        modifier.ratio = ratio
        bpy.context.view_layer.objects.active = source
        source.select_set(True)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        source.select_set(False)


def _bounds(meshes: list) -> tuple[Vector, Vector]:
    coordinates = [item.matrix_world @ Vector(corner) for item in meshes for corner in item.bound_box]
    if not coordinates:
        raise ValueError("Imported model has no renderable bounds.")
    return (
        Vector(tuple(min(point[index] for point in coordinates) for index in range(3))),
        Vector(tuple(max(point[index] for point in coordinates) for index in range(3))),
    )


def _generate_lods(ratios: list[float]) -> None:
    sources = [item for item in bpy.context.scene.objects if item.type == "MESH" and not _is_generated_lod(item)]
    for source in sources:
        for level, ratio in enumerate(ratios, start=1):
            duplicate = source.copy()
            duplicate.data = source.data.copy()
            duplicate.name = f"{source.name}_LOD{level}"
            duplicate["openrpg_forge_generated_lod"] = True
            bpy.context.collection.objects.link(duplicate)
            modifier = duplicate.modifiers.new(name="OpenRPG Forge LOD", type="DECIMATE")
            modifier.ratio = ratio
            bpy.context.view_layer.objects.active = duplicate
            duplicate.select_set(True)
            bpy.ops.object.modifier_apply(modifier=modifier.name)
            duplicate.select_set(False)


def _generate_collision() -> None:
    for source in [item for item in bpy.context.scene.objects if item.type == "MESH" and not _is_generated_lod(item)]:
        collision = source.copy()
        collision.data = source.data.copy()
        collision.name = f"{source.name}-col"
        bpy.context.collection.objects.link(collision)


def _look_at(item, target: Vector) -> None:
    item.rotation_euler = (target - item.location).to_track_quat("-Z", "Y").to_euler()


def _render_thumbnail(path: pathlib.Path) -> None:
    meshes = [
        item for item in bpy.context.scene.objects
        if item.type == "MESH" and not _is_generated_lod(item) and not item.name.endswith("-col")
    ]
    minimum, maximum = _bounds(meshes)
    center = (minimum + maximum) * 0.5
    radius = max((maximum - minimum).length * 0.5, 0.1)
    direction = Vector((1.6, -1.8, 1.15)).normalized()
    camera_distance = max(radius / math.tan(math.radians(50) * 0.5) * 0.92, radius * 2.1)
    hidden = {}
    temporary_objects = []
    try:
        for item in bpy.context.scene.objects:
            if item.type == "MESH" and (_is_generated_lod(item) or item.name.endswith("-col")):
                hidden[item.name] = item.hide_render
                item.hide_render = True

        camera_data = bpy.data.cameras.new("OpenRPG Forge Preview Camera")
        camera = bpy.data.objects.new("OpenRPG Forge Preview Camera", camera_data)
        bpy.context.collection.objects.link(camera)
        camera.location = center + direction * camera_distance
        camera.data.lens = 52
        _look_at(camera, center)
        bpy.context.scene.camera = camera
        temporary_objects.append(camera)

        for name, location, energy_factor, size, color in (
            ("OpenRPG Forge Key", center + Vector((2.2, -2.4, 3.0)) * radius, 1000, 4.0, (1.0, 0.88, 0.78)),
            ("OpenRPG Forge Fill", center + Vector((-2.5, -0.8, 1.6)) * radius, 350, 3.0, (0.68, 0.8, 1.0)),
            ("OpenRPG Forge Rim", center + Vector((0.5, 2.8, 2.2)) * radius, 600, 3.0, (1.0, 0.94, 0.84)),
        ):
            light_data = bpy.data.lights.new(name, type="AREA")
            light_data.energy = energy_factor * radius * radius
            light_data.color = color
            light_data.shape = "DISK"
            light_data.size = max(size * radius, 0.5)
            light = bpy.data.objects.new(name, light_data)
            bpy.context.collection.objects.link(light)
            light.location = location
            _look_at(light, center)
            temporary_objects.append(light)

        scene = bpy.context.scene
        scene.render.engine = "BLENDER_EEVEE_NEXT"
        scene.render.resolution_x = 512
        scene.render.resolution_y = 512
        scene.render.resolution_percentage = 100
        scene.render.image_settings.file_format = "PNG"
        scene.render.image_settings.color_mode = "RGBA"
        scene.render.film_transparent = True
        scene.render.dither_intensity = 0.0
        scene.render.filepath = str(path)
        scene.render.image_settings.color_depth = "8"
        scene.eevee.taa_render_samples = 16
        scene.eevee.use_taa_reprojection = False
        scene.eevee.use_bokeh_jittered = False
        scene.eevee.use_shadow_jitter_viewport = False
        scene.view_settings.look = "AgX - Medium High Contrast"
        scene.view_settings.exposure = -0.65
        scene.world.color = (0.025, 0.02, 0.035)
        path.parent.mkdir(parents=True, exist_ok=True)
        bpy.ops.render.render(write_still=True)
    finally:
        for item in temporary_objects:
            bpy.data.objects.remove(item, do_unlink=True)
        for name, value in hidden.items():
            item = bpy.data.objects.get(name)
            if item is not None:
                item.hide_render = value


def _audit(options: dict, operations: list[str], preview: pathlib.Path, retarget_audits: list[dict]) -> dict:
    base_meshes = [
        item for item in bpy.context.scene.objects
        if item.type == "MESH" and not _is_generated_lod(item) and not item.name.endswith("-col")
    ]
    lod_meshes = [item for item in bpy.context.scene.objects if item.type == "MESH" and _is_generated_lod(item)]
    collision_meshes = [item for item in bpy.context.scene.objects if item.type == "MESH" and item.name.endswith("-col")]
    triangles = 0
    vertices = 0
    invalid_normals = 0
    materials = set()
    for item in base_meshes:
        item.data.calc_loop_triangles()
        triangles += len(item.data.loop_triangles)
        vertices += len(item.data.vertices)
        invalid_normals += sum(
            1 for polygon in item.data.polygons if any(not math.isfinite(component) for component in polygon.normal)
        )
        materials.update(slot.material.name for slot in item.material_slots if slot.material is not None)

    images = [image for image in bpy.data.images if image.source == "FILE" and image.name != "Render Result"]
    missing_textures = []
    absolute_texture_paths = []
    for image in images:
        if image.packed_file is None and image.filepath:
            resolved = pathlib.Path(bpy.path.abspath(image.filepath))
            if not resolved.is_file():
                missing_textures.append(image.name)
            if pathlib.Path(image.filepath).is_absolute():
                absolute_texture_paths.append(image.name)

    minimum = maximum = Vector((0.0, 0.0, 0.0))
    if base_meshes:
        minimum, maximum = _bounds(base_meshes)
    dimensions = maximum - minimum
    center = (minimum + maximum) * 0.5
    pivot_tolerance = max(0.0001, max(dimensions) * 0.01)
    pivot_grounded = abs(minimum.z) <= pivot_tolerance
    pivot_centered_horizontal = abs(center.x) <= pivot_tolerance and abs(center.y) <= pivot_tolerance
    if pivot_grounded and pivot_centered_horizontal:
        pivot_class = "center-base"
    elif pivot_grounded:
        pivot_class = "base-offset"
    elif pivot_centered_horizontal and abs(center.z) <= pivot_tolerance:
        pivot_class = "bounds-center"
    else:
        pivot_class = "offset"
    required_lods = len(base_meshes) * len(options["lodRatios"])
    collision_required = "generate_collision" in operations or options["generateCollision"]
    preview_required = "generate_thumbnail" in operations
    armatures = [item for item in bpy.context.scene.objects if item.type == "ARMATURE"]
    animation_clips = sorted({
        track.name
        for armature in armatures
        if armature.animation_data is not None
        for track in armature.animation_data.nla_tracks
    })
    frames_per_second = max(1.0, bpy.context.scene.render.fps / max(0.0001, bpy.context.scene.render.fps_base))
    animation_durations = {
        track.name: round(
            max(0.0, track.strips[0].action_frame_end - track.strips[0].action_frame_start) / frames_per_second,
            6,
        )
        for armature in armatures
        if armature.animation_data is not None
        for track in armature.animation_data.nla_tracks
        if len(track.strips) > 0
    }
    expected_animation_clips = sorted(source["name"] for source in options.get("animationSources", []))
    checks = []

    def check(code: str, passed: bool, message: str, warning: bool = False) -> None:
        checks.append({
            "code": code,
            "status": "pass" if passed else "warning" if warning else "fail",
            "message": message,
        })

    check("asset.quality.mesh_present", bool(base_meshes), "At least one base mesh is required.")
    check("asset.quality.geometry_present", triangles > 0 and vertices > 0, "Renderable geometry is required.")
    check("asset.quality.normals_finite", invalid_normals == 0, "Mesh normals must contain finite values.")
    check("asset.quality.textures_resolved", not missing_textures, "Every referenced texture must exist.")
    check(
        "asset.quality.texture_paths_portable",
        not absolute_texture_paths,
        "Absolute texture paths are embedded during GLB export but should be normalized at source.",
        warning=True,
    )
    check(
        "asset.quality.material_present",
        bool(materials),
        "The model has no material; a neutral fallback will be used.",
        warning=True,
    )
    material_objects = [bpy.data.materials.get(name) for name in materials]
    visible_materials = 0
    for material in [item for item in material_objects if item is not None]:
        if not material.use_nodes or material.node_tree is None:
            visible_materials += int(material.diffuse_color[3] > 0.001)
            continue
        principled = next((node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"), None)
        alpha = principled.inputs.get("Alpha") if principled is not None else None
        visible_materials += int(alpha is None or alpha.is_linked or float(alpha.default_value) > 0.001)
    check(
        "asset.quality.material_alpha_visible",
        not materials or visible_materials > 0,
        "At least one referenced material must have visible alpha.",
    )
    check(
        "asset.quality.size_coherent",
        all(math.isfinite(value) and 0 <= value <= 1000 for value in dimensions),
        "Model dimensions must be finite and at most 1000 metres per axis.",
    )
    if options["pivot"] == "ground_center":
        check("asset.quality.pivot_grounded", abs(minimum.z) <= 0.01, "Ground pivot must place minimum Z at zero.")
    if collision_required:
        check(
            "asset.quality.collision_present",
            len(collision_meshes) >= len(base_meshes),
            "A generated collision mesh is required for every base mesh.",
        )
    if "generate_lods" in operations and required_lods:
        check(
            "asset.quality.lods_present",
            len(lod_meshes) >= required_lods,
            "Every requested LOD level must be generated.",
        )
    check(
        "asset.quality.triangle_budget",
        triangles <= 500_000,
        "Base mesh exceeds the recommended 500k triangle authoring budget.",
        warning=triangles <= 2_000_000,
    )
    if preview_required:
        check("asset.quality.thumbnail_present", preview.is_file(), "A generated PNG thumbnail is required.")
    if expected_animation_clips:
        check("asset.quality.rig_present", len(armatures) == 1 and len(armatures[0].data.bones) > 0,
              "Animated character must contain one non-empty armature.")
        check("asset.quality.animation_clips_present", animation_clips == expected_animation_clips,
              "Every governed animation clip must be embedded with its stable name.")
    retarget_profile = options.get("retargetProfile")
    if retarget_profile is not None:
        mapped_ratios = [item["mappedTargetRatio"] for item in retarget_audits]
        maximum_expansions = [item["maximumBoundsExpansion"] for item in retarget_audits]
        minimum_ratios = [item["minimumBoundsRatio"] for item in retarget_audits]
        invalid_coordinates = sum(item["invalidCoordinateCount"] for item in retarget_audits)
        temporal_displacements = [item["maximumNormalizedTemporalDisplacement"] for item in retarget_audits]
        check(
            "asset.quality.retarget_profile_applied",
            len(retarget_audits) == len(expected_animation_clips),
            "Every incompatible animation source must use the governed retarget profile.",
        )
        check(
            "asset.quality.retarget_mapping_coverage",
            bool(mapped_ratios) and min(mapped_ratios) >= float(retarget_profile["minimumMappedBoneRatio"]),
            "Retarget mapping coverage must meet the governed target-rig threshold.",
        )
        check(
            "asset.quality.retarget_deformation_finite",
            bool(retarget_audits) and invalid_coordinates == 0,
            "Retargeted skin deformation must contain only finite vertex coordinates.",
        )
        check(
            "asset.quality.retarget_deformation_bounds",
            bool(maximum_expansions)
            and max(maximum_expansions) <= float(retarget_profile["maximumBoundsExpansion"])
            and min(minimum_ratios) >= 0.25,
            "Retargeted skin bounds must neither explode nor collapse across sampled frames.",
        )
        check(
            "asset.quality.retarget_motion_present",
            bool(temporal_displacements) and min(temporal_displacements) >= 0.001,
            "Retargeted clips must produce measurable temporal skin motion.",
        )

    return {
        "schemaVersion": 1,
        "passed": all(item["status"] != "fail" for item in checks),
        "metrics": {
            "baseMeshCount": len(base_meshes),
            "lodMeshCount": len(lod_meshes),
            "collisionMeshCount": len(collision_meshes),
            "vertexCount": vertices,
            "triangleCount": triangles,
            "materialCount": len(materials),
            "textureCount": len(images),
            "missingTextureCount": len(missing_textures),
            "invalidNormalCount": invalid_normals,
            "dimensionsMeters": [round(value, 6) for value in dimensions],
            "boundsMinMeters": [round(value, 6) for value in minimum],
            "boundsMaxMeters": [round(value, 6) for value in maximum],
            "pivotClass": pivot_class,
            "pivotPolicy": options["pivot"],
            "armatureCount": len(armatures),
            "boneCount": sum(len(item.data.bones) for item in armatures),
            "animationClipCount": len(animation_clips),
            "animationClips": animation_clips,
            "animationDurationsSeconds": animation_durations,
            "retargetAuditCount": len(retarget_audits),
            "retargetAudits": retarget_audits,
        },
        "checks": checks,
    }


def main() -> None:
    source, output, preview, report, operations, options = _arguments()
    _reset()
    if "create_primitives" in operations:
        _create_primitives(options["creationRecipe"])
    else:
        _import_model(source)
        _select_meshes(options.get("includedMeshNames", []))
        _merge_supplemental_mesh_sources(options.get("supplementalMeshSources", []))
    _apply_material_textures(options.get("materialTextures", []))
    animation_sources = options.get("animationSources", [])
    if animation_sources:
        animation_clips, retarget_audits = _merge_animation_sources(
            animation_sources,
            options.get("retargetProfile"),
        )
    else:
        animation_clips = _materialize_embedded_actions_as_nla()
        retarget_audits = []
    _bind_unanimated_auxiliary_eye_armatures()
    has_embedded_animations = any(
        item.type == "ARMATURE"
        and item.animation_data is not None
        and len(item.animation_data.nla_tracks) > 0
        for item in bpy.context.scene.objects
    )
    _decimate_base_meshes(float(options.get("baseDecimationRatio", 1.0)))
    if "normalize_model" in operations:
        _normalize(options, source)
    if "generate_lods" in operations:
        _generate_lods(options["lodRatios"])
    if "generate_collision" in operations or options["generateCollision"]:
        _generate_collision()
    if "generate_thumbnail" in operations:
        _render_thumbnail(preview)
    quality = _audit(options, operations, preview, retarget_audits)
    output.parent.mkdir(parents=True, exist_ok=True)
    export_options = {
        "filepath": str(output),
        "export_format": "GLB",
        # Applying transforms during glTF export can invalidate skin animation data. Preserve both
        # externally merged clips and runtime-ready clips already embedded in the source GLB.
        "export_apply": not animation_clips and not has_embedded_animations,
        "export_yup": True,
        "export_extras": True,
    }
    if animation_clips:
        export_options.update({
            "export_animations": True,
            "export_animation_mode": "NLA_TRACKS",
            "export_merge_animation": "NLA_TRACK",
            "export_nla_strips": True,
            "export_anim_single_armature": len([
                item for item in bpy.context.scene.objects if item.type == "ARMATURE"
            ]) == 1,
        })
    bpy.ops.export_scene.gltf(**export_options)
    report.parent.mkdir(parents=True, exist_ok=True)
    report.write_text(json.dumps(quality, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
