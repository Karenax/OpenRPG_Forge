"""Verify packaged geometry against external pinned sources and all blob hashes."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import struct

W = Path('F:/OpenRPG_ImportWork')
O = W / 'gladiator-glb-independent-reopen-v443'
assert not O.exists()
def read(p):
    return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()
assembly_path = W / 'gladiator-autonomous-glb-v442/RESULT.v1.json'
scene_path = W / 'gladiator-scene-geometry-package-v441/RESULT.v1.json'
contract_path = W / 'gladiator-render-contracts-v432/RESULT.v1.json'
assembly, scene_proof, contracts = map(read, [assembly_path, scene_path, contract_path])
pins = {str(p):sha(p) for p in [assembly_path, scene_path, contract_path, Path(__file__)]}
scenes = {r['prefab_guid']:r for r in scene_proof['rows']}
contracts = {r['material_guid']:r for r in contracts['rows']}
rows = []
for row in assembly['rows']:
    path = Path(row['glb'])
    assert sha(path) == row['sha256']
    pins[str(path)] = row['sha256']
    raw = path.read_bytes()
    assert struct.unpack_from('<III',raw) == (0x46546c67,2,len(raw))
    n, tag = struct.unpack_from('<II',raw,12)
    assert tag == 0x4e4f534a
    doc = json.loads(raw[20:20+n])
    size, tag = struct.unpack_from('<II',raw,20+n)
    assert tag == 0x004e4942 and size == len(raw)-28-n
    binary = memoryview(raw)[28+n:]
    assert 0 <= len(binary)-doc['buffers'][0]['byteLength'] < 4
    views = []
    for v in doc['bufferViews']:
        at, size = v.get('byteOffset',0), v['byteLength']
        assert v['buffer']==0 and at%4==0 and size>0 and at+size<=len(binary)
        views.append(binary[at:at+size])
    blobs = 0
    def blob(b):
        nonlocal_placeholder = None
        data = views[b['buffer_view']]
        assert hashlib.sha256(data).hexdigest()==b['sha256']
        return data
    def walk(x):
        global blobs
        if isinstance(x,dict):
            if 'buffer_view' in x and 'sha256' in x:
                blob(x)
                blobs += 1
            for v in x.values(): walk(v)
        elif isinstance(x,list):
            for v in x: walk(v)
    walk(doc)
    scene = scenes[row['prefab_guid']]
    assert doc['extensionsRequired']==['OPENRPG_source_graph']
    assert doc['extras']['original_lod_groups']==scene['original_lod_groups']
    assert doc['extras']['source_cards']==scene['cards']
    assert doc['extras']['source_prefab_sha256']==scene['source_prefab_sha256']
    assert bytes(blob(doc['extras']['source_prefab']))==Path(scene['source_prefab']).read_bytes()
    assert len(doc['nodes'])==len(scene['nodes'])
    def accessor(i):
        a = doc['accessors'][i]
        assert a['componentType'] in [5125,5126] and a.get('byteOffset',0)==0
        width = {'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']]
        data = views[a['bufferView']]
        assert len(data)==a['count']*width*4
        return data
    materials = {}
    mip_count = 0
    for i, mat in enumerate(doc['materials']):
        guid = mat['extras']['source_guid']
        c = contracts[guid]
        assert mat['extras']['original_render_contract']==c
        wire = mat['extensions']['OPENRPG_source_graph']
        assert wire['source_material_sha256']==c['original_material_sha256']
        assert wire['state']['alpha_to_coverage']==bool(c['native_resolved_state']['_AlphaCutoffEnable'])
        assert wire['state']['depth_compare']==c['native_resolved_state']['_ZTestDepthEqualForOpaque']
        assert wire['state']['render_queue']==c['native_render_queue']
        for t in wire['textures']:
            storage = t['imported_storage']
            data = blob(storage['storage'])
            capture = json.loads(bytes(blob(storage['capture'])))
            r = capture['rows'][storage['capture_case_index']]
            assert r['guid']==storage['native_capture']['source_guid']
            assert len(data)==r['whole_bytes']
            assert len(storage['mips'])==r['mip_count']
            for mip in storage['mips']:
                piece = data[mip['offset']:mip['offset']+mip['bytes']]
                assert hashlib.sha256(piece).hexdigest()==mip['sha256']
                mip_count += 1
        materials[i] = guid
    stream_instances, primitives = 0, 0
    paths = {e['source_node']['path']:i for i,e in enumerate(scene['nodes'])}
    for i, (node, expected) in enumerate(zip(doc['nodes'],scene['nodes'])):
        source = expected['source_node']
        assert node['matrix']==expected['target_local_matrix_column_major']
        assert node['extras']['unity_source_node']==source
        children = [j for p,j in paths.items() if p!='root' and p.rsplit('/',1)[0]==source['path']]
        assert node.get('children',[])==children
        if 'geometry' not in expected:
            assert 'mesh' not in node
            continue
        g = expected['geometry']
        mesh = doc['meshes'][node['mesh']]
        assert mesh['extras']['original_mesh_identity']==g['identity']
        assert len(mesh['primitives'])==len(g['primitives'])
        for j,p in enumerate(mesh['primitives']):
            assert materials[p['material']]==g['materials'][j]['guid']
            assert bytes(accessor(p['indices']))==Path(g['primitives'][j]['index_path']).read_bytes()
            primitives += 1
            for semantic,d in g['attributes'].items():
                assert bytes(accessor(p['attributes'][semantic]))==Path(d['path']).read_bytes()
                stream_instances += 1
            for semantic,custom in [('NORMAL','_OPENRPG_SOURCE_NORMAL'),('TANGENT','_OPENRPG_SOURCE_TANGENT'),
                                    ('TEXCOORD_0','_OPENRPG_SOURCE_UV0'),('TEXCOORD_1','_OPENRPG_SOURCE_UV1')]:
                d = g['original_attributes'][semantic]
                packed = bytes(accessor(p['attributes'][custom]))
                original = Path(d['path']).read_bytes()
                if semantic.startswith('TEXCOORD'):
                    assert packed==b''.join(original[x:x+8]+bytes(8) for x in range(0,len(original),8))
                else:
                    assert packed==original
                stream_instances += 1
    assert doc['scenes'][doc['scene']]['nodes']==[paths['root']]
    # Negative hash control against a real emitted forward payload.
    b = doc['materials'][0]['extensions']['OPENRPG_source_graph']['forward']
    changed = bytearray(blob(b)); changed[0] ^= 1
    assert hashlib.sha256(changed).hexdigest()!=b['sha256']
    rows.append(dict(prefab_guid=row['prefab_guid'], glb=row['glb'], sha256=row['sha256'],
        embedded_blobs_verified=blobs, mip_instances_verified=mip_count,
        primitives_verified=primitives, attribute_instances_verified=stream_instances,
        hierarchy_and_lod_metadata_verified=True, changed_forward_payload_detected=True))
    del binary, views, raw
assert len(rows)==5 and sum(r['primitives_verified'] for r in rows)==41
O.mkdir()
result = dict(status='five_autonomous_glbs_independently_reopened_against_pinned_sources',
    observed_utc=datetime.now(timezone.utc).isoformat(), rows=rows, input_pins=pins,
    canonical_rust_reader_accepted=False, gpu_rendered=False, studio_verified=False,
    full_import=False, complete_asset_conversion_increment=0)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note = '''## Gladiator — 5 GLB autonomes créés et réouverts, 10 octobre 2026

5 candidats GLB sur F: (1 145 996 568 octets) incorporent les 17 meshes, 41 primitives, hiérarchies et LOD sources, programmes des six matériaux, PNG et métadonnées d'origine, stockage natif complet des textures et captures Unity. Réouverture indépendante : attributs et indices comparés aux buffers épinglés, hashes des programmes et de chaque mip contrôlés.

Preuves : F:\\OpenRPG_ImportWork\\gladiator-glb-independent-reopen-v443\\RESULT.v1.json
Candidats : F:\\OpenRPG_ImportWork\\gladiator-autonomous-glb-v442
Restent admission par le lecteur Rust actuel, admission runtime des scènes/LOD/colliders, rendus comparés et validation Studio. Le Vault reste inchangé ; aucun nouvel asset complet qualifié.

'''
(O/'LOT.md').write_text(note,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p = W/'SUIVI_LOTS_HIVEMIND'/name
    before = p.read_bytes()
    (O/(name+'.before')).write_bytes(before)
    p.write_bytes(note.encode('utf-8')+before)
print(json.dumps(dict(status=result['status'],glbs=5,primitives=41)))
