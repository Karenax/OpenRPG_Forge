"""Bind raw captured source vectors; leave target geometry and all BIN bytes intact."""
import copy
import datetime
import hashlib
import json
import math
from pathlib import Path
import struct
import sys

from assemble_native_villa_delivery import digest, pack_glb, read, require, sha, split_glb

W = Path('F:/OpenRPG_ImportWork')
REFLECTION = [-1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]

def view_bytes(doc, binary, index):
    view = doc['bufferViews'][index]
    require(view['buffer'] == 0, 'External view refused')
    start = view.get('byteOffset', 0)
    result = binary[start:start + view['byteLength']]
    require(len(result) == view['byteLength'], 'View bounds')
    return result

def accessor_bytes(doc, binary, index, width):
    accessor = doc['accessors'][index]
    view = doc['bufferViews'][accessor['bufferView']]
    require(accessor['componentType'] == 5126 and accessor['type'] == {3: 'VEC3', 4: 'VEC4'}[width]
            and 'sparse' not in accessor and not accessor.get('normalized', False), 'Exact float32 stream required')
    data = view_bytes(doc, binary, accessor['bufferView'])
    offset = accessor.get('byteOffset', 0)
    stride = view.get('byteStride', width * 4)
    result = b''.join(data[offset + i * stride:offset + i * stride + width * 4] for i in range(accessor['count']))
    require(len(result) == accessor['count'] * width * 4, 'Accessor bounds')
    return result

def reflected(raw, width, components):
    result = bytearray(raw)
    require(len(raw) % (width * 4) == 0, 'Vector layout')
    require(all(math.isfinite(x[0]) for x in struct.iter_unpack('<f', raw)), 'Nonfinite captured vector')
    for offset in range(0, len(raw), width * 4):
        for component in components:
            at = offset + component * 4
            value = struct.unpack_from('<I', raw, at)[0] ^ 0x80000000
            struct.pack_into('<I', result, at, value)
    return bytes(result)

def convert(row, root):
    path = Path(row['glb'])
    raw = path.read_bytes()
    require(digest(raw) == row['sha256'], 'Storage-bound source changed')
    original, binary = split_glb(raw)
    doc = copy.deepcopy(original)
    evidence = doc['extras']['unity_captured_vertices']
    require(evidence['position_basis'] == 'Unity mesh local reflected X to Forge right handed; meters; identity mesh child frame', 'Unqualified native position basis')
    require('source_frame_binding' not in doc['extras'], 'Prior binding must not be overwritten')
    capture = evidence['capture_buffers']
    vectors = {}
    for name, width in [('positions.f32', 3), ('normals.f32', 3), ('tangents.f32', 4)]:
        item = capture[name]
        data = view_bytes(doc, binary, item['buffer_view'])
        require(len(data) == item['bytes'] and digest(data) == item['sha256'], 'Original embedded capture changed')
        reflected(data, width, [])
        vectors[name] = data
    count = len(vectors['positions.f32']) // 12
    require(count > 0 and len(vectors['normals.f32']) == count * 12 and len(vectors['tangents.f32']) == count * 16, 'Native source frame coverage')
    new_accessors = {}
    for name, width in [('normals.f32', 3), ('tangents.f32', 4)]:
        new_accessors[name] = len(doc['accessors'])
        doc['accessors'].append({'bufferView': capture[name]['buffer_view'], 'componentType': 5126,
            'count': count, 'type': {3: 'VEC3', 4: 'VEC4'}[width]})
    edits = []
    expected_position = reflected(vectors['positions.f32'], 3, [0])
    expected_normal = reflected(vectors['normals.f32'], 3, [0])
    expected_tangent = reflected(vectors['tangents.f32'], 4, [0])
    source_normal_before = expected_normal
    source_tangent_before = reflected(vectors['tangents.f32'], 4, [0, 3])
    for mesh_index, mesh in enumerate(doc['meshes']):
        for primitive_index, primitive in enumerate(mesh['primitives']):
            material = doc['materials'][primitive['material']]
            if 'OPENRPG_source_graph' not in material.get('extensions', {}):
                continue
            attributes = primitive['attributes']
            require(accessor_bytes(doc, binary, attributes['POSITION'], 3) == expected_position, 'Target positions differ from native reflection')
            require(accessor_bytes(doc, binary, attributes['NORMAL'], 3) == expected_normal, 'Target normals differ from native reflection')
            require(accessor_bytes(doc, binary, attributes['TANGENT'], 4) == expected_tangent, 'Standard tangent basis differs from native capture')
            require(accessor_bytes(doc, binary, attributes['_OPENRPG_SOURCE_NORMAL'], 3) == source_normal_before,
                    'Unexpected previous dedicated normal basis')
            require(accessor_bytes(doc, binary, attributes['_OPENRPG_SOURCE_TANGENT'], 4) == source_tangent_before,
                    'Unexpected previous dedicated tangent basis')
            for attribute, name in [('_OPENRPG_SOURCE_NORMAL', 'normals.f32'), ('_OPENRPG_SOURCE_TANGENT', 'tangents.f32')]:
                edits.append({'mesh': mesh_index, 'primitive': primitive_index, 'attribute': attribute,
                    'before': attributes[attribute], 'after': new_accessors[name]})
                attributes[attribute] = new_accessors[name]
    require(edits, 'No source primitive bound')
    binding = {'schema': 'openrpg.source-frame-binding.v1',
        'target_object_from_source_object': REFLECTION,
        'source_object_from_target_object': REFLECTION,
        'position_basis': evidence['position_basis'],
        'dedicated_normal_and_tangent_basis': 'Unreflected original Unity mesh local capture; original tangent W and UV preserved',
        'source_normal_sha256': capture['normals.f32']['sha256'],
        'source_tangent_sha256': capture['tangents.f32']['sha256'],
        'target_positions_normals_standard_tangents_bitwise_preserved': True,
        'object_basis_from_capture_verified': True, 'world_basis_qualified': False,
        'gpu_rendered': False, 'studio_verified': False}
    doc['extras']['source_frame_binding'] = binding
    folder = root / 'jobs' / row['asset_id']
    folder.mkdir(parents=True, exist_ok=False)
    target = folder / 'model.glb'
    target.write_bytes(pack_glb(doc, binary))
    reopened, new_binary = split_glb(target.read_bytes())
    require(new_binary == binary, 'BIN changed during frame binding')
    for edit in edits:
        width = 3 if edit['attribute'].endswith('NORMAL') else 4
        name = 'normals.f32' if width == 3 else 'tangents.f32'
        attributes = reopened['meshes'][edit['mesh']]['primitives'][edit['primitive']]['attributes']
        require(accessor_bytes(reopened, new_binary, attributes[edit['attribute']], width) == vectors[name], 'Dedicated source vectors not exact original bits')
        attributes[edit['attribute']] = edit['before']
    reopened['accessors'] = reopened['accessors'][:len(original['accessors'])]
    reopened['extras'].pop('source_frame_binding')
    require(reopened == original, 'Unrelated declarations changed')
    return {'asset_id': row['asset_id'], 'glb': str(target), 'sha256': sha(target),
        'source_glb': str(path), 'source_glb_sha256': row['sha256'],
        'unchanged_bin_sha256': digest(binary), 'dedicated_source_attribute_edits': edits,
        'original_source_vectors_reopened_bitwise_equal': True, 'target_geometry_unchanged': True,
        'programs_and_textures_unchanged': True, 'object_basis': binding,
        'canonical_reader_accepted': False, 'runtime_qualified': False, 'full_import': False}

