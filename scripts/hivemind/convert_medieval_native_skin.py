"""Produce an intermediate skinned GLB from the reopened native cloth capture.

This is geometry/deformation only, excluded from the Vault until source
materials, GPU rendering and Studio behavior are separately qualified.
"""
import json
import math
from pathlib import Path
import struct

import verify_medieval_skin_positions as source

OUT = Path('F:/OpenRPG_ImportWork/medieval-native-skinned-geometry-v1')


def reflect(raw, width, columns):
    assert len(raw) % (width * 4) == 0
    out = bytearray(raw)
    for offset in range(0, len(raw), width * 4):
        for column in columns:
            out[offset + column * 4 + 3] ^= 128
    return bytes(out)


def main():
    assert not OUT.exists(), 'Previous derivative preserved'
    capture = source.ROOT / 'export'
    reopened = source.ROOT / 'REOPEN.v1.json'
    pins = dict(json.loads(reopened.read_text('utf-8-sig'))['input_pins'])
    for path in [reopened, Path(__file__).resolve(), Path(source.__file__),
                 Path('F:/OpenRPG_ImportWork/medieval-skin-position-parity-v1/RESULT.v1.json'),
                 Path('F:/OpenRPG_ImportWork/medieval-skin-normal-parity-v1/RESULT.v1.json')]:
        pins[str(path)] = source.sha(path)
    assert all(source.sha(p) == expected for p, expected in pins.items())
    metadata = json.loads((capture / 'RESULT.json').read_text('utf-8-sig'))
    assert metadata['vertices'] == 1024 and metadata['bind_poses'] == 1
    assert metadata['blend_shapes'] == 0 and metadata['clips'] == []
    assert metadata['submeshes'] == 1 and metadata['tangents'] == 0
    assert (capture / 'bones-per-vertex.u8').read_bytes() == bytes([1]) * 1024
    weights = (capture / 'boneweights.i32-f32').read_bytes()
    assert len(weights) == 8192
    assert all(struct.unpack_from('<if', weights, i) == (0, 1.0) for i in range(0, len(weights), 8))
    doc = dict(asset=dict(version='2.0', generator='OpenRPG native skin conversion v1'),
               bufferViews=[], accessors=[], nodes=[], scenes=[dict(nodes=[0])], scene=0)
    binary = bytearray()

    def accessor(raw, component, kind, count, target=None, bounds=False):
        assert raw and count > 0
        while len(binary) % 4:
            binary.append(0)
        view = dict(buffer=0, byteOffset=len(binary), byteLength=len(raw))
        if target is not None:
            view['target'] = target
        doc['bufferViews'].append(view)
        binary.extend(raw)
        row = dict(bufferView=len(doc['bufferViews']) - 1, componentType=component, count=count, type=kind)
        if bounds:
            values = struct.unpack('<' + str(count * 3) + 'f', raw)
            assert all(math.isfinite(v) for v in values)
            row['min'] = [min(values[axis::3]) for axis in range(3)]
            row['max'] = [max(values[axis::3]) for axis in range(3)]
        doc['accessors'].append(row)
        return len(doc['accessors']) - 1

    positions = reflect((capture / 'positions.f32').read_bytes(), 3, [0])
    normals = reflect((capture / 'normals.f32').read_bytes(), 3, [0])
    attributes = dict(POSITION=accessor(positions, 5126, 'VEC3', 1024, 34962, True),
                      NORMAL=accessor(normals, 5126, 'VEC3', 1024, 34962),
                      COLOR_0=accessor((capture / 'colors.f32').read_bytes(), 5126, 'VEC4', 1024, 34962))
    uv = (capture / 'uv0.f32').read_bytes()
    assert len(uv) == 16384
    target_uv = b''.join(uv[i:i + 4] + struct.pack('<f', 1 - struct.unpack_from('<f', uv, i + 4)[0])
                         for i in range(0, len(uv), 16))
    attributes['TEXCOORD_0'] = accessor(target_uv, 5126, 'VEC2', 1024, 34962)
    attributes['JOINTS_0'] = accessor(bytes(1024 * 8), 5123, 'VEC4', 1024, 34962)
    attributes['WEIGHTS_0'] = accessor(struct.pack('<ffff', 1, 0, 0, 0) * 1024, 5126, 'VEC4', 1024, 34962)
    indices = (capture / 'submesh-0.i32').read_bytes()
    assert len(indices) % 12 == 0
    assert all(0 <= index < 1024 for index in struct.unpack('<' + str(len(indices) // 4) + 'i', indices))
    converted_indices = b''.join(indices[i:i + 4] + indices[i + 8:i + 12] + indices[i + 4:i + 8]
                                 for i in range(0, len(indices), 12))
    index_accessor = accessor(converted_indices, 5125, 'SCALAR', len(indices) // 4, 34963)
    binds = reflect((capture / 'bindposes.f32').read_bytes(), 16, [1, 2, 3, 4, 8, 12])
    bind_accessor = accessor(binds, 5126, 'MAT4', 1)
    for original in metadata['hierarchy']:
        assert original['index'] == len(doc['nodes'])
        p, q, s = original['position'], original['rotation'], original['scale']
        node = dict(name=original['name'], translation=[-p['x'], p['y'], p['z']],
                    rotation=[q['x'], -q['y'], -q['z'], q['w']], scale=[s[a] for a in 'xyz'])
        doc['nodes'].append(node)
    for original in metadata['hierarchy']:
        if original['parent'] >= 0:
            doc['nodes'][original['parent']].setdefault('children', []).append(original['index'])
    assert metadata['renderer_node'] == 2 and metadata['bone_nodes'] == [1]
    doc['nodes'][2].update(mesh=0, skin=0)
    doc['skins'] = [dict(joints=[1], skeleton=1, inverseBindMatrices=bind_accessor)]
    doc['meshes'] = [dict(name=metadata['mesh_name'], primitives=[dict(attributes=attributes, indices=index_accessor, mode=4)])]
    doc['buffers'] = [dict(byteLength=len(binary))]
    doc['extras'] = dict(openrpg_intermediate=True, material_qualification_pending=True,
                         source_guid=metadata['guid'], source_file_id=metadata['file_id'],
                         native_capture_sha256=source.sha(capture / 'RESULT.json'),
                         basis='reflection X; matrices R*M*R; UV v=1-v; reversed triangle winding',
                         source_uv_zw_retained_at=str(capture / 'uv0.f32'),
                         source_capture=str(capture), gpu_verified=False, studio_verified=False)
    encoded = json.dumps(doc, separators=(',', ':'), allow_nan=False).encode('utf-8')
    encoded += b' ' * (-len(encoded) % 4)
    binary.extend(bytes(-len(binary) % 4))
    glb = struct.pack('<4sII', b'glTF', 2, 28 + len(encoded) + len(binary))
    glb += struct.pack('<I4s', len(encoded), b'JSON') + encoded
    glb += struct.pack('<I4s', len(binary), b'BIN\0') + binary
    assert all(source.sha(p) == expected for p, expected in pins.items())
    OUT.mkdir()
    output = OUT / 'SKM_MarketCloth_01.skin.geometry.glb'
    output.write_bytes(glb)
    receipt = dict(status='native_skin_geometry_converted_independent_reopen_pending',
                   glb=str(output), glb_sha256=source.sha(output), vertices=1024, joints=1, nodes=3,
                   clips=0, source_inputs=pins, material_qualification_pending=True,
                   vault_indexed=False, gpu_verified=False, studio_verified=False, full_import=False)
    (OUT / 'RESULT.v1.json').write_text(json.dumps(receipt, indent=2), 'utf-8')
    print(json.dumps({k: v for k, v in receipt.items() if k != 'source_inputs'}))


if __name__ == '__main__':
    main()
