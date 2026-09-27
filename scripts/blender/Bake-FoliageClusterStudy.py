"""Blender 5.2 study: three albedo/coverage views of one complete-leaf cluster.

No kit replacement, LOD export or runtime qualification is claimed.
Run headless, factory-startup, disable-autoexec, python-exit-code 1.
"""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).parent))
from foliage_clusters import partition_faces
from foliage_cluster_materials import bake_material


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--sha256', required=True)
    parser.add_argument('--object-name', required=True)
    parser.add_argument('--cell-size', type=float, default=1.5)
    parser.add_argument('--normal', action='store_true', help='also bake linear view-tangent normals')
    parser.add_argument('--output-dir', type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    if bpy.app.version[:2] != (5, 2):
        raise RuntimeError('This study requires Blender 5.2; historical workers are unchanged')
    source, output = args.source.resolve(), args.output_dir.resolve()
    if output.exists() or output.drive.lower() != 'e:' or source.stat().st_size > 128 * 1024**2:
        raise ValueError('fresh E: output and bounded source required')
    if sha(source) != args.sha256.lower():
        raise ValueError('source hash mismatch')
    raw = source.read_bytes()
    magic, version, total, length, chunk = struct.unpack_from('<5I', raw)
    if (magic, version, total, chunk) != (0x46546C67, 2, len(raw), 0x4E4F534A):
        raise ValueError('invalid GLB header')
    gltf = json.loads(raw[20:20 + length])
    if any('uri' in item for item in gltf.get('images', []) + gltf.get('buffers', [])):
        raise ValueError('external dependencies refused')
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(source))
    original = bpy.data.objects.get(args.object_name)
    if original is None or original.type != 'MESH' or original.get('forge_lod') != 0:
        raise ValueError('explicit LOD0 foliage object required')
    if not all(m and m.get('forge_foliage_alpha') for m in original.data.materials):
        raise ValueError('only explicitly tagged foliage materials supported')
    if original.modifiers or original.data.shape_keys or original.data.color_attributes:
        raise ValueError('deformations and vertex colors are not supported by this study')
    positions = [tuple(original.matrix_world @ v.co) for v in original.data.vertices]
    faces = [tuple(p.vertices) for p in original.data.polygons]
    groups = partition_faces(positions, faces, args.cell_size)
    assert groups == partition_faces(positions, faces, args.cell_size)
    assert sorted(i for g in groups for i in g['faces']) == list(range(len(faces)))
    chosen = min(groups, key=lambda g: (-len(g['faces']), g['cell']))
    selected = chosen['faces']
    used = sorted({v for i in selected for v in faces[i]})
    remap = {v:i for i,v in enumerate(used)}
    mesh = bpy.data.meshes.new('StudyCompleteLeafCluster')
    mesh.from_pydata([positions[v] for v in used], [], [tuple(remap[v] for v in faces[i]) for i in selected])
    for material in original.data.materials:
        mesh.materials.append(bake_material(material))
    for target, source_index in zip(mesh.polygons, selected, strict=True):
        target.material_index = original.data.polygons[source_index].material_index
        target.use_smooth = original.data.polygons[source_index].use_smooth
    for layer in original.data.uv_layers:
        target = mesh.uv_layers.new(name=layer.name)
        for polygon, source_index in zip(mesh.polygons, selected, strict=True):
            before = original.data.polygons[source_index]
            for a,b in zip(polygon.loop_indices, before.loop_indices, strict=True):
                target.data[a].uv = layer.data[b].uv
    normal_matrix = original.matrix_world.to_3x3().inverted().transposed()
    mesh.normals_split_custom_set([
        tuple((normal_matrix @ original.data.corner_normals[loop].vector).normalized())
        for source_index in selected for loop in original.data.polygons[source_index].loop_indices])
    for obj in bpy.context.scene.objects:
        obj.hide_render = True
    cluster = bpy.data.objects.new('StudyCluster', mesh)
    bpy.context.collection.objects.link(cluster)
    corners = [Vector(positions[v]) for v in used]
    low = Vector(tuple(min(p[i] for p in corners) for i in range(3)))
    high = Vector(tuple(max(p[i] for p in corners) for i in range(3)))
    center = (low + high) / 2
    diameter = (high - low).length
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 16
    scene.cycles.use_denoising = False
    scene.cycles.use_adaptive_sampling = False
    scene.cycles.transparent_max_bounces = 64
    scene.render.threads_mode = 'FIXED'
    scene.render.threads = 2
    scene.render.resolution_x = scene.render.resolution_y = 512
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'None'
    scene.view_settings.exposure = 0
    scene.view_settings.gamma = 1
    camera_data = bpy.data.cameras.new('StudyCamera')
    camera_data.type = 'ORTHO'
    camera_data.ortho_scale = diameter * 1.08
    camera_data.clip_end = max(100, diameter * 10)
    camera = bpy.data.objects.new('StudyCamera', camera_data)
    bpy.context.collection.objects.link(camera)
    scene.camera = camera
    output.mkdir(parents=True, exist_ok=False)
    records = []
    for label, direction in [('x',Vector((1,0,0))),('y',Vector((0,1,0))),('z',Vector((0,0,1)))]:
        camera.location = center + direction * diameter * 2
        camera.rotation_euler = (-direction).to_track_quat('-Z','Y').to_euler()
        basis = [tuple(camera.rotation_euler.to_matrix() @ Vector(v))
                 for v in ((1,0,0),(0,1,0),(0,0,1))]
        for channel in (('albedo','normal') if args.normal else ('albedo',)):
            for index, source_material in enumerate(original.data.materials):
                mesh.materials[index] = bake_material(source_material, channel, basis)
            scene.view_settings.view_transform = 'Raw' if channel == 'normal' else 'Standard'
            path = output / f'cluster-{label}-{channel}.png'
            scene.render.filepath = str(path)
            bpy.ops.render.render(write_still=True)
            image = bpy.data.images.load(str(path), check_existing=False)
            if channel == 'normal':
                image.colorspace_settings.name = 'Non-Color'
            pixels = list(image.pixels)
            alpha = pixels[3::4]
            if tuple(image.size) != (512,512) or not any(a > .1 for a in alpha) or not any(a == 0 for a in alpha):
                raise ValueError('invalid or empty coverage render')
            records.append({'axis':label,'channel':channel,'sha256':sha(path),'resolution':list(image.size),
                            'covered_pixels':sum(a > .1 for a in alpha), 'view_basis_world':basis})
            bpy.data.images.remove(image)
    if sha(source) != args.sha256.lower():
        raise ValueError('source changed during study')
    report = {'schema':'forge.foliage-cluster-study','schema_version':1,
        'blender':bpy.app.version_string,'source':str(source),'source_sha256':sha(source),
        'script_sha256':sha(Path(__file__)),'partition_script_sha256':sha(Path(__file__).with_name('foliage_clusters.py')),
        'material_script_sha256':sha(Path(__file__).with_name('foliage_cluster_materials.py')),
        'coordinate_system':{'unit':'metre','up_axis':'z','space':'Blender world after glTF import'},
        'normal_encoding':'linear_RGB_0.5N+0.5_view_tangent_camera_right_up_front_facing_hemisphere' if args.normal else None,
        'source_object':args.object_name,'cell_size_m':args.cell_size,'groups':len(groups),
        'source_faces':len(faces),'selected_cell':chosen['cell'],'selected_faces':len(selected),
        'selected_source_face_indices':selected,'bounds_min_m':list(low),'bounds_max_m':list(high),
        'orthographic_size_m':camera_data.ortho_scale,'renders':records,
        'scope':'one cluster material study; no atlas packing, LOD export or runtime qualification',
        'runtime_promoted':False,'human_accepted':False}
    (output/'study.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(f'FOLIAGE_CLUSTER_STUDY_PASS groups={len(groups)} selected_faces={len(selected)} renders={len(records)} source_unchanged=true')


if __name__ == '__main__':
    main()
