"""Reopen an exported retarget GLB in Blender and prove its runtime animation contract."""

from __future__ import annotations

import json
import math
import pathlib
import sys

import bpy


def _arguments() -> tuple[pathlib.Path, str, int]:
    separator = sys.argv.index("--")
    values = sys.argv[separator + 1 :]
    if len(values) != 3:
        raise ValueError("Expected GLB path, clip name and target bone count.")
    return pathlib.Path(values[0]), values[1], int(values[2])


def _pose_signature(armature) -> tuple[float, ...]:
    return tuple(
        round(component, 7)
        for bone in armature.pose.bones
        for row in bone.matrix
        for component in row
    )


def main() -> None:
    path, clip_name, expected_bones = _arguments()
    if not path.is_file() or path.suffix.lower() != ".glb":
        raise ValueError(f"Retarget GLB does not exist: {path}")
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.gltf(filepath=str(path))
    armatures = [item for item in bpy.context.scene.objects if item.type == "ARMATURE"]
    if len(armatures) != 1 or len(armatures[0].data.bones) != expected_bones:
        raise ValueError(
            f"Expected one {expected_bones}-bone target armature, found "
            f"{[(item.name, len(item.data.bones)) for item in armatures]}.")
    armature = armatures[0]
    actions = [action for action in bpy.data.actions if action.name.lower() == clip_name.lower()]
    if len(actions) != 1:
        raise ValueError(f"Expected one '{clip_name}' action, found {[action.name for action in bpy.data.actions]}.")
    action = actions[0]
    if action.frame_range[1] <= action.frame_range[0]:
        raise ValueError(f"Retarget action '{clip_name}' has an empty frame range.")
    armature.animation_data_create()
    armature.animation_data.action = action
    signatures = []
    frames = sorted({
        int(round(action.frame_range[0])),
        int(round((action.frame_range[0] + action.frame_range[1]) * 0.5)),
        int(round(action.frame_range[1])),
    })
    for frame in frames:
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        signature = _pose_signature(armature)
        if any(not math.isfinite(value) for value in signature):
            raise ValueError(f"Retarget action '{clip_name}' contains a non-finite pose at frame {frame}.")
        signatures.append(signature)
    if len(set(signatures)) < 2:
        raise ValueError(f"Retarget action '{clip_name}' is static after GLB round-trip.")
    print(json.dumps({
        "marker": "OPENRPG_FORGE_AV6F_RETARGET_GLB_PASS",
        "clip": clip_name,
        "boneCount": len(armature.data.bones),
        "frameStart": round(action.frame_range[0], 6),
        "frameEnd": round(action.frame_range[1], 6),
        "sampledFrames": frames,
        "distinctPoses": len(set(signatures)),
    }, sort_keys=True))


if __name__ == "__main__":
    main()
