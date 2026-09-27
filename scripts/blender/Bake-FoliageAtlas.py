"""All complete-leaf clusters in two bounded CPU renders, without replacing a kit.

Coordinates and tile rows are Blender Z-up / image bottom-up. The mapping retains
world-space card centers/bases for a subsequent explicit glTF export.
"""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import sys

import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).parent))
from foliage_clusters import partition_faces
from foliage_atlas_layout import atlas_layout
from foliage_cluster_materials import bake_material
from foliage_bake_pixels import material_pixels, png_bytes


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--sha256', required=True)
    parser.add_argument('--object-name', required=True)
    parser.add_argument('--cell-size', type=float, default=1.5)
    parser.add_argument('--output-dir', type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    source, output = args.source.resolve(), args.output_dir.resolve()
    if bpy.app.version[:2] != (5, 2):
        raise ValueError('Blender 5.2 required')
    if output.exists() or output.drive.lower() != 'e:' or source.stat().st_size > 128 * 1024**2:
        raise ValueError('fresh E: output and bounded source required')
    if sha(source) != args.sha256.lower():
        raise ValueError('source hash mismatch')
    raw = source.read_bytes()
    magic, version, total, length, chunk = struct.unpack_from('<5I', raw)
    if (magic, version, total, chunk) != (0x46546c67, 2, len(raw), 0x4e4f534a):
        raise ValueError('invalid GLB')
    gltf = json.loads(raw[20:20 + length])
    if any('uri' in item for item in gltf.get('images', []) + gltf.get('buffers', [])):
        raise ValueError('external dependencies refused')
    if any('occlusionTexture' in m for m in gltf.get('materials', [])
           if m.get('extras', {}).get('forge_foliage_alpha')):
        raise ValueError('source occlusion must not be replaced with neutral AO')
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(source))
    original = bpy.data.objects.get(args.object_name)
    if original is None or original.type != 'MESH' or original.get('forge_lod') != 0:
        raise ValueError('explicit LOD0 foliage object required')
    if not original.data.materials or not all(m and m.get('forge_foliage_alpha') for m in original.data.materials):
        raise ValueError('tagged foliage materials required')
    if (original.modifiers or original.data.shape_keys or original.data.color_attributes or
        len(original.data.uv_layers) != 1):
        raise ValueError('only static foliage with exactly one UV layer supported')
    positions = [original.matrix_world @ v.co for v in original.data.vertices]
    faces = [tuple(p.vertices) for p in original.data.polygons]
    groups = partition_faces(positions, faces, args.cell_size)
    if sorted(i for g in groups for i in g['faces']) != list(range(len(faces))):
        raise ValueError('partition lost or duplicated source faces')
    plan = atlas_layout(len(groups))  # Refuse excessive allocations before constructing the bake mesh.
    bases = {'x': ((0,1,0),(0,0,1),(1,0,0)),
             'y': ((-1,0,0),(0,0,1),(0,1,0)), 'z': ((1,0,0),(0,1,0),(0,0,1))}
    normal_matrix = original.matrix_world.to_3x3().inverted().transposed()
    source_normals = [(normal_matrix @ n.vector).normalized() for n in original.data.corner_normals]
    source_uv = original.data.uv_layers[0]
    vertices, polygons, uvs, normals, material_indices, smoothing = [], [], [], [], [], []
    records = []
    for tile in plan['tiles']:
        group = groups[tile['group']]
        indices = group['faces']
        used = sorted({v for i in indices for v in faces[i]})
        low = Vector(tuple(min(positions[v][i] for v in used) for i in range(3)))
        high = Vector(tuple(max(positions[v][i] for v in used) for i in range(3)))
        center = (low + high) / 2
        diameter = (high - low).length * 1.08
        if diameter <= 0:
            raise ValueError('degenerate cluster bounds')
        basis = [Vector(v) for v in bases[tile['axis']]]
        scale = tile['content_size'] / diameter
        x, y = tile['tile_xy']
        offset = Vector((x + plan['tile_size']/2, y + plan['tile_size']/2, 0))
        remap = {v: len(vertices) + i for i, v in enumerate(used)}
        vertices.extend(tuple(Vector(tuple((positions[v] - center).dot(b) for b in basis)) * scale + offset)
                        for v in used)
        for i in indices:
            polygon = original.data.polygons[i]
            polygons.append(tuple(remap[v] for v in faces[i]))
            material_indices.append(polygon.material_index)
            smoothing.append(polygon.use_smooth)
            for loop in polygon.loop_indices:
                uvs.append(tuple(source_uv.data[loop].uv))
                normals.append(tuple(source_normals[loop].dot(b) for b in basis))
        records.append({**tile, 'cell': group['cell'], 'source_faces': len(indices),
                        'center_blender_m': list(center), 'basis_blender': bases[tile['axis']],
                        'card_size_m': diameter})
    for obj in bpy.context.scene.objects:
        obj.hide_render = True
    mesh = bpy.data.meshes.new('AllClusterAtlasBake')
    mesh.from_pydata(vertices, [], polygons)
    for material in original.data.materials:
        mesh.materials.append(bake_material(material))
    for polygon, index, smooth in zip(mesh.polygons, material_indices, smoothing, strict=True):
        polygon.material_index, polygon.use_smooth = index, smooth
    uv_layer = mesh.uv_layers.new(name=source_uv.name)
    uv_layer.data.foreach_set('uv', np.asarray(uvs, dtype=np.float32).ravel())
    mesh.normals_split_custom_set(normals)
    obj = bpy.data.objects.new('AllClusterAtlasBake', mesh)
    bpy.context.collection.objects.link(obj)
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 16
    # These images encode material data, not noisy lighting. The factory-default
    # denoiser can invent directions outside the encoded normal hemisphere.
    scene.cycles.use_denoising = False
    scene.cycles.use_adaptive_sampling = False
    scene.cycles.transparent_max_bounces = 64
    scene.render.threads_mode, scene.render.threads = 'FIXED', 2
    size = plan['size']
    scene.render.resolution_x = scene.render.resolution_y = size
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = 'OPEN_EXR'
    scene.render.image_settings.color_depth = '32'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.view_settings.look = 'None'
    scene.view_settings.exposure, scene.view_settings.gamma = 0, 1
    camera_data = bpy.data.cameras.new('AtlasCamera')
    camera_data.type, camera_data.ortho_scale, camera_data.clip_end = 'ORTHO', size, 2000
    camera = bpy.data.objects.new('AtlasCamera', camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (size/2, size/2, 1000)
    camera.rotation_euler = (0, 0, 0)
    scene.camera = camera
    output.mkdir(parents=True, exist_ok=False)
    images, raw_channels = [], {}
    for channel in ('albedo', 'normal', 'orm'):
        for i, material in enumerate(original.data.materials):
            mesh.materials[i] = bake_material(material, channel, bases['z'])
        scene.view_settings.view_transform = 'Standard' if channel == 'albedo' else 'Raw'
        path = output / f'raw-{channel}.exr'
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        image = bpy.data.images.load(str(path), check_existing=False)
        image.colorspace_settings.name = 'Non-Color'
        pixels = np.empty(size * size * 4, dtype=np.float32)
        image.pixels.foreach_get(pixels)
        pixels = pixels.reshape(size, size, 4)
        if tuple(image.size) != (size, size) or not np.isfinite(pixels).all():
            raise ValueError('invalid atlas pixels')
        raw_channels[channel] = pixels
        bpy.data.images.remove(image)
    for channel, raw_pixels in raw_channels.items():
        pixels = material_pixels(raw_pixels, raw_channels['orm'])
        path = output / f'atlas-{channel}.png'
        path.write_bytes(png_bytes(pixels, srgb=channel == 'albedo'))
        if channel == 'normal':
            opaque = pixels[:, :, 3] == 1
            if not opaque.any() or pixels[:, :, 2][opaque].min() < 126 / 255:
                raise ValueError('encoded normals escaped the front hemisphere')
        for record in records:
            x, y = record['tile_xy']
            alpha = pixels[y:y+64, x:x+64, 3]
            if max(alpha[:2].max(), alpha[-2:].max(), alpha[:, :2].max(), alpha[:, -2:].max()) > 0:
                raise ValueError('coverage escaped a tile gutter')
            record[f'{channel}_covered_pixels'] = int(np.count_nonzero(alpha > .1))
        for start in range(0, len(records), 3):
            if not any(r[f'{channel}_covered_pixels'] for r in records[start:start+3]):
                raise ValueError('cluster disappeared in every axis')
        images.append({'path': path.name, 'sha256': sha(path), 'channel': channel})
    if sha(source) != args.sha256.lower():
        raise ValueError('source changed')
    report = {'schema': 'forge.foliage-atlas-study', 'schema_version': 1,
              'blender': bpy.app.version_string, 'source': str(source), 'source_sha256': sha(source),
              'scripts': {p.name: sha(p) for p in (Path(__file__), *(Path(__file__).with_name(n)
                          for n in ('foliage_clusters.py', 'foliage_atlas_layout.py', 'foliage_cluster_materials.py',
                                    'foliage_bake_pixels.py')))},
              'source_object': args.object_name, 'cell_size_m': args.cell_size,
              'groups': len(groups), 'source_faces': len(faces), 'layout': plan,
              'tiles': records, 'images': images, 'pixel_origin': 'bottom-left',
              'normal_encoding': 'linear_0.5N+0.5_card_right_up_front_facing_hemisphere',
              'render_settings': {'device': 'CPU', 'threads': 2, 'samples': 16,
                                  'denoising': False, 'adaptive_sampling': False},
              'pixel_conversion': 'float_EXR_radiance_divided_by_shared_unit_emission_reference',
              'runtime_promoted': False, 'human_accepted': False,
              'remaining': ['RGB dilation and mip isolation',
                            'card GLB export and reimport', 'runtime integration and comparison']}
    (output/'atlas.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
    print(f'FOLIAGE_ATLAS_PASS groups={len(groups)} views={len(records)} size={size} source_unchanged=true')


if __name__ == '__main__':
    main()
