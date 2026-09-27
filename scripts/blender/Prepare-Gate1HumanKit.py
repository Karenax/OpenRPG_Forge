"""Reopen exact GLBs, measure composed geometry and prepare separate editable review copies.

No automatic human approval, no source asset mutation, no export back into the active kit.
Run with Blender 5.2 --background --factory-startup --disable-autoexec --python-exit-code 1.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import re
import sys
import bpy


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def geometry():
    result = []
    for obj in sorted(bpy.context.scene.objects, key=lambda value: value.name):
        if obj.type != 'MESH':
            continue
        points = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
        if not points or any(not math.isfinite(value) for point in points for value in point):
            raise ValueError('empty or nonfinite mesh: ' + obj.name)
        low = [min(point[axis] for point in points) for axis in range(3)]
        high = [max(point[axis] for point in points) for axis in range(3)]
        obj.data.calc_loop_triangles()
        result.append({'name': obj.name, 'bounds_min': low, 'bounds_max': high,
                       'origin': list(obj.matrix_world.translation),
                       'triangles': len(obj.data.loop_triangles),
                       'materials': [slot.material.name if slot.material else None for slot in obj.material_slots],
                       'collision_proxy': obj.name.startswith('UCX_')})
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--kit', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    if bpy.app.version[:2] != (5, 2):
        raise ValueError('requires reviewed Blender 5.2')
    root = args.kit.resolve(strict=True)
    output = args.output.resolve()
    if output.drive.lower() != 'e:' or output.exists():
        raise ValueError('requires fresh E: output')
    build_path = root / 'build.json'
    build_hash = digest(build_path)
    build = json.loads(build_path.read_text())
    if build['schema'] != 'forge.blender-asset-kit-build' or not 1 <= len(build['assets']) <= 64:
        raise ValueError('unsupported kit')
    sources = []
    for asset in build['assets']:
        if not re.fullmatch('[a-z0-9_-]+', asset['id']):
            raise ValueError('unsafe ID')
        source = (root / asset['glb']['path']).resolve(strict=True)
        source.relative_to(root)
        if digest(source) != asset['glb']['sha256']:
            raise ValueError('source hash mismatch')
        sources.append((asset, source))
    output.mkdir(parents=True)
    report = {'schema': 'forge.gate1-human-kit-preparation', 'schema_version': 1,
              'blender': bpy.app.version_string, 'script_sha256': digest(Path(__file__)),
              'build_sha256': build_hash, 'coordinates': 'Blender world, meters, Z up (not Forge Y up)',
              'human_retouch_performed': False, 'publication_approved': False, 'assets': []}
    for asset, source in sources:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.context.scene.unit_settings.system = 'METRIC'
        bpy.context.scene.unit_settings.scale_length = 1.0
        bpy.ops.import_scene.gltf(filepath=str(source))
        before = geometry()
        for level in range(3):
            if not any(re.search('_LOD' + str(level) + r'(?:_|$)', item['name']) for item in before):
                raise ValueError('missing LOD after import')
        if not any(item['collision_proxy'] for item in before):
            raise ValueError('missing collision nodes')
        # Keep all original nodes/materials. Hide duplicate LODs/collision proxies for initial review only.
        for obj in bpy.context.scene.objects:
            if obj.type == 'MESH':
                obj.hide_set(bool(re.search(r'_LOD[12](?:_|$)', obj.name) or obj.name.startswith('UCX_')))
        bpy.ops.file.pack_all()
        destination = output / (asset['id'] + '.blend')
        bpy.ops.wm.save_as_mainfile(filepath=str(destination), check_existing=True)
        bpy.ops.wm.open_mainfile(filepath=str(destination), use_scripts=False)
        if geometry() != before:
            raise ValueError('geometry/material assignment changed during blend round trip')
        if digest(source) != asset['glb']['sha256']:
            raise ValueError('source changed during inspection')
        report['assets'].append({'id': asset['id'], 'source': str(source),
                                 'source_sha256': asset['glb']['sha256'], 'blend': str(destination),
                                 'blend_sha256': digest(destination), 'round_trip_equal': True,
                                 'geometry': before, 'pivot_review': 'pending_human_review',
                                 'collision_shape_review': 'pending_human_review'})
    if digest(build_path) != build_hash:
        raise ValueError('build changed during inspection')
    with (output / 'inspection.json').open('x', encoding='utf-8') as stream:
        json.dump(report, stream, indent=2, allow_nan=False)
    print('PASS reopened and round-tripped ' + str(len(sources)) + ' assets; human review pending')


if __name__ == '__main__':
    main()
