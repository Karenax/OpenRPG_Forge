"""Known scalar PBR data through overlapping transparent surfaces."""
import argparse
import json
from pathlib import Path
import sys

import bpy
import numpy as np

sys.path.insert(0,str(Path(__file__).parent))
from foliage_cluster_materials import bake_material
from foliage_bake_pixels import material_pixels


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output-dir',type=Path,required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    args.output_dir.mkdir(parents=True,exist_ok=False)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 16
    scene.cycles.use_denoising = False
    scene.cycles.use_adaptive_sampling = False
    scene.cycles.transparent_max_bounces = 64
    scene.render.threads_mode, scene.render.threads = 'FIXED', 2
    scene.render.resolution_x = scene.render.resolution_y = 32
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = 'OPEN_EXR'
    scene.render.image_settings.color_depth = '32'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.view_settings.view_transform = 'Raw'
    scene.view_settings.look = 'None'
    camera_data = bpy.data.cameras.new('Camera')
    camera_data.type, camera_data.ortho_scale = 'ORTHO', 3
    camera = bpy.data.objects.new('Camera',camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (0,0,5)
    scene.camera = camera
    source = bpy.data.materials.new('KnownPBR')
    source.use_nodes = True
    bsdf = next(n for n in source.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Alpha'].default_value = .5
    bsdf.inputs['Roughness'].default_value = .4
    bsdf.inputs['Metallic'].default_value = .2
    material = bake_material(source,'orm')
    objects, records = [], []
    for count in (1,4,16):
        while len(objects) < count:
            bpy.ops.mesh.primitive_plane_add(location=(0,0,len(objects)*.05))
            obj = bpy.context.object
            obj.data.materials.append(material)
            objects.append(obj)
        path = args.output_dir/f'layers-{count}.exr'
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        image = bpy.data.images.load(str(path),check_existing=False)
        image.colorspace_settings.name = 'Non-Color'
        pixels = np.asarray(image.pixels[:],dtype=np.float32).reshape(32,32,4)
        actual = material_pixels(pixels,pixels)[16,16].tolist()
        records.append({'layers':count,'actual':actual,'raw':pixels[16,16].tolist(),'expected_rgb':[1,.4,.2]})
        bpy.data.images.remove(image)
    (args.output_dir/'results.json').write_text(json.dumps(records,indent=2)+'\n')
    print(json.dumps(records))
    assert all(max(abs(a-b) for a,b in zip(r['actual'][:3],r['expected_rgb']))<=2/255 for r in records)
    print('ORM_LAYER_FIXTURES_PASS')


if __name__ == '__main__':
    main()
