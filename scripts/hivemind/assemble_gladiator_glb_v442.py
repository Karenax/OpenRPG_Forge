"""Create autonomous source-program GLBs; draw and scene admission remain pending."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import struct
from assemble_native_villa_delivery import pack_glb

W = Path('F:/OpenRPG_ImportWork')
O = W / 'gladiator-autonomous-glb-v442'
assert not O.exists()
def read(p):
    return json.loads(Path(p).read_text('utf-8-sig'))
def digest(raw):
    return hashlib.sha256(raw).hexdigest()
def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()
pins = {str(Path(__file__)): sha(Path(__file__)),
        str(Path(__file__).with_name('assemble_native_villa_delivery.py')): sha(Path(__file__).with_name('assemble_native_villa_delivery.py'))}
def load(rel):
    p = W / rel
    proof = read(p)
    pins[str(p)] = sha(p)
    for key in ['input_pins', 'output_pins']:
        for name, h in proof.get(key, {}).items():
            assert sha(name) == h, name
            pins[name] = h
    return proof
scenes = load('gladiator-scene-geometry-package-v441/RESULT.v1.json')
assert scenes['prefabs'] == 5 and scenes['meshes'] == 17
programs = {r['material_guid']: r for r in load('gladiator-native-lit-delivery-v440/RESULT.v1.json')['rows']}
contracts = {r['material_guid']: r for r in load('gladiator-render-contracts-v432/RESULT.v1.json')['rows']}
kernels = {r['material_guid']: r for r in load('gladiator-material-kernels-v419/RESULT.v1.json')['rows']}
stages = {r['material_guid']: r for r in load('gladiator-native-stages-v434/RESULT.v1.json')['rows']}
load('gladiator-all-texture-gpu-delivery-v427/RESULT.v1.json')
load('gladiator-native-texture-seal-v413/RESULT.v1.json')
capture_path = W / 'gladiator-native-texture-storage-v412/NATIVE-RESULT.v1.json'
capture = load('gladiator-native-texture-storage-v412/NATIVE-RESULT.v1.json')
texture_by_guid = {r['guid']: (i, r) for i, r in enumerate(capture['rows'])}
formats = {'RGBA_DXT1_SRGB': 'bc1_rgba_unorm_srgb', 'RGBA_DXT5_UNorm': 'bc3_rgba_unorm',
           'R8G8B8_UNorm': 'rgb8_unorm', 'R8G8B8A8_UNorm': 'rgba8_unorm'}
O.mkdir()
results = []
for scene in scenes['rows']:
    data = bytearray()
    views, accessors, materials, meshes, nodes = [], [], [], [], []
    embedded = {}
    def embed_raw(raw):
        h = digest(raw)
        if h not in embedded:
            data.extend(bytes(-len(data) % 4))
            embedded[h] = len(views)
            views.append(dict(buffer=0, byteOffset=len(data), byteLength=len(raw)))
            data.extend(raw)
        return dict(buffer_view=embedded[h], sha256=h)
    def embed(path, expected=None):
        raw = Path(path).read_bytes()
        h = digest(raw)
        assert expected is None or h == expected, path
        pins[str(path)] = h
        return embed_raw(raw)
    def accessor(raw, width, count, component=5126, bounds=False):
        assert len(raw) == width * count * 4
        blob = embed_raw(raw)
        a = dict(bufferView=blob['buffer_view'], componentType=component, count=count,
                 type={1:'SCALAR', 2:'VEC2', 3:'VEC3', 4:'VEC4'}[width])
        if bounds:
            values = list(struct.iter_unpack('<fff', raw))
            a.update(min=[min(v[i] for v in values) for i in range(3)], max=[max(v[i] for v in values) for i in range(3)])
        accessors.append(a)
        return len(accessors) - 1
    material_ids = sorted({slot['guid'] for n in scene['nodes'] if 'geometry' in n for slot in n['geometry']['materials']})
    for guid in material_ids:
        c, p, k, stage = contracts[guid], programs[guid], kernels[guid], stages[guid]
        textures, modes = [], [None] * 8
        for slot, binding in enumerate(k['source_texture_bindings']):
            ordinal, t = texture_by_guid[binding['guid']]
            assert not t['error'] and t['settings_restored_equal'] and t['gpu_samples_bit_exact']
            settings = json.loads(t['settings_before'])
            mipmaps = settings['mipmaps']
            sampler = dict(filter_mode=t['filter_mode'], wrap=[t['wrap_u'], t['wrap_v'], t['wrap_w']],
                           anisotropy=t['aniso'], mip_bias=t['mip_bias'], source_mipmaps_enabled=bool(mipmaps['m_EnableMipMap']))
            raw = Path(t['whole_file']).read_bytes()
            mips, offset = [], 0
            for m in t['mips']:
                piece = Path(m['file']).read_bytes()
                assert len(piece) == m['bytes'] and raw[offset:offset+len(piece)] == piece
                pins[m['file']] = digest(piece)
                mips.append(dict(width=m['width'], height=m['height'], offset=offset, bytes=len(piece), sha256=digest(piece)))
                offset += len(piece)
            assert offset == len(raw) == t['whole_bytes']
            descriptor = dict(schema_version=2,
                native_capture=dict(protocol='unity_isolated_readable_native_mips.v1', source_guid=t['guid']),
                storage=embed(t['whole_file']), capture=embed(capture_path), capture_case_index=ordinal,
                width=t['width'], height=t['height'], format=formats[t['graphics_format']], mips=mips, sampler=sampler)
            png = next(f for f in binding['source_files'] if f['path'].endswith('.png'))
            meta = next(f for f in binding['source_files'] if f['path'].endswith('.meta'))
            textures.append(dict(png=embed(png['path'], png['sha256']), source_meta=embed(meta['path'], meta['sha256']),
                source_meta_sha256=meta['sha256'], is_srgb=bool(mipmaps['m_sRGBTexture']),
                source_mipmaps=sampler['source_mipmaps_enabled'], filter_mode=sampler['filter_mode'], wrap=sampler['wrap'],
                anisotropy=sampler['anisotropy'], mip_bias=sampler['mip_bias'], imported_storage=descriptor))
            if binding['requires_normal_decode']:
                assert t['graphics_format'] == 'RGBA_DXT5_UNorm'
                modes[slot] = 'rg_or_ag'
        assert len(textures) == stage['texture_count']
        state = c['native_resolved_state']
        wire = dict(schema_version=4, source_program_sha256=stage['program_sha256'],
            source_material_sha256=c['original_material_sha256'], forward=embed(p['forward'], p['forward_sha256']),
            prepass=embed(p['prepass'], p['prepass_sha256']), libraries=[], textures=textures,
            state=dict(color_write_mask=15, color_blend=[int(state['_SrcBlend']), int(state['_DstBlend'])],
                alpha_blend=[int(state['_AlphaSrcBlend']), int(state['_AlphaDstBlend'])],
                forward_cull=int(state['_CullModeForward']), prepass_cull=int(state['_CullMode']),
                depth_compare=int(state['_ZTestDepthEqualForOpaque']), write_depth=state['_ZWrite']==1,
                transparent=c['source_keyword_transparent'], alpha_test=c['source_keyword_alpha_test'],
                alpha_to_coverage=p['original_alpha_to_mask'], render_queue=c['native_render_queue'],
                disabled_passes=c['source_disabled_shader_passes']),
            requires_original_vertex_color=False, normal_decode_modes=modes)
        materials.append(dict(name=c['name'], extensions={'OPENRPG_source_graph': wire},
            extras=dict(source_guid=guid, original_material=embed(c['original_material'], c['original_material_sha256']),
                original_graph=embed(c['original_graph'], c['original_graph_sha256']),
                source_program=embed(stage['program'], stage['program_sha256']),
                original_render_contract=c, actual_product_material_draw_qualified=False)))
    material_index = {guid: i for i, guid in enumerate(material_ids)}
    indices = {n['source_node']['path']: i for i, n in enumerate(scene['nodes'])}
    roots = []
    for entry in scene['nodes']:
        source = entry['source_node']
        n = dict(name=source['name'], matrix=entry['target_local_matrix_column_major'],
            extras=dict(unity_source_node=source, target_world_matrix=entry['target_world_matrix_column_major'],
                renderer_lod_collider_runtime_admission_pending=True))
        if 'geometry' in entry:
            g = entry['geometry']
            attrs = {}
            for semantic, desc in g['attributes'].items():
                raw = Path(desc['path']).read_bytes()
                assert digest(raw) == desc['sha256']
                attrs[semantic] = accessor(raw, desc['dimension'], desc['count'], bounds=semantic=='POSITION')
            for semantic in ['NORMAL', 'TANGENT', 'TEXCOORD_0', 'TEXCOORD_1']:
                desc = g['original_attributes'][semantic]
                raw = Path(desc['path']).read_bytes()
                assert digest(raw) == desc['sha256']
                width = desc['dimension']
                if semantic.startswith('TEXCOORD'):
                    assert width == 2
                    # Unity's dimension-2 UV attribute has no stored z/w lanes.
                    # The shader's Vector4 UV input reads those absent lanes as zero.
                    raw = b''.join(x + bytes(8) for x in [raw[i:i+8] for i in range(0, len(raw), 8)])
                    width = 4
                custom = {'NORMAL':'_OPENRPG_SOURCE_NORMAL', 'TANGENT':'_OPENRPG_SOURCE_TANGENT',
                          'TEXCOORD_0':'_OPENRPG_SOURCE_UV0', 'TEXCOORD_1':'_OPENRPG_SOURCE_UV1'}[semantic]
                attrs[custom] = accessor(raw, width, desc['count'])
            primitives = []
            for i, primitive in enumerate(g['primitives']):
                raw = Path(primitive['index_path']).read_bytes()
                primitives.append(dict(attributes=attrs.copy(), indices=accessor(raw, 1, primitive['indices'], 5125),
                    material=material_index[g['materials'][i]['guid']], mode=4))
            n['mesh'] = len(meshes)
            meshes.append(dict(name=source['name'], primitives=primitives, extras=dict(original_mesh_identity=g['identity'])))
        nodes.append(n)
        if source['path'] == 'root':
            roots.append(len(nodes)-1)
    assert len(roots) == 1
    for i, entry in enumerate(scene['nodes']):
        path = entry['source_node']['path']
        if path != 'root':
            parent = path.rsplit('/', 1)[0]
            nodes[indices[parent]].setdefault('children', []).append(i)
    doc = dict(asset=dict(version='2.0', generator='OpenRPG source-preserving Gladiator conversion'),
        extensionsUsed=['OPENRPG_source_graph'], extensionsRequired=['OPENRPG_source_graph'],
        buffers=[dict(byteLength=len(data))], bufferViews=views, accessors=accessors,
        materials=materials, meshes=meshes, nodes=nodes, scenes=[dict(nodes=roots)], scene=0,
        extras=dict(source_prefab=embed(scene['source_prefab'], scene['source_prefab_sha256']),
            source_prefab_sha256=scene['source_prefab_sha256'], prefab_guid=scene['prefab_guid'], source_cards=scene['cards'],
            original_lod_groups=scene['original_lod_groups'], source_frame_binding=dict(
                target_object_from_source_object=[1,0,0,0,0,1,0,0,0,0,-1,0,0,0,0,1],
                source_object_from_target_object=[1,0,0,0,0,1,0,0,0,0,-1,0,0,0,0,1]),
            original_uv_dimension_2_zero_extension=True, runtime_scene_admission_pending=True, full_import=False))
    # Embedding the original prefab above can append a buffer view.
    doc['buffers'][0]['byteLength'] = len(data)
    path = O / (scene['prefab_guid'] + '.candidate.glb')
    path.write_bytes(pack_glb(doc, bytes(data)))
    results.append(dict(prefab_guid=scene['prefab_guid'], glb=str(path), sha256=sha(path), bytes=path.stat().st_size,
        cards=scene['cards'], nodes=len(nodes), meshes=len(meshes), materials=len(materials),
        canonical_reader_accepted=False, gpu_rendered=False, studio_verified=False, full_import=False))
assert len(results) == 5 and sum(r['meshes'] for r in results) == 17
assert all(sha(p) == h for p, h in pins.items())
result = dict(status='five_autonomous_source_program_glb_candidates_created',
    observed_utc=datetime.now(timezone.utc).isoformat(), rows=results, input_pins=pins,
    required_extension='OPENRPG_source_graph', full_import=False, complete_asset_conversion_increment=0)
(O / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
print(json.dumps(dict(status=result['status'], glbs=5, meshes=17, bytes=sum(r['bytes'] for r in results))))
