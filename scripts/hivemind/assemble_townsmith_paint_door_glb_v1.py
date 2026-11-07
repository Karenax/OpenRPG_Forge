"""Retain three door LOD meshes and hidden index streams with one complete source material."""
from pathlib import Path
from datetime import datetime, timezone
import copy
import hashlib
import json
import struct
import subprocess
import sys

S = Path(__file__).parent
R = S.parents[1]
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'townsmith-paint-door-glb-v1'


def read(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))


def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()


def main():
    assert not ROOT.exists()
    packet_path = W / 'townsmith-s-paint-material-packet-v1/MATERIAL-PACKET.v1.json'
    recipes_path = W / 'townsmith-verified-door-draw-recipes-v1/DRAW_BINDINGS.v1.json'
    packet = read(packet_path)
    recipes = read(recipes_path)
    selected = {}
    for binding in recipes['bindings']:
        assert binding['original_material_slots'][0]['guid'] == 'abc00000000009486088228754689894'
        identity = binding['source_mesh_identity']['file_id']
        selected.setdefault(identity, binding)
    assert len(selected) == 3 and '4300000' in selected
    ROOT.mkdir()
    pins = {str(p): sha(p) for p in (Path(__file__), packet_path, recipes_path)}
    data = bytearray()
    views, accessors, embedded = [], [], {}
    expected_views = []

    def embed(path, expected=None):
        path = Path(path)
        raw = path.read_bytes()
        digest = hashlib.sha256(raw).hexdigest()
        assert expected is None or digest == expected, path
        pins[str(path)] = digest
        if digest in embedded:
            index = embedded[digest]
            view = views[index]
            assert data[view['byteOffset']:view['byteOffset'] + view['byteLength']] == raw
            return {'buffer_view': index, 'sha256': digest}
        data.extend(bytes(-len(data) % 4))
        index = len(views)
        embedded[digest] = index
        views.append({'buffer': 0, 'byteOffset': len(data), 'byteLength': len(raw)})
        expected_views.append({'index': index, 'file': str(path), 'sha256': digest, 'bytes': len(raw)})
        data.extend(raw)
        return {'buffer_view': index, 'sha256': digest}

    # Preserve the packet's view indices; its shared views were already deduplicated.
    for index, view in enumerate(packet['buffer_views']):
        result = embed(view['file'], view['sha256'])
        assert result['buffer_view'] == index

    def accessor(path, width, count, component=5126, bounds=False):
        raw = Path(path).read_bytes()
        assert len(raw) == width * count * 4
        a = {'bufferView': embed(path)['buffer_view'], 'componentType': component, 'count': count,
             'type': {1: 'SCALAR', 2: 'VEC2', 3: 'VEC3', 4: 'VEC4'}[width]}
        if bounds:
            values = list(struct.iter_unpack('<fff', raw))
            a.update(min=[min(v[i] for v in values) for i in range(3)], max=[max(v[i] for v in values) for i in range(3)])
        accessors.append(a)
        return len(accessors) - 1

    meshes, nodes, rows = [], [], []
    for identity, binding in sorted(selected.items()):
        folder = Path(binding['geometry_derivative'])
        geometry_path = folder / 'GEOMETRY.v1.json'
        geometry = read(geometry_path)
        pins[str(geometry_path)] = sha(geometry_path)
        metadata = geometry['metadata']
        assert geometry['expected_identity']['file_id'] == identity
        assert metadata['vertices'] == metadata['normals_count'] == metadata['tangents_count'] == metadata['uv_counts'][0]
        assert metadata['blend_shapes'] == metadata['bind_poses'] == metadata['colors_count'] == 0
        originals = {Path(p).name: Path(p) for p in geometry['source_stream_pins']}
        for p, h in geometry['source_stream_pins'].items():
            assert sha(p) == h, p
            pins[p] = h
        for name, details in geometry['derived_streams'].items():
            assert sha(folder / name) == details['sha256'], name
            pins[str(folder / name)] = details['sha256']
        count = metadata['vertices']
        attrs = {'POSITION': accessor(folder / 'positions.target.f32', 3, count, bounds=True),
                 'NORMAL': accessor(folder / 'normals.target.f32', 3, count),
                 'TANGENT': accessor(folder / 'tangents.target.f32', 4, count),
                 '_OPENRPG_SOURCE_NORMAL': accessor(originals['normals.f32'], 3, count),
                 '_OPENRPG_SOURCE_TANGENT': accessor(originals['tangents.f32'], 4, count)}
        for channel, uv_count in enumerate(metadata['uv_counts']):
            if uv_count:
                assert uv_count == count
                attrs[f'TEXCOORD_{channel}'] = accessor(folder / f'uv{channel}.target.f32', 2, count)
                attrs[f'_OPENRPG_SOURCE_UV{channel}'] = accessor(originals[f'uv{channel}.f32'], 4, count)
        assert binding['visible_draw_recipe'] == [{'submesh': 0, 'source_material_slot': 0, 'indices_stream': str(folder / 'submesh-0.target.u32')}]
        indices_path = folder / 'submesh-0.target.u32'
        indices = accessor(indices_path, 1, indices_path.stat().st_size // 4, 5125)
        retained = []
        for submesh in binding['remaining_submeshes']:
            p = Path(submesh['indices_stream'])
            assert submesh['preserved'] and submesh['no_colour_draw_in_measured_context']
            retained.append({'source_submesh': submesh['submesh'], 'indices_accessor': accessor(p, 1, p.stat().st_size // 4, 5125),
                             'original_source_indices': embed(originals[f"submesh-{submesh['submesh']}.i32"]), 'shadow_policy_qualified': False})
        meshes.append({'name': metadata['mesh_name'], 'primitives': [{'attributes': attrs, 'indices': indices, 'material': 0, 'mode': 4}],
                       'extras': {'source_mesh_identity': binding['source_mesh_identity'], 'retained_submeshes_without_measured_colour_draw': retained,
                                  'original_source_visible_indices': embed(originals['submesh-0.i32']), 'source_geometry_receipt_sha256': sha(geometry_path)}})
        nodes.append({'name': metadata['mesh_name'], 'mesh': len(meshes) - 1})
        rows.append({'file_id': identity, 'mesh_index': len(meshes) - 1, 'vertices': count, 'visible_indices': indices_path.stat().st_size // 4,
                     'source_geometry': str(geometry_path), 'retained_submeshes': len(retained)})
    reflection = [-1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]
    doc = {'asset': {'version': '2.0', 'generator': 'OpenRPG original source graph door carrier'},
           'extensionsUsed': ['OPENRPG_source_graph'], 'extensionsRequired': ['OPENRPG_source_graph'],
           'buffers': [{'byteLength': len(data)}], 'bufferViews': views, 'accessors': accessors,
           'materials': [{'name': 'M_Trim-Wood2', 'extensions': {'OPENRPG_source_graph': copy.deepcopy(packet['contract'])}}],
           'meshes': meshes, 'nodes': nodes, 'scenes': [{'nodes': [0]}], 'scene': 0,
           'extras': {'source_frame_binding': {'target_object_from_source_object': reflection, 'source_object_from_target_object': reflection},
                      'source_renderer_recipes': recipes['bindings'], 'scene_selects_lod0_for_control_only': True,
                      'lod_routing_qualified': False, 'source_shadow_policy_qualified': False, 'full_prefab_assembled': False,
                      'source_render_state_receipt': packet['original_render_state_receipt'], 'full_import': False}}
    binary = bytes(data) + bytes(-len(data) % 4)
    raw_json = json.dumps(doc, separators=(',', ':'), allow_nan=False).encode('utf-8')
    raw_json += b' ' * (-len(raw_json) % 4)
    raw = struct.pack('<4sII', b'glTF', 2, 12 + 8 + len(raw_json) + 8 + len(binary)) + struct.pack('<I4s', len(raw_json), b'JSON') + raw_json + struct.pack('<I4s', len(binary), b'BIN\0') + binary
    glb = ROOT / 'SM_Door-Single-B.source-material.candidate.glb'
    glb.write_bytes(raw)
    expected = {'glb': str(glb), 'sha256': sha(glb), 'bytes': len(raw), 'meshes': rows, 'buffer_views': expected_views}
    (ROOT / 'EXPECTED.v1.json').write_text(json.dumps(expected, indent=2), encoding='utf-8')
    for name in ('source_graph_glb.rs', 'source_graph_attributes.rs'):
        source = R / 'crates/forge_assets/src' / name
        (ROOT / name).write_bytes(source.read_bytes())
        pins[str(source)] = sha(source)
        pins[str(ROOT / name)] = sha(ROOT / name)
    (ROOT / 'PREPARED.v1.json').write_text(json.dumps({'input_pins': pins, 'assembled_utc': datetime.now(timezone.utc).isoformat(),
          'full_import': False, 'forge_editor_verified': False, 'source_unity_render_parity_qualified': False}, indent=2), encoding='utf-8')
    helper = (S / 'check_townsmith_material_packet_v1.py').read_text(encoding='utf-8')
    helper = helper.replace('townsmith-s-paint-material-packet-check-v1', 'townsmith-paint-door-glb-check-v1')
    helper = helper.replace('mod source_graph_bundle;\n', 'mod source_graph_bundle;\n#[path = "../townsmith-paint-door-glb-v1/source_graph_glb.rs"]\nmod source_graph_glb;\n#[path = "../townsmith-paint-door-glb-v1/source_graph_attributes.rs"]\nmod source_graph_attributes;\n')
    marker = ' #[test] fn ten_actual_resource_chains_and_identity_sampler_refusals() {'
    extra = ''' #[test] fn actual_door_glb_reopens_complete_material_and_three_original_geometry_frames(){
  let expected:Value=serde_json::from_slice(&fs::read("F:/OpenRPG_ImportWork/townsmith-paint-door-glb-v1/EXPECTED.v1.json").unwrap()).unwrap();
  let raw=fs::read(expected["glb"].as_str().unwrap()).unwrap();assert_eq!(serde_json::to_value(bytes_sha256(&raw)).unwrap(),expected["sha256"]);
  let parsed=crate::source_graph_glb::read_source_graph_glb(&raw).unwrap().unwrap();assert_eq!(parsed.materials.len(),1);assert_eq!(parsed.materials[0].1.textures.len(),10);
  for view in expected["buffer_views"].as_array().unwrap(){let i=view["index"].as_u64().unwrap() as usize;assert_eq!(parsed.views[i],fs::read(view["file"].as_str().unwrap()).unwrap().as_slice());}
  for row in expected["meshes"].as_array().unwrap(){
   let geometry=crate::source_graph_attributes::read_source_graph_primitive_geometry(&parsed,row["mesh_index"].as_u64().unwrap() as usize,0).unwrap();
   assert_eq!(geometry.positions.len() as u64,row["vertices"].as_u64().unwrap());assert_eq!(geometry.indices.len() as u64,row["visible_indices"].as_u64().unwrap());
   assert!(geometry.source.original_tangents.is_some());assert!(geometry.source.original_additional_uvs[0].is_some());assert!(geometry.source.original_colors.is_none());
  }
 }
'''
    assert helper.count(marker) == 1
    helper = helper.replace(marker, extra + marker)
    target = S / 'check_townsmith_paint_door_glb_v1.py'
    assert not target.exists()
    target.write_text(helper, encoding='utf-8')
    subprocess.run([sys.executable, '-B', str(target)], check=True)
    print(json.dumps({'glb': str(glb), 'bytes': len(raw), 'meshes': len(meshes), 'textures': 10}))


if __name__ == '__main__':
    main()
