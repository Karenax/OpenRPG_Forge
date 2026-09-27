"""Actual CPU renders of known normals, not a textual shader test."""
import argparse
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).parent))
from foliage_cluster_materials import bake_material


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output-dir', type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    args.output_dir.mkdir(parents=True, exist_ok=False)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 8
    scene.cycles.use_denoising = False
    scene.cycles.use_adaptive_sampling = False
    scene.render.threads_mode = 'FIXED'
    scene.render.threads = 2
    scene.render.resolution_x = scene.render.resolution_y = 32
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.view_settings.view_transform = 'Raw'
    scene.view_settings.look = 'None'
    bpy.ops.mesh.primitive_plane_add()
    plane = bpy.context.object
    camera_data = bpy.data.cameras.new('NormalTest')
    camera_data.type = 'ORTHO'
    camera_data.ortho_scale = 3
    camera = bpy.data.objects.new('NormalTest', camera_data)
    bpy.context.collection.objects.link(camera)
    scene.camera = camera
    n = (.3,.4,math.sqrt(.75))
    cases = [('tilted',n,None,(0,0,1),n),
             ('back_face',tuple(-v for v in n),None,(0,0,1),n),
             ('linked_normal',(0,0,1),(.8,0,.6),(0,0,1),(.8,0,.6)),
             ('grazing_positive',(0,0,1),(math.sqrt(.96),0,.2),(0,0,1),(math.sqrt(.96),0,.2)),
             ('camera_x',(1,0,0),None,(1,0,0),(1,0,0))]
    records = []
    for label, geometry_normal, linked_normal, view_direction, expected in cases:
        plane.rotation_mode = 'QUATERNION'
        plane.rotation_quaternion = Vector((0,0,1)).rotation_difference(Vector(geometry_normal))
        camera.location = Vector(view_direction)*4
        camera.rotation_euler = (-Vector(view_direction)).to_track_quat('-Z','Y').to_euler()
        basis = [tuple(camera.rotation_euler.to_matrix() @ Vector(v)) for v in ((1,0,0),(0,1,0),(0,0,1))]
        source = bpy.data.materials.new('Source_'+label)
        source.use_nodes = True
        bsdf = next(n for n in source.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
        if linked_normal:
            normal = source.node_tree.nodes.new('ShaderNodeCombineXYZ')
            for i,value in enumerate(linked_normal): normal.inputs[i].default_value = value
            source.node_tree.links.new(normal.outputs[0],bsdf.inputs['Normal'])
        node_count, link_count = len(source.node_tree.nodes), len(source.node_tree.links)
        material = bake_material(source,'normal',basis)
        assert (node_count,link_count) == (len(source.node_tree.nodes),len(source.node_tree.links))
        plane.data.materials.clear()
        plane.data.materials.append(material)
        path = args.output_dir / f'{label}.png'
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        image = bpy.data.images.load(str(path),check_existing=False)
        image.colorspace_settings.name = 'Non-Color'
        pixels = list(image.pixels)
        actual = pixels[(16*32+16)*4:(16*32+16)*4+4]
        # to_track_quat may roll the camera (notably on the world Z axis).
        # Expected normals above are world-space, so project through the actual
        # recorded camera frame instead of assuming camera right == world X.
        right, up, front = [Vector(v) for v in basis]
        assert abs(right.cross(up).dot(front) - 1) < 1e-5
        target = [Vector(expected).dot(axis)*.5+.5 for axis in (right,up,front)]
        assert actual[3] > .99, (label,actual)
        assert max(abs(a-b) for a,b in zip(actual[:3],target)) <= 2/255, (
            label,actual,target,basis,list(plane.matrix_world.to_3x3() @ Vector((0,0,1))))
        records.append({'case':label,'rgba':actual,'expected_rgb':target})
        bpy.data.images.remove(image)
    (args.output_dir/'results.json').write_text(json.dumps({'status':'PASS','blender':bpy.app.version_string,'cases':records},indent=2)+'\n')
    print('FOLIAGE_CLUSTER_NORMALS_PASS cases=5 source_graphs_unchanged=true')


if __name__ == '__main__':
    main()
