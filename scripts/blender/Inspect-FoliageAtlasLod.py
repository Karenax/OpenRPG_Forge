"""Reimport and paired LOD1 renders of pinned source and atlas-derived GLB."""
import argparse
import hashlib
import json
from pathlib import Path
import sys

import bpy
from mathutils import Vector


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--derivation', type=Path, required=True)
    parser.add_argument('--output-dir', type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    evidence = json.loads(args.derivation.read_text())
    source = Path(evidence['source'])
    candidate = args.derivation.parent/evidence['glb']
    if sha(source) != evidence['source_sha256'] or sha(candidate) != evidence['glb_sha256']:
        raise ValueError('input changed')
    if bpy.app.version[:2] != (5,2): raise ValueError('Blender 5.2 required')
    output = args.output_dir.resolve()
    if output.exists() or output.drive.lower() != 'e:': raise ValueError('fresh E: output required')
    output.mkdir(parents=True,exist_ok=False)
    records, framing = [], None
    for label, path in (('source',source), ('candidate',candidate)):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=str(path))
        scene = bpy.context.scene
        render_objects = []
        for obj in scene.objects:
            visible = obj.type == 'MESH' and obj.get('forge_role') == 'render' and obj.get('forge_lod') == 1
            obj.hide_render = not visible
            if visible: render_objects.append(obj)
        leaves = next(o for o in render_objects if 'Foliage' in o.name)
        leaves.data.calc_loop_triangles()
        expected = evidence['triangles_before' if label == 'source' else 'triangles_after']
        if len(leaves.data.loop_triangles) != expected: raise ValueError('reimport triangle count changed')
        if label == 'candidate' and (len(leaves.data.materials) != 1 or len(leaves.data.uv_layers) != 1):
            raise ValueError('candidate material/UV import mismatch')
        points = [o.matrix_world @ Vector(c) for o in render_objects for c in o.bound_box]
        low = Vector(tuple(min(p[i] for p in points) for i in range(3)))
        high = Vector(tuple(max(p[i] for p in points) for i in range(3)))
        if framing is None: framing = ((low+high)/2, (high-low).length)
        center, diameter = framing
        scene.render.engine = 'CYCLES'
        scene.cycles.device = 'CPU'
        scene.cycles.samples = 16
        scene.cycles.use_denoising = False
        scene.cycles.use_adaptive_sampling = False
        scene.cycles.transparent_max_bounces = 128
        scene.render.threads_mode, scene.render.threads = 'FIXED', 2
        scene.render.resolution_x, scene.render.resolution_y = 512, 768
        scene.render.resolution_percentage = 100
        scene.render.image_settings.file_format = 'PNG'
        scene.world = bpy.data.worlds.new('InspectionWorld')
        scene.world.color = (.22,.22,.22)
        sun_data = bpy.data.lights.new('InspectionSun', 'SUN')
        sun_data.energy = 3
        sun = bpy.data.objects.new('InspectionSun',sun_data)
        bpy.context.collection.objects.link(sun)
        sun.rotation_euler = (.4,-.5,-.4)
        camera_data = bpy.data.cameras.new('InspectionCamera')
        camera_data.type = 'ORTHO'
        camera_data.ortho_scale = diameter * 1.15
        camera_data.clip_end = diameter * 10
        camera = bpy.data.objects.new('InspectionCamera',camera_data)
        bpy.context.collection.objects.link(camera)
        camera.location = center + Vector((1,-2,.55)).normalized()*diameter*2
        camera.rotation_euler = (center-camera.location).to_track_quat('-Z','Y').to_euler()
        scene.camera = camera
        destination = output/(label+'.png')
        scene.render.filepath = str(destination)
        bpy.ops.render.render(write_still=True)
        records.append({'label':label, 'triangles':expected, 'bounds_min':list(low),'bounds_max':list(high),
                        'png':destination.name, 'sha256':sha(destination)})
    if sha(source) != evidence['source_sha256'] or sha(candidate) != evidence['glb_sha256']:
        raise ValueError('input changed during inspection')
    (output/'inspection.json').write_text(json.dumps({'blender':bpy.app.version_string,
        'derivation_sha256':sha(args.derivation), 'script_sha256':sha(Path(__file__)),
        'renders':records,'runtime_qualified':False,'human_accepted':False},indent=2)+'\n',encoding='utf-8')
    print('FOLIAGE_ATLAS_REIMPORT_RENDER_PASS')


if __name__ == '__main__':
    main()