def main(batch_id):
    source = W / ('villa-native-scene-delivery-' + batch_id.lower().replace('.', '-') + '-v2/RUN-RESULT.v1.json')
    root = W / ('villa-native-source-frame-bound-' + batch_id.lower().replace('.', '-') + '-v1')
    require(not root.exists(), 'Existing frame evidence preserved')
    receipt = read(source)
    require(receipt['status'] == 'native_glb_storage_and_normal_bindings_reopened'
            and receipt['verified_derivatives'] == receipt['expected_scenes'], 'Closed assembled batch required')
    root.mkdir()
    state = {'schema': 'openrpg.native-source-frame-binding-batch.v1', 'batch_id': batch_id,
        'status': 'binding_raw_captured_source_vectors', 'rows': [], 'source_receipt': str(source),
        'source_receipt_sha256': sha(source), 'helper_sha256': sha(Path(__file__).resolve()),
        'started_utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
        'expected_scenes': receipt['expected_scenes'], 'full_import': False}
    try:
        for row in receipt['rows']:
            state['rows'].append(convert(row, root))
            (root / 'RUN-RESULT.v1.json').write_text(json.dumps(state, indent=2), 'utf-8')
        require(sha(source) == state['source_receipt_sha256'], 'Assembly receipt changed')
        state.update(status='native_original_source_frames_bound_and_reopened', verified_scenes=len(state['rows']))
        (root / 'INPUTS.v1.json').write_text(json.dumps([{k: r[k] for k in ('asset_id', 'glb', 'sha256')} for r in state['rows']], indent=2), 'utf-8')
    except Exception as error:
        state.update(status='refused', error=str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    (root / 'RUN-RESULT.v1.json').write_text(json.dumps(state, indent=2), 'utf-8')
    print(json.dumps({k: v for k, v in state.items() if k != 'rows'}))
    if state['status'] == 'refused':
        raise SystemExit(1)

if __name__ == '__main__':
    main(sys.argv[1])
