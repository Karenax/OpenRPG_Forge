"""Independently reopen skin accessors, hierarchy and two captured deformations."""
import json
import math
from pathlib import Path
import struct

import verify_medieval_skin_positions as native

W = Path('F:/OpenRPG_ImportWork')
OUT = W / 'medieval-native-skin-glb-reopen-v1'


def main():
    assert not OUT.exists(), 'Previous skin reopen preserved'
    receipt_path = W / 'medieval-native-skinned-geometry-v1/RESULT.v1.json'
    receipt = json.loads(receipt_path.read_text('utf-8-sig'))
    pins = dict(receipt['source_inputs'])
    path = Path(receipt['glb'])
    pins[str(path)] = receipt['glb_sha256']
    pins[str(receipt_path)] = native.sha(receipt_path)
    pins[str(Path(__file__).resolve())] = native.sha(__file__)
    for p, expected in pins.items():
        assert native.sha(p) == expected, 'Input changed'
    raw = path.read_bytes()
    assert struct.unpack_from('<4sII', raw) == (b'glTF', 2, len(raw))
    size, kind = struct.unpack_from('<I4s', raw, 12)
    assert kind == b'JSON' and size % 4 == 0
    doc = json.loads(raw[20:20 + size])
    offset = 20 + size
    length, kind = struct.unpack_from('<I4s', raw, offset)
    assert kind == b'BIN\0' and offset + 8 + length == len(raw)
    binary = raw[offset + 8:]
    assert len(doc['buffers']) == 1 and doc['buffers'][0]['byteLength'] <= len(binary)
    assert len(doc['nodes']) == 3 and len(doc['skins']) == len(doc['meshes']) == 1
    assert not doc.get('animations') and not doc.get('materials')
    assert doc['extras']['material_qualification_pending']

    def access(index):
        a = doc['accessors'][index]
        view = doc['bufferViews'][a['bufferView']]
        width = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}[a['type']]
        code, item_size = {5123: ('H', 2), 5125: ('I', 4), 5126: ('f', 4)}[a['componentType']]
        assert not a.get('sparse') and not a.get('normalized') and not view.get('byteStride')
        start = view.get('byteOffset', 0) + a.get('byteOffset', 0)
        count = a['count'] * width
        assert start % item_size == 0 and count * item_size == view['byteLength']
        assert start + count * item_size <= doc['buffers'][0]['byteLength']
        return struct.unpack_from('<' + str(count) + code, binary, start)

    primitive = doc['meshes'][0]['primitives'][0]
    attrs = primitive['attributes']
    positions, normals = access(attrs['POSITION']), access(attrs['NORMAL'])
    joints, weights = access(attrs['JOINTS_0']), access(attrs['WEIGHTS_0'])
    assert len(positions) == len(normals) == 3072
    assert joints == (0,) * 4096 and weights == (1.0, 0.0, 0.0, 0.0) * 1024
    capture = native.ROOT / 'export'
    meta = json.loads((capture / 'RESULT.json').read_text('utf-8-sig'))
    for semantic, values, file, width in [('POSITION', positions, 'positions.f32', 3),
                                          ('NORMAL', normals, 'normals.f32', 3)]:
        original = native.floats(capture / file)
        assert all(a == (-b if i % width == 0 else b) for i, (a, b) in enumerate(zip(values, original)))
    assert access(attrs['COLOR_0']) == native.floats(capture / 'colors.f32')
    uv, original_uv = access(attrs['TEXCOORD_0']), native.floats(capture / 'uv0.f32')
    for i in range(1024):
        rounded_v = struct.unpack('<f', struct.pack('<f', 1 - original_uv[i * 4 + 1]))[0]
        assert uv[i * 2:i * 2 + 2] == (original_uv[i * 4], rounded_v)
    source_indices = struct.unpack('<5766i', (capture / 'submesh-0.i32').read_bytes())
    assert access(primitive['indices']) == tuple(v for i in range(0, len(source_indices), 3)
                                                for v in (source_indices[i], source_indices[i + 2], source_indices[i + 1]))
    skin = doc['skins'][0]
    assert skin['joints'] == [1] and skin['skeleton'] == 1
    assert doc['nodes'][2]['mesh'] == doc['nodes'][2]['skin'] == 0
    bind_values = access(skin['inverseBindMatrices'])
    bind = [[bind_values[c * 4 + r] for c in range(4)] for r in range(4)]
    source_bind = native.floats(capture / 'bindposes.f32')
    signs = [-1, 1, 1, 1]
    assert all(bind[r][c] == signs[r] * signs[c] * source_bind[c * 4 + r]
               for r in range(4) for c in range(4))
    worlds = []
    for i, original in enumerate(meta['hierarchy']):
        node = doc['nodes'][i]
        assert node['name'] == original['name']
        t, q, s = node['translation'], node['rotation'], node['scale']
        assert t == [-original['position']['x'], original['position']['y'], original['position']['z']]
        assert q == [original['rotation']['x'], -original['rotation']['y'], -original['rotation']['z'], original['rotation']['w']]
        assert s == [original['scale'][a] for a in 'xyz']
        x, y, z, w = q
        assert abs(sum(v * v for v in q) - 1) < 1e-6
        local = [[(1 - 2 * (y*y + z*z))*s[0], 2*(x*y-z*w)*s[1], 2*(x*z+y*w)*s[2], t[0]],
                 [2*(x*y+z*w)*s[0], (1-2*(x*x+z*z))*s[1], 2*(y*z-x*w)*s[2], t[1]],
                 [2*(x*z-y*w)*s[0], 2*(y*z+x*w)*s[1], (1-2*(x*x+y*y))*s[2], t[2]], [0,0,0,1]]
        parent = original['parent']
        world = local if parent < 0 else native.multiply(worlds[parent], local)
        worlds.append(world)
        expected = native.matrix(original['world'])
        assert all(abs(world[r][c] - signs[r]*signs[c]*expected[r][c]) < 1e-6 for r in range(4) for c in range(4))
        children = [row['index'] for row in meta['hierarchy'] if row['parent'] == i]
        assert node.get('children', []) == children
    reports = []
    for pose_index, pose in enumerate(meta['probe_poses']):
        if pose_index == 0:
            mesh_world, joint_world = worlds[2], worlds[1]
        else:
            def reflected_matrix(matrix):
                return [[signs[r]*signs[c]*matrix[f'e{r}{c}'] for c in range(4)] for r in range(4)]
            mesh_world = reflected_matrix(pose['mesh_world'])
            joint_world = reflected_matrix(pose['joint_world'][0])
        palette = native.multiply(native.multiply(native.inverse(mesh_world), joint_world), bind)
        maximum = [0.0, 0.0]
        for attribute_index, (values, name) in enumerate([(positions, 'positions'), (normals, 'normals')]):
            baked = native.floats(capture / f'pose-{pose_index}-{name}.f32')
            for vertex in range(1024):
                vector = [*values[vertex * 3:vertex * 3 + 3], float(attribute_index == 0)]
                for axis in range(3):
                    predicted = sum(palette[axis][k] * vector[k] for k in range(4))
                    observed = (-1 if axis == 0 else 1) * baked[vertex * 3 + axis]
                    error = abs(predicted - observed)
                    assert math.isfinite(error) and error < 2e-5 * max(1, abs(predicted), abs(observed))
                    maximum[attribute_index] = max(maximum[attribute_index], error)
        reports.append(dict(pose=pose['name'], vertices=1024, max_position_error=maximum[0], max_normal_error=maximum[1]))
    assert all(native.sha(p) == expected for p, expected in pins.items())
    result = dict(status='skin_glb_streams_hierarchy_and_two_native_deformations_reopened',
                  glb=str(path), vertices=1024, joints=1, nodes=3, clips=0, poses=reports,
                  input_pins=pins, material_qualification_pending=True, gltf_validator_executed=False,
                  gpu_verified=False, studio_verified=False, vault_indexed=False, full_import=False)
    OUT.mkdir()
    (OUT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps({k: v for k, v in result.items() if k != 'input_pins'}))


if __name__ == '__main__':
    main()
